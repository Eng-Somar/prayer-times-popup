using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public enum PopupKind { Next, Before, At, Iqama, Imsak, Notice }

public class PopupAction
{
    public string Label;
    public bool Primary;
    public Action Run;
    public string Confirm;     // shown on the button for a moment before the card closes
    public Action Then;        // runs once that moment is over
}

public class PopupTool
{
    public ToolIcon Icon;
    public string Label;
    public Action Run;
    public bool KeepOpen;      // acts in place instead of closing the card
    public Func<ToolIcon> LiveIcon;
}

public class PopupInfo
{
    public PopupKind Kind;
    public int Index;          // 0 Fajr, 2 Dhuhr, 3 Asr, 4 Maghrib, 5 Isha
    public DateTime Time;
    public DateTime[] Day;     // the six times of the day this prayer belongs to
    public string Footer = "";
    public string HijriText = "";
    public string Title, Status;   // Notice only
    public DateTime IqamaAt = DateTime.MinValue;
    public bool Late, Iftar;
    public List<PopupAction> Actions = new List<PopupAction>();
    public List<PopupTool> Tools = new List<PopupTool>();
    public Func<string> FooterNote;      // replaces the footer while it returns text
    public int PrayedMask;               // prayers already marked on this day, by index
    public Action<int, bool> SetPrayed;  // when set, clicking a passed prayer in the strip marks it
}

// Borderless, non-activating card shown at the top-right of the screen. Everything on it is live: the
// strip answers hover and click, a line shows where today is between prayers, and it can be dragged away.
public class PopupForm : Form
{
    [DllImport("gdi32.dll")] static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);

    const int ConfirmMs = 900;

    readonly Theme th;
    readonly PopupInfo info;
    readonly bool ar, h24;
    readonly float S;
    readonly int durationMs;
    readonly Timer anim = new Timer();
    readonly Font fSmall, fName, fAlt, fTime, fStatus, fCapName, fCapTime;
    readonly Rectangle closeRect;
    readonly Rectangle[] actionRects, toolRects;
    readonly Tween closeT, pressT, confirmT, rippleT, backT;
    readonly Tween[] actionT, toolT, cellT = new Tween[5], checkT = new Tween[5];
    readonly int targetX;
    int elapsed, phase, lastSecond = -1, hoverAction = -1, hoverTool = -1, hoverCell = -1, confirming = -1;
    int pressAction = -1, pressTool = -1, pressCell = -1, dragStartX;
    bool hover, closeHover, pressClose, dragging, dragArmed, dwmRounded;
    double exitOpacity, backFrom;
    int exitX;
    DateTime phaseStart, lastTick, confirmUntil;

    bool IsNotice { get { return info.Kind == PopupKind.Notice; } }
    int ActionsY { get { return IsNotice ? P(112) : P(184); } }
    int ToolsY { get { return info.Actions.Count > 0 ? P(218) : P(184); } }
    int FooterY { get { return ToolsY + P(46); } }
    Rectangle Strip { get { return new Rectangle(P(18), P(118), Width - 2 * P(18), P(58)); } }

    public bool IsNextCard { get { return info.Kind == PopupKind.Next && phase < 2; } }

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

        bool hasActions = info.Actions.Count > 0;
        int height = IsNotice ? (hasActions ? 158 : 122) : (hasActions ? 288 : 254);
        Size = new Size(P(344), P(height));
        Rectangle wa = (s.PopupOnMouseScreen ? Screen.FromPoint(Cursor.Position) : Screen.PrimaryScreen).WorkingArea;
        targetX = wa.Right - Width - P(16);
        Location = new Point(targetX + P(24), wa.Top + P(16));
        closeRect = Mirror(new Rectangle(Width - P(12) - P(26), P(8), P(26), P(26)));

        Action repaint = Invalidate;
        closeT = new Tween(120, repaint);
        pressT = new Tween(70, repaint);
        confirmT = new Tween(240, repaint);
        rippleT = new Tween(900, repaint, 1);
        backT = new Tween(190, OnBack);

        actionRects = new Rectangle[info.Actions.Count];
        actionT = new Tween[info.Actions.Count];
        if (hasActions)
        {
            int pad = P(18), gap = P(8), n = info.Actions.Count;
            int w = (Width - 2 * pad - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                actionRects[i] = Mirror(new Rectangle(pad + i * (w + gap), ActionsY, w, P(30)));
                actionT[i] = new Tween(120, repaint);
            }
        }

        toolRects = new Rectangle[info.Tools.Count];
        toolT = new Tween[info.Tools.Count];
        if (info.Tools.Count > 0)
        {
            int pad = P(12), n = info.Tools.Count;
            int w = (Width - 2 * pad) / n;
            for (int i = 0; i < n; i++)
            {
                toolRects[i] = Mirror(new Rectangle(pad + i * w, ToolsY, w, P(44)));
                toolT[i] = new Tween(120, repaint);
            }
        }

        for (int c = 0; c < 5; c++)
        {
            cellT[c] = new Tween(120, repaint);
            checkT[c] = new Tween(260, repaint, (info.PrayedMask & 1 << Scheduler.Five[c]) != 0 ? 1 : 0);
        }
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
        dwmRounded = ThemeUi.RoundCorners(Handle, th);
        if (!dwmRounded) Region = Region.FromHrgn(CreateRoundRectRgn(0, 0, Width + 1, Height + 1, P(20), P(20)));
    }

    public void Pop()
    {
        AccessibleName = StatusText();
        phaseStart = DateTime.Now;
        Show();
        anim.Start();
    }

    public void Dismiss()
    {
        if (phase == 2 || IsDisposed) return;
        phase = 2;
        phaseStart = DateTime.Now;
        exitOpacity = Opacity;
        exitX = Left;
        anim.Interval = 15;
    }

    // Updates the strip when a prayer is marked from elsewhere, such as the I prayed button.
    public void ShowPrayed(int index, bool on)
    {
        if (IsDisposed) return;
        int c = Array.IndexOf(Scheduler.Five, index);
        if (c < 0) return;
        info.PrayedMask = on ? info.PrayedMask | 1 << index : info.PrayedMask & ~(1 << index);
        checkT[c].To(on ? 1 : 0);
        Invalidate();
    }

    void OnAnim(object sender, EventArgs e)
    {
        DateTime now = DateTime.Now;
        double t = (now - phaseStart).TotalMilliseconds;
        if (phase == 0)
        {
            double k = Math.Min(1.0, t / 220.0), ease = Ease.OutCubic(k);
            Opacity = ease;
            Left = targetX + (int)((1 - ease) * P(24));
            if (k >= 1)
            {
                phase = 1;
                lastTick = now;
                anim.Interval = 100;
                if (info.Kind == PopupKind.At && !info.Late) rippleT.Play();
            }
        }
        else if (phase == 1)
        {
            int dt = (int)(now - lastTick).TotalMilliseconds;
            lastTick = now;
            if (confirming >= 0)
            {
                if (now >= confirmUntil)
                {
                    Action then = info.Actions[confirming].Then;
                    Dismiss();
                    if (then != null) then();
                }
                return;
            }
            if (!hover && !dragging && durationMs > 0)
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
            Opacity = exitOpacity * (1 - k);
            Left = exitX + (int)(k * P(14));
            if (k >= 1) { anim.Stop(); Close(); }
        }
    }

    // Returns to its place after a drag that wasn't far enough to dismiss it.
    void OnBack()
    {
        if (IsDisposed || phase != 1) return;
        double k = Ease.OutCubic(backT.Value);
        Left = targetX + (int)(backFrom * (1 - k));
        Opacity = 1 - (1 - k) * Math.Min(0.7, backFrom / (Width * 1.2));
    }

    int ActionAt(Point p)
    {
        for (int i = 0; i < actionRects.Length; i++)
            if (actionRects[i].Contains(p)) return i;
        return -1;
    }

    int ToolAt(Point p)
    {
        for (int i = 0; i < toolRects.Length; i++)
            if (toolRects[i].Contains(p)) return i;
        return -1;
    }

    Rectangle CellRect(int c)
    {
        Rectangle strip = Strip;
        int colW = strip.Width / 5, col = ar ? 4 - c : c;
        return new Rectangle(strip.X + col * colW, strip.Y, colW, strip.Height);
    }

    int CellAt(Point p)
    {
        if (IsNotice) return -1;
        for (int c = 0; c < 5; c++)
            if (CellRect(c).Contains(p)) return c;
        return -1;
    }

    bool Markable(int c)
    {
        return c >= 0 && info.SetPrayed != null && confirming < 0 && Scheduler.CanMark(DateTime.Now, info.Day[Scheduler.Five[c]]);
    }

    void SetHover(Tween[] all, ref int current, int next)
    {
        if (current == next) return;
        if (current >= 0) all[current].To(0);
        if (next >= 0) all[next].To(1);
        current = next;
        Invalidate();
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; base.OnMouseEnter(e); }

    protected override void OnMouseLeave(EventArgs e)
    {
        hover = false;
        if (closeHover) { closeHover = false; closeT.To(0); }
        SetHover(actionT, ref hoverAction, -1);
        SetHover(toolT, ref hoverTool, -1);
        SetHover(cellT, ref hoverCell, -1);
        base.OnMouseLeave(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (dragArmed)
        {
            int dx = PointToScreen(e.Location).X - dragStartX;
            if (!dragging && dx > P(6)) dragging = true;
            if (dragging)
            {
                int offset = Math.Max(0, dx);
                Left = targetX + offset;
                Opacity = 1 - Math.Min(0.7, offset / (Width * 1.2));
            }
            base.OnMouseMove(e);
            return;
        }
        bool h = closeRect.Contains(e.Location);
        if (h != closeHover) { closeHover = h; closeT.To(h ? 1 : 0); }
        int a = ActionAt(e.Location), t = ToolAt(e.Location), cell = CellAt(e.Location);
        SetHover(actionT, ref hoverAction, confirming >= 0 ? -1 : a);
        SetHover(toolT, ref hoverTool, t);
        SetHover(cellT, ref hoverCell, cell);
        Cursor = h || a >= 0 || t >= 0 || Markable(cell) ? Cursors.Hand : Cursors.Default;
        base.OnMouseMove(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            pressClose = closeRect.Contains(e.Location);
            pressAction = confirming >= 0 ? -1 : ActionAt(e.Location);
            pressTool = ToolAt(e.Location);
            pressCell = CellAt(e.Location);
            if (pressClose || pressAction >= 0 || pressTool >= 0) pressT.To(1);
            else if (pressCell < 0 && phase == 1 && confirming < 0)
            {
                dragArmed = true;
                dragStartX = PointToScreen(e.Location).X;
            }
        }
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        pressT.To(0);
        if (e.Button != MouseButtons.Left) { base.OnMouseUp(e); return; }
        if (dragArmed)
        {
            dragArmed = false;
            if (dragging)
            {
                dragging = false;
                int offset = Left - targetX;
                if (offset > Width / 3) Dismiss();
                else if (offset > 0)
                {
                    backFrom = offset;
                    backT.Play();
                }
            }
            base.OnMouseUp(e);
            return;
        }
        Point p = e.Location;
        if (pressClose && closeRect.Contains(p)) Dismiss();
        else if (pressAction >= 0 && pressAction == ActionAt(p)) RunAction(pressAction);
        else if (pressTool >= 0 && pressTool == ToolAt(p)) RunTool(pressTool);
        else if (pressCell >= 0 && pressCell == CellAt(p) && Markable(pressCell)) ToggleCell(pressCell);
        pressClose = false;
        pressAction = pressTool = pressCell = -1;
        base.OnMouseUp(e);
    }

    void RunAction(int i)
    {
        PopupAction a = info.Actions[i];
        if (a.Run != null) a.Run();
        if (a.Confirm == null) { Dismiss(); return; }
        // Say something back before leaving, instead of vanishing the moment you click.
        confirming = i;
        confirmUntil = DateTime.Now.AddMilliseconds(ConfirmMs);
        confirmT.Play();
    }

    void RunTool(int i)
    {
        PopupTool t = info.Tools[i];
        if (!t.KeepOpen) Dismiss();
        if (t.Run != null) t.Run();
        Invalidate();
    }

    void ToggleCell(int c)
    {
        int index = Scheduler.Five[c];
        bool on = (info.PrayedMask & 1 << index) == 0;
        info.SetPrayed(index, on);
        ShowPrayed(index, on);
    }

    string HoverHint()
    {
        if (hoverCell < 0) return null;
        int index = Scheduler.Five[hoverCell];
        DateTime t = info.Day[index], now = DateTime.Now;
        if (t == DateTime.MinValue) return null;
        string name = Lang.Prayer(index, info.Time, ar);
        if (now < t) return string.Format(Lang.T("before", ar), name, Lang.Duration(t - now, ar));
        if (info.SetPrayed == null) return string.Format(Lang.T("startedAgo", ar), name, Lang.Duration(now - t, ar));
        return string.Format(Lang.T((info.PrayedMask & 1 << index) != 0 ? "cellUnmark" : "cellMark", ar), name);
    }

    string StatusText()
    {
        if (IsNotice) return info.Status ?? "";
        DateTime now = DateTime.Now;
        string name = Lang.Prayer(info.Index, info.Time, ar);
        string left = Lang.Duration(info.Time - now, ar);
        switch (info.Kind)
        {
            case PopupKind.Iqama: return string.Format(Lang.T("iqamaNow", ar), name);
            case PopupKind.Imsak: return string.Format(Lang.T("imsakIn", ar), left);
        }
        if (info.Kind == PopupKind.At || now >= info.Time)
        {
            string at = info.Late && now > info.Time
                ? string.Format(Lang.T("startedAgo", ar), name, Lang.Duration(now - info.Time, ar))
                : string.Format(Lang.T("timeFor", ar), name);
            if (info.IqamaAt > now) at += "  ·  " + string.Format(Lang.T("iqamaIn", ar), Lang.Duration(info.IqamaAt - now, ar));
            return at;
        }
        if (info.Iftar) return string.Format(Lang.T("iftarIn", ar), left);
        if (info.Kind == PopupKind.Before) return string.Format(Lang.T("before", ar), name, left);
        return Lang.T("next", ar) + "  ·  " + string.Format(Lang.T("in", ar), left);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(th.Bg);
        int W = Width, H = Height, pad = P(18);

        if (!dwmRounded)
            using (var pen = new Pen(th.Border))
            using (GraphicsPath path = RoundRect(new Rectangle(0, 0, W - 1, H - 1), P(10)))
                g.DrawPath(pen, path);

        // Header: crescent + Hijri date, close button.
        DrawCrescent(g, Mirror(new Rectangle(pad, P(15), P(13), P(13))), th.Accent, th.Bg);
        DrawText(g, info.HijriText, fSmall, Mirror(new Rectangle(pad + P(22), P(10), W - 2 * pad - P(22) - P(28), P(22))), th.Muted, -1, false);
        double ch = Ease.OutCubic(closeT.Value);
        if (ch > 0)
            using (var b = new SolidBrush(ThemeUi.Blend(th.Bg, th.Surface, ch)))
            using (GraphicsPath p = RoundRect(closeRect, P(6)))
                g.FillPath(b, p);
        using (var pen = new Pen(ThemeUi.Blend(th.Muted, th.Fg, ch), 1.5f * S))
        {
            pen.StartCap = pen.EndCap = LineCap.Round;
            Rectangle r = Rectangle.Inflate(closeRect, -P(8), -P(8));
            g.DrawLine(pen, r.Left, r.Top, r.Right, r.Bottom);
            g.DrawLine(pen, r.Right, r.Top, r.Left, r.Bottom);
        }

        if (IsNotice)
        {
            DrawText(g, info.Title, fName, Mirror(new Rectangle(pad, P(38), W - 2 * pad, P(44))), th.Fg, -1, true);
            DrawText(g, StatusText(), fStatus, Mirror(new Rectangle(pad, P(84), W - 2 * pad, P(22))), th.Muted, -1, false);
        }
        else PaintPrayerCard(g, W, pad);

        PaintTools(g);
        PaintActions(g);

        // Auto-dismiss progress.
        if (durationMs > 0 && phase >= 1 && confirming < 0)
        {
            double frac = Math.Max(0, 1 - elapsed / (double)durationMs);
            int w = (int)(W * frac);
            var pr = ar ? new Rectangle(W - w, H - P(3), w, P(3)) : new Rectangle(0, H - P(3), w, P(3));
            using (var b = new SolidBrush(Color.FromArgb(170, th.Accent))) g.FillRectangle(b, pr);
        }
    }

    void PaintPrayerCard(Graphics g, int W, int pad)
    {
        // Prayer name (+ name in the other language) and time.
        string name = info.Kind == PopupKind.Imsak ? Lang.T("imsakTitle", ar) : Lang.Prayer(info.Index, info.Time, ar);
        string alt = info.Kind == PopupKind.Imsak ? Lang.T("imsakTitle", !ar) : Lang.Prayer(info.Index, info.Time, !ar);
        const TextFormatFlags mf = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
        Size ns = TextRenderer.MeasureText(g, name, fName, Size.Empty, mf);
        DrawText(g, name, fName, Mirror(new Rectangle(pad, P(38), ns.Width + P(2), P(44))), th.Fg, -1, true);
        DrawText(g, alt, fAlt, Mirror(new Rectangle(pad + ns.Width + P(10), P(38), P(130), P(40))), th.Muted, -1, true);
        DrawText(g, Lang.Time(info.Time, h24, ar), fTime, Mirror(new Rectangle(W / 2, P(38), W / 2 - pad, P(44))), th.Accent, 1, true);

        string hint = HoverHint();
        DrawText(g, hint ?? StatusText(), fStatus, Mirror(new Rectangle(pad, P(86), W - 2 * pad, P(22))), hint != null ? th.Fg : th.Muted, -1, false);

        // Day strip with the five prayers.
        Rectangle strip = Strip;
        using (var b = new SolidBrush(th.Surface))
        using (GraphicsPath p = RoundRect(strip, P(10)))
            g.FillPath(b, p);
        for (int c = 0; c < 5; c++)
        {
            int idx = Scheduler.Five[c];
            Rectangle cell = CellRect(c);
            var pill = new Rectangle(cell.X + P(4), cell.Y + P(4), cell.Width - P(8), P(46));
            bool sel = idx == info.Index;
            double h = Ease.OutCubic(cellT[c].Value);
            if (sel)
            {
                using (var b = new SolidBrush(ThemeUi.Blend(th.Accent, th.Fg, 0.12 * h)))
                using (GraphicsPath p = RoundRect(pill, P(8)))
                    g.FillPath(b, p);
                if (rippleT.Value < 1) PaintRipple(g, pill);
            }
            else if (h > 0)
                using (var b = new SolidBrush(ThemeUi.Blend(th.Surface, th.Border, h)))
                using (GraphicsPath p = RoundRect(pill, P(8)))
                    g.FillPath(b, p);
            Color nameCol = sel ? th.AccentFg : th.Muted, timeCol = sel ? th.AccentFg : th.Fg;
            DrawText(g, Lang.Prayer(idx, info.Time, ar), fCapName, new Rectangle(cell.X, cell.Y + P(8), cell.Width, P(18)), nameCol, 0, false);
            DrawText(g, Lang.ShortTime(info.Day[idx], h24), fCapTime, new Rectangle(cell.X, cell.Y + P(27), cell.Width, P(20)), timeCol, 0, false);
            float size = 6.5f * S;
            Kit.Check(g, cell.X + (cell.Width - size) / 2f, cell.Y + P(44.5f), size, sel ? th.AccentFg : th.Accent, 1.4f * S, checkT[c].Value);
        }
        PaintNowLine(g, strip);

        string note = info.FooterNote == null ? null : info.FooterNote();
        DrawText(g, note ?? info.Footer, fCapName, Mirror(new Rectangle(pad, FooterY, W - 2 * pad, P(18))), note == null ? th.Muted : th.Fg, -1, false);
    }

    // One soft ring as a prayer time arrives, so the card catches the eye even with the sound off.
    void PaintRipple(Graphics g, Rectangle pill)
    {
        double k = Ease.OutCubic(rippleT.Value);
        int grow = (int)(P(7) * k), alpha = (int)(190 * (1 - k));
        using (var pen = new Pen(Color.FromArgb(alpha, th.Accent), 1.5f * S))
        using (GraphicsPath p = RoundRect(Rectangle.Inflate(pill, grow, grow), P(8) + grow))
            g.DrawPath(pen, p);
    }

    // Where now falls between today's prayers.
    void PaintNowLine(Graphics g, Rectangle strip)
    {
        double pos = PrayerDay.NowPosition(DateTime.Now, info.Day);
        if (pos < 0) return;
        Rectangle first = CellRect(0), last = CellRect(4);
        float a = first.X + first.Width / 2f, b = last.X + last.Width / 2f, y = strip.Bottom - P(4);
        float x = a + (b - a) * (float)(pos / 4);
        using (var pen = new Pen(ThemeUi.Blend(th.Surface, th.Muted, 0.4), 2f * S)) g.DrawLine(pen, a, y, b, y);
        using (var pen = new Pen(th.Accent, 2f * S)) g.DrawLine(pen, a, y, x, y);
        float r = 3.2f * S, ring = 1.5f * S;
        using (var br = new SolidBrush(th.Surface)) g.FillEllipse(br, x - r - ring, y - r - ring, 2 * (r + ring), 2 * (r + ring));
        using (var br = new SolidBrush(th.Accent)) g.FillEllipse(br, x - r, y - r, 2 * r, 2 * r);
    }

    void PaintTools(Graphics g)
    {
        for (int i = 0; i < toolRects.Length; i++)
        {
            PopupTool tool = info.Tools[i];
            double h = Ease.OutCubic(toolT[i].Value);
            bool pressed = i == pressTool && pressT.Value > 0.5;
            if (h > 0)
                using (var b = new SolidBrush(ThemeUi.Blend(th.Bg, pressed ? th.Border : th.Surface, h)))
                using (GraphicsPath p = RoundRect(Rectangle.Inflate(toolRects[i], -P(1), 0), P(8)))
                    g.FillPath(b, p);
            ToolIcon icon = tool.LiveIcon != null ? tool.LiveIcon() : tool.Icon;
            bool active = icon == ToolIcon.QuietOn;
            int size = P(18), lift = pressed ? 0 : (int)Math.Round(h * P(1));
            var iconRect = new Rectangle(toolRects[i].X + (toolRects[i].Width - size) / 2, toolRects[i].Y + P(5) - lift, size, size);
            ToolIcons.Draw(g, icon, iconRect, active ? th.Accent : ThemeUi.Blend(th.Muted, th.Fg, h));
            DrawText(g, tool.Label, fCapName, new Rectangle(toolRects[i].X, toolRects[i].Y + P(25), toolRects[i].Width, P(16)), active ? th.Fg : ThemeUi.Blend(th.Muted, th.Fg, h), 0, false);
        }
    }

    void PaintActions(Graphics g)
    {
        double confirm = Ease.OutCubic(confirmT.Value);
        for (int i = 0; i < actionRects.Length; i++)
        {
            PopupAction a = info.Actions[i];
            double h = Ease.OutCubic(actionT[i].Value), d = i == pressAction ? pressT.Value : 0;
            Color fill = a.Primary
                ? ThemeUi.Blend(th.Accent, th.Fg, 0.12 * h + 0.12 * d)
                : ThemeUi.Blend(ThemeUi.Blend(th.Surface, th.Border, h), th.Muted, 0.18 * d);
            Color text = a.Primary ? th.AccentFg : th.Fg;
            bool mine = i == confirming;
            if (confirming >= 0 && !mine)
            {
                fill = ThemeUi.Blend(fill, th.Bg, confirm);
                text = ThemeUi.Blend(text, th.Bg, confirm);
            }
            using (var b = new SolidBrush(fill))
            using (GraphicsPath p = RoundRect(actionRects[i], P(8)))
                g.FillPath(b, p);
            int dy = d > 0.5 ? 1 : 0;
            var r = new Rectangle(actionRects[i].X, actionRects[i].Y + dy, actionRects[i].Width, actionRects[i].Height);
            if (!mine) { DrawText(g, a.Label, fCapTime, r, text, 0, false); continue; }
            // The button turns into a short acknowledgement with a check being drawn beside it.
            DrawText(g, a.Label, fCapTime, r, ThemeUi.Blend(text, fill, Math.Min(1, confirm * 2)), 0, false);
            double appear = Math.Max(0, confirm * 2 - 1);
            const TextFormatFlags mf = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            Size ts = TextRenderer.MeasureText(g, a.Confirm, fCapTime, Size.Empty, mf);
            int size = P(10), gap = P(7), total = ts.Width + size + gap;
            int start = r.X + (r.Width - total) / 2;
            int checkX = ar ? start + total - size : start;
            var tr = ar ? new Rectangle(start, r.Y, ts.Width + P(2), r.Height) : new Rectangle(start + size + gap, r.Y, ts.Width + P(2), r.Height);
            DrawText(g, a.Confirm, fCapTime, tr, ThemeUi.Blend(fill, text, appear), 0, false);
            Kit.Check(g, checkX, r.Y + (r.Height - size) / 2f, size, text, 1.7f * S, appear);
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
            closeT.Stop(); pressT.Stop(); confirmT.Stop(); rippleT.Stop(); backT.Stop();
            foreach (Tween t in actionT) t.Stop();
            foreach (Tween t in toolT) t.Stop();
            foreach (Tween t in cellT) t.Stop();
            foreach (Tween t in checkT) t.Stop();
            fSmall.Dispose(); fName.Dispose(); fAlt.Dispose(); fTime.Dispose();
            fStatus.Dispose(); fCapName.Dispose(); fCapTime.Dispose();
        }
        base.Dispose(disposing);
    }
}
