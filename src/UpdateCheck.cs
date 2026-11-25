using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

public static class UpdateCheck
{
    public const string Version = "1.5.0";
    public const string ReleasesUrl = "https://github.com/Eng-Somar/prayer-times-popup/releases/latest";
    const string ApiUrl = "https://api.github.com/repos/Eng-Somar/prayer-times-popup/releases/latest";

    public static bool IsNewer(string current, string latest)
    {
        Version c, l;
        return System.Version.TryParse(Clean(current), out c) && System.Version.TryParse(Clean(latest), out l) && l > c;
    }

    static string Clean(string v) { return (v ?? "").Trim().TrimStart('v', 'V'); }

    // Only runs when the user asks. Returns the latest tag, or null when it can't be reached.
    public static string LatestTag()
    {
        try
        {
            var req = (HttpWebRequest)WebRequest.Create(ApiUrl);
            req.Timeout = 8000;
            req.ReadWriteTimeout = 8000;
            req.UserAgent = "PrayerTimesPopup";
            using (WebResponse resp = req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                Match m = Regex.Match(sr.ReadToEnd(), "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                return m.Success ? m.Groups[1].Value : null;
            }
        }
        catch (WebException) { return null; }
        catch (IOException) { return null; }
    }
}
