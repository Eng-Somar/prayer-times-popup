using System;
using System.Collections.Generic;
using System.Globalization;

// Order matters: when several events are due at once, the lowest value is shown first.
public enum EventKind { None, At, Iqama, Imsak, Before, AdhkarAfter, AdhkarMorning, AdhkarEvening, AdhkarSleep, FastNotice }

public class DueEvent
{
    public EventKind Kind;
    public int Index;
    public DateTime Time;
    public DateTime[] Day;
    public string Key = "";
    public DateTime Until;   // snoozes only: when the reminder comes back
    public bool Late;        // the prayer time passed a while ago, so stay silent
    public string Reason;    // FastNotice only
}

public class ScheduleOptions
{
    public int RemindBefore = 10;
    public bool[] Remind = { true, true, true, true, true, true };
    public int[] Iqama = new int[6];
    public bool Imsak;
    public int ImsakMinutes = 10;
    public int HijriAdjust;
    public bool FastNotice;
    public bool AdhkarMorning, AdhkarEvening, AdhkarAfter, AdhkarSleep;
    public int AdhkarAfterMinutes = 5;
    public int SleepMinute = 22 * 60 + 30;
    public List<DueEvent> Snoozes = new List<DueEvent>();
}

// Decides what should be on screen right now. Holds no state and shows nothing: the caller marks an
// event's Key as fired only once it was really shown, so anything deferred stays due until its window ends.
public static class Scheduler
{
    public static readonly int[] Five = { 0, 2, 3, 4, 5 };
    const int AtWindow = 15, LateAfter = 2, IqamaWindow = 5, AfterWindow = 10, AdhkarDelay = 20, AdhkarWindow = 60, SleepWindow = 30, SnoozeWindow = 30;

    public static string Key(EventKind kind, DateTime t, int index)
    {
        return t.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + kind + index.ToString(CultureInfo.InvariantCulture);
    }

    public static string PrayedKey(DateTime t, int index)
    {
        return t.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture) + "Prayed" + index.ToString(CultureInfo.InvariantCulture);
    }

    // The date a day's times belong to. Isha can spill past midnight; Dhuhr never does.
    public static DateTime OwnerDay(DateTime[] day) { return day[2].Date; }

    public static bool CanMark(DateTime now, DateTime t) { return t != DateTime.MinValue && now >= t; }

    // days: yesterday, today and tomorrow, so times that spill over midnight are still seen.
    public static List<DueEvent> Due(DateTime now, DateTime[][] days, ScheduleOptions o, ICollection<string> fired)
    {
        var due = new List<DueEvent>();
        foreach (DateTime[] day in days)
        {
            foreach (int i in Five)
            {
                DateTime t = day[i];
                if (t == DateTime.MinValue) continue;
                bool prayed = fired.Contains(PrayedKey(t, i));
                if (o.Remind[i])
                {
                    if (In(now, t, AtWindow))
                    {
                        if (!prayed) Add(due, fired, EventKind.At, i, t, day, (now - t).TotalMinutes >= LateAfter);
                    }
                    else if (o.RemindBefore > 0 && now < t && now >= t.AddMinutes(-o.RemindBefore))
                        Add(due, fired, EventKind.Before, i, t, day, false);
                    if (o.Iqama[i] > 0 && !prayed && In(now, t.AddMinutes(o.Iqama[i]), IqamaWindow))
                        Add(due, fired, EventKind.Iqama, i, t, day, false);
                }
                if (o.AdhkarAfter && In(now, t.AddMinutes(o.AdhkarAfterMinutes), AfterWindow))
                    Add(due, fired, EventKind.AdhkarAfter, i, t, day, false);
            }

            DateTime fajr = day[0], asr = day[3];
            if (fajr != DateTime.MinValue)
            {
                if (o.Imsak && Hijri.IsRamadan(fajr, o.HijriAdjust) && now < fajr && now >= fajr.AddMinutes(-o.ImsakMinutes))
                    Add(due, fired, EventKind.Imsak, 0, fajr, day, false);
                if (o.AdhkarMorning && In(now, fajr.AddMinutes(AdhkarDelay), AdhkarWindow))
                    Add(due, fired, EventKind.AdhkarMorning, 0, fajr, day, false);
            }
            if (o.AdhkarEvening && asr != DateTime.MinValue && In(now, asr.AddMinutes(AdhkarDelay), AdhkarWindow))
                Add(due, fired, EventKind.AdhkarEvening, 3, asr, day, false);
        }

        DateTime[] today = days[1];
        if (o.AdhkarSleep && In(now, now.Date.AddMinutes(o.SleepMinute), SleepWindow))
            Add(due, fired, EventKind.AdhkarSleep, 5, now.Date.AddMinutes(o.SleepMinute), today, false);

        if (o.FastNotice && today[4] != DateTime.MinValue && now >= today[4].AddMinutes(30))
        {
            string reason = Hijri.FastReason(now.Date.AddDays(1), o.HijriAdjust);
            if (reason != null)
            {
                int before = due.Count;
                Add(due, fired, EventKind.FastNotice, 4, now.Date, today, false);
                if (due.Count > before) due[before].Reason = reason;
            }
        }

        foreach (DueEvent s in o.Snoozes)
        {
            if (fired.Contains(PrayedKey(s.Time, s.Index)) || now < s.Until || now >= s.Until.AddMinutes(SnoozeWindow)) continue;
            string key = s.Key + s.Until.ToString("HHmm", CultureInfo.InvariantCulture);
            if (!fired.Contains(key))
                due.Add(new DueEvent { Kind = s.Kind, Index = s.Index, Time = s.Time, Day = s.Day, Key = key, Late = true });
        }

        due.Sort((a, b) => a.Kind.CompareTo(b.Kind));
        return due;
    }

    static bool In(DateTime now, DateTime start, int minutes)
    {
        return now >= start && now < start.AddMinutes(minutes);
    }

    static void Add(List<DueEvent> due, ICollection<string> fired, EventKind kind, int index, DateTime t, DateTime[] day, bool late)
    {
        string key = Key(kind, t, index);
        if (!fired.Contains(key))
            due.Add(new DueEvent { Kind = kind, Index = index, Time = t, Day = day, Key = key, Late = late });
    }
}

public static class Quiet
{
    // shellState comes from SHQueryUserNotificationState: 2 busy/fullscreen, 3 Direct3D fullscreen,
    // 4 presentation mode, 6 Windows quiet hours.
    public static bool Defer(int shellState, bool respectFullscreen, DateTime now, DateTime quietUntil)
    {
        if (now < quietUntil) return true;
        return respectFullscreen && (shellState == 2 || shellState == 3 || shellState == 4 || shellState == 6);
    }
}

public static class PrayerDay
{
    // Where now falls along today's five prayers: 0 at Fajr, 4 at Isha. -1 when these aren't today's
    // times, or some of them don't occur here.
    public static double NowPosition(DateTime now, DateTime[] day)
    {
        foreach (int i in Scheduler.Five) if (day[i] == DateTime.MinValue) return -1;
        if (Scheduler.OwnerDay(day) != now.Date) return -1;
        if (now <= day[Scheduler.Five[0]]) return 0;
        for (int c = 0; c < 4; c++)
        {
            DateTime a = day[Scheduler.Five[c]], b = day[Scheduler.Five[c + 1]];
            if (now < b) return c + (now - a).TotalMinutes / (b - a).TotalMinutes;
        }
        return 4;
    }
}