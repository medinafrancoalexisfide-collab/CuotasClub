using System.Globalization;

namespace CuotasClub.Services;

/// <summary>Formatos argentinos, sin depender de la cultura de Windows.</summary>
public static class Formato
{
    private static readonly NumberFormatInfo Numero = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3],
        NegativeSign = "-",
    };

    public static readonly string[] NombresMeses =
        ["Enero", "Febrero", "Marzo", "Abril", "Mayo", "Junio", "Julio", "Agosto", "Septiembre", "Octubre", "Noviembre", "Diciembre"];

    public static readonly string[] MesesCortos = ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"];

    /// <summary>"$ 20.000"; con centavos solo si los hay: "$ 1.250,50". Negativos: "-$ 3.000".</summary>
    public static string Moneda(decimal monto)
    {
        var abs = Math.Abs(monto);
        var texto = abs == decimal.Truncate(abs) ? abs.ToString("#,0", Numero) : abs.ToString("#,0.00", Numero);
        return (monto < 0 ? "-" : "") + "$ " + texto;
    }

    /// <summary>Número con un decimal: "8,5".</summary>
    public static string Decimal1(decimal valor) => valor.ToString("#,0.#", Numero);

    /// <summary>Interpreta montos escritos a mano: "20000", "20.000", "$ 20.000", "1250,50".</summary>
    public static bool TryParseMoneda(string? texto, out decimal monto)
    {
        monto = 0;
        if (string.IsNullOrWhiteSpace(texto)) return false;
        var limpio = texto.Replace("$", "").Replace(" ", "").Trim();
        return decimal.TryParse(limpio, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, Numero, out monto);
    }

    public static string NombreMes(int mes) => NombresMeses[mes - 1];

    /// <summary>"Marzo 2027".</summary>
    public static string MesAnio(int anio, int mes) => $"{NombreMes(mes)} {anio}";

    public static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
