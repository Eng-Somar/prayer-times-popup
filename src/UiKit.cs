using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

// The controls every window shares, so a button looks and behaves the same everywhere. They are real
// focusable controls: keyboard, focus and screen readers keep working, only the look is custom.
public interface IThemed { void ApplyTheme(Theme th); }

public static class Kit
{
    public static void Apply(Control c, Theme th)
    {
        var themed = c as IThemed;
        if (themed != null) themed.ApplyTheme(th);
        else if (c is Label) c.ForeColor = (c.Tag as string) == "muted" ? th.Muted : th.Fg;
        else if (c is Panel) c.BackColor = th.Bg;
        foreach (Control child in c.Controls) Apply(child, th);
    }

    public static Label Label(Theme th, string text, bool muted = false, int maxWidth = 0)
    {
        return new Label
        {
            Text = text, AutoSize = true, UseMnemonic = false, Tag = muted ? "muted" : null,
            ForeColor = muted ? th.Muted : th.Fg, MaximumSize = new Size(maxWidth, 0),
        };
    }

    public static Color Back(Control c, Theme th) { return c.Parent != null ? c.Parent.BackColor : th.Bg; }

    public static bool Rtl(Control c) { return c.RightToLeft == RightToLeft.Yes; }

    public static TextFormatFlags Centered(Control c)
    {
        TextFormatFlags f = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine
                            | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
        return Rtl(c) ? f | TextFormatFlags.RightToLeft : f;
    }

    public static void FocusRing(Graphics g, Rectangle r, int radius, Theme th, float s)
    {
        using (var pen = new Pen(th.Fg, 1.5f * s))
        using (GraphicsPath p = PopupForm.RoundRect(Rectangle.Inflate(r, -(int)(1.5f * s), -(int)(1.5f * s)), Math.Max(1, radius - (int)s)))
            g.DrawPath(pen, p);
    }

    public static void Check(Graphics g, float x, float y, float size, Color c, float stroke, double progress)
    {
        if (progress <= 0) return;
        PointF a = new PointF(x, y + size * 0.5f), b = new PointF(x + size * 0.36f, y + size * 0.85f), d = new PointF(x + size, y);
        using (var pen = new Pen(c, stroke))
        {
            pen.StartCap = pen.EndCap = LineCap.Round;
            double first = Math.Min(1, progress * 2.4), second = Math.Max(0, (progress - 0.42) / 0.58);
            g.DrawLine(pen, a, Lerp(a, b, first));
            if (second > 0) g.DrawLine(pen, b, Lerp(b, d, second));
        }
    }

    static PointF Lerp(PointF a, PointF b, double t)
    {
        return new PointF(a.X + (float)((b.X - a.X) * t), a.Y + (float)((b.Y - a.Y) * t));
    }
}

public enum ButtonKind { Primary, Secondary, Ghost }

public class ThemedButton : Button, IThemed
{
    Theme th;
    readonly ButtonKind kind;
    readonly float s;
    readonly Tween hot, down, fill;
    public int Chevron;          // -1 or +1 draws a back or forward arrow instead of the text

    public ThemedButton(Theme th, ButtonKind kind, float s, string text)
    {
        this.th = th;
        this.kind = kind;
        this.s = s;
        Text = text;
        hot = new Tween(120, Invalidate);
        down = new Tween(70, Invalidate);
        fill = new Tween(180, Invalidate);
        Cursor = Cursors.Hand;
        UseMnemonic = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Fit();
    }

    public void Fit()
    {
        Size text = TextRenderer.MeasureText(Text, Font);
        Size = new Size(text.Width + (int)(28 * s), (int)(34 * s));
    }

    // Fills the button as a repeated action moves toward its end.
    public double Progress { set { fill.To(value); } }
    public void SnapProgress(double value) { fill.Snap(value); Invalidate(); }

    public void ApplyTheme(Theme t) { th = t; Invalidate(); }

    protected override void OnMouseEnter(EventArgs e) { hot.To(1); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hot.To(0); down.To(0); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down.To(1); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down.To(0); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnChangeUICues(UICuesEventArgs e) { Invalidate(); base.OnChangeUICues(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Kit.Back(this, th));
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        double h = Ease.OutCubic(hot.Value), d = down.Value;
        Color bg, fg;
        if (!Enabled)
        {
            bg = kind == ButtonKind.Ghost ? Kit.Back(this, th) : ThemeUi.Blend(th.Bg, th.Surface, 0.6);
            fg = ThemeUi.Blend(th.Bg, th.Muted, 0.7);
        }
        else if (kind == ButtonKind.Primary)
        {
            bg = ThemeUi.Blend(th.Accent, th.Fg, 0.12 * h + 0.12 * d);
            fg = th.AccentFg;
        }
        else if (kind == ButtonKind.Secondary)
        {
            bg = ThemeUi.Blend(ThemeUi.Blend(th.Surface, th.Border, h), th.Muted, 0.18 * d);
            fg = th.Fg;
        }
        else
        {
            bg = ThemeUi.Blend(ThemeUi.Blend(Kit.Back(this, th), th.Surface, h), th.Border, d);
            fg = th.Fg;
        }
        int radius = (int)(8 * s);
        using (GraphicsPath p = PopupForm.RoundRect(r, radius))
        {
            using (var b = new SolidBrush(bg)) g.FillPath(b, p);
            if (fill.Value > 0 && Enabled)
            {
                int w = (int)(Width * Ease.OutCubic(fill.Value));
                g.SetClip(p);
                using (var b = new SolidBrush(ThemeUi.Blend(bg, fg, 0.2)))
                    g.FillRectangle(b, Kit.Rtl(this) ? new Rectangle(Width - w, 0, w, Height) : new Rectangle(0, 0, w, Height));
                g.ResetClip();
            }
        }
        int dy = d > 0.5 ? 1 : 0;
        if (Chevron != 0)
        {
            float cx = Width / 2f, cy = Height / 2f + dy, u = 4.5f * s;
            float dir = (Chevron > 0) != Kit.Rtl(this) ? 1 : -1;
            using (var pen = new Pen(fg, 1.8f * s))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                g.DrawLine(pen, cx - dir * u * 0.5f, cy - u, cx + dir * u * 0.5f, cy);
                g.DrawLine(pen, cx + dir * u * 0.5f, cy, cx - dir * u * 0.5f, cy + u);
            }
        }
        else TextRenderer.DrawText(g, Text, Font, new Rectangle(0, dy, Width, Height), fg, Kit.Centered(this));
        if (Focused && ShowFocusCues) Kit.FocusRing(g, r, radius, th, s);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { hot.Stop(); down.Stop(); fill.Stop(); }
        base.Dispose(disposing);
    }
}

public class ToggleSwitch : CheckBox, IThemed
{
    Theme th;
    readonly float s;
    readonly Tween knob, hot;

    public ToggleSwitch(Theme th, float s, string text, bool on, int maxTextWidth = 0)
    {
        this.th = th;
        this.s = s;
        knob = new Tween(160, Invalidate, on ? 1 : 0);
        hot = new Tween(120, Invalidate);
        Checked = on;
        Text = text;
        AutoSize = false;
        Cursor = Cursors.Hand;
        UseMnemonic = false;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Fit();
    }

    void Fit()
    {
        int textWidth = Text == "" ? 0 : TextRenderer.MeasureText(Text, Font).Width + (int)(12 * s);
        Size = new Size((int)(36 * s) + textWidth + (int)(4 * s), (int)(26 * s));
    }

    // The window's font only arrives once this is added to it, and the text must be measured with that font.
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        if (s > 0) Fit();
    }

    public void ApplyTheme(Theme t) { th = t; Invalidate(); }

    protected override void OnCheckedChanged(EventArgs e) { knob.To(Checked ? 1 : 0); base.OnCheckedChanged(e); }
    protected override void OnMouseEnter(EventArgs e) { hot.To(1); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hot.To(0); base.OnMouseLeave(e); }
    protected override void OnChangeUICues(UICuesEventArgs e) { Invalidate(); base.OnChangeUICues(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Kit.Back(this, th));
        bool rtl = Kit.Rtl(this);
        int tw = (int)(36 * s), th2 = (int)(20 * s), y = (Height - th2) / 2;
        int edge = (int)(2 * s);
        var track = rtl ? new Rectangle(Width - tw - edge, y, tw, th2) : new Rectangle(edge, y, tw, th2);
        double k = Ease.OutCubic(knob.Value);
        Color off = ThemeUi.Blend(th.Border, th.Muted, 0.35 + 0.3 * hot.Value);
        using (GraphicsPath p = PopupForm.RoundRect(track, th2 / 2))
        using (var b = new SolidBrush(ThemeUi.Blend(off, th.Accent, k)))
            g.FillPath(b, p);
        int d = th2 - (int)(6 * s);
        double travel = (tw - th2) * k;
        float x = rtl ? track.Right - (int)(3 * s) - d - (float)travel : track.X + (int)(3 * s) + (float)travel;
        using (var b = new SolidBrush(ThemeUi.Blend(th.Bg, th.AccentFg, k)))
            g.FillEllipse(b, x, track.Y + (int)(3 * s), d, d);
        if (Focused && ShowFocusCues)
        {
            Rectangle ring = Rectangle.Inflate(track, (int)(2 * s), (int)(2 * s));
            Kit.FocusRing(g, ring, ring.Height / 2, th, s);
        }
        if (Text == "") return;
        int gap = (int)(10 * s);
        var tr = rtl ? new Rectangle(0, 0, track.X - gap, Height) : new Rectangle(track.Right + gap, 0, Width - track.Right - gap, Height);
        TextFormatFlags f = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        f |= rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left;
        TextRenderer.DrawText(g, Text, Font, tr, th.Fg, f);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { knob.Stop(); hot.Stop(); }
        base.Dispose(disposing);
    }
}

// A row of options with a pill that slides to the selected one.
public class Segmented : Control, IThemed
{
    Theme th;
    readonly string[] options;
    readonly bool strong;
    readonly float s;
    readonly Tween slide;
    int selected, hover = -1;
    double from, to;

    public event EventHandler SelectedIndexChanged;

    public Segmented(Theme th, float s, int width, bool strong, params string[] options)
    {
        this.th = th;
        this.s = s;
        this.strong = strong;
        this.options = options;
        slide = new Tween(190, Invalidate, 1);
        Size = new Size(width, (int)((strong ? 36 : 32) * s));
        TabStop = true;
        Cursor = Cursors.Hand;
        Text = options[0];
        AccessibleRole = AccessibleRole.ComboBox;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable | ControlStyles.ResizeRedraw, true);
    }

    public int SelectedIndex
    {
        get { return selected; }
        set
        {
            if (value == selected || value < 0 || value >= options.Length) return;
            from = Position();
            to = value;
            selected = value;
            Text = options[value];
            slide.Play();
            Invalidate();
            if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
        }
    }

    // Sets the starting value without animating or raising the event.
    public void Init(int index)
    {
        selected = index;
        from = to = index;
        Text = options[index];
        slide.Snap(1);
    }

    double Position() { return from + (to - from) * Ease.OutCubic(slide.Value); }

    public void ApplyTheme(Theme t) { th = t; Invalidate(); }

    int IndexAt(int x)
    {
        int cell = Width / options.Length;
        int i = Math.Max(0, Math.Min(options.Length - 1, x / cell));
        return Kit.Rtl(this) ? options.Length - 1 - i : i;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int i = IndexAt(e.X);
        if (i != hover) { hover = i; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { hover = -1; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button == MouseButtons.Left) SelectedIndex = IndexAt(e.X);
        base.OnMouseDown(e);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        int forward = Kit.Rtl(this) ? -1 : 1;
        if (e.KeyCode == Keys.Right) SelectedIndex = Stepper.Next(selected, forward, 0, options.Length - 1, false);
        else if (e.KeyCode == Keys.Left) SelectedIndex = Stepper.Next(selected, -forward, 0, options.Length - 1, false);
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnChangeUICues(UICuesEventArgs e) { Invalidate(); base.OnChangeUICues(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Kit.Back(this, th));
        int radius = (int)(8 * s), inset = (int)(3 * s), n = options.Length;
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = PopupForm.RoundRect(r, radius))
        using (var b = new SolidBrush(th.Surface))
            g.FillPath(b, p);
        bool rtl = Kit.Rtl(this);
        float cell = Width / (float)n;
        double pos = Position();
        float x = (float)(rtl ? Width - (pos + 1) * cell : pos * cell);
        var pill = new Rectangle((int)x + inset, inset, (int)cell - 2 * inset, Height - 1 - 2 * inset);
        using (GraphicsPath p = PopupForm.RoundRect(pill, radius - inset / 2))
        {
            if (strong)
                using (var b = new SolidBrush(th.Accent)) g.FillPath(b, p);
            else
            {
                using (var b = new SolidBrush(th.Bg)) g.FillPath(b, p);
                using (var pen = new Pen(th.Accent, 1.25f * s)) g.DrawPath(pen, p);
            }
        }
        for (int i = 0; i < n; i++)
        {
            int visual = rtl ? n - 1 - i : i;
            var cellRect = new Rectangle((int)(visual * cell), 0, (int)cell, Height);
            double on = Math.Max(0, 1 - Math.Abs(pos - i));
            Color rest = i == hover ? th.Fg : th.Muted;
            Color active = strong ? th.AccentFg : th.Fg;
            TextRenderer.DrawText(g, options[i], Font, cellRect, ThemeUi.Blend(rest, active, on), Kit.Centered(this));
        }
        if (Focused && ShowFocusCues) Kit.FocusRing(g, Rectangle.Inflate(pill, (int)(2 * s), (int)(2 * s)), radius, th, s);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) slide.Stop();
        base.Dispose(disposing);
    }
}

// A value with minus and plus. Hold to repeat, scroll, use the arrow keys, or type digits.
public class Stepper : Control, IThemed
{
    public static int Next(int value, int delta, int min, int max, bool wrap)
    {
        int n = value + delta;
        if (wrap)
        {
            int range = max - min + 1;
            return min + ((n - min) % range + range) % range;
        }
        return Math.Max(min, Math.Min(max, n));
    }

    Theme th;
    readonly float s;
    readonly int min, max;
    readonly Timer repeat = new Timer(), typing = new Timer();
    int value, hoverZone, pressZone;
    string typed = "";

    public int Step = 1;
    public bool Wrap;
    public Func<int, string> Format;
    public event EventHandler ValueChanged;

    public Stepper(Theme th, float s, int min, int max, int value)
    {
        this.th = th;
        this.s = s;
        this.min = min;
        this.max = max;
        this.value = Math.Max(min, Math.Min(max, value));
        Size = new Size((int)(108 * s), (int)(30 * s));
        TabStop = true;
        AccessibleRole = AccessibleRole.SpinButton;
        Text = this.value.ToString(CultureInfo.InvariantCulture);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        repeat.Tick += (o, e) => { repeat.Interval = 70; Bump(pressZone); };
        typing.Interval = 1200;
        typing.Tick += (o, e) => { typing.Stop(); typed = ""; };
    }

    public int Value
    {
        get { return value; }
        set
        {
            int v = Math.Max(min, Math.Min(max, value));
            if (v == this.value) return;
            this.value = v;
            Text = Display();
            Invalidate();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }
    }

    string Display() { return Format != null ? Format(value) : value.ToString(CultureInfo.InvariantCulture); }

    public void Refresh2() { Text = Display(); Invalidate(); }

    public void ApplyTheme(Theme t) { th = t; Invalidate(); }

    void Bump(int zone) { Value = Next(value, zone * Step, min, max, Wrap); }

    // -1 the minus side, +1 the plus side, 0 the value in the middle.
    int ZoneAt(int x)
    {
        int w = (int)(32 * s);
        int side = x < w ? -1 : x >= Width - w ? 1 : 0;
        return Kit.Rtl(this) ? -side : side;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        int z = ZoneAt(e.X);
        if (z != hoverZone) { hoverZone = z; Cursor = z != 0 ? Cursors.Hand : Cursors.Default; Invalidate(); }
        base.OnMouseMove(e);
    }

    protected override void OnMouseLeave(EventArgs e) { hoverZone = 0; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        pressZone = ZoneAt(e.X);
        if (pressZone != 0 && e.Button == MouseButtons.Left)
        {
            Bump(pressZone);
            repeat.Interval = 380;
            repeat.Start();
            Invalidate();
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        repeat.Stop();
        pressZone = 0;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        Bump(e.Delta > 0 ? 1 : -1);
        base.OnMouseWheel(e);
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Up) Bump(1);
        else if (e.KeyCode == Keys.Down) Bump(-1);
        base.OnKeyDown(e);
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        if (Format == null && (char.IsDigit(e.KeyChar) || (e.KeyChar == '-' && typed == "" && min < 0)))
        {
            typed += e.KeyChar;
            typing.Stop();
            typing.Start();
            int parsed;
            if (int.TryParse(typed, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)) Value = parsed;
            e.Handled = true;
        }
        base.OnKeyPress(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnChangeUICues(UICuesEventArgs e) { Invalidate(); base.OnChangeUICues(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Kit.Back(this, th));
        int radius = (int)(8 * s), w = (int)(32 * s);
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        bool rtl = Kit.Rtl(this);
        using (GraphicsPath p = PopupForm.RoundRect(r, radius))
        {
            using (var b = new SolidBrush(th.Surface)) g.FillPath(b, p);
            g.SetClip(p);
            foreach (int zone in new[] { -1, 1 })
            {
                bool startSide = (zone < 0) != rtl;
                var z = startSide ? new Rectangle(0, 0, w, Height) : new Rectangle(Width - w, 0, w, Height);
                if (zone == hoverZone || zone == pressZone)
                    using (var b = new SolidBrush(zone == pressZone ? ThemeUi.Blend(th.Border, th.Muted, 0.3) : th.Border)) g.FillRectangle(b, z);
                bool atLimit = !Wrap && (zone < 0 ? value <= min : value >= max);
                using (var pen = new Pen(atLimit ? ThemeUi.Blend(th.Surface, th.Muted, 0.5) : th.Fg, 1.6f * s))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    float cx = z.X + z.Width / 2f, cy = Height / 2f, u = 4f * s;
                    g.DrawLine(pen, cx - u, cy, cx + u, cy);
                    if (zone > 0) g.DrawLine(pen, cx, cy - u, cx, cy + u);
                }
            }
            g.ResetClip();
        }
        TextRenderer.DrawText(g, Display(), Font, new Rectangle(w, 0, Width - 2 * w, Height), th.Fg, Kit.Centered(this));
        if (Focused && ShowFocusCues) Kit.FocusRing(g, r, radius, th, s);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { repeat.Dispose(); typing.Dispose(); }
        base.Dispose(disposing);
    }
}

// A value picked from a themed list.
public class DropList : Control, IThemed
{
    Theme th;
    readonly float s;
    readonly string[] items;
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    readonly Tween hot;
    int selected;

    public event EventHandler SelectedIndexChanged;

    public DropList(Theme th, float s, int width, params string[] items)
    {
        this.th = th;
        this.s = s;
        this.items = items;
        hot = new Tween(120, Invalidate);
        Size = new Size(width, (int)(32 * s));
        TabStop = true;
        Cursor = Cursors.Hand;
        AccessibleRole = AccessibleRole.ComboBox;
        Text = items[0];
        menu.Renderer = new MenuRenderer(th);
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
    }

    public int SelectedIndex
    {
        get { return selected; }
        set
        {
            if (value == selected || value < 0 || value >= items.Length) return;
            selected = value;
            Text = items[value];
            Invalidate();
            if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
        }
    }

    public void Init(int index) { selected = index; Text = items[index]; }

    public void ApplyTheme(Theme t) { th = t; menu.Renderer = new MenuRenderer(t); Invalidate(); }

    void Open()
    {
        bool rtl = Kit.Rtl(this);
        menu.Items.Clear();
        menu.RightToLeft = RightToLeft;
        menu.MinimumSize = new Size(Width, 0);
        for (int i = 0; i < items.Length; i++)
        {
            int index = i;
            var it = new ToolStripMenuItem(items[i], null, (o, e) => SelectedIndex = index) { Checked = i == selected };
            menu.Items.Add(it);
        }
        int gap = (int)(3 * s);
        if (rtl) menu.Show(this, new Point(Width, Height + gap), ToolStripDropDownDirection.BelowLeft);
        else menu.Show(this, new Point(0, Height + gap), ToolStripDropDownDirection.BelowRight);
    }

    protected override void OnMouseEnter(EventArgs e) { hot.To(1); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hot.To(0); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); if (e.Button == MouseButtons.Left) Open(); base.OnMouseDown(e); }
    protected override bool IsInputKey(Keys keyData) { return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space || e.KeyCode == Keys.F4) Open();
        else if (e.KeyCode == Keys.Down) SelectedIndex = Stepper.Next(selected, 1, 0, items.Length - 1, false);
        else if (e.KeyCode == Keys.Up) SelectedIndex = Stepper.Next(selected, -1, 0, items.Length - 1, false);
        base.OnKeyDown(e);
    }

    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnChangeUICues(UICuesEventArgs e) { Invalidate(); base.OnChangeUICues(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Kit.Back(this, th));
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        int radius = (int)(8 * s), arrow = (int)(28 * s), pad = (int)(10 * s);
        bool rtl = Kit.Rtl(this);
        using (GraphicsPath p = PopupForm.RoundRect(r, radius))
        using (var b = new SolidBrush(ThemeUi.Blend(th.Surface, th.Border, Ease.OutCubic(hot.Value))))
            g.FillPath(b, p);
        var tr = rtl ? new Rectangle(arrow, 0, Width - arrow - pad, Height) : new Rectangle(pad, 0, Width - arrow - pad, Height);
        TextFormatFlags f = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
        f |= rtl ? TextFormatFlags.Right | TextFormatFlags.RightToLeft : TextFormatFlags.Left;
        TextRenderer.DrawText(g, items[selected], Font, tr, th.Fg, f);
        float cx = rtl ? arrow / 2f : Width - arrow / 2f, cy = Height / 2f, u = 4f * s;
        using (var pen = new Pen(th.Muted, 1.6f * s))
        {
            pen.StartCap = pen.EndCap = LineCap.Round;
            g.DrawLine(pen, cx - u, cy - u * 0.4f, cx, cy + u * 0.5f);
            g.DrawLine(pen, cx, cy + u * 0.5f, cx + u, cy - u * 0.4f);
        }
        if (Focused && ShowFocusCues) Kit.FocusRing(g, r, radius, th, s);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { hot.Stop(); menu.Dispose(); }
        base.Dispose(disposing);
    }
}

public class ThemedInput : Panel, IThemed
{
    Theme th;
    readonly float s;
    bool readOnly;
    public readonly TextBox Box;

    public ThemedInput(Theme th, float s, int width, string text)
    {
        this.th = th;
        this.s = s;
        Box = new TextBox { BorderStyle = BorderStyle.None, Text = text };
        Controls.Add(Box);
        Size = new Size(width, (int)(32 * s));
        Box.GotFocus += (o, e) => Invalidate();
        Box.LostFocus += (o, e) => Invalidate();
        Click += (o, e) => Box.Focus();
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        ApplyColors();
    }

    public bool ReadOnly
    {
        get { return readOnly; }
        set { readOnly = value; Box.ReadOnly = value; ApplyColors(); }
    }

    void ApplyColors()
    {
        Box.BackColor = th.Surface;
        Box.ForeColor = readOnly ? th.Muted : th.Fg;
    }

    public void ApplyTheme(Theme t) { th = t; ApplyColors(); Invalidate(); }

    protected override void OnLayout(LayoutEventArgs e)
    {
        int pad = (int)(10 * s);
        Box.Width = Width - 2 * pad;
        Box.Location = new Point(pad, (Height - Box.Height) / 2);
        base.OnLayout(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Kit.Back(this, th));
        var r = new Rectangle(0, 0, Width - 1, Height - 1);
        using (GraphicsPath p = PopupForm.RoundRect(r, (int)(8 * s)))
        {
            using (var b = new SolidBrush(th.Surface)) g.FillPath(b, p);
            if (Box.Focused && !readOnly)
                using (var pen = new Pen(th.Accent, 1.5f * s)) g.DrawPath(pen, p);
        }
    }
}

// A small working copy of a design. Choosing one previews it straight away.
public class ThemeSwatch : RadioButton, IThemed
{
    Theme ui;
    readonly Theme[] shown;
    readonly float s;
    readonly Tween hot, on;
    readonly Font fName, fTime;
    public readonly string Key;

    public ThemeSwatch(Theme ui, float s, string key, string label, int width, params Theme[] shown)
    {
        this.ui = ui;
        this.s = s;
        this.shown = shown;
        Key = key;
        Text = label;
        hot = new Tween(120, Invalidate);
        on = new Tween(160, Invalidate);
        fName = new Font("Segoe UI Semibold", 9.5f);
        fTime = new Font("Segoe UI Semibold", 9.5f);
        AutoSize = false;
        Size = new Size(width, (int)(92 * s));
        Cursor = Cursors.Hand;
        UseMnemonic = false;
        Margin = new Padding(0, 0, (int)(10 * s), (int)(8 * s));
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    public void ApplyTheme(Theme t) { ui = t; Invalidate(); }

    public void SetInitial(bool value) { Checked = value; on.Snap(value ? 1 : 0); }

    protected override void OnCheckedChanged(EventArgs e) { on.To(Checked ? 1 : 0); base.OnCheckedChanged(e); }
    protected override void OnMouseEnter(EventArgs e) { hot.To(1); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hot.To(0); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    protected override void OnChangeUICues(UICuesEventArgs e) { Invalidate(); base.OnChangeUICues(e); }

    void DrawThumb(Graphics g, Rectangle r, Theme t, bool withText)
    {
        int radius = (int)(9 * s), pad = (int)(10 * s);
        using (GraphicsPath p = PopupForm.RoundRect(r, radius))
        using (var b = new SolidBrush(t.Bg))
            g.FillPath(b, p);
        const TextFormatFlags f = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.PreserveGraphicsClipping;
        if (withText)
        {
            TextRenderer.DrawText(g, "Asr", fName, new Point(r.X + pad, r.Y + (int)(8 * s)), t.Fg, f);
            string time = "3:12";
            Size ts = TextRenderer.MeasureText(g, time, fTime, Size.Empty, f);
            TextRenderer.DrawText(g, time, fTime, new Point(r.Right - pad - ts.Width, r.Y + (int)(8 * s)), t.Accent, f);
        }
        int chips = 3, gap = (int)(4 * s), h = (int)(16 * s), y = r.Bottom - h - (int)(8 * s);
        int w = (r.Width - 2 * pad - gap * (chips - 1)) / chips;
        for (int i = 0; i < chips; i++)
        {
            var chip = new Rectangle(r.X + pad + i * (w + gap), y, w, h);
            using (GraphicsPath p = PopupForm.RoundRect(chip, (int)(4 * s)))
            using (var b = new SolidBrush(i == 1 ? t.Accent : t.Surface))
                g.FillPath(b, p);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Kit.Back(this, ui));
        var thumb = new Rectangle((int)(2 * s), (int)(2 * s), Width - (int)(5 * s), (int)(64 * s));
        if (shown.Length == 1) DrawThumb(g, thumb, shown[0], true);
        else
        {
            // Auto follows Windows, so it shows both looks at once.
            g.SetClip(new Rectangle(0, 0, Width / 2, Height));
            DrawThumb(g, thumb, shown[0], false);
            g.SetClip(new Rectangle(Width / 2, 0, Width - Width / 2, Height));
            DrawThumb(g, thumb, shown[1], false);
            g.ResetClip();
        }
        double selected = Ease.OutCubic(on.Value), h = Ease.OutCubic(hot.Value);
        using (GraphicsPath p = PopupForm.RoundRect(thumb, (int)(9 * s)))
        {
            using (var pen = new Pen(ThemeUi.Blend(ui.Border, ui.Muted, h), 1f * s)) g.DrawPath(pen, p);
            if (selected > 0)
                using (var pen = new Pen(Color.FromArgb((int)(255 * selected), ui.Accent), 2.5f * s)) g.DrawPath(pen, p);
        }
        if (Focused && ShowFocusCues) Kit.FocusRing(g, Rectangle.Inflate(thumb, (int)(2 * s), (int)(2 * s)), (int)(10 * s), ui, s);
        var label = new Rectangle(0, thumb.Bottom + (int)(3 * s), Width, (int)(20 * s));
        TextRenderer.DrawText(g, Text, Font, label, Checked ? ui.Fg : ui.Muted, Kit.Centered(this));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { hot.Stop(); on.Stop(); fName.Dispose(); fTime.Dispose(); }
        base.Dispose(disposing);
    }
}

// Tray, tasbih and list menus in the chosen design.
public class MenuRenderer : ToolStripRenderer
{
    readonly Theme th;

    public MenuRenderer(Theme th) { this.th = th; }

    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using (var b = new SolidBrush(th.Bg)) e.Graphics.FillRectangle(b, e.AffectedBounds);
    }

    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using (var pen = new Pen(th.Border))
            e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }

    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        if (!e.Item.Selected || !e.Item.Enabled) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var b = new SolidBrush(th.Surface))
        using (GraphicsPath p = PopupForm.RoundRect(new Rectangle(3, 1, e.Item.Width - 7, e.Item.Height - 3), 4))
            e.Graphics.FillPath(b, p);
    }

    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = e.Item.Enabled ? th.Fg : th.Muted;
        base.OnRenderItemText(e);
    }

    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
    {
        e.ArrowColor = th.Muted;
        base.OnRenderArrow(e);
    }

    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        int y = e.Item.Height / 2;
        using (var pen = new Pen(th.Border)) e.Graphics.DrawLine(pen, 6, y, e.Item.Width - 7, y);
    }

    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Rectangle r = e.ImageRectangle;
        float size = r.Width * 0.62f;
        Kit.Check(e.Graphics, r.X + (r.Width - size) / 2, r.Y + (r.Height - size) / 2, size, th.Accent, 1.8f, 1);
    }
}
