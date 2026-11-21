using System.Text;

namespace NexoRh.Api.Services;

/// <summary>Lectura de CSV como los guarda Excel: separador “;” o “,” (según la cabecera), comillas y UTF-8 o ANSI.</summary>
public static class Csv
{
    /// <summary>UTF-8 (con o sin BOM); si el archivo no es UTF-8 válido se lee como Windows-1252/Latin-1 (CSV de Excel “normal”).</summary>
    public static string Texto(byte[] bytes)
    {
        try { return new UTF8Encoding(false, true).GetString(bytes).TrimStart('﻿'); }
        catch (DecoderFallbackException) { return Encoding.Latin1.GetString(bytes); }
    }

    public static List<string[]> Leer(string texto)
    {
        var primera = texto.Split('\n', 2)[0];
        var sep = primera.Count(c => c == ';') >= primera.Count(c => c == ',') ? ';' : ',';
        var filas = new List<string[]>();
        var fila = new List<string>();
        var celda = new StringBuilder();
        var comillas = false;
        for (var i = 0; i < texto.Length; i++)
        {
            var ch = texto[i];
            if (comillas)
            {
                if (ch == '"' && i + 1 < texto.Length && texto[i + 1] == '"') { celda.Append('"'); i++; }
                else if (ch == '"') comillas = false;
                else celda.Append(ch);
            }
            else if (ch == '"' && celda.Length == 0) comillas = true;
            else if (ch == sep) { fila.Add(celda.ToString()); celda.Clear(); }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n') i++;
                fila.Add(celda.ToString()); celda.Clear();
                filas.Add([.. fila]); fila.Clear();
            }
            else celda.Append(ch);
        }
        if (celda.Length > 0 || fila.Count > 0) { fila.Add(celda.ToString()); filas.Add([.. fila]); }
        return filas;
    }
}
