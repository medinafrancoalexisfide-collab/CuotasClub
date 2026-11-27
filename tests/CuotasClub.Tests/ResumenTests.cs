using CuotasClub.Models;
using CuotasClub.Services;

namespace CuotasClub.Tests;

public class ResumenTests
{
    private static readonly DateOnly Inicio = new(2027, 1, 1);

    private static List<Alumno> Alumnos(int n) => Enumerable.Range(1, n)
        .Select(i => new Alumno { Id = i, Nombre = "N", Apellido = $"A{i:00}", Dni = $"4000000{i % 10}", FechaAlta = new DateOnly(2027, 1, 1) })
        .ToList();

    private static List<Pago> Pagos(int anio, int mes, int n, decimal monto = 20000, decimal pct = 30) => Enumerable.Range(1, n)
        .Select(i => new Pago { AlumnoId = i, Anio = anio, Mes = mes, Monto = monto, PorcentajeClub = pct })
        .ToList();

    [Fact]
    public void Ejemplo_del_documento_11_alumnos_por_20000()
    {
        var r = CalculoCuotas.ResumenMensual(2027, 3, Alumnos(20), Pagos(2027, 3, 11), 15000m, 30m, new DateOnly(2027, 4, 2), Inicio);
        Assert.Equal(20, r.AlumnosActivos);
        Assert.Equal(11, r.AlumnosQuePagaron);
        Assert.Equal(9, r.AlumnosQueDeben);
        Assert.Equal(220000m, r.TotalIngresado);
        Assert.Equal(66000m, r.ParaElClub);
        Assert.Equal(154000m, r.ParaMi);
        Assert.Equal(15000m, r.Gastos);
        Assert.Equal(139000m, r.GananciaNeta);
    }

    [Fact]
    public void En_el_mes_en_curso_nadie_debe_todavia()
    {
        var r = CalculoCuotas.ResumenMensual(2027, 3, Alumnos(20), Pagos(2027, 3, 4), 0, 30, new DateOnly(2027, 3, 20), Inicio);
        Assert.Equal(0, r.AlumnosQueDeben);
        Assert.Equal(4, r.AlumnosQuePagaron);
    }

    [Fact]
    public void El_reparto_usa_el_porcentaje_guardado_en_cada_pago()
    {
        // 2 pagos con 30% (registrados antes del cambio) y 1 con 40%.
        var pagos = Pagos(2027, 5, 2, pct: 30);
        pagos.Add(new Pago { AlumnoId = 3, Anio = 2027, Mes = 5, Monto = 20000, PorcentajeClub = 40 });
        var r = CalculoCuotas.ResumenMensual(2027, 5, Alumnos(3), pagos, 0, 40, new DateOnly(2027, 6, 1), Inicio);
        Assert.Equal(60000m, r.TotalIngresado);
        Assert.Equal(6000m + 6000m + 8000m, r.ParaElClub);
        Assert.Equal(40000m, r.ParaMi);
    }

    [Fact]
    public void Ganancia_neta_puede_ser_negativa()
    {
        var r = CalculoCuotas.ResumenMensual(2027, 2, Alumnos(1), [], 5000, 30, new DateOnly(2027, 3, 1), Inicio);
        Assert.Equal(-5000m, r.GananciaNeta);
    }

    [Fact]
    public void Promedio_anual_solo_sobre_meses_transcurridos_con_actividad()
    {
        var hoy = new DateOnly(2027, 4, 15);
        var meses = Enumerable.Range(1, 12).Select(m => m switch
        {
            // Enero sin clases ni gastos → no cuenta. Febrero, marzo y abril sí. Mayo en adelante: futuro.
            1 => new ResumenMensual { Anio = 2027, Mes = 1 },
            2 => new ResumenMensual { Anio = 2027, Mes = 2, AlumnosActivos = 10, AlumnosQuePagaron = 8, TotalIngresado = 160000, ParaElClub = 48000, ParaMi = 112000, Gastos = 12000, GananciaNeta = 100000 },
            3 => new ResumenMensual { Anio = 2027, Mes = 3, AlumnosActivos = 10, AlumnosQuePagaron = 9, TotalIngresado = 180000, ParaElClub = 54000, ParaMi = 126000, Gastos = 6000, GananciaNeta = 120000 },
            4 => new ResumenMensual { Anio = 2027, Mes = 4, AlumnosActivos = 10, AlumnosQuePagaron = 0, TotalIngresado = 0, ParaElClub = 0, ParaMi = 0, Gastos = 0, GananciaNeta = 0 },
            _ => new ResumenMensual { Anio = 2027, Mes = m, AlumnosActivos = 10 },
        }).ToList();

        var r = CalculoCuotas.ResumenAnual(2027, meses, hoy);

        Assert.Equal(3, r.MesesConActividad);
        Assert.Equal(340000m, r.Total.TotalIngresado);
        Assert.Equal(220000m, r.Total.GananciaNeta);
        Assert.Equal(113333.33m, r.Promedio.TotalIngresado);
        Assert.Equal(5.67m, r.Promedio.AlumnosQuePagaron);
    }

    [Fact]
    public void Anio_sin_actividad_promedio_cero()
    {
        var meses = Enumerable.Range(1, 12).Select(m => new ResumenMensual { Anio = 2027, Mes = m }).ToList();
        var r = CalculoCuotas.ResumenAnual(2027, meses, new DateOnly(2027, 12, 31));
        Assert.Equal(0, r.MesesConActividad);
        Assert.Equal(0m, r.Promedio.TotalIngresado);
    }
}
