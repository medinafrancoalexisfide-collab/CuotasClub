using System.Globalization;
using CuotasClub.Data;
using CuotasClub.Repositories;
using Microsoft.Data.Sqlite;

namespace CuotasClub.Services;

/// <summary>
/// Copias de seguridad de cuotasclub.db en una carpeta (por defecto Documentos\CuotasClub\Copias).
/// Si la carpeta es una de Google Drive para escritorio, cada copia se sube sola a la nube.
/// Las copias se hacen con VACUUM INTO, que da un archivo consistente aunque la base esté en uso.
/// </summary>
public class CopiaSeguridadService(Database db)
{
    /// <summary>Cuántas copias se conservan; las más viejas se borran.</summary>
    public const int CopiasAConservar = 30;

    private const string Prefijo = "cuotasclub-";
    private const string FormatoFecha = "yyyy-MM-dd_HH-mm-ss";

    private readonly ConfiguracionRepository _repo = new(db);

    public static string CarpetaPorDefecto =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CuotasClub", "Copias");

    /// <summary>Carpeta elegida en Configuración, o la de por defecto.</summary>
    public string Carpeta
    {
        get => _repo.Obtener(ClavesConfiguracion.CarpetaCopias) is { Length: > 0 } c ? c : CarpetaPorDefecto;
        set => _repo.Guardar(ClavesConfiguracion.CarpetaCopias, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    /// <summary>Fecha y hora local de la última copia que salió bien.</summary>
    public DateTime? UltimaCopia
    {
        get => DateTime.TryParse(_repo.Obtener(ClavesConfiguracion.UltimaCopia), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        private set => _repo.Guardar(ClavesConfiguracion.UltimaCopia, value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
    }

    /// <summary>Error de la última copia automática (por ejemplo, Google Drive cerrado). Vacío si salió bien.</summary>
    public string? ErrorUltimaCopia
    {
        get => _repo.Obtener(ClavesConfiguracion.ErrorUltimaCopia);
        private set => _repo.Guardar(ClavesConfiguracion.ErrorUltimaCopia, value);
    }

    /// <summary>
    /// Guarda una copia fechada en la carpeta y borra las que pasen de <see cref="CopiasAConservar"/>.
    /// Devuelve la ruta de la copia. Si falla, lo anota en <see cref="ErrorUltimaCopia"/> y lanza la excepción.
    /// </summary>
    public string HacerCopia(DateTime? ahora = null, string? sufijo = null)
    {
        try
        {
            var ruta = CopiarA(Carpeta, ahora ?? DateTime.Now, sufijo);
            LimpiarViejas(Carpeta);
            UltimaCopia = ahora ?? DateTime.Now;
            ErrorUltimaCopia = null;
            return ruta;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SqliteException or ArgumentException or NotSupportedException)
        {
            try { ErrorUltimaCopia = ex.Message; } catch (SqliteException) { }
            throw new ReglaNegocioException($"No se pudo guardar la copia en {Carpeta}.{Environment.NewLine}{ex.Message}");
        }
    }

    /// <summary>Copia hecha al cerrar el programa: nunca muestra error, lo deja anotado para Configuración.</summary>
    public bool HacerCopiaSilenciosa()
    {
        try { HacerCopia(); return true; }
        catch (ReglaNegocioException) { return false; }
    }

    /// <summary>Copias de la carpeta, de la más nueva a la más vieja.</summary>
    public IReadOnlyList<string> ListarCopias() => ListarCopias(Carpeta);

    private static List<string> ListarCopias(string carpeta) =>
        !Directory.Exists(carpeta)
            ? []
            : Directory.GetFiles(carpeta, Prefijo + "*.db")
                .OrderByDescending(Path.GetFileName, StringComparer.Ordinal)
                .ToList();

    private string CopiarA(string carpeta, DateTime ahora, string? sufijo)
    {
        Directory.CreateDirectory(carpeta);
        var nombre = Prefijo + ahora.ToString(FormatoFecha, CultureInfo.InvariantCulture) + (sufijo is null ? "" : "-" + sufijo);
        var ruta = Path.Combine(carpeta, nombre + ".db");
        for (int i = 2; File.Exists(ruta); i++) ruta = Path.Combine(carpeta, $"{nombre}-{i}.db");

        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "VACUUM INTO $ruta";
        cmd.Parameters.AddWithValue("$ruta", ruta);
        cmd.ExecuteNonQuery();
        return ruta;
    }

    private static void LimpiarViejas(string carpeta)
    {
        foreach (var vieja in ListarCopias(carpeta).Skip(CopiasAConservar))
        {
            try { File.Delete(vieja); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }

    /// <summary>Revisa que el archivo sea una base de CuotasClub sana. Devuelve null si está bien, o el motivo.</summary>
    public static string? ValidarCopia(string ruta)
    {
        if (!File.Exists(ruta)) return "No se encuentra el archivo.";
        try
        {
            var cs = new SqliteConnectionStringBuilder { DataSource = ruta, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
            using var cn = new SqliteConnection(cs);
            cn.Open();
            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('Alumnos', 'Pagos', 'Configuracion')";
            if (Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture) != 3)
                return "El archivo no es una base de CuotasClub.";
            cmd.CommandText = "PRAGMA quick_check";
            if (cmd.ExecuteScalar() as string != "ok") return "El archivo está dañado.";
            return null;
        }
        catch (SqliteException)
        {
            return "El archivo no es una base de CuotasClub.";
        }
    }

    /// <summary>
    /// Reemplaza la base actual por la copia elegida. Antes guarda una copia de la base actual
    /// (termina en "-antes-de-restaurar") y la devuelve. La carpeta de copias elegida se mantiene.
    /// Después de esto hay que reiniciar el programa.
    /// </summary>
    public string Restaurar(string rutaCopia)
    {
        if (ValidarCopia(rutaCopia) is { } motivo) throw new ReglaNegocioException(motivo);

        var carpeta = _repo.Obtener(ClavesConfiguracion.CarpetaCopias);
        var respaldo = HacerCopia(sufijo: "antes-de-restaurar");

        // Se copia primero a un temporal junto a la base: si algo falla, la base actual queda intacta.
        var destino = Path.GetFullPath(db.RutaArchivo);
        var temporal = destino + ".restaurando";
        File.Copy(rutaCopia, temporal, overwrite: true);

        SqliteConnection.ClearAllPools(); // suelta el archivo para poder reemplazarlo
        File.Move(temporal, destino, overwrite: true);
        foreach (var extra in new[] { destino + "-wal", destino + "-shm", destino + "-journal" })
            if (File.Exists(extra)) File.Delete(extra);

        db.Inicializar(); // si la copia es de una versión anterior, suma lo que falte
        _repo.Guardar(ClavesConfiguracion.CarpetaCopias, carpeta);
        UltimaCopia = DateTime.Now;
        ErrorUltimaCopia = null;
        return respaldo;
    }
}
