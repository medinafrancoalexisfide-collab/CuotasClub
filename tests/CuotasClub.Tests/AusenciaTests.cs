using CuotasClub.Models;
using CuotasClub.Repositories;
using CuotasClub.Services;
using Microsoft.Data.Sqlite;

namespace CuotasClub.Tests;

/// <summary>Alumnos que se van unos meses y vuelven: lo del medio es "—" y se cobra desde el mes de regreso.</summary>
public class AusenciaTests : BaseTemporal
{
    private static Alumno ConAusencia(string baja, string regreso) => new()
    {
        Nombre = "Juan", Apellido = "Pérez", Dni = "45123456",
        FechaAlta = new DateOnly(2027, 1, 10),
        Ausencias = [new Ausencia(DateOnly.Parse(baja), DateOnly.Parse(regreso))],
    };

    [Fact]
    public void El_mes_de_la_baja_y_los_del_medio_no_corresponden_y_el_de_regreso_si()
    {
        var a = ConAusencia("2027-03-10", "2027-06-05");
        Assert.True(a.EstaActivoEn(2027, 2));
        Assert.False(a.EstaActivoEn(2027, 3)); // mes de la baja: no se cobra (igual que una baja común)
        Assert.False(a.EstaActivoEn(2027, 4));
        Assert.False(a.EstaActivoEn(2027, 5));
        Assert.True(a.EstaActivoEn(2027, 6));  // vuelve en junio: se cobra junio
        Assert.True(a.EstaActivoEn(2027, 7));
    }

    [Fact]
    public void Baja_a_fin_de_mes_se_cobra_ese_mes()
    {
        var a = ConAusencia("2027-03-31", "2027-05-02");
        Assert.True(a.EstaActivoEn(2027, 3));
        Assert.False(a.EstaActivoEn(2027, 4));
        Assert.True(a.EstaActivoEn(2027, 5));
    }

    [Fact]
    public void Reactivar_conserva_el_hueco_en_grilla_deudores_y_resumen()
    {
        var a = NuevoAlumno(alta: new DateOnly(2027, 1, 5)); new AlumnoService(Db).Guardar(a);
        var repo = new AlumnoRepository(Db);
        repo.DarDeBaja(a.Id, new DateOnly(2027, 2, 15));
        repo.Reactivar(a.Id, new DateOnly(2027, 5, 3));

        var leido = repo.ObtenerPorId(a.Id)!;
        Assert.Null(leido.FechaBaja);
        Assert.Equal([new Ausencia(new DateOnly(2027, 2, 15), new DateOnly(2027, 5, 3))], leido.Ausencias);

        var cuotas = new CuotaService(Db);
        var hoy = new DateOnly(2027, 6, 10);
        var fila = cuotas.ObtenerGrillaAnual(2027, hoy).Single();
        Assert.Equal(
            [EstadoCuota.Debe, EstadoCuota.NoCorresponde, EstadoCuota.NoCorresponde, EstadoCuota.NoCorresponde, EstadoCuota.Debe, EstadoCuota.Pendiente],
            fila.Estados.Take(6));
        Assert.Equal([(2027, 1), (2027, 5)], cuotas.ObtenerDeudores(hoy).Single().MesesAdeudados);
        Assert.Throws<ReglaNegocioException>(() => cuotas.RegistrarPago(a.Id, 2027, 3, hoy, MedioPago.Efectivo));
        cuotas.RegistrarPago(a.Id, 2027, 5, hoy, MedioPago.Efectivo);

        var resumen = new ResumenService(Db);
        Assert.Equal(0, resumen.ObtenerResumenMensual(2027, 4, hoy).AlumnosActivos);
        Assert.Equal(0, resumen.ObtenerResumenMensual(2027, 4, hoy).AlumnosQueDeben);
        Assert.Equal(1, resumen.ObtenerResumenMensual(2027, 5, hoy).AlumnosActivos);

        // Se puede ir y volver otra vez; la nueva baja no puede ser antes del último regreso.
        Assert.Throws<ReglaNegocioException>(() => repo.DarDeBaja(a.Id, new DateOnly(2027, 4, 1)));
        repo.DarDeBaja(a.Id, new DateOnly(2027, 8, 20));
        repo.Reactivar(a.Id, new DateOnly(2027, 10, 1));
        Assert.Equal(2, repo.ObtenerPorId(a.Id)!.Ausencias.Count);
        Assert.False(repo.ObtenerPorId(a.Id)!.EstaActivoEn(2027, 9));
    }

    [Fact]
    public void Regreso_tiene_que_ser_despues_de_la_baja_y_solo_si_tiene_baja()
    {
        var a = NuevoAlumno(); new AlumnoService(Db).Guardar(a);
        var repo = new AlumnoRepository(Db);
        Assert.Throws<ReglaNegocioException>(() => repo.Reactivar(a.Id, new DateOnly(2027, 5, 1)));
        repo.DarDeBaja(a.Id, new DateOnly(2027, 3, 10));
        Assert.Throws<ReglaNegocioException>(() => repo.Reactivar(a.Id, new DateOnly(2027, 3, 10)));
        Assert.NotNull(repo.ObtenerPorId(a.Id)!.FechaBaja);
    }

    [Fact]
    public void Ausente_hoy_no_figura_entre_los_activos()
    {
        var a = NuevoAlumno(); new AlumnoService(Db).Guardar(a);
        var repo = new AlumnoRepository(Db);
        repo.DarDeBaja(a.Id, new DateOnly(2027, 3, 10));
        repo.Reactivar(a.Id, new DateOnly(2027, 6, 1)); // avisó que vuelve en junio
        Assert.Empty(repo.Listar(soloActivos: true, hoy: new DateOnly(2027, 4, 15)));
        Assert.Single(repo.Listar(soloActivos: true, hoy: new DateOnly(2027, 6, 1)));
    }

    [Fact]
    public void Una_base_vieja_suma_la_tabla_sin_perder_datos()
    {
        var a = NuevoAlumno(baja: new DateOnly(2027, 4, 10)); new AlumnoService(Db).Guardar(a);
        new CuotaService(Db).RegistrarPago(a.Id, 2027, 1, new DateOnly(2027, 1, 5), MedioPago.Efectivo);
        using (var cn = Db.AbrirConexion())
        using (var cmd = cn.CreateCommand())
        {
            // Así quedaba una base de la versión 0.8.
            cmd.CommandText = "DROP TABLE Ausencias; UPDATE Configuracion SET Valor = '1' WHERE Clave = 'VersionEsquema';";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();

        Db.Inicializar(new DateOnly(2027, 5, 1));
        var repo = new AlumnoRepository(Db);
        Assert.Equal(new DateOnly(2027, 4, 10), repo.ObtenerPorId(a.Id)!.FechaBaja);
        Assert.Equal(EstadoCuota.Pagado, new CuotaService(Db).ObtenerEstado(a.Id, 2027, 1, new DateOnly(2027, 5, 1)));
        repo.Reactivar(a.Id, new DateOnly(2027, 6, 1));
        Assert.Single(repo.ObtenerPorId(a.Id)!.Ausencias);
        using var cn2 = Db.AbrirConexion();
        using var cmd2 = cn2.CreateCommand();
        cmd2.CommandText = "SELECT Valor FROM Configuracion WHERE Clave = 'VersionEsquema'";
        Assert.Equal("3", cmd2.ExecuteScalar());
    }
}
