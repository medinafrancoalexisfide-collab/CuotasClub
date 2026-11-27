using System.Globalization;
using Microsoft.Data.Sqlite;

namespace CuotasClub.Data;

/// <summary>Conversiones entre tipos de .NET y columnas SQLite.</summary>
internal static class Conversiones
{
    private const string FormatoFecha = "yyyy-MM-dd";

    public static string FechaATexto(DateOnly f) => f.ToString(FormatoFecha, CultureInfo.InvariantCulture);
    public static DateOnly TextoAFecha(string s) => DateOnly.ParseExact(s, FormatoFecha, CultureInfo.InvariantCulture);

    /// <summary>Pesos (decimal) a centavos (long), redondeando a 2 decimales.</summary>
    public static long PesosACentavos(decimal pesos) => (long)Math.Round(pesos * 100m, 0, MidpointRounding.AwayFromZero);
    public static decimal CentavosAPesos(long centavos) => centavos / 100m;

    public static object ANull(string? s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s.Trim();
    public static object ANull(DateOnly? f) => f is null ? DBNull.Value : FechaATexto(f.Value);

    public static string? TextoONull(this SqliteDataReader r, string col)
    {
        var i = r.GetOrdinal(col);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    public static DateOnly Fecha(this SqliteDataReader r, string col) => TextoAFecha(r.GetString(r.GetOrdinal(col)));

    public static DateOnly? FechaONull(this SqliteDataReader r, string col)
    {
        var s = r.TextoONull(col);
        return s is null ? null : TextoAFecha(s);
    }

    public static decimal Pesos(this SqliteDataReader r, string col) => CentavosAPesos(r.GetInt64(r.GetOrdinal(col)));
}
