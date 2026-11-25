using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

public class AdhkarForm : Form
{
    // How far through the set you are.
    class ProgressLine : Control
    {
        readonly Theme th;
        readonly Tween move;
        double from, to;

        public ProgressLine(Theme th, float s)
        {
            this.th = th;
            move = new Tween(240, Invalidate, 1);
            Height = Math.Max(3, (int)(4 * s));
            TabStop = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        public void SetValue(double value, bool animate)
        {
            from = Current();
            to = value;
            if (animate) move.Play(); else { from = value; move.Snap(1); Invalidate(); }
        }

        double Current() { return from + (to - from) * Ease.OutCubic(move.Value); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(th.Bg);
            using (GraphicsPath p = PopupForm.RoundRect(new Rectangle(0, 0, Width - 1, Height - 1), Height / 2))
            using (var b = new SolidBrush(th.Surface))
                g.FillPath(b, p);
            int w = (int)(Width * Current());
            if (w < Height) return;
            var r = Kit.Rtl(this) ? new Rectangle(Width - w, 0, w - 1, Height - 1) : new Rectangle(0, 0, w - 1, Height - 1);
            using (GraphicsPath p = PopupForm.RoundRect(r, Height / 2))
            using (var b = new SolidBrush(th.Accent))
                g.FillPath(b, p);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) move.Stop();
            base.Dispose(disposing);
        }
    }

    static readonly string[] SetKeys = { "setM", "setE", "setA", "setS" };

    readonly Theme th;
    readonly bool ar;
    readonly Label lProgress, lText, lSource;
    readonly Panel scroller;
    readonly Segmented sets;
    readonly ProgressLine line;
    readonly ThemedButton bCount, bPrev, bNext;
    readonly Font bodyFont;
    readonly Tween textFade;
    readonly Action<AdhkarSet> onFinished;
    List<Dhikr> items = new List<Dhikr>();
    int[] remaining = new int[0];
    AdhkarSet current;
    int index;

    public AdhkarForm(Settings s, AdhkarSet set, Action<AdhkarSet> onFinished)
    {
        th = Theme.Resolve(s.Theme);
        ar = s.Language == "ar";
        this.onFinished = onFinished;

        float S;
        using (Graphics g = CreateGraphics()) S = g.DpiX / 96f;
        SuspendLayout();
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        StartPosition = FormStartPosition.Manual;
        Load += (o, e) => ThemeUi.Center(this);
        ClientSize = new Size((int)(620 * S), (int)(540 * S));
        MinimumSize = new Size((int)(500 * S), (int)(400 * S));
        KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (ArgumentException) { }
        if (ar) { RightToLeft = RightToLeft.Yes; RightToLeftLayout = true; }
        ThemeUi.Frame(this, th);

        // Traditional Naskh reads best with full tashkeel; Segoe UI is the fallback when it isn't installed.
        bodyFont = new Font("Sakkal Majalla", 20f);
        if (bodyFont.Name != "Sakkal Majalla") { bodyFont.Dispose(); bodyFont = new Font("Segoe UI", 15f); }
        textFade = new Tween(170, () => { if (lText != null) lText.ForeColor = ThemeUi.Blend(th.Bg, th.Fg, Ease.OutCubic(textFade.Value)); }, 1);

        int pad = (int)(20 * S);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(pad), BackColor = th.Bg };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        string[] names = new string[SetKeys.Length];
        for (int i = 0; i < names.Length; i++) names[i] = Lang.T(SetKeys[i], ar);
        sets = new Segmented(th, S, ClientSize.Width - 2 * pad, true, names) { Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, (int)(14 * S)), AccessibleName = Lang.T("adhkar", ar) };
        sets.Init((int)set);
        sets.SelectedIndexChanged += (o, e) => { LoadSet((AdhkarSet)sets.SelectedIndex); ActiveControl = bCount; };

        var progress = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, 0, 0, (int)(14 * S)), BackColor = th.Bg };
        progress.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progress.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        line = new ProgressLine(th, S) { Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(0, (int)(8 * S), (int)(12 * S), 0) };
        lProgress = new Label { AutoSize = true, ForeColor = th.Muted, Anchor = AnchorStyles.None, Margin = Padding.Empty };
        progress.Controls.Add(line, 0, 0);
        progress.Controls.Add(lProgress, 1, 0);

        scroller = new Panel { Dock = DockStyle.Fill, BackColor = th.Bg, Margin = Padding.Empty, TabStop = false, Cursor = Cursors.Hand };
        // Text wraps to the width, so it only ever needs to scroll up and down.
        scroller.HorizontalScroll.Enabled = false;
        scroller.HorizontalScroll.Visible = false;
        scroller.HorizontalScroll.Maximum = 0;
        scroller.AutoScroll = true;
        lText = new Label { AutoSize = true, Font = bodyFont, ForeColor = th.Fg, RightToLeft = RightToLeft.Yes, TextAlign = ContentAlignment.TopRight, UseMnemonic = false, Cursor = Cursors.Hand };
        scroller.Controls.Add(lText);
        scroller.Resize += (o, e) => FitText();
        // Clicking the text counts too, so your eyes can stay on the words.
        lText.Click += (o, e) => Count();
        scroller.Click += (o, e) => Count();
        lSource = new Label { AutoSize = true, ForeColor = th.Muted, Margin = new Padding(0, (int)(10 * S), 0, (int)(12 * S)) };

        int buttonH = (int)(42 * S);
        bPrev = new ThemedButton(th, ButtonKind.Secondary, S, Lang.T("prev", ar)) { Size = new Size((int)(100 * S), buttonH), Margin = new Padding(0, 0, (int)(8 * S), 0) };
        bNext = new ThemedButton(th, ButtonKind.Secondary, S, Lang.T("nextItem", ar)) { Size = new Size((int)(100 * S), buttonH), Margin = new Padding(0, 0, (int)(8 * S), 0) };
        bCount = new ThemedButton(th, ButtonKind.Primary, S, "") { Size = new Size((int)(240 * S), buttonH), Margin = Padding.Empty };
        bPrev.Click += (o, e) => Go(index - 1);
        bNext.Click += (o, e) => Go(index + 1);
        bCount.Click += (o, e) => Count();
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, BackColor = th.Bg };
        buttons.Controls.Add(bPrev);
        buttons.Controls.Add(bNext);
        buttons.Controls.Add(bCount);

        layout.Controls.Add(sets, 0, 0);
        layout.Controls.Add(progress, 0, 1);
        layout.Controls.Add(scroller, 0, 2);
        layout.Controls.Add(lSource, 0, 3);
        layout.Controls.Add(buttons, 0, 4);
        Controls.Add(layout);
        AcceptButton = bCount;
        ActiveControl = bCount;
        ResumeLayout(true);
        ThemeUi.ScrollBars(scroller, th);
        LoadSet(set);
    }

    void LoadSet(AdhkarSet set)
    {
        current = set;
        Text = Lang.T(Adhkar.TitleKey(set), ar);
        items = Adhkar.Load(set);
        remaining = new int[items.Count];
        for (int i = 0; i < items.Count; i++) remaining[i] = items[i].Repeat;
        ShowItem(0);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (sets.Focused && e.KeyCode != Keys.Escape) { base.OnKeyDown(e); return; }
        // Arrows follow the reading direction: in Arabic, left moves forward.
        int forward = ar ? (int)Keys.Left : (int)Keys.Right, back = ar ? (int)Keys.Right : (int)Keys.Left;
        if ((int)e.KeyCode == forward) { Go(index + 1); e.Handled = true; }
        else if ((int)e.KeyCode == back) { Go(index - 1); e.Handled = true; }
        else if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
        base.OnKeyDown(e);
    }

    void FitText()
    {
        int w = scroller.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 4;
        if (w > 0) lText.MaximumSize = new Size(w, 0);
        // A right-to-left panel scrolls from the right edge, so the label stays on the reading side.
        lText.Location = new Point(Math.Max(0, scroller.ClientSize.Width - lText.Width - 1), scroller.AutoScrollPosition.Y);
    }

    void Go(int i)
    {
        if (i >= 0 && i < items.Count) ShowItem(i);
    }

    void Count()
    {
        if (items.Count == 0 || index >= items.Count) { Close(); return; }
        if (remaining[index] > 0) remaining[index]--;
        if (remaining[index] > 0) { UpdateControls(true); return; }
        if (index + 1 < items.Count) ShowItem(index + 1);
        else Finish();
    }

    void Finish()
    {
        lText.Text = Lang.T("allDone", ar);
        lSource.Text = "";
        lProgress.Text = string.Format(Lang.T("of", ar), items.Count, items.Count);
        line.SetValue(1, true);
        bCount.Text = Lang.T("close", ar);
        bCount.SnapProgress(0);
        index = items.Count;
        bPrev.Enabled = true;
        bNext.Enabled = false;
        textFade.Play();
        FitText();
        onFinished(current);
    }

    void ShowItem(int i)
    {
        if (items.Count == 0)
        {
            lText.Text = Lang.T("noAdhkar", ar);
            bCount.Text = Lang.T("close", ar);
            bPrev.Enabled = false;
            bNext.Enabled = false;
            return;
        }
        index = i;
        if (remaining[i] == 0) remaining[i] = items[i].Repeat; // reading again from the start of this one
        lText.Text = items[i].Text;
        scroller.AutoScrollPosition = new Point(0, 0);
        bCount.SnapProgress(1 - remaining[i] / (double)items[i].Repeat);
        UpdateControls(false);
        textFade.Play();
        FitText();
    }

    void UpdateControls(bool counting)
    {
        lProgress.Text = string.Format(Lang.T("of", ar), index + 1, items.Count);
        lSource.Text = string.Format(Lang.T("source", ar), items[index].Id);
        bCount.Text = string.Format(Lang.T("count", ar), remaining[index]);
        if (counting) bCount.Progress = 1 - remaining[index] / (double)items[index].Repeat;
        line.SetValue(index / (double)items.Count, IsHandleCreated);
        bPrev.Enabled = index > 0;
        bNext.Enabled = index + 1 < items.Count;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { textFade.Stop(); bodyFont.Dispose(); }
        base.Dispose(disposing);
    }
}
