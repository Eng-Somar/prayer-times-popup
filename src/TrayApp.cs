using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

public class TrayApp : ApplicationContext
{
    [DllImport("psapi.dll")] static extern bool EmptyWorkingSet(IntPtr process);
    [DllImport("shell32.dll")] static extern int SHQueryUserNotificationState(out int state);

    Settings s;
    readonly NotifyIcon tray = new NotifyIcon();
    readonly Timer timer = new Timer();
    readonly Control invoker = new Control();
    readonly HashSet<string> fired = new HashSet<string>();
    readonly List<DueEvent> snoozes = new List<DueEvent>();
    PopupForm popup;
    SettingsForm settingsForm;
    TasbihForm tasbihForm;
    AdhkarForm adhkarForm;
    QiblaForm qiblaForm;
    StatsForm statsForm;
    PrayerStats stats;
    bool statsDirty;
    SoundPlayer player;
    DateTime cacheDate = DateTime.MinValue, quietUntil = DateTime.MinValue;
    DateTime[][] days;
    GeoResult pendingLocation;
    Action balloonAction;
    bool detecting;

    // Hidden top-level window: receives the "open settings" broadcast from a second launch.
    class MsgWindow : NativeWindow
    {
        readonly Action onOpen;
        public MsgWindow(Action onOpen) { this.onOpen = onOpen; CreateHandle(new CreateParams()); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.OpenSettingsMsg) onOpen();
            base.WndProc(ref m);
        }
    }

    readonly MsgWindow msgWindow;

    public TrayApp(string[] args)
    {
        s = Settings.Load();
        stats = PrayerStats.Load();
        Lang.Ar = s.Language == "ar";
        invoker.CreateControl();
        IntPtr unused = invoker.Handle;
        msgWindow = new MsgWindow(OpenSettings);

        tray.Icon = IconArt.TrayIcon(SystemInformation.SmallIconSize.Width);
        tray.Visible = true;
        tray.MouseClick += (o, e) => { if (e.Button == MouseButtons.Left) ShowNext(); };
        tray.BalloonTipClicked += (o, e) => { Action a = balloonAction; balloonAction = null; if (a != null) a(); };
        BuildMenu();

        timer.Interval = 10000;
        timer.Tick += (o, e) => Tick();
        timer.Start();

        SystemEvents.PowerModeChanged += OnPowerChanged;
        SystemEvents.TimeChanged += OnTimeChanged;

        bool demo = false;
        foreach (string a in args) demo |= RunDemoArg(a);
        if (!demo && s.HasLoc)
        {
            Tick();
            Delay(1500, () => { if (!PopupOpen() && !Deferred()) ShowNext(); });
        }
        else if (!demo) UpdateTrayText();

        if (!s.HasLoc || s.LocMode == "auto") Detect(false);
        Delay(20000, Trim);
    }

    // Opens one window directly. Used by the launch options in the README and for checking the UI.
    bool RunDemoArg(string a)
    {
        switch (a)
        {
            case "--settings": OpenSettings(); return true;
            case "--preview": Preview(s); return true;
            case "--tasbih": ToggleTasbih(); return true;
            case "--qibla": OpenQibla(); return true;
            case "--adhkar": OpenAdhkar(AdhkarSet.Morning); return true;
            case "--stats": OpenStats(); return true;
            case "--notice": ShowEvent(new DueEvent { Kind = EventKind.AdhkarMorning, Index = 0, Time = DateTime.Now, Day = Times(s, DateTime.Now.Date) }, false); return true;
            default: return false;
        }
    }

    void OnPowerChanged(object o, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) invoker.BeginInvoke((Action)Refresh);
    }

    void OnTimeChanged(object o, EventArgs e)
    {
        invoker.BeginInvoke((Action)Refresh);
    }

    void Refresh()
    {
        TimeZoneInfo.ClearCachedData(); // otherwise a time zone change is ignored until the app restarts
        cacheDate = DateTime.MinValue;
        Tick();
    }

    void Delay(int ms, Action a)
    {
        var t = new Timer { Interval = ms };
        t.Tick += (o, e) => { t.Stop(); t.Dispose(); a(); };
        t.Start();
    }

    // ---------- schedule ----------

    DateTime[] Times(Settings st, DateTime date)
    {
        double lat = st.HasLoc ? st.Lat : 21.4225, lon = st.HasLoc ? st.Lon : 39.8262; // Makkah as preview fallback
        DateTime[] raw = PrayCalc.Compute(date, lat, lon, PrayCalc.Resolve(st.Method, st.Country), st.Asr == 2 ? 2 : 1, st.HighLat, st.HijriAdjust);
        return PrayCalc.ApplyOffsets(raw, st.Offsets);
    }

    void EnsureDays(DateTime now)
    {
        if (now.Date == cacheDate) return;
        days = new[] { Times(s, now.Date.AddDays(-1)), Times(s, now.Date), Times(s, now.Date.AddDays(1)) };
        cacheDate = now.Date;
        Prune(now);
        // A prayer marked before a restart stays marked, so it doesn't remind you again.
        foreach (DateTime[] day in days)
            foreach (int i in Scheduler.Five)
                if (day[i] != DateTime.MinValue && stats.IsPrayed(Scheduler.OwnerDay(day), i))
                    fired.Add(Scheduler.PrayedKey(day[i], i));
    }

    // Keys start with yyyyMMdd, so anything older than yesterday can go.
    void Prune(DateTime now)
    {
        string keep = now.Date.AddDays(-1).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        fired.RemoveWhere(k => string.CompareOrdinal(k, 0, keep, 0, 8) < 0);
        snoozes.RemoveAll(z => z.Until < now.AddHours(-1));
    }

    bool PopupOpen() { return popup != null && !popup.IsDisposed; }

    bool Deferred()
    {
        int state = 5;
        try { SHQueryUserNotificationState(out state); }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        return Quiet.Defer(state, s.DndFullscreen, DateTime.Now, quietUntil);
    }

    void FlushStats()
    {
        if (!statsDirty) return;
        statsDirty = false;
        stats.Save();
    }

    void Tick()
    {
        FlushStats(); // tasbih taps are saved in batches rather than on every tap
        UpdateTrayText();
        if (!s.HasLoc) return;
        DateTime now = DateTime.Now;
        EnsureDays(now);
        // Nothing is marked as fired while deferred, so it appears when you're free, if its window is still open.
        if (Deferred()) return;

        ScheduleOptions o = s.ToSchedule();
        o.Snoozes = snoozes;
        List<DueEvent> due = Scheduler.Due(now, days, o, fired);
        if (due.Count == 0) return;
        DueEvent e = due[0];
        // A prayer time replaces whatever is showing. Anything else waits its turn.
        if (PopupOpen() && e.Kind != EventKind.At) return;
        fired.Add(e.Key);
        ShowEvent(e, true);
    }

    void ShowEvent(DueEvent e, bool live)
    {
        bool ar = s.Language == "ar";
        switch (e.Kind)
        {
            case EventKind.At:
                PopupInfo at = Make(s, PopupKind.At, e.Index, e.Time, e.Day);
                at.Late = e.Late;
                if (s.Iqama[e.Index] > 0) at.IqamaAt = e.Time.AddMinutes(s.Iqama[e.Index]);
                if (live) AddPrayerActions(at, e);
                Show(s, at, s.Duration);
                if (live && !e.Late && s.SoundBy[e.Index]) PlaySound();
                break;
            case EventKind.Before:
                Show(s, Make(s, PopupKind.Before, e.Index, e.Time, e.Day), s.Duration == 0 ? 0 : Math.Min(s.Duration, 20));
                break;
            case EventKind.Iqama:
                PopupInfo iq = Make(s, PopupKind.Iqama, e.Index, e.Time, e.Day);
                iq.Actions.Add(PrayedAction(e, ar));
                Show(s, iq, Math.Min(s.Duration == 0 ? 30 : s.Duration, 30));
                break;
            case EventKind.Imsak:
                Show(s, Make(s, PopupKind.Imsak, 0, e.Time, e.Day), Math.Min(s.Duration == 0 ? 30 : s.Duration, 30));
                break;
            case EventKind.AdhkarMorning: Notice(AdhkarSet.Morning); break;
            case EventKind.AdhkarEvening: Notice(AdhkarSet.Evening); break;
            case EventKind.AdhkarAfter: Notice(AdhkarSet.AfterPrayer); break;
            case EventKind.AdhkarSleep: Notice(AdhkarSet.Sleep); break;
            case EventKind.FastNotice:
                DayOfWeek tomorrow = DateTime.Now.AddDays(1).DayOfWeek;
                string key = e.Reason == "white" ? "fastWhite" : tomorrow == DayOfWeek.Monday ? "fastMon" : "fastThu";
                PopupInfo fast = NoticeInfo(Lang.T("fastTitle", ar), Lang.T(key, ar));
                Show(s, fast, 20);
                break;
        }
    }

    void AddPrayerActions(PopupInfo info, DueEvent e)
    {
        bool ar = s.Language == "ar";
        info.Actions.Add(PrayedAction(e, ar));
        info.Actions.Add(new PopupAction { Label = Lang.T("later", ar), Run = () => Snooze(e) });
    }

    PopupAction PrayedAction(DueEvent e, bool ar)
    {
        return new PopupAction
        {
            Label = Lang.T("prayed", ar), Primary = true, Confirm = Lang.T("accepted", ar),
            Run = () => MarkPrayed(e.Index, e.Day),
            Then = () => AfterPrayed(e.Time, e.Index),
        };
    }

    // Every option from the tray menu, one click away on the prayer times card.
    void AddTools(PopupInfo info, bool ar)
    {
        info.Tools.Add(new PopupTool { Icon = ToolIcon.Tasbih, Label = Lang.T("tasbih", ar), Run = ShowTasbih });
        info.Tools.Add(new PopupTool { Icon = ToolIcon.Adhkar, Label = Lang.T("adhkar", ar), Run = OpenSuggestedAdhkar });
        info.Tools.Add(new PopupTool { Icon = ToolIcon.Qibla, Label = Lang.T("toolQibla", ar), Run = OpenQibla });
        info.Tools.Add(new PopupTool { Icon = ToolIcon.Stats, Label = Lang.T("toolStats", ar), Run = OpenStats });
        info.Tools.Add(new PopupTool
        {
            Icon = ToolIcon.Quiet, Label = Lang.T("toolQuiet", ar), KeepOpen = true,
            Run = () => SetQuiet(DateTime.Now < quietUntil ? 0 : 60, false),
            LiveIcon = () => DateTime.Now < quietUntil ? ToolIcon.QuietOn : ToolIcon.Quiet,
        });
        info.Tools.Add(new PopupTool { Icon = ToolIcon.Settings, Label = Lang.T("settings", ar), Run = OpenSettings });
        info.FooterNote = () => DateTime.Now < quietUntil
            ? string.Format(Lang.T("quietTill", ar), Lang.Time(quietUntil, s.Use24h, ar))
            : null;
    }

    void Notice(AdhkarSet set)
    {
        bool ar = s.Language == "ar";
        string titleKey = Adhkar.TitleKey(set);
        PopupInfo info = NoticeInfo(Lang.T(titleKey, ar), Lang.T("adhkarTime", ar));
        info.Actions.Add(new PopupAction { Label = Lang.T("read", ar), Primary = true, Run = () => OpenAdhkar(set) });
        Show(s, info, Math.Min(s.Duration == 0 ? 30 : s.Duration, 30));
    }

    PopupInfo NoticeInfo(string title, string status)
    {
        DateTime now = DateTime.Now;
        return new PopupInfo { Kind = PopupKind.Notice, Time = now, Day = new DateTime[6], Title = title, Status = status, HijriText = HijriNow(s, now) };
    }

    string HijriNow(Settings st, DateTime t)
    {
        DateTime[] day = Times(st, t.Date);
        return Lang.Hijri(t, st.Language == "ar", st.HijriAdjust, Hijri.HijriDay(t, day[4]));
    }

    void MarkPrayed(int index, DateTime[] day)
    {
        SetPrayed(Scheduler.OwnerDay(day), index, true);
        if (PopupOpen()) popup.ShowPrayed(index, true);
    }

    void AfterPrayed(DateTime t, int index)
    {
        if (!s.AdhkarAfter) return;
        fired.Add(Scheduler.Key(EventKind.AdhkarAfter, t, index));
        Notice(AdhkarSet.AfterPrayer);
    }

    // The single place a prayer's mark changes, from the card or from the statistics table.
    void SetPrayed(DateTime day, int index, bool on)
    {
        stats.SetPrayed(day, index, on);
        stats.Save();
        DateTime t = Times(s, day)[index];
        if (t != DateTime.MinValue)
        {
            string key = Scheduler.PrayedKey(t, index);
            if (on) fired.Add(key); else fired.Remove(key);
            if (on) snoozes.RemoveAll(z => z.Time == t && z.Index == index);
        }
        ReloadStats();
    }

    void ReloadStats()
    {
        if (Open(statsForm)) statsForm.Reload();
    }

    void Snooze(DueEvent e)
    {
        snoozes.RemoveAll(z => z.Time == e.Time && z.Index == e.Index);
        snoozes.Add(new DueEvent { Kind = EventKind.At, Index = e.Index, Time = e.Time, Day = e.Day, Key = "Z" + Scheduler.Key(EventKind.At, e.Time, e.Index), Until = DateTime.Now.AddMinutes(5) });
    }

    PopupInfo NextInfo(Settings st, PopupKind kind)
    {
        DateTime now = DateTime.Now;
        DateTime[][] all = { Times(st, now.Date.AddDays(-1)), Times(st, now.Date), Times(st, now.Date.AddDays(1)), Times(st, now.Date.AddDays(2)) };
        DateTime best = DateTime.MaxValue;
        DateTime[] bestDay = null;
        int bestIndex = -1;
        foreach (DateTime[] day in all)
            foreach (int i in Scheduler.Five)
                if (day[i] > now && day[i] < best) { best = day[i]; bestDay = day; bestIndex = i; }
        if (bestIndex < 0) return null; // no prayer time occurs at this latitude right now
        return Make(st, kind, bestIndex, best, bestDay);
    }

    PopupInfo Make(Settings st, PopupKind kind, int idx, DateTime t, DateTime[] day)
    {
        bool ar = st.Language == "ar";
        string place = st.HasLoc
            ? (string.IsNullOrEmpty(st.City)
                ? st.Lat.ToString("0.00", CultureInfo.InvariantCulture) + ", " + st.Lon.ToString("0.00", CultureInfo.InvariantCulture)
                : st.City)
            : Lang.T("noLoc", ar);
        return new PopupInfo
        {
            Kind = kind, Index = idx, Time = t, Day = day,
            Footer = place + "  ·  " + PrayCalc.Resolve(st.Method, st.Country).Name(ar),
            HijriText = Lang.Hijri(t, ar, st.HijriAdjust, Hijri.HijriDay(t, day[4])),
            Iftar = idx == 4 && Hijri.IsRamadan(t, st.HijriAdjust),
            PrayedMask = stats.Mask(Scheduler.OwnerDay(day)),
            SetPrayed = (i, on) => SetPrayed(Scheduler.OwnerDay(day), i, on),
        };
    }

    void Show(Settings st, PopupInfo info, int seconds)
    {
        if (PopupOpen()) popup.Close();
        if (info.Kind != PopupKind.Notice) AddTools(info, st.Language == "ar");
        var form = new PopupForm(st, info, seconds);
        popup = form;
        form.FormClosed += (o, e) => Delay(1000, Trim);
        form.Pop();
    }

    public void ShowNext()
    {
        if (!s.HasLoc) { OpenSettings(); return; }
        if (PopupOpen() && popup.IsNextCard) { popup.Dismiss(); return; } // clicking the tray icon again closes it
        PopupInfo info = NextInfo(s, PopupKind.Next);
        if (info != null) Show(s, info, 20);
    }

    public void Preview(Settings st)
    {
        PopupInfo info = NextInfo(st, PopupKind.At);
        if (info == null) return;
        bool ar = st.Language == "ar";
        info.Actions.Add(new PopupAction { Label = Lang.T("prayed", ar), Primary = true });
        info.Actions.Add(new PopupAction { Label = Lang.T("later", ar) });
        info.SetPrayed = null; // only a preview, so it shouldn't change anything
        Show(st, info, 12);
    }

    // Keep the idle footprint small: release memory once nothing is on screen.
    void Trim()
    {
        if (PopupOpen() || Open(settingsForm) || Open(tasbihForm) || Open(adhkarForm) || Open(qiblaForm) || Open(statsForm)) return;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        using (Process p = Process.GetCurrentProcess())
            try { EmptyWorkingSet(p.Handle); } catch (DllNotFoundException) { }
    }

    static bool Open(Form f) { return f != null && !f.IsDisposed; }

    void PlaySound()
    {
        try
        {
            string wav = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "adhan.wav");
            if (File.Exists(wav))
            {
                if (player == null) player = new SoundPlayer(wav);
                player.Play();
            }
            else SystemSounds.Asterisk.Play();
        }
        catch (Exception ex) { ErrorLog.Write(ex); SystemSounds.Asterisk.Play(); }
    }

    void UpdateTrayText()
    {
        string text = Lang.T("app");
        DateTime now = DateTime.Now;
        if (s.HasLoc)
        {
            PopupInfo n = NextInfo(s, PopupKind.Next);
            if (n != null)
            {
                string left = n.Iftar ? string.Format(Lang.T("iftarIn"), Lang.Duration(n.Time - now, Lang.Ar))
                                      : string.Format(Lang.T("in"), Lang.Duration(n.Time - now, Lang.Ar));
                text = Lang.Prayer(n.Index, n.Time, Lang.Ar) + " " + Lang.Time(n.Time, s.Use24h, Lang.Ar) + " · " + left;
            }
            if (now < quietUntil) text = string.Format(Lang.T("quietTill"), Lang.Time(quietUntil, s.Use24h, Lang.Ar)) + "\n" + text;
        }
        else text += " — " + Lang.T("noLoc");
        tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
    }

    // ---------- tray menu ----------

    void BuildMenu()
    {
        var old = tray.ContextMenuStrip;
        var menu = new ContextMenuStrip();
        menu.RightToLeft = Lang.Ar ? RightToLeft.Yes : RightToLeft.No;
        menu.Opening += (o, e) => { FillMenu(menu); e.Cancel = false; };
        FillMenu(menu);
        tray.ContextMenuStrip = menu;
        if (old != null) old.Dispose();
    }

    void FillMenu(ContextMenuStrip menu)
    {
        var renderer = new MenuRenderer(Theme.Resolve(s.Theme));
        menu.Renderer = renderer;
        menu.Items.Clear();
        if (s.HasLoc)
        {
            DateTime now = DateTime.Now;
            DateTime[] day = Times(s, now.Date);
            menu.Items.Add(new ToolStripMenuItem(HijriNow(s, now)) { Enabled = false });
            if (s.Imsak && day[0] != DateTime.MinValue && Hijri.IsRamadan(now, s.HijriAdjust))
                menu.Items.Add(TimeRow(Lang.T("imsakTitle"), day[0].AddMinutes(-s.ImsakMinutes)));
            foreach (int i in new[] { 0, 1, 2, 3, 4, 5 })
                menu.Items.Add(TimeRow(Lang.Prayer(i, now, Lang.Ar), day[i]));
            menu.Items.Add(new ToolStripSeparator());
            if (pendingLocation != null)
            {
                string place = string.IsNullOrEmpty(pendingLocation.City) ? pendingLocation.Country : pendingLocation.City;
                menu.Items.Add(string.Format(Lang.T("offerMenu"), place), null, (o, e) => AcceptPending());
                menu.Items.Add(new ToolStripSeparator());
            }
        }
        menu.Items.Add(Lang.T("tasbih"), null, (o, e) => ToggleTasbih());
        var adhkar = new ToolStripMenuItem(Lang.T("adhkar"));
        adhkar.DropDownItems.Add(Lang.T("adhkarM"), null, (o, e) => OpenAdhkar(AdhkarSet.Morning));
        adhkar.DropDownItems.Add(Lang.T("adhkarE"), null, (o, e) => OpenAdhkar(AdhkarSet.Evening));
        adhkar.DropDownItems.Add(Lang.T("adhkarA"), null, (o, e) => OpenAdhkar(AdhkarSet.AfterPrayer));
        adhkar.DropDownItems.Add(Lang.T("adhkarS"), null, (o, e) => OpenAdhkar(AdhkarSet.Sleep));
        adhkar.DropDown.Renderer = renderer;
        menu.Items.Add(adhkar);
        if (s.HasLoc) menu.Items.Add(Lang.T("qibla"), null, (o, e) => OpenQibla());
        menu.Items.Add(Lang.T("stats"), null, (o, e) => OpenStats());
        menu.Items.Add(new ToolStripSeparator());

        var quiet = new ToolStripMenuItem(Lang.T("quiet"));
        bool isQuiet = DateTime.Now < quietUntil;
        quiet.Checked = isQuiet;
        quiet.DropDownItems.Add(Lang.T("quiet1"), null, (o, e) => SetQuiet(60, true));
        quiet.DropDownItems.Add(Lang.T("quiet3"), null, (o, e) => SetQuiet(180, true));
        if (isQuiet) quiet.DropDownItems.Add(Lang.T("quietOff"), null, (o, e) => SetQuiet(0, true));
        quiet.DropDown.Renderer = renderer;
        menu.Items.Add(quiet);
        menu.Items.Add(Lang.T("showNext"), null, (o, e) => ShowNext());
        menu.Items.Add(Lang.T("settings"), null, (o, e) => OpenSettings());
        if (s.LocMode == "auto") menu.Items.Add(Lang.T("refresh"), null, (o, e) => Detect(true));
        menu.Items.Add(Lang.T("updates"), null, (o, e) => CheckUpdates());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Lang.T("exit"), null, (o, e) => Quit());
    }

    ToolStripMenuItem TimeRow(string name, DateTime t)
    {
        var it = new ToolStripMenuItem(name, null, (o, e) => ShowNext());
        it.ShortcutKeyDisplayString = Lang.Time(t, s.Use24h, Lang.Ar);
        return it;
    }

    void SetQuiet(int minutes, bool closeCard)
    {
        quietUntil = minutes > 0 ? DateTime.Now.AddMinutes(minutes) : DateTime.MinValue;
        if (closeCard && minutes > 0 && PopupOpen()) popup.Dismiss();
        UpdateTrayText();
    }

    void Balloon(string text, ToolTipIcon icon, Action onClick)
    {
        balloonAction = onClick;
        tray.ShowBalloonTip(8000, Lang.T("app"), text, icon);
    }

    // ---------- windows ----------

    public void OpenSettings()
    {
        if (Open(settingsForm)) { settingsForm.Activate(); return; }
        settingsForm = new SettingsForm(this, s.Clone());
        settingsForm.FormClosed += (o, e) => Delay(1000, Trim);
        settingsForm.Show();
        settingsForm.Activate();
    }

    void ShowTasbih()
    {
        if (Open(tasbihForm)) return;
        tasbihForm = new TasbihForm(s, (x, y) => { s.TasbihX = x; s.TasbihY = y; s.Save(); }, OnTasbihTap);
        tasbihForm.FormClosed += (o, e) => { FlushStats(); Delay(1000, Trim); };
        tasbihForm.Show();
    }

    void ToggleTasbih()
    {
        if (Open(tasbihForm)) tasbihForm.Close();
        else ShowTasbih();
    }

    void OnTasbihTap()
    {
        stats.AddTasbih(DateTime.Now, 1);
        statsDirty = true;
        ReloadStats();
    }

    void OpenSuggestedAdhkar()
    {
        DateTime now = DateTime.Now;
        OpenAdhkar(Adhkar.Suggest(now, Times(s, now.Date)));
    }

    void OpenAdhkar(AdhkarSet set)
    {
        if (Open(adhkarForm)) adhkarForm.Close();
        adhkarForm = new AdhkarForm(s, set, finished =>
        {
            stats.MarkAdhkar(DateTime.Now, finished);
            stats.Save();
            ReloadStats();
        });
        adhkarForm.FormClosed += (o, e) => Delay(1000, Trim);
        adhkarForm.Show();
        adhkarForm.Activate();
    }

    void OpenQibla()
    {
        if (!s.HasLoc) { OpenSettings(); return; }
        if (Open(qiblaForm)) qiblaForm.Close();
        qiblaForm = new QiblaForm(s);
        qiblaForm.FormClosed += (o, e) => Delay(1000, Trim);
        qiblaForm.Show();
        qiblaForm.Activate();
    }

    public void OpenStats()
    {
        if (Open(statsForm)) { statsForm.Activate(); return; }
        statsForm = new StatsForm(s, stats, day => Times(s, day), SetPrayed, () => { stats.Clear(); stats.Save(); });
        statsForm.FormClosed += (o, e) => Delay(1000, Trim);
        statsForm.Show();
        statsForm.Activate();
    }

    public void ApplySettings(Settings n)
    {
        n.TasbihX = s.TasbihX;
        n.TasbihY = s.TasbihY;
        s = n;
        Lang.Ar = s.Language == "ar";
        s.Save();
        s.ApplyStartup();
        cacheDate = DateTime.MinValue;
        pendingLocation = null;
        BuildMenu();
        if (Open(tasbihForm)) { tasbihForm.Close(); ShowTasbih(); }
        if (Open(statsForm)) { statsForm.Close(); OpenStats(); }
        Tick();
        if (!s.HasLoc && s.LocMode == "auto") Detect(true);
    }

    // ---------- location & updates ----------

    void Detect(bool userAsked)
    {
        if (detecting) return;
        detecting = true;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            GeoResult r = null;
            try { r = Locator.Detect(); } catch (Exception ex) { ErrorLog.Write(ex); }
            invoker.BeginInvoke((Action)(() => OnDetected(r, userAsked)));
        });
    }

    void OnDetected(GeoResult r, bool userAsked)
    {
        detecting = false;
        if (r == null)
        {
            if (!s.HasLoc || userAsked) Balloon(Lang.T("detectFail"), ToolTipIcon.Warning, null);
            if (!s.HasLoc) OpenSettings();
            return;
        }
        if (s.HasLoc && s.LocMode != "auto") return;

        switch (LocationPolicy.Decide(s.HasLoc, userAsked, s.Lat, s.Lon, r.Lat, r.Lon, s.HasOffer, s.OfferLat, s.OfferLon))
        {
            case LocationDecision.Apply:
                bool show = !s.HasLoc || userAsked;
                ApplyLocation(r);
                if (show) ShowNext();
                break;
            case LocationDecision.Offer:
                pendingLocation = r;
                s.HasOffer = true;
                s.OfferLat = r.Lat;
                s.OfferLon = r.Lon;
                s.Save();
                string place = string.IsNullOrEmpty(r.City) ? r.Country : r.City;
                Balloon(string.Format(Lang.T("offer"), place, s.City), ToolTipIcon.Info, AcceptPending);
                break;
            case LocationDecision.Ignore:
                pendingLocation = r; // keep it reachable from the tray menu without nagging again
                break;
        }
    }

    void AcceptPending()
    {
        GeoResult r = pendingLocation;
        if (r == null) return;
        ApplyLocation(r);
        ShowNext();
    }

    void ApplyLocation(GeoResult r)
    {
        s.Lat = r.Lat;
        s.Lon = r.Lon;
        if (!string.IsNullOrEmpty(r.City)) s.City = r.City;
        if (!string.IsNullOrEmpty(r.Country)) s.Country = r.Country;
        s.HasLoc = true;
        s.HasOffer = false;
        pendingLocation = null;
        s.Save();
        cacheDate = DateTime.MinValue;
        Tick();
    }

    void CheckUpdates()
    {
        ThreadPool.QueueUserWorkItem(_ =>
        {
            string tag = UpdateCheck.LatestTag();
            invoker.BeginInvoke((Action)(() =>
            {
                if (tag == null) Balloon(Lang.T("updateFail"), ToolTipIcon.Warning, null);
                else if (UpdateCheck.IsNewer(UpdateCheck.Version, tag))
                    Balloon(string.Format(Lang.T("updateNew"), tag), ToolTipIcon.Info, () => Process.Start(UpdateCheck.ReleasesUrl));
                else Balloon(string.Format(Lang.T("upToDate"), UpdateCheck.Version), ToolTipIcon.Info, null);
            }));
        });
    }

    void Quit()
    {
        timer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
        if (Open(tasbihForm)) tasbihForm.Close();
        FlushStats();
        tray.Visible = false;
        tray.Dispose();
        msgWindow.DestroyHandle();
        ExitThread();
    }
}
