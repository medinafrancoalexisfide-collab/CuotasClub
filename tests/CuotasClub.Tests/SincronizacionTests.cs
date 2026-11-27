using CuotasClub.Models;
using CuotasClub.Repositories;
using CuotasClub.Services;

namespace CuotasClub.Tests;

public class SincronizacionTests : BaseTemporal
{
    private static readonly DateTime Ahora = new(2027, 3, 10, 18, 30, 0);

    /// <summary>Destino falso: guarda lo que se escribiría en Google, o falla como se le pida.</summary>
    private sealed class DestinoFalso(Exception? falla = null, Action? durante = null) : IDestinoPlanilla
    {
        public string? Id;
        public IReadOnlyList<PestanaPlanilla>? Pestanas;
        public Task EscribirAsync(string idPlanilla, IReadOnlyList<PestanaPlanilla> pestanas, CancellationToken ct = default)
        {
            durante?.Invoke();
            if (falla is not null) throw falla;
            Id = idPlanilla;
            Pestanas = pestanas;
            return Task.CompletedTask;
        }
    }

    private string _json = "";

    private ConfiguracionService ConfigurarGoogle()
    {
        _json = Path.Combine(Path.GetTempPath(), $"cred-{Guid.NewGuid():N}.json");
        File.WriteAllText(_json, """{"type":"service_account","client_email":"cuotasclub-sync@proyecto.iam.gserviceaccount.com"}""");
        var c = new ConfiguracionService(Db)
        {
            RutaCredenciales = _json,
            IdPlanillaTesoreria = "https://docs.google.com/spreadsheets/d/ABC123_x-9/edit#gid=0",
        };
        return c;
    }

    private void CargarDatos()
    {
        var alumnos = new AlumnoRepository(Db);
        var cuotas = new CuotaService(Db);
        var a1 = alumnos.Insertar(NuevoAlumno(dni: "40000001"));
        var a2 = alumnos.Insertar(NuevoAlumno(apellido: "Gómez", dni: "40000002"));
        cuotas.RegistrarPago(a1, 2027, 1, new DateOnly(2027, 1, 5), MedioPago.Efectivo);
        cuotas.RegistrarPago(a2, 2027, 1, new DateOnly(2027, 1, 6), MedioPago.Transferencia);
        cuotas.RegistrarPago(a1, 2027, 2, new DateOnly(2027, 2, 3), MedioPago.Efectivo);
        new GastoRepository(Db).Insertar(new Gasto { Fecha = new DateOnly(2027, 1, 20), Categoria = CategoriaGasto.Transporte, Descripcion = "Colectivo", Monto = 7777m });
    }

    [Fact]
    public void Extrae_el_id_del_enlace_o_lo_deja_si_ya_es_un_id()
    {
        Assert.Equal("ABC123_x-9", PlanillaTesoreria.ExtraerIdPlanilla("https://docs.google.com/spreadsheets/d/ABC123_x-9/edit?usp=sharing"));
        Assert.Equal("ABC123_x-9", PlanillaTesoreria.ExtraerIdPlanilla("  ABC123_x-9 "));
        Assert.Null(PlanillaTesoreria.ExtraerIdPlanilla("  "));
    }

    [Fact]
    public async Task Sin_configurar_no_falla_y_queda_pendiente()
    {
        CargarDatos();
        var destino = new DestinoFalso();
        var r = await new SincronizacionService(Db, _ => destino).SincronizarAsync(Ahora);
        Assert.Equal(EstadoSincronizacion.NoConfigurado, r.Estado);
        Assert.Null(destino.Pestanas);
        Assert.True(new ConfiguracionService(Db).SincronizacionPendiente);
    }

    [Fact]
    public async Task Sube_solo_ingresos_y_parte_del_club_nunca_gastos()
    {
        ConfigurarGoogle();
        CargarDatos();
        var destino = new DestinoFalso();
        var r = await new SincronizacionService(Db, _ => destino).SincronizarAsync(Ahora);

        Assert.True(r.Ok, r.Mensaje);
        Assert.Equal("ABC123_x-9", destino.Id);
        var p = Assert.Single(destino.Pestanas!);
        Assert.Equal("2027", p.Nombre);
        Assert.Equal(["Mes", "Alumnos que pagaron", "Total ingresado", "Corresponde al club (30%)"], p.Filas[0]);
        Assert.Equal(["Enero", 2, 40000m, 12000m], p.Filas[1]);
        Assert.Equal(["Febrero", 1, 20000m, 6000m], p.Filas[2]);
        Assert.Equal(["Marzo", 0, 0m, 0m], p.Filas[3]);   // mes en curso
        Assert.Equal(["Abril", "", "", ""], p.Filas[4]);  // futuro: vacío
        Assert.Equal(13, p.FilaTotal);
        Assert.Equal(["Total", "", 60000m, 18000m], p.Filas[13]);
        Assert.Equal("Última actualización: 10/03/2027 18:30", p.Filas[^1][0]);

        // Nada de gastos, ni la parte del profesor (70%), ni ganancia.
        var celdas = p.Filas.SelectMany(f => f).Select(c => c.ToString()!).ToList();
        Assert.DoesNotContain(celdas, c => c.Contains("Gasto") || c.Contains("Ganancia") || c.Contains("7777"));
        Assert.DoesNotContain(28000m, p.Filas[1]);

        var c = new ConfiguracionService(Db);
        Assert.False(c.SincronizacionPendiente);
        Assert.Equal(Ahora, c.UltimaSincronizacion);
    }

    [Fact]
    public async Task Sin_internet_queda_pendiente_y_avisa()
    {
        ConfigurarGoogle();
        CargarDatos();
        var r = await new SincronizacionService(Db, _ => new DestinoFalso(new HttpRequestException("sin red"))).SincronizarAsync(Ahora);
        Assert.Equal(EstadoSincronizacion.Error, r.Estado);
        Assert.Contains("internet", r.Mensaje);
        var c = new ConfiguracionService(Db);
        Assert.True(c.SincronizacionPendiente);
        Assert.Null(c.UltimaSincronizacion);
    }

    [Fact]
    public async Task Un_pago_registrado_mientras_sube_queda_pendiente_para_la_proxima()
    {
        ConfigurarGoogle();
        CargarDatos();
        var destino = new DestinoFalso(durante: () =>
        {
            Thread.Sleep(5);
            new ConfiguracionService(Db).SincronizacionPendiente = true; // como hace CuotaService al registrar un pago
        });
        var r = await new SincronizacionService(Db, _ => destino).SincronizarAsync(Ahora);
        Assert.True(r.Ok);
        Assert.True(new ConfiguracionService(Db).SincronizacionPendiente);
    }

    [Fact]
    public void Una_pestana_por_anio_desde_el_inicio_de_cobros()
    {
        ConfigurarGoogle();
        var pestanas = new SincronizacionService(Db, _ => new DestinoFalso()).ArmarPestanas(new DateTime(2028, 2, 1));
        Assert.Equal(["2028", "2027"], pestanas.Select(p => p.Nombre)); // el año más nuevo primero
    }

    [Fact]
    public void Antes_del_inicio_de_cobros_igual_crea_la_pestana_del_anio_de_inicio()
    {
        ConfigurarGoogle();
        var p = Assert.Single(new SincronizacionService(Db, _ => new DestinoFalso()).ArmarPestanas(new DateTime(2026, 11, 25)));
        Assert.Equal("2027", p.Nombre);
        Assert.Equal(["Enero", "", "", ""], p.Filas[1]);
    }

    [Fact]
    public void Los_meses_anteriores_al_inicio_de_cobros_quedan_vacios()
    {
        var c = ConfigurarGoogle();
        c.InicioCobros = new DateOnly(2026, 11, 1);
        var p = Assert.Single(new SincronizacionService(Db, _ => new DestinoFalso()).ArmarPestanas(new DateTime(2026, 11, 25)));
        Assert.Equal("2026", p.Nombre);
        Assert.Equal(["Octubre", "", "", ""], p.Filas[10]);
        Assert.Equal(["Noviembre", 0, 0m, 0m], p.Filas[11]);
    }

    [Fact]
    public void Lee_el_email_de_la_cuenta_de_servicio()
    {
        ConfigurarGoogle();
        Assert.Equal("cuotasclub-sync@proyecto.iam.gserviceaccount.com", SincronizacionService.LeerEmailCuentaServicio(_json));
        Assert.Null(SincronizacionService.LeerEmailCuentaServicio("no-existe.json"));
    }
}
