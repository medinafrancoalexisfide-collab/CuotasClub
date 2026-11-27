using CuotasClub.Models;
using CuotasClub.Repositories;

namespace CuotasClub.Services;

/// <summary>
/// Reglas puras (sin base de datos) de estado de cuota y resúmenes. Todo lo que depende de "hoy" lo recibe por parámetro.
/// </summary>
public static class CalculoCuotas
{
    /// <summary>
    /// Estado de la cuota de un alumno en un mes:
    /// 1. Tiene un pago no anulado → Pagado (aunque el mes no corresponda: p. ej. se cobró a mano el mes de la baja).
    /// 2. Mes anterior a <paramref name="inicioCobros"/>, o el alumno no estaba activo ese mes → NoCorresponde.
    /// 3. El mes ya terminó → Debe.
    /// 4. Mes en curso o futuro → Pendiente (tiene todo el mes para pagar).
    /// </summary>
    public static EstadoCuota CalcularEstado(Alumno alumno, int anio, int mes, Pago? pago, DateOnly hoy, DateOnly inicioCobros)
    {
        if (pago is not null && !pago.Anulado) return EstadoCuota.Pagado;
        var inicioMes = new DateOnly(anio, mes, 1);
        if (inicioMes < new DateOnly(inicioCobros.Year, inicioCobros.Month, 1)) return EstadoCuota.NoCorresponde;
        if (!alumno.EstaActivoEn(anio, mes)) return EstadoCuota.NoCorresponde;
        var finMes = inicioMes.AddMonths(1).AddDays(-1);
        return hoy > finMes ? EstadoCuota.Debe : EstadoCuota.Pendiente;
    }

    /// <summary>La cuota corresponde ese mes (se cuenta como alumno activo del mes).</summary>
    public static bool Corresponde(Alumno alumno, int anio, int mes, DateOnly inicioCobros) =>
        new DateOnly(anio, mes, 1) >= new DateOnly(inicioCobros.Year, inicioCobros.Month, 1) && alumno.EstaActivoEn(anio, mes);

    public static decimal ParteClub(decimal total, decimal porcentajeClub) =>
        Math.Round(total * porcentajeClub / 100m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Parte del club de un conjunto de pagos, cada uno con su propio % guardado.</summary>
    public static decimal ParteClub(IEnumerable<Pago> pagos) =>
        Math.Round(pagos.Sum(p => p.Monto * p.PorcentajeClub / 100m), 2, MidpointRounding.AwayFromZero);

    /// <summary>Resumen de un mes a partir de los datos ya leídos.</summary>
    /// <param name="pagosDelMes">Pagos no anulados de ese mes (de cualquier alumno). El reparto usa el % guardado en cada pago.</param>
    /// <param name="porcentajeClub">% vigente del mes, solo informativo (se muestra en pantalla).</param>
    public static ResumenMensual ResumenMensual(int anio, int mes, IReadOnlyCollection<Alumno> alumnos, IReadOnlyCollection<Pago> pagosDelMes,
        decimal gastosDelMes, decimal porcentajeClub, DateOnly hoy, DateOnly inicioCobros)
    {
        var pagos = pagosDelMes.Where(p => !p.Anulado && p.Anio == anio && p.Mes == mes).ToList();
        var pagaron = pagos.Select(p => p.AlumnoId).ToHashSet();
        int activos = 0, deben = 0;
        foreach (var a in alumnos)
        {
            if (Corresponde(a, anio, mes, inicioCobros)) activos++;
            var pago = pagos.FirstOrDefault(p => p.AlumnoId == a.Id);
            if (CalcularEstado(a, anio, mes, pago, hoy, inicioCobros) == EstadoCuota.Debe) deben++;
        }
        var total = pagos.Sum(p => p.Monto);
        var club = ParteClub(pagos);
        var mio = total - club;
        return new ResumenMensual
        {
            Anio = anio,
            Mes = mes,
            PorcentajeClub = porcentajeClub,
            AlumnosActivos = activos,
            AlumnosQuePagaron = pagaron.Count,
            AlumnosQueDeben = deben,
            TotalIngresado = total,
            ParaElClub = club,
            ParaMi = mio,
            Gastos = gastosDelMes,
            GananciaNeta = mio - gastosDelMes,
        };
    }

    /// <summary>Totales y promedios del año a partir de los 12 resúmenes mensuales.</summary>
    public static ResumenAnual ResumenAnual(int anio, IReadOnlyList<ResumenMensual> meses, DateOnly hoy)
    {
        if (meses.Count != 12) throw new ArgumentException("Se esperan 12 meses.", nameof(meses));
        var conActividad = meses
            .Where(m => new DateOnly(anio, m.Mes, 1) <= hoy)
            .Where(m => m.AlumnosActivos > 0 || m.AlumnosQuePagaron > 0 || m.Gastos != 0)
            .ToList();
        int n = conActividad.Count;
        decimal Prom(Func<ResumenMensual, decimal> f) => n == 0 ? 0 : Math.Round(conActividad.Sum(f) / n, 2, MidpointRounding.AwayFromZero);

        return new ResumenAnual
        {
            Anio = anio,
            Meses = meses,
            MesesConActividad = n,
            Total = new ResumenMensual
            {
                Anio = anio,
                Mes = 0,
                AlumnosActivos = meses.Sum(m => m.AlumnosActivos),
                AlumnosQuePagaron = meses.Sum(m => m.AlumnosQuePagaron),
                AlumnosQueDeben = meses.Sum(m => m.AlumnosQueDeben),
                TotalIngresado = meses.Sum(m => m.TotalIngresado),
                ParaElClub = meses.Sum(m => m.ParaElClub),
                ParaMi = meses.Sum(m => m.ParaMi),
                Gastos = meses.Sum(m => m.Gastos),
                GananciaNeta = meses.Sum(m => m.GananciaNeta),
            },
            Promedio = new PromedioMensual
            {
                AlumnosActivos = Prom(m => m.AlumnosActivos),
                AlumnosQuePagaron = Prom(m => m.AlumnosQuePagaron),
                AlumnosQueDeben = Prom(m => m.AlumnosQueDeben),
                TotalIngresado = Prom(m => m.TotalIngresado),
                ParaElClub = Prom(m => m.ParaElClub),
                ParaMi = Prom(m => m.ParaMi),
                Gastos = Prom(m => m.Gastos),
                GananciaNeta = Prom(m => m.GananciaNeta),
            },
        };
    }

    /// <summary>Meses adeudados (estado Debe) de un alumno hasta el último mes terminado.</summary>
    public static List<(int Anio, int Mes)> MesesAdeudados(Alumno alumno, IReadOnlyCollection<Pago> pagosDelAlumno, DateOnly hoy, DateOnly inicioCobros)
    {
        var lista = new List<(int, int)>();
        var desde = new DateOnly(Math.Max(inicioCobros.Year * 100 + inicioCobros.Month, alumno.FechaAlta.Year * 100 + alumno.FechaAlta.Month) / 100,
                                 Math.Max(inicioCobros.Year * 100 + inicioCobros.Month, alumno.FechaAlta.Year * 100 + alumno.FechaAlta.Month) % 100, 1);
        var mesActual = new DateOnly(hoy.Year, hoy.Month, 1);
        for (var m = desde; m < mesActual; m = m.AddMonths(1))
        {
            var pago = pagosDelAlumno.FirstOrDefault(p => !p.Anulado && p.Anio == m.Year && p.Mes == m.Month);
            if (CalcularEstado(alumno, m.Year, m.Month, pago, hoy, inicioCobros) == EstadoCuota.Debe) lista.Add((m.Year, m.Month));
        }
        return lista;
    }
}
