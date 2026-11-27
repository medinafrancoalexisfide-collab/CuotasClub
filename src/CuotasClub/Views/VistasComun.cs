using System.Globalization;

namespace CuotasClub.Views;

/// <summary>Utilidades compartidas por las pantallas de Alumnos y Cuotas.</summary>
internal static class VistasComun
{
    public static readonly CultureInfo Cultura = new("es-AR");

    public static string[] MesesCortos => Services.Formato.MesesCortos;
    public static string[] Meses => Services.Formato.NombresMeses;

    public static DateOnly Hoy => DateOnly.FromDateTime(DateTime.Today);

    public static string Fecha(DateOnly f) => Services.Formato.Fecha(f);
    public static string Fecha(DateOnly? f) => f is { } v ? Fecha(v) : "";

    /// <summary>Acepta dd/MM/yyyy, d/M/yyyy, dd-MM-yyyy y ddMMyyyy.</summary>
    public static bool TryParseFecha(string? texto, out DateOnly fecha)
    {
        string[] formatos = ["dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "ddMMyyyy"];
        return DateOnly.TryParseExact((texto ?? "").Trim(), formatos, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out fecha);
    }

    public static DateOnly ADateOnly(DateTime d) => DateOnly.FromDateTime(d);
    public static DateTime ADateTime(DateOnly d) => d.ToDateTime(TimeOnly.MinValue);

    /// <summary>Estilo común de las grillas (ver <see cref="Tema.EstilarGrilla"/>).</summary>
    public static void EstilarGrilla(DataGridView g) => Tema.EstilarGrilla(g);

    public static DataGridViewTextBoxColumn Columna(string nombre, string titulo, int ancho,
        DataGridViewContentAlignment alineacion = DataGridViewContentAlignment.MiddleLeft)
    {
        var c = new DataGridViewTextBoxColumn
        {
            Name = nombre,
            HeaderText = titulo,
            Width = ancho,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        c.DefaultCellStyle.Alignment = alineacion;
        return c;
    }

    public static void Error(IWin32Window owner, string mensaje) =>
        MessageBox.Show(owner, mensaje, "CuotasClub", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    public static bool Confirmar(IWin32Window owner, string mensaje) =>
        MessageBox.Show(owner, mensaje, "CuotasClub", MessageBoxButtons.YesNo, MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
}
