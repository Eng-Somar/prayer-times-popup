using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

public class StatsRange
{
    public int Prayed, Possible, Tasbih, Adhkar;
}

// What you marked, by day. One line per day in stats.tsv: date, prayed bits, tasbih count, adhkar bits.
// Stays on this PC and only ever contains what you record yourself.
public class PrayerStats
{
    class Row { public int Prayed, Tasbih, Adhkar; }

    const int FullDay = 1 << 0 | 1 << 2 | 1 << 3 | 1 << 4 | 1 << 5; // Fajr, Dhuhr, Asr, Maghrib, Isha
    const int AllSets = 15;
    const string DateFormat = "yyyy-MM-dd";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static readonly string FilePath = Path.Combine(Settings.Dir, "stats.tsv");

    readonly SortedDictionary<DateTime, Row> rows = new SortedDictionary<DateTime, Row>();

    public bool IsEmpty { get { return rows.Count == 0; } }

    public void Clear() { rows.Clear(); }

    Row Get(DateTime day)
    {
        Row r;
        if (!rows.TryGetValue(day.Date, out r)) { r = new Row(); rows[day.Date] = r; }
        return r;
    }

    Row Peek(DateTime day)
    {
        Row r;
        return rows.TryGetValue(day.Date, out r) ? r : null;
    }

    public int Mask(DateTime day)
    {
        Row r = Peek(day);
        return r == null ? 0 : r.Prayed;
    }

    public bool IsPrayed(DateTime day, int index) { return (Mask(day) & 1 << index) != 0; }

    public void SetPrayed(DateTime day, int index, bool on)
    {
        Row r = Get(day);
        r.Prayed = on ? r.Prayed | 1 << index : r.Prayed & ~(1 << index);
        // An empty row would wrongly count as the first day of history.
        if (r.Prayed == 0 && r.Tasbih == 0 && r.Adhkar == 0) rows.Remove(day.Date);
    }

    public int TasbihOn(DateTime day)
    {
        Row r = Peek(day);
        return r == null ? 0 : r.Tasbih;
    }

    public void AddTasbih(DateTime day, int count) { Get(day).Tasbih += count; }

    public void MarkAdhkar(DateTime day, AdhkarSet set) { Get(day).Adhkar |= 1 << (int)set; }

    public StatsRange Range(DateTime today, int days)
    {
        var res = new StatsRange();
        for (int i = 0; i < days; i++)
        {
            Row r = Peek(today.AddDays(-i));
            if (r == null) continue;
            res.Prayed += Bits(r.Prayed);
            res.Tasbih += r.Tasbih;
            res.Adhkar += Bits(r.Adhkar);
        }
        DateTime first = today.Date;
        foreach (DateTime d in rows.Keys) { if (d < first) first = d; break; }
        res.Possible = 5 * Math.Min(days, (today.Date - first).Days + 1);
        return res;
    }

    // Consecutive days with all five marked. A today that isn't finished yet doesn't break it.
    public int FullDaysStreak(DateTime today)
    {
        DateTime d = today.Date;
        if (Mask(d) != FullDay) d = d.AddDays(-1);
        int streak = 0;
        while (Mask(d) == FullDay) { streak++; d = d.AddDays(-1); }
        return streak;
    }

    static int Bits(int v)
    {
        int n = 0;
        for (; v != 0; v &= v - 1) n++;
        return n;
    }

    public string Serialize()
    {
        var sb = new StringBuilder();
        foreach (KeyValuePair<DateTime, Row> kv in rows)
            sb.Append(kv.Key.ToString(DateFormat, Inv)).Append('\t').Append(kv.Value.Prayed).Append('\t')
              .Append(kv.Value.Tasbih).Append('\t').Append(kv.Value.Adhkar).Append('\n');
        return sb.ToString();
    }

    public static PrayerStats Parse(string text)
    {
        var s = new PrayerStats();
        foreach (string line in (text ?? "").Split('\n'))
        {
            string[] f = line.Trim().Split('\t');
            DateTime day;
            int prayed, tasbih, adhkar;
            if (f.Length != 4
                || !DateTime.TryParseExact(f[0], DateFormat, Inv, DateTimeStyles.None, out day)
                || !int.TryParse(f[1], NumberStyles.Integer, Inv, out prayed)
                || !int.TryParse(f[2], NumberStyles.Integer, Inv, out tasbih)
                || !int.TryParse(f[3], NumberStyles.Integer, Inv, out adhkar))
                continue;
            Row r = s.Get(day);
            r.Prayed = prayed & FullDay;
            r.Tasbih = Math.Max(0, tasbih);
            r.Adhkar = adhkar & AllSets;
        }
        return s;
    }

    public static PrayerStats Load()
    {
        try { if (File.Exists(FilePath)) return Parse(File.ReadAllText(FilePath, Encoding.UTF8)); }
        catch (IOException ex) { ErrorLog.Write(ex); }
        catch (UnauthorizedAccessException ex) { ErrorLog.Write(ex); }
        return new PrayerStats();
    }

    // Writes to a temporary file first, so a crash or power cut can't leave a half-written history.
    public void Save()
    {
        string tmp = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Settings.Dir);
            File.WriteAllText(tmp, Serialize(), new UTF8Encoding(false));
            if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
            else File.Move(tmp, FilePath);
        }
        catch (IOException ex) { ErrorLog.Write(ex); }
        catch (UnauthorizedAccessException ex) { ErrorLog.Write(ex); }
    }
}

public static class StatsPage
{
    // Column 6 is the newest day of the page; each page is one week further back.
    public static DateTime Day(DateTime today, int weekOffset, int column)
    {
        return today.Date.AddDays(-7 * weekOffset + column - 6);
    }
}