using CuotasClub.Services;

namespace CuotasClub.Tests;

public class FormatoTests
{
    [Theory]
    [InlineData(20000, "$ 20.000")]
    [InlineData(240000, "$ 240.000")]
    [InlineData(1250.5, "$ 1.250,50")]
    [InlineData(0, "$ 0")]
    [InlineData(-3000, "-$ 3.000")]
    [InlineData(1234567, "$ 1.234.567")]
    public void Moneda_formato_argentino(double monto, string esperado) => Assert.Equal(esperado, Formato.Moneda((decimal)monto));

    [Theory]
    [InlineData("20000", 20000)]
    [InlineData("20.000", 20000)]
    [InlineData("$ 20.000", 20000)]
    [InlineData("1250,50", 1250.5)]
    public void Parse_moneda(string texto, double esperado)
    {
        Assert.True(Formato.TryParseMoneda(texto, out var m));
        Assert.Equal((decimal)esperado, m);
    }

    [Fact]
    public void Validaciones_de_dni()
    {
        Assert.True(AlumnoValidador.DniValido("1234567"));
        Assert.True(AlumnoValidador.DniValido("12345678"));
        Assert.False(AlumnoValidador.DniValido("123456"));
        Assert.False(AlumnoValidador.DniValido("123456789"));
        Assert.False(AlumnoValidador.DniValido("12.345.678"));
        Assert.Equal("12345678", AlumnoValidador.NormalizarDni("12.345.678"));
    }
}
