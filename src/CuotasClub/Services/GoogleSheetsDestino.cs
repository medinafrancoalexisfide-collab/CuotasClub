using System.Net;
using CuotasClub.Services;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace CuotasClub.Services;

/// <summary>
/// Escribe la planilla del tesorero en Google Sheets con una cuenta de servicio (archivo JSON de credenciales).
/// Por cada año: crea la pestaña si no existe, la limpia, escribe los valores y da formato de moneda.
/// </summary>
public sealed class GoogleSheetsDestino(string rutaCredenciales) : IDestinoPlanilla
{
    private const string FormatoMoneda = "\"$\" #,##0";

    public async Task EscribirAsync(string idPlanilla, IReadOnlyList<PestanaPlanilla> pestanas, CancellationToken ct = default)
    {
        using var servicio = CrearServicio();
        try
        {
            var idsPestanas = await AsegurarPestanasAsync(servicio, idPlanilla, pestanas, ct);
            foreach (var p in pestanas)
            {
                var rango = $"'{p.Nombre}'";
                await servicio.Spreadsheets.Values.Clear(new ClearValuesRequest(), idPlanilla, rango).ExecuteAsync(ct);
                var update = servicio.Spreadsheets.Values.Update(new ValueRange { Values = p.Filas }, idPlanilla, rango + "!A1");
                update.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                await update.ExecuteAsync(ct);
            }
            var formatos = new List<Request>
            {
                // Planilla en español de Argentina: "$ 20.000" (punto de miles) y fechas dd/mm/aaaa.
                new() { UpdateSpreadsheetProperties = new UpdateSpreadsheetPropertiesRequest
                    { Properties = new SpreadsheetProperties { Locale = "es_AR" }, Fields = "locale" } }
            };
            formatos.AddRange(OrdenarYLimpiar(idsPestanas, pestanas));
            formatos.AddRange(pestanas.SelectMany(p => Formatear(idsPestanas[p.Nombre], p)));
            await servicio.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = formatos }, idPlanilla).ExecuteAsync(ct);
        }
        catch (GoogleApiException ex)
        {
            throw Traducir(ex);
        }
        catch (TokenResponseException ex)
        {
            throw new SincronizacionException(
                "Google rechazó las credenciales (puede que la clave se haya borrado en Google Cloud). " +
                "Creá una clave nueva, guardala como credenciales-google.json y volvé a probar.", ex);
        }
    }

    private SheetsService CrearServicio()
    {
        GoogleCredential credencial;
        try
        {
            credencial = CredentialFactory.FromFile<ServiceAccountCredential>(rutaCredenciales)
                .ToGoogleCredential()
                .CreateScoped(SheetsService.Scope.Spreadsheets);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new SincronizacionException(
                "El archivo de credenciales no es válido. Tiene que ser la clave JSON de una cuenta de servicio " +
                $"descargada de Google Cloud ({ex.Message}).", ex);
        }
        var servicio = new SheetsService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credencial,
            ApplicationName = "CuotasClub",
        });
        servicio.HttpClient.Timeout = TimeSpan.FromSeconds(30);
        return servicio;
    }

    /// <summary>Crea las pestañas que falten. Devuelve nombre → sheetId.</summary>
    private static async Task<Dictionary<string, int>> AsegurarPestanasAsync(SheetsService servicio, string idPlanilla,
        IReadOnlyList<PestanaPlanilla> pestanas, CancellationToken ct)
    {
        var get = servicio.Spreadsheets.Get(idPlanilla);
        get.Fields = "sheets.properties(sheetId,title)";
        var planilla = await get.ExecuteAsync(ct);
        var ids = planilla.Sheets.ToDictionary(s => s.Properties.Title, s => s.Properties.SheetId ?? 0);

        var faltan = pestanas.Where(p => !ids.ContainsKey(p.Nombre)).ToList();
        if (faltan.Count == 0) return ids;

        var req = new BatchUpdateSpreadsheetRequest
        {
            Requests = faltan.Select(p => new Request
            {
                AddSheet = new AddSheetRequest { Properties = new SheetProperties { Title = p.Nombre } }
            }).ToList()
        };
        var resp = await servicio.Spreadsheets.BatchUpdate(req, idPlanilla).ExecuteAsync(ct);
        foreach (var r in resp.Replies)
            if (r.AddSheet?.Properties is { } props) ids[props.Title] = props.SheetId ?? 0;
        return ids;
    }

    /// <summary>
    /// Pone las pestañas en el orden recibido (el año actual primero, así la planilla abre ahí)
    /// y borra pestañas de años que el programa ya no publica (por ejemplo si cambió el inicio de cobros).
    /// Las pestañas que no son un año (como "Hoja 1") no se tocan.
    /// </summary>
    private static IEnumerable<Request> OrdenarYLimpiar(Dictionary<string, int> ids, IReadOnlyList<PestanaPlanilla> pestanas)
    {
        var publicadas = pestanas.Select(p => p.Nombre).ToHashSet();
        foreach (var (titulo, id) in ids)
            if (!publicadas.Contains(titulo) && System.Text.RegularExpressions.Regex.IsMatch(titulo, @"^\d{4}$"))
                yield return new Request { DeleteSheet = new DeleteSheetRequest { SheetId = id } };
        for (int i = 0; i < pestanas.Count; i++)
            yield return new Request
            {
                UpdateSheetProperties = new UpdateSheetPropertiesRequest
                {
                    Properties = new SheetProperties { SheetId = ids[pestanas[i].Nombre], Index = i },
                    Fields = "index"
                }
            };
    }

    private static IEnumerable<Request> Formatear(int sheetId, PestanaPlanilla p)
    {
        GridRange Rango(int fila, int filaHasta, int col, int colHasta) => new()
        {
            SheetId = sheetId, StartRowIndex = fila, EndRowIndex = filaHasta, StartColumnIndex = col, EndColumnIndex = colHasta
        };

        // Todo sin negrita y sin formato de número, para que no quede nada de una versión anterior.
        yield return new Request
        {
            RepeatCell = new RepeatCellRequest
            {
                Range = Rango(0, p.Filas.Count + 5, 0, p.CantidadColumnas),
                Cell = new CellData { UserEnteredFormat = new CellFormat { TextFormat = new TextFormat { Bold = false } } },
                Fields = "userEnteredFormat.textFormat,userEnteredFormat.numberFormat"
            }
        };
        foreach (var fila in new[] { p.FilaEncabezado, p.FilaTotal })
        {
            yield return new Request
            {
                RepeatCell = new RepeatCellRequest
                {
                    Range = Rango(fila, fila + 1, 0, p.CantidadColumnas),
                    Cell = new CellData { UserEnteredFormat = new CellFormat { TextFormat = new TextFormat { Bold = true } } },
                    Fields = "userEnteredFormat.textFormat.bold"
                }
            };
        }
        foreach (var col in p.ColumnasMoneda)
        {
            yield return new Request
            {
                RepeatCell = new RepeatCellRequest
                {
                    Range = Rango(p.FilaEncabezado + 1, p.FilaTotal + 1, col, col + 1),
                    Cell = new CellData { UserEnteredFormat = new CellFormat { NumberFormat = new NumberFormat { Type = "CURRENCY", Pattern = FormatoMoneda } } },
                    Fields = "userEnteredFormat.numberFormat"
                }
            };
        }
        yield return new Request
        {
            UpdateSheetProperties = new UpdateSheetPropertiesRequest
            {
                Properties = new SheetProperties { SheetId = sheetId, GridProperties = new GridProperties { FrozenRowCount = p.FilaEncabezado + 1 } },
                Fields = "gridProperties.frozenRowCount"
            }
        };
        yield return new Request
        {
            AutoResizeDimensions = new AutoResizeDimensionsRequest
            {
                Dimensions = new DimensionRange { SheetId = sheetId, Dimension = "COLUMNS", StartIndex = 0, EndIndex = p.CantidadColumnas }
            }
        };
    }

    private SincronizacionException Traducir(GoogleApiException ex)
    {
        var email = SincronizacionService.LeerEmailCuentaServicio(rutaCredenciales) ?? "la cuenta de servicio";
        var mensaje = ex.HttpStatusCode switch
        {
            HttpStatusCode.Forbidden when ex.Message.Contains("has not been used", StringComparison.OrdinalIgnoreCase)
                                       || ex.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase) =>
                "La Google Sheets API no está habilitada en el proyecto de Google Cloud. Habilitala (APIs y servicios → Biblioteca) y esperá unos minutos.",
            HttpStatusCode.Forbidden =>
                $"La planilla no está compartida con {email}. Abrila en Google Drive → Compartir → agregá ese email como Editor.",
            HttpStatusCode.NotFound =>
                "No existe una planilla con ese ID. Copiá de nuevo el enlace de la planilla en Configuración.",
            HttpStatusCode.Unauthorized =>
                "Google rechazó las credenciales. Creá una clave nueva en Google Cloud y reemplazá credenciales-google.json.",
            _ => "Google respondió con un error: " + (ex.Error?.Message ?? ex.Message),
        };
        return new SincronizacionException(mensaje, ex);
    }
}
