namespace NexoRh.Api.Services;

/// <summary>Distancias sobre la Tierra (marcación con ubicación real).</summary>
public static class Geo
{
    /// <summary>Metros entre dos coordenadas (fórmula del haversine).</summary>
    public static double Metros(double lat1, double lng1, double lat2, double lng2)
    {
        static double Rad(double g) => g * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(dLng / 2), 2);
        return 6_371_000 * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }
}
