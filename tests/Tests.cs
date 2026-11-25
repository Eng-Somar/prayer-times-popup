using System;
using System.Collections.Generic;

// Minimal dependency-free test runner: run with test.ps1.
public static class TestRunner
{
    static int passed, failed;

    static void Eq<T>(string name, T expected, T actual)
    {
        if (EqualityComparer<T>.Default.Equals(expected, actual)) passed++;
        else { failed++; Console.WriteLine("FAIL " + name + ": expected [" + expected + "] got [" + actual + "]"); }
    }

    static void True(string name, bool ok) { Eq(name, true, ok); }

    static void Near(string name, double expected, double actual, double tolerance)
    {
        if (Math.Abs(expected - actual) <= tolerance) passed++;
        else { failed++; Console.WriteLine("FAIL " + name + ": expected " + expected + " +/- " + tolerance + " got " + actual); }
    }

    public static int Main()
    {
        V11Existing();
        V11Location();
        V11Schedule();
        V11Misc();
        V12Reminders();
        V12Fasting();
        V12Qibla();
        V12Tasbih();
        V13Adhkar();
        SettingsFile();
        ReferenceTimes();
        UaeTimetable();
        IndependentSolarCheck();
        ThemeContrast();
        PrayedSuppressesReminder();
        Stats();
        AdhkarSuggestion();
        MotionLogic();
        RingLogic();
        NowLine();
        StatsWeeks();
        StepperLogic();
        Console.WriteLine(passed + " passed, " + failed + " failed");
        return failed == 0 ? 0 : 1;
    }

    static double Luminance(System.Drawing.Color c)
    {
        Func<int, double> f = v => { double s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); };
        return 0.2126 * f(c.R) + 0.7152 * f(c.G) + 0.0722 * f(c.B);
    }

    static double Contrast(System.Drawing.Color a, System.Drawing.Color b)
    {
        double x = Luminance(a), y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }

    static void ThemeContrast()
    {
        foreach (Theme t in Theme.All)
        {
            True(t.Key + ": text on background", Contrast(t.Fg, t.Bg) >= 4.5);
            True(t.Key + ": text on surface", Contrast(t.Fg, t.Surface) >= 4.5);
            True(t.Key + ": muted on background " + Contrast(t.Muted, t.Bg).ToString("0.00"), Contrast(t.Muted, t.Bg) >= 4.5);
            True(t.Key + ": muted on surface " + Contrast(t.Muted, t.Surface).ToString("0.00"), Contrast(t.Muted, t.Surface) >= 4.5);
            True(t.Key + ": text on accent " + Contrast(t.AccentFg, t.Accent).ToString("0.00"), Contrast(t.AccentFg, t.Accent) >= 4.5);
            True(t.Key + ": accent on background " + Contrast(t.Accent, t.Bg).ToString("0.00"), Contrast(t.Accent, t.Bg) >= 3);
        }
    }

    static void Stats()
    {
        var s = new PrayerStats();
        True("starts empty", s.IsEmpty);
        s.SetPrayed(D, 3, true);
        True("marks a prayer", s.IsPrayed(D, 3));
        Eq("other prayers stay unmarked", false, s.IsPrayed(D, 2));
        Eq("other days stay unmarked", false, s.IsPrayed(D.AddDays(-1), 3));
        s.SetPrayed(D, 3, false);
        Eq("a mark can be corrected", false, s.IsPrayed(D, 3));

        s = new PrayerStats();
        s.SetPrayed(D.AddDays(-6), 0, true);
        s.SetPrayed(D.AddDays(-7), 0, true);
        s.SetPrayed(D, 2, true);
        s.SetPrayed(D, 2, true);
        s.AddTasbih(D, 33);
        s.AddTasbih(D, 1);
        s.AddTasbih(D.AddDays(-7), 100);
        s.MarkAdhkar(D, AdhkarSet.Morning);
        s.MarkAdhkar(D, AdhkarSet.Morning);
        s.MarkAdhkar(D.AddDays(-1), AdhkarSet.Sleep);
        Eq("tasbih on a day", 34, s.TasbihOn(D));
        StatsRange week = s.Range(D, 7);
        Eq("range covers exactly 7 days of prayers", 2, week.Prayed);
        Eq("range covers exactly 7 days of tasbih", 34, week.Tasbih);
        Eq("repeating a set counts once a day", 2, week.Adhkar);
        Eq("today alone", 1, s.Range(D, 1).Prayed);
        Eq("today possible", 5, s.Range(D, 1).Possible);
        Eq("possible counts every day in range once history covers it", 35, week.Possible);
        var fresh = new PrayerStats();
        fresh.SetPrayed(D.AddDays(-1), 0, true);
        Eq("possible starts from the first recorded day, so new users see a fair total", 10, fresh.Range(D, 30).Possible);
        Eq("no history still counts today", 5, new PrayerStats().Range(D, 30).Possible);

        var st = new PrayerStats();
        Eq("no streak yet", 0, st.FullDaysStreak(D));
        foreach (int back in new[] { 1, 2, 4 })
            foreach (int i in Scheduler.Five) st.SetPrayed(D.AddDays(-back), i, true);
        Eq("an unfinished today does not break the streak", 2, st.FullDaysStreak(D));
        foreach (int i in Scheduler.Five) st.SetPrayed(D, i, true);
        Eq("a finished today extends the streak", 3, st.FullDaysStreak(D));

        s.SetPrayed(D, 5, true);
        PrayerStats back2 = PrayerStats.Parse(s.Serialize());
        True("saved mark survives", back2.IsPrayed(D, 5));
        Eq("saved tasbih survives", 34, back2.TasbihOn(D));
        Eq("saved adhkar survive", 1, back2.Range(D, 1).Adhkar);
        Eq("saved old day survives", 100, back2.TasbihOn(D.AddDays(-7)));
        True("corrupt file is safe", PrayerStats.Parse("garbage\n2026-13-99\t1\t2\t3\nx\ty\tz\tw").IsEmpty);
        True("null file is safe", PrayerStats.Parse(null).IsEmpty);
        Eq("corrupt numbers are ignored", 0, PrayerStats.Parse("2026-11-06\tabc\t-5\tq").TasbihOn(D));
        back2.Clear();
        True("clear removes everything", back2.IsEmpty);

        DateTime[] spill = Day(D);
        spill[5] = D.AddDays(1).AddMinutes(12);
        Eq("Isha after midnight still belongs to its own day", D, Scheduler.OwnerDay(spill));
        True("a passed prayer can be marked", Scheduler.CanMark(D.AddHours(16), D.AddHours(15)));
        Eq("a future prayer cannot be marked", false, Scheduler.CanMark(D.AddHours(14), D.AddHours(15)));
        Eq("an unavailable prayer cannot be marked", false, Scheduler.CanMark(D, DateTime.MinValue));

        Eq("English day name", "Fri", Lang.DayName(D, false));
        Eq("Arabic day name", "الجمعة", Lang.DayName(D, true));
    }

    static void AdhkarSuggestion()
    {
        DateTime[] day = Day(D);
        Eq("after Fajr suggests morning", AdhkarSet.Morning, Adhkar.Suggest(D.AddHours(9), day));
        Eq("after Asr suggests evening", AdhkarSet.Evening, Adhkar.Suggest(D.AddHours(16), day));
        Eq("after Isha suggests sleep", AdhkarSet.Sleep, Adhkar.Suggest(D.AddHours(23), day));
        Eq("before Fajr suggests sleep", AdhkarSet.Sleep, Adhkar.Suggest(D.AddHours(2), day));
        Eq("unknown times fall back to the clock", AdhkarSet.Evening, Adhkar.Suggest(D.AddHours(16), new DateTime[6]));
        Eq("title of a set", "adhkarS", Adhkar.TitleKey(AdhkarSet.Sleep));
    }

    static void MotionLogic()
    {
        var t = new Tween(200, null);
        t.Target = 1;
        True("a tween keeps going before its time is up", t.Step(100));
        Near("a tween moves at a steady rate", 0.5, t.Value, 0.001);
        Eq("a tween stops when it arrives", false, t.Step(100));
        Eq("a tween lands exactly on target", 1.0, t.Value);
        Eq("a big time jump does not overshoot", false, t.Step(10000));
        Eq("still on target", 1.0, t.Value);
        t.Target = 0;
        t.Step(50);
        Near("a tween can be interrupted and reversed", 0.75, t.Value, 0.001);
        Eq("easing starts at zero", 0.0, Ease.OutCubic(0));
        Eq("easing ends at one", 1.0, Ease.OutCubic(1));
        True("easing decelerates", Ease.OutCubic(0.5) > 0.5);
        Eq("easing clamps past the end", 1.0, Ease.OutCubic(5));
        Eq("easing clamps before the start", 0.0, Ease.OutCubic(-1));
        Near("the pop curve ends at rest", 1.0, Ease.OutBack(1), 0.0001);
        True("the pop curve overshoots slightly", Ease.OutBack(0.7) > 1 && Ease.OutBack(0.7) < 1.2);
    }

    static void RingLogic()
    {
        Eq("33 target has 33 beads", 33, TasbihRing.Beads(33));
        Eq("34 target has 34 beads", 34, TasbihRing.Beads(34));
        Eq("large targets cycle on 33 beads", 33, TasbihRing.Beads(1000));
        Eq("small targets use one bead each", 7, TasbihRing.Beads(7));
        Eq("nothing filled at zero", 0, TasbihRing.Filled(0, 33, false));
        Eq("one bead per count", 12, TasbihRing.Filled(12, 33, false));
        Eq("finished fills the ring", 34, TasbihRing.Filled(34, 34, true));
        Eq("large target wraps", 1, TasbihRing.Filled(34, 100, false));
        Eq("large target shows a full ring at each 33", 33, TasbihRing.Filled(66, 100, false));
    }

    static void NowLine()
    {
        DateTime[] day = Day(D);
        Eq("before Fajr sits at the start", 0.0, PrayerDay.NowPosition(D.AddHours(3), day));
        Eq("at Fajr sits on the first prayer", 0.0, PrayerDay.NowPosition(D.AddHours(5), day));
        Eq("at Dhuhr sits on the second prayer", 1.0, PrayerDay.NowPosition(D.AddHours(12), day));
        Eq("halfway from Dhuhr to Asr", 1.5, PrayerDay.NowPosition(D.AddHours(13.5), day));
        Eq("after Isha sits at the end", 4.0, PrayerDay.NowPosition(D.AddHours(23), day));
        Eq("a card for tomorrow shows no line", -1.0, PrayerDay.NowPosition(D.AddDays(-1).AddHours(23), day));
        DateTime[] polar = Day(D); polar[4] = DateTime.MinValue;
        Eq("unavailable times show no line", -1.0, PrayerDay.NowPosition(D.AddHours(13), polar));
    }

    static void StatsWeeks()
    {
        Eq("newest column is today", D, StatsPage.Day(D, 0, 6));
        Eq("oldest column is six days ago", D.AddDays(-6), StatsPage.Day(D, 0, 0));
        Eq("one week back, newest column", D.AddDays(-7), StatsPage.Day(D, 1, 6));
        Eq("one week back, oldest column", D.AddDays(-13), StatsPage.Day(D, 1, 0));
    }

    static void StepperLogic()
    {
        Eq("steps up", 11, Stepper.Next(10, 1, 0, 60, false));
        Eq("stops at the top", 60, Stepper.Next(60, 1, 0, 60, false));
        Eq("stops at the bottom", -60, Stepper.Next(-60, -1, -60, 60, false));
        Eq("a time wraps past midnight", 0, Stepper.Next(1435, 5, 0, 1439, true));
        Eq("a time wraps back before midnight", 1435, Stepper.Next(0, -5, 0, 1439, true));
    }

    static void PrayedSuppressesReminder()
    {
        var o = new ScheduleOptions();
        DateTime t = D.AddHours(15), now = t.AddMinutes(3);
        var fired = new HashSet<string> { Scheduler.PrayedKey(t, 3) };
        Eq("a prayer already marked doesn't remind again after a restart", null, First(now, Days(now), o, fired));
    }

    static readonly DateTime D = new DateTime(2026, 11, 6);

    static DateTime[] Day(DateTime date)
    {
        date = date.Date;
        return new[] { date.AddHours(5), date.AddHours(6), date.AddHours(12), date.AddHours(15), date.AddHours(18), date.AddHours(19) };
    }

    static DateTime[][] Days(DateTime date) { return new[] { Day(date.AddDays(-1)), Day(date), Day(date.AddDays(1)) }; }

    static DueEvent First(DateTime now, DateTime[][] days, ScheduleOptions o, HashSet<string> fired)
    {
        List<DueEvent> due = Scheduler.Due(now, days, o, fired);
        return due.Count == 0 ? null : due[0];
    }

    static DueEvent First(DateTime now, ScheduleOptions o)
    {
        return First(now, Days(now), o, new HashSet<string>());
    }

    static EventKind KindAt(DateTime now, ScheduleOptions o)
    {
        DueEvent e = First(now, o);
        return e == null ? EventKind.None : e.Kind;
    }

    static void V11Location()
    {
        Near("distance Dubai to Riyadh", 870, Geo.DistanceKm(25.2, 55.27, 24.71, 46.68), 25);
        Eq("first detection applies", LocationDecision.Apply, LocationPolicy.Decide(false, false, 0, 0, 25.2, 55.27, false, 0, 0));
        Eq("small move applies", LocationDecision.Apply, LocationPolicy.Decide(true, false, 25.2, 55.27, 25.35, 55.42, false, 0, 0));
        Eq("VPN-sized jump is only offered", LocationDecision.Offer, LocationPolicy.Decide(true, false, 25.2, 55.27, 52.37, 4.9, false, 0, 0));
        Eq("an offer is not repeated", LocationDecision.Ignore, LocationPolicy.Decide(true, false, 25.2, 55.27, 52.4, 4.95, true, 52.37, 4.9));
        Eq("a new far place is offered again", LocationDecision.Offer, LocationPolicy.Decide(true, false, 25.2, 55.27, 40.7, -74, true, 52.37, 4.9));
        Eq("user refresh always applies", LocationDecision.Apply, LocationPolicy.Decide(true, true, 25.2, 55.27, 52.37, 4.9, false, 0, 0));

        Eq("manual city far away clears the country", "", LocationPolicy.CountryFor("AE", 25.2, 55.27, 21.42, 39.83));
        Eq("manual small correction keeps the country", "AE", LocationPolicy.CountryFor("AE", 25.2, 55.27, 25.25, 55.3));
    }

    static void V11Schedule()
    {
        var o = new ScheduleOptions();
        Eq("before window", EventKind.Before, KindAt(D.AddHours(14).AddMinutes(55), o));
        Eq("nothing outside windows", EventKind.None, KindAt(D.AddHours(14), o));
        DueEvent at = First(D.AddHours(15).AddSeconds(30), o);
        Eq("at prayer time", EventKind.At, at.Kind);
        Eq("on time is not late", false, at.Late);
        DueEvent late = First(D.AddHours(15).AddMinutes(14), o);
        Eq("started late still reminds", EventKind.At, late.Kind);
        Eq("started late is flagged so it stays silent", true, late.Late);
        Eq("after the window nothing fires", EventKind.None, KindAt(D.AddHours(15).AddMinutes(16), o));

        var fired = new HashSet<string>();
        DateTime now = D.AddHours(15).AddMinutes(1);
        DueEvent e = First(now, Days(now), o, fired);
        fired.Add(e.Key);
        Eq("fires once", null, First(now.AddSeconds(10), Days(now), o, fired));

        // Isha after midnight belongs to yesterday's times and must still fire after the rollover.
        DateTime yesterday = D.AddDays(-1);
        DateTime[] night = Day(yesterday);
        night[5] = D.AddMinutes(12);
        DateTime[][] across = { night, Day(D), Day(D.AddDays(1)) };
        DueEvent isha = First(D.AddMinutes(13), across, o, new HashSet<string>());
        Eq("Isha after midnight fires", 5, isha == null ? -1 : isha.Index);

        // Unavailable (polar) times never fire.
        DateTime[] polar = Day(D);
        polar[4] = DateTime.MinValue;
        DateTime[][] pd = { polar, polar, polar };
        Eq("unavailable time is skipped", null, First(new DateTime(1, 1, 1, 0, 5, 0), pd, o, new HashSet<string>()));

        var off = new ScheduleOptions();
        off.Remind[3] = false;
        Eq("per-prayer switch silences it", EventKind.None, KindAt(D.AddHours(15).AddMinutes(1), off));

        DateTime[] adjusted = PrayCalc.ApplyOffsets(Day(D), new[] { 0, 0, 2, -3, 0, 0 });
        Eq("positive offset", D.AddHours(12).AddMinutes(2), adjusted[2]);
        Eq("negative offset", D.AddHours(15).AddMinutes(-3), adjusted[3]);
        DateTime[] unav = Day(D); unav[4] = DateTime.MinValue;
        Eq("offset keeps unavailable", DateTime.MinValue, PrayCalc.ApplyOffsets(unav, new[] { 0, 0, 0, 0, 5, 0 })[4]);

        DateTime future = D.AddHours(30);
        True("manual quiet defers", Quiet.Defer(5, false, D, future));
        True("fullscreen defers when enabled", Quiet.Defer(3, true, D, DateTime.MinValue));
        True("presentation mode defers when enabled", Quiet.Defer(4, true, D, DateTime.MinValue));
        Eq("fullscreen ignored when disabled", false, Quiet.Defer(3, false, D, DateTime.MinValue));
        Eq("normal desktop shows", false, Quiet.Defer(5, true, D, DateTime.MinValue));
        Eq("expired quiet shows", false, Quiet.Defer(5, false, future.AddDays(1), future));
    }

    static void V11Misc()
    {
        True("newer version", UpdateCheck.IsNewer("1.0.0", "v1.3.0"));
        Eq("same version", false, UpdateCheck.IsNewer("1.3.0", "v1.3.0"));
        Eq("older version", false, UpdateCheck.IsNewer("1.3.0", "1.2.9"));
        Eq("two digit minor", true, UpdateCheck.IsNewer("1.9.0", "1.10.0"));
        Eq("garbage is not newer", false, UpdateCheck.IsNewer("1.3.0", "latest"));

        var cal = new System.Globalization.UmAlQuraCalendar();
        DateTime first = cal.ToDateTime(1448, 9, 1, 12, 0, 0, 0);
        True("Ramadan detected", Hijri.IsRamadan(first, 0));
        Eq("day before is Sha'ban", false, Hijri.IsRamadan(first.AddDays(-1), 0));
        True("adjust moves Ramadan a day earlier", Hijri.IsRamadan(first.AddDays(-1), 1));
        DateTime maghrib = D.AddHours(18);
        Eq("Hijri day holds before Maghrib", D, Hijri.HijriDay(D.AddHours(17), maghrib));
        Eq("Hijri day rolls at Maghrib", D.AddDays(1), Hijri.HijriDay(D.AddHours(18), maghrib));
        Eq("unknown Maghrib never rolls", D, Hijri.HijriDay(D.AddHours(23), DateTime.MinValue));
        Eq("unavailable time prints a dash", "—", Lang.Time(DateTime.MinValue, false, false));
        Eq("unavailable short time prints a dash", "—", Lang.ShortTime(DateTime.MinValue, true));
    }

    static void V12Reminders()
    {
        var o = new ScheduleOptions();
        o.Iqama[3] = 10;
        DateTime iq = D.AddHours(15).AddMinutes(10);
        var fired = new HashSet<string>();
        fired.Add(Scheduler.Key(EventKind.At, D.AddHours(15), 3));
        Eq("iqama fires", EventKind.Iqama, First(iq, Days(iq), o, fired).Kind);
        fired.Add(Scheduler.PrayedKey(D.AddHours(15), 3));
        Eq("no iqama after marked prayed", null, First(iq, Days(iq), o, fired));

        var s = new ScheduleOptions();
        s.Snoozes.Add(new DueEvent { Kind = EventKind.At, Index = 3, Time = D.AddHours(15), Day = Day(D), Key = "snooze", Until = D.AddHours(15).AddMinutes(5) });
        var f2 = new HashSet<string> { Scheduler.Key(EventKind.At, D.AddHours(15), 3) };
        DateTime t1 = D.AddHours(15).AddMinutes(4), t2 = D.AddHours(15).AddMinutes(5);
        Eq("snooze waits", null, First(t1, Days(t1), s, f2));
        Eq("snooze returns", 3, First(t2, Days(t2), s, f2).Index);
        f2.Add(Scheduler.PrayedKey(D.AddHours(15), 3));
        Eq("snooze cancelled by prayed", null, First(t2, Days(t2), s, f2));

        var cal = new System.Globalization.UmAlQuraCalendar();
        DateTime r = cal.ToDateTime(1448, 9, 10, 0, 0, 0, 0);
        var ro = new ScheduleOptions { RemindBefore = 0, Imsak = true, ImsakMinutes = 10 };
        Eq("imsak fires in Ramadan", EventKind.Imsak, KindAt(r.AddHours(4).AddMinutes(51), ro));
        Eq("no imsak outside Ramadan", EventKind.None, KindAt(D.AddHours(4).AddMinutes(51), ro));
        var fo = new ScheduleOptions { FastNotice = true, RemindBefore = 0 };
        DateTime sunday = new DateTime(2026, 11, 8);
        DueEvent fn = First(sunday.AddHours(20), fo);
        Eq("fast notice on the eve of Monday", EventKind.FastNotice, fn == null ? EventKind.None : fn.Kind);
        Eq("fast notice names the reason", "monthu", fn == null ? "" : fn.Reason);
        Eq("no fast notice by default", EventKind.None, KindAt(sunday.AddHours(20), new ScheduleOptions { RemindBefore = 0 }));
    }

    static void V12Fasting()
    {
        var cal = new System.Globalization.UmAlQuraCalendar();
        Func<int, int, DateTime> h = (m, d) => cal.ToDateTime(1448, m, d, 12, 0, 0, 0);
        DateTime tuesdayish = h(6, 13);
        Eq("white day", "white", Hijri.FastReason(tuesdayish, 0));
        Eq("Monday", "monthu", Hijri.FastReason(new DateTime(2026, 11, 9), 0));
        Eq("Thursday", "monthu", Hijri.FastReason(new DateTime(2026, 11, 5), 0));
        Eq("plain Tuesday", null, Hijri.FastReason(new DateTime(2026, 11, 3), 0));
        Eq("Ramadan needs no reminder", null, Hijri.FastReason(h(9, 13), 0));
        Eq("Eid al-Fitr is excluded", null, Hijri.FastReason(h(10, 1), 0));
        Eq("Eid al-Adha is excluded", null, Hijri.FastReason(h(12, 10), 0));
        Eq("Tashreeq day is excluded even though it is a white day", null, Hijri.FastReason(h(12, 13), 0));
    }

    static void V12Qibla()
    {
        Near("Qibla from Dubai", 258, Geo.QiblaBearing(25.2, 55.27), 1.5);
        Near("Qibla from New York", 58.5, Geo.QiblaBearing(40.71, -74.0), 1.5);
        Near("Qibla from London", 119, Geo.QiblaBearing(51.5, -0.13), 1.5);
    }

    static void V12Tasbih()
    {
        var t = new TasbihModel();
        for (int i = 0; i < 32; i++) t.Tap(D);
        Eq("counts", 32, t.Count);
        Eq("first phrase", 0, t.Phase);
        Eq("phase completes", TasbihResult.PhaseDone, t.Tap(D));
        Eq("moves to the next phrase", 1, t.Phase);
        Eq("count restarts", 0, t.Count);
        for (int i = 0; i < 33; i++) t.Tap(D);
        Eq("third phrase", 2, t.Phase);
        Eq("third phrase target", 34, t.Target);
        for (int i = 0; i < 33; i++) t.Tap(D);
        Eq("last tap finishes", TasbihResult.AllDone, t.Tap(D));
        True("finished state", t.Finished);
        Eq("day total", 100, t.DayTotal);
        t.Tap(D);
        Eq("tap after finish starts over", 1, t.Count);
        Eq("back to first phrase", 0, t.Phase);

        var free = new TasbihModel { Mode = 1, FreeTarget = 3 };
        free.Tap(D); free.Tap(D);
        Eq("free completes a round", TasbihResult.AllDone, free.Tap(D));
        Eq("free counts rounds", 1, free.Rounds);
        free.Tap(D.AddDays(1));
        Eq("day total resets on a new day", 1, free.DayTotal);

        var saved = new TasbihModel { Mode = 1, FreeTarget = 99, FreePhrase = 2 };
        saved.Tap(D);
        TasbihModel loaded = TasbihModel.Parse(saved.Serialize(), D);
        Eq("restores count", 1, loaded.Count);
        Eq("restores mode", 1, loaded.Mode);
        Eq("restores phrase", 2, loaded.FreePhrase);
        Eq("old day is cleared", 0, TasbihModel.Parse(saved.Serialize(), D.AddDays(1)).Count);
        Eq("corrupt file is safe", 0, TasbihModel.Parse("garbage\nCount=abc\nPhase=99", D).Phase);
    }

    static void V13Adhkar()
    {
        List<Dhikr> morning = Adhkar.Load(AdhkarSet.Morning), evening = Adhkar.Load(AdhkarSet.Evening);
        List<Dhikr> after = Adhkar.Load(AdhkarSet.AfterPrayer), sleep = Adhkar.Load(AdhkarSet.Sleep);
        Eq("morning count", 23, morning.Count);
        Eq("evening count", 21, evening.Count);
        Eq("after-prayer count", 8, after.Count);
        Eq("sleep count", 13, sleep.Count);
        foreach (List<Dhikr> set in new[] { morning, evening, after, sleep })
            foreach (Dhikr d in set)
            {
                True("text present for " + d.Id, d.Text.Length > 5);
                True("repeat valid for " + d.Id, d.Repeat >= 1);
                True("tatweel stripped for " + d.Id, d.Text.IndexOf('ـ') < 0);
            }
        True("morning-only item excluded from evening", !evening.Exists(d => d.Id == 93));
        True("evening-only item excluded from morning", !morning.Exists(d => d.Id == 97));
        Eq("repeat follows the text when the field disagrees", 7, morning.Find(d => d.Id == 83).Repeat);

        var off = new ScheduleOptions { RemindBefore = 0 };
        Eq("adhkar are off by default", EventKind.None, KindAt(D.AddHours(5).AddMinutes(25), off));
        var on = new ScheduleOptions { RemindBefore = 0, AdhkarMorning = true, AdhkarEvening = true, AdhkarSleep = true, SleepMinute = 22 * 60 + 30, AdhkarAfter = true, AdhkarAfterMinutes = 5 };
        var noAt = new HashSet<string>();
        foreach (int i in new[] { 0, 2, 3, 4, 5 }) noAt.Add(Scheduler.Key(EventKind.At, Day(D)[i], i));
        Eq("morning adhkar", EventKind.AdhkarMorning, First(D.AddHours(5).AddMinutes(25), Days(D), on, noAt).Kind);
        Eq("evening adhkar", EventKind.AdhkarEvening, First(D.AddHours(15).AddMinutes(25), Days(D), on, noAt).Kind);
        Eq("after-prayer adhkar", EventKind.AdhkarAfter, First(D.AddHours(12).AddMinutes(6), Days(D), on, noAt).Kind);
        Eq("sleep adhkar", EventKind.AdhkarSleep, First(D.AddHours(22).AddMinutes(31), Days(D), on, noAt).Kind);
    }

    // Recorded from api.aladhan.com for 2026-10-05 (UTC). Guards the calculation against regressions.
    static void ReferenceTimes()
    {
        Ref("Makkah", "makkah", 21.4225, 39.8262, "01:57 03:13 09:09 15:05 16:35");
        Ref("London", "mwl", 51.5074, -0.1278, "04:16 06:08 11:49 17:29 19:14");
        Ref("New York", "isna", 40.7128, -74.006, "09:42 10:57 16:44 22:31 23:46");
        Ref("Cairo", "egypt", 30.0444, 31.2357, "02:24 03:51 09:43 15:36 16:53");
    }

    // Dubai monthly timetable published by Emirates 24|7 from IACAD data, November 2026.
    static void UaeTimetable()
    {
        Uae(1, "05:08 06:23 12:05 15:15 17:42 18:57");
        Uae(6, "05:10 06:26 12:05 15:13 17:39 18:54");
        Uae(15, "05:15 06:32 12:06 15:10 17:35 18:51");
        Uae(30, "05:24 06:42 12:10 15:09 17:32 18:50");
    }

    // Every season and both hemispheres against a second calculation that shares no code with PrayCalc.
    static void IndependentSolarCheck()
    {
        // Latitude, longitude. Places where the sun always rises and sets.
        double[][] places =
        {
            new[] { 21.4225, 39.8262 }, new[] { 51.5074, -0.1278 }, new[] { 40.7128, -74.006 }, new[] { 30.0444, 31.2357 },
            new[] { 59.9139, 10.7522 }, new[] { -6.2088, 106.8456 }, new[] { -33.8688, 151.2093 }, new[] { 25.2, 55.27 },
        };
        MethodDef mwl = PrayCalc.Find("mwl");
        foreach (double[] p in places)
            for (int month = 1; month <= 12; month++)
            {
                var date = new DateTime(2026, month, 15);
                string at = " at " + p[0] + "," + p[1] + " in month " + month;
                DateTime[] standard = PrayCalc.Compute(date, p[0], p[1], mwl, 1, 0);
                DateTime[] hanafi = PrayCalc.Compute(date, p[0], p[1], mwl, 2, 0);
                NearUtc("sunrise" + at, SolarReference.Crossing(date, p[0], p[1], -0.833, -1), standard[1], date);
                NearUtc("Dhuhr" + at, SolarReference.Noon(date, p[1]), standard[2], date);
                NearUtc("standard Asr" + at, SolarReference.Asr(date, p[0], p[1], 1), standard[3], date);
                NearUtc("Hanafi Asr" + at, SolarReference.Asr(date, p[0], p[1], 2), hanafi[3], date);
                NearUtc("Maghrib" + at, SolarReference.Crossing(date, p[0], p[1], -0.833, 1), standard[4], date);
            }
    }

    // Half a minute of rounding, plus the difference between the two models. Compares the time of day.
    static void NearUtc(string name, double expectedMinutes, DateTime actual, DateTime date)
    {
        double tz = TimeZoneInfo.Local.GetUtcOffset(date.AddHours(12)).TotalHours;
        double diff = ((actual.AddHours(-tz) - date).TotalMinutes - expectedMinutes) % 1440;
        if (diff > 720) diff -= 1440;
        if (diff < -720) diff += 1440;
        Near(name, 0, diff, 1);
    }

    static void Uae(int day, string published)
    {
        var date = new DateTime(2026, 11, day);
        double fromLocal = 4 - TimeZoneInfo.Local.GetUtcOffset(date.AddHours(12)).TotalHours;
        DateTime[] t = PrayCalc.Compute(date, 25.2, 55.27, PrayCalc.Find("dubai"), 1, 0);
        string[] expected = published.Split(new[] { (char)32 });
        string[] names = { "Fajr", "Sunrise", "Dhuhr", "Asr", "Maghrib", "Isha" };
        for (int i = 0; i < 6; i++)
        {
            TimeSpan e = TimeSpan.Parse(expected[i]);
            Near("UAE " + names[i] + " on Nov " + day, e.TotalMinutes, (t[i].AddHours(fromLocal) - date).TotalMinutes, 1);
        }
    }

    static void Ref(string city, string method, double lat, double lon, string expected)
    {
        var day = new DateTime(2026, 10, 5);
        double tz = TimeZoneInfo.Local.GetUtcOffset(day.AddHours(12)).TotalHours;
        DateTime[] t = PrayCalc.Compute(day, lat, lon, PrayCalc.Find(method), 1, 0);
        string actual = "";
        foreach (int i in new[] { 0, 1, 2, 4, 5 }) actual += (actual == "" ? "" : " ") + t[i].AddHours(-tz).ToString("HH:mm");
        Eq(city + " matches the reference", expected, actual);
    }

    static void SettingsFile()
    {
        var s = new Settings { City = "مسقط", Lat = 23.588, Lon = 58.38, HasLoc = true, HijriAdjust = -1, AdhkarSleep = true, SleepMinute = 1400, FastNotice = true };
        s.Offsets[2] = 3;
        s.Iqama[3] = 15;
        s.RemindBy[4] = false;
        s.SoundBy[0] = false;
        Settings back = Settings.Parse(s.Serialize());
        Eq("city survives", "مسقط", back.City);
        Eq("offset survives", 3, back.Offsets[2]);
        Eq("iqama survives", 15, back.Iqama[3]);
        Eq("remind switch survives", false, back.RemindBy[4]);
        Eq("sound switch survives", false, back.SoundBy[0]);
        Eq("hijri adjust survives", -1, back.HijriAdjust);
        Eq("sleep time survives", 1400, back.SleepMinute);

        Settings v10 = Settings.Parse("Language=ar\nTheme=midnight\nCity=Dubai\nHasLoc=1\nLat=25.1\nLon=55.3\nSound=0\nRemindBefore=7");
        Eq("v1.0 file keeps theme", "midnight", v10.Theme);
        Eq("v1.0 file keeps reminder", 7, v10.RemindBefore);
        Eq("v1.0 sound off applies to every prayer", false, v10.SoundBy[3]);
        True("v1.0 file reminds for every prayer", v10.RemindBy[3]);
        Eq("adhkar stay off for v1.0 users", false, v10.AdhkarMorning);
        Eq("corrupt arrays fall back", 0, Settings.Parse("Offsets=a,b").Offsets[0]);
        Eq("offset is clamped", 60, Settings.Parse("Offsets=999,0,0,0,0,0").Offsets[0]);

        Settings copy = s.Clone();
        copy.Offsets[2] = 9;
        Eq("clone doesn't share arrays, so Cancel really cancels", 3, s.Offsets[2]);

        ScheduleOptions o = s.ToSchedule();
        Eq("schedule gets the iqama", 15, o.Iqama[3]);
        Eq("schedule gets the switch", false, o.Remind[4]);
    }

    // Behaviour bugs reproduced against the shipped v1.0 API.
    static void V11Existing()
    {
        var makkah = PrayCalc.Find("makkah");
        DateTime[] ramadan = PrayCalc.Compute(new DateTime(2027, 2, 20), 21.42, 39.83, makkah, 1, 0);
        Eq("Umm al-Qura Isha is 120 min after Maghrib in Ramadan", 120.0, (ramadan[5] - ramadan[4]).TotalMinutes);
        DateTime[] normal = PrayCalc.Compute(new DateTime(2026, 11, 6), 21.42, 39.83, makkah, 1, 0);
        Eq("Umm al-Qura Isha is 90 min outside Ramadan", 90.0, (normal[5] - normal[4]).TotalMinutes);

        var mwl = PrayCalc.Find("mwl");
        DateTime[] polarDay = PrayCalc.Compute(new DateTime(2026, 6, 21), 69.65, 18.96, mwl, 1, 0);
        Eq("polar day: sunrise is unavailable, not midnight", DateTime.MinValue, polarDay[1]);
        Eq("polar day: maghrib is unavailable, not midnight", DateTime.MinValue, polarDay[4]);
        DateTime[] polarNight = PrayCalc.Compute(new DateTime(2026, 12, 21), 69.65, 18.96, mwl, 1, 0);
        Eq("polar night: asr is unavailable", DateTime.MinValue, polarNight[3]);

        Eq("Arabic: one minute", "دقيقة واحدة", Lang.Duration(TimeSpan.FromMinutes(1), true));
        Eq("Arabic: two minutes", "دقيقتان", Lang.Duration(TimeSpan.FromMinutes(2), true));
        Eq("Arabic: three to ten", "5 دقائق", Lang.Duration(TimeSpan.FromMinutes(5), true));
        Eq("Arabic: eleven and over", "15 دقيقة", Lang.Duration(TimeSpan.FromMinutes(15), true));
        Eq("Arabic: two hours", "ساعتان", Lang.Duration(TimeSpan.FromHours(2), true));
        Eq("English: unchanged", "1h 5m", Lang.Duration(TimeSpan.FromMinutes(65), false));
    }
}
