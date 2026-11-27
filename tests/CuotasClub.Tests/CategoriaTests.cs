using CuotasClub.Services;

namespace CuotasClub.Tests;

public class CategoriaTests
{
    [Fact]
    public void Ejemplo_del_documento_nacido_2016_en_2027_es_Sub12() =>
        Assert.Equal("Sub-12", CategoriaService.CalcularCategoria(new DateOnly(2016, 12, 31), 2027));

    [Theory]
    [InlineData(2020, "Sub-8")]   // 7
    [InlineData(2019, "Sub-10")]  // 8
    [InlineData(2018, "Sub-10")]  // 9
    [InlineData(2017, "Sub-12")]  // 10
    [InlineData(2015, "Sub-14")]  // 12
    [InlineData(2013, "Sub-16")]  // 14
    [InlineData(2011, "Sub-18")]  // 16
    [InlineData(2010, "Sub-18")]  // 17
    [InlineData(2009, "Mayor")]   // 18
    [InlineData(1980, "Mayor")]
    public void Categoria_por_edad_al_31_de_diciembre(int anioNacimiento, string esperada) =>
        Assert.Equal(esperada, CategoriaService.CalcularCategoria(new DateOnly(anioNacimiento, 6, 15), 2027));

    [Fact]
    public void La_categoria_no_cambia_el_dia_del_cumpleanios() =>
        Assert.Equal(CategoriaService.CalcularCategoria(new DateOnly(2016, 12, 31), new DateOnly(2027, 1, 2)),
                     CategoriaService.CalcularCategoria(new DateOnly(2016, 1, 1), new DateOnly(2027, 1, 2)));

    [Theory]
    [InlineData("2016-05-10", "2027-05-09", 10)]
    [InlineData("2016-05-10", "2027-05-10", 11)]
    [InlineData("2016-02-29", "2027-02-27", 10)]
    [InlineData("2016-02-29", "2027-03-01", 11)]
    public void Edad_cumplida(string nac, string hoy, int esperada) =>
        Assert.Equal(esperada, CategoriaService.CalcularEdad(DateOnly.Parse(nac), DateOnly.Parse(hoy)));
}
