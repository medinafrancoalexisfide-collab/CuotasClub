using CuotasClub.Data;
using CuotasClub.Services;

namespace CuotasClub.Views;

/// <summary>
/// Asistente de primer uso: aparece al abrir el programa cuando la base todavía no tiene precio de cuota
/// o % del club (por ejemplo, la primera vez que lo abre un profesor nuevo). Pide los datos del club,
/// el precio, el % y desde qué mes se cobra. Todo se puede cambiar después en Configuración.
/// </summary>
public class PrimerUsoForm : VentanaBase
{
    private readonly ConfiguracionService _config;

    private readonly TextBox _club = new() { Width = 300, PlaceholderText = "Ej.: Cuotas Club" };
    private readonly TextBox _taller = new() { Width = 300, PlaceholderText = "Ej.: Taller de Ajedrez" };
    private readonly TextBox _profesor = new() { Width = 300, PlaceholderText = "Opcional: sale en los recibos" };
    private readonly TextBox _precio = new() { Width = 110, TextAlign = HorizontalAlignment.Right, PlaceholderText = "20000" };
    private readonly NumericUpDown _porcentaje = new() { Minimum = 0, Maximum = 100, DecimalPlaces = 1, Width = 76 };
    private readonly ComboBox _mes = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly NumericUpDown _anio = new() { Minimum = 2020, Maximum = 2100, Width = 76 };

    private readonly Database _db;

    public PrimerUsoForm(Database db)
    {
        _db = db;
        _config = new ConfiguracionService(db);

        Text = "Bienvenido a CuotasClub";
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(560, 560);

        var tabla = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            Padding = new Padding(24, 14, 24, 8),
            BackColor = Tema.Fondo,
        };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void Fila(string etiqueta, params Control[] controles)
        {
            var lbl = Tema.Etiqueta(etiqueta);
            lbl.Anchor = AnchorStyles.Left;
            lbl.Margin = new Padding(0, 8, 12, 4);
            var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
            foreach (var c in controles)
            {
                if (c is not Label) c.Margin = new Padding(0, 4, 8, 2);
                f.Controls.Add(c);
            }
            tabla.Controls.Add(lbl);
            tabla.Controls.Add(f);
        }

        void Nota(string texto)
        {
            var n = Tema.Nota(texto, 480);
            n.Margin = new Padding(0, 12, 0, 2);
            tabla.Controls.Add(n);
            tabla.SetColumnSpan(n, 2);
        }

        Nota("Datos del club. Aparecen en la pantalla principal y en los recibos.");
        Fila("Club", _club);
        Fila("Taller o actividad", _taller);
        Fila("Profesor", _profesor);
        Nota("Cuota y reparto. Activos y Cadetes pagan lo mismo. Más adelante podés cambiar el precio o el % " +
             "desde un mes en Configuración, sin alterar los meses anteriores.");
        Fila("Precio de la cuota $", _precio);
        Fila("% para el club", _porcentaje, Tema.Etiqueta("(0 si no le corresponde nada)"));
        Nota("Primer mes que se cobra con el programa. Los meses anteriores quedan como \"—\" y no cuentan como deuda.");
        Fila("Cobrar desde", _mes, _anio);

        Nota("¿Ya usabas CuotasClub en esta u otra PC? Tocá \"Traer mis datos\" y elegí tu archivo cuotasclub.db " +
             "o una copia de seguridad (Documentos\\CuotasClub\\Copias). No hace falta completar lo de arriba.");
        var traer = Tema.BotonSecundario("&Traer mis datos…");
        traer.Margin = new Padding(0, 4, 0, 2);
        tabla.Controls.Add(traer);
        tabla.SetColumnSpan(traer, 2);
        traer.Click += (_, _) => TraerDatos();

        var empezar = Tema.BotonPrimario("&Empezar");
        var salir = Tema.BotonSecundario("&Salir");
        salir.DialogResult = DialogResult.Cancel;
        AcceptButton = empezar;
        CancelButton = salir;

        Controls.Add(tabla);
        Controls.Add(Tema.Pie(new Label { AutoSize = true, Text = "Todo se puede cambiar después en Configuración.", ForeColor = Tema.TextoSuave }, empezar, salir));
        Controls.Add(Tema.Encabezado("Configuración inicial", "Cargá estos datos una sola vez para empezar a usar el programa"));

        _mes.Items.AddRange(Formato.NombresMeses);
        var inicio = _config.InicioCobros;
        _mes.SelectedIndex = inicio.Month - 1;
        _anio.Value = Math.Clamp(inicio.Year, (int)_anio.Minimum, (int)_anio.Maximum);
        _club.Text = _config.NombreClub;
        _taller.Text = _config.NombreTaller;
        _profesor.Text = _config.NombreProfesor;
        _porcentaje.Value = 30;

        empezar.Click += (_, _) => Guardar();
        Shown += (_, _) => _club.Focus();
    }

    private void TraerDatos()
    {
        if (!ImportarBase.Elegir(this, _db)) return;
        if (new ConfiguracionService(_db).FaltaConfiguracionInicial)
        {
            // La base traída no tiene precio o %: se piden acá mismo con los datos que sí trajo.
            MessageBox.Show(this, "Datos traídos. Falta cargar el precio de la cuota o el % del club: completalos y tocá Empezar.",
                "CuotasClub", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK; // el programa vuelve a abrir este asistente con los datos nuevos
            Close();
            return;
        }
        MessageBox.Show(this, "Listo, tus datos ya están en el programa.", "CuotasClub", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Guardar()
    {
        if (string.IsNullOrWhiteSpace(_club.Text) && string.IsNullOrWhiteSpace(_taller.Text))
        {
            VistasComun.Error(this, "Escribí el nombre del club o del taller.");
            _club.Focus();
            return;
        }
        if (!Formato.TryParseMoneda(_precio.Text, out var precio) || precio <= 0)
        {
            VistasComun.Error(this, "Escribí el precio de la cuota, por ejemplo 20000.");
            _precio.Focus();
            return;
        }
        var desde = new DateOnly((int)_anio.Value, _mes.SelectedIndex + 1, 1);
        if (!VistasComun.Confirmar(this,
                $"¿Empezar con estos datos?{Environment.NewLine}{Environment.NewLine}" +
                $"Cuota: {Formato.Moneda(precio)}{Environment.NewLine}" +
                $"Para el club: {Formato.Decimal1(_porcentaje.Value)}%{Environment.NewLine}" +
                $"Se cobra desde: {Formato.MesAnio(desde.Year, desde.Month)}"))
            return;

        _config.GuardarConfiguracionInicial(_club.Text, _taller.Text, _profesor.Text, precio, _porcentaje.Value, desde);
        DialogResult = DialogResult.OK;
        Close();
    }
}
