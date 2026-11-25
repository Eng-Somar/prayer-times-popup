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
    public double Lat, Lon, OfferLat, OfferLon;
    public bool HasLoc, HasOffer, Use24h, Startup, PopupOnMouseScreen;
    public int Asr = 1, HighLat = 0, RemindBefore = 10, Duration = 45, HijriAdjust;

    // One entry per time: Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha. Sunrise is shown but never reminded.
    public int[] Offsets = new int[6], Iqama = new int[6];
    public bool[] RemindBy = { true, true, true, true, true, true }, SoundBy = { true, true, true, true, true, true };

    public bool DndFullscreen = true, Imsak = true, FastNotice;
    public int ImsakMinutes = 10;
    public bool AdhkarMorning, AdhkarEvening, AdhkarAfter, AdhkarSleep;
    public int AdhkarAfterMinutes = 5, SleepMinute = 22 * 60 + 30;
    public int TasbihX = -1, TasbihY = -1;

    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run", RunName = "PrayerTimesPopup";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PrayerTimesPopup");
    static readonly string FilePath = Path.Combine(Dir, "settings.ini");

    public Settings Clone()
    {
        var c = (Settings)MemberwiseClone();
        c.Offsets = (int[])Offsets.Clone();
        c.Iqama = (int[])Iqama.Clone();
        c.RemindBy = (bool[])RemindBy.Clone();
        c.SoundBy = (bool[])SoundBy.Clone();
        return c;
    }

    public ScheduleOptions ToSchedule()
    {
        return new ScheduleOptions
        {
            RemindBefore = RemindBefore, Remind = RemindBy, Iqama = Iqama, Imsak = Imsak, ImsakMinutes = ImsakMinutes,
            HijriAdjust = HijriAdjust, FastNotice = FastNotice,
            AdhkarMorning = AdhkarMorning, AdhkarEvening = AdhkarEvening, AdhkarAfter = AdhkarAfter, AdhkarSleep = AdhkarSleep,
            AdhkarAfterMinutes = AdhkarAfterMinutes, SleepMinute = SleepMinute,
        };
    }

    public static Settings Load()
    {
        Settings s = null;
        try { if (File.Exists(FilePath)) s = Parse(File.ReadAllText(FilePath, Encoding.UTF8)); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        if (s == null)
        {
            s = new Settings();
            if (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ar") s.Language = "ar";
        }
        s.Startup = StartupRegistered();
        return s;
    }

    public static Settings Parse(string text)
    {
        var d = new Dictionary<string, string>();
        foreach (string raw in (text ?? "").Split('\n'))
        {
            int i = raw.IndexOf('=');
            if (i > 0) d[raw.Substring(0, i).Trim().TrimStart('﻿')] = raw.Substring(i + 1).Trim();
        }
        var s = new Settings();
        s.Language = Str(d, "Language", s.Language);
        s.Theme = Str(d, "Theme", s.Theme);
        s.LocMode = Str(d, "LocMode", s.LocMode);
        s.City = Str(d, "City", s.City);
        s.Country = Str(d, "Country", s.Country);
        s.Method = Str(d, "Method", s.Method);
        s.Lat = Dbl(d, "Lat", 0);
        s.Lon = Dbl(d, "Lon", 0);
        s.OfferLat = Dbl(d, "OfferLat", 0);
        s.OfferLon = Dbl(d, "OfferLon", 0);
        s.HasLoc = Flag(d, "HasLoc", false);
        s.HasOffer = Flag(d, "HasOffer", false);
        s.Use24h = Flag(d, "Use24h", false);
        s.PopupOnMouseScreen = Flag(d, "PopupOnMouseScreen", false);
        s.Asr = Int(d, "Asr", 1, 1, 2);
        s.HighLat = Int(d, "HighLat", 0, 0, 2);
        s.RemindBefore = Int(d, "RemindBefore", 10, 0, 60);
        s.Duration = Int(d, "Duration", 45, 0, 600);
        s.HijriAdjust = Int(d, "HijriAdjust", 0, -2, 2);
        s.Offsets = Ints(d, "Offsets", -60, 60);
        s.Iqama = Ints(d, "Iqama", 0, 90);
        s.RemindBy = Flags(d, "RemindBy", true);
        s.SoundBy = Flags(d, "SoundBy", Flag(d, "Sound", true)); // v1.0 had a single Sound switch
        s.DndFullscreen = Flag(d, "DndFullscreen", true);
        s.Imsak = Flag(d, "Imsak", true);
        s.ImsakMinutes = Int(d, "ImsakMinutes", 10, 1, 60);
        s.FastNotice = Flag(d, "FastNotice", false);
        s.AdhkarMorning = Flag(d, "AdhkarMorning", false);
        s.AdhkarEvening = Flag(d, "AdhkarEvening", false);
        s.AdhkarAfter = Flag(d, "AdhkarAfter", false);
        s.AdhkarSleep = Flag(d, "AdhkarSleep", false);
        s.AdhkarAfterMinutes = Int(d, "AdhkarAfterMinutes", 5, 0, 60);
        s.SleepMinute = Int(d, "SleepMinute", 22 * 60 + 30, 0, 24 * 60 - 1);
        s.TasbihX = Int(d, "TasbihX", -1, -1, 100000);
        s.TasbihY = Int(d, "TasbihY", -1, -1, 100000);
        return s;
    }

    public string Serialize()
    {
        var sb = new StringBuilder();
        Action<string, object> w = (k, v) => sb.Append(k).Append('=').Append(Convert.ToString(v, Inv)).Append('\n');
        w("Language", Language);
        w("Theme", Theme);
        w("LocMode", LocMode);
        w("City", (City ?? "").Replace("\r", "").Replace("\n", " "));
        w("Country", Country);
        w("Method", Method);
        w("Lat", Lat.ToString("R", Inv));
        w("Lon", Lon.ToString("R", Inv));
        w("OfferLat", OfferLat.ToString("R", Inv));
        w("OfferLon", OfferLon.ToString("R", Inv));
        w("HasLoc", HasLoc ? 1 : 0);
        w("HasOffer", HasOffer ? 1 : 0);
        w("Use24h", Use24h ? 1 : 0);
        w("PopupOnMouseScreen", PopupOnMouseScreen ? 1 : 0);
        w("Asr", Asr);
        w("HighLat", HighLat);
        w("RemindBefore", RemindBefore);
        w("Duration", Duration);
        w("HijriAdjust", HijriAdjust);
        w("Offsets", string.Join(",", Offsets));
        w("Iqama", string.Join(",", Iqama));
        w("RemindBy", Bits(RemindBy));
        w("SoundBy", Bits(SoundBy));
        w("DndFullscreen", DndFullscreen ? 1 : 0);
        w("Imsak", Imsak ? 1 : 0);
        w("ImsakMinutes", ImsakMinutes);
        w("FastNotice", FastNotice ? 1 : 0);
        w("AdhkarMorning", AdhkarMorning ? 1 : 0);
        w("AdhkarEvening", AdhkarEvening ? 1 : 0);
        w("AdhkarAfter", AdhkarAfter ? 1 : 0);
        w("AdhkarSleep", AdhkarSleep ? 1 : 0);
        w("AdhkarAfterMinutes", AdhkarAfterMinutes);
        w("SleepMinute", SleepMinute);
        w("TasbihX", TasbihX);
        w("TasbihY", TasbihY);
        return sb.ToString();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, Serialize(), Encoding.UTF8);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
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
        catch (UnauthorizedAccessException) { }
        catch (System.Security.SecurityException) { }
    }

    static bool StartupRegistered()
    {
        try
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunName) != null;
        }
        catch (System.Security.SecurityException) { return false; }
    }

    static string Bits(bool[] v)
    {
        var sb = new StringBuilder();
        foreach (bool b in v) sb.Append(b ? '1' : '0');
        return sb.ToString();
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

    static int Int(Dictionary<string, string> d, string k, int def, int min, int max)
    {
        string v; double r;
        if (!d.TryGetValue(k, out v) || !double.TryParse(v, NumberStyles.Float, Inv, out r)) return def;
        return Math.Max(min, Math.Min(max, (int)r));
    }

    static bool Flag(Dictionary<string, string> d, string k, bool def)
    {
        string v;
        return d.TryGetValue(k, out v) ? v == "1" : def;
    }

    static int[] Ints(Dictionary<string, string> d, string k, int min, int max)
    {
        var res = new int[6];
        string v;
        if (!d.TryGetValue(k, out v)) return res;
        string[] parts = v.Split(',');
        for (int i = 0; i < 6 && i < parts.Length; i++)
        {
            int r;
            if (int.TryParse(parts[i], NumberStyles.Integer, Inv, out r)) res[i] = Math.Max(min, Math.Min(max, r));
        }
        return res;
    }

    static bool[] Flags(Dictionary<string, string> d, string k, bool def)
    {
        bool[] res = { def, def, def, def, def, def };
        string v;
        if (d.TryGetValue(k, out v))
            for (int i = 0; i < 6 && i < v.Length; i++) res[i] = v[i] == '1';
        return res;
    }
}
