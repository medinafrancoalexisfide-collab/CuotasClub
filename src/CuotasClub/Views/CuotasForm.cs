using CuotasClub.Data;
using CuotasClub.Models;
using CuotasClub.Repositories;
using CuotasClub.Services;

namespace CuotasClub.Views;

/// <summary>
/// Grilla anual alumnos × meses. Verde = pagó, rojo = debe, gris = "—" (no corresponde),
/// amarillo = pendiente (mes en curso todavía sin pagar), blanco = meses futuros.
/// Doble clic (o Enter) en una celda: registra el pago, o muestra el pago para recibo / anulación.
/// </summary>
public class CuotasForm : VentanaBase
{
    private static readonly Color VerdeFondo = Tema.PagoFondo;
    private static readonly Color VerdeTexto = Tema.PagoTexto;
    private static readonly Color RojoFondo = Tema.DebeFondo;
    private static readonly Color RojoTexto = Tema.DebeTexto;
    private static readonly Color GrisFondo = Tema.NoCorrespondeFondo;
    private static readonly Color GrisTexto = Tema.NoCorrespondeTexto;
    private static readonly Color AmarilloFondo = Tema.PendienteFondo;
    private static readonly Color AmarilloTexto = Tema.PendienteTexto;

    private const int ColPrimerMes = 1;

    private readonly CuotaService _cuotas;
    private readonly AlumnoRepository _alumnos;

    private readonly NumericUpDown _anio = Tema.Campo(new NumericUpDown { Minimum = 2020, Maximum = 2100, TextAlign = HorizontalAlignment.Center }, 76);
    private readonly Button _anioAnterior = Tema.BotonFlecha("◀");
    private readonly Button _anioSiguiente = Tema.BotonFlecha("▶");
    private readonly TextBox _buscar = Tema.Campo(new TextBox { PlaceholderText = "Filtrar por nombre o DNI" }, 240);
    private readonly Button _actualizar = Tema.BotonSecundario("Actualizar (F5)");
    private readonly DataGridView _grilla = new();
    private readonly Label _resumen = new() { AutoSize = true, Font = Tema.FuenteSemibold, ForeColor = Tema.Texto, Margin = new Padding(0, 6, 0, 0) };
    private readonly ContextMenuStrip _menu = new() { Font = Tema.Fuente };
    private readonly ToolStripMenuItem _miRegistrar = new("Registrar pago…");
    private readonly ToolStripMenuItem _miRecibo = new("Generar recibo");
    private readonly ToolStripMenuItem _miAnular = new("Anular pago…");

    private List<FilaGrillaCuotas> _filas = [];

    private readonly Database _db;

    public CuotasForm(Database db)
    {
        _db = db;
        _cuotas = new CuotaService(db);
        _alumnos = new AlumnoRepository(db);

        Text = "Cuotas";
        Size = new Size(1240, 700);
        MinimumSize = new Size(920, 460);
        KeyPreview = true;

        _anio.Value = Math.Clamp(DateTime.Today.Year, (int)_anio.Minimum, (int)_anio.Maximum);
        _anio.Font = Tema.FuenteSemibold;
        _anioAnterior.Margin = new Padding(0, 2, 4, 2);
        _anioSiguiente.Margin = new Padding(0, 2, 16, 2);
        _anio.Margin = new Padding(0, 4, 4, 2);

        var barra = Tema.Barra();
        barra.Controls.AddRange([Tema.Etiqueta("Año"), _anioAnterior, _anio, _anioSiguiente, _buscar, _actualizar, Tema.Espacio(12)]);
        barra.Controls.Add(Tema.Chip("✓  Pagó", VerdeFondo, VerdeTexto));
        barra.Controls.Add(Tema.Chip("Pendiente (mes en curso)", AmarilloFondo, AmarilloTexto));
        barra.Controls.Add(Tema.Chip("Debe", RojoFondo, RojoTexto));
        barra.Controls.Add(Tema.Chip("—  No corresponde", GrisFondo, GrisTexto));

        ConfigurarGrilla();
        _menu.Items.AddRange([_miRegistrar, _miRecibo, new ToolStripSeparator(), _miAnular]);
        _grilla.ContextMenuStrip = _menu;

        Controls.Add(Tema.Contenedor(_grilla));
        Controls.Add(Tema.Pie(_resumen));
        Controls.Add(barra);
        Controls.Add(Tema.Encabezado("Cuotas",
            "Doble clic en un mes para registrar el pago (o ver el recibo) · Clic derecho para más opciones"));

        _anio.ValueChanged += (_, _) => Recargar();
        _anioAnterior.Click += (_, _) => { if (_anio.Value > _anio.Minimum) _anio.Value--; };
        _anioSiguiente.Click += (_, _) => { if (_anio.Value < _anio.Maximum) _anio.Value++; };
        _buscar.TextChanged += (_, _) => Recargar();
        _actualizar.Click += (_, _) => Recargar();
        _grilla.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0 && e.ColumnIndex >= 0) AccionCelda(e.RowIndex, e.ColumnIndex); };
        _grilla.CellFormatting += Grilla_CellFormatting;
        _grilla.CellPainting += Grilla_CellPainting;
        _grilla.CurrentCellChanged += (_, _) => { ActualizarResumen(); _grilla.Invalidate(); };
        _grilla.CellMouseDown += (_, e) =>
        {
            // Clic derecho selecciona la celda antes de abrir el menú.
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0 && e.ColumnIndex >= 0)
                _grilla.CurrentCell = _grilla[e.ColumnIndex, e.RowIndex];
        };
        _menu.Opening += Menu_Opening;
        _miRegistrar.Click += (_, _) => { if (CeldaActual() is var (f, m)) RegistrarPago(f, m); };
        _miRecibo.Click += (_, _) => { if (CeldaActual() is var (f, m) && _filas[f].Pagos[m] is { } p) ReciboUi.GenerarYMostrar(this, _db, p, _filas[f].Alumno); };
        _miAnular.Click += (_, _) => { if (CeldaActual() is var (f, m)) AnularPago(f, m); };

        Load += (_, _) => { Recargar(); SeleccionarMesActual(); };
    }

    /// <summary>Abre en un año y con el filtro ya escrito (lo usa Deudores).</summary>
    public CuotasForm(Database db, int anio, string filtro) : this(db)
    {
        _anio.Value = Math.Clamp(anio, (int)_anio.Minimum, (int)_anio.Maximum);
        _buscar.Text = filtro;
    }

    private void ConfigurarGrilla()
    {
        VistasComun.EstilarGrilla(_grilla);
        _grilla.SelectionMode = DataGridViewSelectionMode.CellSelect;
        _grilla.MultiSelect = false;
        _grilla.ShowCellToolTips = true;
        _grilla.CellBorderStyle = DataGridViewCellBorderStyle.Single; // líneas finas entre meses
        _grilla.GridColor = Tema.LineaSuave;

        var alumno = VistasComun.Columna("Alumno", "Alumno", 230);
        alumno.Frozen = true;
        alumno.DefaultCellStyle.Font = Tema.FuenteSemibold;
        _grilla.Columns.Add(alumno);
        for (int m = 0; m < 12; m++)
        {
            var c = VistasComun.Columna($"M{m + 1}", VistasComun.MesesCortos[m], 66, DataGridViewContentAlignment.MiddleCenter);
            c.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            c.DefaultCellStyle.Font = Tema.FuenteSemibold;
            c.DefaultCellStyle.Padding = Padding.Empty;
            c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; // los meses se reparten el ancho de la ventana
            c.MinimumWidth = 58;
            _grilla.Columns.Add(c);
        }
        var debe = VistasComun.Columna("Debe", "Meses que debe", 120, DataGridViewContentAlignment.MiddleCenter);
        debe.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
        debe.DefaultCellStyle.Font = Tema.FuenteSemibold;

        _grilla.Columns.Add(debe);
    }

    private int Anio => (int)_anio.Value;

    private void Recargar()
    {
        var actual = _grilla.CurrentCell is { } c ? (alumnoId: (_grilla.Rows[c.RowIndex].Tag as FilaGrillaCuotas)?.Alumno.Id, col: c.ColumnIndex) : (null, -1);

        try
        {
            Cursor.Current = Cursors.WaitCursor;
            var todas = _cuotas.ObtenerGrillaAnual(Anio, VistasComun.Hoy);
            // El servicio ya trae solo los alumnos con algún mes cobrable o pago en el año, ordenados por apellido.
            var palabras = _buscar.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            _filas = todas.Where(f => palabras.All(p => Contiene(f.Alumno.NombreCompleto, p) || f.Alumno.Dni.Contains(p))).ToList();
        }
        catch (Exception ex)
        {
            VistasComun.Error(this, "No se pudo leer la grilla de cuotas.\n\n" + ex.Message);
            _filas = [];
        }
        finally
        {
            Cursor.Current = Cursors.Default;
        }

        _grilla.SuspendLayout();
        _grilla.Rows.Clear();
        foreach (var f in _filas)
        {
            var valores = new object[14];
            valores[0] = f.Alumno.NombreCompleto;
            for (int m = 0; m < 12; m++) valores[ColPrimerMes + m] = TextoCelda(f, m);
            int deuda = f.Estados.Count(e => e == EstadoCuota.Debe);
            valores[13] = deuda == 0 ? "" : deuda.ToString();
            int i = _grilla.Rows.Add(valores);
            var row = _grilla.Rows[i];
            row.Tag = f;
            for (int m = 0; m < 12; m++) row.Cells[ColPrimerMes + m].ToolTipText = Tooltip(f, m);
            if (deuda > 0) row.Cells[13].Style.ForeColor = RojoTexto;
            if (f.Alumno.Id == actual.alumnoId && actual.col >= 0)
                _grilla.CurrentCell = row.Cells[actual.col];
        }
        _grilla.ResumeLayout();
        ActualizarResumen();
    }

    private static bool Contiene(string texto, string parte) =>
        VistasComun.Cultura.CompareInfo.IndexOf(texto, parte,
            System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0;

    private string TextoCelda(FilaGrillaCuotas f, int m) => f.Estados[m] switch
    {
        EstadoCuota.Pagado => "✓",
        EstadoCuota.Debe => "Debe",
        EstadoCuota.Pendiente when f.Pagos[m] is null && EsMesEnCurso(m) => "Pend.",
        EstadoCuota.NoCorresponde => f.Pagos[m] is not null ? "✓" : "—",
        _ => "",
    };

    private string Tooltip(FilaGrillaCuotas f, int m)
    {
        string mes = $"{VistasComun.Meses[m]} {Anio}";
        if (f.Pagos[m] is { } p)
            return $"{mes}: pagó {Formato.Moneda(p.Monto)} el {VistasComun.Fecha(p.FechaPago)} ({p.MedioPago.ATexto()}).\nDoble clic: recibo o anular.";
        return f.Estados[m] switch
        {
            EstadoCuota.Debe => $"{mes}: debe. Doble clic para registrar el pago.",
            EstadoCuota.Pendiente => $"{mes}: todavía no venció. Doble clic para registrar el pago.",
            _ => $"{mes}: no corresponde (no estaba activo), no se cobra.",
        };
    }

    private void Grilla_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < ColPrimerMes || e.ColumnIndex >= ColPrimerMes + 12 || e.CellStyle is null) return;
        if (_grilla.Rows[e.RowIndex].Tag is not FilaGrillaCuotas f) return;
        int m = e.ColumnIndex - ColPrimerMes;

        (Color fondo, Color letra) = (f.Pagos[m] is not null ? EstadoCuota.Pagado : f.Estados[m]) switch
        {
            EstadoCuota.Pagado => (VerdeFondo, VerdeTexto),
            EstadoCuota.Debe => (RojoFondo, RojoTexto),
            EstadoCuota.NoCorresponde => (GrisFondo, GrisTexto),
            _ => (EsMesEnCurso(m) ? AmarilloFondo : Tema.Superficie, EsMesEnCurso(m) ? AmarilloTexto : Tema.Texto),
        };
        e.CellStyle.BackColor = fondo;
        e.CellStyle.ForeColor = letra;
        // La celda elegida conserva su color de estado; se marca con un borde azul (ver CellPainting).
        e.CellStyle.SelectionBackColor = fondo;
        e.CellStyle.SelectionForeColor = letra;
    }

    /// <summary>Borde azul alrededor de la celda seleccionada, para no tapar el color del estado.</summary>
    private void Grilla_CellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Graphics is null) return;
        if (_grilla.CurrentCell is not { } c || c.RowIndex != e.RowIndex || c.ColumnIndex != e.ColumnIndex) return;
        e.Paint(e.CellBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.Focus);
        float s = _grilla.DeviceDpi / 96f;
        int grosor = Math.Max(2, (int)Math.Round(2 * s));
        var r = e.CellBounds;
        r.Inflate(-grosor / 2 - 1, -grosor / 2 - 1);
        using var pen = new Pen(Tema.Primario, grosor);
        e.Graphics.DrawRectangle(pen, r);
        e.Handled = true;
    }

    private bool EsMesEnCurso(int m) => Anio == DateTime.Today.Year && m + 1 == DateTime.Today.Month;

    private (int fila, int mes)? CeldaActual()
    {
        if (_grilla.CurrentCell is not { } c) return null;
        int m = c.ColumnIndex - ColPrimerMes;
        if (m is < 0 or > 11 || c.RowIndex < 0 || c.RowIndex >= _filas.Count) return null;
        return (c.RowIndex, m);
    }

    private void Menu_Opening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (CeldaActual() is not var (f, m)) { e.Cancel = true; return; }
        bool pagado = _filas[f].Pagos[m] is not null;
        _miRegistrar.Enabled = !pagado;
        _miRecibo.Enabled = pagado;
        _miAnular.Enabled = pagado;
    }

    private void AccionCelda(int fila, int col)
    {
        int m = col - ColPrimerMes;
        if (m is < 0 or > 11) return;
        var f = _filas[fila];
        if (f.Pagos[m] is { } pago)
        {
            using var d = new DetallePagoForm(f.Alumno, pago);
            if (d.ShowDialog(this) != DialogResult.OK) return;
            if (d.AccionElegida == DetallePagoForm.Accion.Recibo) ReciboUi.GenerarYMostrar(this, _db, pago, f.Alumno);
            else if (d.AccionElegida == DetallePagoForm.Accion.Anular) AnularPago(fila, m);
        }
        else
        {
            RegistrarPago(fila, m);
        }
    }

    private void RegistrarPago(int fila, int m)
    {
        var f = _filas[fila];
        int mes = m + 1;
        if (f.Estados[m] == EstadoCuota.NoCorresponde)
        {
            MessageBox.Show(this,
                $"{Formato.MesAnio(Anio, mes)} no corresponde para {f.Alumno.NombreCompleto} " +
                "(antes del alta, mes de la baja o después, o antes de que empiecen los cobros), así que no se cobra.\n\n" +
                "Si se dio de baja ese mes y querés cobrarlo, en Alumnos tocá \"Dar de baja\" otra vez y poné el último día del mes.",
                "Cuotas", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_cuotas.ObtenerPrecioVigente(Anio, mes) is not { } monto)
        {
            VistasComun.Error(this, $"No hay un precio de cuota cargado para {Formato.MesAnio(Anio, mes)}. Cargalo en Configuración.");
            return;
        }

        using var dlg = new RegistrarPagoForm(f.Alumno, Anio, mes, monto);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        Pago pago;
        try
        {
            pago = _cuotas.RegistrarPago(f.Alumno.Id, Anio, mes, dlg.FechaPago, dlg.MedioPago);
        }
        catch (ReglaNegocioException ex)
        {
            VistasComun.Error(this, ex.Message);
            Recargar();
            return;
        }
        catch (Exception ex)
        {
            VistasComun.Error(this, "No se pudo registrar el pago.\n\n" + ex.Message);
            Recargar();
            return;
        }

        Recargar();
        Sincronizador.SiHayPendientes(); // planilla del tesorero, en segundo plano
        if (dlg.GenerarRecibo)
            ReciboUi.GenerarYMostrar(this, _db, pago, _alumnos.ObtenerPorId(f.Alumno.Id) ?? f.Alumno);
    }

    private void AnularPago(int fila, int m)
    {
        var f = _filas[fila];
        if (f.Pagos[m] is not { } pago) return;
        if (!VistasComun.Confirmar(this,
                $"¿Anular el pago de {VistasComun.Meses[m]} {Anio} de {f.Alumno.NombreCompleto} " +
                $"({Formato.Moneda(pago.Monto)}, {VistasComun.Fecha(pago.FechaPago)}, {pago.MedioPago.ATexto()})?\n\n" +
                "El pago queda guardado como anulado y el mes vuelve a figurar como no pagado."))
            return;
        try
        {
            _cuotas.AnularPago(pago.Id);
            Sincronizador.SiHayPendientes();
        }
        catch (Exception ex)
        {
            VistasComun.Error(this, "No se pudo anular el pago.\n\n" + ex.Message);
        }
        Recargar();
    }

    private void SeleccionarMesActual()
    {
        if (_grilla.Rows.Count == 0) return;
        int col = Anio == DateTime.Today.Year ? ColPrimerMes + DateTime.Today.Month - 1 : ColPrimerMes;
        _grilla.CurrentCell = _grilla[col, 0];
    }

    /// <summary>Totales del mes de la celda seleccionada (o del mes actual).</summary>
    private void ActualizarResumen()
    {
        int m = CeldaActual()?.mes ?? (Anio == DateTime.Today.Year ? DateTime.Today.Month - 1 : 0);
        int pagaron = _filas.Count(f => f.Pagos[m] is not null);
        int deben = _filas.Count(f => f.Estados[m] == EstadoCuota.Debe);
        int pendientes = _filas.Count(f => f.Estados[m] == EstadoCuota.Pendiente && f.Pagos[m] is null);
        decimal total = _filas.Sum(f => f.Pagos[m]?.Monto ?? 0m);
        string filtro = _buscar.Text.Trim().Length > 0 ? " (filtrado)" : "";
        _resumen.Text = $"{VistasComun.Meses[m]} {Anio}{filtro}:  pagaron {pagaron}  ·  deben {deben}" +
                        (pendientes > 0 ? $"  ·  sin pagar todavía {pendientes}" : "") +
                        $"  ·  ingresado {Formato.Moneda(total)}";
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.F5:
                Recargar();
                return true;
            case Keys.Enter when _grilla.Focused && _grilla.CurrentCell is { } c:
                AccionCelda(c.RowIndex, c.ColumnIndex);
                return true;
            case Keys.Control | Keys.F:
                _buscar.Focus();
                _buscar.SelectAll();
                return true;
            case Keys.Escape:
                Close();
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
