using CuotasClub.Data;
using CuotasClub.Models;
using CuotasClub.Repositories;

namespace CuotasClub.Services;

/// <summary>Resumen mensual y anual (incluye gastos y ganancia: solo para mostrar en el programa).</summary>
public class ResumenService(Database db)
{
    private readonly AlumnoRepository _alumnos = new(db);
    private readonly PagoRepository _pagos = new(db);
    private readonly GastoRepository _gastos = new(db);
    private readonly ConfiguracionService _config = new(db);

    public ResumenMensual ObtenerResumenMensual(int anio, int mes, DateOnly? hoy = null) =>
        CalculoCuotas.ResumenMensual(anio, mes, _alumnos.Listar(), _pagos.ListarNoAnuladosDelMes(anio, mes),
            _gastos.SumarPorMes(anio, mes), _config.PorcentajeClubVigente(anio, mes), hoy ?? Hoy, _config.InicioCobros);

    public ResumenAnual ObtenerResumenAnual(int anio, DateOnly? hoy = null)
    {
        var h = hoy ?? Hoy;
        var alumnos = _alumnos.Listar();
        var pagos = _pagos.ListarNoAnuladosDelAnio(anio);
        var gastos = _gastos.SumarPorMesDelAnio(anio);
        var inicio = _config.InicioCobros;
        var meses = Enumerable.Range(1, 12)
            .Select(m => CalculoCuotas.ResumenMensual(anio, m, alumnos, pagos.Where(p => p.Mes == m).ToList(), gastos[m - 1], _config.PorcentajeClubVigente(anio, m), h, inicio))
            .ToList();
        return CalculoCuotas.ResumenAnual(anio, meses, h);
    }

    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);
}
