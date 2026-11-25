using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

// Window chrome shared by every themed window.
public static class ThemeUi
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

    public static Color Blend(Color a, Color b, double t)
    {
        return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
    }

    // Windows 11 rounds the window and draws its border. Returns false on older versions.
    public static bool RoundCorners(IntPtr hwnd, Theme th)
    {
        try
        {
            int pref = 2; // DWMWCP_ROUND
            if (DwmSetWindowAttribute(hwnd, 33, ref pref, 4) != 0) return false;
            int col = th.Border.R | (th.Border.G << 8) | (th.Border.B << 16);
            DwmSetWindowAttribute(hwnd, 34, ref col, 4);
            return true;
        }
        catch (DllNotFoundException) { return false; }
    }

    // Title bar and flat buttons for the windows that have a normal frame.
    public static void Frame(Form f, Theme th)
    {
        f.BackColor = th.Bg;
        f.ForeColor = th.Fg;
        try
        {
            int dark = th.IsDark ? 1 : 0;
            DwmSetWindowAttribute(f.Handle, 20, ref dark, 4); // DWMWA_USE_IMMERSIVE_DARK_MODE
        }
        catch (DllNotFoundException) { }
    }

    // Called once the window has its final size. The system's own centring runs too early, when the size
    // is still the default, which pushed larger windows down under the taskbar.
    public static void Center(Form f)
    {
        f.PerformLayout();
        Rectangle wa = Screen.FromPoint(Cursor.Position).WorkingArea;
        f.Location = new Point(wa.X + (wa.Width - f.Width) / 2, wa.Y + Math.Max(0, (wa.Height - f.Height) / 2));
    }

    // Native scroll bars are light unless asked otherwise, which glares on a dark design.
    public static void ScrollBars(Control c, Theme th)
    {
        try { SetWindowTheme(c.Handle, th.IsDark ? "DarkMode_Explorer" : null, null); }
        catch (DllNotFoundException) { }
    }

}

public class Theme
{
    public readonly string Key, En, Ar;
    public readonly Color Bg, Surface, Fg, Muted, Accent, AccentFg, Border;

    Theme(string key, string en, string ar, string bg, string surface, string fg, string muted, string accent, string accentFg, string border)
    {
        Key = key; En = en; Ar = ar;
        Bg = ColorTranslator.FromHtml(bg); Surface = ColorTranslator.FromHtml(surface);
        Fg = ColorTranslator.FromHtml(fg); Muted = ColorTranslator.FromHtml(muted);
        Accent = ColorTranslator.FromHtml(accent); AccentFg = ColorTranslator.FromHtml(accentFg);
        Border = ColorTranslator.FromHtml(border);
    }

    public string Name(bool ar) { return ar ? Ar : En; }

    public bool IsDark { get { return (Bg.R + Bg.G + Bg.B) / 3 < 128; } }

    // The 7 built-in designs.
    public static readonly Theme[] All =
    {
        new Theme("light",    "Light",         "فاتح",          "#FFFFFF", "#F3F4F6", "#111827", "#5F6775", "#0F766E", "#FFFFFF", "#E5E7EB"),
        new Theme("dark",     "Dark",          "داكن",          "#1C1C1E", "#2C2C2E", "#F5F5F7", "#A1A1AA", "#34D399", "#06281B", "#3A3A3C"),
        new Theme("midnight", "Midnight Gold", "ذهبي ليلي",     "#0F172A", "#1E293B", "#F8FAFC", "#94A3B8", "#E8B84A", "#1A1405", "#273449"),
        new Theme("emerald",  "Emerald",       "زمردي",         "#053B2F", "#0A4D3E", "#ECFDF5", "#9FD8C3", "#F2C94C", "#2B2200", "#11604D"),
        new Theme("sand",     "Desert Sand",   "رمال الصحراء",  "#F7EFE3", "#EDE0CC", "#3B2A1E", "#6F5A49", "#A85A22", "#FFFFFF", "#E3D3BC"),
        new Theme("ocean",    "Ocean Breeze",  "نسيم المحيط",   "#EEF6FC", "#DCEBF7", "#0B2540", "#48627C", "#0277B5", "#FFFFFF", "#CFE2F1"),
        new Theme("twilight", "Twilight",      "الشفق",         "#241E33", "#322945", "#F3EEFF", "#B6A9D3", "#F49AC1", "#2A0F1E", "#3E3354"),
    };

    public static Theme Resolve(string key)
    {
        if (key == "auto") return SystemUsesLight() ? All[0] : All[1];
        foreach (Theme t in All) if (t.Key == key) return t;
        return All[0];
    }

    static bool SystemUsesLight()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            {
                object v = k == null ? null : k.GetValue("AppsUseLightTheme");
                return v == null || (int)v != 0;
            }
        }
        catch { return true; }
    }
}
