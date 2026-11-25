using System;
using System.Collections.Generic;
using System.Globalization;

public enum TasbihResult { Counted, PhaseDone, AllDone }

public class TasbihModel
{
    static readonly int[] PostPrayer = { 33, 33, 34 };
    public static readonly string[] PhrasesAr = { "سُبْحَانَ اللَّهِ", "الْحَمْدُ لِلَّهِ", "اللَّهُ أَكْبَرُ", "لَا إِلَهَ إِلَّا اللَّهُ", "أَسْتَغْفِرُ اللَّهَ", "سُبْحَانَ اللَّهِ وَبِحَمْدِهِ" };
    public static readonly string[] PhrasesEn = { "SubhanAllah", "Alhamdulillah", "Allahu Akbar", "La ilaha illa Allah", "Astaghfirullah", "SubhanAllahi wa bihamdihi" };

    public int Mode;            // 0 after-prayer sequence, 1 free count
    public int Phase, Count, Rounds, DayTotal;
    public int FreeTarget = 33, FreePhrase;
    public bool Finished;
    public DateTime Day = DateTime.MinValue;

    public int Target { get { return Mode == 0 ? PostPrayer[Phase] : FreeTarget; } }
    public int PhraseIndex { get { return Mode == 0 ? Phase : FreePhrase; } }

    public TasbihResult Tap(DateTime now)
    {
        if (Day != now.Date)
        {
            if (Day != DateTime.MinValue) ClearProgress();
            Day = now.Date;
        }
        if (Finished) { Finished = false; Phase = 0; Count = 0; }
        Count++;
        DayTotal++;
        if (Count < Target) return TasbihResult.Counted;
        if (Mode == 1) { Rounds++; Count = 0; return TasbihResult.AllDone; }
        if (Phase < 2) { Phase++; Count = 0; return TasbihResult.PhaseDone; }
        Finished = true;
        return TasbihResult.AllDone;
    }

    public void Reset() { Phase = 0; Count = 0; Rounds = 0; Finished = false; }

    void ClearProgress() { Reset(); DayTotal = 0; }

    public string Serialize()
    {
        return "Mode=" + Mode + "\nPhase=" + Phase + "\nCount=" + Count + "\nRounds=" + Rounds + "\nFreeTarget=" + FreeTarget
             + "\nFreePhrase=" + FreePhrase + "\nDayTotal=" + DayTotal + "\nFinished=" + (Finished ? 1 : 0)
             + "\nDay=" + Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static TasbihModel Parse(string text, DateTime today)
    {
        var d = new Dictionary<string, string>();
        foreach (string line in (text ?? "").Split('\n'))
        {
            int i = line.IndexOf('=');
            if (i > 0) d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
        }
        var t = new TasbihModel();
        t.Mode = Int(d, "Mode", 0, 0, 1);
        t.FreeTarget = Int(d, "FreeTarget", 33, 1, 100000);
        t.FreePhrase = Int(d, "FreePhrase", 0, 0, PhrasesAr.Length - 1);
        string day;
        DateTime parsed;
        if (d.TryGetValue("Day", out day) && DateTime.TryParseExact(day, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) && parsed == today.Date)
        {
            t.Day = parsed;
            t.Phase = Int(d, "Phase", 0, 0, 2);
            t.Count = Int(d, "Count", 0, 0, t.Target);
            t.Rounds = Int(d, "Rounds", 0, 0, int.MaxValue);
            t.DayTotal = Int(d, "DayTotal", 0, 0, int.MaxValue);
            t.Finished = Int(d, "Finished", 0, 0, 1) == 1;
        }
        return t;
    }

    static int Int(Dictionary<string, string> d, string key, int def, int min, int max)
    {
        string v; int r;
        if (!d.TryGetValue(key, out v) || !int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out r)) return def;
        return Math.Max(min, Math.Min(max, r));
    }
}

// How a target maps onto a ring of beads. Long counts go around a 33-bead ring again and again.
public static class TasbihRing
{
    const int LargestRing = 34, CycleBeads = 33;

    public static int Beads(int target) { return target <= LargestRing ? target : CycleBeads; }

    public static int Filled(int count, int target, bool finished)
    {
        int beads = Beads(target);
        if (finished) return beads;
        if (count <= 0) return 0;
        return (count - 1) % beads + 1;
    }
}