using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using NexoRh.Api.Auth;
using NexoRh.Api.Data;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Services;

/// <summary>
/// A quién puede ver y gestionar el usuario actual. Administradores: a todos. Supervisores: a sí mismos y a todo el
/// equipo que depende de ellos (directa o indirectamente). Colaboradores: solo a sí mismos.
/// Gestionar (aprobar, registrar marcas, asignar turnos) nunca incluye a uno mismo, salvo para administradores.
/// </summary>
public class Alcance(NexoDbContext db, IHttpContextAccessor http)
{
    private ClaimsPrincipal Usuario => http.HttpContext!.User;
    private HashSet<int>? visibles;

    public bool EsAdmin => Usuario.Rol() == Rol.Administrador;
    public int? Propio => Usuario.ColaboradorId();

    public static HashSet<int> Calcular(ClaimsPrincipal u, IEnumerable<(int Id, int? JefeId)> todos)
    {
        var lista = todos.ToList();
        if (u.Rol() == Rol.Administrador) return lista.Select(c => c.Id).ToHashSet();
        var propio = u.ColaboradorId();
        var set = new HashSet<int>();
        if (propio is null) return set;
        set.Add(propio.Value);
        if (u.Rol() != Rol.Supervisor) return set;
        var cola = new Queue<int>([propio.Value]);
        while (cola.Count > 0)
        {
            var jefe = cola.Dequeue();
            foreach (var h in lista.Where(x => x.JefeId == jefe && set.Add(x.Id))) cola.Enqueue(h.Id);
        }
        return set;
    }

    /// <summary>Colaboradores cuyos datos completos puede ver.</summary>
    public async Task<HashSet<int>> VisiblesAsync()
    {
        if (visibles is not null) return visibles;
        var todos = await db.Colaboradores.AsNoTracking().Select(c => new { c.Id, c.JefeId }).ToListAsync();
        return visibles = Calcular(Usuario, todos.Select(c => (c.Id, c.JefeId)));
    }

    /// <summary>Puede aprobar, registrar o corregir datos de asistencia de este colaborador.</summary>
    public async Task<bool> GestionaAsync(int colaboradorId)
    {
        if (EsAdmin) return true;
        if (Usuario.Rol() != Rol.Supervisor || colaboradorId == Propio) return false;
        return (await VisiblesAsync()).Contains(colaboradorId);
    }
}
