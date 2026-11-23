using System.Security.Cryptography;

namespace NexoRh.Api.Auth;

/// <summary>Códigos de 6 dígitos de las apps de autenticación (TOTP, RFC 6238: SHA-1, pasos de 30 s).</summary>
public static class Totp
{
    private const string Base32 = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Clave nueva de 160 bits en Base32 (lo que se escribe en la app o va en el código QR).</summary>
    public static string NuevaClave() => ABase32(RandomNumberGenerator.GetBytes(20));

    /// <summary>Enlace otpauth:// que reconocen Google Authenticator, Microsoft Authenticator, 1Password, etc.</summary>
    public static string Uri(string emisor, string cuenta, string clave) =>
        $"otpauth://totp/{System.Uri.EscapeDataString(emisor)}:{System.Uri.EscapeDataString(cuenta)}?secret={clave}&issuer={System.Uri.EscapeDataString(emisor)}&digits=6&period=30";

    /// <summary>Acepta el código del paso actual y el de un paso antes o después (relojes desfasados).</summary>
    public static bool Verificar(string clave, string? codigo, DateTimeOffset? ahora = null)
    {
        codigo = new string((codigo ?? "").Where(char.IsDigit).ToArray());
        if (codigo.Length != 6) return false;
        var bytes = DeBase32(clave);
        var paso = (ahora ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30;
        for (var d = -1; d <= 1; d++)
            if (CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(Codigo(bytes, paso + d)), System.Text.Encoding.ASCII.GetBytes(codigo)))
                return true;
        return false;
    }

    public static string Codigo(byte[] clave, long paso)
    {
        var contador = BitConverter.GetBytes(paso);
        if (BitConverter.IsLittleEndian) Array.Reverse(contador);
        var h = HMACSHA1.HashData(clave, contador);
        var o = h[^1] & 0x0F;
        var n = ((h[o] & 0x7F) << 24) | (h[o + 1] << 16) | (h[o + 2] << 8) | h[o + 3];
        return (n % 1_000_000).ToString("D6");
    }

    public static string ABase32(byte[] datos)
    {
        var sb = new System.Text.StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in datos)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5) { sb.Append(Base32[(buffer >> (bits - 5)) & 31]); bits -= 5; }
        }
        if (bits > 0) sb.Append(Base32[(buffer << (5 - bits)) & 31]);
        return sb.ToString();
    }

    public static byte[] DeBase32(string texto)
    {
        var limpio = texto.Trim().TrimEnd('=').Replace(" ", "").ToUpperInvariant();
        var r = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in limpio)
        {
            var v = Base32.IndexOf(c);
            if (v < 0) throw new FormatException("Clave Base32 no válida.");
            buffer = (buffer << 5) | v;
            bits += 5;
            if (bits >= 8) { r.Add((byte)(buffer >> (bits - 8))); bits -= 8; }
        }
        return [.. r];
    }
}
