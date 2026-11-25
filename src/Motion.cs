using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

public static class Ease
{
    static double Clamp(double t) { return t < 0 ? 0 : t > 1 ? 1 : t; }

    public static double OutCubic(double t)
    {
        t = Clamp(t);
        return 1 - Math.Pow(1 - t, 3);
    }

    // A small overshoot for things that land, like a bead or a check mark.
    public static double OutBack(double t)
    {
        t = Clamp(t) - 1;
        const double s = 1.4;
        return t * t * ((s + 1) * t + s) + 1;
    }
}

// A value that moves toward its target at a steady rate. Curves are applied when painting, so it can be
// interrupted and reversed at any point without a jump.
public class Tween
{
    public double Value, Target;
    readonly double ms;
    readonly Action changed;

    public Tween(double ms, Action changed, double start = 0)
    {
        this.ms = ms;
        this.changed = changed;
        Value = Target = start;
    }

    // Returns true while there is still somewhere to go.
    public bool Step(double dtMs)
    {
        double move = dtMs / ms;
        if (Math.Abs(Target - Value) <= move) { Value = Target; return false; }
        Value += Target > Value ? move : -move;
        return true;
    }

    public void To(double target)
    {
        if (target == Target && target == Value) return;
        Target = target;
        if (Motion.Enabled) { Motion.Track(this); return; }
        Value = target;
        Notify();
    }

    // Runs once from the start. Without animation it simply shows the end state.
    public void Play()
    {
        Value = 0;
        Target = 0;
        To(1);
    }

    public void Snap(double value)
    {
        Motion.Forget(this);
        Value = Target = value;
    }

    public void Stop() { Motion.Forget(this); }

    internal void Notify() { if (changed != null) changed(); }
}

// One shared timer that only runs while something is moving, so an idle app uses no CPU for animation.
public static class Motion
{
    [DllImport("user32.dll")] static extern bool SystemParametersInfo(int action, int param, ref int value, int winIni);
    const int SpiGetClientAreaAnimation = 0x1042;

    static readonly List<Tween> active = new List<Tween>();
    static readonly Stopwatch clock = Stopwatch.StartNew();
    static Timer timer;
    static long last;

    // Follows "Show animations in Windows" in the Windows accessibility settings.
    public static bool Enabled
    {
        get
        {
            int on = 1;
            return !SystemParametersInfo(SpiGetClientAreaAnimation, 0, ref on, 0) || on != 0;
        }
    }

    public static void Track(Tween t)
    {
        if (!active.Contains(t)) active.Add(t);
        if (timer == null)
        {
            timer = new Timer { Interval = 15 };
            timer.Tick += OnTick;
        }
        if (!timer.Enabled)
        {
            last = clock.ElapsedMilliseconds;
            timer.Start();
        }
    }

    public static void Forget(Tween t)
    {
        active.Remove(t);
    }

    static void OnTick(object sender, EventArgs e)
    {
        long now = clock.ElapsedMilliseconds;
        double dt = now - last;
        last = now;
        for (int i = active.Count - 1; i >= 0; i--)
        {
            if (i >= active.Count) continue; // a change handler may have removed others
            Tween t = active[i];
            if (!t.Step(dt)) active.Remove(t);
            t.Notify();
        }
        if (active.Count == 0) timer.Stop();
    }
}
