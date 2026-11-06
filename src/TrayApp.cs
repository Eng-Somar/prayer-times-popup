using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Media;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

public class TrayApp : ApplicationContext
{
    static readonly int[] Five = { 0, 2, 3, 4, 5 };

    [System.Runtime.InteropServices.DllImport("psapi.dll")] static extern bool EmptyWorkingSet(IntPtr process);

    Settings s;
    readonly NotifyIcon tray = new NotifyIcon();
    readonly Timer timer = new Timer();
    readonly Control invoker = new Control();
    readonly HashSet<string> fired = new HashSet<string>();
    PopupForm popup;
    SettingsForm settingsForm;
    SoundPlayer player;
    DateTime cacheDate = DateTime.MinValue, firedDate = DateTime.MinValue;
    DateTime[] today;
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

    public TrayApp(bool preview, bool openSettings)
    {
        s = Settings.Load();
        Lang.Ar = s.Language == "ar";
        invoker.CreateControl();
        IntPtr unused = invoker.Handle;
        msgWindow = new MsgWindow(OpenSettings);

        tray.Icon = IconArt.TrayIcon(SystemInformation.SmallIconSize.Width);
        tray.Visible = true;
        tray.MouseClick += (o, e) => { if (e.Button == MouseButtons.Left) ShowNext(); };
        BuildMenu();

        timer.Interval = 10000;
        timer.Tick += (o, e) => Tick();
        timer.Start();

        SystemEvents.PowerModeChanged += OnPowerChanged;
        SystemEvents.TimeChanged += OnTimeChanged;

        if (openSettings) { OpenSettings(); }
        else if (preview) { Preview(s); }
        else if (s.HasLoc)
        {
            Tick();
            Delay(1500, () => { if (popup == null || popup.IsDisposed) ShowNext(); });
        }
        else UpdateTrayText();

        if (!s.HasLoc || s.LocMode == "auto") Detect(false);
        Delay(20000, Trim);
    }

    void OnPowerChanged(object o, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) invoker.BeginInvoke((Action)Tick);
    }

    void OnTimeChanged(object o, EventArgs e)
    {
        invoker.BeginInvoke((Action)(() => { cacheDate = DateTime.MinValue; Tick(); }));
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
        return PrayCalc.Compute(date, lat, lon, PrayCalc.Resolve(st.Method, st.Country), st.Asr == 2 ? 2 : 1, st.HighLat);
    }

    void Tick()
    {
        if (!s.HasLoc) { UpdateTrayText(); return; }
        DateTime now = DateTime.Now;
        if (now.Date != cacheDate) { today = Times(s, now.Date); cacheDate = now.Date; }
        if (now.Date != firedDate) { fired.Clear(); firedDate = now.Date; }

        foreach (int i in Five)
        {
            DateTime t = today[i];
            string k = i.ToString(CultureInfo.InvariantCulture);
            if (now >= t && now < t.AddMinutes(15))
            {
                if (fired.Add("a" + k))
                {
                    fired.Add("b" + k);
                    Show(s, Make(s, PopupKind.At, i, t, today), s.Duration);
                    PlaySound();
                }
            }
            else if (s.RemindBefore > 0 && now < t && now >= t.AddMinutes(-s.RemindBefore))
            {
                if (fired.Add("b" + k))
                    Show(s, Make(s, PopupKind.Before, i, t, today), s.Duration == 0 ? 0 : Math.Min(s.Duration, 20));
            }
        }
        UpdateTrayText();
    }

    PopupInfo NextInfo(Settings st, PopupKind kind)
    {
        DateTime now = DateTime.Now;
        DateTime[] day = Times(st, now.Date);
        foreach (int i in Five)
            if (day[i] > now) return Make(st, kind, i, day[i], day);
        DateTime[] tm = Times(st, now.Date.AddDays(1));
        return Make(st, kind, 0, tm[0], tm);
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
        };
    }

    void Show(Settings st, PopupInfo info, int seconds)
    {
        if (popup != null && !popup.IsDisposed) popup.Close();
        popup = new PopupForm(st, info, seconds);
        popup.FormClosed += (o, e) => Delay(1000, Trim);
        popup.Pop();
    }

    public void ShowNext()
    {
        if (!s.HasLoc) { OpenSettings(); return; }
        Show(s, NextInfo(s, PopupKind.Next), 10);
    }

    public void Preview(Settings st)
    {
        Show(st, NextInfo(st, PopupKind.At), 12);
    }

    // Keep the idle footprint small: release memory once nothing is on screen.
    void Trim()
    {
        if ((popup != null && !popup.IsDisposed) || (settingsForm != null && !settingsForm.IsDisposed)) return;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        try { EmptyWorkingSet(System.Diagnostics.Process.GetCurrentProcess().Handle); } catch { }
    }

    void PlaySound()
    {
        if (!s.Sound) return;
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
        catch { }
    }

    void UpdateTrayText()
    {
        string text = Lang.T("app");
        if (s.HasLoc)
        {
            PopupInfo n = NextInfo(s, PopupKind.Next);
            text = Lang.Prayer(n.Index, n.Time, Lang.Ar) + " " + Lang.Time(n.Time, s.Use24h, Lang.Ar)
                   + " · " + string.Format(Lang.T("in"), Lang.Duration(n.Time - DateTime.Now, Lang.Ar));
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
        menu.Opening += (o, e) => FillMenu(menu);
        FillMenu(menu);
        tray.ContextMenuStrip = menu;
        if (old != null) old.Dispose();
    }

    void FillMenu(ContextMenuStrip menu)
    {
        menu.Items.Clear();
        if (s.HasLoc)
        {
            DateTime now = DateTime.Now;
            DateTime[] day = Times(s, now.Date);
            var head = new ToolStripMenuItem(Lang.Hijri(now, Lang.Ar)) { Enabled = false };
            menu.Items.Add(head);
            foreach (int i in new[] { 0, 1, 2, 3, 4, 5 })
            {
                var it = new ToolStripMenuItem(Lang.Prayer(i, day[i], Lang.Ar), null, (o, e) => ShowNext());
                it.ShortcutKeyDisplayString = Lang.Time(day[i], s.Use24h, Lang.Ar);
                menu.Items.Add(it);
            }
            menu.Items.Add(new ToolStripSeparator());
        }
        menu.Items.Add(Lang.T("showNext"), null, (o, e) => ShowNext());
        menu.Items.Add(Lang.T("settings"), null, (o, e) => OpenSettings());
        menu.Items.Add(Lang.T("refresh"), null, (o, e) => Detect(true));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(Lang.T("exit"), null, (o, e) => Quit());
    }

    // ---------- settings & location ----------

    public void OpenSettings()
    {
        if (settingsForm != null && !settingsForm.IsDisposed) { settingsForm.Activate(); return; }
        settingsForm = new SettingsForm(this, s.Clone());
        settingsForm.FormClosed += (o, e) => Delay(1000, Trim);
        settingsForm.Show();
        settingsForm.Activate();
    }

    public void ApplySettings(Settings n)
    {
        s = n;
        Lang.Ar = s.Language == "ar";
        s.Save();
        s.ApplyStartup();
        cacheDate = DateTime.MinValue;
        BuildMenu();
        Tick();
        if (!s.HasLoc && s.LocMode == "auto") Detect(true);
    }

    void Detect(bool userAsked)
    {
        if (detecting) return;
        detecting = true;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            GeoResult r = null;
            try { r = Locator.Detect(); } catch { }
            invoker.BeginInvoke((Action)(() => OnDetected(r, userAsked)));
        });
    }

    void OnDetected(GeoResult r, bool userAsked)
    {
        detecting = false;
        if (r != null)
        {
            bool first = !s.HasLoc;
            if (s.LocMode == "auto" || first)
            {
                s.Lat = r.Lat; s.Lon = r.Lon;
                if (!string.IsNullOrEmpty(r.City)) s.City = r.City;
                if (!string.IsNullOrEmpty(r.Country)) s.Country = r.Country;
                s.HasLoc = true;
                if (first) s.LocMode = "auto";
                s.Save();
                cacheDate = DateTime.MinValue;
                Tick();
                if (first || userAsked) ShowNext();
            }
        }
        else if (!s.HasLoc || userAsked)
        {
            tray.ShowBalloonTip(5000, Lang.T("app"), Lang.T("detectFail"), ToolTipIcon.Warning);
            if (!s.HasLoc) OpenSettings();
        }
    }

    void Quit()
    {
        timer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
        tray.Visible = false;
        tray.Dispose();
        msgWindow.DestroyHandle();
        ExitThread();
    }
}
