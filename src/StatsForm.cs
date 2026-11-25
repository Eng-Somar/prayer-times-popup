using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

// A marks table you can correct and page back through, then a plain summary. Every number is written out as text.
public class StatsForm : Form
{
    // A real check box, so keyboard, focus and screen readers work; only its look is custom.
    class MarkCell : CheckBox
    {
        readonly Theme th;
        readonly float s;
        readonly Tween fill, hot;
        public int Column, Index;

        public MarkCell(Theme th, float s)
        {
            this.th = th;
            this.s = s;
            fill = new Tween(190, Invalidate);
            hot = new Tween(110, Invalidate);
            AutoSize = false;
            Size = new Size((int)(30 * s), (int)(30 * s));
            Margin = new Padding(0);
            Anchor = AnchorStyles.None;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        public void SnapFill() { fill.Snap(Checked ? 1 : 0); Invalidate(); }

        protected override void OnCheckedChanged(EventArgs e) { fill.To(Checked ? 1 : 0); base.OnCheckedChanged(e); }
        protected override void OnChangeUICues(UICuesEventArgs e) { Invalidate(); base.OnChangeUICues(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnMouseEnter(EventArgs e) { hot.To(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hot.To(0); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(th.Bg);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = Width / 2f, cy = Height / 2f;
            if (!Enabled && !Checked)
            {
                float dot = 2f * s; // this prayer hasn't happened yet, or doesn't occur here
                using (var b = new SolidBrush(th.Muted)) g.FillEllipse(b, cx - dot / 2, cy - dot / 2, dot, dot);
                return;
            }
            float r = 8f * s, hover = (float)Ease.OutCubic(hot.Value);
            if (hover > 0)
                using (var b = new SolidBrush(ThemeUi.Blend(th.Bg, th.Surface, hover))) g.FillEllipse(b, cx - r - 3 * s, cy - r - 3 * s, 2 * r + 6 * s, 2 * r + 6 * s);
            using (var p = new Pen(ThemeUi.Blend(th.Muted, th.Fg, hover), 1.5f * s)) g.DrawEllipse(p, cx - r, cy - r, 2 * r, 2 * r);
            float fr = r * (float)Ease.OutBack(fill.Value);
            if (fill.Value > 0)
                using (var b = new SolidBrush(th.Accent)) g.FillEllipse(b, cx - fr, cy - fr, 2 * fr, 2 * fr);
            if (Focused && ShowFocusCues)
                using (var p = new Pen(th.Fg, 1f * s)) g.DrawEllipse(p, cx - r - 3 * s, cy - r - 3 * s, 2 * r + 6 * s, 2 * r + 6 * s);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { fill.Stop(); hot.Stop(); }
            base.Dispose(disposing);
        }
    }

    const int Days = 7;
    static readonly DateTime NotFriday = new DateTime(2026, 11, 2); // Dhuhr keeps its own name in the table

    readonly Theme th;
    readonly bool ar;
    readonly PrayerStats stats;
    readonly Func<DateTime, DateTime[]> times;
    readonly Action<DateTime, int, bool> setPrayed;
    readonly Action clear;
    readonly Font fHead;
    readonly ToolTip tip = new ToolTip();
    readonly List<MarkCell> cells = new List<MarkCell>();
    readonly Label[] dayHeads = new Label[Days];
    readonly Dictionary<int, Label> rowHeads = new Dictionary<int, Label>();
    readonly Label[,] summary = new Label[3, 3];
    readonly Label lStreak, lWeek;
    readonly ThemedButton bClear, bOlder, bNewer;
    int weekOffset, hoverColumn = -1, hoverIndex = -1;

    public StatsForm(Settings s, PrayerStats stats, Func<DateTime, DateTime[]> times, Action<DateTime, int, bool> setPrayed, Action clear)
    {
        th = Theme.Resolve(s.Theme);
        ar = s.Language == "ar";
        this.stats = stats;
        this.times = times;
        this.setPrayed = setPrayed;
        this.clear = clear;
        float S;
        using (Graphics g = CreateGraphics()) S = g.DpiX / 96f;
        Func<float, int> P = v => (int)Math.Round(v * S);

        SuspendLayout();
        Text = Lang.T("stats", ar);
        Font = new Font("Segoe UI", 10f);
        fHead = new Font("Segoe UI Semibold", 11f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.Manual;
        Load += (o, e) => ThemeUi.Center(this);
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (ArgumentException) { }
        if (ar) { RightToLeft = RightToLeft.Yes; RightToLeftLayout = true; }
        ThemeUi.Frame(this, th);

        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(P(20)), BackColor = th.Bg };

        // Heading with arrows to page back through older weeks.
        var nav = new TableLayoutPanel { ColumnCount = 3, RowCount = 1, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, P(6)), BackColor = th.Bg };
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        nav.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        lWeek = new Label { AutoSize = true, Font = fHead, ForeColor = th.Fg, Anchor = AnchorStyles.Left, Margin = Padding.Empty };
        bOlder = new ThemedButton(th, ButtonKind.Ghost, S, Lang.T("prevWeek", ar)) { Chevron = -1, Size = new Size(P(32), P(30)), Margin = Padding.Empty };
        bNewer = new ThemedButton(th, ButtonKind.Ghost, S, Lang.T("nextWeek", ar)) { Chevron = 1, Size = new Size(P(32), P(30)), Margin = Padding.Empty };
        bOlder.Click += (o, e) => { weekOffset++; Reload(); };
        bNewer.Click += (o, e) => { if (weekOffset > 0) weekOffset--; Reload(); };
        tip.SetToolTip(bOlder, Lang.T("prevWeek", ar));
        tip.SetToolTip(bNewer, Lang.T("nextWeek", ar));
        nav.Controls.Add(lWeek, 0, 0);
        nav.Controls.Add(bOlder, 1, 0);
        nav.Controls.Add(bNewer, 2, 0);
        root.Controls.Add(nav);

        // Marks: oldest to newest in reading direction.
        var grid = new TableLayoutPanel { ColumnCount = Days + 1, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, P(20)), BackColor = th.Bg };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        for (int c = 0; c < Days; c++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, P(62)));
        grid.Controls.Add(new Label { AutoSize = true });
        for (int c = 0; c < Days; c++)
        {
            dayHeads[c] = new Label { AutoSize = true, Anchor = AnchorStyles.None, TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(0, 0, 0, P(4)) };
            grid.Controls.Add(dayHeads[c]);
        }
        foreach (int i in Scheduler.Five)
        {
            var head = new Label { Text = Lang.Prayer(i, NotFriday, ar), AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = th.Muted, Margin = new Padding(0, P(5), P(14), P(5)) };
            rowHeads[i] = head;
            grid.Controls.Add(head);
            for (int c = 0; c < Days; c++)
            {
                var cell = new MarkCell(th, S) { Column = c, Index = i };
                cell.Click += OnCellClick;
                cell.MouseEnter += (o, e) => Highlight(cell.Column, cell.Index);
                cell.MouseLeave += (o, e) => Highlight(-1, -1);
                cells.Add(cell);
                grid.Controls.Add(cell);
            }
        }
        root.Controls.Add(grid);

        root.Controls.Add(new Label { Text = Lang.T("statsSummary", ar), AutoSize = true, Font = fHead, ForeColor = th.Fg, Margin = new Padding(0, 0, 0, P(8)) });
        var table = new TableLayoutPanel { ColumnCount = 4, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, BackColor = th.Bg, Margin = new Padding(0, 0, 0, P(10)) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, P(190)));
        for (int c = 0; c < 3; c++) table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, P(104)));
        table.Controls.Add(new Label { AutoSize = true });
        foreach (string key in new[] { "colToday", "col7", "col30" })
            table.Controls.Add(new Label { Text = Lang.T(key, ar), AutoSize = true, ForeColor = th.Muted, Anchor = AnchorStyles.None, Margin = new Padding(0, 0, 0, P(4)) });
        string[] rowKeys = { "statPrayers", "statTasbih", "statAdhkar" };
        for (int row = 0; row < 3; row++)
        {
            table.Controls.Add(new Label { Text = Lang.T(rowKeys[row], ar), AutoSize = true, ForeColor = th.Fg, Anchor = AnchorStyles.Left, Margin = new Padding(0, P(4), 0, P(4)) });
            for (int c = 0; c < 3; c++)
            {
                summary[row, c] = new Label { AutoSize = true, ForeColor = th.Fg, Anchor = AnchorStyles.None, Margin = new Padding(0, P(4), 0, P(4)) };
                table.Controls.Add(summary[row, c]);
            }
        }
        root.Controls.Add(table);

        lStreak = new Label { AutoSize = true, ForeColor = th.Fg, Margin = new Padding(0, 0, 0, P(10)) };
        root.Controls.Add(lStreak);
        root.Controls.Add(new Label { Text = Lang.T("statsHint", ar), AutoSize = true, ForeColor = th.Muted, MaximumSize = new Size(P(500), 0), Margin = new Padding(0, 0, 0, P(14)) });

        bClear = new ThemedButton(th, ButtonKind.Secondary, S, Lang.T("statsClear", ar)) { Margin = Padding.Empty };
        bClear.Font = Font;
        bClear.Fit();
        bClear.Click += OnClear;
        root.Controls.Add(bClear);

        Controls.Add(root);
        ResumeLayout(true);
        Activated += (o, e) => Reload();
        Reload();
        foreach (MarkCell cell in cells) cell.SnapFill();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
        base.OnKeyDown(e);
    }

    // Lights up the row and column of the cell under the pointer, so a mark is easy to read across the table.
    void Highlight(int column, int index)
    {
        hoverColumn = column;
        hoverIndex = index;
        UpdateHeads();
    }

    void UpdateHeads()
    {
        DateTime today = DateTime.Now.Date;
        for (int c = 0; c < Days; c++)
        {
            DateTime day = StatsPage.Day(today, weekOffset, c);
            dayHeads[c].ForeColor = day == today || c == hoverColumn ? th.Fg : th.Muted;
            dayHeads[c].Font = day == today ? fHead : Font;
        }
        foreach (KeyValuePair<int, Label> kv in rowHeads)
            kv.Value.ForeColor = kv.Key == hoverIndex ? th.Fg : th.Muted;
    }

    void OnCellClick(object sender, EventArgs e)
    {
        var cell = (MarkCell)sender;
        setPrayed(StatsPage.Day(DateTime.Now.Date, weekOffset, cell.Column), cell.Index, cell.Checked);
        Reload();
    }

    void OnClear(object sender, EventArgs e)
    {
        DialogResult answer = MessageBox.Show(this, Lang.T("statsClearAsk", ar), Lang.T("stats", ar), MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2,
            ar ? MessageBoxOptions.RightAlign | MessageBoxOptions.RtlReading : 0);
        if (answer != DialogResult.Yes) return;
        clear();
        Reload();
    }

    // Also runs when the window is reactivated, so one left open overnight moves on to the new day.
    public void Reload()
    {
        DateTime now = DateTime.Now, today = now.Date;
        var dayTimes = new DateTime[Days][];
        for (int c = 0; c < Days; c++)
        {
            DateTime day = StatsPage.Day(today, weekOffset, c);
            dayTimes[c] = times(day);
            dayHeads[c].Text = Lang.DayName(day, ar) + "\n" + day.Day.ToString(CultureInfo.InvariantCulture);
        }
        UpdateHeads();
        foreach (MarkCell cell in cells)
        {
            DateTime day = StatsPage.Day(today, weekOffset, cell.Column);
            cell.Checked = stats.IsPrayed(day, cell.Index);
            cell.Enabled = cell.Checked || Scheduler.CanMark(now, dayTimes[cell.Column][cell.Index]);
            string name = Lang.Prayer(cell.Index, NotFriday, ar) + ", " + Lang.DayName(day, ar) + " " + day.Day.ToString(CultureInfo.InvariantCulture);
            cell.Text = name;
            tip.SetToolTip(cell, name);
            cell.Invalidate();
        }
        DateTime first = StatsPage.Day(today, weekOffset, 0), last = StatsPage.Day(today, weekOffset, Days - 1);
        lWeek.Text = weekOffset == 0
            ? Lang.T("statsWeek", ar)
            : string.Format(Lang.T("weekRange", ar), first.ToString("d/M", CultureInfo.InvariantCulture), last.ToString("d/M", CultureInfo.InvariantCulture));
        bNewer.Enabled = weekOffset > 0;
        int[] spans = { 1, 7, 30 };
        for (int c = 0; c < 3; c++)
        {
            StatsRange r = stats.Range(today, spans[c]);
            summary[0, c].Text = string.Format(Lang.T("of", ar), r.Prayed, r.Possible);
            summary[1, c].Text = r.Tasbih.ToString("N0", CultureInfo.InvariantCulture);
            summary[2, c].Text = r.Adhkar.ToString(CultureInfo.InvariantCulture);
        }
        lStreak.Text = string.Format(Lang.T("statStreak", ar), stats.FullDaysStreak(today));
        bClear.Enabled = !stats.IsEmpty;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { fHead.Dispose(); tip.Dispose(); }
        base.Dispose(disposing);
    }
}
