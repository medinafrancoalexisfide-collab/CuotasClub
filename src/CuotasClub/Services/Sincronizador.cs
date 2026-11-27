using CuotasClub.Data;

namespace CuotasClub.Services;

/// <summary>
/// Punto único para sincronizar con la planilla del tesorero desde las pantallas.
/// Corre en segundo plano (no congela la pantalla) y nunca dos sincronizaciones a la vez.
/// </summary>
internal static class Sincronizador
{
    private static readonly SemaphoreSlim Candado = new(1, 1);
    private static Database? _db;

    /// <summary>Se dispara al empezar y al terminar una sincronización (puede venir de otro hilo: usar BeginInvoke).</summary>
    public static event Action? EstadoCambio;

    public static bool EnCurso { get; private set; }

    /// <summary>Resultado de la última sincronización de esta sesión (null si todavía no hubo).</summary>
    public static ResultadoSincronizacion? UltimoResultado { get; private set; }

    public static void Inicializar(Database db) => _db = db;

    /// <summary>Sincroniza sin esperar el resultado (después de registrar o anular un pago).</summary>
    public static void EnSegundoPlano() => _ = SincronizarAsync();

    /// <summary>Sincroniza solo si hay cambios pendientes y Google está configurado (al abrir el programa).</summary>
    public static void SiHayPendientes()
    {
        if (_db is null) return;
        var config = new ConfiguracionService(_db);
        if (config.SincronizacionPendiente && config.SheetsConfigurado) EnSegundoPlano();
    }

    public static async Task<ResultadoSincronizacion> SincronizarAsync()
    {
        if (_db is null) return new(EstadoSincronizacion.NoConfigurado, "El programa no terminó de iniciar.");
        await Candado.WaitAsync();
        try
        {
            EnCurso = true;
            Avisar();
            var servicio = new SincronizacionService(_db, ruta => new GoogleSheetsDestino(ruta));
            // Task.Run: la lectura de SQLite y el armado también quedan fuera del hilo de la pantalla.
            UltimoResultado = await Task.Run(() => servicio.SincronizarAsync());
            return UltimoResultado;
        }
        finally
        {
            EnCurso = false;
            Candado.Release();
            Avisar();
        }
    }

    /// <summary>Texto corto para la barra de estado de la pantalla principal.</summary>
    public static string TextoEstado()
    {
        if (_db is null) return "";
        if (EnCurso) return "Planilla del tesorero: sincronizando…";
        var config = new ConfiguracionService(_db);
        if (!config.SheetsConfigurado) return "Planilla del tesorero: sin configurar (ver Configuración).";
        var ultima = config.UltimaSincronizacion is { } u ? u.ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture) : "nunca";
        return config.SincronizacionPendiente
            ? $"Planilla del tesorero: pendiente de sincronizar (última: {ultima})."
            : $"Planilla del tesorero: última sincronización {ultima}.";
    }

    private static void Avisar()
    {
        try { EstadoCambio?.Invoke(); }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }
}
