using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

// A small floating misbaha. It never takes focus, so you can count while typing elsewhere. Each tap fills
// the next bead, and finishing a round sends a wave around the ring.
public class TasbihForm : Form
{
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, int mods, int vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);

    const int HotkeyId = 1, ModAlt = 1, ModControl = 2, ModShift = 4, ModNoRepeat = 0x4000, VkSpace = 0x20, WmHotkey = 0x0312;
    const int HoldMs = 650, WaveBeads = 5;
    static readonly int[] Targets = { 33, 99, 100, 1000 };
    static readonly string FilePath = Path.Combine(Settings.Dir, "tasbih.ini");

    readonly Settings s;
    readonly Theme th;
    readonly bool ar;
    readonly float S;
    readonly TasbihModel model;
    readonly Font fPhrase, fCount, fTarget, fSmall;
    readonly Rectangle closeRect, resetRect;
    readonly Timer saveTimer = new Timer(), holdTimer = new Timer(), hintTimer = new Timer();
    readonly ToolTip tip = new ToolTip { ShowAlways = true };
    readonly Action<int, int> onMoved;
    readonly Action onTap;
    readonly Tween hoverT, downT, rollT, popT, waveT, phraseT, holdT, closeT, resetT;
    Point downAt;
    bool pressed, dragging, closeHover, resetHover, holding, holdDone, drain, showHint;
    string previousCount = "";
    int previousBeads;

    public TasbihForm(Settings s, Action<int, int> onMoved, Action onTap)
    {
        this.s = s;
        this.onMoved = onMoved;
        this.onTap = onTap;
        th = Theme.Resolve(s.Theme);
        ar = s.Language == "ar";
        model = LoadModel();

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = th.Bg;
        Text = Lang.T("tasbih", ar);
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        using (Graphics g = CreateGraphics()) S = g.DpiX / 96f;

        fPhrase = new Font("Segoe UI Semibold", 10.5f);
        fCount = new Font("Segoe UI Semibold", 26f);
        fTarget = new Font("Segoe UI", 9f);
        fSmall = new Font("Segoe UI", 8.25f);

        Size = new Size(P(216), P(240));
        Location = StartLocation();
        closeRect = Mirror(new Rectangle(Width - P(8) - P(24), P(6), P(24), P(24)));
        resetRect = Mirror(new Rectangle(P(8), P(6), P(24), P(24)));

        Action repaint = Invalidate;
        hoverT = new Tween(150, repaint);
        downT = new Tween(70, repaint);
        rollT = new Tween(150, repaint, 1);
        popT = new Tween(240, repaint, 1);
        waveT = new Tween(620, repaint, 1);
        phraseT = new Tween(260, repaint, 1);
        holdT = new Tween(HoldMs, repaint);
        closeT = new Tween(120, repaint);
        resetT = new Tween(120, repaint);

        saveTimer.Interval = 1500;
        saveTimer.Tick += (o, e) => { saveTimer.Stop(); Save(); };
        holdTimer.Interval = HoldMs;
        holdTimer.Tick += (o, e) => { holdTimer.Stop(); holdDone = true; holdT.Snap(0); DoReset(); };
        hintTimer.Interval = 2200;
        hintTimer.Tick += (o, e) => { hintTimer.Stop(); showHint = false; Invalidate(); };
        ContextMenuStrip = BuildMenu();
        UpdateAccessibleName();
    }

    int P(float v) { return (int)Math.Round(v * S); }

    Rectangle Mirror(Rectangle r) { return ar ? new Rectangle(Width - r.Right, r.Y, r.Width, r.Height) : r; }

    Point StartLocation()
    {
        var p = new Point(s.TasbihX, s.TasbihY);
        foreach (Screen sc in Screen.AllScreens)
            if (s.TasbihX >= 0 && sc.WorkingArea.Contains(new Rectangle(p, Size))) return p;
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        return new Point(wa.Right - Width - P(16), wa.Bottom - Height - P(16));
    }

    static TasbihModel LoadModel()
    {
        try { if (File.Exists(FilePath)) return TasbihModel.Parse(File.ReadAllText(FilePath, Encoding.UTF8), DateTime.Now); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return new TasbihModel();
    }

    void Save()
    {
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(FilePath, model.Serialize(), Encoding.UTF8);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Renderer = new MenuRenderer(th);
        menu.RightToLeft = ar ? RightToLeft.Yes : RightToLeft.No;
        menu.Opening += (o, e) =>
        {
            menu.Items.Clear();
            var seq = new ToolStripMenuItem(Lang.T("seqAfter", ar), null, (a, b) => SetMode(0, model.FreePhrase, model.FreeTarget));
            seq.Checked = model.Mode == 0;
            menu.Items.Add(seq);
            menu.Items.Add(new ToolStripSeparator());
            string[] phrases = ar ? TasbihModel.PhrasesAr : TasbihModel.PhrasesEn;
            for (int i = 0; i < phrases.Length; i++)
            {
                int index = i;
                var it = new ToolStripMenuItem(phrases[i], null, (a, b) => SetMode(1, index, model.FreeTarget));
                it.Checked = model.Mode == 1 && model.FreePhrase == i;
                menu.Items.Add(it);
            }
            menu.Items.Add(new ToolStripSeparator());
            foreach (int t in Targets)
            {
                int target = t;
                var it = new ToolStripMenuItem(string.Format(Lang.T("targetN", ar), t), null, (a, b) => SetMode(1, model.FreePhrase, target));
                it.Checked = model.Mode == 1 && model.FreeTarget == t;
                menu.Items.Add(it);
            }
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(Lang.T("reset", ar), null, (a, b) => DoReset());
            menu.Items.Add(Lang.T("close", ar), null, (a, b) => Close());
            e.Cancel = false;
        };
        menu.Items.Add("-");
        return menu;
    }

    void SetMode(int mode, int phrase, int target)
    {
        bool changed = model.Mode != mode;
        model.Mode = mode;
        model.FreePhrase = phrase;
        model.FreeTarget = target;
        if (changed || model.Count >= model.Target) model.Reset();
        phraseT.Play();
        Changed();
    }

    void DoReset()
    {
        previousCount = model.Count.ToString();
        model.Reset();
        rollT.Play();
        phraseT.Play();
        Changed();
    }

    void Tap()
    {
        previousCount = model.Count.ToString();
        previousBeads = TasbihRing.Beads(model.Target);
        TasbihResult r = model.Tap(DateTime.Now);
        onTap();
        rollT.Play();
        popT.Play();
        if (r != TasbihResult.Counted)
        {
            // The ring drains as the wave passes, unless this was the very end, when it stays full.
            drain = !model.Finished;
            waveT.Play();
            phraseT.Play();
        }
        Changed();
    }

    void Changed()
    {
        UpdateAccessibleName();
        saveTimer.Stop();
        saveTimer.Start();
        Invalidate();
    }

    void UpdateAccessibleName()
    {
        AccessibleName = Lang.T("tasbih", ar) + ": " + Phrase() + " " + model.Count + " / " + model.Target;
    }

    string Phrase()
    {
        if (model.Finished) return Lang.T("accepted", ar);
        return (ar ? TasbihModel.PhrasesAr : TasbihModel.PhrasesEn)[model.PhraseIndex];
    }

    protected override bool ShowWithoutActivation { get { return true; } }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 /*NOACTIVATE*/ | 0x00000080 /*TOOLWINDOW*/ | 0x00000008 /*TOPMOST*/;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Another app may own the shortcut (Claude uses Ctrl+Alt+Space), so only promise it when it really registered.
        if (RegisterHotKey(Handle, HotkeyId, ModControl | ModAlt | ModShift | ModNoRepeat, VkSpace))
            tip.SetToolTip(this, Lang.T("hotkeyHint", ar));
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterHotKey(Handle, HotkeyId);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0021) { m.Result = (IntPtr)3; return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId) { Tap(); return; }
        base.WndProc(ref m);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        if (!ThemeUi.RoundCorners(Handle, th)) Region = new Region(PopupForm.RoundRect(new Rectangle(0, 0, Width, Height), P(10)));
    }

    protected override void OnMouseEnter(EventArgs e) { hoverT.To(1); base.OnMouseEnter(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            pressed = true;
            dragging = false;
            holdDone = false;
            downAt = e.Location;
            if (resetRect.Contains(e.Location))
            {
                holding = true;
                holdT.To(1);
                holdTimer.Start();
            }
            else if (!closeRect.Contains(e.Location)) downT.To(1);
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (pressed && !holding && (dragging || Math.Abs(e.X - downAt.X) + Math.Abs(e.Y - downAt.Y) > P(4)))
        {
            dragging = true;
            downT.To(0);
            Location = new Point(Left + e.X - downAt.X, Top + e.Y - downAt.Y);
        }
        bool c = closeRect.Contains(e.Location), r = resetRect.Contains(e.Location);
        if (c != closeHover) { closeHover = c; closeT.To(c ? 1 : 0); }
        if (r != resetHover) { resetHover = r; resetT.To(r ? 1 : 0); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        closeHover = false;
        resetHover = false;
        closeT.To(0);
        resetT.To(0);
        hoverT.To(0);
        downT.To(0);
        base.OnMouseLeave(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        downT.To(0);
        if (e.Button == MouseButtons.Left && pressed)
        {
            pressed = false;
            if (holding)
            {
                holding = false;
                holdTimer.Stop();
                if (!holdDone)
                {
                    // A single click on reset must not wipe a long count, so say how it works instead.
                    holdT.To(0);
                    showHint = true;
                    hintTimer.Stop();
                    hintTimer.Start();
                    Invalidate();
                }
            }
            else if (dragging) onMoved(Left, Top);
            else if (closeRect.Contains(e.Location)) { Close(); return; }
            else Tap();
        }
        base.OnMouseUp(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(th.Bg);
        int W = Width;
        float cx = W / 2f, cy = P(150), R = P(74);
        double h = Ease.OutCubic(hoverT.Value);

        // The middle of the ring shows it can be tapped.
        float disc = R - P(14);
        Color discColor = ThemeUi.Blend(ThemeUi.Blend(th.Bg, th.Surface, h), th.Border, downT.Value);
        if (h > 0 || downT.Value > 0)
            using (var b = new SolidBrush(discColor)) g.FillEllipse(b, cx - disc, cy - disc, 2 * disc, 2 * disc);

        PaintBeads(g, cx, cy, R);
        PaintCount(g, cx, cy, W, discColor);
        PaintControls(g, h);

        string top = showHint ? Lang.T("holdReset", ar) : string.Format(Lang.T("dayTotal", ar), model.DayTotal);
        Draw(g, top, fSmall, new Rectangle(P(36), P(6), W - P(72), P(24)), showHint ? th.Fg : th.Muted);
        Draw(g, Phrase(), fPhrase, new Rectangle(P(6), P(32), W - P(12), P(26)), ThemeUi.Blend(th.Bg, th.Fg, Ease.OutCubic(phraseT.Value)));
    }

    void PaintBeads(Graphics g, float cx, float cy, float R)
    {
        bool waving = waveT.Value < 1;
        int n = waving && drain ? previousBeads : TasbihRing.Beads(model.Target);
        int filled = waving && drain ? n : TasbihRing.Filled(model.Count, model.Target, model.Finished);
        Color empty = ThemeUi.Blend(th.Bg, th.Muted, 0.42);
        double front = waveT.Value * (n + WaveBeads);
        double pop = Ease.OutBack(popT.Value);
        for (int i = 0; i < n; i++)
        {
            // Clockwise in English; counter-clockwise in Arabic, following the reading direction.
            double a = (ar ? -1 : 1) * i * 2 * Math.PI / n - Math.PI / 2;
            float x = cx + R * (float)Math.Cos(a), y = cy + R * (float)Math.Sin(a);
            float r = P(3.4f) * (i == 0 ? 1.3f : 1f);
            Color c = i < filled ? th.Accent : empty;
            if (!waving && i == filled - 1 && popT.Value < 1) r *= 1 + 0.75f * (float)(1 - pop);
            if (waving)
            {
                double d = front - i;
                if (d >= 0 && d < WaveBeads)
                {
                    double k = 1 - d / WaveBeads;
                    r += P(2.2f) * (float)k;
                    c = th.Accent;
                }
                else if (drain && d >= WaveBeads) c = empty;
            }
            using (var b = new SolidBrush(c)) g.FillEllipse(b, x - r, y - r, 2 * r, 2 * r);
        }
    }

    void PaintCount(Graphics g, float cx, float cy, int W, Color back)
    {
        const TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        double e = Ease.OutCubic(rollT.Value);
        int box = P(46), top = (int)cy - P(36), travel = P(11);
        if (e < 1)
            TextRenderer.DrawText(g, previousCount, fCount, new Rectangle(0, top - (int)(travel * e), W, box), ThemeUi.Blend(back, th.Accent, 1 - e), flags);
        TextRenderer.DrawText(g, model.Count.ToString(), fCount, new Rectangle(0, top + (int)(travel * (1 - e)), W, box), ThemeUi.Blend(back, th.Accent, e), flags);

        string target = string.Format(Lang.T("ofN", ar), model.Target);
        Draw(g, target, fTarget, new Rectangle(0, (int)cy + P(8), W, P(18)), th.Muted);

        if (model.Mode == 0)
        {
            float gap = P(11), r = P(2.6f), y = cy + P(34);
            for (int i = 0; i < 3; i++)
            {
                float x = cx + (ar ? 1 : -1) * gap * (1 - i);
                bool done = model.Finished || i < model.Phase;
                if (done || i == model.Phase)
                    using (var b = new SolidBrush(th.Accent)) g.FillEllipse(b, x - r, y - r, 2 * r, 2 * r);
                else
                    using (var pen = new Pen(th.Muted, 1f * S)) g.DrawEllipse(pen, x - r, y - r, 2 * r, 2 * r);
            }
        }
        else if (model.Rounds > 0)
            Draw(g, "× " + model.Rounds, fTarget, new Rectangle(0, (int)cy + P(26), W, P(18)), th.Muted);
    }

    void PaintControls(Graphics g, double h)
    {
        // They fade in only when the pointer is on the widget, so it stays calm while counting.
        double close = Ease.OutCubic(closeT.Value), reset = Ease.OutCubic(resetT.Value);
        if (close > 0)
            using (var b = new SolidBrush(ThemeUi.Blend(th.Bg, th.Surface, close)))
            using (GraphicsPath p = PopupForm.RoundRect(closeRect, P(6))) g.FillPath(b, p);
        if (reset > 0)
            using (var b = new SolidBrush(ThemeUi.Blend(th.Bg, th.Surface, reset)))
            using (GraphicsPath p = PopupForm.RoundRect(resetRect, P(6))) g.FillPath(b, p);
        Color rest = ThemeUi.Blend(th.Bg, th.Muted, h);
        using (var pen = new Pen(ThemeUi.Blend(rest, th.Fg, close), 1.5f * S))
        {
            pen.StartCap = pen.EndCap = LineCap.Round;
            Rectangle r = Rectangle.Inflate(closeRect, -P(7), -P(7));
            g.DrawLine(pen, r.Left, r.Top, r.Right, r.Bottom);
            g.DrawLine(pen, r.Right, r.Top, r.Left, r.Bottom);
        }
        Rectangle arc = Rectangle.Inflate(resetRect, -P(6), -P(6));
        using (var pen = new Pen(ThemeUi.Blend(rest, th.Fg, reset), 1.5f * S))
        {
            pen.StartCap = pen.EndCap = LineCap.Round;
            g.DrawArc(pen, arc, -60, 300);
            float tipX = arc.X + arc.Width * 0.75f, tipY = arc.Y + arc.Height * 0.067f;
            g.DrawLine(pen, tipX, tipY, tipX, tipY - P(4));
            g.DrawLine(pen, tipX, tipY, tipX - P(4), tipY);
        }
        // Press and hold: the arc fills, then the count resets.
        if (holdT.Value > 0)
            using (var pen = new Pen(th.Accent, 2f * S))
                g.DrawArc(pen, Rectangle.Inflate(resetRect, -P(2), -P(2)), -90, (float)(360 * holdT.Value));
    }

    void Draw(Graphics g, string text, Font f, Rectangle r, Color c)
    {
        TextFormatFlags fl = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
                             | TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter;
        if (ar) fl |= TextFormatFlags.RightToLeft;
        TextRenderer.DrawText(g, text, f, r, c, fl);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        saveTimer.Stop();
        Save();
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            saveTimer.Dispose(); holdTimer.Dispose(); hintTimer.Dispose(); tip.Dispose();
            foreach (Tween t in new[] { hoverT, downT, rollT, popT, waveT, phraseT, holdT, closeT, resetT }) t.Stop();
            fPhrase.Dispose(); fCount.Dispose(); fTarget.Dispose(); fSmall.Dispose();
            if (ContextMenuStrip != null) ContextMenuStrip.Dispose();
        }
        base.Dispose(disposing);
    }
}
