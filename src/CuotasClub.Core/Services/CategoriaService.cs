namespace CuotasClub.Services;

/// <summary>Edad y categoría (Sub-N o Mayor) del alumno.</summary>
public static class CategoriaService
{
    public static readonly string[] Categorias = ["Sub-8", "Sub-10", "Sub-12", "Sub-14", "Sub-16", "Sub-18", "Mayor"];

    /// <summary>Edad cumplida a la fecha dada.</summary>
    public static int CalcularEdad(DateOnly fechaNacimiento, DateOnly hoy)
    {
        var edad = hoy.Year - fechaNacimiento.Year;
        if (hoy < fechaNacimiento.AddYears(edad)) edad--;
        return Math.Max(edad, 0);
    }

    /// <summary>Edad que cumple (o cumplió) en el año: la edad al 31 de diciembre. Es la que define la categoría.</summary>
    public static int EdadAlCierreDelAnio(DateOnly fechaNacimiento, int anio) => anio - fechaNacimiento.Year;

    /// <summary>
    /// Categoría según la edad al 31 de diciembre de <paramref name="anio"/>.
    /// Sub-N = edad &lt; N (ej. 11 años → Sub-12; 12 años → Sub-14); 18 o más → Mayor.
    /// </summary>
    public static string CalcularCategoria(DateOnly fechaNacimiento, int anio)
    {
        var edad = EdadAlCierreDelAnio(fechaNacimiento, anio);
        return edad switch
        {
            < 8 => "Sub-8",
            < 10 => "Sub-10",
            < 12 => "Sub-12",
            < 14 => "Sub-14",
            < 16 => "Sub-16",
            < 18 => "Sub-18",
            _ => "Mayor"
        };
    }

    /// <summary>Categoría en el año de la fecha dada.</summary>
    public static string CalcularCategoria(DateOnly fechaNacimiento, DateOnly hoy) => CalcularCategoria(fechaNacimiento, hoy.Year);
}
