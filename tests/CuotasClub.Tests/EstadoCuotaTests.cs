using CuotasClub.Models;
using CuotasClub.Services;

namespace CuotasClub.Tests;

public class EstadoCuotaTests
{
    private static readonly DateOnly Inicio = new(2027, 1, 1);

    private static Alumno Alumno(string alta, string? baja = null) => new()
    {
        Id = 1, Nombre = "Ana", Apellido = "Gómez", Dni = "40111222",
        FechaAlta = DateOnly.Parse(alta), FechaBaja = baja is null ? null : DateOnly.Parse(baja)
    };

    private static Pago PagoDe(int anio, int mes) => new() { AlumnoId = 1, Anio = anio, Mes = mes, Monto = 20000, PorcentajeClub = 30 };

    [Fact]
    public void Mes_terminado_sin_pago_debe() =>
        Assert.Equal(EstadoCuota.Debe, CalculoCuotas.CalcularEstado(Alumno("2027-01-10"), 2027, 2, null, new DateOnly(2027, 3, 1), Inicio));

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(31)]
    public void Mes_en_curso_sin_pago_esta_pendiente_todo_el_mes(int dia) =>
        Assert.Equal(EstadoCuota.Pendiente, CalculoCuotas.CalcularEstado(Alumno("2027-01-10"), 2027, 3, null, new DateOnly(2027, 3, dia), Inicio));

    [Fact]
    public void Mes_futuro_esta_pendiente() =>
        Assert.Equal(EstadoCuota.Pendiente, CalculoCuotas.CalcularEstado(Alumno("2027-01-10"), 2027, 9, null, new DateOnly(2027, 3, 5), Inicio));

    [Fact]
    public void Con_pago_esta_pagado() =>
        Assert.Equal(EstadoCuota.Pagado, CalculoCuotas.CalcularEstado(Alumno("2027-01-10"), 2027, 2, PagoDe(2027, 2), new DateOnly(2027, 3, 1), Inicio));

    [Fact]
    public void Pago_anulado_no_cuenta()
    {
        var p = PagoDe(2027, 2); p.Anulado = true;
        Assert.Equal(EstadoCuota.Debe, CalculoCuotas.CalcularEstado(Alumno("2027-01-10"), 2027, 2, p, new DateOnly(2027, 3, 1), Inicio));
    }

    [Fact]
    public void Antes_del_alta_no_corresponde() =>
        Assert.Equal(EstadoCuota.NoCorresponde, CalculoCuotas.CalcularEstado(Alumno("2027-04-20"), 2027, 3, null, new DateOnly(2027, 6, 1), Inicio));

    [Fact]
    public void El_mes_del_alta_corresponde_aunque_entre_a_fin_de_mes() =>
        Assert.Equal(EstadoCuota.Debe, CalculoCuotas.CalcularEstado(Alumno("2027-04-28"), 2027, 4, null, new DateOnly(2027, 6, 1), Inicio));

    [Fact]
    public void El_mes_de_la_baja_no_corresponde() =>
        Assert.Equal(EstadoCuota.NoCorresponde, CalculoCuotas.CalcularEstado(Alumno("2027-01-10", "2027-06-03"), 2027, 6, null, new DateOnly(2027, 8, 1), Inicio));

    [Fact]
    public void El_mes_anterior_a_la_baja_si_corresponde() =>
        Assert.Equal(EstadoCuota.Debe, CalculoCuotas.CalcularEstado(Alumno("2027-01-10", "2027-06-03"), 2027, 5, null, new DateOnly(2027, 8, 1), Inicio));

    [Fact]
    public void Baja_el_primer_dia_del_mes_siguiente_cobra_el_mes_anterior_completo() =>
        Assert.Equal(EstadoCuota.Debe, CalculoCuotas.CalcularEstado(Alumno("2027-01-10", "2027-07-01"), 2027, 6, null, new DateOnly(2027, 8, 1), Inicio));

    [Fact]
    public void Baja_el_ultimo_dia_del_mes_cobra_ese_mes() =>
        Assert.Equal(EstadoCuota.Debe, CalculoCuotas.CalcularEstado(Alumno("2027-01-10", "2027-06-30"), 2027, 6, null, new DateOnly(2027, 8, 1), Inicio));

    [Fact]
    public void Baja_el_ultimo_dia_de_febrero_cobra_febrero() =>
        Assert.Equal(EstadoCuota.Debe, CalculoCuotas.CalcularEstado(Alumno("2027-01-10", "2027-02-28"), 2027, 2, null, new DateOnly(2027, 4, 1), Inicio));

    [Fact]
    public void Baja_el_penultimo_dia_no_cobra_ese_mes() =>
        Assert.Equal(EstadoCuota.NoCorresponde, CalculoCuotas.CalcularEstado(Alumno("2027-01-10", "2027-06-29"), 2027, 6, null, new DateOnly(2027, 8, 1), Inicio));

    [Fact]
    public void Despues_de_la_baja_no_corresponde() =>
        Assert.Equal(EstadoCuota.NoCorresponde, CalculoCuotas.CalcularEstado(Alumno("2027-01-10", "2027-06-03"), 2027, 7, null, new DateOnly(2027, 8, 1), Inicio));

    [Fact]
    public void Antes_del_inicio_del_sistema_no_corresponde_aunque_el_alta_sea_vieja() =>
        Assert.Equal(EstadoCuota.NoCorresponde, CalculoCuotas.CalcularEstado(Alumno("2024-03-01"), 2026, 11, null, new DateOnly(2027, 2, 1), Inicio));

    [Fact]
    public void Activo_hasta_el_mes_de_la_baja_solo_si_es_a_fin_de_mes()
    {
        Assert.False(Alumno("2027-01-10", "2027-06-20").EstaActivoEn(2027, 6));
        Assert.True(Alumno("2027-01-10", "2027-06-30").EstaActivoEn(2027, 6));
        Assert.False(Alumno("2027-01-10", "2027-06-30").EstaActivoEn(2027, 7));
    }
}
