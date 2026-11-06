using System.Drawing;
using Microsoft.Win32;

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

    // The 7 built-in designs.
    public static readonly Theme[] All =
    {
        new Theme("light",    "Light",         "فاتح",          "#FFFFFF", "#F3F4F6", "#111827", "#6B7280", "#0F766E", "#FFFFFF", "#E5E7EB"),
        new Theme("dark",     "Dark",          "داكن",          "#1C1C1E", "#2C2C2E", "#F5F5F7", "#A1A1AA", "#34D399", "#06281B", "#3A3A3C"),
        new Theme("midnight", "Midnight Gold", "ذهبي ليلي",     "#0F172A", "#1E293B", "#F8FAFC", "#94A3B8", "#E8B84A", "#1A1405", "#273449"),
        new Theme("emerald",  "Emerald",       "زمردي",         "#053B2F", "#0A4D3E", "#ECFDF5", "#9FD8C3", "#F2C94C", "#2B2200", "#11604D"),
        new Theme("sand",     "Desert Sand",   "رمال الصحراء",  "#F7EFE3", "#EDE0CC", "#3B2A1E", "#8A7360", "#B5652B", "#FFFFFF", "#E3D3BC"),
        new Theme("ocean",    "Ocean Breeze",  "نسيم المحيط",   "#EEF6FC", "#DCEBF7", "#0B2540", "#5A7590", "#0284C7", "#FFFFFF", "#CFE2F1"),
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
