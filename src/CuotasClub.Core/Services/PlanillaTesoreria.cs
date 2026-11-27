using System.Globalization;
using System.Text.RegularExpressions;
using CuotasClub.Models;

namespace CuotasClub.Services;

/// <summary>
/// Una pestaña de la planilla del tesorero, lista para escribir desde A1.
/// Solo tiene ingresos y la parte del club: nunca gastos, la parte del profesor ni la ganancia.
/// </summary>
public sealed class PestanaPlanilla
{
    /// <summary>Nombre de la pestaña: el año, por ejemplo "2027".</summary>
    public required string Nombre { get; init; }
    /// <summary>Filas desde A1. Montos como decimal (para que Sheets les dé formato de moneda).</summary>
    public required IList<IList<object>> Filas { get; init; }
    /// <summary>Índice (desde 0) de la fila de encabezado.</summary>
    public int FilaEncabezado { get; init; }
    /// <summary>Índice (desde 0) de la fila "Total".</summary>
    public int FilaTotal { get; init; }
    /// <summary>Columnas con montos (desde 0): Total ingresado y Corresponde al club.</summary>
    public IReadOnlyList<int> ColumnasMoneda { get; init; } = [2, 3];
    public int CantidadColumnas => 4;
}

/// <summary>Arma el contenido de la planilla del tesorero (cálculo puro, sin Google).</summary>
public static class PlanillaTesoreria
{
    public const string NombreSugerido = "Cuotas Taller de Ajedrez – Tesorería";

    /// <summary>
    /// Pestaña de un año: Mes | Alumnos que pagaron | Total ingresado | Corresponde al club (30%),
    /// fila Total y debajo "Última actualización". Los meses que todavía no empezaron y los anteriores
    /// al inicio de cobros quedan vacíos.
    /// </summary>
    public static PestanaPlanilla ArmarPestana(ResumenAnual resumen, DateTime ahora, DateOnly? inicioCobros = null)
    {
        var inicio = inicioCobros ?? DateOnly.MinValue;
        inicio = new DateOnly(inicio.Year, inicio.Month, 1);
        var hoy = DateOnly.FromDateTime(ahora);
        var filas = new List<IList<object>>
        {
            new List<object> { "Mes", "Alumnos que pagaron", "Total ingresado", TituloClub(resumen, hoy) }
        };

        foreach (var m in resumen.Meses)
        {
            var primeroDelMes = new DateOnly(m.Anio, m.Mes, 1);
            bool sinDatos = primeroDelMes > hoy || (primeroDelMes < inicio && m.TotalIngresado == 0);
            filas.Add(sinDatos
                ? new List<object> { Formato.NombreMes(m.Mes), "", "", "" }
                : new List<object> { Formato.NombreMes(m.Mes), m.AlumnosQuePagaron, m.TotalIngresado, m.ParaElClub });
        }

        int filaTotal = filas.Count;
        filas.Add(new List<object> { "Total", "", resumen.Total.TotalIngresado, resumen.Total.ParaElClub });
        filas.Add(new List<object>());
        filas.Add(new List<object> { "Última actualización: " + ahora.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) });

        return new PestanaPlanilla
        {
            Nombre = resumen.Anio.ToString(CultureInfo.InvariantCulture),
            Filas = filas,
            FilaEncabezado = 0,
            FilaTotal = filaTotal,
        };
    }

    /// <summary>"Corresponde al club (30%)". Si el % cambió durante el año, sin el número.</summary>
    private static string TituloClub(ResumenAnual resumen, DateOnly hoy)
    {
        var porcentajes = resumen.Meses
            .Where(m => new DateOnly(m.Anio, m.Mes, 1) <= hoy)
            .Select(m => m.PorcentajeClub).Distinct().ToList();
        if (porcentajes.Count == 0) porcentajes = [resumen.Meses[0].PorcentajeClub];
        return porcentajes.Count == 1
            ? $"Corresponde al club ({Formato.Decimal1(porcentajes[0])}%)"
            : "Corresponde al club";
    }

    /// <summary>
    /// Acepta el ID de la planilla o el enlace completo
    /// (https://docs.google.com/spreadsheets/d/ID/edit#gid=0) y devuelve solo el ID.
    /// </summary>
    public static string? ExtraerIdPlanilla(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        var t = texto.Trim();
        var m = Regex.Match(t, @"/d/([A-Za-z0-9_-]+)");
        return m.Success ? m.Groups[1].Value : t;
    }
}
