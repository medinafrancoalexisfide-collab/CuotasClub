using System.Globalization;
using CuotasClub.Data;
using CuotasClub.Models;
using CuotasClub.Repositories;
using CuotasClub.Services;

namespace CuotasClub.Views;

/// <summary>
/// Carga y listado mensual de gastos (Transporte, Materiales, Otros).
/// Los gastos son privados: quedan solo en SQLite y nunca se sincronizan con Google Sheets.
/// Uso rápido: elegir fecha, escribir descripción y monto, Enter para guardar.
/// Doble clic en una fila para editarla; Supr para eliminarla.
/// </summary>
public class GastosForm : VentanaBase
{
    // Formato argentino explícito, sin depender de la configuración regional de Windows.
    private static readonly NumberFormatInfo NumeroAr = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = ".",
        NumberGroupSizes = [3]
    };

    private readonly GastoRepository _repo;

    // Selección de mes
    private readonly ComboBox _cmbMes = Tema.Campo(new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList }, 130);
    private readonly NumericUpDown _numAnio = Tema.Campo(new NumericUpDown { Minimum = 2020, Maximum = 2100, TextAlign = HorizontalAlignment.Center }, 76);
    private readonly Button _btnMesAnterior = Tema.BotonFlecha("◀");
    private readonly Button _btnMesSiguiente = Tema.BotonFlecha("▶");

    // Carga
    private readonly Label _tituloCarga = Tema.Seccion("Nuevo gasto");
    private readonly DateTimePicker _dtpFecha = new() { Format = DateTimePickerFormat.Short, Width = 120 };
    private readonly ComboBox _cmbCategoria = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 130 };
    private readonly TextBox _txtDescripcion = new() { Width = 300 };
    private readonly TextBox _txtMonto = new() { Width = 110, TextAlign = HorizontalAlignment.Right, PlaceholderText = "0" };
    private readonly Button _btnGuardar = Tema.BotonPrimario("Agregar");
    private readonly Button _btnCancelar = Tema.BotonSecundario("Cancelar");

    // Listado
    private readonly DataGridView _grilla = new();
    private readonly Button _btnEliminar = Tema.BotonPeligro("Eliminar");
    private readonly Label _lblTotal = new() { AutoSize = true, Font = Tema.FuenteSeccion, ForeColor = Tema.Texto };
    private readonly Label _lblPorCategoria = new() { AutoSize = true, ForeColor = Tema.TextoSuave };

    private List<Gasto> _gastos = [];
    private Gasto? _enEdicion;
    private bool _cargando;

    public GastosForm(Database db) : this(db, DateTime.Today.Year, DateTime.Today.Month) { }

    public GastosForm(Database db, int anio, int mes)
    {
        _repo = new GastoRepository(db);

        Text = "Gastos";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 560);
        Size = new Size(1060, 680);
        KeyPreview = true;

        ArmarPantalla();

        _cargando = true;
        _cmbMes.Items.AddRange(Enumerable.Range(1, 12)
            .Select(m => (object)Formato.NombreMes(m)).ToArray());
        _cmbCategoria.Items.AddRange(Enum.GetNames<CategoriaGasto>());
        _cmbCategoria.SelectedIndex = 0;
        _numAnio.Value = Math.Clamp(anio, (int)_numAnio.Minimum, (int)_numAnio.Maximum);
        _cmbMes.SelectedIndex = mes - 1;
        _cargando = false;

        CargarAutocompletado();
        Recargar();
        LimpiarCarga();
    }

    private void ArmarPantalla()
    {
        // --- Fila 1: mes mostrado
        var panelMes = Tema.Barra();
        _btnMesAnterior.Margin = new Padding(0, 2, 4, 2);
        _cmbMes.Margin = new Padding(0, 4, 4, 2);
        _numAnio.Margin = new Padding(0, 4, 4, 2);
        panelMes.Controls.AddRange([Tema.Etiqueta("Mes"), _btnMesAnterior, _cmbMes, _numAnio, _btnMesSiguiente]);

        // --- Fila 2: carga de un gasto, en una tarjeta blanca
        var panelCarga = new FlowLayoutPanel { Dock = DockStyle.Top, WrapContents = true, AutoSize = true, Padding = new Padding(14, 4, 14, 10) };
        panelCarga.Controls.AddRange([
            CampoConTitulo("Fecha", _dtpFecha),
            CampoConTitulo("Categoría", _cmbCategoria),
            CampoConTitulo("Descripción", _txtDescripcion),
            CampoConTitulo("Monto $", _txtMonto),
            CampoConTitulo("\u00A0", _btnGuardar),
            CampoConTitulo("\u00A0", _btnCancelar),
        ]);
        _btnCancelar.Parent!.Visible = false;
        _tituloCarga.Dock = DockStyle.Top;
        _tituloCarga.Padding = new Padding(14, 12, 0, 0);
        var tarjetaCarga = Tema.TarjetaPanel();
        tarjetaCarga.Dock = DockStyle.Top;
        tarjetaCarga.AutoSize = true;
        tarjetaCarga.Controls.Add(panelCarga);
        tarjetaCarga.Controls.Add(_tituloCarga);
        var grupoCarga = new Panel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16, 6, 16, 6), BackColor = Tema.Fondo };
        grupoCarga.Controls.Add(tarjetaCarga);

        // Orden de tabulación: fecha → categoría → descripción → monto → guardar
        int orden = 0;
        foreach (Control c in panelCarga.Controls) c.TabIndex = orden++;

        // --- Grilla
        Tema.EstilarGrilla(_grilla);
        _grilla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grilla.MultiSelect = false;
        _grilla.Columns.Add(new DataGridViewTextBoxColumn { Name = "Fecha", HeaderText = "Fecha", Width = 110, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grilla.Columns.Add(new DataGridViewTextBoxColumn { Name = "Categoria", HeaderText = "Categoría", Width = 140, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grilla.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "Descripcion", HeaderText = "Descripción", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.NotSortable });
        _grilla.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = "Monto", HeaderText = "Monto", Width = 140, SortMode = DataGridViewColumnSortMode.NotSortable,
            DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight, Font = Tema.FuenteSemibold },
            HeaderCell = { Style = { Alignment = DataGridViewContentAlignment.MiddleRight } },
        });

        // --- Pie: totales y eliminar
        var totales = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = Padding.Empty };
        totales.Controls.Add(_lblTotal);
        totales.Controls.Add(_lblPorCategoria);
        totales.Controls.Add(Tema.Nota("Privado: los gastos no se publican en la planilla del tesorero."));
        _btnEliminar.Margin = Padding.Empty;

        Controls.Add(Tema.Contenedor(_grilla));
        Controls.Add(Tema.Pie(totales, _btnEliminar));
        Controls.Add(grupoCarga);
        Controls.Add(panelMes);
        Controls.Add(Tema.Encabezado("Gastos", "Elegí la fecha, escribí descripción y monto, y Enter para guardar · Doble clic para editar · Supr para eliminar"));

        AcceptButton = _btnGuardar;

        // --- Eventos
        _cmbMes.SelectedIndexChanged += (_, _) => { if (!_cargando) Recargar(); };
        _numAnio.ValueChanged += (_, _) => { if (!_cargando) Recargar(); };
        _btnMesAnterior.Click += (_, _) => MoverMes(-1);
        _btnMesSiguiente.Click += (_, _) => MoverMes(+1);
        _btnGuardar.Click += (_, _) => Guardar();
        _btnCancelar.Click += (_, _) => LimpiarCarga();
        _btnEliminar.Click += (_, _) => EliminarSeleccionado();
        _grilla.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditarFila(e.RowIndex); };
        _grilla.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Delete) { EliminarSeleccionado(); e.Handled = true; }
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape && _enEdicion is not null) { LimpiarCarga(); e.Handled = true; }
        };
    }

    /// <summary>Campo con su título arriba (formulario de carga).</summary>
    private static Control CampoConTitulo(string titulo, Control campo)
    {
        var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Margin = new Padding(0, 0, 12, 0) };
        p.Controls.Add(new Label { Text = titulo, AutoSize = true, ForeColor = Tema.TextoSuave, Font = Tema.FuenteChica, Margin = new Padding(0, 0, 0, 3) });
        campo.Margin = new Padding(0);
        p.Controls.Add(campo);
        return p;
    }

    private int AnioSeleccionado => (int)_numAnio.Value;
    private int MesSeleccionado => _cmbMes.SelectedIndex + 1;

    private void MoverMes(int delta)
    {
        var f = new DateTime(AnioSeleccionado, MesSeleccionado, 1).AddMonths(delta);
        if (f.Year < _numAnio.Minimum || f.Year > _numAnio.Maximum) return;
        MostrarMes(f.Year, f.Month);
    }

    private void MostrarMes(int anio, int mes)
    {
        _cargando = true;
        _numAnio.Value = anio;
        _cmbMes.SelectedIndex = mes - 1;
        _cargando = false;
        Recargar();
    }

    private void Recargar()
    {
        try
        {
            _gastos = _repo.ListarPorMes(AnioSeleccionado, MesSeleccionado);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudieron leer los gastos:\n" + ex.Message, "Gastos",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            _gastos = [];
        }

        _grilla.Rows.Clear();
        foreach (var g in _gastos)
        {
            int i = _grilla.Rows.Add(
                Formato.Fecha(g.Fecha),
                g.Categoria.ToString(),
                g.Descripcion,
                Formato.Moneda(g.Monto));
            _grilla.Rows[i].Tag = g;
        }

        decimal total = _gastos.Sum(g => g.Monto);
        _lblTotal.Text = $"Total {_cmbMes.Text.ToLowerInvariant()} {AnioSeleccionado}: {Formato.Moneda(total)}"
                         + $"  ({_gastos.Count} {(_gastos.Count == 1 ? "gasto" : "gastos")})";
        _lblPorCategoria.Text = string.Join("   ·   ", Enum.GetValues<CategoriaGasto>()
            .Select(c => $"{c}: {Formato.Moneda(_gastos.Where(g => g.Categoria == c).Sum(g => g.Monto))}"));
        _btnEliminar.Enabled = _gastos.Count > 0;
    }

    private void CargarAutocompletado()
    {
        var sugerencias = new AutoCompleteStringCollection();
        // Solo se sugieren descripciones que el profesor ya cargó (sin sugerencias fijas).
        try
        {
            sugerencias.AddRange(_repo.DescripcionesFrecuentes().ToArray());
        }
        catch
        {
            // El autocompletado es una ayuda: si falla, se sigue sin sugerencias.
        }
        _txtDescripcion.AutoCompleteSource = AutoCompleteSource.CustomSource;
        _txtDescripcion.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _txtDescripcion.AutoCompleteCustomSource = sugerencias;
    }

    private void LimpiarCarga()
    {
        _enEdicion = null;
        // Por defecto: hoy si se está viendo el mes actual, si no el día 1 del mes mostrado.
        var hoy = DateTime.Today;
        _dtpFecha.Value = hoy.Year == AnioSeleccionado && hoy.Month == MesSeleccionado
            ? hoy
            : new DateTime(AnioSeleccionado, MesSeleccionado, 1);
        _cmbCategoria.SelectedItem = nameof(CategoriaGasto.Transporte);
        _txtDescripcion.Clear();
        _txtMonto.Clear();
        _btnGuardar.Text = "Agregar";
        _btnCancelar.Parent!.Visible = false;
        _tituloCarga.Text = "Nuevo gasto";
        _txtDescripcion.Focus();
    }

    private void EditarFila(int fila)
    {
        if (_grilla.Rows[fila].Tag is not Gasto g) return;
        _enEdicion = g;
        _dtpFecha.Value = g.Fecha.ToDateTime(TimeOnly.MinValue);
        _cmbCategoria.SelectedItem = g.Categoria.ToString();
        _txtDescripcion.Text = g.Descripcion;
        _txtMonto.Text = Formato.Moneda(g.Monto).Replace("$ ", "");
        _btnGuardar.Text = "Guardar";
        _btnCancelar.Parent!.Visible = true;
        _tituloCarga.Text = "Editando gasto (Esc para cancelar)";
        _txtMonto.Focus();
        _txtMonto.SelectAll();
    }

    private void Guardar()
    {
        if (!TryLeerMonto(_txtMonto.Text, out decimal monto) || monto <= 0)
        {
            MessageBox.Show(this, "Ingresá un monto mayor a cero. Ejemplos: 3500, 3.500 o 3.500,50.",
                "Monto inválido", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtMonto.Focus();
            _txtMonto.SelectAll();
            return;
        }

        string descripcion = _txtDescripcion.Text.Trim();
        if (descripcion.Length == 0)
        {
            MessageBox.Show(this, "Escribí una descripción del gasto.",
                "Falta la descripción", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtDescripcion.Focus();
            return;
        }

        var gasto = _enEdicion ?? new Gasto();
        gasto.Fecha = DateOnly.FromDateTime(_dtpFecha.Value);
        gasto.Categoria = Enum.Parse<CategoriaGasto>((string)_cmbCategoria.SelectedItem!);
        gasto.Descripcion = descripcion;
        gasto.Monto = monto;

        try
        {
            if (_enEdicion is null) _repo.Insertar(gasto);
            else _repo.Actualizar(gasto);
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "Gastos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo guardar el gasto:\n" + ex.Message, "Gastos",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        if (!_txtDescripcion.AutoCompleteCustomSource.Contains(descripcion))
            _txtDescripcion.AutoCompleteCustomSource.Add(descripcion);

        // Si el gasto es de otro mes, se muestra ese mes para que se vea dónde quedó.
        if (gasto.Fecha.Year != AnioSeleccionado || gasto.Fecha.Month != MesSeleccionado)
            MostrarMes(gasto.Fecha.Year, gasto.Fecha.Month);
        else
            Recargar();

        SeleccionarGasto(gasto.Id);
        var fechaUsada = _dtpFecha.Value;
        LimpiarCarga();
        // Al cargar varios gastos seguidos, se mantiene la última fecha usada.
        _dtpFecha.Value = fechaUsada;
    }

    private void SeleccionarGasto(int id)
    {
        foreach (DataGridViewRow r in _grilla.Rows)
        {
            if (r.Tag is Gasto g && g.Id == id)
            {
                r.Selected = true;
                _grilla.CurrentCell = r.Cells[0];
                return;
            }
        }
    }

    private void EliminarSeleccionado()
    {
        if (_grilla.CurrentRow?.Tag is not Gasto g) return;
        var r = MessageBox.Show(this,
            $"¿Eliminar el gasto del {Formato.Fecha(g.Fecha)} \"{g.Descripcion}\" por {Formato.Moneda(g.Monto)}?",
            "Eliminar gasto", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
        if (r != DialogResult.Yes) return;

        try
        {
            _repo.Eliminar(g.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo eliminar el gasto:\n" + ex.Message, "Gastos",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        if (_enEdicion?.Id == g.Id) LimpiarCarga();
        Recargar();
    }

    /// <summary>
    /// Lee montos escritos a mano: "3500", "3.500", "3.500,50", "$ 3.500", "3500,5" o "3500.50".
    /// Si hay coma, se toma formato argentino. Si solo hay puntos, un punto seguido de exactamente
    /// 3 dígitos se toma como separador de miles; si no, como decimal.
    /// </summary>
    public static bool TryLeerMonto(string texto, out decimal monto)
    {
        monto = 0;
        string s = texto.Replace("$", "").Replace(" ", "").Trim();
        if (s.Length == 0) return false;

        if (!s.Contains(','))
        {
            var partes = s.Split('.');
            bool sonMiles = partes.Length > 1 && partes.Skip(1).All(p => p.Length == 3);
            s = sonMiles ? s.Replace(".", "") : s.Replace('.', ',');
        }
        return decimal.TryParse(s, NumberStyles.AllowThousands | NumberStyles.AllowDecimalPoint, NumeroAr, out monto)
               && decimal.Round(monto, 2) == monto;
    }
}
