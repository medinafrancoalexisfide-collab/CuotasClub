using System.Globalization;
using CuotasClub.Data;
using CuotasClub.Repositories;

namespace CuotasClub.Services;

/// <summary>Valores de configuración con tipo.</summary>
public class ConfiguracionService(Database db)
{
    private readonly ConfiguracionRepository _repo = new(db);

    private readonly PorcentajeClubRepository _porcentajes = new(db);

    private readonly PrecioCuotaRepository _precios = new(db);

    /// <summary>
    /// % del club que rige este mes (solo lectura). Para cambiarlo usar <see cref="GuardarPorcentajeClub"/>,
    /// que lo guarda con vigencia desde un mes y no altera los meses anteriores.
    /// </summary>
    public decimal PorcentajeClub
    {
        get
        {
            var hoy = DateTime.Today;
            return _porcentajes.ObtenerVigente(hoy.Year, hoy.Month);
        }
    }

    public decimal PorcentajeClubVigente(int anio, int mes) => _porcentajes.ObtenerVigente(anio, mes);

    public void GuardarPorcentajeClub(int desdeAnio, int desdeMes, decimal porcentaje) => _porcentajes.Guardar(desdeAnio, desdeMes, porcentaje);

    /// <summary>
    /// Primer mes que se cobra con el programa (día 1). Los meses anteriores son "—" aunque el alumno
    /// tenga una fecha de alta más vieja, así se puede cargar la fecha real de ingreso al taller.
    /// </summary>
    public DateOnly InicioCobros
    {
        get => DateOnly.TryParseExact(_repo.Obtener(ClavesConfiguracion.InicioCobros) + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d : new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
        set => _repo.Guardar(ClavesConfiguracion.InicioCobros, value.ToString("yyyy-MM", CultureInfo.InvariantCulture));
    }

    /// <summary>Nombre del club o institución. Vacío hasta que se carga en el primer uso o en Configuración.</summary>
    public string NombreClub
    {
        get => _repo.Obtener(ClavesConfiguracion.NombreClub)?.Trim() ?? "";
        set => _repo.Guardar(ClavesConfiguracion.NombreClub, value?.Trim() ?? "");
    }

    /// <summary>Nombre del taller o actividad (por ejemplo "Taller de Ajedrez").</summary>
    public string NombreTaller
    {
        get => _repo.Obtener(ClavesConfiguracion.NombreTaller)?.Trim() ?? "";
        set => _repo.Guardar(ClavesConfiguracion.NombreTaller, value?.Trim() ?? "");
    }

    /// <summary>Nombre del profesor (opcional). Si está cargado, sale en el recibo.</summary>
    public string NombreProfesor
    {
        get => _repo.Obtener(ClavesConfiguracion.NombreProfesor)?.Trim() ?? "";
        set => _repo.Guardar(ClavesConfiguracion.NombreProfesor, value?.Trim() ?? "");
    }

    /// <summary>Título para pantallas y recibos: el taller, o el club si no hay taller, o "CuotasClub".</summary>
    public string TituloTaller =>
        NombreTaller is { Length: > 0 } t ? t : NombreClub is { Length: > 0 } c ? c : "CuotasClub";

    /// <summary>
    /// La base todavía no tiene precio de cuota o % del club: hay que mostrar el asistente de primer uso.
    /// </summary>
    public bool FaltaConfiguracionInicial => _precios.Listar().Count == 0 || _porcentajes.Listar().Count == 0;

    /// <summary>
    /// Guarda lo que pide el asistente de primer uso: nombres, inicio de cobros, y precio y % vigentes desde ese mes.
    /// </summary>
    public void GuardarConfiguracionInicial(string nombreClub, string nombreTaller, string nombreProfesor,
        decimal precio, decimal porcentajeClub, DateOnly inicioCobros)
    {
        if (string.IsNullOrWhiteSpace(nombreClub) && string.IsNullOrWhiteSpace(nombreTaller))
            throw new ReglaNegocioException("Escribí el nombre del club o del taller.");
        if (precio <= 0) throw new ReglaNegocioException("El precio de la cuota tiene que ser mayor a cero.");
        if (porcentajeClub is < 0 or > 100) throw new ReglaNegocioException("El porcentaje del club tiene que estar entre 0 y 100.");

        NombreClub = nombreClub;
        NombreTaller = nombreTaller;
        NombreProfesor = nombreProfesor;
        InicioCobros = new DateOnly(inicioCobros.Year, inicioCobros.Month, 1);
        _precios.Guardar(inicioCobros.Year, inicioCobros.Month, precio);
        _porcentajes.Guardar(inicioCobros.Year, inicioCobros.Month, porcentajeClub);
    }

    public string? RutaCredenciales
    {
        get => _repo.Obtener(ClavesConfiguracion.RutaCredenciales);
        set => _repo.Guardar(ClavesConfiguracion.RutaCredenciales, string.IsNullOrWhiteSpace(value) ? null : value.Trim());
    }

    /// <summary>Nombre del archivo de credenciales que se busca junto al programa si no se eligió otro.</summary>
    public const string ArchivoCredencialesPorDefecto = "credenciales-google.json";

    /// <summary>Documentos\CuotasClub: ahí van los recibos, las copias y, si no se elige otro, el archivo de credenciales.</summary>
    public static string CarpetaDocumentos =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CuotasClub");

    /// <summary>
    /// Ruta completa del archivo de credenciales que se usa: la cargada en Configuración, o credenciales-google.json.
    /// Un nombre sin carpeta se busca en Documentos\CuotasClub y, si no está, junto al programa (como hasta la v0.13).
    /// </summary>
    public string RutaCredencialesEfectiva
    {
        get
        {
            var ruta = string.IsNullOrWhiteSpace(RutaCredenciales) ? ArchivoCredencialesPorDefecto : RutaCredenciales;
            if (Path.IsPathRooted(ruta)) return ruta;
            var enDocumentos = Path.Combine(CarpetaDocumentos, ruta);
            var juntoAlPrograma = Path.Combine(AppContext.BaseDirectory, ruta);
            return !File.Exists(enDocumentos) && File.Exists(juntoAlPrograma) ? juntoAlPrograma : enDocumentos;
        }
    }

    /// <summary>ID de la planilla. Al guardar acepta también el enlace completo y se queda con el ID.</summary>
    public string? IdPlanillaTesoreria
    {
        get => _repo.Obtener(ClavesConfiguracion.IdPlanillaTesoreria);
        set => _repo.Guardar(ClavesConfiguracion.IdPlanillaTesoreria, PlanillaTesoreria.ExtraerIdPlanilla(value));
    }

    /// <summary>Fecha y hora local de la última sincronización exitosa con Google Sheets.</summary>
    public DateTime? UltimaSincronizacion
    {
        get => DateTime.TryParse(_repo.Obtener(ClavesConfiguracion.UltimaSincronizacion), CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        set => _repo.Guardar(ClavesConfiguracion.UltimaSincronizacion, value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
    }

    /// <summary>Hay cambios que todavía no llegaron a la planilla del tesorero.</summary>
    public bool SincronizacionPendiente
    {
        get => _repo.Obtener(ClavesConfiguracion.SincronizacionPendiente) == "1";
        set
        {
            _repo.Guardar(ClavesConfiguracion.SincronizacionPendiente, value ? "1" : "0");
            if (value) _repo.Guardar(ClavesConfiguracion.UltimoCambio, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// Quita la marca de pendiente, salvo que haya habido un cambio después de <paramref name="inicioUtc"/>
    /// (un pago registrado mientras se subía la planilla queda para la próxima sincronización).
    /// </summary>
    public void MarcarSincronizado(DateTime inicioUtc)
    {
        var ultimo = long.TryParse(_repo.Obtener(ClavesConfiguracion.UltimoCambio), out var t) ? t : 0;
        if (ultimo < inicioUtc.Ticks) _repo.Guardar(ClavesConfiguracion.SincronizacionPendiente, "0");
    }

    /// <summary>Google Sheets está configurado (hay ID de planilla y el archivo de credenciales existe).</summary>
    public bool SheetsConfigurado =>
        !string.IsNullOrWhiteSpace(IdPlanillaTesoreria) && File.Exists(RutaCredencialesEfectiva);
}
