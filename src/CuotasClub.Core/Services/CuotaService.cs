using CuotasClub.Data;
using CuotasClub.Models;
using CuotasClub.Repositories;

namespace CuotasClub.Services;

/// <summary>Grilla anual, registro y anulación de pagos, deudores.</summary>
public class CuotaService(Database db)
{
    private readonly AlumnoRepository _alumnos = new(db);
    private readonly PagoRepository _pagos = new(db);
    private readonly PrecioCuotaRepository _precios = new(db);
    private readonly PorcentajeClubRepository _porcentajes = new(db);
    private readonly ConfiguracionService _config = new(db);

    /// <summary>
    /// Una fila por alumno que tuvo al menos un mes cobrable en el año o algún pago (ordenado por apellido).
    /// </summary>
    public List<FilaGrillaCuotas> ObtenerGrillaAnual(int anio, DateOnly? hoy = null)
    {
        var h = hoy ?? Hoy;
        var inicio = _config.InicioCobros;
        var pagos = _pagos.ListarNoAnuladosDelAnio(anio).ToLookup(p => p.AlumnoId);
        var filas = new List<FilaGrillaCuotas>();
        foreach (var a in _alumnos.Listar())
        {
            var pagosAlumno = pagos[a.Id].ToList();
            var fila = new FilaGrillaCuotas { Alumno = a };
            bool incluir = pagosAlumno.Count > 0;
            for (int mes = 1; mes <= 12; mes++)
            {
                var pago = pagosAlumno.FirstOrDefault(p => p.Mes == mes);
                fila.Pagos[mes - 1] = pago;
                fila.Estados[mes - 1] = CalculoCuotas.CalcularEstado(a, anio, mes, pago, h, inicio);
                if (fila.Estados[mes - 1] != EstadoCuota.NoCorresponde) incluir = true;
            }
            if (incluir) filas.Add(fila);
        }
        return filas;
    }

    public EstadoCuota ObtenerEstado(int alumnoId, int anio, int mes, DateOnly? hoy = null)
    {
        var a = _alumnos.ObtenerPorId(alumnoId) ?? throw new ReglaNegocioException("El alumno no existe.");
        return CalculoCuotas.CalcularEstado(a, anio, mes, _pagos.ObtenerNoAnulado(alumnoId, anio, mes), hoy ?? Hoy, _config.InicioCobros);
    }

    public decimal? ObtenerPrecioVigente(int anio, int mes) => _precios.ObtenerVigente(anio, mes);

    /// <summary>
    /// Registra el pago completo de un mes con el precio y el % del club vigentes para ese mes (quedan guardados en el pago).
    /// Solo en meses en que el alumno está activo (<see cref="Alumno.EstaActivoEn"/>): el mes de la baja no se cobra,
    /// salvo que la baja sea el último día del mes. Errores: <see cref="ReglaNegocioException"/>.
    /// </summary>
    public Pago RegistrarPago(int alumnoId, int anio, int mes, DateOnly fechaPago, MedioPago medio)
    {
        if (mes is < 1 or > 12) throw new ReglaNegocioException("El mes tiene que estar entre 1 y 12.");
        var a = _alumnos.ObtenerPorId(alumnoId) ?? throw new ReglaNegocioException("El alumno no existe.");
        var inicio = _config.InicioCobros;
        if (new DateOnly(anio, mes, 1) < new DateOnly(inicio.Year, inicio.Month, 1))
            throw new ReglaNegocioException($"El programa cobra desde {Formato.MesAnio(inicio.Year, inicio.Month)}.");
        if (!a.EstaActivoEn(anio, mes))
            throw new ReglaNegocioException(
                $"{a.NombreCompleto} no estaba activo en {Formato.MesAnio(anio, mes)}, así que ese mes no se cobra. " +
                "Si se dio de baja ese mes y querés cobrarlo, poné la fecha de baja el último día del mes. " +
                "Si estuvo ausente y volvió, se cobra desde el mes de regreso.");
        var precio = _precios.ObtenerVigente(anio, mes)
            ?? throw new ReglaNegocioException($"No hay un precio de cuota cargado para {Formato.MesAnio(anio, mes)}. Cargalo en Configuración.");
        var porcentaje = _porcentajes.ObtenerVigenteONulo(anio, mes)
            ?? throw new ReglaNegocioException($"No hay un porcentaje del club cargado para {Formato.MesAnio(anio, mes)}. Cargalo en Configuración.");
        var pago = new Pago
        {
            AlumnoId = alumnoId, Anio = anio, Mes = mes,
            Monto = precio,                                         // precio vigente de ese mes, queda fijo
            PorcentajeClub = porcentaje,                            // % vigente de ese mes, queda fijo
            FechaPago = fechaPago, MedioPago = medio
        };
        _pagos.Insertar(pago);
        _config.SincronizacionPendiente = true;
        return pago;
    }

    /// <summary>Anula un pago cargado por error. El mes vuelve a quedar como Debe/Pendiente.</summary>
    public void AnularPago(int pagoId)
    {
        _pagos.Anular(pagoId);
        _config.SincronizacionPendiente = true;
    }

    /// <summary>Alumnos con meses terminados sin pagar, ordenados por apellido. Incluye a los dados de baja que quedaron debiendo.</summary>
    public List<Deudor> ObtenerDeudores(DateOnly? hoy = null)
    {
        var h = hoy ?? Hoy;
        var inicio = _config.InicioCobros;
        var precios = _precios.Listar();
        var pagos = _pagos.ListarNoAnulados().ToLookup(p => p.AlumnoId);
        var deudores = new List<Deudor>();
        foreach (var a in _alumnos.Listar())
        {
            var meses = CalculoCuotas.MesesAdeudados(a, pagos[a.Id].ToList(), h, inicio);
            if (meses.Count == 0) continue;
            deudores.Add(new Deudor
            {
                Alumno = a,
                MesesAdeudados = meses,
                MontoAdeudado = meses.Sum(m => PrecioCuotaCalculo.Vigente(precios, m.Anio, m.Mes) ?? 0),
            });
        }
        return deudores;
    }

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);
}
