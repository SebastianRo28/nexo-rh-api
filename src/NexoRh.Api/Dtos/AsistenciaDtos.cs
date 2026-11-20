namespace NexoRh.Api.Dtos;

// Mismas formas que las interfaces del frontend (sample/asistencia.ts y sample/control-asistencia.ts): fechas dd/MM/yyyy,
// horas HH:mm. "hasta" vacío = vigente.

public record FeriadoDto(string Fecha, string Nombre);
public record PerfilAsistenciaDto(int ColaboradorId, int? RecintoId, bool UsaApp, decimal? Jornal, decimal SaldoInicial, string Contrato);
public record RecintoDto(int Id, string Nombre, string Empresa, string Ruc, string CodigoInterno, string Pais, string Departamento, string Provincia,
    string Distrito, string Direccion, string Clave, string Info, int? JefaturaId, int? AdministrativoId, double Lat, double Lng, int Radio,
    List<string> Emails, bool Activo, List<int> Usuarios);
public record CicloDto(int Trabajo, int Descanso);
public record TurnoDto(int Id, string Nombre, string Sigla, string Tipo, string Jornada, string Entrada, string Salida, int Colacion, List<int> Dias,
    int Tolerancia, string Color, CicloDto? Ciclo, bool Activo);
public record AsignacionTurnoDto(int Id, int ColaboradorId, int TurnoId, string Desde, string Hasta, string? CicloInicio, string CreadoPor);
/// <summary>Valor: id de turno (número) o código DES / LIB / COMP.</summary>
public record CambioTurnoDto(int ColaboradorId, string Fecha, object Valor, string CreadoPor);
public record MarcaDto(int Id, int ColaboradorId, int RecintoId, string Fecha, string Hora, string Tipo, string Metodo, double Lat, double Lng,
    bool DentroRadio, bool Foto, string Observacion, string? CreadoPor, string? Motivo);
public record MarcaFallidaDto(int Id, int ColaboradorId, int RecintoId, string Fecha, string Hora, string Tipo, string Metodo, string Motivo, int? Distancia);
public record NotaDto(int Id, int ColaboradorId, string Fecha, string Texto, string Autor);
public record VacacionDto(int Id, int ColaboradorId, string Desde, string Hasta, int Dias, string Origen, string Observacion);
public record AusenciaDto(int Id, int ColaboradorId, string Tipo, string Desde, string Hasta, int Dias, string Observacion);
public record LicenciaDto(int Id, string Folio, int ColaboradorId, string Emisor, string Diagnostico, string Desde, string Hasta, int Dias,
    string Estado, string Recibida, int? AusenciaId);
public record SolicitudAsistenciaDto(int Id, int ColaboradorId, string Tipo, string Desde, string Hasta, int Dias, string Motivo, string Creada,
    string Estado, string Aprobador, string Comentario);
public record RecargoDto(int Id, int ColaboradorId, string Fecha, decimal Horas, bool DescansoSustitutorio, string Observacion);
public record LineaHheeDto(int Minutos, int Tipo);
public record AprobacionHheeDto(int ColaboradorId, string Fecha, string Estado, List<LineaHheeDto> Lineas, string Observacion, string AprobadoPor, bool Enviado);

/// <summary>Todo lo que el usuario puede ver de asistencia, en una sola respuesta.</summary>
public record AsistenciaDatos(
    string Corte, string HoraCorte, string InicioMarcas, bool PuedeGestionar,
    List<FeriadoDto> Feriados, List<RecintoDto> Recintos, List<TurnoDto> Turnos, List<PerfilAsistenciaDto> Perfiles,
    List<AsignacionTurnoDto> Asignaciones, List<CambioTurnoDto> CambiosTurno, List<MarcaDto> Marcas, List<MarcaFallidaDto> Fallidas,
    List<NotaDto> Notas, List<VacacionDto> Vacaciones, List<AusenciaDto> Ausencias, List<LicenciaDto> Licencias,
    List<SolicitudAsistenciaDto> Solicitudes, List<RecargoDto> Recargos, List<AprobacionHheeDto> Aprobaciones);

// --- Entradas ---------------------------------------------------------------------------------------------------------
public record Marcar(string? Tipo, string? Metodo, int? RecintoId);
public record FallidaNueva(string? Tipo, string? Metodo, int? RecintoId, string? Hora, string? Motivo, int? Distancia);
public record MarcaManual(int ColaboradorId, string? Fecha, string? Hora, string? Tipo, string? Motivo);
public record NotaNueva(int ColaboradorId, string? Texto);
public record TurnoGuardar(string? Nombre, string? Sigla, string? Tipo, string? Jornada, string? Entrada, string? Salida, int Colacion, List<int>? Dias,
    int Tolerancia, string? Color, CicloDto? Ciclo, bool Activo = true);
public record EstadoActivo(bool Activo);
/// <summary>Modo: dia (solo esa fecha), hasta (rango con días de la semana) o permanente (nuevo turno base).</summary>
public record AsignarTurno(List<int>? ColaboradorIds, string? Fecha, int? TurnoId, string? Codigo, string? Modo, string? Hasta, List<int>? Dias);
public record RecintoGuardar(string? Nombre, string? Empresa, string? Ruc, string? CodigoInterno, string? Pais, string? Departamento, string? Provincia,
    string? Distrito, string? Direccion, string? Clave, string? Info, int? JefaturaId, int? AdministrativoId, double Lat, double Lng, int Radio,
    List<string>? Emails, bool Activo, List<int>? Usuarios);
/// <summary>Accion: aprobar, rechazar o revertir. MinutosExceso: el exceso calculado del día (para "Aprobado" o "Aprobado parcial").</summary>
public record DecisionHhee(int ColaboradorId, string? Fecha, string? Accion, List<LineaHheeDto>? Lineas, string? Observacion, int MinutosExceso);
public record EnvioHhee(List<ClaveHhee>? Items);
public record ClaveHhee(int ColaboradorId, string? Fecha);
public record EnvioHheeResultado(int Enviados, List<string> Omitidos);
public record VacacionNueva(int ColaboradorId, string? Desde, string? Hasta, string? Origen, string? Observacion);
public record AusenciaNueva(int ColaboradorId, string? Tipo, string? Desde, string? Hasta, string? Observacion);
public record LicenciaNueva(int ColaboradorId, string? Folio, string? Emisor, string? Diagnostico, string? Desde, string? Hasta);
public record EstadoLicencia(string? Estado);
public record LicenciaResultado(LicenciaDto Licencia, AusenciaDto? Ausencia);
public record SolicitudNueva(int ColaboradorId, string? Tipo, string? Desde, string? Hasta, string? Motivo);
public record ResolverSolicitud(bool Aprobar, string? Comentario);
public record SolicitudResultado(SolicitudAsistenciaDto Solicitud, VacacionDto? Vacacion, AusenciaDto? Ausencia);
public record RecargoNuevo(int ColaboradorId, string? Fecha, decimal Horas, bool DescansoSustitutorio, string? Observacion);
public record JornalGuardar(decimal? Jornal);
