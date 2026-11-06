using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

public class SettingsForm : Form
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly string[] HLKeys = { "hl0", "hl1", "hl2" };

    readonly TrayApp app;
    readonly Settings orig;
    readonly float S;
    readonly ComboBox cLang, cTheme, cLoc, cMethod, cAsr, cHL, cFmt;
    readonly TextBox tCity, tLat, tLon;
    readonly NumericUpDown nRemind, nDur;
    readonly CheckBox kSound, kStart;
    readonly Button bDetect, bPreview, bSave, bCancel;
    readonly Label lStatus;
    readonly TableLayoutPanel grid;
    string country;

    public SettingsForm(TrayApp app, Settings s)
    {
        this.app = app;
        orig = s;
        country = s.Country;
        bool ar = Lang.Ar;

        SuspendLayout();
        Text = Lang.T("settings") + " — " + Lang.T("app");
        Font = new Font("Segoe UI", 9.75f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ShowInTaskbar = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        if (ar) { RightToLeft = RightToLeft.Yes; RightToLeftLayout = true; }
        using (Graphics g = CreateGraphics()) S = g.DpiX / 96f;
        int fieldW = (int)(300 * S);

        grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding((int)(14 * S)), Dock = DockStyle.Fill };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        Controls.Add(grid);

        cLang = Combo(fieldW, "English", "العربية");
        cLang.SelectedIndex = s.Language == "ar" ? 1 : 0;
        Row(Lang.T("language"), cLang);

        cTheme = Combo(fieldW - (int)(96 * S));
        cTheme.Items.Add(Lang.T("autoTheme"));
        foreach (Theme t in Theme.All) cTheme.Items.Add(t.Name(ar));
        cTheme.SelectedIndex = 0;
        for (int i = 0; i < Theme.All.Length; i++) if (Theme.All[i].Key == s.Theme) cTheme.SelectedIndex = i + 1;
        bPreview = new Button { Text = Lang.T("preview"), AutoSize = true, Margin = new Padding(6, 3, 0, 3) };
        bPreview.Click += (o, e) => { Settings n = Collect(false); if (n != null) app.Preview(n); };
        Row(Lang.T("theme"), Flow(cTheme, bPreview));

        cLoc = Combo(fieldW, Lang.T("locAuto"), Lang.T("locManual"));
        cLoc.SelectedIndex = s.LocMode == "manual" ? 1 : 0;
        cLoc.SelectedIndexChanged += (o, e) => SyncLocFields();
        Row(Lang.T("location"), cLoc);

        tCity = new TextBox { Width = fieldW, Text = s.City };
        Row(Lang.T("city"), tCity);
        tLat = new TextBox { Width = fieldW, Text = s.HasLoc ? s.Lat.ToString("0.######", Inv) : "" };
        Row(Lang.T("lat"), tLat);
        tLon = new TextBox { Width = fieldW, Text = s.HasLoc ? s.Lon.ToString("0.######", Inv) : "" };
        Row(Lang.T("lon"), tLon);

        bDetect = new Button { Text = Lang.T("detect"), AutoSize = true, Margin = new Padding(0, 3, 6, 3) };
        bDetect.Click += (o, e) => DetectNow();
        lStatus = new Label { AutoSize = true, ForeColor = SystemColors.GrayText, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 0, 0) };
        Row("", Flow(bDetect, lStatus));

        cMethod = Combo(fieldW);
        cMethod.Items.Add(Lang.T("autoMethod"));
        foreach (MethodDef m in PrayCalc.Methods) cMethod.Items.Add(m.Name(ar));
        cMethod.SelectedIndex = 0;
        for (int i = 0; i < PrayCalc.Methods.Length; i++) if (PrayCalc.Methods[i].Key == s.Method) cMethod.SelectedIndex = i + 1;
        Row(Lang.T("method"), cMethod);

        cAsr = Combo(fieldW, Lang.T("asrStd"), Lang.T("asrHanafi"));
        cAsr.SelectedIndex = s.Asr == 2 ? 1 : 0;
        Row(Lang.T("asr"), cAsr);

        cHL = Combo(fieldW, Lang.T(HLKeys[0]), Lang.T(HLKeys[1]), Lang.T(HLKeys[2]));
        cHL.SelectedIndex = Math.Max(0, Math.Min(2, s.HighLat));
        Row(Lang.T("highLat"), cHL);

        nRemind = new NumericUpDown { Minimum = 0, Maximum = 60, Value = Math.Max(0, Math.Min(60, s.RemindBefore)), Width = (int)(90 * S) };
        Row(Lang.T("remind"), nRemind);
        nDur = new NumericUpDown { Minimum = 0, Maximum = 600, Value = Math.Max(0, Math.Min(600, s.Duration)), Width = (int)(90 * S) };
        Row(Lang.T("duration"), nDur);

        cFmt = Combo(fieldW, Lang.T("t12"), Lang.T("t24"));
        cFmt.SelectedIndex = s.Use24h ? 1 : 0;
        Row(Lang.T("timeFmt"), cFmt);

        kSound = new CheckBox { Text = Lang.T("sound"), AutoSize = true, Checked = s.Sound };
        Row("", kSound);
        kStart = new CheckBox { Text = Lang.T("startup"), AutoSize = true, Checked = s.Startup };
        Row("", kStart);

        bSave = new Button { Text = Lang.T("save"), AutoSize = true, MinimumSize = new Size((int)(90 * S), 0) };
        bCancel = new Button { Text = Lang.T("cancel"), AutoSize = true, MinimumSize = new Size((int)(90 * S), 0) };
        bSave.Click += (o, e) => { Settings n = Collect(true); if (n != null) { app.ApplySettings(n); Close(); } };
        bCancel.Click += (o, e) => Close();
        AcceptButton = bSave;
        CancelButton = bCancel;
        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, (int)(12 * S), 0, 0) };
        buttons.Controls.Add(bCancel);
        buttons.Controls.Add(bSave);
        grid.Controls.Add(buttons);
        grid.SetColumnSpan(buttons, 2);

        SyncLocFields();
        ResumeLayout(true);
    }

    ComboBox Combo(int width, params string[] items)
    {
        var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = width };
        c.Items.AddRange(items);
        return c;
    }

    FlowLayoutPanel Flow(params Control[] cs)
    {
        var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        f.Controls.AddRange(cs);
        return f;
    }

    void Row(string label, Control c)
    {
        var l = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, (int)(16 * S), 6) };
        c.Anchor = AnchorStyles.Left;
        if (!(c is FlowLayoutPanel)) c.Margin = new Padding(0, 4, 0, 4);
        grid.Controls.Add(l);
        grid.Controls.Add(c);
    }

    void SyncLocFields()
    {
        bool manual = cLoc.SelectedIndex == 1;
        tCity.ReadOnly = !manual;
        tLat.ReadOnly = !manual;
        tLon.ReadOnly = !manual;
    }

    void DetectNow()
    {
        bDetect.Enabled = false;
        lStatus.Text = Lang.T("detecting");
        ThreadPool.QueueUserWorkItem(_ =>
        {
            GeoResult r = null;
            try { r = Locator.Detect(); } catch { }
            try { BeginInvoke((Action)(() => OnDetected(r))); } catch { }
        });
    }

    void OnDetected(GeoResult r)
    {
        if (IsDisposed) return;
        bDetect.Enabled = true;
        if (r == null) { lStatus.Text = Lang.T("detectFail"); return; }
        tLat.Text = r.Lat.ToString("0.######", Inv);
        tLon.Text = r.Lon.ToString("0.######", Inv);
        if (!string.IsNullOrEmpty(r.City)) tCity.Text = r.City;
        if (!string.IsNullOrEmpty(r.Country)) country = r.Country;
        lStatus.Text = "✓ " + (string.IsNullOrEmpty(r.City) ? tLat.Text + ", " + tLon.Text : r.City);
    }

    static bool ParseCoord(string text, double limit, out double v)
    {
        return double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, Inv, out v) && Math.Abs(v) <= limit;
    }

    Settings Collect(bool strict)
    {
        Settings n = orig.Clone();
        n.Language = cLang.SelectedIndex == 1 ? "ar" : "en";
        n.Theme = cTheme.SelectedIndex <= 0 ? "auto" : Theme.All[cTheme.SelectedIndex - 1].Key;
        n.LocMode = cLoc.SelectedIndex == 1 ? "manual" : "auto";
        n.City = tCity.Text.Trim();
        n.Country = country;
        n.Method = cMethod.SelectedIndex <= 0 ? "auto" : PrayCalc.Methods[cMethod.SelectedIndex - 1].Key;
        n.Asr = cAsr.SelectedIndex == 1 ? 2 : 1;
        n.HighLat = cHL.SelectedIndex;
        n.RemindBefore = (int)nRemind.Value;
        n.Duration = (int)nDur.Value;
        n.Use24h = cFmt.SelectedIndex == 1;
        n.Sound = kSound.Checked;
        n.Startup = kStart.Checked;

        double lat, lon;
        bool blank = tLat.Text.Trim() == "" && tLon.Text.Trim() == "";
        if (ParseCoord(tLat.Text, 90, out lat) && ParseCoord(tLon.Text, 180, out lon))
        {
            n.Lat = lat; n.Lon = lon; n.HasLoc = true;
        }
        else if (blank && n.LocMode == "auto")
        {
            n.HasLoc = false;
        }
        else if (strict || !blank)
        {
            MessageBox.Show(this, Lang.T("badCoords"), Lang.T("app"), MessageBoxButtons.OK, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button1,
                Lang.Ar ? MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading : 0);
            return null;
        }
        return n;
    }
}
