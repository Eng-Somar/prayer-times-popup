using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

// A PC has no compass, so this shows the angle from true north instead of pointing live.
public class QiblaForm : Form
{
    readonly Theme th;
    readonly bool ar;
    readonly double bearing;
    readonly string place;
    readonly float S;
    readonly Tween sweep;

    public QiblaForm(Settings s)
    {
        th = Theme.Resolve(s.Theme);
        ar = s.Language == "ar";
        bearing = Geo.QiblaBearing(s.Lat, s.Lon);
        place = s.City;
        sweep = new Tween(850, Invalidate);
        using (Graphics g = CreateGraphics()) S = g.DpiX / 96f;
        Text = Lang.T("qibla", ar);
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.Manual;
        Load += (o, e) => ThemeUi.Center(this);
        ClientSize = new Size(P(320), P(410));
        DoubleBuffered = true;
        KeyPreview = true;
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (ArgumentException) { }
        ThemeUi.Frame(this, th);
    }

    int P(float v) { return (int)Math.Round(v * S); }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) Close();
        base.OnKeyDown(e);
    }

    // The needle turns from north to the Qibla, which is exactly what you do with a compass in your hand.
    protected override void OnShown(EventArgs e)
    {
        sweep.Play();
        base.OnShown(e);
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        sweep.Play();
        base.OnMouseClick(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) sweep.Stop();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int W = ClientSize.Width, cx = W / 2, cy = P(138), r = P(108);

        using (var b = new SolidBrush(th.Surface)) g.FillEllipse(b, cx - r, cy - r, 2 * r, 2 * r);
        using (var pen = new Pen(th.Border, 1f * S)) g.DrawEllipse(pen, cx - r, cy - r, 2 * r, 2 * r);
        using (var pen = new Pen(th.Muted, 1f * S))
            for (int d = 0; d < 360; d += 30)
            {
                double a = d * Math.PI / 180;
                int len = d % 90 == 0 ? P(10) : P(5);
                g.DrawLine(pen, cx + (float)(Math.Sin(a) * (r - len)), cy - (float)(Math.Cos(a) * (r - len)),
                                cx + (float)(Math.Sin(a) * r), cy - (float)(Math.Cos(a) * r));
            }

        string[] compass = ar ? new[] { "ش", "ق", "ج", "غ" } : new[] { "N", "E", "S", "W" };
        for (int i = 0; i < 4; i++)
        {
            double a = i * Math.PI / 2;
            int size = P(22), lr = r - P(24);
            var rc = new Rectangle(cx + (int)(Math.Sin(a) * lr) - size / 2, cy - (int)(Math.Cos(a) * lr) - size / 2, size, size);
            TextRenderer.DrawText(g, compass[i], Font, rc, i == 0 ? th.Fg : th.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        double shown = bearing * Ease.OutCubic(sweep.Value);
        double q = shown * Math.PI / 180;
        // The arrow stops short of the compass letters so it never covers one, whatever the bearing.
        float tipX = cx + (float)(Math.Sin(q) * (r - P(38))), tipY = cy - (float)(Math.Cos(q) * (r - P(38)));
        int arcR = r - P(58);
        if (shown > 0.5)
            using (var pen = new Pen(Color.FromArgb(120, th.Accent), 2f * S))
                g.DrawArc(pen, cx - arcR, cy - arcR, 2 * arcR, 2 * arcR, -90, (float)shown);
        using (var pen = new Pen(th.Accent, 3f * S))
        {
            pen.CustomEndCap = new AdjustableArrowCap(4, 5);
            g.DrawLine(pen, cx, cy, tipX, tipY);
        }
        using (var b = new SolidBrush(th.Accent)) g.FillEllipse(b, cx - P(5), cy - P(5), P(10), P(10));

        using (var f = new Font("Segoe UI Semibold", 22f))
            Text2(g, Math.Round(shown).ToString(CultureInfo.InvariantCulture) + "°", f, P(254), P(44), th.Accent);
        Text2(g, string.Format(Lang.T("qiblaDeg", ar), Math.Round(bearing).ToString(CultureInfo.InvariantCulture)), Font, P(300), P(26), th.Fg);
        Text2(g, place ?? "", Font, P(326), P(24), th.Muted);
        Text2(g, Lang.T("qiblaHint", ar), Font, P(350), P(48), th.Muted);
    }

    void Text2(Graphics g, string text, Font f, int y, int h, Color c)
    {
        TextFormatFlags fl = TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix;
        if (ar) fl |= TextFormatFlags.RightToLeft;
        TextRenderer.DrawText(g, text, f, new Rectangle(P(16), y, ClientSize.Width - P(32), h), c, fl);
    }
}
