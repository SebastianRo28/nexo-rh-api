using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Dtos;

/// <summary>Entidades de asistencia → DTOs con fechas dd/MM/yyyy.</summary>
public static class Mapeo
{
    private static string T(DateOnly d) => Fechas.Texto(d);

    public static RecintoDto Recinto(Recinto r) => new(r.Id, r.Nombre, r.Empresa, r.Ruc, r.CodigoInterno, r.Pais, r.Departamento, r.Provincia, r.Distrito,
        r.Direccion, r.Clave, r.Info, r.JefaturaId, r.AdministrativoId, r.Lat, r.Lng, r.Radio, [.. r.Emails], r.Activo, [.. r.Usuarios]);

    public static TurnoDto Turno(Turno t) => new(t.Id, t.Nombre, t.Sigla, t.Tipo, t.Jornada, t.Entrada, t.Salida, t.Colacion, [.. t.Dias], t.Tolerancia, t.Color,
        t.CicloTrabajo is { } a && t.CicloDescanso is { } b ? new CicloDto(a, b) : null, t.Activo);

    public static AsignacionTurnoDto Asignacion(AsignacionTurno a) =>
        new(a.Id, a.ColaboradorId, a.TurnoId, T(a.Desde), a.Hasta is { } h ? T(h) : "", a.CicloInicio is { } c ? T(c) : null, a.CreadoPor);

    public static CambioTurnoDto Cambio(CambioTurno c) => new(c.ColaboradorId, T(c.Fecha), c.TurnoId is { } t ? t : c.Codigo ?? "DES", c.CreadoPor);

    public static MarcaDto Marca(Marca m) => new(m.Id, m.ColaboradorId, m.RecintoId, T(m.Fecha), m.Hora, m.Tipo, m.Metodo, m.Lat, m.Lng, m.DentroRadio, m.Foto,
        m.Observacion, m.CreadoPor, m.Motivo);

    public static MarcaFallidaDto Fallida(MarcaFallida f) => new(f.Id, f.ColaboradorId, f.RecintoId, T(f.Fecha), f.Hora, f.Tipo, f.Metodo, f.Motivo, f.Distancia);
    public static NotaDto Nota(NotaSupervisor n) => new(n.Id, n.ColaboradorId, T(n.Fecha), n.Texto, n.Autor);
    public static VacacionDto Vacacion(Vacacion v) => new(v.Id, v.ColaboradorId, T(v.Desde), T(v.Hasta), v.Dias, v.Origen, v.Observacion);
    public static AusenciaDto Ausencia(Ausencia a) => new(a.Id, a.ColaboradorId, a.Tipo, T(a.Desde), T(a.Hasta), a.Dias, a.Observacion);

    public static LicenciaDto Licencia(LicenciaMedica l) =>
        new(l.Id, l.Folio, l.ColaboradorId, l.Emisor, l.Diagnostico, T(l.Desde), T(l.Hasta), l.Dias, l.Estado, T(l.Recibida), l.AusenciaId);

    public static SolicitudAsistenciaDto Solicitud(SolicitudAsistencia s) =>
        new(s.Id, s.ColaboradorId, s.Tipo, T(s.Desde), T(s.Hasta), s.Dias, s.Motivo, T(s.Creada), s.Estado, s.Aprobador, s.Comentario);

    public static RecargoDto Recargo(RecargoDomingo r) => new(r.Id, r.ColaboradorId, T(r.Fecha), r.Horas, r.DescansoSustitutorio, r.Observacion);

    public static AprobacionHheeDto Aprobacion(AprobacionHhee a) =>
        new(a.ColaboradorId, T(a.Fecha), a.Estado, a.Lineas.Select(l => new LineaHheeDto(l.Minutos, l.Tipo)).ToList(), a.Observacion, a.AprobadoPor, a.Enviado);
}
