using System;

// A second, independent solar calculation to check PrayCalc against. It shares no code with src\PrayCalc.cs:
// the position comes from Meeus, "Astronomical Algorithms" (apparent position, hour angle from sidereal time),
// and times are found by bisecting the altitude instead of using the closed-form hour angle.
public static class SolarReference
{
    static double Rad(double d) { return d * Math.PI / 180; }
    static double Deg(double r) { return r * 180 / Math.PI; }

    static void Sun(double jd, out double ra, out double decl, out double sidereal)
    {
        double t = (jd - 2451545) / 36525;
        double l = 280.46646 + t * (36000.76983 + 0.0003032 * t);
        double m = Rad(357.52911 + t * (35999.05029 - 0.0001537 * t));
        double c = Math.Sin(m) * (1.914602 - t * (0.004817 + 0.000014 * t))
                 + Math.Sin(2 * m) * (0.019993 - 0.000101 * t) + Math.Sin(3 * m) * 0.000289;
        double omega = Rad(125.04 - 1934.136 * t);
        double lambda = Rad(l + c - 0.00569 - 0.00478 * Math.Sin(omega));
        double e = Rad(23.439291 - 0.0130042 * t + 0.00256 * Math.Cos(omega));
        ra = Deg(Math.Atan2(Math.Cos(e) * Math.Sin(lambda), Math.Cos(lambda)));
        decl = Deg(Math.Asin(Math.Sin(e) * Math.Sin(lambda)));
        sidereal = 280.46061837 + 360.98564736629 * (jd - 2451545);
    }

    static double Altitude(double jd, double lat, double lng, out double decl)
    {
        double ra, sidereal;
        Sun(jd, out ra, out decl, out sidereal);
        return Deg(Math.Asin(Math.Sin(Rad(lat)) * Math.Sin(Rad(decl))
            + Math.Cos(Rad(lat)) * Math.Cos(Rad(decl)) * Math.Cos(Rad(sidereal + lng - ra))));
    }

    static double Midnight(DateTime utc) { return utc.Date.ToOADate() + 2415018.5; }

    static double NoonJd(DateTime date, double lng)
    {
        double jd = Midnight(date) + 0.5 - lng / 360;
        for (int i = 0; i < 5; i++)
        {
            double ra, decl, sidereal;
            Sun(jd, out ra, out decl, out sidereal);
            double h = (sidereal + lng - ra) % 360;
            if (h > 180) h -= 360;
            if (h < -180) h += 360;
            jd -= h / 361;
        }
        return jd;
    }

    // All results are minutes after 00:00 UTC on the given date.
    public static double Noon(DateTime date, double lng) { return (NoonJd(date, lng) - Midnight(date)) * 1440; }

    // When the sun crosses a fixed altitude, before (-1) or after (+1) noon.
    public static double Crossing(DateTime date, double lat, double lng, double altitude, int side)
    {
        double high = NoonJd(date, lng), low = high + side * 0.5;
        for (int i = 0; i < 50; i++)
        {
            double mid = (high + low) / 2, decl;
            if (Altitude(mid, lat, lng, out decl) > altitude) high = mid; else low = mid;
        }
        return (high - Midnight(date)) * 1440;
    }

    // When an object's shadow is factor times its height longer than its shadow at noon.
    public static double Asr(DateTime date, double lat, double lng, int factor)
    {
        double high = NoonJd(date, lng), low = high + 0.5;
        for (int i = 0; i < 50; i++)
        {
            double mid = (high + low) / 2, decl;
            double altitude = Altitude(mid, lat, lng, out decl);
            double target = Deg(Math.Atan(1 / (factor + Math.Tan(Rad(Math.Abs(lat - decl))))));
            if (altitude > target) high = mid; else low = mid;
        }
        return (high - Midnight(date)) * 1440;
    }
}
