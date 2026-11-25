using System.Drawing;
using System.Drawing.Drawing2D;

public enum ToolIcon { Tasbih, Adhkar, Qibla, Stats, Quiet, QuietOn, Settings }

// One stroke weight on an 18-unit grid, scaled to the requested size.
public static class ToolIcons
{
    public static void Draw(Graphics g, ToolIcon icon, Rectangle r, Color c)
    {
        GraphicsState state = g.Save();
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TranslateTransform(r.X, r.Y);
        g.ScaleTransform(r.Width / 18f, r.Height / 18f);
        using (var pen = new Pen(c, 1.5f))
        using (var brush = new SolidBrush(c))
        {
            pen.StartCap = pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            switch (icon)
            {
                case ToolIcon.Tasbih: Tasbih(g, pen, brush); break;
                case ToolIcon.Adhkar: Book(g, pen); break;
                case ToolIcon.Qibla: Compass(g, pen, brush); break;
                case ToolIcon.Stats: Bars(g, pen); break;
                case ToolIcon.Quiet: Bell(g, pen); break;
                case ToolIcon.QuietOn: Bell(g, pen); g.DrawLine(pen, 3f, 2.5f, 15f, 15.5f); break;
                case ToolIcon.Settings: Sliders(g, pen, brush); break;
            }
        }
        g.Restore(state);
    }

    static void Tasbih(Graphics g, Pen pen, Brush brush)
    {
        const float cx = 9, cy = 7.5f, ring = 5.8f, bead = 1.45f;
        for (int i = 0; i < 9; i++)
        {
            double a = System.Math.PI / 2 + i * 2 * System.Math.PI / 9;
            float x = cx + ring * (float)System.Math.Cos(a), y = cy + ring * (float)System.Math.Sin(a);
            g.FillEllipse(brush, x - bead, y - bead, 2 * bead, 2 * bead);
        }
        g.DrawLine(pen, cx, cy + ring, cx, 17f);
    }

    static void Book(Graphics g, Pen pen)
    {
        g.DrawPolygon(pen, new[]
        {
            new PointF(9, 5), new PointF(2.2f, 3.2f), new PointF(2.2f, 13.8f), new PointF(9, 15.6f),
            new PointF(15.8f, 13.8f), new PointF(15.8f, 3.2f),
        });
        g.DrawLine(pen, 9f, 5f, 9f, 15.6f);
    }

    static void Compass(Graphics g, Pen pen, Brush brush)
    {
        g.DrawEllipse(pen, 1.5f, 1.5f, 15f, 15f);
        g.FillPolygon(brush, new[] { new PointF(12.6f, 5.4f), new PointF(10.2f, 10.2f), new PointF(5.4f, 12.6f), new PointF(7.8f, 7.8f) });
    }

    static void Bars(Graphics g, Pen pen)
    {
        pen.Width = 2.4f;
        g.DrawLine(pen, 3.6f, 15.5f, 3.6f, 10f);
        g.DrawLine(pen, 9f, 15.5f, 9f, 6f);
        g.DrawLine(pen, 14.4f, 15.5f, 14.4f, 2.5f);
    }

    static void Bell(Graphics g, Pen pen)
    {
        using (var path = new GraphicsPath())
        {
            path.AddBezier(4.2f, 12.6f, 4.8f, 10f, 3.8f, 2.4f, 9f, 2.4f);
            path.AddBezier(9f, 2.4f, 14.2f, 2.4f, 13.2f, 10f, 13.8f, 12.6f);
            g.DrawPath(pen, path);
        }
        g.DrawLine(pen, 2.8f, 12.8f, 15.2f, 12.8f);
        g.DrawLine(pen, 7.6f, 15.8f, 10.4f, 15.8f);
    }

    static void Sliders(Graphics g, Pen pen, Brush brush)
    {
        float[] ys = { 3.8f, 9f, 14.2f }, knobs = { 11.8f, 5.6f, 12.6f };
        for (int i = 0; i < 3; i++)
        {
            g.DrawLine(pen, 1.8f, ys[i], 16.2f, ys[i]);
            g.FillEllipse(brush, knobs[i] - 2.3f, ys[i] - 2.3f, 4.6f, 4.6f);
        }
    }
}
