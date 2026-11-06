using System;

public class MethodDef
{
    public readonly string Key, En, Ar;
    public readonly double Fajr, Isha, Maghrib; // twilight angles (Maghrib 0 = sunset)
    public readonly int IshaMin;                // fixed minutes after maghrib (0 = use angle)

    public MethodDef(string key, string en, string ar, double fajr, double isha, int ishaMin, double maghrib)
    {
        Key = key; En = en; Ar = ar; Fajr = fajr; Isha = isha; IshaMin = ishaMin; Maghrib = maghrib;
    }

    public string Name(bool ar) { return ar ? Ar : En; }
}

// Astronomical prayer-time calculation. Fully offline.
// C# port of PrayTimes.js, Copyright (C) 2007-2017 PrayTimes.org, developer Hamid Zarrabi-Zadeh,
// licensed under GNU LGPL v3.0. Original work: http://praytimes.org
public static class PrayCalc
{
    public static readonly MethodDef[] Methods =
    {
        new MethodDef("mwl",       "Muslim World League",              "رابطة العالم الإسلامي",              18,   17,   0,  0),
        new MethodDef("makkah",    "Umm al-Qura, Makkah",              "أم القرى، مكة المكرمة",              18.5, 0,    90, 0),
        new MethodDef("egypt",     "Egyptian General Authority",       "الهيئة المصرية العامة للمساحة",      19.5, 17.5, 0,  0),
        new MethodDef("dubai",     "UAE (GAIAE)",                      "الإمارات (الهيئة العامة للشؤون الإسلامية)",                  18.2, 18.2, 0,  0),
        new MethodDef("kuwait",    "Kuwait",                           "الكويت",                             18,   17.5, 0,  0),
        new MethodDef("qatar",     "Qatar",                            "قطر",                                18,   0,    90, 0),
        new MethodDef("jordan",    "Jordan / Palestine",               "الأردن / فلسطين",                    18,   18,   0,  0),
        new MethodDef("karachi",   "University of Islamic Sciences, Karachi", "جامعة العلوم الإسلامية، كراتشي", 18, 18,  0,  0),
        new MethodDef("isna",      "ISNA (North America)",             "الجمعية الإسلامية لأمريكا الشمالية", 15,   15,   0,  0),
        new MethodDef("turkey",    "Diyanet (Turkey)",                 "رئاسة الشؤون الدينية (تركيا)",       18,   17,   0,  0),
        new MethodDef("singapore", "Singapore / Malaysia / Indonesia", "سنغافورة / ماليزيا / إندونيسيا",     20,   18,   0,  0),
        new MethodDef("tehran",    "Institute of Geophysics, Tehran",  "معهد الجيوفيزياء، طهران",            17.7, 14,   0,  4.5),
        new MethodDef("france",    "UOIF (France)",                    "اتحاد المنظمات الإسلامية (فرنسا)",   12,   12,   0,  0),
        new MethodDef("russia",    "Spiritual Administration (Russia)","الإدارة الدينية (روسيا)",            16,   15,   0,  0),
    };

    public static MethodDef Find(string key)
    {
        foreach (MethodDef m in Methods) if (m.Key == key) return m;
        return Methods[0];
    }

    public static string AutoKey(string cc)
    {
        switch ((cc ?? "").ToUpperInvariant())
        {
            case "SA": case "YE": case "BH": case "OM": return "makkah";
            case "EG": case "SY": case "LB": case "IQ": case "SD": case "LY": return "egypt";
            case "AE": return "dubai";
            case "KW": return "kuwait";
            case "QA": return "qatar";
            case "JO": case "PS": return "jordan";
            case "PK": case "IN": case "BD": case "AF": return "karachi";
            case "US": case "CA": return "isna";
            case "TR": return "turkey";
            case "SG": case "MY": case "ID": case "BN": return "singapore";
            case "IR": return "tehran";
            case "FR": return "france";
            case "RU": return "russia";
            default: return "mwl";
        }
    }

    public static MethodDef Resolve(string key, string country)
    {
        return Find(key == "auto" ? AutoKey(country) : key);
    }

    // Returns [Fajr, Sunrise, Dhuhr, Asr, Maghrib, Isha] for the given local date.
    public static DateTime[] Compute(DateTime date, double lat, double lng, MethodDef m, int asrFactor, int highLatRule)
    {
        double tz = TimeZoneInfo.Local.GetUtcOffset(date.Date.AddHours(12)).TotalHours;
        var c = new Calc { Lat = lat, JDate = Julian(date.Year, date.Month, date.Day) - lng / (15 * 24.0) };

        double fajr = 5, sunrise = 6, dhuhr = 12, asr = 13, sunset = 18, maghrib = 18, isha = 18;
        for (int pass = 0; pass < 3; pass++) // refine: each pass uses the previous estimate for the sun's position
        {
            fajr    = c.SunAngleTime(m.Fajr, Guess(fajr, 5), true);
            sunrise = c.SunAngleTime(0.833, Guess(sunrise, 6), true);
            dhuhr   = c.MidDay(Guess(dhuhr, 12));
            asr     = c.AsrTime(asrFactor, Guess(asr, 13));
            sunset  = c.SunAngleTime(0.833, Guess(sunset, 18), false);
            maghrib = m.Maghrib > 0 ? c.SunAngleTime(m.Maghrib, Guess(maghrib, 18), false) : sunset;
            isha    = m.IshaMin > 0 ? double.NaN : c.SunAngleTime(m.Isha, Guess(isha, 18), false);
        }

        double adj = tz - lng / 15.0;
        fajr += adj; sunrise += adj; dhuhr += adj; asr += adj; sunset += adj; maghrib += adj; isha += adj;

        // High-latitude safety: keep Fajr/Isha within a sane portion of the night.
        double night = FixHour(sunrise - sunset);
        fajr = AdjustHL(fajr, sunrise, m.Fajr, night, true, highLatRule);
        if (m.IshaMin == 0) isha = AdjustHL(isha, sunset, m.Isha, night, false, highLatRule);
        if (m.Maghrib > 0) maghrib = AdjustHL(maghrib, sunset, m.Maghrib, night, false, highLatRule);
        if (m.IshaMin > 0) isha = maghrib + m.IshaMin / 60.0;

        double[] hrs = { fajr, sunrise, dhuhr, asr, maghrib, isha };
        var res = new DateTime[6];
        for (int i = 0; i < 6; i++)
        {
            double h = double.IsNaN(hrs[i]) ? 0 : hrs[i];
            res[i] = date.Date.AddMinutes(Math.Round(h * 60));
        }
        return res;
    }

    static double Guess(double hours, double fallback)
    {
        return (double.IsNaN(hours) ? fallback : hours) / 24.0;
    }

    static double AdjustHL(double time, double baseT, double angle, double night, bool ccw, int rule)
    {
        double portion = rule == 1 ? night / 2 : rule == 2 ? night / 7 : angle / 60.0 * night;
        double diff = ccw ? FixHour(baseT - time) : FixHour(time - baseT);
        if (double.IsNaN(time) || diff > portion) time = baseT + (ccw ? -portion : portion);
        return time;
    }

    class Calc
    {
        public double Lat, JDate;

        void SunPos(double jd, out double decl, out double eqt)
        {
            double d = jd - 2451545.0;
            double g = FixAngle(357.529 + 0.98560028 * d);
            double q = FixAngle(280.459 + 0.98564736 * d);
            double l = FixAngle(q + 1.915 * Sin(g) + 0.020 * Sin(2 * g));
            double e = 23.439 - 0.00000036 * d;
            double ra = ArcTan2(Cos(e) * Sin(l), Cos(l)) / 15.0;
            eqt = q / 15.0 - FixHour(ra);
            decl = ArcSin(Sin(e) * Sin(l));
        }

        public double MidDay(double t)
        {
            double decl, eqt;
            SunPos(JDate + t, out decl, out eqt);
            return FixHour(12 - eqt);
        }

        public double SunAngleTime(double angle, double t, bool ccw)
        {
            double decl, eqt;
            SunPos(JDate + t, out decl, out eqt);
            double noon = MidDay(t);
            double v = (-Sin(angle) - Sin(decl) * Sin(Lat)) / (Cos(decl) * Cos(Lat));
            if (v > 1 || v < -1) return double.NaN;
            double tt = ArcCos(v) / 15.0;
            return noon + (ccw ? -tt : tt);
        }

        public double AsrTime(int factor, double t)
        {
            double decl, eqt;
            SunPos(JDate + t, out decl, out eqt);
            double angle = -ArcCot(factor + Tan(Math.Abs(Lat - decl)));
            return SunAngleTime(angle, t, false);
        }
    }

    static double Julian(int y, int m, int d)
    {
        if (m <= 2) { y -= 1; m += 12; }
        double a = Math.Floor(y / 100.0);
        double b = 2 - a + Math.Floor(a / 4);
        return Math.Floor(365.25 * (y + 4716)) + Math.Floor(30.6001 * (m + 1)) + d + b - 1524.5;
    }

    static double Rad(double d) { return d * Math.PI / 180.0; }
    static double Deg(double r) { return r * 180.0 / Math.PI; }
    static double Sin(double d) { return Math.Sin(Rad(d)); }
    static double Cos(double d) { return Math.Cos(Rad(d)); }
    static double Tan(double d) { return Math.Tan(Rad(d)); }
    static double ArcSin(double x) { return Deg(Math.Asin(x)); }
    static double ArcCos(double x) { return Deg(Math.Acos(x)); }
    static double ArcTan2(double y, double x) { return Deg(Math.Atan2(y, x)); }
    static double ArcCot(double x) { return Deg(Math.Atan(1 / x)); }
    static double Fix(double a, double b) { a = a - b * Math.Floor(a / b); return a < 0 ? a + b : a; }
    static double FixAngle(double a) { return Fix(a, 360); }
    static double FixHour(double a) { return Fix(a, 24); }
}
