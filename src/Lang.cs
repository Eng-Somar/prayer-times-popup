using System;
using System.Collections.Generic;
using System.Globalization;

public static class Lang
{
    public static bool Ar;
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static readonly Dictionary<string, string[]> D = new Dictionary<string, string[]>
    {
        { "app",        new[] { "Prayer Times", "أوقات الصلاة" } },
        { "next",       new[] { "Next prayer", "الصلاة القادمة" } },
        { "timeFor",    new[] { "It's time for {0} prayer", "حان الآن موعد صلاة {0}" } },
        { "before",     new[] { "{0} prayer in {1}", "صلاة {0} بعد {1}" } },
        { "in",         new[] { "in {0}", "بعد {0}" } },
        { "showNext",   new[] { "Show next prayer", "عرض الصلاة القادمة" } },
        { "settings",   new[] { "Settings", "الإعدادات" } },
        { "refresh",    new[] { "Refresh location", "تحديث الموقع" } },
        { "exit",       new[] { "Exit", "خروج" } },
        { "language",   new[] { "Language", "اللغة" } },
        { "theme",      new[] { "Design", "التصميم" } },
        { "preview",    new[] { "Preview", "معاينة" } },
        { "location",   new[] { "Location", "الموقع" } },
        { "locAuto",    new[] { "Auto-detect", "تحديد تلقائي" } },
        { "locManual",  new[] { "Manual", "يدوي" } },
        { "city",       new[] { "City", "المدينة" } },
        { "lat",        new[] { "Latitude", "خط العرض" } },
        { "lon",        new[] { "Longitude", "خط الطول" } },
        { "detect",     new[] { "Detect now", "تحديد الآن" } },
        { "detecting",  new[] { "Detecting…", "جارٍ التحديد…" } },
        { "detectFail", new[] { "Could not detect location. Enter it manually.", "تعذّر تحديد الموقع. أدخله يدويًا." } },
        { "method",     new[] { "Calculation method", "طريقة الحساب" } },
        { "asr",        new[] { "Asr calculation", "حساب العصر" } },
        { "asrStd",     new[] { "Standard (Shafi'i, Maliki, Hanbali)", "الجمهور (الشافعي، المالكي، الحنبلي)" } },
        { "asrHanafi",  new[] { "Hanafi", "الحنفي" } },
        { "highLat",    new[] { "High latitude rule", "خطوط العرض العليا" } },
        { "hl0",        new[] { "Angle-based", "حسب الزاوية" } },
        { "hl1",        new[] { "Middle of the night", "منتصف الليل" } },
        { "hl2",        new[] { "One-seventh of the night", "سُبع الليل" } },
        { "remind",     new[] { "Remind before (minutes, 0 = off)", "التذكير قبل (دقائق، 0 = إيقاف)" } },
        { "duration",   new[] { "Popup stays (seconds, 0 = until closed)", "مدة الظهور (ثوانٍ، 0 = حتى الإغلاق)" } },
        { "timeFmt",    new[] { "Time format", "تنسيق الوقت" } },
        { "t12",        new[] { "12-hour", "12 ساعة" } },
        { "t24",        new[] { "24-hour", "24 ساعة" } },
        { "sound",      new[] { "Play sound at prayer time", "تشغيل صوت عند دخول الوقت" } },
        { "startup",    new[] { "Start with Windows", "التشغيل مع بدء ويندوز" } },
        { "save",       new[] { "Save", "حفظ" } },
        { "cancel",     new[] { "Cancel", "إلغاء" } },
        { "badCoords",  new[] { "Please enter a valid latitude (-90..90) and longitude (-180..180).", "يرجى إدخال خط عرض (-90..90) وخط طول (-180..180) صحيحين." } },
        { "noLoc",      new[] { "Location not set", "لم يتم تحديد الموقع" } },
        { "autoTheme",  new[] { "Auto (match Windows)", "تلقائي (حسب ويندوز)" } },
        { "autoMethod", new[] { "Auto (by country)", "تلقائي (حسب الدولة)" } },
    };

    public static string T(string key) { return T(key, Ar); }

    public static string T(string key, bool ar)
    {
        string[] v;
        return D.TryGetValue(key, out v) ? (ar ? v[1] : v[0]) : key;
    }

    static readonly string[] PrayerEn = { "Fajr", "Sunrise", "Dhuhr", "Asr", "Maghrib", "Isha" };
    static readonly string[] PrayerAr = { "الفجر", "الشروق", "الظهر", "العصر", "المغرب", "العشاء" };

    public static string Prayer(int i, DateTime day, bool ar)
    {
        if (i == 2 && day.DayOfWeek == DayOfWeek.Friday) return ar ? "الجمعة" : "Jumu'ah";
        return ar ? PrayerAr[i] : PrayerEn[i];
    }

    public static string Duration(TimeSpan ts, bool ar)
    {
        int mins = (int)Math.Ceiling(ts.TotalMinutes);
        if (mins < 1) mins = 1;
        int h = mins / 60, m = mins % 60;
        if (ar)
        {
            if (h == 0) return m + (m <= 10 && m > 2 ? " دقائق" : " دقيقة");
            return m == 0 ? h + " ساعة" : h + " س " + m + " د";
        }
        if (h == 0) return m + " min";
        return m == 0 ? h + "h" : h + "h " + m + "m";
    }

    public static string Time(DateTime t, bool h24, bool ar)
    {
        if (h24) return t.ToString("HH:mm", Inv);
        return t.ToString("h:mm", Inv) + " " + (ar ? (t.Hour < 12 ? "ص" : "م") : (t.Hour < 12 ? "AM" : "PM"));
    }

    public static string ShortTime(DateTime t, bool h24)
    {
        return t.ToString(h24 ? "HH:mm" : "h:mm", Inv);
    }

    static readonly string[] HMonEn = { "Muharram", "Safar", "Rabi' al-Awwal", "Rabi' al-Thani", "Jumada al-Ula", "Jumada al-Akhirah",
                                        "Rajab", "Sha'ban", "Ramadan", "Shawwal", "Dhu al-Qa'dah", "Dhu al-Hijjah" };
    static readonly string[] HMonAr = { "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة",
                                        "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة" };
    static readonly string[] DayAr = { "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };

    public static string Hijri(DateTime d, bool ar)
    {
        try
        {
            var c = new UmAlQuraCalendar();
            int y = c.GetYear(d), m = c.GetMonth(d), day = c.GetDayOfMonth(d);
            return ar
                ? DayAr[(int)d.DayOfWeek] + "، " + day + " " + HMonAr[m - 1] + " " + y + " هـ"
                : d.ToString("dddd", Inv) + ", " + day + " " + HMonEn[m - 1] + " " + y + " AH";
        }
        catch { return d.ToString("dddd, d MMMM yyyy", Inv); }
    }
}
