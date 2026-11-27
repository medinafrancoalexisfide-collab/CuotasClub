using System.Drawing.Drawing2D;
using CuotasClub.Data;
using CuotasClub.Services;

namespace CuotasClub.Views;

/// <summary>
/// Pantalla principal: encabezado del taller y un mosaico por módulo.
/// Cada módulo es un formulario con constructor (Database db) que se abre con ShowDialog.
/// Para agregar un módulo, sumar una línea en <see cref="CrearMenu"/>.
/// </summary>
public class MainForm : VentanaBase
{
    private readonly Database _db;
    private readonly TableLayoutPanel _mosaicos;
    private readonly Label _titulo;
    private readonly Label _subtitulo;
    private readonly Label _estadoSync = new() { AutoSize = true, ForeColor = Tema.TextoSuave, Font = Tema.FuenteChica, Margin = new Padding(0, 2, 10, 0) };
    private readonly LinkLabel _sincronizarAhora = new()
    {
        Text = "Sincronizar ahora",
        AutoSize = true,
        Font = Tema.FuenteChica,
        LinkColor = Tema.Primario,
        ActiveLinkColor = Tema.PrimarioOscuro,
        LinkBehavior = LinkBehavior.HoverUnderline,
        Margin = new Padding(0, 2, 0, 0),
    };

    public MainForm(Database db)
    {
        _db = db;
        Text = "CuotasClub";
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = true;
        ClientSize = new Size(780, 560);
        MinimumSize = new Size(620, 500);

        // --- Encabezado azul
        var encabezado = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Tema.Primario, Padding = new Padding(28, 20, 28, 16) };
        var hoy = DateTime.Today;
        var fecha = new Label
        {
            Text = $"{Formato.NombreMes(hoy.Month)} {hoy.Year}",
            Dock = DockStyle.Right,
            AutoSize = true,
            ForeColor = Color.FromArgb(200, 218, 240),
            Font = Tema.FuenteSemibold,
            Padding = new Padding(0, 8, 0, 0),
        };
        var subtitulo = _subtitulo = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            ForeColor = Color.FromArgb(200, 218, 240),
            Font = new Font(Tema.Familia, 10.5f),
        };
        var titulo = _titulo = new Label
        {
            Dock = DockStyle.Top,
            Height = 42,
            ForeColor = Color.White,
            Font = new Font("Segoe UI Semibold", 20f),
        };
        encabezado.Controls.Add(subtitulo);
        encabezado.Controls.Add(titulo);
        encabezado.Controls.Add(fecha);

        // --- Mosaicos: 2 columnas × 3 filas
        _mosaicos = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(20, 18, 20, 10),
            BackColor = Tema.Fondo,
        };
        _mosaicos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        _mosaicos.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        for (int i = 0; i < 3; i++) _mosaicos.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

        // --- Barra de estado (planilla del tesorero)
        var barraEstado = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 38,
            Padding = new Padding(28, 10, 28, 0),
            WrapContents = false,
            BackColor = Tema.Superficie,
        };
        barraEstado.Paint += (_, e) =>
        {
            using var pen = new Pen(Tema.Borde);
            e.Graphics.DrawLine(pen, 0, 0, barraEstado.Width, 0);
        };
        barraEstado.Controls.Add(_estadoSync);
        barraEstado.Controls.Add(_sincronizarAhora);
        _sincronizarAhora.LinkClicked += async (_, _) =>
        {
            var r = await Sincronizador.SincronizarAsync();
            if (!r.Ok && !IsDisposed)
                MessageBox.Show(this, r.Mensaje, "Planilla del tesorero", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        };

        Controls.Add(_mosaicos);
        Controls.Add(encabezado);
        Controls.Add(barraEstado);
        CrearMenu();
        ActualizarNombres();

        Sincronizador.EstadoCambio += AlCambiarEstadoSync;
        FormClosed += (_, _) => Sincronizador.EstadoCambio -= AlCambiarEstadoSync;
        // Al abrir: si quedaron cambios sin subir (por ejemplo, sin internet la última vez), se reintenta.
        Shown += (_, _) => { ActualizarEstadoSync(); Sincronizador.SiHayPendientes(); };
    }

    /// <summary>Muestra el nombre del taller y del club cargados en Configuración.</summary>
    private void ActualizarNombres()
    {
        var config = new ConfiguracionService(_db);
        _titulo.Text = config.TituloTaller;
        var club = config.NombreClub;
        _subtitulo.Text = club.Length > 0 && club != config.TituloTaller ? $"{club} · Control de cuotas" : "Control de cuotas";
        Text = $"CuotasClub · {config.TituloTaller}";
    }

    private void AlCambiarEstadoSync()
    {
        if (IsHandleCreated && !IsDisposed) BeginInvoke(ActualizarEstadoSync);
    }

    private void ActualizarEstadoSync()
    {
        var config = new ConfiguracionService(_db);
        _estadoSync.Text = Sincronizador.TextoEstado();
        _sincronizarAhora.Visible = config.SheetsConfigurado && !Sincronizador.EnCurso;
    }

    private void CrearMenu()
    {
        AgregarMosaico("", "&Alumnos", "Altas, bajas y datos de contacto", db => new AlumnosForm(db));
        AgregarMosaico("", "&Cuotas", "Registrar pagos y ver quién pagó cada mes", db => new CuotasForm(db));
        AgregarMosaico("", "&Deudores", "Quién debe, qué meses y cuánto", db => new DeudoresForm(db), Tema.DebeTexto);
        AgregarMosaico("", "&Gastos", "Transporte, materiales y otros", db => new GastosForm(db));
        AgregarMosaico("", "&Resumen", "Ingresos, parte del club y ganancia", db => new ResumenForm(db));
        AgregarMosaico("", "C&onfiguración", "Datos del club, precio, % del club, copias y planilla", db => new ConfiguracionForm(db));
    }

    private void AgregarMosaico(string icono, string titulo, string descripcion, Func<Database, Form> crear, Color? acento = null)
    {
        var m = new Mosaico(icono, titulo, descripcion, acento ?? Tema.Primario) { Dock = DockStyle.Fill, Margin = new Padding(8) };
        m.Click += (_, _) =>
        {
            using var f = crear(_db);
            f.ShowDialog(this);
            ActualizarNombres();
            ActualizarEstadoSync();
        };
        _mosaicos.Controls.Add(m);
    }
}

/// <summary>Botón grande de la pantalla principal: ícono, título y una línea que explica para qué sirve.</summary>
internal class Mosaico : Button
{
    private readonly string _icono;
    private readonly string _descripcion;
    private readonly Color _acento;
    private readonly Font _fuenteTitulo = new("Segoe UI Semibold", 13f);
    private bool _encima;

    public Mosaico(string icono, string titulo, string descripcion, Color acento)
    {
        _icono = icono;
        _descripcion = descripcion;
        _acento = acento;
        Text = titulo; // con "&" para el atajo Alt+letra
        Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
    }

    protected override void OnMouseEnter(EventArgs e) { _encima = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _encima = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _fuenteTitulo.Dispose();
        base.Dispose(disposing);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Parent?.BackColor ?? Tema.Fondo);

        float s = DeviceDpi / 96f;
        var rect = new RectangleF(1f, 1f, Width - 2.5f, Height - 2.5f);
        using (var path = Tema.Redondeado(rect, 8 * s))
        {
            using var fondo = new SolidBrush(_encima ? Tema.PrimarioSuave : Tema.Superficie);
            g.FillPath(fondo, path);
            using var borde = new Pen(_encima || Focused ? _acento : Tema.Borde, Focused ? 2 * s : 1);
            g.DrawPath(borde, path);
        }

        // Ícono en un círculo de color suave
        float diam = 46 * s;
        float x = 18 * s;
        float yCirculo = (Height - diam) / 2f;
        using (var circulo = new SolidBrush(Color.FromArgb(28, _acento)))
            g.FillEllipse(circulo, x, yCirculo, diam, diam);
        if (Tema.FuenteIconos is { } fi)
        {
            var r = new Rectangle((int)x, (int)yCirculo, (int)diam, (int)diam);
            TextRenderer.DrawText(g, _icono, fi, r, _acento, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        // Título y descripción, centrados en vertical
        int textoX = (int)(x + diam + 16 * s);
        int ancho = Math.Max(10, Width - textoX - (int)(12 * s));
        int altoTitulo = TextRenderer.MeasureText(g, "Ag", _fuenteTitulo).Height;
        var flagsDesc = TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        int altoDesc = TextRenderer.MeasureText(g, _descripcion, Tema.Fuente, new Size(ancho, 0), flagsDesc).Height;
        altoDesc = Math.Max(0, Math.Min(altoDesc, Height - altoTitulo - (int)(12 * s)));
        int y = (Height - altoTitulo - altoDesc) / 2;
        var flagsTitulo = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | (ShowKeyboardCues ? TextFormatFlags.Default : TextFormatFlags.HidePrefix);
        TextRenderer.DrawText(g, Text, _fuenteTitulo, new Rectangle(textoX, y, ancho, altoTitulo), Tema.Texto, flagsTitulo);
        TextRenderer.DrawText(g, _descripcion, Tema.Fuente, new Rectangle(textoX, y + altoTitulo, ancho, altoDesc), Tema.TextoSuave, flagsDesc);
    }
}
