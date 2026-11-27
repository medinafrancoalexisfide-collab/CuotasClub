using System.Drawing.Drawing2D;

namespace CuotasClub.Views;

/// <summary>
/// Aspecto común de todas las pantallas: colores, tipografía y "piezas" (encabezado, botones, grillas, tarjetas).
/// Todo lo visual se decide acá, así las pantallas solo arman su contenido.
/// Medidas en píxeles a 96 ppp: <see cref="VentanaBase"/> las agranda según la escala de Windows (125 %, 150 %…).
/// </summary>
internal static class Tema
{
    // --- Colores generales
    public static readonly Color Fondo = Color.FromArgb(243, 245, 248);
    public static readonly Color Superficie = Color.White;
    public static readonly Color Borde = Color.FromArgb(221, 226, 232);
    public static readonly Color LineaSuave = Color.FromArgb(234, 237, 241);
    public static readonly Color Texto = Color.FromArgb(31, 41, 55);
    public static readonly Color TextoSuave = Color.FromArgb(107, 114, 128);
    public static readonly Color Primario = Color.FromArgb(29, 78, 137);
    public static readonly Color PrimarioOscuro = Color.FromArgb(22, 61, 107);
    public static readonly Color PrimarioSuave = Color.FromArgb(232, 240, 250);
    public static readonly Color Seleccion = Color.FromArgb(219, 232, 248);
    public static readonly Color Peligro = Color.FromArgb(180, 35, 24);
    public static readonly Color Exito = Color.FromArgb(23, 102, 58);

    // --- Estados de cuota (mismos en Cuotas, Deudores y leyendas)
    public static readonly Color PagoFondo = Color.FromArgb(214, 242, 223);
    public static readonly Color PagoTexto = Color.FromArgb(23, 102, 58);
    public static readonly Color PendienteFondo = Color.FromArgb(255, 241, 194);
    public static readonly Color PendienteTexto = Color.FromArgb(133, 87, 0);
    public static readonly Color DebeFondo = Color.FromArgb(253, 222, 220);
    public static readonly Color DebeTexto = Color.FromArgb(180, 35, 24);
    public static readonly Color NoCorrespondeFondo = Color.FromArgb(241, 243, 245);
    public static readonly Color NoCorrespondeTexto = Color.FromArgb(160, 167, 177);

    // --- Tipografía (en puntos: Windows la agranda sola con la escala)
    public const string Familia = "Segoe UI";
    public static readonly Font Fuente = new(Familia, 10f);
    public static readonly Font FuenteChica = new(Familia, 9f);
    public static readonly Font FuenteNegrita = new(Familia, 10f, FontStyle.Bold);
    public static readonly Font FuenteSemibold = new("Segoe UI Semibold", 10f);
    public static readonly Font FuenteTitulo = new("Segoe UI Semibold", 16f);
    public static readonly Font FuenteSeccion = new("Segoe UI Semibold", 12f);
    public static readonly Font FuenteNumero = new("Segoe UI Semibold", 15f);

    /// <summary>Íconos de Windows 10/11. Si la fuente no está, las pantallas se arreglan sin íconos.</summary>
    public static readonly Font? FuenteIconos = CrearFuenteIconos();

    private static Font? CrearFuenteIconos()
    {
        foreach (var nombre in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
            if (FontFamily.Families.Any(f => f.Name == nombre))
                return new Font(nombre, 16f);
        return null;
    }

    // ---------------------------------------------------------------- Encabezado y estructura

    /// <summary>Franja blanca de arriba con el título de la pantalla y una línea de ayuda.</summary>
    public static Panel Encabezado(string titulo, string? subtitulo = null)
    {
        var panel = new Panel { Dock = DockStyle.Top, Height = subtitulo is null ? 58 : 76, BackColor = Superficie, Padding = new Padding(20, 12, 20, 10) };
        var lblSub = new Label
        {
            Text = subtitulo ?? "",
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 22,
            Font = Fuente,
            ForeColor = TextoSuave,
            Visible = subtitulo is not null,
        };
        var lblTitulo = new Label { Text = titulo, Dock = DockStyle.Top, Height = 32, Font = FuenteTitulo, ForeColor = Texto };
        panel.Controls.Add(lblSub);
        panel.Controls.Add(lblTitulo);
        panel.Paint += (_, e) => LineaInferior(panel, e.Graphics);
        return panel;
    }

    /// <summary>Barra de controles (filtros, botones) debajo del encabezado. Se acomoda en varias líneas si la ventana es angosta.</summary>
    public static FlowLayoutPanel Barra() => new()
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        WrapContents = true,
        Padding = new Padding(16, 12, 16, 6),
        BackColor = Fondo,
    };

    /// <summary>Contenedor blanco con borde fino ("tarjeta") que ocupa el resto de la ventana.</summary>
    public static Panel Contenedor(Control contenido, Padding? margen = null)
    {
        var externo = new Panel { Dock = DockStyle.Fill, Padding = margen ?? new Padding(16, 6, 16, 12), BackColor = Fondo };
        var tarjeta = TarjetaPanel();
        tarjeta.Dock = DockStyle.Fill;
        contenido.Dock = DockStyle.Fill;
        tarjeta.Controls.Add(contenido);
        externo.Controls.Add(tarjeta);
        return externo;
    }

    /// <summary>Panel blanco con borde de 1 px.</summary>
    public static Panel TarjetaPanel()
    {
        var p = new Panel { BackColor = Superficie, Padding = new Padding(1) };
        p.Paint += (_, e) =>
        {
            using var pen = new Pen(Borde);
            e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
        };
        p.Resize += (_, _) => p.Invalidate();
        return p;
    }

    /// <summary>Pie de la ventana: texto de estado a la izquierda y botones a la derecha.</summary>
    public static TableLayoutPanel Pie(Control izquierda, params Control[] derecha)
    {
        var pie = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(16, 0, 16, 12),
            BackColor = Fondo,
        };
        pie.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pie.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        izquierda.Anchor = AnchorStyles.Left;
        pie.Controls.Add(izquierda, 0, 0);
        var botones = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty, Anchor = AnchorStyles.Right };
        botones.Controls.AddRange(derecha);
        pie.Controls.Add(botones, 1, 0);
        return pie;
    }

    public static void LineaInferior(Control c, Graphics g)
    {
        using var pen = new Pen(Borde);
        g.DrawLine(pen, 0, c.Height - 1, c.Width, c.Height - 1);
    }

    // ---------------------------------------------------------------- Textos

    public static Label Etiqueta(string texto) => new()
    {
        Text = texto,
        AutoSize = true,
        ForeColor = Texto,
        Margin = new Padding(0, 8, 6, 0),
    };

    public static Label Nota(string texto, int anchoMaximo = 0) => new()
    {
        Text = texto,
        AutoSize = true,
        ForeColor = TextoSuave,
        Font = FuenteChica,
        MaximumSize = new Size(anchoMaximo, 0),
        Margin = new Padding(0, 4, 0, 4),
    };

    public static Label Seccion(string texto) => new()
    {
        Text = texto,
        AutoSize = true,
        Font = FuenteSeccion,
        ForeColor = Texto,
        Margin = new Padding(0, 0, 0, 6),
    };

    /// <summary>Etiqueta de color para leyendas ("Pagó", "Debe"…).</summary>
    public static Label Chip(string texto, Color fondo, Color letra) => new()
    {
        Text = texto,
        AutoSize = true,
        BackColor = fondo,
        ForeColor = letra,
        Font = FuenteChica,
        Padding = new Padding(8, 3, 8, 3),
        Margin = new Padding(6, 7, 0, 0),
    };

    /// <summary>Separador vertical invisible para dar aire entre grupos de una barra.</summary>
    public static Control Espacio(int ancho = 16) => new Panel { Width = ancho, Height = 1, Margin = Padding.Empty };

    // ---------------------------------------------------------------- Botones

    public static Button BotonPrimario(string texto) => Boton(texto, Primario, Color.White, Primario, PrimarioOscuro);

    public static Button BotonSecundario(string texto) => Boton(texto, Superficie, Texto, Borde, Color.FromArgb(245, 247, 250));

    public static Button BotonPeligro(string texto) => Boton(texto, Superficie, Peligro, Color.FromArgb(240, 199, 195), Color.FromArgb(254, 243, 242));

    /// <summary>Botón cuadrado chico para ◀ ▶.</summary>
    public static Button BotonFlecha(string texto)
    {
        var b = BotonSecundario(texto);
        b.AutoSize = false;
        b.Size = new Size(34, 32);
        b.Padding = Padding.Empty;
        return b;
    }

    private static Button Boton(string texto, Color fondo, Color letra, Color borde, Color hover)
    {
        var b = new Button
        {
            Text = texto,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(0, 32),
            Padding = new Padding(10, 2, 10, 2),
            Margin = new Padding(0, 2, 8, 2),
            FlatStyle = FlatStyle.Flat,
            BackColor = fondo,
            ForeColor = letra,
            Font = FuenteSemibold,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderColor = borde;
        b.FlatAppearance.BorderSize = 1;
        b.FlatAppearance.MouseOverBackColor = hover;
        b.FlatAppearance.MouseDownBackColor = hover;
        // Deshabilitado: gris claro, para que se note.
        b.EnabledChanged += (_, _) =>
        {
            b.BackColor = b.Enabled ? fondo : Color.FromArgb(240, 242, 245);
            b.FlatAppearance.BorderColor = b.Enabled ? borde : Borde;
        };
        return b;
    }

    // ---------------------------------------------------------------- Campos

    /// <summary>Aplica tamaño y márgenes comunes a un campo de una barra.</summary>
    public static T Campo<T>(T c, int ancho) where T : Control
    {
        c.Width = ancho;
        c.Margin = new Padding(0, 4, 8, 2);
        return c;
    }

    // ---------------------------------------------------------------- Grillas

    /// <summary>Grilla de solo lectura, sin líneas verticales, encabezado gris claro y filas altas.</summary>
    public static void EstilarGrilla(DataGridView g)
    {
        g.ReadOnly = true;
        g.AllowUserToAddRows = false;
        g.AllowUserToDeleteRows = false;
        g.AllowUserToResizeRows = false;
        g.AllowUserToOrderColumns = false;
        g.RowHeadersVisible = false;
        g.AutoGenerateColumns = false;
        g.BackgroundColor = Superficie;
        g.BorderStyle = BorderStyle.None;
        g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        g.GridColor = LineaSuave;
        g.EnableHeadersVisualStyles = false;
        g.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        g.ColumnHeadersHeight = 40;
        g.Font = Fuente;

        var enc = g.ColumnHeadersDefaultCellStyle;
        enc.BackColor = Color.FromArgb(247, 248, 250);
        enc.ForeColor = TextoSuave;
        enc.SelectionBackColor = enc.BackColor;
        enc.SelectionForeColor = enc.ForeColor;
        enc.Font = FuenteSemibold;
        enc.Padding = new Padding(8, 0, 8, 0);
        enc.WrapMode = DataGridViewTriState.False;

        var celda = g.DefaultCellStyle;
        celda.BackColor = Superficie;
        celda.ForeColor = Texto;
        celda.SelectionBackColor = Seleccion;
        celda.SelectionForeColor = Texto;
        celda.Padding = new Padding(8, 0, 8, 0);

        g.RowTemplate.Height = 34;
        // Evita parpadeo al repintar la grilla con colores.
        typeof(DataGridView).GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?.SetValue(g, true);
    }

    /// <summary>
    /// Las grillas no se agrandan solas con la escala de Windows: se ajusta a mano el alto de filas,
    /// el del encabezado y el ancho de las columnas que no se estiran.
    /// </summary>
    public static void EscalarGrillas(Control raiz, float factor)
    {
        foreach (Control c in raiz.Controls)
        {
            if (c is DataGridView g)
            {
                int S(int v) => (int)Math.Round(v * factor);
                g.RowTemplate.Height = S(g.RowTemplate.Height);
                g.ColumnHeadersHeight = S(g.ColumnHeadersHeight);
                foreach (DataGridViewColumn col in g.Columns)
                {
                    col.MinimumWidth = Math.Max(2, S(col.MinimumWidth));
                    if (col.AutoSizeMode != DataGridViewAutoSizeColumnMode.Fill && g.AutoSizeColumnsMode != DataGridViewAutoSizeColumnsMode.Fill)
                        col.Width = S(col.Width);
                }
                foreach (DataGridViewRow r in g.Rows) r.Height = g.RowTemplate.Height;
            }
            EscalarGrillas(c, factor);
        }
    }

    // ---------------------------------------------------------------- Dibujo

    public static GraphicsPath Redondeado(RectangleF r, float radio)
    {
        float d = radio * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }
}

/// <summary>
/// Base de todas las ventanas: fuente, colores y escala para pantallas con zoom de Windows
/// (125 %, 150 %…). Las medidas se escriben a 96 ppp y acá se agrandan una sola vez al abrir.
/// </summary>
public class VentanaBase : Form
{
    private bool _escalada;

    public VentanaBase()
    {
        AutoScaleMode = AutoScaleMode.None; // la escala la hacemos nosotros en OnLoad, una sola vez
        Font = Tema.Fuente;
        BackColor = Tema.Fondo;
        ForeColor = Tema.Texto;
        StartPosition = FormStartPosition.CenterParent;
        ShowInTaskbar = false;
        if (IconoPrograma is { } icono) Icon = icono;
    }

    /// <summary>El ícono del .exe, para que todas las ventanas lo muestren en la barra de título y de tareas.</summary>
    private static readonly Icon? IconoPrograma = CargarIcono();

    private static Icon? CargarIcono()
    {
        try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch (Exception ex) when (ex is ArgumentException or IOException) { return null; }
    }

    protected override void OnLoad(EventArgs e)
    {
        if (!_escalada)
        {
            _escalada = true;
            float factor = DeviceDpi / 96f;
            if (Math.Abs(factor - 1f) > 0.01f)
            {
                Scale(new SizeF(factor, factor));
                Tema.EscalarGrillas(this, factor);
            }
            AjustarAlContenido();
            AjustarAPantalla();
        }
        base.OnLoad(e);
    }

    /// <summary>Lo usan los diálogos para calcular su tamaño según lo que tienen adentro.</summary>
    protected virtual void AjustarAlContenido() { }

    /// <summary>Que la ventana nunca quede más grande que la pantalla (notebooks con zoom alto) y quede centrada.</summary>
    private void AjustarAPantalla()
    {
        var area = Screen.FromControl(Owner ?? (Control)this).WorkingArea;
        if (!AutoSize)
        {
            int ancho = Math.Min(Width, (int)(area.Width * 0.96));
            int alto = Math.Min(Height, (int)(area.Height * 0.94));
            if (MinimumSize.Width > ancho || MinimumSize.Height > alto)
                MinimumSize = new Size(Math.Min(MinimumSize.Width, ancho), Math.Min(MinimumSize.Height, alto));
            Size = new Size(ancho, alto);
        }
        if (StartPosition == FormStartPosition.CenterParent && Owner is not null) CenterToParent();
        else if (StartPosition is FormStartPosition.CenterParent or FormStartPosition.CenterScreen) CenterToScreen();
    }
}

/// <summary>
/// Diálogo chico (pago, baja, edición). Los controles van apilados con Dock = Top; al abrir, la ventana
/// toma el tamaño justo para mostrar todo (no se usa AutoSize del formulario, que en Windows la dejaba diminuta).
/// Se puede agrandar, pero no achicar por debajo de ese tamaño.
/// </summary>
public class DialogoBase : VentanaBase
{
    public DialogoBase()
    {
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = MinimizeBox = false;
        SizeGripStyle = SizeGripStyle.Show;
        AutoSize = false;
        BackColor = Tema.Superficie;
        Padding = new Padding(20, 16, 20, 16);
        ClientSize = new Size(480, 300); // provisorio: se recalcula al abrir
    }

    protected override void AjustarAlContenido()
    {
        var hijos = Controls.Cast<Control>().ToList(); // (Visible da false hasta que se muestra la ventana)
        if (hijos.Count == 0) return;
        int Ancho(Control c) => c.AutoSize ? c.GetPreferredSize(Size.Empty).Width : c.Width;
        int minimoCliente = MinimumSize.Width > 0 ? MinimumSize.Width - (Width - ClientSize.Width) : 0;
        int interior = Math.Max(hijos.Max(Ancho), minimoCliente - Padding.Horizontal);
        int Alto(Control c) => c.AutoSize ? c.GetPreferredSize(new Size(interior, 0)).Height : c.Height;
        int alto = hijos.Sum(Alto) + Padding.Vertical;
        ClientSize = new Size(interior + Padding.Horizontal, alto);
        MinimumSize = Size;
    }

    /// <summary>Fila de botones alineada a la derecha (el primero queda a la derecha de todo).</summary>
    protected static FlowLayoutPanel Botones(params Button[] botones)
    {
        var f = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 14, 0, 0),
        };
        foreach (var b in botones) { b.Margin = new Padding(8, 0, 0, 0); f.Controls.Add(b); }
        return f;
    }
}

/// <summary>
/// Tarjeta de un dato: el texto va en dos renglones separados por \n; el primero se dibuja chico y gris
/// (qué es) y el segundo grande (el número). El color de letra se aplica al número.
/// </summary>
internal class TarjetaDato : Label
{
    private readonly Font _fuenteValor = new("Segoe UI Semibold", 15F);

    public TarjetaDato()
    {
        AutoSize = false;
        Dock = DockStyle.Fill;
        BackColor = Tema.Superficie;
        ForeColor = Tema.Texto;
        Font = Tema.FuenteChica;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        var partes = Text.Split('\n', 2);
        string titulo = partes[0], valor = partes.Length > 1 ? partes[1] : "";
        float s = DeviceDpi / 96f;
        int x = (int)(14 * s), ancho = Width - x - (int)(6 * s);
        int altoT = TextRenderer.MeasureText(e.Graphics, "Ag", Font).Height;
        int altoV = TextRenderer.MeasureText(e.Graphics, "Ag", _fuenteValor).Height;
        int y = (Height - altoT - altoV) / 2;
        var flags = TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        TextRenderer.DrawText(e.Graphics, titulo, Font, new Rectangle(x, y, ancho, altoT), Tema.TextoSuave, flags);
        TextRenderer.DrawText(e.Graphics, valor, _fuenteValor, new Rectangle(x, y + altoT, ancho, altoV), ForeColor, flags);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _fuenteValor.Dispose();
        base.Dispose(disposing);
    }
}
