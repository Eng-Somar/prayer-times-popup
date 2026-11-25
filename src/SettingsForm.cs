using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

// Uses the same design as the rest of the app. Picking a design re-themes this window and previews the card at once.
public class SettingsForm : Form
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly DateTime NotFriday = new DateTime(2026, 11, 2); // so Dhuhr keeps its own name in the table

    readonly TrayApp app;
    readonly Settings orig;
    readonly float S;
    readonly bool ar;
    readonly int labelW, fieldW;
    Theme th;
    readonly Segmented nav, sLang, sFmt, sScreen, sLoc;
    readonly DropList dMethod, dAsr, dHL;
    readonly ThemedInput tCity, tLat, tLon;
    readonly Stepper nRemind, nDur, nHijri, nImsak, nAfter, nSleep;
    readonly ToggleSwitch kStart, kDnd, kImsak, kFast, kM, kE, kA, kS;
    readonly ToggleSwitch[] kRemind = new ToggleSwitch[6], kSound = new ToggleSwitch[6];
    readonly Stepper[] nOffset = new Stepper[6], nIqama = new Stepper[6];
    readonly List<ThemeSwatch> swatches = new List<ThemeSwatch>();
    readonly ThemedButton bDetect, bPreview, bSave, bCancel;
    readonly Label lStatus, lEffective, lError;
    readonly Panel[] pages = new Panel[4];
    string themeKey, country;
    double baseLat, baseLon;
    bool hasBase;

    public SettingsForm(TrayApp app, Settings s)
    {
        this.app = app;
        orig = s;
        country = s.Country;
        baseLat = s.Lat;
        baseLon = s.Lon;
        hasBase = s.HasLoc;
        themeKey = s.Theme;
        ar = Lang.Ar;
        th = Theme.Resolve(s.Theme);

        SuspendLayout();
        Text = Lang.T("settings") + " — " + Lang.T("app");
        Font = new Font("Segoe UI", 9.75f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = true;
        KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (ArgumentException) { }
        if (ar) { RightToLeft = RightToLeft.Yes; RightToLeftLayout = true; }
        using (Graphics g = CreateGraphics()) S = g.DpiX / 96f;
        labelW = Px(250);
        fieldW = Px(380);
        ClientSize = new Size(Px(720), Px(620));
        ThemeUi.Frame(this, th);

        int pad = Px(20);
        nav = new Segmented(th, S, ClientSize.Width - 2 * pad, true, Lang.T("tabGeneral"), Lang.T("tabLoc"), Lang.T("tabPrayers"), Lang.T("tabRemind")) { Location = new Point(pad, Px(16)), AccessibleName = Lang.T("settings") };
        nav.SelectedIndexChanged += (o, e) => ShowPage(nav.SelectedIndex);
        var top = new Panel { Dock = DockStyle.Top, Height = Px(66), BackColor = th.Bg };
        top.Controls.Add(nav);

        var content = new Panel { Dock = DockStyle.Fill, BackColor = th.Bg };

        // General
        TableLayoutPanel general = NewPage(content, 0);
        AddHeading(general, Lang.T("theme"), true);
        var swatchBox = new FlowLayoutPanel { Width = ClientSize.Width - 2 * pad + Px(10), Height = Px(208), Margin = new Padding(0, 0, 0, Px(8)), BackColor = th.Bg };
        int swatchW = (ClientSize.Width - 2 * pad - 3 * Px(10)) / 4;
        var auto = new ThemeSwatch(th, S, "auto", Lang.T("autoThemeShort"), swatchW, Theme.All[0], Theme.All[1]);
        swatches.Add(auto);
        foreach (Theme t in Theme.All) swatches.Add(new ThemeSwatch(th, S, t.Key, t.Name(ar), swatchW, t));
        foreach (ThemeSwatch sw in swatches)
        {
            sw.SetInitial(sw.Key == s.Theme);
            sw.CheckedChanged += OnSwatch;
            swatchBox.Controls.Add(sw);
        }
        general.Controls.Add(swatchBox, 0, general.RowCount++);
        general.SetColumnSpan(swatchBox, 2);

        sLang = new Segmented(th, S, fieldW, false, "English", "العربية") { AccessibleName = Lang.T("language") };
        sLang.Init(s.Language == "ar" ? 1 : 0);
        Row(general, Lang.T("language"), sLang);
        sFmt = new Segmented(th, S, fieldW, false, Lang.T("t12"), Lang.T("t24")) { AccessibleName = Lang.T("timeFmt") };
        sFmt.Init(s.Use24h ? 1 : 0);
        Row(general, Lang.T("timeFmt"), sFmt);
        sScreen = new Segmented(th, S, fieldW, false, Lang.T("screenMain"), Lang.T("screenMouse")) { AccessibleName = Lang.T("screen") };
        sScreen.Init(s.PopupOnMouseScreen ? 1 : 0);
        Row(general, Lang.T("screen"), sScreen);
        nDur = NewStepper("duration", 0, 600, s.Duration);
        nDur.Step = 5;
        Row(general, Lang.T("duration"), nDur);
        nHijri = NewStepper("hijriAdj", -2, 2, s.HijriAdjust);
        Row(general, Lang.T("hijriAdj"), nHijri);
        kStart = new ToggleSwitch(th, S, Lang.T("startup"), s.Startup);
        Row(general, "", kStart);

        // Location and calculation
        TableLayoutPanel loc = NewPage(content, 1);
        sLoc = new Segmented(th, S, fieldW, false, Lang.T("locAuto"), Lang.T("locManual")) { AccessibleName = Lang.T("location") };
        sLoc.Init(s.LocMode == "manual" ? 1 : 0);
        sLoc.SelectedIndexChanged += (o, e) => SyncLocFields();
        Row(loc, Lang.T("location"), sLoc);
        tCity = new ThemedInput(th, S, fieldW, s.City);
        Row(loc, Lang.T("city"), tCity);
        tLat = new ThemedInput(th, S, fieldW, s.HasLoc ? s.Lat.ToString("0.######", Inv) : "");
        Row(loc, Lang.T("lat"), tLat);
        tLon = new ThemedInput(th, S, fieldW, s.HasLoc ? s.Lon.ToString("0.######", Inv) : "");
        Row(loc, Lang.T("lon"), tLon);
        tLat.Box.TextChanged += (o, e) => SyncEffective();
        tLon.Box.TextChanged += (o, e) => SyncEffective();
        lError = Kit.Label(th, "", false, fieldW);
        Row(loc, "", lError);

        bDetect = new ThemedButton(th, ButtonKind.Secondary, S, Lang.T("detect")) { Margin = new Padding(0, 0, Px(10), 0) };
        bDetect.Font = Font;
        bDetect.Fit();
        bDetect.Click += (o, e) => DetectNow();
        lStatus = Kit.Label(th, "", true);
        lStatus.Margin = new Padding(0, Px(8), 0, 0);
        Row(loc, "", Flow(bDetect, lStatus));

        var methods = new List<string> { Lang.T("autoMethod") };
        foreach (MethodDef m in PrayCalc.Methods) methods.Add(m.Name(ar));
        dMethod = new DropList(th, S, fieldW, methods.ToArray()) { AccessibleName = Lang.T("method") };
        for (int i = 0; i < PrayCalc.Methods.Length; i++) if (PrayCalc.Methods[i].Key == s.Method) dMethod.Init(i + 1);
        dMethod.SelectedIndexChanged += (o, e) => SyncEffective();
        Row(loc, Lang.T("method"), dMethod);
        lEffective = Kit.Label(th, "", true, fieldW);
        Row(loc, "", lEffective);

        dAsr = new DropList(th, S, fieldW, Lang.T("asrStd"), Lang.T("asrHanafi")) { AccessibleName = Lang.T("asr") };
        dAsr.Init(s.Asr == 2 ? 1 : 0);
        Row(loc, Lang.T("asr"), dAsr);
        dHL = new DropList(th, S, fieldW, Lang.T("hl0"), Lang.T("hl1"), Lang.T("hl2")) { AccessibleName = Lang.T("highLat") };
        dHL.Init(Math.Max(0, Math.Min(2, s.HighLat)));
        Row(loc, Lang.T("highLat"), dHL);

        // Per-prayer table
        var prayers = new Panel { Dock = DockStyle.Fill, BackColor = th.Bg, Padding = new Padding(pad, Px(4), pad, 0) };
        var table = new TableLayoutPanel { ColumnCount = 5, AutoSize = true, Dock = DockStyle.Top, BackColor = th.Bg };
        foreach (string h in new[] { "colPrayer", "colRemind", "colSound", "colAdjust", "colIqama" })
        {
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Label head = Kit.Label(th, Lang.T(h), true);
            head.Margin = new Padding(0, 0, Px(24), Px(10));
            table.Controls.Add(head);
        }
        foreach (int i in Scheduler.Five)
        {
            string name = Lang.Prayer(i, NotFriday, ar);
            Label l = Kit.Label(th, name);
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(0, Px(8), Px(24), Px(8));
            table.Controls.Add(l);
            kRemind[i] = new ToggleSwitch(th, S, "", s.RemindBy[i]) { AccessibleName = Lang.T("colRemind") + " " + name, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, Px(24), 0) };
            kSound[i] = new ToggleSwitch(th, S, "", s.SoundBy[i]) { AccessibleName = Lang.T("colSound") + " " + name, Anchor = AnchorStyles.Left, Margin = new Padding(0, 0, Px(24), 0) };
            nOffset[i] = NewStepper(null, -60, 60, s.Offsets[i]);
            nOffset[i].AccessibleName = Lang.T("colAdjust") + " " + name;
            nOffset[i].Margin = new Padding(0, 0, Px(24), 0);
            nOffset[i].Anchor = AnchorStyles.Left;
            nIqama[i] = NewStepper(null, 0, 90, s.Iqama[i]);
            nIqama[i].AccessibleName = Lang.T("colIqama") + " " + name;
            nIqama[i].Anchor = AnchorStyles.Left;
            nIqama[i].Margin = Padding.Empty;
            table.Controls.Add(kRemind[i]);
            table.Controls.Add(kSound[i]);
            table.Controls.Add(nOffset[i]);
            table.Controls.Add(nIqama[i]);
        }
        Label prayersHint = Kit.Label(th, Lang.T("prayersHint"), true, ClientSize.Width - 2 * pad);
        prayersHint.Dock = DockStyle.Top;
        prayersHint.Padding = new Padding(0, Px(16), 0, 0);
        prayers.Controls.Add(prayersHint);
        prayers.Controls.Add(table);
        content.Controls.Add(prayers);
        pages[2] = prayers;

        // Reminders
        TableLayoutPanel remind = NewPage(content, 3);
        remind.ColumnStyles[0] = new ColumnStyle(SizeType.Percent, 100);
        remind.ColumnStyles[1] = new ColumnStyle(SizeType.AutoSize);
        AddHeading(remind, Lang.T("grpPrayer"), true);
        nRemind = NewStepper("remind", 0, 60, s.RemindBefore);
        Row(remind, Lang.T("remind"), nRemind);
        kDnd = Option(remind, "dnd", s.DndFullscreen, null);
        AddHeading(remind, Lang.T("grpFast"), false);
        nImsak = NewStepper("imsakOpt", 1, 60, s.ImsakMinutes);
        kImsak = Option(remind, "imsakOpt", s.Imsak, nImsak);
        kFast = Option(remind, "fastOpt", s.FastNotice, null);
        AddHeading(remind, Lang.T("adhkar"), false);
        kM = Option(remind, "optM", s.AdhkarMorning, null);
        kE = Option(remind, "optE", s.AdhkarEvening, null);
        nAfter = NewStepper("optA", 0, 60, s.AdhkarAfterMinutes);
        kA = Option(remind, "optA", s.AdhkarAfter, nAfter);
        nSleep = NewStepper("optS", 0, 24 * 60 - 1, s.SleepMinute);
        nSleep.Step = 5;
        nSleep.Wrap = true;
        nSleep.Format = v => string.Format(Inv, "{0:00}:{1:00}", v / 60, v % 60);
        nSleep.Refresh2();
        kS = Option(remind, "optS", s.AdhkarSleep, nSleep);

        // Footer
        bSave = new ThemedButton(th, ButtonKind.Primary, S, Lang.T("save")) { Margin = new Padding(Px(8), 0, 0, 0) };
        bCancel = new ThemedButton(th, ButtonKind.Secondary, S, Lang.T("cancel")) { Margin = new Padding(Px(8), 0, 0, 0) };
        bPreview = new ThemedButton(th, ButtonKind.Ghost, S, Lang.T("preview")) { Margin = new Padding(Px(8), 0, Px(18), 0) };
        foreach (ThemedButton b in new[] { bSave, bCancel, bPreview })
        {
            b.Font = Font;
            b.Fit();
            b.Width = Math.Max(b.Width, Px(96));
        }
        bSave.Click += (o, e) => { Settings n = Collect(true); if (n != null) { app.ApplySettings(n); Close(); } };
        bCancel.Click += (o, e) => Close();
        bPreview.Click += (o, e) => app.Preview(Collect(false));
        AcceptButton = bSave;
        CancelButton = bCancel;
        var footer = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Bottom, Height = Px(62), Padding = new Padding(pad, Px(12), pad, 0), BackColor = th.Bg };
        footer.Controls.Add(bCancel);
        footer.Controls.Add(bSave);
        footer.Controls.Add(bPreview);

        Controls.Add(content);
        Controls.Add(top);
        Controls.Add(footer);
        SyncLocFields();
        SyncEffective();
        ResumeLayout(true);
        // Every page starts visible so its controls exist before the window appears; switching tabs is then instant.
        Load += (o, e) => { ShowPage(0); ThemeUi.Center(this); };
    }

    int Px(float v) { return (int)Math.Round(v * S); }

    TableLayoutPanel NewPage(Panel content, int index)
    {
        var page = new Panel { Dock = DockStyle.Fill, BackColor = th.Bg };
        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(Px(20), Px(4), Px(20), 0), BackColor = th.Bg };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelW));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        page.Controls.Add(grid);
        content.Controls.Add(page);
        pages[index] = page;
        return grid;
    }

    void ShowPage(int index)
    {
        for (int i = 0; i < pages.Length; i++) pages[i].Visible = i == index;
    }

    Label Heading(string text)
    {
        Label l = Kit.Label(th, text);
        l.Font = new Font("Segoe UI Semibold", 10.5f);
        l.Margin = new Padding(0, 0, 0, Px(8));
        return l;
    }

    void AddHeading(TableLayoutPanel grid, string text, bool first)
    {
        Label l = Heading(text);
        l.Margin = new Padding(0, first ? 0 : Px(16), 0, Px(6));
        grid.Controls.Add(l, 0, grid.RowCount++);
        grid.SetColumnSpan(l, 2);
    }

    Stepper NewStepper(string nameKey, int min, int max, int value)
    {
        var n = new Stepper(th, S, min, max, value);
        if (nameKey != null) n.AccessibleName = Lang.T(nameKey);
        return n;
    }

    FlowLayoutPanel Flow(params Control[] cs)
    {
        var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, BackColor = th.Bg };
        f.Controls.AddRange(cs);
        return f;
    }

    void Row(TableLayoutPanel grid, string label, Control c)
    {
        Control l;
        if (label == "") l = new Panel { Size = Size.Empty, Margin = Padding.Empty };
        else
        {
            l = Kit.Label(th, label);
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(0, Px(6), Px(16), Px(6));
        }
        c.Anchor = AnchorStyles.Left;
        if (!(c is FlowLayoutPanel)) c.Margin = new Padding(0, Px(3), 0, Px(3));
        // Explicit cells: automatic placement skips hidden controls, which would shift every later row sideways.
        int row = grid.RowCount++;
        grid.Controls.Add(l, 0, row);
        grid.Controls.Add(c, 1, row);
    }

    ToggleSwitch Option(TableLayoutPanel grid, string key, bool value, Control extra)
    {
        var k = new ToggleSwitch(th, S, Lang.T(key), value) { Anchor = AnchorStyles.Left, Margin = new Padding(0, Px(3), Px(12), Px(3)) };
        int row = grid.RowCount++;
        grid.Controls.Add(k, 0, row);
        if (extra == null) grid.SetColumnSpan(k, 2);
        else { extra.Anchor = AnchorStyles.Left; extra.Margin = new Padding(0, Px(3), 0, Px(3)); grid.Controls.Add(extra, 1, row); }
        return k;
    }

    void OnSwatch(object sender, EventArgs e)
    {
        var sw = (ThemeSwatch)sender;
        if (!sw.Checked) return;
        themeKey = sw.Key;
        th = Theme.Resolve(themeKey);
        ThemeUi.Frame(this, th);
        Kit.Apply(this, th);
        Invalidate(true);
        app.Preview(Collect(false));
    }

    void SyncLocFields()
    {
        bool manual = sLoc.SelectedIndex == 1;
        tCity.ReadOnly = !manual;
        tLat.ReadOnly = !manual;
        tLon.ReadOnly = !manual;
    }

    // An empty note takes no room, so it doesn't leave a gap in the form.
    static void SetNote(Label note, string text)
    {
        note.Text = text;
        note.Visible = text != "";
    }

    // Shows which method Auto really uses for the coordinates currently typed.
    void SyncEffective()
    {
        SetNote(lError, "");
        double lat, lon;
        string cc = country;
        if (hasBase && ParseCoord(tLat.Box.Text, 90, out lat) && ParseCoord(tLon.Box.Text, 180, out lon))
            cc = LocationPolicy.CountryFor(country, baseLat, baseLon, lat, lon);
        string key = dMethod.SelectedIndex <= 0 ? "auto" : PrayCalc.Methods[dMethod.SelectedIndex - 1].Key;
        SetNote(lEffective, key == "auto" ? string.Format(Lang.T("effective"), PrayCalc.Resolve(key, cc).Name(Lang.Ar)) : "");
    }

    void DetectNow()
    {
        bDetect.Enabled = false;
        lStatus.Text = Lang.T("detecting");
        ThreadPool.QueueUserWorkItem(_ =>
        {
            GeoResult r = null;
            try { r = Locator.Detect(); } catch (Exception ex) { ErrorLog.Write(ex); }
            try { BeginInvoke((Action)(() => OnDetected(r))); } catch (InvalidOperationException) { }
        });
    }

    void OnDetected(GeoResult r)
    {
        if (IsDisposed) return;
        bDetect.Enabled = true;
        if (r == null) { lStatus.Text = Lang.T("detectFail"); return; }
        baseLat = r.Lat;
        baseLon = r.Lon;
        hasBase = true;
        if (!string.IsNullOrEmpty(r.Country)) country = r.Country;
        tLat.Box.Text = r.Lat.ToString("0.######", Inv);
        tLon.Box.Text = r.Lon.ToString("0.######", Inv);
        if (!string.IsNullOrEmpty(r.City)) tCity.Box.Text = r.City;
        lStatus.Text = string.IsNullOrEmpty(r.City) ? tLat.Box.Text + ", " + tLon.Box.Text : r.City;
        SyncEffective();
    }

    static bool ParseCoord(string text, double limit, out double v)
    {
        return double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, Inv, out v) && Math.Abs(v) <= limit;
    }

    // strict is for saving: bad coordinates stop it and say so. A preview simply keeps the saved ones.
    Settings Collect(bool strict)
    {
        Settings n = orig.Clone();
        n.Language = sLang.SelectedIndex == 1 ? "ar" : "en";
        n.Theme = themeKey;
        n.LocMode = sLoc.SelectedIndex == 1 ? "manual" : "auto";
        n.City = tCity.Box.Text.Trim();
        n.Country = country;
        n.Method = dMethod.SelectedIndex <= 0 ? "auto" : PrayCalc.Methods[dMethod.SelectedIndex - 1].Key;
        n.Asr = dAsr.SelectedIndex == 1 ? 2 : 1;
        n.HighLat = dHL.SelectedIndex;
        n.RemindBefore = nRemind.Value;
        n.Duration = nDur.Value;
        n.Use24h = sFmt.SelectedIndex == 1;
        n.PopupOnMouseScreen = sScreen.SelectedIndex == 1;
        n.HijriAdjust = nHijri.Value;
        n.Startup = kStart.Checked;
        n.DndFullscreen = kDnd.Checked;
        n.Imsak = kImsak.Checked;
        n.ImsakMinutes = nImsak.Value;
        n.FastNotice = kFast.Checked;
        n.AdhkarMorning = kM.Checked;
        n.AdhkarEvening = kE.Checked;
        n.AdhkarAfter = kA.Checked;
        n.AdhkarAfterMinutes = nAfter.Value;
        n.AdhkarSleep = kS.Checked;
        n.SleepMinute = nSleep.Value;
        foreach (int i in Scheduler.Five)
        {
            n.RemindBy[i] = kRemind[i].Checked;
            n.SoundBy[i] = kSound[i].Checked;
            n.Offsets[i] = nOffset[i].Value;
            n.Iqama[i] = nIqama[i].Value;
        }

        double lat, lon;
        bool blank = tLat.Box.Text.Trim() == "" && tLon.Box.Text.Trim() == "";
        if (ParseCoord(tLat.Box.Text, 90, out lat) && ParseCoord(tLon.Box.Text, 180, out lon))
        {
            n.Lat = lat;
            n.Lon = lon;
            n.HasLoc = true;
            if (hasBase) n.Country = LocationPolicy.CountryFor(country, baseLat, baseLon, lat, lon);
        }
        else if (blank && n.LocMode == "auto")
        {
            n.HasLoc = false;
        }
        else if (strict)
        {
            SetNote(lError, Lang.T("badCoords"));
            nav.SelectedIndex = 1;
            tLat.Box.Focus();
            return null;
        }
        return n;
    }
}
