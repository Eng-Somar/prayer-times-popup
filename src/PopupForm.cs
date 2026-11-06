using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public enum PopupKind { Next, Before, At }

public class PopupInfo
{
    public PopupKind Kind;
    public int Index;          // 0 Fajr, 2 Dhuhr, 3 Asr, 4 Maghrib, 5 Isha
    public DateTime Time;
    public DateTime[] Day;     // the six times of the day this prayer belongs to
    public string Footer = "";
}

// Borderless, non-activating notification card shown at the top-right of the primary screen.
public class PopupForm : Form
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

    static readonly int[] Five = { 0, 2, 3, 4, 5 };

    readonly Theme th;
    readonly PopupInfo info;
    readonly bool ar, h24;
    readonly float S;
    readonly int durationMs;
    readonly Timer anim = new Timer();
    readonly Font fSmall, fName, fAlt, fTime, fStatus, fCapName, fCapTime;
    readonly Rectangle closeRect;
    readonly int targetX;
    int elapsed, phase, lastSecond = -1;
    bool hover, closeHover, dwmRounded;
    DateTime phaseStart, lastTick;

    public PopupForm(Settings s, PopupInfo info, int durationSec)
    {
        th = Theme.Resolve(s.Theme);
        this.info = info;
        ar = s.Language == "ar";
        h24 = s.Use24h;
        durationMs = Math.Max(0, durationSec) * 1000;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = th.Bg;
        Text = Lang.T("app", ar);
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

        using (Graphics g = CreateGraphics()) S = g.DpiX / 96f;
        fSmall = new Font("Segoe UI", 9f);
        fName = new Font("Segoe UI Semibold", 19f);
        fAlt = new Font("Segoe UI", 12f);
        fTime = new Font("Segoe UI Semibold", 19f);
        fStatus = new Font("Segoe UI", 10f);
        fCapName = new Font("Segoe UI", 8.25f);
        fCapTime = new Font("Segoe UI Semibold", 9.75f);

        Size = new Size(P(344), P(206));
        Rectangle wa = Screen.PrimaryScreen.WorkingArea;
        targetX = wa.Right - Width - P(16);
        Location = new Point(targetX + P(24), wa.Top + P(16));
        closeRect = Mirror(new Rectangle(Width - P(12) - P(26), P(8), P(26), P(26)));
        Opacity = 0;
        anim.Interval = 15;
        anim.Tick += OnAnim;
    }

    int P(float v) { return (int)Math.Round(v * S); }

    Rectangle Mirror(Rectangle r) { return ar ? new Rectangle(Width - r.Right, r.Y, r.Width, r.Height) : r; }

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

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0021) { m.Result = (IntPtr)3; return; } // WM_MOUSEACTIVATE -> MA_NOACTIVATE
        base.WndProc(ref m);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        try
        {
            int pref = 2; // DWMWCP_ROUND (Windows 11)
            dwmRounded = DwmSetWindowAttribute(Handle, 33, ref pref, 4) == 0;
            if (dwmRounded)
            {
                int col = th.Border.R | (th.Border.G << 8) | (th.Border.B << 16);
                DwmSetWindowAttribute(Handle, 34, ref col, 4);
            }
        }
        catch { dwmRounded = false; }
        if (!dwmRounded) Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, P(20), P(20)));
    }

    public void Pop()
    {
        phaseStart = DateTime.Now;
        Show();
        anim.Start();
    }

    public void Dismiss()
    {
        if (phase == 2 || IsDisposed) return;
        phase = 2;
        phaseStart = DateTime.Now;
        anim.Interval = 15;
    }

    void OnAnim(object sender, EventArgs e)
    {
        DateTime now = DateTime.Now;
        double t = (now - phaseStart).TotalMilliseconds;
        if (phase == 0)
        {
            double k = Math.Min(1.0, t / 220.0), ease = 1 - Math.Pow(1 - k, 3);
            Opacity = ease;
            Left = targetX + (int)((1 - ease) * P(24));
            if (k >= 1) { phase = 1; lastTick = now; anim.Interval = 100; }
        }
        else if (phase == 1)
        {
            int dt = (int)(now - lastTick).TotalMilliseconds;
            lastTick = now;
            if (!hover && durationMs > 0)
            {
                elapsed += dt;
                if (elapsed >= durationMs) { Dismiss(); return; }
                Invalidate(new Rectangle(0, Height - P(4), Width, P(4)));
            }
            if (now.Second != lastSecond) { lastSecond = now.Second; Invalidate(); }
        }
        else
        {
            double k = Math.Min(1.0, t / 160.0);
            Opacity = 1 - k;
            Left = targetX + (int)(k * P(12));
            if (k >= 1) { anim.Stop(); Close(); }
        }
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; base.OnMouseEnter(e); }

    protected override void OnMouseLeave(EventArgs e)
    {
        hover = false;
        if (closeHover) { closeHover = false; Invalidate(); }
        base.OnMouseLeave(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        bool h = closeRect.Contains(e.Location);
        if (h != closeHover) { closeHover = h; Invalidate(); }
        Cursor = Cursors.Hand;
        base.OnMouseMove(e);
    }

    protected override void OnMouseUp(MouseEventArgs e) { Dismiss(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(th.Bg);
        int W = Width, H = Height, pad = P(18);
        DateTime now = DateTime.Now;

        if (!dwmRounded)
            using (var pen = new Pen(th.Border))
            using (GraphicsPath path = RoundRect(new Rectangle(0, 0, W - 1, H - 1), P(10)))
                g.DrawPath(pen, path);

        // Header: crescent + Hijri date, close button.
        DrawCrescent(g, Mirror(new Rectangle(pad, P(15), P(13), P(13))), th.Accent, th.Bg);
        DrawText(g, Lang.Hijri(info.Time, ar), fSmall, Mirror(new Rectangle(pad + P(22), P(10), W - 2 * pad - P(22) - P(28), P(22))), th.Muted, -1, false);
        if (closeHover)
            using (var b = new SolidBrush(th.Surface))
            using (GraphicsPath p = RoundRect(closeRect, P(6)))
                g.FillPath(b, p);
        using (var pen = new Pen(closeHover ? th.Fg : th.Muted, 1.5f * S))
        {
            Rectangle r = Rectangle.Inflate(closeRect, -P(8), -P(8));
            g.DrawLine(pen, r.Left, r.Top, r.Right, r.Bottom);
            g.DrawLine(pen, r.Right, r.Top, r.Left, r.Bottom);
        }

        // Prayer name (+ name in the other language) and time.
        string name = Lang.Prayer(info.Index, info.Time, ar);
        string alt = Lang.Prayer(info.Index, info.Time, !ar);
        const TextFormatFlags mf = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        Size ns = TextRenderer.MeasureText(g, name, fName, Size.Empty, mf);
        DrawText(g, name, fName, Mirror(new Rectangle(pad, P(38), ns.Width + P(2), P(44))), th.Fg, -1, true);
        DrawText(g, alt, fAlt, Mirror(new Rectangle(pad + ns.Width + P(10), P(38), P(130), P(40))), th.Muted, -1, true);
        DrawText(g, Lang.Time(info.Time, h24, ar), fTime, Mirror(new Rectangle(W / 2, P(38), W / 2 - pad, P(44))), th.Accent, 1, true);

        // Status line.
        string status;
        if (info.Kind == PopupKind.At || now >= info.Time)
            status = string.Format(Lang.T("timeFor", ar), name);
        else if (info.Kind == PopupKind.Before)
            status = string.Format(Lang.T("before", ar), name, Lang.Duration(info.Time - now, ar));
        else
            status = Lang.T("next", ar) + "  ·  " + string.Format(Lang.T("in", ar), Lang.Duration(info.Time - now, ar));
        DrawText(g, status, fStatus, Mirror(new Rectangle(pad, P(86), W - 2 * pad, P(22))), th.Muted, -1, false);

        // Day strip with the five prayers.
        var strip = new Rectangle(pad, P(118), W - 2 * pad, P(54));
        using (var b = new SolidBrush(th.Surface))
        using (GraphicsPath p = RoundRect(strip, P(10)))
            g.FillPath(b, p);
        int colW = strip.Width / 5;
        for (int c = 0; c < 5; c++)
        {
            int idx = Five[c];
            int col = ar ? 4 - c : c;
            var cell = new Rectangle(strip.X + col * colW, strip.Y, colW, strip.Height);
            bool sel = idx == info.Index;
            if (sel)
                using (var b = new SolidBrush(th.Accent))
                using (GraphicsPath p = RoundRect(Rectangle.Inflate(cell, -P(4), -P(4)), P(8)))
                    g.FillPath(b, p);
            Color nameCol = sel ? th.AccentFg : th.Muted, timeCol = sel ? th.AccentFg : th.Fg;
            DrawText(g, Lang.Prayer(idx, info.Day[idx], ar), fCapName, new Rectangle(cell.X, cell.Y + P(8), cell.Width, P(18)), nameCol, 0, false);
            DrawText(g, Lang.ShortTime(info.Day[idx], h24), fCapTime, new Rectangle(cell.X, cell.Y + P(27), cell.Width, P(20)), timeCol, 0, false);
        }

        // Footer: location and method.
        DrawText(g, info.Footer, fCapName, Mirror(new Rectangle(pad, P(178), W - 2 * pad, P(18))), th.Muted, -1, false);

        // Auto-dismiss progress.
        if (durationMs > 0 && phase >= 1)
        {
            double frac = Math.Max(0, 1 - elapsed / (double)durationMs);
            int w = (int)(W * frac);
            var pr = ar ? new Rectangle(W - w, H - P(3), w, P(3)) : new Rectangle(0, H - P(3), w, P(3));
            using (var b = new SolidBrush(Color.FromArgb(170, th.Accent))) g.FillRectangle(b, pr);
        }
    }

    // align: -1 start, 0 center, 1 end (start/end flip in Arabic).
    void DrawText(Graphics g, string text, Font f, Rectangle r, Color c, int align, bool bottom)
    {
        TextFormatFlags fl = TextFormatFlags.SingleLine | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix
                             | (bottom ? TextFormatFlags.Bottom : TextFormatFlags.VerticalCenter);
        if (align == 0) fl |= TextFormatFlags.HorizontalCenter;
        else if ((align < 0) != ar) fl |= TextFormatFlags.Left;
        else fl |= TextFormatFlags.Right;
        if (ar) fl |= TextFormatFlags.RightToLeft;
        TextRenderer.DrawText(g, text, f, r, c, fl);
    }

    static void DrawCrescent(Graphics g, Rectangle r, Color fg, Color bg)
    {
        using (var b = new SolidBrush(fg)) g.FillEllipse(b, r);
        var r2 = new RectangleF(r.X + r.Width * 0.34f, r.Y - r.Height * 0.12f, r.Width, r.Height);
        using (var b = new SolidBrush(bg)) g.FillEllipse(b, r2);
    }

    public static GraphicsPath RoundRect(Rectangle r, int radius)
    {
        var p = new GraphicsPath();
        int d = Math.Max(1, radius * 2);
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            anim.Dispose();
            fSmall.Dispose(); fName.Dispose(); fAlt.Dispose(); fTime.Dispose();
            fStatus.Dispose(); fCapName.Dispose(); fCapTime.Dispose();
        }
        base.Dispose(disposing);
    }
}
