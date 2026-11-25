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
        { "prayed",     new[] { "I prayed", "صلّيت" } },
        { "later",      new[] { "In 5 min", "بعد 5 دقائق" } },
        { "tasbih",     new[] { "Tasbih", "التسبيح" } },
        { "startedAgo", new[] { "{0} prayer started {1} ago", "دخل وقت صلاة {0} منذ {1}" } },
        { "iqamaIn",    new[] { "Iqama in {0}", "الإقامة بعد {0}" } },
        { "iqamaNow",   new[] { "It's time for the {0} iqama", "حان وقت إقامة صلاة {0}" } },
        { "iftarIn",    new[] { "Iftar in {0}", "الإفطار بعد {0}" } },
        { "imsakTitle", new[] { "Imsak", "الإمساك" } },
        { "imsakIn",    new[] { "Fajr in {0}", "أذان الفجر بعد {0}" } },
        { "adhkar",     new[] { "Adhkar", "الأذكار" } },
        { "adhkarM",    new[] { "Morning adhkar", "أذكار الصباح" } },
        { "adhkarE",    new[] { "Evening adhkar", "أذكار المساء" } },
        { "adhkarA",    new[] { "Adhkar after prayer", "الأذكار بعد الصلاة" } },
        { "adhkarS",    new[] { "Sleep adhkar", "أذكار النوم" } },
        { "adhkarTime", new[] { "It's a good time to read them", "هذا وقت مناسب لقراءتها" } },
        { "read",       new[] { "Read", "اقرأ" } },
        { "fastTitle",  new[] { "Fasting tomorrow", "صيام الغد" } },
        { "fastMon",    new[] { "Tomorrow is Monday, a recommended fast", "غدًا الاثنين، ويُستحب صيامه" } },
        { "fastThu",    new[] { "Tomorrow is Thursday, a recommended fast", "غدًا الخميس، ويُستحب صيامه" } },
        { "fastWhite",  new[] { "Tomorrow is one of the White Days", "غدًا من الأيام البيض" } },
        { "qibla",      new[] { "Qibla direction", "اتجاه القبلة" } },
        { "qiblaDeg",   new[] { "{0}° clockwise from true north", "{0}° من الشمال الحقيقي مع عقارب الساعة" } },
        { "qiblaHint",  new[] { "Use a compass to find north, then turn by this angle.", "حدّد الشمال بالبوصلة، ثم استدر بهذه الزاوية." } },
        { "quiet",      new[] { "Do not disturb", "عدم الإزعاج" } },
        { "quiet1",     new[] { "For 1 hour", "لمدة ساعة" } },
        { "quiet3",     new[] { "For 3 hours", "لمدة 3 ساعات" } },
        { "quietOff",   new[] { "Turn off", "إيقاف" } },
        { "quietTill",  new[] { "Quiet until {0}", "هادئ حتى {0}" } },
        { "updates",    new[] { "Check for updates", "البحث عن تحديثات" } },
        { "upToDate",   new[] { "You have the latest version ({0}).", "لديك أحدث إصدار ({0})." } },
        { "updateNew",  new[] { "Version {0} is available. Click to open the download page.", "الإصدار {0} متاح. انقر لفتح صفحة التنزيل." } },
        { "updateFail", new[] { "Couldn't check for updates. Check your connection and try again.", "تعذّر البحث عن تحديثات. تحقق من الاتصال وحاول مرة أخرى." } },
        { "offer",      new[] { "Your location looks like {0}, possibly because of a VPN. Prayer times stay on {1} unless you click here to switch.", "يبدو أن موقعك {0}، وقد يكون السبب VPN. ستبقى الأوقات على {1} ما لم تنقر هنا للتبديل." } },
        { "offerMenu",  new[] { "Switch location to {0}", "تحويل الموقع إلى {0}" } },
        { "tabGeneral", new[] { "General", "عام" } },
        { "tabLoc",     new[] { "Location", "الموقع والحساب" } },
        { "tabPrayers", new[] { "Prayers", "الصلوات" } },
        { "tabRemind",  new[] { "Reminders", "التذكيرات" } },
        { "colPrayer",  new[] { "Prayer", "الصلاة" } },
        { "colRemind",  new[] { "Remind", "تذكير" } },
        { "colSound",   new[] { "Sound", "صوت" } },
        { "colAdjust",  new[] { "Adjust (min)", "تعديل (د)" } },
        { "colIqama",   new[] { "Iqama (min)", "الإقامة (د)" } },
        { "prayersHint",new[] { "Adjust a time to match your official timetable. Iqama 0 means off.", "عدّل الوقت ليطابق التقويم الرسمي. الإقامة 0 تعني إيقافها." } },
        { "hijriAdj",   new[] { "Hijri date adjustment (days)", "تعديل التاريخ الهجري (أيام)" } },
        { "screen",     new[] { "Popup appears on", "تظهر النافذة على" } },
        { "screenMain", new[] { "Main screen", "الشاشة الرئيسية" } },
        { "screenMouse",new[] { "Screen with the mouse", "الشاشة التي عليها المؤشر" } },
        { "dnd",        new[] { "Hold reminders during full-screen apps and presentations", "تأجيل التذكير أثناء ملء الشاشة والعروض التقديمية" } },
        { "imsakOpt",   new[] { "Imsak reminder in Ramadan, minutes before Fajr", "تذكير الإمساك في رمضان، دقائق قبل الفجر" } },
        { "fastOpt",    new[] { "Remind me the evening before recommended fasts", "ذكّرني مساءً قبل أيام الصيام المستحب" } },
        { "optM",       new[] { "Morning adhkar, 20 minutes after Fajr", "أذكار الصباح، بعد الفجر بـ20 دقيقة" } },
        { "optE",       new[] { "Evening adhkar, 20 minutes after Asr", "أذكار المساء، بعد العصر بـ20 دقيقة" } },
        { "optA",       new[] { "Adhkar after prayer, minutes after the prayer time", "الأذكار بعد الصلاة، دقائق بعد دخول الوقت" } },
        { "optS",       new[] { "Sleep adhkar at", "أذكار النوم في الساعة" } },
        { "effective",  new[] { "In use: {0}", "المستخدمة الآن: {0}" } },
        { "source",     new[] { "Source: Hisn al-Muslim, item {0}", "المصدر: حصن المسلم، رقم {0}" } },
        { "count",      new[] { "Count ({0} left)", "عدّ (المتبقي {0})" } },
        { "prev",       new[] { "Previous", "السابق" } },
        { "nextItem",   new[] { "Next", "التالي" } },
        { "of",         new[] { "{0} of {1}", "{0} من {1}" } },
        { "allDone",    new[] { "All done. May Allah accept.", "تمّت الأذكار. تقبّل الله." } },
        { "noAdhkar",   new[] { "The adhkar text couldn't be loaded. Please download the app again.", "تعذّر تحميل نص الأذكار. يرجى تنزيل التطبيق مرة أخرى." } },
        { "accepted",   new[] { "May Allah accept", "تقبّل الله" } },
        { "dayTotal",   new[] { "Today {0}", "اليوم {0}" } },
        { "seqAfter",   new[] { "After-prayer sequence (33, 33, 34)", "تسبيح ما بعد الصلاة (33، 33، 34)" } },
        { "freeCount",  new[] { "Free count", "عدّ حر" } },
        { "targetN",    new[] { "Target {0}", "العدد {0}" } },
        { "reset",      new[] { "Reset", "تصفير" } },
        { "close",      new[] { "Close", "إغلاق" } },
        { "toolQibla",  new[] { "Qibla", "القبلة" } },
        { "toolStats",  new[] { "Stats", "إحصائيات" } },
        { "toolQuiet",  new[] { "Quiet", "هدوء" } },
        { "stats",      new[] { "Statistics", "الإحصائيات" } },
        { "statsWeek",  new[] { "Last 7 days", "آخر 7 أيام" } },
        { "statsSummary", new[] { "Summary", "الملخص" } },
        { "colToday",   new[] { "Today", "اليوم" } },
        { "col7",       new[] { "7 days", "7 أيام" } },
        { "col30",      new[] { "30 days", "30 يومًا" } },
        { "statPrayers",new[] { "Prayers marked", "الصلوات المسجّلة" } },
        { "statTasbih", new[] { "Tasbih count", "عدد التسبيح" } },
        { "statAdhkar", new[] { "Adhkar read to the end", "الأذكار المكتملة" } },
        { "statStreak", new[] { "Full days in a row: {0}", "أيام مكتملة متتالية: {0}" } },
        { "statsHint",  new[] { "Counts only what you mark with I prayed or in this table, tasbih you count here, and adhkar you read to the end. Saved only on this PC.", "تُحسب فقط الصلوات التي تسجّلها بزر «صلّيت» أو في هذا الجدول، والتسبيح الذي تعدّه هنا، والأذكار التي تقرؤها حتى نهايتها. تُحفظ على هذا الجهاز فقط." } },
        { "statsClear", new[] { "Clear statistics", "مسح الإحصائيات" } },
        { "statsClearAsk", new[] { "Clear all your statistics? This can't be undone.", "هل تريد مسح كل إحصائياتك؟ لا يمكن التراجع عن ذلك." } },
        { "cellMark",   new[] { "Click to mark {0} as prayed", "انقر لتسجيل صلاة {0}" } },
        { "cellUnmark", new[] { "{0} is marked. Click to undo", "صلاة {0} مسجّلة. انقر للإلغاء" } },
        { "holdReset",  new[] { "Hold to reset", "اضغط مطولًا للتصفير" } },
        { "ofN",        new[] { "of {0}", "من {0}" } },
        { "setM",       new[] { "Morning", "الصباح" } },
        { "setE",       new[] { "Evening", "المساء" } },
        { "setA",       new[] { "After prayer", "بعد الصلاة" } },
        { "setS",       new[] { "Sleep", "النوم" } },
        { "grpPrayer",  new[] { "Prayer reminders", "تذكير الصلاة" } },
        { "grpFast",    new[] { "Ramadan and fasting", "رمضان والصيام" } },
        { "autoThemeShort", new[] { "Auto", "تلقائي" } },
        { "prevWeek",   new[] { "Previous week", "الأسبوع السابق" } },
        { "nextWeek",   new[] { "Next week", "الأسبوع التالي" } },
        { "weekRange",  new[] { "{0} – {1}", "{0} – {1}" } },
        { "hotkeyHint", new[] { "Ctrl+Alt+Shift+Space counts while this is open", "Ctrl+Alt+Shift+Space للعدّ ما دامت النافذة مفتوحة" } },
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
            string mins2 = m == 1 ? "دقيقة واحدة" : m == 2 ? "دقيقتان" : m <= 10 ? m + " دقائق" : m + " دقيقة";
            if (h == 0) return mins2;
            string hours = h == 1 ? "ساعة" : h == 2 ? "ساعتان" : h <= 10 ? h + " ساعات" : h + " ساعة";
            if (m == 0) return hours;
            return hours + " و" + (m == 1 ? "دقيقة" : m == 2 ? "دقيقتان" : m <= 10 ? m + " دقائق" : m + " دقيقة");
        }
        if (h == 0) return m + " min";
        return m == 0 ? h + "h" : h + "h " + m + "m";
    }

    public static string Time(DateTime t, bool h24, bool ar)
    {
        if (t == DateTime.MinValue) return Unavailable;
        if (h24) return t.ToString("HH:mm", Inv);
        return t.ToString("h:mm", Inv) + " " + (ar ? (t.Hour < 12 ? "ص" : "م") : (t.Hour < 12 ? "AM" : "PM"));
    }

    public static string ShortTime(DateTime t, bool h24)
    {
        return t == DateTime.MinValue ? Unavailable : t.ToString(h24 ? "HH:mm" : "h:mm", Inv);
    }

    static readonly string[] HMonEn = { "Muharram", "Safar", "Rabi' al-Awwal", "Rabi' al-Thani", "Jumada al-Ula", "Jumada al-Akhirah",
                                        "Rajab", "Sha'ban", "Ramadan", "Shawwal", "Dhu al-Qa'dah", "Dhu al-Hijjah" };
    static readonly string[] HMonAr = { "محرم", "صفر", "ربيع الأول", "ربيع الآخر", "جمادى الأولى", "جمادى الآخرة",
                                        "رجب", "شعبان", "رمضان", "شوال", "ذو القعدة", "ذو الحجة" };
    static readonly string[] DayAr = { "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت" };

    const string Unavailable = "—";

    public static string DayName(DateTime d, bool ar)
    {
        return ar ? DayAr[(int)d.DayOfWeek] : d.ToString("ddd", Inv);
    }

    // d supplies the weekday; hijriDay (default d) supplies the Hijri date, which turns over at Maghrib.
    public static string Hijri(DateTime d, bool ar, int adjust = 0, DateTime? hijriDay = null)
    {
        try
        {
            int y, m, day;
            global::Hijri.Of(hijriDay ?? d, adjust, out y, out m, out day);
            return ar
                ? DayAr[(int)d.DayOfWeek] + "، " + day + " " + HMonAr[m - 1] + " " + y + " هـ"
                : d.ToString("dddd", Inv) + ", " + day + " " + HMonEn[m - 1] + " " + y + " AH";
        }
        catch { return d.ToString("dddd, d MMMM yyyy", Inv); }
    }
}
