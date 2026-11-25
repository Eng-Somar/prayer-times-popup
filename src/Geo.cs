using System;

public static class Geo
{
    const double KaabaLat = 21.4225, KaabaLon = 39.8262;

    static double Rad(double d) { return d * Math.PI / 180.0; }

    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        double dLat = Rad(lat2 - lat1), dLon = Rad(lon2 - lon1);
        double a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 6371.0 * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // Degrees clockwise from true north.
    public static double QiblaBearing(double lat, double lon)
    {
        double dLon = Rad(KaabaLon - lon);
        double y = Math.Sin(dLon);
        double x = Math.Cos(Rad(lat)) * Math.Tan(Rad(KaabaLat)) - Math.Sin(Rad(lat)) * Math.Cos(dLon);
        double deg = Math.Atan2(y, x) * 180.0 / Math.PI;
        return deg < 0 ? deg + 360 : deg;
    }
}

public enum LocationDecision { Apply, Offer, Ignore }

public static class LocationPolicy
{
    const double FarKm = 100;

    // A far jump usually means a VPN rather than travel, so it never changes prayer times silently.
    public static LocationDecision Decide(bool hasSaved, bool userAsked, double lat, double lon, double newLat, double newLon,
                                          bool hasOffer, double offerLat, double offerLon)
    {
        if (!hasSaved || userAsked) return LocationDecision.Apply;
        if (Geo.DistanceKm(lat, lon, newLat, newLon) < FarKm) return LocationDecision.Apply;
        if (hasOffer && Geo.DistanceKm(offerLat, offerLon, newLat, newLon) < FarKm) return LocationDecision.Ignore;
        return LocationDecision.Offer;
    }

    // The saved country only describes the place it was detected at. Auto method falls back to MWL without it.
    public static string CountryFor(string country, double baseLat, double baseLon, double newLat, double newLon)
    {
        return Geo.DistanceKm(baseLat, baseLon, newLat, newLon) < FarKm ? country : "";
    }
}
