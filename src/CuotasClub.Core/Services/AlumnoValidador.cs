using CuotasClub.Models;

namespace CuotasClub.Services;

/// <summary>Validaciones de la ficha del alumno. Devuelve mensajes listos para mostrar.</summary>
public static class AlumnoValidador
{
    /// <summary>Valida campos (sin ir a la base). La unicidad del DNI la controla <see cref="AlumnoService"/>.</summary>
    public static List<string> Validar(Alumno a, DateOnly? hoy = null)
    {
        var h = hoy ?? DateOnly.FromDateTime(DateTime.Today);
        var errores = new List<string>();
        if (string.IsNullOrWhiteSpace(a.Nombre)) errores.Add("Falta el nombre.");
        if (string.IsNullOrWhiteSpace(a.Apellido)) errores.Add("Falta el apellido.");
        if (!DniValido(a.Dni)) errores.Add("El DNI tiene que tener 7 u 8 dígitos, sin puntos.");
        if (a.FechaNacimiento > h) errores.Add("La fecha de nacimiento no puede ser futura.");
        else if (a.FechaNacimiento < new DateOnly(1900, 1, 1)) errores.Add("La fecha de nacimiento no es válida.");
        if (a.FechaBaja is not null && a.FechaBaja < a.FechaAlta) errores.Add("La fecha de baja no puede ser anterior a la de alta.");
        if (!string.IsNullOrWhiteSpace(a.IdFide) && !a.IdFide.Trim().All(char.IsAsciiDigit)) errores.Add("El ID FIDE tiene que ser solo números.");
        // Solo se valida el teléfono que se muestra según el tipo de socio (el otro queda guardado, oculto).
        if (a.TieneTutor)
        {
            if (!CelularValido(a.ResponsableCelular)) errores.Add("El teléfono del tutor no es válido (solo números, con característica).");
        }
        else if (!CelularValido(a.Celular)) errores.Add("El teléfono no es válido (solo números, con característica).");
        return errores;
    }

    /// <summary>Saca puntos y espacios: "12.345.678" → "12345678".</summary>
    public static string NormalizarDni(string? dni) => new((dni ?? "").Where(char.IsAsciiDigit).ToArray());

    public static bool DniValido(string? dni)
    {
        var d = (dni ?? "").Trim();
        return d.Length is 7 or 8 && d.All(char.IsAsciiDigit);
    }

    /// <summary>Vacío es válido (es opcional). Si hay algo: entre 8 y 13 dígitos después de sacar espacios, guiones, paréntesis y "+".</summary>
    public static bool CelularValido(string? celular)
    {
        if (string.IsNullOrWhiteSpace(celular)) return true;
        var limpio = celular.Where(c => !" -()+".Contains(c)).ToArray();
        return limpio.Length is >= 8 and <= 13 && limpio.All(char.IsAsciiDigit);
    }
}
