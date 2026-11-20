using Microsoft.Extensions.Options;

namespace NexoRh.Api.Services;

/// <summary>
/// "Hoy" para Asistencia. En producción es la fecha y hora de Lima. Para trabajar con los datos de ejemplo (marzo de
/// 2026) se puede fijar la fecha con Asistencia:FechaCorte (dd/MM/yyyy); las marcas se registran en esa fecha con la
/// hora real del servidor.
/// </summary>
public class RelojAsistencia(IOptions<RelojAsistencia.Opciones> opciones)
{
    public class Opciones
    {
        public string? FechaCorte { get; set; }
        /// <summary>Hora mostrada como "ahora" cuando la fecha está fijada (por defecto, la hora real).</summary>
        public string? HoraCorte { get; set; }
    }

    private DateTime AhoraLima => DateTime.UtcNow.Add(Fechas.Lima);

    public bool Fijo => Fechas.TryLeer(opciones.Value.FechaCorte, out _);
    public DateOnly Hoy => Fechas.TryLeer(opciones.Value.FechaCorte, out var f) ? f : DateOnly.FromDateTime(AhoraLima);
    /// <summary>Hora real de Lima, "HH:mm" (la que se guarda en las marcas).</summary>
    public string HoraReal => AhoraLima.ToString("HH:mm");
    /// <summary>Hora de referencia para los cálculos del día (atrasos, "por iniciar").</summary>
    public string HoraCorte => Fijo && !string.IsNullOrWhiteSpace(opciones.Value.HoraCorte) ? opciones.Value.HoraCorte! : HoraReal;
}
