using System;
using System.Device.Location;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

public class GeoResult
{
    public double Lat, Lon;
    public string City = "", Country = "";
}

// Finds the user's location: city/country from an IP lookup, refined with Windows Location if it is enabled.
public static class Locator
{
    public static GeoResult Detect()
    {
        GeoResult ip = FromIp();
        GeoResult win = FromWindows();
        if (win == null) return ip;
        if (ip == null) return win;
        ip.Lat = win.Lat;
        ip.Lon = win.Lon;
        return ip;
    }

    static GeoResult FromIp()
    {
        string[][] apis =
        {
            new[] { "https://ipwho.is/", "latitude", "longitude", "city", "country_code" },
            new[] { "https://ipapi.co/json/", "latitude", "longitude", "city", "country_code" },
            new[] { "http://ip-api.com/json/", "lat", "lon", "city", "countryCode" },
        };
        foreach (string[] a in apis)
        {
            try
            {
                string j = Get(a[0]);
                double lat, lon;
                if (Num(j, a[1], out lat) && Num(j, a[2], out lon))
                    return new GeoResult { Lat = lat, Lon = lon, City = Str(j, a[3]), Country = Str(j, a[4]).ToUpperInvariant() };
            }
            catch { }
        }
        return null;
    }

    static GeoResult FromWindows()
    {
        try
        {
            using (var w = new GeoCoordinateWatcher(GeoPositionAccuracy.Default))
            {
                if (!w.TryStart(true, TimeSpan.FromSeconds(6))) return null;
                GeoCoordinate c = w.Position.Location;
                w.Stop();
                if (c == null || c.IsUnknown) return null;
                return new GeoResult { Lat = c.Latitude, Lon = c.Longitude };
            }
        }
        catch { return null; }
    }

    static string Get(string url)
    {
        var req = (HttpWebRequest)WebRequest.Create(url);
        req.Timeout = 7000;
        req.ReadWriteTimeout = 7000;
        req.UserAgent = "PrayerTimesPopup/1.0";
        using (WebResponse resp = req.GetResponse())
        using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            return sr.ReadToEnd();
    }

    static bool Num(string json, string key, out double v)
    {
        v = 0;
        Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"?(-?[0-9]+(?:\\.[0-9]+)?)");
        return m.Success && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }

    static string Str(string json, string key)
    {
        Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
        if (!m.Success) return "";
        try { return Regex.Unescape(m.Groups[1].Value); } catch { return m.Groups[1].Value; }
    }
}
