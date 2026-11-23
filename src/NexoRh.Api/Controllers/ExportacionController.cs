using System.Globalization;
using System.IO.Compression;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NexoRh.Api.Auth;
using NexoRh.Api.Catalogo;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;
using NexoRh.Api.Services;

namespace NexoRh.Api.Controllers;

/// <summary>
/// “Descargar todos mis datos” (sección 8): ZIP con un CSV por tabla (se abre en Excel) y los archivos guardados. Disponible
/// para los administradores en cualquier estado de la cuenta, también suspendida o cancelada. Sin contraseñas ni secretos.
/// </summary>
[ApiController]
[Route("api/exportacion")]
[Authorize(Policy = Politicas.Administrador)]
public class ExportacionController(NexoDbContext db, CatalogoDbContext catalogo, ContextoTenant tenant, Auditoria auditoria) : ControllerBase
{
    /// <summary>Columnas que nunca salen de la plataforma.</summary>
    private static readonly HashSet<string> Secretas = ["PasswordHash", "TotpSecreto", "Sello", "TenantId"];
    private static readonly HashSet<string> TablasExcluidas = [nameof(TokenUsuario)];

    [HttpGet]
    public async Task<IActionResult> Descargar(CancellationToken ct)
    {
        var ms = new MemoryStream();
        var resumen = new List<string>();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var e in db.Model.GetEntityTypes().Where(e => !e.IsOwned() && e.GetTableName() is not null && !TablasExcluidas.Contains(e.ClrType.Name)).OrderBy(e => e.GetTableName()))
            {
                var filas = await (Task<List<object>>)typeof(ExportacionController).GetMethod(nameof(Filas), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                    .MakeGenericMethod(e.ClrType).Invoke(null, [db, ct])!;
                if (filas.Count == 0) continue;
                var tabla = e.GetTableName()!;
                var props = e.GetProperties().Where(p => !Secretas.Contains(p.Name) && p.PropertyInfo is not null).ToList();
                var binarias = props.Where(p => p.ClrType == typeof(byte[])).ToList();
                var columnas = props.Except(binarias).ToList();
                var csv = new StringBuilder("﻿");
                csv.AppendLine(string.Join(";", columnas.Select(p => Celda(p.Name)).Concat(binarias.Select(b => Celda(b.Name + " (archivo)")))));
                var clave = e.FindPrimaryKey()!.Properties.Where(p => p.Name != nameof(IEntidadTenant.TenantId)).ToList();
                foreach (var f in filas)
                {
                    var valores = columnas.Select(p => Celda(Texto(p.PropertyInfo!.GetValue(f)))).ToList();
                    foreach (var b in binarias)
                    {
                        if (b.PropertyInfo!.GetValue(f) is not byte[] { Length: > 0 } datos) { valores.Add(""); continue; }
                        var nombre = $"archivos/{tabla}/{string.Join("-", clave.Select(k => k.PropertyInfo!.GetValue(f)))}{Extension(e, f)}";
                        await using (var s = zip.CreateEntry(nombre, CompressionLevel.Fastest).Open()) await s.WriteAsync(datos, ct);
                        valores.Add(Celda(nombre));
                    }
                    csv.AppendLine(string.Join(";", valores));
                }
                var entrada = zip.CreateEntry($"{tabla}.csv", CompressionLevel.Optimal);
                await using (var s = new StreamWriter(entrada.Open(), new UTF8Encoding(false))) await s.WriteAsync(csv.ToString());
                resumen.Add($"{tabla}: {filas.Count}");
            }
            var leeme = zip.CreateEntry("LEEME.txt");
            await using (var s = new StreamWriter(leeme.Open(), new UTF8Encoding(true)))
                await s.WriteAsync($"Exportación de datos de {tenant.Nombre} ({tenant.Slug}) · Nexo RH\r\nGenerada: {Fechas.TextoHora(DateTime.UtcNow)} (hora de Lima) por {User.Firma()}\r\n\r\n" +
                    "Cada tabla está en un archivo CSV separado por punto y coma (UTF-8): ábrelo con Excel.\r\nLos archivos (evidencias, imágenes) están en la carpeta “archivos”.\r\n" +
                    "No se incluyen contraseñas, claves de doble factor ni tokens.\r\n\r\nFilas por tabla:\r\n" + string.Join("\r\n", resumen));
        }
        auditoria.RegistrarTexto("Exportación de datos", "Crear", null, null, "exportación completa", null, $"{resumen.Count} tablas");
        await db.SaveChangesAsync(ct);
        catalogo.AuditoriaPlataforma.Add(new AuditoriaPlataforma { Actor = User.Firma(), Accion = "tenant.exported", TenantId = tenant.Id, Detalle = $"{resumen.Count} tablas",
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString() });
        await catalogo.SaveChangesAsync(ct);
        ms.Position = 0;
        return File(ms, "application/zip", $"nexo-rh-{tenant.Slug}-{DateTime.UtcNow:yyyyMMdd}.zip");
    }

    private static async Task<List<object>> Filas<T>(NexoDbContext db, CancellationToken ct) where T : class =>
        (await db.Set<T>().AsNoTracking().ToListAsync(ct)).Cast<object>().ToList();

    private static string Texto(object? v) => v switch
    {
        null => "",
        DateTime d => d.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        System.Collections.IEnumerable l and not string => string.Join(", ", l.Cast<object>()),
        _ => v.ToString() ?? "",
    };

    /// <summary>Celda CSV: entre comillas si hace falta; las que empiezan con = + - @ se neutralizan (inyección de fórmulas).</summary>
    private static string Celda(string s)
    {
        if (s.Length > 0 && "=+-@".Contains(s[0]) && !decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out _)) s = "'" + s;
        return s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
    }

    /// <summary>Extensión tomada de una columna de nombre de archivo de la misma fila, si la hay.</summary>
    private static string Extension(IEntityType e, object fila)
    {
        foreach (var p in e.GetProperties().Where(p => p.ClrType == typeof(string) && p.Name.Contains("Nombre", StringComparison.OrdinalIgnoreCase)))
            if (p.PropertyInfo?.GetValue(fila) is string n && Path.GetExtension(n) is { Length: > 1 and < 8 } ext) return ext.ToLowerInvariant();
        return ".bin";
    }
}
