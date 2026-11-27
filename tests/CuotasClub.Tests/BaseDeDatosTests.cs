using CuotasClub.Models;
using CuotasClub.Repositories;
using CuotasClub.Services;

namespace CuotasClub.Tests;

public class BaseDeDatosTests : BaseTemporal
{
    [Fact]
    public void Inicializar_dos_veces_no_duplica_valores_iniciales()
    {
        Db.Inicializar();
        Assert.Single(new PrecioCuotaRepository(Db).Listar());
        Assert.Single(new PorcentajeClubRepository(Db).Listar());
        Assert.Equal(20000m, new PrecioCuotaRepository(Db).ObtenerVigente(2027, 1));
        Assert.Equal(30m, new ConfiguracionService(Db).PorcentajeClubVigente(2027, 1));
        Assert.Equal(new DateOnly(2027, 1, 1), new ConfiguracionService(Db).InicioCobros);
    }

    [Fact]
    public void Alumnos_se_listan_por_apellido_y_se_buscan()
    {
        var s = new AlumnoService(Db);
        s.Guardar(NuevoAlumno("Zárate", "Luis", "30000001"), new DateOnly(2027, 1, 1));
        s.Guardar(NuevoAlumno("Acosta", "María", "30000002"), new DateOnly(2027, 1, 1));
        s.Guardar(NuevoAlumno("mendoza", "Lucía", "30000003"), new DateOnly(2027, 1, 1));
        var repo = new AlumnoRepository(Db);

        Assert.Equal(["Acosta", "mendoza", "Zárate"], repo.Listar().Select(a => a.Apellido));
        Assert.Equal("mendoza", repo.Listar(texto: "MEND").Single().Apellido);
        Assert.Equal("Zárate", repo.Listar(texto: "zarate").Single().Apellido);
        Assert.Equal("Acosta", repo.Listar(texto: "maria").Single().Apellido);
        Assert.Equal("Acosta", repo.Listar(texto: "30000002").Single().Apellido);
        Assert.Equal("Zárate", repo.Listar(texto: "luis zár").Single().Apellido);
        Assert.Empty(repo.Listar(texto: "%"));
    }

    [Fact]
    public void Dni_repetido_se_rechaza()
    {
        var s = new AlumnoService(Db);
        s.Guardar(NuevoAlumno(dni: "12345678"));
        var ex = Assert.Throws<ReglaNegocioException>(() => s.Guardar(NuevoAlumno("Otro", "X", "12.345.678")));
        Assert.Contains("DNI", ex.Message);
    }

    [Fact]
    public void Editar_un_alumno_conserva_su_propio_dni()
    {
        var s = new AlumnoService(Db);
        var a = NuevoAlumno(dni: "12345678");
        s.Guardar(a);
        a.Celular = "3764 123456";
        s.Guardar(a);
        Assert.Equal("3764 123456", new AlumnoRepository(Db).ObtenerPorId(a.Id)!.Celular);
    }

    [Fact]
    public void Baja_y_lista_de_activos()
    {
        var s = new AlumnoService(Db);
        var a = NuevoAlumno(dni: "11111111"); s.Guardar(a);
        var b = NuevoAlumno("Bravo", dni: "22222222"); s.Guardar(b);
        var repo = new AlumnoRepository(Db);
        repo.DarDeBaja(a.Id, new DateOnly(2027, 5, 10));

        Assert.Equal(2, repo.Listar(soloActivos: true, hoy: new DateOnly(2027, 5, 9)).Count);
        Assert.Single(repo.Listar(soloActivos: true, hoy: new DateOnly(2027, 5, 10)));
        Assert.Equal(2, repo.Listar().Count);

        repo.QuitarBaja(a.Id);
        Assert.Null(repo.ObtenerPorId(a.Id)!.FechaBaja);
        Assert.Empty(repo.ObtenerPorId(a.Id)!.Ausencias);
        Assert.Throws<ReglaNegocioException>(() => repo.DarDeBaja(a.Id, new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void Registrar_pago_usa_precio_y_porcentaje_vigentes_y_los_conserva()
    {
        var a = NuevoAlumno(); new AlumnoService(Db).Guardar(a);
        var cuotas = new CuotaService(Db);
        var config = new ConfiguracionService(Db);

        var marzo = cuotas.RegistrarPago(a.Id, 2027, 3, new DateOnly(2027, 3, 5), MedioPago.MercadoPago);

        // En abril cambia el precio y el % del club, desde abril.
        new PrecioCuotaRepository(Db).Guardar(2027, 4, 25000m);
        config.GuardarPorcentajeClub(2027, 4, 35m);
        var abril = cuotas.RegistrarPago(a.Id, 2027, 4, new DateOnly(2027, 4, 6), MedioPago.Efectivo);
        // Un pago atrasado de febrero, registrado después del cambio, usa los valores de febrero.
        var febrero = cuotas.RegistrarPago(a.Id, 2027, 2, new DateOnly(2027, 4, 7), MedioPago.Transferencia);

        var pagos = new PagoRepository(Db);
        Assert.Equal((20000m, 30m), (pagos.ObtenerPorId(marzo.Id)!.Monto, pagos.ObtenerPorId(marzo.Id)!.PorcentajeClub));
        Assert.Equal((25000m, 35m), (pagos.ObtenerPorId(abril.Id)!.Monto, pagos.ObtenerPorId(abril.Id)!.PorcentajeClub));
        Assert.Equal((20000m, 30m), (pagos.ObtenerPorId(febrero.Id)!.Monto, pagos.ObtenerPorId(febrero.Id)!.PorcentajeClub));
        Assert.Equal(MedioPago.MercadoPago, pagos.ObtenerPorId(marzo.Id)!.MedioPago);

        // Aunque después cambie otra vez el % de marzo hacia atrás, el resumen de marzo no cambia.
        config.GuardarPorcentajeClub(2027, 1, 50m);
        var resumenMarzo = new ResumenService(Db).ObtenerResumenMensual(2027, 3, new DateOnly(2027, 5, 1));
        Assert.Equal(6000m, resumenMarzo.ParaElClub);
        Assert.Equal(14000m, resumenMarzo.ParaMi);
    }

    [Fact]
    public void No_se_puede_pagar_dos_veces_el_mismo_mes_pero_si_despues_de_anular()
    {
        var a = NuevoAlumno(); new AlumnoService(Db).Guardar(a);
        var cuotas = new CuotaService(Db);
        var p = cuotas.RegistrarPago(a.Id, 2027, 3, new DateOnly(2027, 3, 5), MedioPago.Efectivo);

        Assert.Throws<ReglaNegocioException>(() => cuotas.RegistrarPago(a.Id, 2027, 3, new DateOnly(2027, 3, 6), MedioPago.Efectivo));

        cuotas.AnularPago(p.Id);
        Assert.Equal(EstadoCuota.Debe, cuotas.ObtenerEstado(a.Id, 2027, 3, new DateOnly(2027, 4, 1)));
        Assert.Throws<ReglaNegocioException>(() => cuotas.AnularPago(p.Id));

        cuotas.RegistrarPago(a.Id, 2027, 3, new DateOnly(2027, 3, 6), MedioPago.Transferencia);
        Assert.Equal(EstadoCuota.Pagado, cuotas.ObtenerEstado(a.Id, 2027, 3, new DateOnly(2027, 4, 1)));
        Assert.Equal(2, new PagoRepository(Db).ListarPorAlumno(a.Id).Count); // el anulado queda en el historial
        Assert.True(new ConfiguracionService(Db).SincronizacionPendiente);
    }

    [Fact]
    public void Pagos_fuera_del_periodo_se_rechazan()
    {
        var a = NuevoAlumno(alta: new DateOnly(2027, 3, 15), baja: new DateOnly(2027, 8, 10));
        new AlumnoService(Db).Guardar(a);
        var cuotas = new CuotaService(Db);

        Assert.Throws<ReglaNegocioException>(() => cuotas.RegistrarPago(a.Id, 2027, 2, new DateOnly(2027, 3, 15), MedioPago.Efectivo));
        Assert.Throws<ReglaNegocioException>(() => cuotas.RegistrarPago(a.Id, 2027, 9, new DateOnly(2027, 9, 1), MedioPago.Efectivo));
        Assert.Throws<ReglaNegocioException>(() => cuotas.RegistrarPago(a.Id, 2026, 12, new DateOnly(2027, 1, 1), MedioPago.Efectivo));
        // El mes de la baja (a mitad de mes) no se cobra.
        Assert.Throws<ReglaNegocioException>(() => cuotas.RegistrarPago(a.Id, 2027, 8, new DateOnly(2027, 8, 5), MedioPago.Efectivo));
        cuotas.RegistrarPago(a.Id, 2027, 7, new DateOnly(2027, 7, 5), MedioPago.Efectivo);
    }

    [Fact]
    public void Baja_a_fin_de_mes_permite_cobrar_ese_mes()
    {
        var a = NuevoAlumno(alta: new DateOnly(2027, 3, 15), baja: new DateOnly(2027, 8, 31));
        new AlumnoService(Db).Guardar(a);
        var cuotas = new CuotaService(Db);
        cuotas.RegistrarPago(a.Id, 2027, 8, new DateOnly(2027, 8, 5), MedioPago.Efectivo);
        Assert.Throws<ReglaNegocioException>(() => cuotas.RegistrarPago(a.Id, 2027, 9, new DateOnly(2027, 9, 5), MedioPago.Efectivo));
    }

    [Fact]
    public void Grilla_anual_y_deudores()
    {
        var s = new AlumnoService(Db);
        var ana = NuevoAlumno("Gómez", "Ana", "11111111", alta: new DateOnly(2027, 1, 5));
        var beto = NuevoAlumno("Álvarez", "Beto", "22222222", alta: new DateOnly(2027, 3, 1), baja: new DateOnly(2027, 4, 20));
        var caro = NuevoAlumno("Ruiz", "Caro", "33333333", alta: new DateOnly(2028, 2, 1)); // no aparece en 2027
        s.Guardar(ana); s.Guardar(beto); s.Guardar(caro);
        var cuotas = new CuotaService(Db);
        cuotas.RegistrarPago(ana.Id, 2027, 1, new DateOnly(2027, 1, 5), MedioPago.Efectivo);
        cuotas.RegistrarPago(ana.Id, 2027, 3, new DateOnly(2027, 3, 5), MedioPago.Efectivo);

        var hoy = new DateOnly(2027, 5, 10);
        var grilla = cuotas.ObtenerGrillaAnual(2027, hoy);

        Assert.Equal(["Álvarez", "Gómez"], grilla.Select(f => f.Alumno.Apellido));
        var g = grilla.Single(f => f.Alumno.Id == ana.Id);
        Assert.Equal(
            [EstadoCuota.Pagado, EstadoCuota.Debe, EstadoCuota.Pagado, EstadoCuota.Debe, EstadoCuota.Pendiente, EstadoCuota.Pendiente],
            g.Estados.Take(6));
        Assert.NotNull(g.PagoDe(1));
        var b = grilla.Single(f => f.Alumno.Id == beto.Id);
        Assert.Equal([EstadoCuota.NoCorresponde, EstadoCuota.NoCorresponde, EstadoCuota.Debe, EstadoCuota.NoCorresponde, EstadoCuota.NoCorresponde],
            b.Estados.Take(5));

        var deudores = cuotas.ObtenerDeudores(hoy);
        Assert.Equal(["Álvarez", "Gómez"], deudores.Select(d => d.Alumno.Apellido));
        Assert.Equal([(2027, 2), (2027, 4)], deudores.Single(d => d.Alumno.Id == ana.Id).MesesAdeudados);
        Assert.Equal(40000m, deudores.Single(d => d.Alumno.Id == ana.Id).MontoAdeudado);
        Assert.Equal(1, deudores.Single(d => d.Alumno.Id == beto.Id).CantidadMeses);
    }

    [Fact]
    public void Resumen_mensual_desde_la_base_con_gastos()
    {
        var s = new AlumnoService(Db);
        var cuotas = new CuotaService(Db);
        for (int i = 0; i < 12; i++)
        {
            var a = NuevoAlumno($"Ap{i:00}", "X", $"4000{i:0000}");
            s.Guardar(a);
            if (i < 11) cuotas.RegistrarPago(a.Id, 2027, 3, new DateOnly(2027, 3, 5), MedioPago.Efectivo);
        }
        var gastos = new GastoRepository(Db);
        gastos.Insertar(new Gasto { Fecha = new DateOnly(2027, 3, 5), Categoria = CategoriaGasto.Transporte, Descripcion = "Uber al club", Monto = 4500 });
        gastos.Insertar(new Gasto { Fecha = new DateOnly(2027, 3, 31), Categoria = CategoriaGasto.Transporte, Descripcion = "Colectivo", Monto = 1200.5m });
        gastos.Insertar(new Gasto { Fecha = new DateOnly(2027, 4, 1), Categoria = CategoriaGasto.Materiales, Descripcion = "Relojes", Monto = 30000 });

        var r = new ResumenService(Db).ObtenerResumenMensual(2027, 3, new DateOnly(2027, 4, 10));
        Assert.Equal(12, r.AlumnosActivos);
        Assert.Equal(11, r.AlumnosQuePagaron);
        Assert.Equal(1, r.AlumnosQueDeben);
        Assert.Equal(220000m, r.TotalIngresado);
        Assert.Equal(66000m, r.ParaElClub);
        Assert.Equal(154000m, r.ParaMi);
        Assert.Equal(5700.5m, r.Gastos);
        Assert.Equal(148299.5m, r.GananciaNeta);
        Assert.Equal(30m, r.PorcentajeClub);

        var anual = new ResumenService(Db).ObtenerResumenAnual(2027, new DateOnly(2027, 4, 10));
        Assert.Equal(220000m, anual.Total.TotalIngresado);
        Assert.Equal(4, anual.MesesConActividad); // ene–abr con alumnos activos
        Assert.Equal(2, gastos.ListarPorMes(2027, 3).Count);
        gastos.Insertar(new Gasto { Fecha = new DateOnly(2027, 4, 2), Categoria = CategoriaGasto.Transporte, Descripcion = "Uber al club", Monto = 4500 });
        Assert.Equal(["Uber al club", "Relojes", "Colectivo"], gastos.DescripcionesFrecuentes());
    }
}
