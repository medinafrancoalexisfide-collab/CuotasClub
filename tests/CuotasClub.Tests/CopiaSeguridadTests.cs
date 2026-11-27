using CuotasClub.Repositories;
using CuotasClub.Services;
using Microsoft.Data.Sqlite;

namespace CuotasClub.Tests;

public class CopiaSeguridadTests : BaseTemporal
{
    private readonly string _carpeta = Path.Combine(Path.GetTempPath(), $"cuotasclub-copias-{Guid.NewGuid():N}");
    private readonly CopiaSeguridadService _copias;

    public CopiaSeguridadTests()
    {
        _copias = new CopiaSeguridadService(Db) { Carpeta = _carpeta };
    }

    [Fact]
    public void Carpeta_vacia_usa_Documentos_CuotasClub_Copias()
    {
        var servicio = new CopiaSeguridadService(Db) { Carpeta = "" };
        Assert.Equal(CopiaSeguridadService.CarpetaPorDefecto, servicio.Carpeta);
        Assert.EndsWith(Path.Combine("CuotasClub", "Copias"), CopiaSeguridadService.CarpetaPorDefecto);
    }

    [Fact]
    public void HacerCopia_guarda_archivo_fechado_con_los_datos()
    {
        new AlumnoRepository(Db).Insertar(NuevoAlumno());
        var ruta = _copias.HacerCopia(new DateTime(2027, 3, 5, 18, 30, 0));

        Assert.Equal("cuotasclub-2027-03-05_18-30-00.db", Path.GetFileName(ruta));
        Assert.Null(CopiaSeguridadService.ValidarCopia(ruta));
        Assert.Equal(1, ContarAlumnos(ruta));
        Assert.NotNull(_copias.UltimaCopia);
        Assert.Null(_copias.ErrorUltimaCopia);
    }

    [Fact]
    public void Dos_copias_en_el_mismo_segundo_no_se_pisan()
    {
        var ahora = new DateTime(2027, 3, 5, 18, 30, 0);
        var a = _copias.HacerCopia(ahora);
        var b = _copias.HacerCopia(ahora);
        Assert.NotEqual(a, b);
        Assert.Equal(2, _copias.ListarCopias().Count);
    }

    [Fact]
    public void Conserva_las_ultimas_30_y_borra_las_mas_viejas()
    {
        var inicio = new DateTime(2027, 1, 1, 20, 0, 0);
        for (int i = 0; i < 33; i++) _copias.HacerCopia(inicio.AddDays(i));

        var copias = _copias.ListarCopias();
        Assert.Equal(CopiaSeguridadService.CopiasAConservar, copias.Count);
        Assert.Equal("cuotasclub-2027-02-02_20-00-00.db", Path.GetFileName(copias[0])); // la más nueva primero
        Assert.DoesNotContain(copias, c => Path.GetFileName(c).StartsWith("cuotasclub-2027-01-01", StringComparison.Ordinal));
    }

    [Fact]
    public void No_borra_otros_archivos_de_la_carpeta()
    {
        Directory.CreateDirectory(_carpeta);
        var otro = Path.Combine(_carpeta, "notas.txt");
        File.WriteAllText(otro, "hola");
        for (int i = 0; i < 32; i++) _copias.HacerCopia(new DateTime(2027, 1, 1).AddHours(i));
        Assert.True(File.Exists(otro));
    }

    [Fact]
    public void Si_la_carpeta_no_se_puede_usar_queda_anotado_el_error()
    {
        var archivo = Path.Combine(Path.GetTempPath(), $"no-es-carpeta-{Guid.NewGuid():N}");
        File.WriteAllText(archivo, "x");
        try
        {
            var servicio = new CopiaSeguridadService(Db) { Carpeta = archivo };
            Assert.False(servicio.HacerCopiaSilenciosa());
            Assert.False(string.IsNullOrEmpty(servicio.ErrorUltimaCopia));
            Assert.Throws<ReglaNegocioException>(() => servicio.HacerCopia());
        }
        finally { File.Delete(archivo); }
    }

    [Fact]
    public void ValidarCopia_rechaza_archivos_que_no_son_de_CuotasClub()
    {
        Directory.CreateDirectory(_carpeta);
        var txt = Path.Combine(_carpeta, "cualquiera.db");
        File.WriteAllText(txt, "esto no es una base");
        Assert.NotNull(CopiaSeguridadService.ValidarCopia(txt));
        Assert.NotNull(CopiaSeguridadService.ValidarCopia(Path.Combine(_carpeta, "no-existe.db")));
    }

    [Fact]
    public void Restaurar_vuelve_a_la_copia_y_antes_guarda_la_base_actual()
    {
        var alumnos = new AlumnoRepository(Db);
        alumnos.Insertar(NuevoAlumno());
        var copia = _copias.HacerCopia(new DateTime(2027, 3, 1, 10, 0, 0));
        alumnos.Insertar(NuevoAlumno("Gómez", "Ana", "47000111"));
        Assert.Equal(2, alumnos.Listar().Count);

        var respaldo = _copias.Restaurar(copia);

        Assert.Single(new AlumnoRepository(Db).Listar());
        Assert.EndsWith("-antes-de-restaurar.db", respaldo);
        Assert.Equal(2, ContarAlumnos(respaldo));
        Assert.Equal(_carpeta, new CopiaSeguridadService(Db).Carpeta); // la carpeta elegida no se pierde
    }

    [Fact]
    public void Restaurar_un_archivo_invalido_no_toca_la_base()
    {
        new AlumnoRepository(Db).Insertar(NuevoAlumno());
        Directory.CreateDirectory(_carpeta);
        var malo = Path.Combine(_carpeta, "malo.db");
        File.WriteAllText(malo, "basura");

        Assert.Throws<ReglaNegocioException>(() => _copias.Restaurar(malo));
        Assert.Single(new AlumnoRepository(Db).Listar());
    }

    private static int ContarAlumnos(string ruta)
    {
        using var cn = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = ruta, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        cn.Open();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Alumnos";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    protected override void Limpiar()
    {
        try { Directory.Delete(_carpeta, recursive: true); } catch (IOException) { }
    }
}
