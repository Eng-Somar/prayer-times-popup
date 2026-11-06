using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// App icon: white crescent on a teal rounded square. Used for the tray and exported as the .exe icon at build time.
public static class IconArt
{
    static readonly Color Teal = Color.FromArgb(15, 118, 110);

    public static Bitmap Draw(int sz)
    {
        var bmp = new Bitmap(sz, sz, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);
            using (GraphicsPath p = PopupForm.RoundRect(new Rectangle(0, 0, sz - 1, sz - 1), System.Math.Max(2, sz / 4)))
            using (var b = new SolidBrush(Teal))
                g.FillPath(b, p);
            float d = sz * 0.62f;
            var c = new RectangleF((sz - d) / 2f - sz * 0.05f, (sz - d) / 2f, d, d);
            g.FillEllipse(Brushes.White, c);
            var c2 = c;
            c2.Offset(d * 0.33f, -d * 0.12f);
            using (var b = new SolidBrush(Teal)) g.FillEllipse(b, c2);
        }
        return bmp;
    }

    public static Icon TrayIcon(int sz)
    {
        using (Bitmap b = Draw(sz)) return Icon.FromHandle(b.GetHicon());
    }

    public static void SaveIco(string path)
    {
        int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 256 };
        var pngs = new List<byte[]>();
        foreach (int sz in sizes)
            using (Bitmap b = Draw(sz))
            using (var ms = new MemoryStream())
            {
                b.Save(ms, ImageFormat.Png);
                pngs.Add(ms.ToArray());
            }
        using (FileStream fs = File.Create(path))
        using (var w = new BinaryWriter(fs))
        {
            w.Write((short)0); w.Write((short)1); w.Write((short)sizes.Length);
            int off = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                w.Write(dim); w.Write(dim); w.Write((byte)0); w.Write((byte)0);
                w.Write((short)1); w.Write((short)32);
                w.Write(pngs[i].Length); w.Write(off);
                off += pngs[i].Length;
            }
            foreach (byte[] p in pngs) w.Write(p);
        }
    }
}
