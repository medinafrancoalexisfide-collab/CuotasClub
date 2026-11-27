using System.Diagnostics;
using CuotasClub.Data;
using CuotasClub.Models;
using CuotasClub.Services;

namespace CuotasClub.Views;

/// <summary>
/// Registrar el pago de una cuota: fecha y medio de pago. El monto no se edita:
/// es el precio vigente de ese mes (no hay pagos parciales).
/// </summary>
public class RegistrarPagoForm : DialogoBase
{
    private static MedioPago _ultimoMedio = MedioPago.Efectivo;
    private static bool _ultimoGenerarRecibo = true;

    private readonly DateTimePicker _fecha = new() { Format = DateTimePickerFormat.Short, Width = 150 };
    private readonly ComboBox _medio = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly CheckBox _recibo = new() { Text = "Generar recibo en PDF", AutoSize = true };

    public DateOnly FechaPago => VistasComun.ADateOnly(_fecha.Value);
    private static readonly MedioPago[] Medios = Enum.GetValues<MedioPago>();
    public MedioPago MedioPago => Medios[Math.Max(0, _medio.SelectedIndex)];
    public bool GenerarRecibo => _recibo.Checked;

    public RegistrarPagoForm(Alumno alumno, int anio, int mes, decimal monto)
    {
        Text = "Registrar pago";

        foreach (var m in Medios) _medio.Items.Add(m.ATexto());
        _medio.SelectedIndex = Math.Max(0, Array.IndexOf(Medios, _ultimoMedio));
        _recibo.Checked = _ultimoGenerarRecibo;

        // Fecha por defecto: hoy (vale también para pagos atrasados o adelantados; se puede cambiar).
        _fecha.Value = DateTime.Today;
        _fecha.MaxDate = DateTime.Today;

        // Resumen arriba: alumno, cuota y monto grande
        var resumen = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Tema.PrimarioSuave, Padding = new Padding(16, 10, 16, 10) };
        var lblMonto = new Label { Text = Formato.Moneda(monto), Dock = DockStyle.Right, AutoSize = true, Font = new Font("Segoe UI Semibold", 20f), ForeColor = Tema.Primario, Padding = new Padding(16, 18, 0, 0) };
        var lblCuota = new Label { Text = $"Cuota de {VistasComun.Meses[mes - 1].ToLowerInvariant()} {anio}", Dock = DockStyle.Top, Height = 24, ForeColor = Tema.TextoSuave };
        var lblAlumno = new Label { Text = alumno.NombreCompleto, Dock = DockStyle.Top, Height = 34, Font = Tema.FuenteSeccion, ForeColor = Tema.Texto, AutoEllipsis = true };
        resumen.Controls.Add(lblCuota);
        resumen.Controls.Add(lblAlumno);
        resumen.Controls.Add(lblMonto);

        var t = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 14, 0, 0) };
        void Fila(string etiqueta, Control c)
        {
            int r = t.RowCount++;
            t.Controls.Add(new Label { Text = etiqueta, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 18, 0), ForeColor = Tema.TextoSuave }, 0, r);
            c.Margin = new Padding(0, 4, 0, 4);
            t.Controls.Add(c, 1, r);
        }

        Fila("Fecha de pago", _fecha);
        Fila("Medio de pago", _medio);
        Fila("", _recibo);

        var ok = Tema.BotonPrimario("&Registrar pago");
        var cancelar = Tema.BotonSecundario("Cancelar");
        cancelar.DialogResult = DialogResult.Cancel;

        Controls.Add(Botones(cancelar, ok));
        Controls.Add(t);
        Controls.Add(resumen);
        MinimumSize = new Size(440, 0);

        AcceptButton = ok;
        CancelButton = cancelar;
        ok.Click += (_, _) =>
        {
            if (FechaPago > VistasComun.Hoy)
            {
                VistasComun.Error(this, "La fecha de pago no puede ser futura.");
                return;
            }
            _ultimoMedio = MedioPago;
            _ultimoGenerarRecibo = GenerarRecibo;
            DialogResult = DialogResult.OK;
            Close();
        };
        Shown += (_, _) => _medio.Focus();
    }
}

/// <summary>Detalle de un pago ya registrado: volver a generar el recibo o anularlo.</summary>
public class DetallePagoForm : DialogoBase
{
    public enum Accion { Ninguna, Recibo, Anular }

    public Accion AccionElegida { get; private set; } = Accion.Ninguna;

    public DetallePagoForm(Alumno alumno, Pago pago)
    {
        Text = "Pago registrado";
        MinimumSize = new Size(420, 0);

        var estado = Tema.Chip("✓  Pagó", Tema.PagoFondo, Tema.PagoTexto);
        estado.Dock = DockStyle.Top;
        estado.AutoSize = false;
        estado.Height = 30;
        estado.TextAlign = ContentAlignment.MiddleLeft;
        var nombre = new Label { Text = alumno.NombreCompleto, Dock = DockStyle.Top, AutoSize = true, Font = Tema.FuenteSeccion, Padding = new Padding(0, 10, 0, 4) };
        var info = new Label
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ForeColor = Tema.TextoSuave,
            Text = $"Cuota {VistasComun.Meses[pago.Mes - 1]} {pago.Anio}: {Formato.Moneda(pago.Monto)}\n" +
                   $"Pagado el {VistasComun.Fecha(pago.FechaPago)} · {pago.MedioPago.ATexto()}",
        };

        var recibo = Tema.BotonPrimario("Generar &recibo");
        var anular = Tema.BotonPeligro("&Anular pago…");
        var cerrar = Tema.BotonSecundario("Cerrar");
        cerrar.DialogResult = DialogResult.Cancel;

        Controls.Add(Botones(cerrar, recibo, anular));
        Controls.Add(info);
        Controls.Add(nombre);
        Controls.Add(estado);
        CancelButton = cerrar;

        recibo.Click += (_, _) => { AccionElegida = Accion.Recibo; DialogResult = DialogResult.OK; Close(); };
        anular.Click += (_, _) => { AccionElegida = Accion.Anular; DialogResult = DialogResult.OK; Close(); };
        Shown += (_, _) => recibo.Focus();
    }
}

/// <summary>Genera el recibo y muestra qué hacer con él: mandarlo por WhatsApp, verlo en la carpeta o abrirlo.</summary>
internal static class ReciboUi
{
    public static void GenerarYMostrar(IWin32Window owner, Database db, Pago pago, Alumno alumno)
    {
        string ruta;
        try
        {
            Cursor.Current = Cursors.WaitCursor;
            var config = new ConfiguracionService(db);
            ruta = new ReciboPdfService(config.NombreClub, config.NombreTaller, config.NombreProfesor).Generar(pago, alumno);
        }
        catch (Exception ex)
        {
            VistasComun.Error(owner, "El pago quedó registrado, pero no se pudo generar el recibo.\n\n" + ex.Message);
            return;
        }
        finally
        {
            Cursor.Current = Cursors.Default;
        }

        using var f = new ReciboListoForm(alumno, pago, ruta);
        f.ShowDialog(owner);
    }
}

/// <summary>Recibo generado: enviar por WhatsApp, mostrar en la carpeta, abrir el PDF o cerrar.</summary>
public class ReciboListoForm : DialogoBase
{
    public ReciboListoForm(Alumno alumno, Pago pago, string ruta)
    {
        Text = "Recibo";
        MinimumSize = new Size(520, 0);

        var titulo = new Label { Text = "Recibo generado", Dock = DockStyle.Top, AutoSize = true, Font = Tema.FuenteSeccion, Padding = new Padding(0, 0, 0, 4) };
        var archivo = new Label { Text = Path.GetFileName(ruta), Dock = DockStyle.Top, AutoSize = true, ForeColor = Tema.TextoSuave };
        var contacto = alumno.TieneTutor ? $"tutor de {alumno.Nombre}" : alumno.Nombre;
        var nota = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ForeColor = Tema.TextoSuave,
            Font = Tema.FuenteChica,
            Padding = new Padding(0, 10, 0, 0),
            MaximumSize = new Size(480, 0),
            Text = WhatsApp.ProblemaConTelefono(alumno) ?? 
                $"\"Enviar por WhatsApp\" abre el chat con {contacto} ({alumno.CelularContacto}) con el mensaje escrito " +
                "y la carpeta con el recibo seleccionado: arrastrá el PDF al chat.",
        };

        var whatsapp = Tema.BotonPrimario("Enviar por &WhatsApp");
        var carpeta = Tema.BotonSecundario("Mostrar en &carpeta");
        var pdf = Tema.BotonSecundario("Abrir &PDF");
        var cerrar = Tema.BotonSecundario("Cerrar");
        cerrar.DialogResult = DialogResult.Cancel;

        Controls.Add(Botones(cerrar, whatsapp, carpeta, pdf));
        Controls.Add(nota);
        Controls.Add(archivo);
        Controls.Add(titulo);
        CancelButton = cerrar;

        whatsapp.Click += (_, _) =>
        {
            if (!WhatsAppUi.AbrirChat(this, alumno, WhatsApp.MensajeRecibo(alumno, pago))) return;
            Abrir(() => Explorador.MostrarArchivo(ruta));
            Close();
        };
        carpeta.Click += (_, _) => { Abrir(() => Explorador.MostrarArchivo(ruta)); Close(); };
        pdf.Click += (_, _) => { Abrir(() => Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true })); Close(); };
        Shown += (_, _) => (WhatsApp.ProblemaConTelefono(alumno) is null ? whatsapp : carpeta).Focus();
    }

    private void Abrir(Action accion)
    {
        try { accion(); }
        catch (Exception ex) { VistasComun.Error(this, "No se pudo abrir: " + ex.Message); }
    }
}
