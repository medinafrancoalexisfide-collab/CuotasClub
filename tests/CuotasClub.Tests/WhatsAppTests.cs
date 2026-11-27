using CuotasClub.Models;
using CuotasClub.Services;

namespace CuotasClub.Tests;

public class WhatsAppTests
{
    [Theory]
    [InlineData("3764 123456", "5493764123456")]
    [InlineData("3764 15-123456", "5493764123456")]
    [InlineData("03764 15 123456", "5493764123456")]
    [InlineData("+54 9 3764 123456", "5493764123456")]
    [InlineData("+54 3764 123456", "5493764123456")]
    [InlineData("0054 9 3764 123456", "5493764123456")]
    [InlineData("11 1512345678", "5491112345678")]
    [InlineData("(011) 15-1234-5678", "5491112345678")]
    [InlineData("351 15 1234567", "5493511234567")]
    [InlineData("5493764123456", "5493764123456")]
    public void NormalizarNumero_formatoArgentino(string entrada, string esperado) =>
        Assert.Equal(esperado, WhatsApp.NormalizarNumero(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456")]      // sin código de área
    [InlineData("15 123456")]
    public void NormalizarNumero_incompleto(string? entrada) => Assert.Null(WhatsApp.NormalizarNumero(entrada));

    private static Alumno Cadete() => new()
    {
        Nombre = "Juan", Apellido = "Pérez", TipoSocio = TipoSocio.Cadete,
        ResponsableNombre = "Ana Gómez", ResponsableCelular = "3764 123456", Celular = "3764 999999",
    };

    private static Alumno Activo() => new() { Nombre = "Laura", Apellido = "Díaz", TipoSocio = TipoSocio.Activo, Celular = "3764 555555" };

    [Fact]
    public void Recibo_cadete_leEscribeAlTutor()
    {
        var a = Cadete();
        Assert.Equal("5493764123456", WhatsApp.NormalizarNumero(a.CelularContacto));
        Assert.Equal("¡Hola Ana! Te paso el recibo de la cuota de noviembre 2026 de Juan. ¡Gracias!",
            WhatsApp.MensajeRecibo(a, new Pago { Anio = 2026, Mes = 11 }));
    }

    [Fact]
    public void Recibo_activo_leEscribeAlAlumno() =>
        Assert.Equal("¡Hola Laura! Te paso el recibo de tu cuota de marzo 2027. ¡Gracias!",
            WhatsApp.MensajeRecibo(Activo(), new Pago { Anio = 2027, Mes = 3 }));

    [Fact]
    public void Recordatorio_variosMesesYAnios()
    {
        var d = new Deudor { Alumno = Cadete(), MesesAdeudados = [(2026, 11), (2026, 12), (2027, 1)], MontoAdeudado = 60000 };
        Assert.Equal("¡Hola Ana! ¿Cómo estás? Te escribo para recordarte que quedaron pendientes las cuotas de " +
            "noviembre y diciembre 2026 y enero 2027 de Juan (en total $ 60.000). Cuando puedas, avisame. ¡Gracias!",
            WhatsApp.MensajeRecordatorio(d));
    }

    [Fact]
    public void Recordatorio_unMes_activo()
    {
        var d = new Deudor { Alumno = Activo(), MesesAdeudados = [(2026, 10)], MontoAdeudado = 20000 };
        Assert.Equal("¡Hola Laura! ¿Cómo estás? Te escribo para recordarte que quedó pendiente la cuota de " +
            "octubre 2026 ($ 20.000). Cuando puedas, avisame. ¡Gracias!", WhatsApp.MensajeRecordatorio(d));
    }

    [Fact]
    public void SinTelefono_avisa()
    {
        var a = Cadete();
        a.ResponsableCelular = null; a.Celular = null;
        Assert.Equal("No hay teléfono cargado del tutor de Juan. Cargalo en Alumnos.", WhatsApp.ProblemaConTelefono(a));
        a.ResponsableCelular = "123456";
        Assert.Contains("código de área", WhatsApp.ProblemaConTelefono(a));
        Assert.Null(WhatsApp.ProblemaConTelefono(Activo()));
    }

    [Fact]
    public void Enlace_codificaElMensaje() =>
        Assert.Equal("https://wa.me/5493764123456?text=%C2%A1Hola%20Ana%21", WhatsApp.Enlace("5493764123456", "¡Hola Ana!"));
}
