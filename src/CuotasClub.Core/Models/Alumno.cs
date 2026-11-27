namespace CuotasClub.Models;

public class Alumno
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    /// <summary>Solo dígitos, 7 u 8. Único.</summary>
    public string Dni { get; set; } = "";
    public DateOnly FechaNacimiento { get; set; }
    public string? IdFide { get; set; }
    public string? Celular { get; set; }
    public TipoSocio TipoSocio { get; set; } = TipoSocio.Activo;
    public string? ResponsableNombre { get; set; }
    public string? ResponsableCelular { get; set; }
    public DateOnly FechaAlta { get; set; }
    public DateOnly? FechaBaja { get; set; }
    public string? Observaciones { get; set; }

    /// <summary>
    /// Ausencias anteriores: el alumno se fue y después volvió. Se crean al reactivar con fecha de regreso.
    /// Los meses del medio son "—" (no corresponden, no son deuda, no se cobran).
    /// </summary>
    public List<Ausencia> Ausencias { get; set; } = [];

    /// <summary>"Apellido, Nombre".</summary>
    public string NombreCompleto => $"{Apellido}, {Nombre}";

    /// <summary>
    /// Activo en un mes (la cuota corresponde y se puede cobrar): FechaAlta ≤ fin de mes y
    /// (sin baja o FechaBaja ≥ fin de mes). El mes del alta cuenta. El mes de la baja NO se cobra,
    /// salvo que la baja sea el último día del mes: así, si el profesor quiere cobrar el mes en que el alumno
    /// se fue, pone la baja a fin de ese mes (regla acordada con el usuario, nov 2026).
    /// Las ausencias siguen la misma regla: el mes en que se fue no se cobra (salvo baja a fin de mes)
    /// y el mes en que vuelve sí se cobra.
    /// </summary>
    public bool EstaActivoEn(int anio, int mes)
    {
        var fin = new DateOnly(anio, mes, 1).AddMonths(1).AddDays(-1);
        return FechaAlta <= fin && (FechaBaja is null || FechaBaja.Value >= fin)
            && !Ausencias.Any(x => x.FechaBaja < fin && fin < x.FechaRegreso);
    }

    /// <summary>Sigue en el taller en la fecha dada (no tiene baja, o la baja es posterior, y no está en una ausencia).</summary>
    public bool EstaActivoAl(DateOnly fecha) => FechaAlta <= fecha && (FechaBaja is null || FechaBaja.Value >= fecha)
        && !Ausencias.Any(x => x.FechaBaja < fecha && fecha < x.FechaRegreso);

    /// <summary>
    /// Desde la v0.10: el Cadete tiene tutor (nombre y teléfono del tutor); el Activo (adulto) solo su teléfono.
    /// Los datos del otro caso no se borran de la base, solo no se muestran.
    /// </summary>
    public bool TieneTutor => TipoSocio == TipoSocio.Cadete;

    /// <summary>Nombre del tutor (solo Cadete).</summary>
    public string? Tutor => TieneTutor && !string.IsNullOrWhiteSpace(ResponsableNombre) ? ResponsableNombre!.Trim() : null;

    /// <summary>Teléfono al que conviene escribir: Cadete → el del tutor; Activo → el del alumno.
    /// Si ese está vacío, usa el otro que haya (datos cargados antes de la v0.10).</summary>
    public string? CelularContacto
    {
        get
        {
            var (primero, segundo) = TieneTutor ? (ResponsableCelular, Celular) : (Celular, ResponsableCelular);
            if (!string.IsNullOrWhiteSpace(primero)) return primero.Trim();
            return string.IsNullOrWhiteSpace(segundo) ? null : segundo.Trim();
        }
    }

    /// <summary>Para listas: "3764 123456 (tutor: Ana Gómez)" en Cadete, "3764 123456" en Activo.</summary>
    public string ContactoTexto
    {
        get
        {
            var tel = CelularContacto ?? "";
            if (Tutor is not { } t) return tel;
            return tel.Length == 0 ? $"Tutor: {t}" : $"{tel} (tutor: {t})";
        }
    }
}

/// <summary>Un período en que el alumno no vino: desde la baja hasta el día en que volvió.</summary>
public record Ausencia(DateOnly FechaBaja, DateOnly FechaRegreso);
