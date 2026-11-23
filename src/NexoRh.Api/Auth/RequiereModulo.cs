using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NexoRh.Api.Catalogo;

namespace NexoRh.Api.Auth;

/// <summary>
/// [RequiereModulo("PAYROLL")]: el endpoint solo responde si el tenant tiene el módulo contratado (o incluido en su plan).
/// Si no, 403 con codigo "module_not_enabled": el frontend muestra el modal comercial, no un error.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class RequiereModuloAttribute(string modulo) : Attribute, IAsyncAuthorizationFilter
{
    public string Modulo { get; } = modulo;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx)
    {
        var sp = ctx.HttpContext.RequestServices;
        if (sp.GetRequiredService<ContextoTenant>().Id is not { } tenant) return;
        var s = await sp.GetRequiredService<Suscripciones>().DeAsync(tenant, ctx.HttpContext.RequestAborted);
        if (s.Tiene(Modulo)) return;
        var nombre = Modulos.Catalogo.FirstOrDefault(m => m.Codigo == Modulo)?.Nombre ?? Modulo;
        ctx.Result = new ObjectResult(new { title = $"El módulo {nombre} no está incluido en tu plan.", status = 403, codigo = "module_not_enabled", modulo = Modulo })
            { StatusCode = StatusCodes.Status403Forbidden };
    }
}
