using CuotasClub.Data;
using CuotasClub.Models;
using CuotasClub.Repositories;

namespace CuotasClub.Services;

/// <summary>Alta y edición de alumnos con todas las validaciones (campos + DNI único).</summary>
public class AlumnoService(Database db)
{
    private readonly AlumnoRepository _repo = new(db);

    /// <summary>Valida y guarda (inserta si Id = 0, si no actualiza). Lanza <see cref="ReglaNegocioException"/> con todos los errores juntos.</summary>
    public void Guardar(Alumno a, DateOnly? hoy = null)
    {
        a.Dni = AlumnoValidador.NormalizarDni(a.Dni);
        var errores = AlumnoValidador.Validar(a, hoy);
        if (AlumnoValidador.DniValido(a.Dni) && _repo.ExisteDni(a.Dni, a.Id == 0 ? null : a.Id))
            errores.Add("Ya hay un alumno con ese DNI.");
        if (errores.Count > 0) throw new ReglaNegocioException(string.Join(Environment.NewLine, errores));
        if (a.Id == 0) _repo.Insertar(a); else _repo.Actualizar(a);
    }
}
