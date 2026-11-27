using CuotasClub.Data;
using CuotasClub.Models;
using CuotasClub.Repositories;
using CuotasClub.Services;

namespace CuotasClub.Views;

/// <summary>
/// Listado de alumnos ordenado por apellido, con búsqueda y alta / edición / baja.
/// Atajos: Ctrl+N o Insert = nuevo, Enter o doble clic = editar, Ctrl+F = buscar.
/// </summary>
public class AlumnosForm : VentanaBase
{
    private readonly Database _db;
    private readonly AlumnoRepository _alumnos;

    private readonly TextBox _txtBuscar = Tema.Campo(new TextBox { PlaceholderText = "Buscar por nombre, apellido o DNI" }, 280);
    private readonly CheckBox _chkTodos = new() { Text = "Mostrar también los dados de baja", AutoSize = true, Margin = new Padding(4, 9, 16, 0) };
    private readonly Button _btnNuevo = Tema.BotonPrimario("+  &Nuevo alumno");
    private readonly Button _btnEditar = Tema.BotonSecundario("&Editar");
    private readonly Button _btnBaja = Tema.BotonSecundario("Dar de &baja");
    private readonly Button _btnReactivar = Tema.BotonSecundario("&Reactivar");
    private readonly DataGridView _grilla = new();
    private readonly Label _lblEstado = new() { AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(0, 6, 0, 0) };

    private List<Alumno> _lista = [];

    public AlumnosForm(Database db)
    {
        _db = db;
        _alumnos = new AlumnoRepository(db);

        Text = "Alumnos";
        Size = new Size(1180, 660);
        MinimumSize = new Size(820, 440);
        KeyPreview = true;

        var barra = Tema.Barra();
        barra.Controls.AddRange([_txtBuscar, _chkTodos, _btnNuevo, _btnEditar, _btnBaja, _btnReactivar]);

        ConfigurarGrilla();

        Controls.Add(Tema.Contenedor(_grilla));
        Controls.Add(Tema.Pie(_lblEstado));
        Controls.Add(barra);
        Controls.Add(Tema.Encabezado("Alumnos", "Doble clic o Enter para editar · Ctrl+N alumno nuevo · Ctrl+F buscar"));

        _txtBuscar.TextChanged += (_, _) => Recargar();
        _chkTodos.CheckedChanged += (_, _) => Recargar();
        _btnNuevo.Click += (_, _) => Nuevo();
        _btnEditar.Click += (_, _) => EditarSeleccionado();
        _btnBaja.Click += (_, _) => DarDeBajaSeleccionado();
        _btnReactivar.Click += (_, _) => ReactivarSeleccionado();
        _grilla.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) EditarSeleccionado(); };
        _grilla.SelectionChanged += (_, _) => ActualizarBotones();
        _txtBuscar.KeyDown += TxtBuscar_KeyDown;

        Load += (_, _) => { Recargar(); _txtBuscar.Focus(); };
    }

    private void ConfigurarGrilla()
    {
        VistasComun.EstilarGrilla(_grilla);
        _grilla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grilla.MultiSelect = false;
        var centro = DataGridViewContentAlignment.MiddleCenter;
        _grilla.Columns.AddRange(
            VistasComun.Columna("Apellido", "Apellido", 140),
            VistasComun.Columna("Nombre", "Nombre", 140),
            VistasComun.Columna("Dni", "DNI", 90, centro),
            VistasComun.Columna("Nacimiento", "Nacimiento", 95, centro),
            VistasComun.Columna("Edad", "Edad", 50, centro),
            VistasComun.Columna("Categoria", "Categoría", 75, centro),
            VistasComun.Columna("Tipo", "Socio", 65, centro),
            VistasComun.Columna("Fide", "ID FIDE", 85, centro),
            VistasComun.Columna("Telefono", "Teléfono", 110),
            VistasComun.Columna("Tutor", "Tutor", 150),
            VistasComun.Columna("Alta", "Alta", 90, centro),
            VistasComun.Columna("Baja", "Baja", 160, centro));
        _grilla.Columns["Tutor"]!.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
    }

    private void Recargar(int? seleccionarId = null)
    {
        seleccionarId ??= Seleccionado()?.Id;
        var texto = string.IsNullOrWhiteSpace(_txtBuscar.Text) ? null : _txtBuscar.Text.Trim();
        try
        {
            // Ordenado por apellido (en español, sin distinguir acentos) y filtrado por el repositorio.
            _lista = _alumnos.Listar(soloActivos: !_chkTodos.Checked, texto);
        }
        catch (Exception ex)
        {
            VistasComun.Error(this, "No se pudo leer la lista de alumnos.\n\n" + ex.Message);
            _lista = [];
        }

        var hoy = VistasComun.Hoy;
        _grilla.SuspendLayout();
        _grilla.Rows.Clear();
        foreach (var a in _lista)
        {
            int i = _grilla.Rows.Add(
                a.Apellido, a.Nombre, a.Dni,
                VistasComun.Fecha(a.FechaNacimiento),
                CategoriaService.CalcularEdad(a.FechaNacimiento, hoy),
                CategoriaService.CalcularCategoria(a.FechaNacimiento, hoy.Year),
                a.TipoSocio.ATexto(),
                a.IdFide ?? "",
                a.CelularContacto ?? "",
                a.Tutor ?? "",
                VistasComun.Fecha(a.FechaAlta),
                BajaTexto(a));
            var fila = _grilla.Rows[i];
            fila.Tag = a;
            if (EstaDadoDeBaja(a, hoy))
                fila.DefaultCellStyle.ForeColor = Tema.NoCorrespondeTexto;
            if (a.Id == seleccionarId)
            {
                fila.Selected = true;
                _grilla.CurrentCell = fila.Cells[0];
            }
        }
        _grilla.ResumeLayout();

        int activos = _lista.Count(a => !EstaDadoDeBaja(a, hoy));
        _lblEstado.Text = _chkTodos.Checked
            ? $"{_lista.Count} alumnos ({activos} activos)"
            : $"{_lista.Count} alumnos activos";
        ActualizarBotones();
    }

    private static bool EstaDadoDeBaja(Alumno a, DateOnly hoy) =>
        (a.FechaBaja is { } b && b <= hoy) || a.Ausencias.Any(x => x.FechaBaja < hoy && hoy < x.FechaRegreso);

    /// <summary>Columna "Baja": la baja actual o, si volvió, sus ausencias ("10/03/2027 → 02/05/2027").</summary>
    private static string BajaTexto(Alumno a)
    {
        var partes = a.Ausencias.Select(x => $"{VistasComun.Fecha(x.FechaBaja)} → {VistasComun.Fecha(x.FechaRegreso)}").ToList();
        if (a.FechaBaja is { } b) partes.Add(VistasComun.Fecha(b));
        return string.Join("; ", partes);
    }

    private Alumno? Seleccionado() => _grilla.CurrentRow?.Tag as Alumno;

    private void ActualizarBotones()
    {
        var a = Seleccionado();
        _btnEditar.Enabled = a is not null;
        _btnBaja.Enabled = a is not null; // con baja: permite corregir la fecha
        _btnBaja.Text = a?.FechaBaja is null ? "Dar de &baja" : "Cambiar fecha de &baja";
        _btnReactivar.Enabled = a is not null && a.FechaBaja is not null;
    }

    private void Nuevo()
    {
        bool otro;
        do
        {
            using var f = new AlumnoEditarForm(_db, null);
            if (f.ShowDialog(this) != DialogResult.OK) return;
            _txtBuscar.Clear();
            Recargar(f.AlumnoId);
            otro = f.CargarOtro;
        } while (otro);
    }

    private void EditarSeleccionado()
    {
        if (Seleccionado() is not { } a) return;
        var actual = _alumnos.ObtenerPorId(a.Id) ?? a;
        using var f = new AlumnoEditarForm(_db, actual);
        if (f.ShowDialog(this) == DialogResult.OK) Recargar(a.Id);
    }

    private void DarDeBajaSeleccionado()
    {
        if (Seleccionado() is not { } a) return;
        using var f = new FechaBajaForm(a);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            _alumnos.DarDeBaja(a.Id, f.Fecha);
            new ConfiguracionService(_db).SincronizacionPendiente = true; // cambian los "—" de la planilla
            Recargar(a.Id);
        }
        catch (Exception ex)
        {
            VistasComun.Error(this, "No se pudo dar de baja.\n\n" + ex.Message);
        }
    }

    private void ReactivarSeleccionado()
    {
        if (Seleccionado() is not { } a) return;
        using var f = new ReactivarForm(a);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            if (f.BajaPorError) _alumnos.QuitarBaja(a.Id);
            else _alumnos.Reactivar(a.Id, f.FechaRegreso);
            new ConfiguracionService(_db).SincronizacionPendiente = true; // cambian los "—" de la planilla
            Recargar(a.Id);
        }
        catch (Exception ex)
        {
            VistasComun.Error(this, "No se pudo reactivar.\n\n" + ex.Message);
        }
    }

    private void TxtBuscar_KeyDown(object? sender, KeyEventArgs e)
    {
        // Flechas desde el buscador mueven la selección de la grilla; Enter edita.
        if (e.KeyCode is Keys.Down or Keys.Up && _grilla.Rows.Count > 0)
        {
            _grilla.Focus();
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Enter)
        {
            EditarSeleccionado();
            e.Handled = e.SuppressKeyPress = true;
        }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.N:
            case Keys.Insert:
                Nuevo();
                return true;
            case Keys.Control | Keys.F:
                _txtBuscar.Focus();
                _txtBuscar.SelectAll();
                return true;
            case Keys.Enter when _grilla.Focused:
                EditarSeleccionado();
                return true;
            case Keys.Escape:
                Close();
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}

/// <summary>Pide la fecha de baja de un alumno.</summary>
internal class FechaBajaForm : DialogoBase
{
    private readonly DateTimePicker _fecha = new() { Format = DateTimePickerFormat.Short, Width = 140, Margin = new Padding(0, 2, 8, 0) };
    public DateOnly Fecha => VistasComun.ADateOnly(_fecha.Value);

    public FechaBajaForm(Alumno a)
    {
        Text = a.FechaBaja is null ? "Dar de baja" : "Cambiar fecha de baja";

        var titulo = new Label { Text = a.NombreCompleto, AutoSize = true, Font = Tema.FuenteSeccion, Dock = DockStyle.Top };
        var lbl = new Label
        {
            Text = "Desde el mes de la baja figura como \"—\" y ese mes no se cobra.\n" +
                   "Para cobrar el mes en que se va, poné el último día del mes.",
            AutoSize = true,
            ForeColor = Tema.TextoSuave,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 6, 0, 12),
        };
        _fecha.MinDate = VistasComun.ADateTime(a.FechaAlta);
        var inicial = a.FechaBaja is { } b ? VistasComun.ADateTime(b) : DateTime.Today;
        _fecha.Value = inicial < _fecha.MinDate ? _fecha.MinDate : inicial;

        var finDeMes = Tema.BotonSecundario("Último día del mes");
        finDeMes.Click += (_, _) =>
        {
            var d = _fecha.Value.Date;
            _fecha.Value = new DateTime(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month));
        };
        var fila = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Top };
        fila.Controls.AddRange([Tema.Etiqueta("Fecha de baja"), _fecha, finDeMes]);

        var ok = Tema.BotonPrimario("Guardar baja");
        ok.DialogResult = DialogResult.OK;
        var cancelar = Tema.BotonSecundario("Cancelar");
        cancelar.DialogResult = DialogResult.Cancel;

        Controls.Add(Botones(cancelar, ok));
        Controls.Add(fila);
        Controls.Add(lbl);
        Controls.Add(titulo);
        AcceptButton = ok;
        CancelButton = cancelar;
    }
}

/// <summary>
/// Reactivar: pide la fecha en que vuelve. Los meses entre la baja y el regreso quedan en "—" (no se cobran)
/// y se cobra desde el mes de regreso. Si la baja fue un error, se puede borrar sin dejar hueco.
/// </summary>
internal class ReactivarForm : DialogoBase
{
    private readonly DateTimePicker _fecha = new() { Format = DateTimePickerFormat.Short, Width = 140, Margin = new Padding(0, 2, 8, 0) };
    private readonly CheckBox _porError = new()
    {
        Text = "La baja fue un error: borrarla (cuenta como si nunca se hubiera ido)",
        AutoSize = true,
        Dock = DockStyle.Top,
        Padding = new Padding(0, 10, 0, 0),
    };
    public DateOnly FechaRegreso => VistasComun.ADateOnly(_fecha.Value);
    public bool BajaPorError => _porError.Checked;

    public ReactivarForm(Alumno a)
    {
        Text = "Reactivar alumno";
        var baja = a.FechaBaja!.Value;

        var titulo = new Label { Text = a.NombreCompleto, AutoSize = true, Font = Tema.FuenteSeccion, Dock = DockStyle.Top };
        var lbl = new Label
        {
            Text = $"Se fue el {VistasComun.Fecha(baja)}. Los meses que estuvo afuera quedan en \"—\":\n" +
                   "no son deuda y no se cobran. Se cobra desde el mes de regreso.",
            AutoSize = true,
            ForeColor = Tema.TextoSuave,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 6, 0, 12),
        };
        _fecha.MinDate = VistasComun.ADateTime(baja).AddDays(1);
        _fecha.Value = DateTime.Today < _fecha.MinDate ? _fecha.MinDate : DateTime.Today;
        _porError.CheckedChanged += (_, _) => _fecha.Enabled = !_porError.Checked;

        var fila = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Top };
        fila.Controls.AddRange([Tema.Etiqueta("Vuelve el"), _fecha]);

        var ok = Tema.BotonPrimario("Reactivar");
        ok.DialogResult = DialogResult.OK;
        var cancelar = Tema.BotonSecundario("Cancelar");
        cancelar.DialogResult = DialogResult.Cancel;

        Controls.Add(Botones(cancelar, ok));
        Controls.Add(_porError);
        Controls.Add(fila);
        Controls.Add(lbl);
        Controls.Add(titulo);
        AcceptButton = ok;
        CancelButton = cancelar;
    }
}
