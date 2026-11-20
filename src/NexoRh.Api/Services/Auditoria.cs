using System.Security.Claims;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Services;

/// <summary>Agrega registros al historial de cambios; se guardan junto con el cambio (mismo SaveChanges).</summary>
public class Auditoria(NexoDbContext db, IHttpContextAccessor http)
{
    private ClaimsPrincipal? Usuario => http.HttpContext?.User;

    public void Registrar(string entidad, string accion, Colaborador? c = null, string propiedad = "—", string anterior = "—", string actual = "—", string origen = "Web")
    {
        db.Cambios.Add(new Cambio
        {
            Usuario = Usuario?.Identity?.IsAuthenticated == true ? Usuario.Firma() : "Sistema",
            Origen = origen,
            Colaborador = c is null ? "—" : $"{c.Apellidos}, {c.Nombres}",
            Documento = c?.Documento ?? "—",
            Entidad = Recortar(entidad, 120),
            Accion = accion,
            Propiedad = Recortar(propiedad, 200),
            Anterior = Recortar(Vacio(anterior), 500),
            Actual = Recortar(Vacio(actual), 500),
        });
    }

    /// <summary>Variante con el colaborador como texto (cambios informados por el frontend).</summary>
    public void RegistrarTexto(string entidad, string accion, string? colaborador, string? documento, string? propiedad, string? anterior, string? actual)
    {
        Registrar(entidad, accion, null, Vacio(propiedad), Vacio(anterior), Vacio(actual));
        var c = db.ChangeTracker.Entries<Cambio>().Last().Entity;
        c.Colaborador = Recortar(Vacio(colaborador?.Trim()), 170);
        c.Documento = Recortar(Vacio(documento?.Trim()), 20);
    }

    private static string Vacio(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s;
    private static string Recortar(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";
}
