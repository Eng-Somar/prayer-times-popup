using System;
using System.Globalization;

public static class Hijri
{
    static readonly UmAlQuraCalendar Cal = new UmAlQuraCalendar();

    public static void Of(DateTime d, int adjust, out int year, out int month, out int day)
    {
        DateTime a = d.AddDays(adjust);
        year = Cal.GetYear(a);
        month = Cal.GetMonth(a);
        day = Cal.GetDayOfMonth(a);
    }

    public static bool IsRamadan(DateTime d, int adjust)
    {
        int y, m, day;
        try { Of(d, adjust, out y, out m, out day); } catch (ArgumentException) { return false; }
        return m == 9;
    }

    // The Hijri day begins at Maghrib, so the evening already belongs to the next one.
    public static DateTime HijriDay(DateTime now, DateTime maghrib)
    {
        return maghrib != DateTime.MinValue && now >= maghrib ? now.Date.AddDays(1) : now.Date;
    }

    // Why this date is a recommended fast: "white", "monthu", or null. Never suggests a day on which
    // fasting is forbidden (the two Eids and the days of Tashreeq) or Ramadan, which needs no reminder.
    public static string FastReason(DateTime d, int adjust)
    {
        int y, m, day;
        try { Of(d, adjust, out y, out m, out day); } catch (ArgumentException) { return null; }
        if (m == 9) return null;
        if (m == 10 && day == 1) return null;
        if (m == 12 && day >= 10 && day <= 13) return null;
        if (day >= 13 && day <= 15) return "white";
        if (d.DayOfWeek == DayOfWeek.Monday || d.DayOfWeek == DayOfWeek.Thursday) return "monthu";
        return null;
    }
}
