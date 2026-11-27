using Microsoft.Data.Sqlite;

namespace CuotasClub.Data;

/// <summary>
/// Acceso al archivo SQLite. Cada operación abre su propia conexión (SQLite + pooling lo hace barato)
/// y la cierra al terminar, así no quedan conexiones colgadas entre formularios.
/// </summary>
public class Database
{
    public const string NombreArchivo = "cuotasclub.db";

    /// <summary>
    /// Carpeta de datos del programa: %LOCALAPPDATA%\CuotasClub. Se usa esta y no la del programa porque,
    /// instalado en "Archivos de programa", Windows no deja escribir junto al .exe.
    /// </summary>
    public static string CarpetaDatos =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CuotasClub");

    /// <summary>Base que usa el programa.</summary>
    public static string RutaPorDefecto => Path.Combine(CarpetaDatos, NombreArchivo);

    /// <summary>Donde estaba la base hasta la v0.13: junto al ejecutable.</summary>
    public static string RutaAntigua => Path.Combine(AppContext.BaseDirectory, NombreArchivo);

    /// <summary>
    /// Si todavía no hay base en la carpeta de datos pero hay una junto al programa (la de una versión anterior,
    /// o una que se pegó ahí), la copia a la carpeta de datos. La original no se toca.
    /// Devuelve true si copió.
    /// </summary>
    public static bool MigrarBaseAntigua(string origen, string destino)
    {
        if (File.Exists(destino) || !File.Exists(origen)) return false;
        if (string.Equals(Path.GetFullPath(origen), Path.GetFullPath(destino), StringComparison.OrdinalIgnoreCase)) return false;
        var dir = Path.GetDirectoryName(Path.GetFullPath(destino));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // VACUUM INTO arma una copia consistente aunque la base tenga cambios pendientes en el -wal.
        var cs = new SqliteConnectionStringBuilder { DataSource = origen, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString();
        using var cn = new SqliteConnection(cs);
        cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "VACUUM INTO $ruta";
        cmd.Parameters.AddWithValue("$ruta", destino);
        cmd.ExecuteNonQuery();
        return true;
    }

    public string RutaArchivo { get; }
    public string ConnectionString { get; }

    public Database(string rutaArchivo)
    {
        RutaArchivo = rutaArchivo;
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = rutaArchivo,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();
    }

    /// <summary>Devuelve una conexión ya abierta. Usar con <c>using</c>.</summary>
    public SqliteConnection AbrirConexion()
    {
        var cn = new SqliteConnection(ConnectionString);
        cn.Open();
        return cn;
    }

    /// <summary>Crea las tablas si no existen y carga los valores iniciales de configuración (una base nueva arranca sin precio ni %).</summary>
    public void Inicializar(DateOnly? hoy = null)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(RutaArchivo));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        using var cn = AbrirConexion();
        using var tx = cn.BeginTransaction();
        using (var cmd = cn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = Esquema.Sql;
            cmd.ExecuteNonQuery();
        }
        Esquema.CargarValoresIniciales(cn, tx, hoy ?? DateOnly.FromDateTime(DateTime.Today));
        tx.Commit();
    }
}
