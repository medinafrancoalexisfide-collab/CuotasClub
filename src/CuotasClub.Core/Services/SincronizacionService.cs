using System.Text.Json;
using CuotasClub.Data;
using CuotasClub.Repositories;

namespace CuotasClub.Services;

/// <summary>Donde se escribe la planilla del tesorero. La implementación real (Google Sheets) está en el proyecto de Windows.</summary>
public interface IDestinoPlanilla
{
    /// <summary>
    /// Escribe cada pestaña (la crea si no existe, la limpia y la vuelve a llenar).
    /// Ante un problema conocido lanza <see cref="SincronizacionException"/> con un mensaje para mostrar.
    /// </summary>
    Task EscribirAsync(string idPlanilla, IReadOnlyList<PestanaPlanilla> pestanas, CancellationToken ct = default);
}

/// <summary>Error de sincronización con un mensaje claro para el usuario.</summary>
public class SincronizacionException(string mensaje, Exception? interna = null) : Exception(mensaje, interna);

public enum EstadoSincronizacion { Ok, NoConfigurado, Error }

public sealed record ResultadoSincronizacion(EstadoSincronizacion Estado, string Mensaje)
{
    public bool Ok => Estado == EstadoSincronizacion.Ok;
}

/// <summary>
/// Copia a la planilla del tesorero el resumen de cada año: alumnos que pagaron, total ingresado y parte del club.
/// La base SQLite es la fuente de verdad; la planilla se sobrescribe completa en cada sincronización.
/// Nunca lanza excepciones: si no hay internet o Google no está configurado, queda "pendiente de sincronizar"
/// y se reintenta en la próxima.
/// </summary>
public class SincronizacionService(Database db, Func<string, IDestinoPlanilla> crearDestino)
{
    private readonly ConfiguracionService _config = new(db);
    private readonly ResumenService _resumen = new(db);
    private readonly PagoRepository _pagos = new(db);

    public async Task<ResultadoSincronizacion> SincronizarAsync(DateTime? ahora = null, CancellationToken ct = default)
    {
        var momento = ahora ?? DateTime.Now;
        if (!_config.SheetsConfigurado)
            return new(EstadoSincronizacion.NoConfigurado, MotivoNoConfigurado());

        // Marca de tiempo antes de leer los datos: si se registra un pago mientras se sube,
        // la sincronización queda pendiente para la próxima vez.
        var inicio = DateTime.UtcNow;
        try
        {
            var pestanas = ArmarPestanas(momento);
            var destino = crearDestino(_config.RutaCredencialesEfectiva);
            await destino.EscribirAsync(_config.IdPlanillaTesoreria!, pestanas, ct).ConfigureAwait(false);
            _config.UltimaSincronizacion = momento;
            _config.MarcarSincronizado(inicio);
            return new(EstadoSincronizacion.Ok, "Planilla del tesorero actualizada.");
        }
        catch (Exception ex)
        {
            _config.SincronizacionPendiente = true;
            return new(EstadoSincronizacion.Error, MensajeDeError(ex));
        }
    }

    /// <summary>
    /// Una pestaña por año, desde el año de inicio de cobros hasta el actual (o el último año con pagos).
    /// Siempre al menos la del año de inicio: antes de empezar a cobrar (por ejemplo en 2026 con inicio 2027)
    /// se crea la pestaña 2027 con los meses vacíos.
    /// </summary>
    public List<PestanaPlanilla> ArmarPestanas(DateTime ahora)
    {
        var hoy = DateOnly.FromDateTime(ahora);
        int desde = _config.InicioCobros.Year;
        int hasta = Math.Max(Math.Max(desde, hoy.Year), _pagos.ListarNoAnulados().Select(p => p.Anio).DefaultIfEmpty(desde).Max());
        return Enumerable.Range(desde, hasta - desde + 1)
            .Select(anio => PlanillaTesoreria.ArmarPestana(_resumen.ObtenerResumenAnual(anio, hoy), ahora, _config.InicioCobros))
            .OrderByDescending(p => p.Nombre, StringComparer.Ordinal) // el año más nuevo, primera pestaña
            .ToList();
    }

    private string MotivoNoConfigurado()
    {
        if (string.IsNullOrWhiteSpace(_config.IdPlanillaTesoreria))
            return "Falta cargar la planilla del tesorero en Configuración.";
        return $"No se encontró el archivo de credenciales ({_config.RutaCredencialesEfectiva}).";
    }

    private static string MensajeDeError(Exception ex) => ex switch
    {
        SincronizacionException s => s.Message,
        HttpRequestException or TaskCanceledException or TimeoutException =>
            "No hay conexión con Google (¿sin internet?). Los datos quedaron guardados y se van a subir en la próxima sincronización.",
        _ => "No se pudo actualizar la planilla: " + ex.Message,
    };

    /// <summary>Email de la cuenta de servicio (client_email) del archivo de credenciales, o null si no se puede leer.</summary>
    public static string? LeerEmailCuentaServicio(string? rutaCredenciales)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rutaCredenciales) || !File.Exists(rutaCredenciales)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(rutaCredenciales));
            return doc.RootElement.TryGetProperty("client_email", out var e) ? e.GetString() : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
