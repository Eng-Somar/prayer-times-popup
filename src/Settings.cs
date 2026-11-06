using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

public class Settings
{
    public string Language = "en", Theme = "auto", LocMode = "auto", City = "", Country = "", Method = "auto";
    public double Lat, Lon;
    public bool HasLoc, Use24h, Sound = true, Startup;
    public int Asr = 1, HighLat = 0, RemindBefore = 10, Duration = 45;

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunName = "PrayerTimesPopup";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PrayerTimesPopup");
    static readonly string FilePath = Path.Combine(Dir, "settings.ini");

    public Settings Clone() { return (Settings)MemberwiseClone(); }

    public static Settings Load()
    {
        var s = new Settings();
        if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar") s.Language = "ar";
        try
        {
            if (File.Exists(FilePath))
            {
                var d = new Dictionary<string, string>();
                foreach (string line in File.ReadAllLines(FilePath, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                }
                s.Language = Str(d, "Language", s.Language);
                s.Theme = Str(d, "Theme", s.Theme);
                s.LocMode = Str(d, "LocMode", s.LocMode);
                s.City = Str(d, "City", s.City);
                s.Country = Str(d, "Country", s.Country);
                s.Method = Str(d, "Method", s.Method);
                s.Lat = Dbl(d, "Lat", 0);
                s.Lon = Dbl(d, "Lon", 0);
                s.HasLoc = Str(d, "HasLoc", "0") == "1";
                s.Use24h = Str(d, "Use24h", "0") == "1";
                s.Sound = Str(d, "Sound", "1") == "1";
                s.Asr = (int)Dbl(d, "Asr", 1);
                s.HighLat = (int)Dbl(d, "HighLat", 0);
                s.RemindBefore = (int)Dbl(d, "RemindBefore", 10);
                s.Duration = (int)Dbl(d, "Duration", 45);
            }
        }
        catch { }
        s.Startup = StartupRegistered();
        return s;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var sb = new StringBuilder();
            sb.AppendLine("Language=" + Language);
            sb.AppendLine("Theme=" + Theme);
            sb.AppendLine("LocMode=" + LocMode);
            sb.AppendLine("City=" + (City ?? "").Replace("\r", "").Replace("\n", " "));
            sb.AppendLine("Country=" + Country);
            sb.AppendLine("Method=" + Method);
            sb.AppendLine("Lat=" + Lat.ToString("R", Inv));
            sb.AppendLine("Lon=" + Lon.ToString("R", Inv));
            sb.AppendLine("HasLoc=" + (HasLoc ? "1" : "0"));
            sb.AppendLine("Use24h=" + (Use24h ? "1" : "0"));
            sb.AppendLine("Sound=" + (Sound ? "1" : "0"));
            sb.AppendLine("Asr=" + Asr);
            sb.AppendLine("HighLat=" + HighLat);
            sb.AppendLine("RemindBefore=" + RemindBefore);
            sb.AppendLine("Duration=" + Duration);
            File.WriteAllText(FilePath, sb.ToString(), Encoding.UTF8);
        }
        catch { }
    }

    public void ApplyStartup()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (Startup) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                else if (k.GetValue(RunName) != null) k.DeleteValue(RunName, false);
            }
        }
        catch { }
    }

    static bool StartupRegistered()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunName) != null;
        }
        catch { return false; }
    }

    static string Str(Dictionary<string, string> d, string k, string def)
    {
        string v;
        return d.TryGetValue(k, out v) ? v : def;
    }

    static double Dbl(Dictionary<string, string> d, string k, double def)
    {
        string v; double r;
        return d.TryGetValue(k, out v) && double.TryParse(v, NumberStyles.Float, Inv, out r) ? r : def;
    }
}
