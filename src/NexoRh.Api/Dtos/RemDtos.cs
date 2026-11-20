namespace NexoRh.Api.Dtos;

// Remuneraciones. Períodos como "MM/YYYY" (igual que el frontend); "hasta" vacío = sin fin.

public record TipoItemDto(int Id, string Codigo, string Nombre, string Clase, string Plame, bool Essalud, bool Pension, bool Quinta, bool Automatico, bool Activo,
    bool Calculado);
public record TipoItemGuardar(string? Codigo, string? Nombre, string? Clase, string? Plame, bool Essalud, bool Pension, bool Quinta, bool Activo = true);
public record AsignacionItemDto(int Id, int ColaboradorId, int TipoId, decimal Monto, string Desde, string Hasta, string Observacion);
public record AsignacionItemNueva(int ColaboradorId, int TipoId, decimal Monto, string? Desde, string? Hasta, string? Observacion);

public record ParametroRemDto(string Clave, string Nombre, decimal Valor, string Unidad, string VigenteDesde, string Fuente);
public record ParametroGuardar(decimal Valor, string? VigenteDesde);
public record ComisionAfpDto(string Afp, decimal ComisionFlujoPct, decimal ComisionSaldoPct);
public record TramoRentaDto(int Id, decimal Desde, decimal? Hasta, decimal TasaPct);
public record TramoGuardar(decimal TasaPct);
public record ParametrosRem(List<ParametroRemDto> Generales, List<ComisionAfpDto> Afp, List<TramoRentaDto> Tramos);

public record PeriodoDto(string Periodo, bool Cerrado, string? ProcesadoEn, string ProcesadoPor, string? CerradoEn, string CerradoPor,
    int Boletas, decimal Ingresos, decimal Descuentos, decimal Aportes, decimal Neto);

public record LineaBoletaDto(string Codigo, string Nombre, string Clase, string Detalle, decimal Monto);
public record BoletaResumenDto(int Id, string Periodo, int ColaboradorId, string Nombre, string Documento, string Cargo, string Regimen,
    int DiasLaborados, int DiasVacaciones, int DiasNoTrabajados, decimal Ingresos, decimal Descuentos, decimal Aportes, decimal Neto,
    decimal HorasExtras, decimal Recargos, bool Definitiva, List<string> Observaciones);
public record BoletaDto(BoletaResumenDto Resumen, List<LineaBoletaDto> Lineas);
