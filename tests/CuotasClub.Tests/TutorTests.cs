using CuotasClub.Models;
using CuotasClub.Repositories;
using CuotasClub.Services;

namespace CuotasClub.Tests;

/// <summary>v0.10: Cadete → tutor y teléfono del tutor; Activo → solo su teléfono.</summary>
public class TutorTests : BaseTemporal
{
    [Fact]
    public void Cadete_usa_el_telefono_y_el_nombre_del_tutor()
    {
        var a = NuevoAlumno();
        a.TipoSocio = TipoSocio.Cadete;
        a.ResponsableNombre = "Ana Gómez";
        a.ResponsableCelular = "3764 111111";
        a.Celular = "3764 999999";
        Assert.Equal("Ana Gómez", a.Tutor);
        Assert.Equal("3764 111111", a.CelularContacto);
        Assert.Equal("3764 111111 (tutor: Ana Gómez)", a.ContactoTexto);
    }

    [Fact]
    public void Activo_usa_su_telefono_y_no_tiene_tutor()
    {
        var a = NuevoAlumno();
        a.ResponsableNombre = "Ana Gómez";
        a.ResponsableCelular = "3764 111111";
        a.Celular = "3764 999999";
        Assert.Null(a.Tutor);
        Assert.Equal("3764 999999", a.CelularContacto);
        Assert.Equal("3764 999999", a.ContactoTexto);
    }

    [Fact]
    public void Si_falta_el_telefono_que_corresponde_usa_el_otro()
    {
        var a = NuevoAlumno();
        a.ResponsableCelular = "3764 111111";
        Assert.Equal("3764 111111", a.CelularContacto);
    }

    [Fact]
    public void Solo_se_valida_el_telefono_que_se_muestra()
    {
        var activo = NuevoAlumno();
        activo.ResponsableCelular = "abc";
        Assert.Empty(AlumnoValidador.Validar(activo, new DateOnly(2027, 1, 1)));
        activo.Celular = "12";
        Assert.Contains("El teléfono no es válido (solo números, con característica).", AlumnoValidador.Validar(activo, new DateOnly(2027, 1, 1)));

        var cadete = NuevoAlumno();
        cadete.TipoSocio = TipoSocio.Cadete;
        cadete.ResponsableCelular = "12";
        Assert.Contains("El teléfono del tutor no es válido (solo números, con característica).", AlumnoValidador.Validar(cadete, new DateOnly(2027, 1, 1)));
    }

    [Fact]
    public void Al_abrir_una_base_vieja_copia_el_telefono_al_campo_que_se_ve_sin_borrar_nada()
    {
        var s = new AlumnoService(Db);
        var adulto = NuevoAlumno("Adulto", dni: "30000001");
        var chico = NuevoAlumno("Chico", dni: "30000002");
        chico.TipoSocio = TipoSocio.Cadete;
        s.Guardar(adulto, new DateOnly(2027, 1, 1));
        s.Guardar(chico, new DateOnly(2027, 1, 1));

        // Como quedaban cargados antes: el adulto con el celular en "responsable" y el chico en el propio.
        using (var cn = Db.AbrirConexion())
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = """
                UPDATE Alumnos SET Celular = NULL, ResponsableNombre = 'Pepe', ResponsableCelular = '3764 111111' WHERE Dni = '30000001';
                UPDATE Alumnos SET Celular = '3764 222222', ResponsableNombre = 'Mamá' WHERE Dni = '30000002';
                """;
            cmd.ExecuteNonQuery();
        }

        Db.Inicializar(new DateOnly(2027, 1, 1));
        var repo = new AlumnoRepository(Db);
        var a = repo.ObtenerPorId(adulto.Id)!;
        var c = repo.ObtenerPorId(chico.Id)!;

        Assert.Equal("3764 111111", a.Celular);
        Assert.Equal("Pepe", a.ResponsableNombre); // queda guardado, aunque no se muestre
        Assert.Equal("3764 222222", c.ResponsableCelular);
        Assert.Equal("3764 222222", c.Celular);
        Assert.Equal("Mamá", c.Tutor);
    }
}
