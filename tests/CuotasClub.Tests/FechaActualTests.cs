using CuotasClub.Data;
using CuotasClub.Repositories;
using CuotasClub.Services;
using Microsoft.Data.Sqlite;

namespace CuotasClub.Tests;

/// <summary>El programa arranca en el mes actual, no en una fecha fija.</summary>
public class FechaActualTests : IDisposable
{
    private readonly string _ruta = Path.Combine(Path.GetTempPath(), $"cuotasclub-test-{Guid.NewGuid():N}.db");

    [Fact]
    public void Base_nueva_arranca_vacia_y_pide_configuracion()
    {
        var db = new Database(_ruta);
        db.Inicializar(new DateOnly(2026, 11, 25));
        var config = new ConfiguracionService(db);
        Assert.True(config.FaltaConfiguracionInicial);
        Assert.Equal(new DateOnly(2026, 11, 1), config.InicioCobros);
        Assert.Empty(new PrecioCuotaRepository(db).Listar());
        Assert.Empty(new PorcentajeClubRepository(db).Listar());
        Assert.Equal("", config.NombreClub);
        Assert.Equal("", config.NombreTaller);
        Assert.Equal("CuotasClub", config.TituloTaller);

        // Si se cierra el asistente sin terminar, al volver a abrir sigue vacía (no aparecen nombres de otro club).
        db.Inicializar(new DateOnly(2026, 11, 26));
        Assert.True(config.FaltaConfiguracionInicial);
        Assert.Equal("", config.NombreClub);
    }

    [Fact]
    public void Asistente_de_primer_uso_guarda_todo_desde_el_inicio_de_cobros()
    {
        var db = new Database(_ruta);
        db.Inicializar(new DateOnly(2026, 11, 25));
        var config = new ConfiguracionService(db);
        config.GuardarConfiguracionInicial(" Club Atlético ", "Taller de Ajedrez", "Ana López", 18000m, 25m, new DateOnly(2027, 3, 15));

        Assert.False(config.FaltaConfiguracionInicial);
        Assert.Equal("Club Atlético", config.NombreClub);
        Assert.Equal("Taller de Ajedrez", config.TituloTaller);
        Assert.Equal("Ana López", config.NombreProfesor);
        Assert.Equal(new DateOnly(2027, 3, 1), config.InicioCobros);
        Assert.Null(new PrecioCuotaRepository(db).ObtenerVigente(2027, 2));
        Assert.Equal(18000m, new PrecioCuotaRepository(db).ObtenerVigente(2027, 3));
        Assert.Equal(25m, config.PorcentajeClubVigente(2027, 3));

        // Al volver a abrir no se pisa nada.
        db.Inicializar(new DateOnly(2027, 4, 1));
        Assert.Equal("Club Atlético", config.NombreClub);
        Assert.Equal(18000m, Assert.Single(new PrecioCuotaRepository(db).Listar()).Monto);
    }

    [Fact]
    public void Asistente_rechaza_datos_incompletos()
    {
        var db = new Database(_ruta);
        db.Inicializar(new DateOnly(2026, 11, 25));
        var config = new ConfiguracionService(db);
        Assert.Throws<ReglaNegocioException>(() => config.GuardarConfiguracionInicial("", " ", "", 20000m, 30m, new DateOnly(2026, 11, 1)));
        Assert.Throws<ReglaNegocioException>(() => config.GuardarConfiguracionInicial("Club", "", "", 0m, 30m, new DateOnly(2026, 11, 1)));
        Assert.Throws<ReglaNegocioException>(() => config.GuardarConfiguracionInicial("Club", "", "", 20000m, 120m, new DateOnly(2026, 11, 1)));
        Assert.True(config.FaltaConfiguracionInicial);
    }

    [Fact]
    public void Base_existente_con_datos_conserva_todo_y_recibe_los_nombres_de_antes()
    {
        // Base como la creaba la 0.7: precio y % cargados solos, sin nombres guardados.
        var db = new Database(_ruta);
        db.Inicializar(new DateOnly(2026, 11, 25));
        new PrecioCuotaRepository(db).Guardar(2026, 11, 20000m);
        new PorcentajeClubRepository(db).Guardar(2026, 11, 30m);

        db.Inicializar(new DateOnly(2026, 11, 26));
        var config = new ConfiguracionService(db);
        Assert.False(config.FaltaConfiguracionInicial);
        Assert.Equal("Cuotas Club", config.NombreClub);
        Assert.Equal("Taller de Ajedrez", config.NombreTaller);
        Assert.Equal(20000m, new PrecioCuotaRepository(db).ObtenerVigente(2026, 11));
        Assert.Equal(30m, config.PorcentajeClubVigente(2026, 11));

        // Si después los cambia, no se vuelven a pisar.
        config.NombreClub = "Otro club";
        db.Inicializar(new DateOnly(2026, 11, 27));
        Assert.Equal("Otro club", config.NombreClub);
    }

    [Fact]
    public void Base_vieja_con_arranque_2027_y_sin_pagos_pasa_al_mes_actual()
    {
        CrearBaseVersionVieja();
        var db = new Database(_ruta);
        db.Inicializar(new DateOnly(2026, 11, 25));
        Assert.Equal(new DateOnly(2026, 11, 1), new ConfiguracionService(db).InicioCobros);
        var precio = Assert.Single(new PrecioCuotaRepository(db).Listar());
        Assert.Equal((2026, 11), (precio.VigenteDesdeAnio, precio.VigenteDesdeMes));
        Assert.Equal(30m, new ConfiguracionService(db).PorcentajeClubVigente(2026, 11));

        // Si después se elige enero 2027 a mano, no se vuelve a cambiar al abrir.
        new ConfiguracionService(db).InicioCobros = new DateOnly(2027, 1, 1);
        db.Inicializar(new DateOnly(2026, 12, 2));
        Assert.Equal(new DateOnly(2027, 1, 1), new ConfiguracionService(db).InicioCobros);
    }

    /// <summary>Base como la creaban las versiones 0.1 a 0.4: todo desde enero 2027 y sin la marca de revisión.</summary>
    private void CrearBaseVersionVieja()
    {
        var db = new Database(_ruta);
        db.Inicializar(new DateOnly(2027, 1, 1));
        new PrecioCuotaRepository(db).Guardar(2027, 1, 20000m);
        new PorcentajeClubRepository(db).Guardar(2027, 1, 30m);
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"DELETE FROM Configuracion WHERE Clave = '{ClavesConfiguracion.ArranqueRevisado}'";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { File.Delete(_ruta); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }
}
