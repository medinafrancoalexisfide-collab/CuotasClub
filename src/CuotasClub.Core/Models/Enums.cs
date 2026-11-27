namespace CuotasClub.Models;

public enum TipoSocio
{
    Activo,
    Cadete
}

public enum MedioPago
{
    Efectivo,
    Transferencia,
    MercadoPago
}

public enum CategoriaGasto
{
    Transporte,
    Materiales,
    Otros
}

/// <summary>Estado de la cuota de un alumno en un mes.</summary>
public enum EstadoCuota
{
    /// <summary>La cuota no corresponde (antes del alta, mes de la baja o posterior). Se muestra "—".</summary>
    NoCorresponde,
    /// <summary>Tiene un pago no anulado para ese mes.</summary>
    Pagado,
    /// <summary>Estaba activo, el mes ya terminó y no pagó.</summary>
    Debe,
    /// <summary>Activo pero el mes todavía no terminó (mes en curso o futuro): tiene todo el mes para pagar.</summary>
    Pendiente
}

/// <summary>Conversión entre los enums y el texto que se guarda en la base / se muestra en pantalla.</summary>
public static class EnumTextos
{
    public static string ATexto(this MedioPago medio) => medio switch
    {
        MedioPago.Efectivo => "Efectivo",
        MedioPago.Transferencia => "Transferencia",
        MedioPago.MercadoPago => "Mercado Pago",
        _ => throw new ArgumentOutOfRangeException(nameof(medio))
    };

    public static MedioPago ParseMedioPago(string texto) => texto switch
    {
        "Efectivo" => MedioPago.Efectivo,
        "Transferencia" => MedioPago.Transferencia,
        "Mercado Pago" => MedioPago.MercadoPago,
        _ => throw new FormatException($"Medio de pago desconocido: '{texto}'.")
    };

    public static string ATexto(this TipoSocio tipo) => tipo.ToString();

    public static TipoSocio ParseTipoSocio(string texto) =>
        Enum.TryParse<TipoSocio>(texto, out var t) ? t : throw new FormatException($"Tipo de socio desconocido: '{texto}'.");

    public static string ATexto(this CategoriaGasto categoria) => categoria.ToString();

    public static CategoriaGasto ParseCategoriaGasto(string texto) =>
        Enum.TryParse<CategoriaGasto>(texto, out var c) ? c : throw new FormatException($"Categoría de gasto desconocida: '{texto}'.");

    public static string ATexto(this EstadoCuota estado) => estado switch
    {
        EstadoCuota.NoCorresponde => "—",
        EstadoCuota.Pagado => "Pagó",
        EstadoCuota.Debe => "Debe",
        EstadoCuota.Pendiente => "Pendiente",
        _ => throw new ArgumentOutOfRangeException(nameof(estado))
    };
}
