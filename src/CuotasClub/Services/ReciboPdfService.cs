using System.Globalization;
using System.Text;
using CuotasClub.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CuotasClub.Services;

/// <summary>
/// Genera el recibo de un pago en PDF (QuestPDF, licencia Community) para mandarlo por WhatsApp.
/// Formato A6 apaisado: chico y legible en el celular.
/// </summary>
/// <param name="nombreClub">Línea principal del encabezado (vacío = se usa el taller).</param>
/// <param name="nombreTaller">Segunda línea del encabezado.</param>
/// <param name="nombreProfesor">Opcional: si está, el recibo dice quién recibió el pago.</param>
public class ReciboPdfService(string nombreClub, string nombreTaller, string nombreProfesor = "")
{

    static ReciboPdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>Carpeta por defecto: Documentos\CuotasClub\Recibos.</summary>
    public static string CarpetaPorDefecto => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CuotasClub", "Recibos");

    /// <summary>Genera el PDF y devuelve la ruta del archivo creado.</summary>
    public string Generar(Pago pago, Alumno alumno, string? carpeta = null)
    {
        ArgumentNullException.ThrowIfNull(pago);
        ArgumentNullException.ThrowIfNull(alumno);

        carpeta ??= CarpetaPorDefecto;
        Directory.CreateDirectory(carpeta);
        string ruta = Path.Combine(carpeta,
            $"Recibo_{pago.Anio}-{pago.Mes:00}_{Limpiar(alumno.Apellido)}_{Limpiar(alumno.Nombre)}.pdf");

        CrearDocumento(pago, alumno).GeneratePdf(ruta);
        return ruta;
    }

    /// <summary>Documento QuestPDF (separado para poder previsualizarlo o probarlo).</summary>
    public IDocument CrearDocumento(Pago pago, Alumno alumno)
    {
        string mes = Formato.MesAnio(pago.Anio, pago.Mes);
        string numero = pago.Id.ToString("000000", CultureInfo.InvariantCulture);
        string fecha = Formato.Fecha(pago.FechaPago);
        // Cadete con tutor → "Recibí de" el tutor; Activo → el propio alumno.
        string recibiDe = alumno.Tutor ?? $"{alumno.Nombre} {alumno.Apellido}";
        var verde = QuestPDF.Infrastructure.Color.FromHex("#2E7D32");

        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                page.Size(PageSizes.A6.Landscape());
                page.Margin(18);
                page.DefaultTextStyle(t => t.FontSize(10).FontColor(Colors.Grey.Darken4));

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        string principal = nombreClub.Length > 0 ? nombreClub : nombreTaller.Length > 0 ? nombreTaller : "Recibo de cuota";
                        c.Item().Text(principal).FontSize(13).Bold();
                        if (nombreClub.Length > 0 && nombreTaller.Length > 0)
                            c.Item().Text(nombreTaller).FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(120).AlignRight().Column(c =>
                    {
                        c.Item().AlignRight().Text("RECIBO").FontSize(13).Bold().FontColor(verde);
                        c.Item().AlignRight().Text($"N° {numero}").FontSize(9);
                        c.Item().AlignRight().Text(fecha).FontSize(9);
                    });
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(4);
                    col.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    col.Item().PaddingTop(4).Element(e => Dato(e, "Recibí de", recibiDe));
                    col.Item().Element(e => Dato(e, "Alumno", $"{alumno.Apellido}, {alumno.Nombre}"));
                    col.Item().Element(e => Dato(e, "DNI", alumno.Dni));
                    col.Item().Element(e => Dato(e, "Concepto", $"Cuota {mes}"));
                    col.Item().Element(e => Dato(e, "Medio de pago", pago.MedioPago.ATexto()));
                    col.Item().PaddingTop(6).Background(Colors.Green.Lighten5).Padding(6).Row(r =>
                    {
                        r.RelativeItem().Text("Total").FontSize(12).Bold();
                        r.RelativeItem().AlignRight().Text(Formato.Moneda(pago.Monto)).FontSize(14).Bold().FontColor(verde);
                    });
                    if (nombreProfesor.Length > 0)
                        col.Item().PaddingTop(4).Element(e => Dato(e, "Recibió", nombreProfesor));
                });

                page.Footer().AlignCenter()
                    .Text("Gracias por acompañar el taller.")
                    .FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private static void Dato(IContainer c, string etiqueta, string valor) =>
        c.Row(r =>
        {
            r.ConstantItem(90).Text(etiqueta + ":").FontColor(Colors.Grey.Darken1);
            r.RelativeItem().Text(valor).SemiBold();
        });

    /// <summary>Quita acentos y caracteres no válidos para nombres de archivo.</summary>
    private static string Limpiar(string texto)
    {
        var sb = new StringBuilder();
        foreach (char ch in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            else if (ch is ' ' or '-') sb.Append('-');
        }
        return sb.Length == 0 ? "alumno" : sb.ToString();
    }
}
