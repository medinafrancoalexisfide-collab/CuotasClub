using CuotasClub.Data;
using CuotasClub.Models;
using CuotasClub.Services;

namespace CuotasClub.Views;

/// <summary>
/// Alta y edición de un alumno. Pensado para cargar rápido: Tab en el orden de los campos,
/// Enter guarda, Esc cancela. Las fechas se escriben como dd/mm/aaaa.
/// </summary>
public class AlumnoEditarForm : DialogoBase
{
    private readonly AlumnoService _servicio;
    private readonly Alumno? _original;

    private readonly TextBox _apellido = new();
    private readonly TextBox _nombre = new();
    private readonly TextBox _dni = new() { MaxLength = 10 };
    private readonly MaskedTextBox _nacimiento = new() { Mask = "00/00/0000", ValidatingType = null };
    private readonly Label _edadCategoria = new() { AutoSize = true, ForeColor = Tema.TextoSuave, Margin = new Padding(10, 7, 0, 0) };
    private readonly ComboBox _tipo = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _celular = new();
    private readonly TextBox _fide = new() { MaxLength = 12 };
    private readonly TextBox _respNombre = new();
    private readonly TextBox _respCelular = new();
    private readonly MaskedTextBox _alta = new() { Mask = "00/00/0000" };
    private readonly TextBox _observaciones = new() { Multiline = true, Height = 64, ScrollBars = ScrollBars.Vertical };
    private readonly Button _btnGuardar = Tema.BotonPrimario("&Guardar");
    private readonly Button _btnGuardarOtro = Tema.BotonSecundario("Guardar y cargar &otro");
    private readonly Button _btnCancelar = Tema.BotonSecundario("Cancelar");
    private readonly ErrorProvider _errores = new() { BlinkStyle = ErrorBlinkStyle.NeverBlink };

    private bool _tipoElegidoAMano;

    // Filas que cambian según el tipo de socio: Cadete → tutor y teléfono del tutor; Activo → teléfono del alumno.
    private Label _lblCelular = null!, _lblRespNombre = null!, _lblRespCelular = null!;

    /// <summary>Id del alumno guardado (nuevo o editado).</summary>
    public int AlumnoId { get; private set; }

    /// <summary>true si el usuario eligió "Guardar y cargar otro".</summary>
    public bool CargarOtro { get; private set; }

    public AlumnoEditarForm(Database db, Alumno? alumno)
    {
        _servicio = new AlumnoService(db);
        _original = alumno;

        Text = alumno is null ? "Nuevo alumno" : $"Editar alumno – {alumno.NombreCompleto}";
        _btnCancelar.DialogResult = DialogResult.Cancel;

        _tipo.Items.AddRange([TipoSocio.Cadete.ATexto(), TipoSocio.Activo.ToString()]);

        var tabla = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top };
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        tabla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));

        Fila(tabla, "Apellido *", _apellido);
        Fila(tabla, "Nombre *", _nombre);
        Fila(tabla, "DNI *", _dni, ancho: 120);
        var nac = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        _nacimiento.Width = 100;
        nac.Controls.AddRange([_nacimiento, _edadCategoria]);
        Fila(tabla, "Fecha de nacimiento *", nac);
        Fila(tabla, "Tipo de socio *", _tipo, ancho: 120);
        _lblRespNombre = Fila(tabla, "Tutor", _respNombre);
        _lblRespCelular = Fila(tabla, "Teléfono del tutor", _respCelular, ancho: 160);
        _lblCelular = Fila(tabla, "Teléfono", _celular, ancho: 160);
        Fila(tabla, "ID FIDE", _fide, ancho: 120);
        Fila(tabla, "Fecha de alta *", _alta, ancho: 100);
        Fila(tabla, "Observaciones", _observaciones);

        var nota = new Label
        {
            Text = "* obligatorio · Fechas dd/mm/aaaa · Enter guarda · Esc cancela",
            AutoSize = true,
            ForeColor = Tema.TextoSuave,
            Font = Tema.FuenteChica,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 8, 0, 0),
        };
        var titulo = new Label
        {
            Text = alumno is null ? "Nuevo alumno" : alumno.NombreCompleto,
            AutoSize = true,
            Font = Tema.FuenteSeccion,
            Dock = DockStyle.Top,
            Padding = new Padding(0, 0, 0, 10),
        };

        var botones = alumno is null ? Botones(_btnCancelar, _btnGuardar, _btnGuardarOtro) : Botones(_btnCancelar, _btnGuardar);

        // Con Dock=Top el último agregado queda arriba.
        Controls.Add(botones);
        Controls.Add(nota);
        Controls.Add(tabla);
        Controls.Add(titulo);

        // Orden de tabulación explícito.
        Control[] orden = [_apellido, _nombre, _dni, _nacimiento, _tipo, _respNombre, _respCelular, _celular, _fide,
                           _alta, _observaciones, _btnGuardarOtro, _btnGuardar, _btnCancelar];
        for (int i = 0; i < orden.Length; i++) orden[i].TabIndex = i;
        tabla.TabIndex = 0; nac.TabIndex = 3; botones.TabIndex = 20;

        AcceptButton = _btnGuardar;
        CancelButton = _btnCancelar;

        _btnGuardar.Click += (_, _) => Guardar(cargarOtro: false);
        _btnGuardarOtro.Click += (_, _) => Guardar(cargarOtro: true);
        _nacimiento.TextChanged += (_, _) => ActualizarEdad();
        _tipo.SelectionChangeCommitted += (_, _) => _tipoElegidoAMano = true;
        _tipo.SelectedIndexChanged += (_, _) => MostrarCamposSegunTipo();
        _dni.KeyPress += (_, e) =>
        {
            // Solo números (se ignoran los puntos que suele tipear la gente).
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true;
        };
        foreach (var t in new[] { _celular, _respCelular })
            t.KeyPress += (_, e) =>
            {
                if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && e.KeyChar is not (' ' or '-' or '+' or '(' or ')'))
                    e.Handled = true;
            };
        _fide.KeyPress += (_, e) => { if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar)) e.Handled = true; };

        Cargar();
    }

    private static Label Fila(TableLayoutPanel t, string etiqueta, Control control, int? ancho = null)
    {
        var lbl = new Label
        {
            Text = etiqueta,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Tema.TextoSuave,
            Margin = new Padding(0, 6, 16, 0),
        };
        if (ancho is { } w) control.Width = w;
        else if (control is TextBox) control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 4, 0, 4);
        int fila = t.RowCount++;
        t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        t.Controls.Add(lbl, 0, fila);
        t.Controls.Add(control, 1, fila);
        return lbl;
    }

    private bool EsCadete => _tipo.SelectedItem as string == TipoSocio.Cadete.ToString();

    /// <summary>
    /// Cadete: muestra Tutor y Teléfono del tutor. Activo: solo Teléfono. Lo que estaba escrito en los campos
    /// ocultos no se borra (si se vuelve a cambiar el tipo, sigue ahí).
    /// </summary>
    private void MostrarCamposSegunTipo()
    {
        bool cadete = EsCadete;
        _lblRespNombre.Visible = _respNombre.Visible = cadete;
        _lblRespCelular.Visible = _respCelular.Visible = cadete;
        _lblCelular.Visible = _celular.Visible = !cadete;

        // Si cambia el tipo con un solo teléfono cargado, lo pasa al campo que ahora se ve.
        if (cadete && _respCelular.Text.Trim().Length == 0) _respCelular.Text = _celular.Text.Trim();
        if (!cadete && _celular.Text.Trim().Length == 0) _celular.Text = _respCelular.Text.Trim();

        if (IsHandleCreated)
        {
            // Recalcula el alto de la ventana (con las filas ocultas queda más corta).
            MinimumSize = Size.Empty;
            AjustarAlContenido();
        }
    }

    private void Cargar()
    {
        if (_original is not { } a)
        {
            _tipo.SelectedItem = TipoSocio.Cadete.ATexto();
            _alta.Text = VistasComun.Fecha(VistasComun.Hoy);
            ActualizarEdad();
            return;
        }

        _tipoElegidoAMano = true;
        _apellido.Text = a.Apellido;
        _nombre.Text = a.Nombre;
        _dni.Text = a.Dni;
        _nacimiento.Text = VistasComun.Fecha(a.FechaNacimiento);
        _tipo.SelectedItem = a.TipoSocio.ToString();
        _celular.Text = a.Celular ?? "";
        _fide.Text = a.IdFide ?? "";
        _respNombre.Text = a.ResponsableNombre ?? "";
        _respCelular.Text = a.ResponsableCelular ?? "";
        _alta.Text = VistasComun.Fecha(a.FechaAlta);
        _observaciones.Text = a.Observaciones ?? "";
        ActualizarEdad();
    }

    private void ActualizarEdad()
    {
        if (!VistasComun.TryParseFecha(_nacimiento.Text, out var nac) || nac > VistasComun.Hoy)
        {
            _edadCategoria.Text = "";
            return;
        }
        var hoy = VistasComun.Hoy;
        int edad = CategoriaService.CalcularEdad(nac, hoy);
        _edadCategoria.Text = $"{edad} años · {CategoriaService.CalcularCategoria(nac, hoy.Year)}";

        // Sugerencia: menores de 18 → Cadete, salvo que el tipo se haya elegido a mano.
        if (!_tipoElegidoAMano)
            _tipo.SelectedItem = (edad < 18 ? TipoSocio.Cadete : TipoSocio.Activo).ToString();
    }

    private static string? Vacio(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private void Guardar(bool cargarOtro)
    {
        _errores.Clear();
        var errores = new List<string>();

        void Marcar(Control c, string msg)
        {
            if (string.IsNullOrEmpty(_errores.GetError(c))) _errores.SetError(c, msg);
            errores.Add(msg);
        }

        string dni = new(_dni.Text.Where(char.IsDigit).ToArray());

        if (string.IsNullOrWhiteSpace(_apellido.Text)) Marcar(_apellido, "Falta el apellido.");
        if (string.IsNullOrWhiteSpace(_nombre.Text)) Marcar(_nombre, "Falta el nombre.");
        if (dni.Length is < 7 or > 8) Marcar(_dni, "El DNI debe tener 7 u 8 dígitos.");

        DateOnly nacimiento = default, alta = default;
        if (!VistasComun.TryParseFecha(_nacimiento.Text, out nacimiento))
            Marcar(_nacimiento, "La fecha de nacimiento no es válida (dd/mm/aaaa).");
        else if (nacimiento > VistasComun.Hoy || nacimiento.Year < 1920)
            Marcar(_nacimiento, "La fecha de nacimiento está fuera de rango.");

        if (!VistasComun.TryParseFecha(_alta.Text, out alta))
            Marcar(_alta, "La fecha de alta no es válida (dd/mm/aaaa).");
        else if (_original?.FechaBaja is { } baja && alta > baja)
            Marcar(_alta, "La fecha de alta no puede ser posterior a la fecha de baja.");

        if (_tipo.SelectedItem is null) Marcar(_tipo, "Elegí el tipo de socio.");

        var alumno = new Alumno
        {
            Id = _original?.Id ?? 0,
            Apellido = Capitalizar(_apellido.Text),
            Nombre = Capitalizar(_nombre.Text),
            Dni = dni,
            FechaNacimiento = nacimiento,
            TipoSocio = Enum.TryParse<TipoSocio>(_tipo.SelectedItem as string, out var tipo) ? tipo : TipoSocio.Cadete,
            Celular = Vacio(_celular.Text),
            IdFide = Vacio(_fide.Text),
            ResponsableNombre = Vacio(_respNombre.Text),
            ResponsableCelular = Vacio(_respCelular.Text),
            FechaAlta = alta,
            FechaBaja = _original?.FechaBaja,
            Observaciones = Vacio(_observaciones.Text),
        };

        if (errores.Count > 0)
        {
            VistasComun.Error(this, "Revisá estos datos:\n\n• " + string.Join("\n• ", errores));
            var primero = new Control[] { _apellido, _nombre, _dni, _nacimiento, _tipo, _alta }
                .FirstOrDefault(c => !string.IsNullOrEmpty(_errores.GetError(c)));
            primero?.Focus();
            return;
        }

        try
        {
            // El servicio vuelve a validar todo, incluido que el DNI no esté repetido.
            _servicio.Guardar(alumno);
            AlumnoId = alumno.Id;
        }
        catch (ReglaNegocioException ex)
        {
            VistasComun.Error(this, "Revisá estos datos:\n\n" + ex.Message);
            if (ex.Message.Contains("DNI")) _dni.Focus();
            return;
        }
        catch (Exception ex)
        {
            VistasComun.Error(this, "No se pudo guardar el alumno.\n\n" + ex.Message);
            return;
        }

        CargarOtro = cargarOtro;
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>"pérez garcía" → "Pérez García". Respeta lo que ya viene con mayúsculas.</summary>
    private static string Capitalizar(string texto)
    {
        texto = string.Join(' ', texto.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (texto.Any(char.IsUpper)) return texto;
        return VistasComun.Cultura.TextInfo.ToTitleCase(texto);
    }
}
