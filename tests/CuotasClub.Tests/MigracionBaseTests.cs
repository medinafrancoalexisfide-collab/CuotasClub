using CuotasClub.Data;
using CuotasClub.Services;
using Xunit;

namespace CuotasClub.Tests;

public class MigracionBaseTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"cuotasclub-mig-{Guid.NewGuid():N}");

    public MigracionBaseTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_dir, true); } catch (IOException) { }
    }

    private string CrearBaseVieja()
    {
        var ruta = Path.Combine(_dir, "programa", Database.NombreArchivo);
        var db = new Database(ruta);
        db.Inicializar(new DateOnly(2027, 1, 1));
        new ConfiguracionService(db).GuardarConfiguracionInicial("Club viejo", "Taller", "", 15000m, 30m, new DateOnly(2027, 1, 1));
        return ruta;
    }

    [Fact]
    public void Copia_la_base_vieja_si_no_hay_en_la_carpeta_de_datos()
    {
        var vieja = CrearBaseVieja();
        var nueva = Path.Combine(_dir, "datos", Database.NombreArchivo);

        Assert.True(Database.MigrarBaseAntigua(vieja, nueva));

        Assert.True(File.Exists(vieja)); // la original no se toca
        var db = new Database(nueva);
        db.Inicializar();
        var config = new ConfiguracionService(db);
        Assert.Equal("Club viejo", config.NombreClub);
        Assert.False(config.FaltaConfiguracionInicial);
    }

    [Fact]
    public void No_pisa_una_base_que_ya_existe()
    {
        var vieja = CrearBaseVieja();
        var nueva = Path.Combine(_dir, "datos", Database.NombreArchivo);
        Directory.CreateDirectory(Path.GetDirectoryName(nueva)!);
        File.WriteAllText(nueva, "ya estaba");

        Assert.False(Database.MigrarBaseAntigua(vieja, nueva));
        Assert.Equal("ya estaba", File.ReadAllText(nueva));
    }

    [Fact]
    public void Sin_base_vieja_no_hace_nada()
    {
        var nueva = Path.Combine(_dir, "datos", Database.NombreArchivo);
        Assert.False(Database.MigrarBaseAntigua(Path.Combine(_dir, "no-existe.db"), nueva));
        Assert.False(File.Exists(nueva));
    }

    [Fact]
    public void La_carpeta_de_datos_no_es_la_del_programa()
    {
        Assert.EndsWith(Path.Combine("CuotasClub", Database.NombreArchivo), Database.RutaPorDefecto);
        Assert.NotEqual(Database.RutaAntigua, Database.RutaPorDefecto);
    }
}
