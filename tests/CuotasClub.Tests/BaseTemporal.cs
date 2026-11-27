using CuotasClub.Data;
using CuotasClub.Models;
using Microsoft.Data.Sqlite;

namespace CuotasClub.Tests;

/// <summary>Base SQLite en un archivo temporal, nueva para cada prueba.</summary>
public abstract class BaseTemporal : IDisposable
{
    private readonly string _ruta = Path.Combine(Path.GetTempPath(), $"cuotasclub-test-{Guid.NewGuid():N}.db");
    protected Database Db { get; }

    protected BaseTemporal()
    {
        Db = new Database(_ruta);
        Db.Inicializar(new DateOnly(2027, 1, 1)); // las pruebas simulan una base creada en enero 2027
        // Lo que se carga en el asistente de primer uso: $20.000 y 30% desde enero 2027.
        new Services.ConfiguracionService(Db).GuardarConfiguracionInicial("Club de prueba", "Taller de prueba", "",
            20000m, 30m, new DateOnly(2027, 1, 1));
    }

    protected static Alumno NuevoAlumno(string apellido = "Pérez", string nombre = "Juan", string dni = "45123456",
        DateOnly? alta = null, DateOnly? baja = null, DateOnly? nacimiento = null) => new()
    {
        Nombre = nombre,
        Apellido = apellido,
        Dni = dni,
        FechaNacimiento = nacimiento ?? new DateOnly(2016, 5, 10),
        FechaAlta = alta ?? new DateOnly(2027, 1, 1),
        FechaBaja = baja,
        TipoSocio = TipoSocio.Activo,
    };

    /// <summary>Para borrar archivos extra de una prueba (se llama después de soltar la base).</summary>
    protected virtual void Limpiar() { }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_ruta); } catch (IOException) { }
        Limpiar();
        GC.SuppressFinalize(this);
    }
}
