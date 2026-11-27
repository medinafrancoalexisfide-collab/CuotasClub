using CuotasClub.Services;

namespace CuotasClub.Tests;

public class RutasTests
{
    [Theory]
    [InlineData(@"C:\Users\Profe\Documents\CuotasClub\Recibos", @"c:\users\profe\documents\cuotasclub\recibos")]
    [InlineData(@"C:\Users\Profe\Documents\CuotasClub\Recibos\", @"C:\Users\Profe\Documents\CuotasClub\Recibos")]
    [InlineData(@"C:\", @"c:\")]
    public void MismaCarpeta_ignoraMayusculasYBarraFinal(string a, string b) => Assert.True(Rutas.MismaCarpeta(a, b));

    [Theory]
    [InlineData(@"C:\CuotasClub\Recibos", @"C:\CuotasClub\Copias")]
    [InlineData(@"C:\CuotasClub\Recibos", @"C:\CuotasClub\Recibos2")]
    [InlineData(@"C:\CuotasClub\Recibos", "")]
    [InlineData(null, @"C:\CuotasClub\Recibos")]
    public void MismaCarpeta_distintas(string? a, string? b) => Assert.False(Rutas.MismaCarpeta(a, b));
}
