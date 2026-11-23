using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Services;

/// <summary>Uso frente a un límite del plan. estado: ok, aviso (desde el 80 %) o bloqueado (100 %).</summary>
public record UsoLimite(string Clave, string Nombre, double Usado, double Maximo, string Unidad, int Pct, string Estado);
public record PasoImplementacion(string Clave, string Titulo, int Avance);

/// <summary>Límites del plan (sección 6.2): empresas, almacenamiento, administradores y API.</summary>
public class LimitesPlan(NexoDbContext db, CatalogoDbContext catalogo, ContextoTenant tenant, Suscripciones suscripciones)
{
    public const string Empresas = "empresas", Admins = "admins", Almacenamiento = "almacenamiento", Api = "api";

    public async Task<List<UsoLimite>> UsoAsync()
    {
        var plan = (await suscripciones.DeAsync(tenant.Id!.Value)).Plan;
        var empresas = await db.FilasCatalogo.CountAsync(f => f.Clave == "empresas");
        var admins = await db.Usuarios.CountAsync(u => u.Activo && u.Rol == Rol.Administrador);
        // Archivos guardados en la BD: evidencias de SST e imágenes de la marca.
        var bytes = await db.ArchivosEvidenciaSst.SumAsync(a => (long?)a.Contenido.Length) ?? 0;
        bytes += await catalogo.ArchivosMarca.Where(a => a.TenantId == tenant.Id).SumAsync(a => (long?)a.Datos.Length) ?? 0;
        return
        [
            Uso(Empresas, "Empresas (razones sociales)", empresas, plan.LimiteEmpresas, ""),
            Uso(Admins, "Usuarios administradores", admins, plan.LimiteAdmins, ""),
            Uso(Almacenamiento, "Almacenamiento", Math.Round(bytes / 1024d / 1024 / 1024, 3), plan.LimiteAlmacenamientoGb, "GB"),
            Uso(Api, "Llamadas a la API por minuto", 0, plan.LimiteApiPorMinuto, "por minuto"),
        ];
    }

    /// <summary>Error 403 "limit_reached" si agregar uno más supera el límite; null si se puede.</summary>
    public async Task<ObjectResult?> VerificarAsync(string clave)
    {
        var u = (await UsoAsync()).First(x => x.Clave == clave);
        if (u.Usado + 1 <= u.Maximo) return null;
        return new ObjectResult(new
        {
            title = $"Llegaste al límite de tu plan: {u.Maximo:0} {u.Nombre.ToLowerInvariant()}. Para agregar más, amplía tu plan desde Estado de cuenta.",
            status = 403, codigo = "limit_reached", limite = clave,
        }) { StatusCode = StatusCodes.Status403Forbidden };
    }

    private static UsoLimite Uso(string clave, string nombre, double usado, double maximo, string unidad)
    {
        var pct = maximo <= 0 ? (usado > 0 ? 100 : 0) : (int)Math.Min(100, Math.Round(usado / maximo * 100));
        return new UsoLimite(clave, nombre, usado, maximo, unidad, pct, maximo > 0 && pct >= 100 ? "bloqueado" : pct >= 80 && maximo > 0 ? "aviso" : "ok");
    }
}

/// <summary>Checklist de implementación (sección 10), con el avance de cada paso calculado a partir de los datos.</summary>
public class Implementacion(NexoDbContext db, ContextoTenant tenant, Marcas marcas)
{
    public async Task<List<PasoImplementacion>> PasosAsync()
    {
        static int Pct(int parte, int total) => total == 0 ? 0 : (int)Math.Min(100, Math.Round(parte * 100.0 / total));
        var colaboradores = await db.Colaboradores.CountAsync();
        var usuariosColab = await db.Usuarios.CountAsync(u => u.ColaboradorId != null);
        var accedieron = await db.Usuarios.CountAsync(u => u.ColaboradorId != null && u.UltimoAcceso != null);
        var feriados = await db.FilasCatalogo.AnyAsync(f => f.Clave == "feriados");
        var ausencias = await db.Vacaciones.AnyAsync() || await db.Ausencias.AnyAsync();
        return
        [
            new("marca", "Personaliza tu marca", (await marcas.DeAsync(tenant.Id!.Value)).Version > 0 ? 100 : 0),
            new("empresas", "Crea tus empresas", await db.FilasCatalogo.AnyAsync(f => f.Clave == "empresas") ? 100 : 0),
            new("estructura", "Carga la estructura (áreas)", await db.Colaboradores.AnyAsync(c => c.Division != "") ? 100 : 0),
            new("cargos", "Carga los cargos", await db.Cargos.AnyAsync() ? 100 : 0),
            new("colaboradores", "Importa los colaboradores", colaboradores > 0 ? 100 : 0),
            new("ausencias", "Configura vacaciones y ausencias", (feriados ? 50 : 0) + (ausencias ? 50 : 0)),
            new("usuarios", "Invita a los usuarios", Pct(usuariosColab, colaboradores)),
            new("portal", "Activa el portal", Pct(accedieron, usuariosColab)),
        ];
    }
}
