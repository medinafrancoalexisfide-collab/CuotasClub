namespace CuotasClub.Services;

/// <summary>Comparación de carpetas tal como las ve Windows (sin distinguir mayúsculas ni la barra final).</summary>
public static class Rutas
{
    public static bool MismaCarpeta(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        return string.Equals(Normalizar(a), Normalizar(b), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalizar(string ruta)
    {
        var r = ruta.Trim().Replace('/', '\\');
        try { r = Path.GetFullPath(r); } catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { }
        r = r.Replace('/', '\\');
        // "C:\" se deja como está; "C:\Recibos\" queda "C:\Recibos".
        return r.Length > 3 ? r.TrimEnd('\\') : r;
    }
}
