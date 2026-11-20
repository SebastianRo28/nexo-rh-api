using System.Globalization;

namespace NexoRh.Api.Services;

/// <summary>El frontend usa fechas dd/MM/yyyy y hora de Lima (UTC-5, sin horario de verano).</summary>
public static class Fechas
{
    public static readonly TimeSpan Lima = TimeSpan.FromHours(-5);
    public static string Texto(DateOnly d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public static string? Texto(DateOnly? d) => d is { } x ? Texto(x) : null;
    public static string TextoHora(DateTime utc) => utc.Add(Lima).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
    public static bool TryLeer(string? s, out DateOnly d) =>
        DateOnly.TryParseExact(s?.Trim(), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d);
    public static DateOnly HoyLima() => DateOnly.FromDateTime(DateTime.UtcNow.Add(Lima));
}
