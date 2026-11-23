using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using NexoRh.Api.Domain;

namespace NexoRh.Api.Auth;

/// <summary>Política, hash y generación de contraseñas.</summary>
public static class Passwords
{
    public const int Minimo = 10;
    public const int Maximo = 128;
    public static readonly PasswordHasher<Usuario> Hasher = new();

    // Sin caracteres que se confunden al leerlos (0/O, 1/l/I).
    private const string Letras = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Digitos = "23456789";

    /// <summary>Devuelve el motivo por el que la contraseña no es válida, o null si cumple la política.</summary>
    public static string? Validar(string? password, string correo, int minimo = Minimo)
    {
        minimo = Math.Clamp(minimo, 8, 64);
        if (string.IsNullOrEmpty(password) || password.Length < minimo) return $"La contraseña debe tener al menos {minimo} caracteres.";
        if (password.Length > Maximo) return $"La contraseña no puede superar {Maximo} caracteres.";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "La contraseña debe combinar letras y números.";
        var usuario = correo.Split('@')[0];
        if (usuario.Length >= 3 && password.Contains(usuario, StringComparison.OrdinalIgnoreCase)) return "La contraseña no puede contener tu usuario de correo.";
        return null;
    }

    public static bool Verificar(Usuario u, string password) =>
        !string.IsNullOrEmpty(password) && Hasher.VerifyHashedPassword(u, u.PasswordHash, password) != PasswordVerificationResult.Failed;

    public static void Asignar(Usuario u, string password, bool temporal)
    {
        u.PasswordHash = Hasher.HashPassword(u, password);
        u.DebeCambiarPassword = temporal;
        u.IntentosFallidos = 0;
        u.BloqueadoHasta = null;
        RenovarSello(u);
    }

    /// <summary>Invalida los tokens ya emitidos para el usuario.</summary>
    public static void RenovarSello(Usuario u) => u.Sello = Guid.NewGuid().ToString("N");

    /// <summary>Contraseña temporal aleatoria de 14 caracteres con letras y números.</summary>
    public static string Temporal()
    {
        const string todos = Letras + Digitos;
        var c = new char[14];
        for (var i = 0; i < c.Length; i++) c[i] = todos[RandomNumberGenerator.GetInt32(todos.Length)];
        c[RandomNumberGenerator.GetInt32(7)] = Digitos[RandomNumberGenerator.GetInt32(Digitos.Length)];
        c[7 + RandomNumberGenerator.GetInt32(7)] = Letras[RandomNumberGenerator.GetInt32(Letras.Length)];
        return new string(c);
    }

    public static string PerfilDe(Rol r) => r == Rol.Colaborador ? "Normal / Colaborador" : r.ToString();
}
