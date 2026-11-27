namespace CuotasClub.Models;

/// <summary>Una fila de la grilla anual de cuotas: un alumno y sus 12 meses.</summary>
public class FilaGrillaCuotas
{
    public required Alumno Alumno { get; init; }
    /// <summary>Índice 0 = enero … 11 = diciembre.</summary>
    public EstadoCuota[] Estados { get; } = new EstadoCuota[12];
    /// <summary>Pago no anulado de cada mes (null si no hay). Índice 0 = enero.</summary>
    public Pago?[] Pagos { get; } = new Pago?[12];

    public EstadoCuota EstadoDe(int mes) => Estados[mes - 1];
    public Pago? PagoDe(int mes) => Pagos[mes - 1];
}

public class ResumenMensual
{
    public int Anio { get; init; }
    public int Mes { get; init; }
    /// <summary>% del club vigente en ese mes (informativo; el reparto usa el % guardado en cada pago).</summary>
    public decimal PorcentajeClub { get; init; }
    public int AlumnosActivos { get; init; }
    public int AlumnosQuePagaron { get; init; }
    /// <summary>Activos que no pagaron un mes ya terminado (estado Debe). En el mes en curso es 0.</summary>
    public int AlumnosQueDeben { get; init; }
    public decimal TotalIngresado { get; init; }
    public decimal ParaElClub { get; init; }
    public decimal ParaMi { get; init; }
    public decimal Gastos { get; init; }
    public decimal GananciaNeta { get; init; }
}

public class ResumenAnual
{
    public int Anio { get; init; }
    /// <summary>Los 12 meses, enero a diciembre.</summary>
    public required IReadOnlyList<ResumenMensual> Meses { get; init; }
    /// <summary>Sumas del año (Mes = 0). Los conteos de alumnos son sumas de "alumno-mes".</summary>
    public required ResumenMensual Total { get; init; }
    /// <summary>Promedio mensual sobre <see cref="MesesConActividad"/>.</summary>
    public required PromedioMensual Promedio { get; init; }
    /// <summary>
    /// Meses usados para el promedio: meses ya iniciados del año (hasta hoy) que tuvieron
    /// alumnos activos, pagos o gastos. Un enero sin clases no baja el promedio.
    /// </summary>
    public int MesesConActividad { get; init; }
}

/// <summary>Promedios mensuales (decimales, también los conteos de alumnos).</summary>
public class PromedioMensual
{
    public decimal AlumnosActivos { get; init; }
    public decimal AlumnosQuePagaron { get; init; }
    public decimal AlumnosQueDeben { get; init; }
    public decimal TotalIngresado { get; init; }
    public decimal ParaElClub { get; init; }
    public decimal ParaMi { get; init; }
    public decimal Gastos { get; init; }
    public decimal GananciaNeta { get; init; }
}

/// <summary>Alumno con cuotas vencidas sin pagar.</summary>
public class Deudor
{
    public required Alumno Alumno { get; init; }
    /// <summary>Meses adeudados en orden (Anio, Mes).</summary>
    public required IReadOnlyList<(int Anio, int Mes)> MesesAdeudados { get; init; }
    public int CantidadMeses => MesesAdeudados.Count;
    /// <summary>Suma del precio vigente de cada mes adeudado.</summary>
    public decimal MontoAdeudado { get; init; }
}
