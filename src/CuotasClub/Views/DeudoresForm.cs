using CuotasClub.Data;
using CuotasClub.Models;
using CuotasClub.Services;

namespace CuotasClub.Views;

/// <summary>
/// Alumnos con meses vencidos sin pagar (estado Debe), con los meses, el monto y un teléfono de contacto.
/// Solo lectura: para cobrar se usa Cuotas (doble clic abre Cuotas filtrado por ese alumno, en el año del primer mes adeudado).
/// </summary>
public class DeudoresForm : VentanaBase
{
    private readonly Database _db;
    private readonly CuotaService _cuotas;

    private readonly TextBox _buscar = Tema.Campo(new TextBox { PlaceholderText = "Filtrar por nombre o DNI" }, 240);
    private readonly Button _copiar = Tema.BotonSecundario("Copiar lista");
    private readonly Button _whatsapp = Tema.BotonSecundario("Recordar por WhatsApp");
    private readonly Button _irACuotas = Tema.BotonPrimario("Ir a Cuotas");
    private readonly DataGridView _grilla = new();
    private readonly Label _lblCantidad = Tarjeta();
    private readonly Label _lblMonto = Tarjeta();
    private readonly Label _lblNota = new() { AutoSize = true, ForeColor = Tema.TextoSuave, Font = Tema.FuenteChica, Margin = new Padding(0, 6, 0, 0) };

    private List<Deudor> _deudores = [];

    public DeudoresForm(Database db)
    {
        _db = db;
        _cuotas = new CuotaService(db);

        Text = "Deudores";
        Size = new Size(1000, 640);
        MinimumSize = new Size(760, 440);
        KeyPreview = true;

        var tarjetas = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16, 12, 16, 0), BackColor = Tema.Fondo };
        tarjetas.Controls.AddRange([Envolver(_lblCantidad), Envolver(_lblMonto)]);

        var barra = Tema.Barra();
        barra.Controls.AddRange([_buscar, _copiar, _whatsapp, _irACuotas]);

        Tema.EstilarGrilla(_grilla);
        _grilla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grilla.MultiSelect = false;
        _grilla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        Columna("Alumno", "Alumno", 180, DataGridViewContentAlignment.MiddleLeft).DefaultCellStyle.Font = Tema.FuenteSemibold;
        Columna("Contacto", "Contacto", 170, DataGridViewContentAlignment.MiddleLeft);
        Columna("Meses", "Meses que debe", 260, DataGridViewContentAlignment.MiddleLeft);
        Columna("Cantidad", "Cant.", 60, DataGridViewContentAlignment.MiddleCenter);
        var monto = Columna("Monto", "Monto", 110, DataGridViewContentAlignment.MiddleRight);
        monto.DefaultCellStyle.Font = Tema.FuenteSemibold;
        monto.DefaultCellStyle.ForeColor = Tema.DebeTexto;
        monto.DefaultCellStyle.SelectionForeColor = Tema.DebeTexto;

        Controls.Add(Tema.Contenedor(_grilla));
        Controls.Add(Tema.Pie(_lblNota));
        Controls.Add(barra);
        Controls.Add(tarjetas);
        Controls.Add(Tema.Encabezado("Deudores",
            "Cuotas de meses ya terminados sin pagar. El mes en curso no cuenta como deuda hasta que termina."));

        _buscar.TextChanged += (_, _) => MostrarFiltrado();
        _copiar.Click += (_, _) => CopiarLista();
        _irACuotas.Click += (_, _) => AbrirCuotas();
        _whatsapp.Click += (_, _) => Recordar();
        _grilla.CellDoubleClick += (_, e) => { if (e.RowIndex >= 0) AbrirCuotas(); };
        _grilla.SelectionChanged += (_, _) => _irACuotas.Enabled = _whatsapp.Enabled = _grilla.CurrentRow is not null;

        Load += (_, _) => { Recargar(); _buscar.Focus(); };
    }

    private static Label Tarjeta() => new TarjetaDato();

    private static Panel Envolver(Label l)
    {
        var p = Tema.TarjetaPanel();
        p.Size = new Size(220, 70);
        p.Margin = new Padding(0, 0, 12, 0);
        p.Controls.Add(l);
        return p;
    }

    private DataGridViewTextBoxColumn Columna(string nombre, string titulo, int peso, DataGridViewContentAlignment alineacion)
    {
        var c = new DataGridViewTextBoxColumn
        {
            Name = nombre,
            HeaderText = titulo,
            FillWeight = peso,
            SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        c.DefaultCellStyle.Alignment = alineacion;
        _grilla.Columns.Add(c);
        return c;
    }

    private void Recargar()
    {
        try
        {
            _deudores = _cuotas.ObtenerDeudores(VistasComun.Hoy);
        }
        catch (Exception ex)
        {
            VistasComun.Error(this, "No se pudo calcular la lista de deudores.\n\n" + ex.Message);
            _deudores = [];
        }
        _lblCantidad.Text = $"Alumnos que deben\n{_deudores.Count}";
        _lblMonto.Text = $"Total adeudado\n{Formato.Moneda(_deudores.Sum(d => d.MontoAdeudado))}";
        _lblCantidad.ForeColor = _deudores.Count > 0 ? Tema.DebeTexto : Tema.Exito;
        _lblMonto.ForeColor = _deudores.Count > 0 ? Tema.DebeTexto : Tema.Exito;
        MostrarFiltrado();
    }

    private IEnumerable<Deudor> Filtrados()
    {
        var palabras = _buscar.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return _deudores.Where(d => palabras.All(p =>
            VistasComun.Cultura.CompareInfo.IndexOf(d.Alumno.NombreCompleto, p,
                System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace) >= 0
            || d.Alumno.Dni.Contains(p)));
    }

    private void MostrarFiltrado()
    {
        _grilla.Rows.Clear();
        foreach (var d in Filtrados())
        {
            int i = _grilla.Rows.Add(d.Alumno.NombreCompleto, Contacto(d.Alumno), TextoMeses(d.MesesAdeudados),
                d.CantidadMeses, Formato.Moneda(d.MontoAdeudado));
            _grilla.Rows[i].Tag = d;
        }
        _lblNota.Text = _deudores.Count == 0
            ? "Nadie debe cuotas vencidas."
            : "Doble clic en un alumno para ir a Cuotas y registrar el pago. El monto usa el precio vigente de cada mes.";
        _irACuotas.Enabled = _whatsapp.Enabled = _grilla.Rows.Count > 0;
        _copiar.Enabled = _grilla.Rows.Count > 0;
    }

    /// <summary>Cadete → teléfono del tutor (con su nombre); Activo → teléfono del alumno.</summary>
    private static string Contacto(Alumno a) => a.ContactoTexto;

    /// <summary>"Feb, Mar 2027 · Ene 2028".</summary>
    private static string TextoMeses(IReadOnlyList<(int Anio, int Mes)> meses) =>
        string.Join(" · ", meses.GroupBy(m => m.Anio)
            .Select(g => string.Join(", ", g.Select(m => VistasComun.MesesCortos[m.Mes - 1])) + " " + g.Key));

    private void CopiarLista()
    {
        var lineas = Filtrados().Select(d =>
            $"{d.Alumno.NombreCompleto}: {d.CantidadMeses} {(d.CantidadMeses == 1 ? "mes" : "meses")} ({TextoMeses(d.MesesAdeudados)}) · {Formato.Moneda(d.MontoAdeudado)}");
        try
        {
            Clipboard.SetText(string.Join(Environment.NewLine, lineas));
            _lblNota.Text = "Lista copiada: pegala donde quieras (WhatsApp, un mail…).";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            VistasComun.Error(this, "No se pudo copiar. Probá de nuevo.");
        }
    }

    /// <summary>Abre WhatsApp con el alumno (o el tutor) y un recordatorio amable de lo que debe.</summary>
    private void Recordar()
    {
        if (_grilla.CurrentRow?.Tag is not Deudor d) return;
        if (WhatsAppUi.AbrirChat(this, d.Alumno, WhatsApp.MensajeRecordatorio(d)))
            _lblNota.Text = $"Se abrió WhatsApp con el recordatorio para {d.Alumno.NombreCompleto}: revisalo y tocá enviar.";
    }

    private void AbrirCuotas()
    {
        if (_grilla.CurrentRow?.Tag is not Deudor d) return;
        using var f = new CuotasForm(_db, d.MesesAdeudados[0].Anio, d.Alumno.Dni);
        f.ShowDialog(this);
        Recargar(); // por si se registraron pagos
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.Control | Keys.F:
                _buscar.Focus();
                _buscar.SelectAll();
                return true;
            case Keys.F5:
                Recargar();
                return true;
            case Keys.Escape:
                Close();
                return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
}
