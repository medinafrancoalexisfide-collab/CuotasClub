using System.Diagnostics;
using CuotasClub.Data;
using CuotasClub.Repositories;
using CuotasClub.Services;

namespace CuotasClub.Views;

/// <summary>
/// Configuración: datos del club, precio de la cuota, % del club, inicio de cobros y planilla del tesorero (Google Sheets).
/// El precio y el % se guardan "desde un mes": no cambian los meses anteriores.
/// </summary>
public class ConfiguracionForm : VentanaBase
{
    private readonly ConfiguracionService _config;
    private readonly CopiaSeguridadService _copias;
    private readonly PrecioCuotaRepository _precios;
    private readonly Database _db;

    private const int AnchoTarjeta = 660;
    private const int AnchoTexto = AnchoTarjeta - 40;

    private readonly TextBox _rutaCredenciales = new() { Width = 440 };
    private readonly Label _email = new() { AutoSize = true, MaximumSize = new Size(AnchoTexto, 0), Margin = new Padding(0, 2, 0, 8) };
    private readonly TextBox _idPlanilla = new() { Width = 440, PlaceholderText = "Pegá acá el enlace de la planilla" };
    private readonly Label _estado = new() { AutoSize = true, MaximumSize = new Size(AnchoTexto, 0), Margin = new Padding(0, 8, 0, 0) };
    private readonly Button _sincronizar = Tema.BotonPrimario("&Guardar y sincronizar ahora");

    private readonly TextBox _nombreClub = new() { Width = 320 };
    private readonly TextBox _nombreTaller = new() { Width = 320 };
    private readonly TextBox _nombreProfesor = new() { Width = 320, PlaceholderText = "Opcional" };

    private readonly Label _historialPrecios = new() { AutoSize = true, MaximumSize = new Size(AnchoTexto, 0), Margin = new Padding(0, 0, 0, 10) };
    private readonly TextBox _nuevoPrecio = new() { Width = 110, TextAlign = HorizontalAlignment.Right, PlaceholderText = "25000" };
    private readonly ComboBox _precioMes = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly NumericUpDown _precioAnio = new() { Minimum = 2020, Maximum = 2100, Width = 76 };

    private readonly Label _historialPorcentaje = new() { AutoSize = true, MaximumSize = new Size(AnchoTexto, 0), Margin = new Padding(0, 0, 0, 10) };
    private readonly NumericUpDown _nuevoPorcentaje = new() { Minimum = 0, Maximum = 100, DecimalPlaces = 1, Width = 76 };
    private readonly ComboBox _pctMes = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly NumericUpDown _pctAnio = new() { Minimum = 2020, Maximum = 2100, Width = 76 };

    private readonly TextBox _carpetaCopias = new() { Width = 440, ReadOnly = true };
    private readonly Label _estadoCopias = new() { AutoSize = true, MaximumSize = new Size(AnchoTexto, 0), Margin = new Padding(0, 8, 0, 0) };

    private readonly Label _inicioActual = new() { AutoSize = true, MaximumSize = new Size(AnchoTexto, 0), Margin = new Padding(0, 0, 0, 10) };
    private readonly ComboBox _inicioMes = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    private readonly NumericUpDown _inicioAnio = new() { Minimum = 2020, Maximum = 2100, Width = 76 };

    public ConfiguracionForm(Database db)
    {
        _db = db;
        _config = new ConfiguracionService(db);
        _precios = new PrecioCuotaRepository(db);
        _copias = new CopiaSeguridadService(db);

        Text = "Configuración";
        Size = new Size(760, 800);
        MinimumSize = new Size(720, 520);

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(20, 16, 20, 16),
            BackColor = Tema.Fondo,
        };

        // --- Datos del club (encabezado y recibos)
        var guardarDatos = Tema.BotonPrimario("Guardar &datos");
        panel.Controls.Add(Tarjeta("Datos del club",
            "Se muestran en la pantalla principal y en los recibos. El profesor es opcional: si lo cargás, el recibo dice quién recibió el pago.",
            Fila(EtiquetaFija("Club"), _nombreClub),
            Fila(EtiquetaFija("Taller o actividad"), _nombreTaller),
            Fila(EtiquetaFija("Profesor"), _nombreProfesor),
            Fila(guardarDatos)));

        // --- Precio de la cuota
        var guardarPrecio = Tema.BotonPrimario("Guardar &precio");
        panel.Controls.Add(Tarjeta("Precio de la cuota",
            "Cadete y Activo pagan lo mismo. Un precio nuevo rige desde el mes que elijas: los meses anteriores y los pagos ya registrados no cambian.",
            _historialPrecios,
            Fila(Tema.Etiqueta("Nuevo precio $"), _nuevoPrecio, Tema.Etiqueta("desde"), _precioMes, _precioAnio, guardarPrecio)));

        // --- % del club
        var guardarPct = Tema.BotonPrimario("Guardar p&orcentaje");
        panel.Controls.Add(Tarjeta("Porcentaje para el club",
            null,
            _historialPorcentaje,
            Fila(Tema.Etiqueta("Nuevo %"), _nuevoPorcentaje, Tema.Etiqueta("desde"), _pctMes, _pctAnio, guardarPct)));

        // --- Inicio de cobros
        var guardarInicio = Tema.BotonPrimario("Guardar &inicio");
        panel.Controls.Add(Tarjeta("Inicio de cobros",
            "Primer mes que se cobra con el programa. Los meses anteriores aparecen como \"—\" y no cuentan como deuda, " +
            "aunque el alumno tenga una fecha de alta más vieja.",
            _inicioActual,
            Fila(Tema.Etiqueta("Cobrar desde"), _inicioMes, _inicioAnio, guardarInicio)));

        // --- Copias de seguridad
        var elegirCarpeta = Tema.BotonSecundario("Elegir &carpeta…");
        var abrirCarpeta = Tema.BotonSecundario("A&brir");
        var copiarAhora = Tema.BotonPrimario("&Hacer copia ahora");
        var restaurar = Tema.BotonPeligro("&Restaurar copia…");
        var traerDatos = Tema.BotonSecundario("&Traer datos de otra PC…");
        var abrirDatos = Tema.BotonSecundario("Abrir carpeta de &datos");
        panel.Controls.Add(Tarjeta("Copias de seguridad",
            $"Cada vez que cerrás el programa se guarda una copia de todos los datos en esta carpeta. Se conservan las últimas {CopiaSeguridadService.CopiasAConservar} " +
            "y las más viejas se borran solas.",
            Tema.Nota("Para tenerlas también en la nube, instalá Google Drive para escritorio, iniciá sesión con tu cuenta de Google y elegí acá " +
                      "una carpeta dentro de \"Mi unidad\" (por ejemplo G:\\Mi unidad\\CuotasClub-Copias). Drive las sube solo.", AnchoTexto),
            Fila(_carpetaCopias, elegirCarpeta, abrirCarpeta),
            Fila(copiarAhora, restaurar),
            _estadoCopias,
            Tema.Nota("¿Venís de la versión anterior o de otra PC? Con \"Traer datos de otra PC\" elegís el archivo cuotasclub.db " +
                      "(o una copia) y el programa pasa a usar esos datos. Tus datos se guardan en " + Database.CarpetaDatos + ".", AnchoTexto),
            Fila(traerDatos, abrirDatos)));

        // --- Planilla del tesorero (opcional)
        var examinar = Tema.BotonSecundario("&Examinar…");
        var abrir = Tema.BotonSecundario("&Abrir");
        var verPasos = new LinkLabel
        {
            Text = "Ver los pasos para conseguir el archivo de credenciales",
            AutoSize = true,
            LinkColor = Tema.Primario,
            ActiveLinkColor = Tema.PrimarioOscuro,
            Margin = new Padding(0, 0, 0, 8),
        };
        panel.Controls.Add(Tarjeta("Planilla del tesorero (opcional)",
            "Sirve para que la planilla de Google del tesorero se actualice sola con lo que ingresó cada mes y la parte del club. " +
            "Los gastos y tu parte nunca se publican. Si no la usás, dejá todo vacío: el programa funciona igual.",
            Subtitulo("1. Archivo de credenciales"),
            Tema.Nota("Es un archivo .json que se descarga una sola vez desde Google Cloud (\"clave de cuenta de servicio\"). " +
                      "Funciona como una contraseña para que el programa pueda escribir en la planilla. No es tu usuario ni tu clave de Gmail.", AnchoTexto),
            verPasos,
            Fila(_rutaCredenciales, examinar),
            _email,
            Subtitulo("2. Enlace de la planilla"),
            Fila(_idPlanilla, abrir),
            Fila(_sincronizar),
            _estado));

        var cerrar = Tema.BotonSecundario("Cerrar");
        cerrar.DialogResult = DialogResult.Cancel;
        CancelButton = cerrar;

        Controls.Add(panel);
        Controls.Add(Tema.Pie(new Label { AutoSize = true }, cerrar));
        Controls.Add(Tema.Encabezado("Configuración", "Datos del club, precio de la cuota, porcentaje para el club, copias de seguridad y planilla del tesorero"));

        foreach (var c in new[] { _precioMes, _pctMes, _inicioMes }) c.Items.AddRange(Formato.NombresMeses);
        var hoy = DateTime.Today;
        _precioMes.SelectedIndex = _pctMes.SelectedIndex = hoy.Month - 1;
        _precioAnio.Value = _pctAnio.Value = hoy.Year;

        examinar.Click += (_, _) => ElegirCredenciales();
        abrir.Click += (_, _) => AbrirPlanilla();
        verPasos.LinkClicked += (_, _) => AbrirGuia();
        _sincronizar.Click += async (_, _) => await GuardarYSincronizar();
        _rutaCredenciales.Leave += (_, _) => GuardarGoogle();
        _idPlanilla.Leave += (_, _) => GuardarGoogle();
        elegirCarpeta.Click += (_, _) => ElegirCarpetaCopias();
        abrirCarpeta.Click += (_, _) => AbrirCarpetaCopias();
        copiarAhora.Click += (_, _) => HacerCopiaAhora();
        restaurar.Click += (_, _) => RestaurarCopia();
        traerDatos.Click += (_, _) => TraerDatos();
        abrirDatos.Click += (_, _) => Explorador.MostrarArchivo(_db.RutaArchivo);
        guardarDatos.Click += (_, _) => GuardarDatosClub();
        guardarPrecio.Click += (_, _) => GuardarPrecio();
        guardarPct.Click += (_, _) => GuardarPorcentaje();
        guardarInicio.Click += (_, _) => GuardarInicio();

        Sincronizador.EstadoCambio += AlCambiarEstado;
        FormClosed += (_, _) => Sincronizador.EstadoCambio -= AlCambiarEstado;
        Load += (_, _) => Cargar();
    }

    private void Cargar()
    {
        _rutaCredenciales.Text = _config.RutaCredenciales ?? "";
        _rutaCredenciales.PlaceholderText = $"Vacío = {ConfiguracionService.ArchivoCredencialesPorDefecto} en Documentos\\CuotasClub";
        _idPlanilla.Text = _config.IdPlanillaTesoreria ?? "";
        _nuevoPorcentaje.Value = _config.PorcentajeClub;
        _nombreClub.Text = _config.NombreClub;
        _nombreTaller.Text = _config.NombreTaller;
        _nombreProfesor.Text = _config.NombreProfesor;
        ActualizarEmail();
        ActualizarEstado();
        CargarHistoriales();
        ActualizarCopias();
    }

    private void ActualizarCopias()
    {
        _carpetaCopias.Text = _copias.Carpeta;
        var ultima = _copias.UltimaCopia;
        var error = _copias.ErrorUltimaCopia;
        var cantidad = _copias.ListarCopias().Count;
        if (!string.IsNullOrEmpty(error))
        {
            _estadoCopias.ForeColor = Tema.Peligro;
            _estadoCopias.Text = "La última copia no se pudo guardar: " + error + Environment.NewLine +
                                 "Revisá que la carpeta exista (si es de Google Drive, que Drive esté abierto) o elegí otra.";
        }
        else
        {
            _estadoCopias.ForeColor = Tema.TextoSuave;
            _estadoCopias.Text = ultima is { } u
                ? $"Última copia: {u.ToString("dd/MM/yyyy HH:mm", VistasComun.Cultura)}. Copias en la carpeta: {cantidad}."
                : "Todavía no se hizo ninguna copia. La primera se guarda al cerrar el programa, o ahora con \"Hacer copia ahora\".";
        }
    }

    private void ElegirCarpetaCopias()
    {
        using var dlg = new FolderBrowserDialog
        {
            Description = "Elegí la carpeta para las copias de seguridad (puede ser una de Google Drive)",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = Directory.Exists(_copias.Carpeta) ? _copias.Carpeta : "",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _copias.Carpeta = dlg.SelectedPath;
        if (VistasComun.Confirmar(this, "Carpeta guardada. ¿Hago una copia ahora para probar que funciona?"))
            HacerCopiaAhora();
        else
            ActualizarCopias();
    }

    private void AbrirCarpetaCopias()
    {
        try
        {
            Explorador.AbrirCarpeta(_copias.Carpeta);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            VistasComun.Error(this, "No se pudo abrir la carpeta: " + ex.Message);
        }
    }

    private void HacerCopiaAhora()
    {
        try
        {
            var ruta = _copias.HacerCopia();
            ActualizarCopias();
            MessageBox.Show(this, $"Copia guardada:{Environment.NewLine}{ruta}", "Copias de seguridad",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (ReglaNegocioException ex)
        {
            ActualizarCopias();
            VistasComun.Error(this, ex.Message);
        }
    }

    private void TraerDatos()
    {
        if (Sincronizador.EnCurso)
        {
            VistasComun.Error(this, "Se está actualizando la planilla del tesorero. Esperá unos segundos y probá de nuevo.");
            return;
        }
        if (!ImportarBase.Elegir(this, _db)) return;
        MessageBox.Show(this, "Datos traídos. El programa se va a reiniciar.", "CuotasClub", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Program.OmitirCopiaAlCerrar = true;
        Application.Restart();
    }

    private void RestaurarCopia()
    {
        if (Sincronizador.EnCurso)
        {
            VistasComun.Error(this, "Se está actualizando la planilla del tesorero. Esperá unos segundos y probá de nuevo.");
            return;
        }
        using var dlg = new OpenFileDialog
        {
            Title = "Elegí la copia que querés restaurar",
            Filter = "Copias de CuotasClub (cuotasclub-*.db)|cuotasclub-*.db|Todas las bases (*.db)|*.db",
            InitialDirectory = Directory.Exists(_copias.Carpeta) ? _copias.Carpeta : "",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        if (CopiaSeguridadService.ValidarCopia(dlg.FileName) is { } motivo)
        {
            VistasComun.Error(this, motivo);
            return;
        }
        var fecha = File.GetLastWriteTime(dlg.FileName).ToString("dd/MM/yyyy HH:mm", VistasComun.Cultura);
        if (!VistasComun.Confirmar(this, $"¿Restaurar la copia del {fecha}?{Environment.NewLine}{Environment.NewLine}" +
                                         "Todos los datos actuales se reemplazan por los de esa copia. Antes se guarda una copia de cómo está todo ahora " +
                                         "(termina en \"antes-de-restaurar\"), por si te arrepentís. Después el programa se reinicia."))
            return;
        try
        {
            _copias.Restaurar(dlg.FileName);
        }
        catch (Exception ex) when (ex is ReglaNegocioException or IOException or UnauthorizedAccessException)
        {
            VistasComun.Error(this, "No se pudo restaurar la copia: " + ex.Message);
            return;
        }
        MessageBox.Show(this, "Copia restaurada. El programa se va a reiniciar.", "Copias de seguridad",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
        Program.OmitirCopiaAlCerrar = true;
        Application.Restart();
    }

    private void CargarHistoriales()
    {
        var precios = _precios.Listar();
        _historialPrecios.Text = precios.Count == 0
            ? "No hay precios cargados."
            : string.Join(Environment.NewLine, precios.Select(p => $"Desde {Formato.MesAnio(p.VigenteDesdeAnio, p.VigenteDesdeMes)}: {Formato.Moneda(p.Monto)}"));
        var hoy = DateTime.Today;
        var inicio = _config.InicioCobros;
        _inicioActual.Text = $"Hoy se cobra desde {Formato.MesAnio(inicio.Year, inicio.Month)}.";
        _inicioMes.SelectedIndex = inicio.Month - 1;
        _inicioAnio.Value = Math.Clamp(inicio.Year, (int)_inicioAnio.Minimum, (int)_inicioAnio.Maximum);
        _historialPorcentaje.Text = $"Vigente este mes: {Formato.Decimal1(_config.PorcentajeClubVigente(hoy.Year, hoy.Month))}% para el club. " +
                                    "Cada pago guarda el % de su mes, así que un cambio no altera lo ya cobrado.";
    }

    private void ElegirCredenciales()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Elegí el archivo de credenciales de Google",
            Filter = "Clave JSON (*.json)|*.json",
            InitialDirectory = Directory.Exists(ConfiguracionService.CarpetaDocumentos) ? ConfiguracionService.CarpetaDocumentos : "",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        _rutaCredenciales.Text = dlg.FileName;
        GuardarGoogle();
    }

    private void GuardarGoogle()
    {
        _config.RutaCredenciales = _rutaCredenciales.Text;
        _config.IdPlanillaTesoreria = _idPlanilla.Text;
        _idPlanilla.Text = _config.IdPlanillaTesoreria ?? "";
        ActualizarEmail();
        ActualizarEstado();
    }

    private void ActualizarEmail()
    {
        var ruta = _config.RutaCredencialesEfectiva;
        if (!File.Exists(ruta))
        {
            bool elegido = !string.IsNullOrWhiteSpace(_config.RutaCredenciales);
            _email.ForeColor = elegido ? Tema.Peligro : Tema.TextoSuave;
            _email.Text = elegido
                ? $"No se encuentra el archivo: {ruta}"
                : "Todavía no elegiste el archivo. Sin él la planilla no se actualiza, pero el resto del programa funciona igual.";
            return;
        }
        var email = SincronizacionService.LeerEmailCuentaServicio(ruta);
        _email.ForeColor = email is null ? Tema.Peligro : Tema.Exito;
        _email.Text = email is null
            ? "El archivo no parece una clave JSON de cuenta de servicio."
            : $"✓ Archivo correcto. Compartí la planilla como Editor con: {email}";
        if (email is not null)
        {
            try { Clipboard.SetText(email); _email.Text += "  (copiado)"; }
            catch (System.Runtime.InteropServices.ExternalException) { }
        }
    }

    private void ActualizarEstado()
    {
        var r = Sincronizador.UltimoResultado;
        _estado.ForeColor = r is { Ok: false } ? Tema.Peligro : Tema.TextoSuave;
        _estado.Text = Sincronizador.TextoEstado() + (r is { Ok: false } ? Environment.NewLine + r.Mensaje : "");
        _sincronizar.Enabled = !Sincronizador.EnCurso;
    }

    private void AlCambiarEstado()
    {
        if (IsHandleCreated && !IsDisposed) BeginInvoke(ActualizarEstado);
    }

    private async Task GuardarYSincronizar()
    {
        GuardarGoogle();
        if (!_config.SheetsConfigurado)
        {
            VistasComun.Error(this, string.IsNullOrWhiteSpace(_config.IdPlanillaTesoreria)
                ? "Falta pegar el enlace de la planilla."
                : $"No se encuentra el archivo de credenciales:{Environment.NewLine}{_config.RutaCredencialesEfectiva}");
            return;
        }
        var r = await Sincronizador.SincronizarAsync();
        if (IsDisposed) return;
        ActualizarEstado();
        MessageBox.Show(this, r.Mensaje, "Planilla del tesorero", MessageBoxButtons.OK,
            r.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private void AbrirPlanilla()
    {
        GuardarGoogle();
        if (string.IsNullOrWhiteSpace(_config.IdPlanillaTesoreria)) return;
        Process.Start(new ProcessStartInfo($"https://docs.google.com/spreadsheets/d/{_config.IdPlanillaTesoreria}/edit") { UseShellExecute = true });
    }

    private void GuardarDatosClub()
    {
        if (string.IsNullOrWhiteSpace(_nombreClub.Text) && string.IsNullOrWhiteSpace(_nombreTaller.Text))
        {
            VistasComun.Error(this, "Escribí el nombre del club o del taller.");
            return;
        }
        _config.NombreClub = _nombreClub.Text;
        _config.NombreTaller = _nombreTaller.Text;
        _config.NombreProfesor = _nombreProfesor.Text;
        MessageBox.Show(this, "Datos guardados. Los próximos recibos ya salen con estos nombres.", "Configuración",
            MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private static Label EtiquetaFija(string texto)
    {
        var l = Tema.Etiqueta(texto);
        l.AutoSize = false;
        l.Size = new Size(130, 26);
        l.TextAlign = ContentAlignment.MiddleLeft;
        return l;
    }

    private void GuardarPrecio()
    {
        if (!Formato.TryParseMoneda(_nuevoPrecio.Text, out var monto) || monto <= 0)
        {
            VistasComun.Error(this, "Escribí el precio, por ejemplo 25000.");
            return;
        }
        int anio = (int)_precioAnio.Value, mes = _precioMes.SelectedIndex + 1;
        if (!VistasComun.Confirmar(this, $"¿Cuota de {Formato.Moneda(monto)} desde {Formato.MesAnio(anio, mes)}?{Environment.NewLine}{Environment.NewLine}" +
                                         "Los meses anteriores y los pagos ya registrados no cambian."))
            return;
        _precios.Guardar(anio, mes, monto);
        _nuevoPrecio.Clear();
        CargarHistoriales();
    }

    private void GuardarPorcentaje()
    {
        int anio = (int)_pctAnio.Value, mes = _pctMes.SelectedIndex + 1;
        var pct = _nuevoPorcentaje.Value;
        if (!VistasComun.Confirmar(this, $"¿{Formato.Decimal1(pct)}% para el club desde {Formato.MesAnio(anio, mes)}?{Environment.NewLine}{Environment.NewLine}" +
                                         "Los pagos ya registrados conservan el % con el que se cobraron."))
            return;
        _config.GuardarPorcentajeClub(anio, mes, pct);
        _config.SincronizacionPendiente = true; // el encabezado de la planilla muestra el %
        CargarHistoriales();
        Sincronizador.SiHayPendientes();
    }

    private void GuardarInicio()
    {
        int anio = (int)_inicioAnio.Value, mes = _inicioMes.SelectedIndex + 1;
        if (!VistasComun.Confirmar(this, $"¿Cobrar desde {Formato.MesAnio(anio, mes)}?{Environment.NewLine}{Environment.NewLine}" +
                                         "Los meses anteriores van a aparecer como \"—\" y no cuentan como deuda. Los pagos ya registrados no se borran."))
            return;
        _config.InicioCobros = new DateOnly(anio, mes, 1);
        _config.SincronizacionPendiente = true;
        CargarHistoriales();
        Sincronizador.SiHayPendientes();
    }

    private void AbrirGuia()
    {
        var guia = Path.Combine(AppContext.BaseDirectory, "docs", "CONECTAR-GOOGLE-SHEETS.md");
        try
        {
            if (File.Exists(guia))
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{guia}\"") { UseShellExecute = true });
            else
                VistasComun.Error(this, "No se encontró la guía junto al programa. Está en la carpeta docs del ZIP: CONECTAR-GOOGLE-SHEETS.md");
        }
        catch (Exception ex)
        {
            VistasComun.Error(this, "No se pudo abrir la guía: " + ex.Message);
        }
    }

    /// <summary>Tarjeta blanca con título, una explicación opcional y su contenido.</summary>
    private static Control Tarjeta(string titulo, string? explicacion, params Control[] contenido)
    {
        var interno = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Dock = DockStyle.Top,
            Padding = new Padding(18, 14, 18, 16),
            BackColor = Tema.Superficie,
        };
        interno.Controls.Add(Tema.Seccion(titulo));
        if (explicacion is not null) interno.Controls.Add(Tema.Nota(explicacion, AnchoTexto));
        interno.Controls.AddRange(contenido);

        var tarjeta = Tema.TarjetaPanel();
        tarjeta.Width = AnchoTarjeta;
        tarjeta.AutoSize = true;
        tarjeta.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tarjeta.MinimumSize = new Size(AnchoTarjeta, 0);
        tarjeta.Margin = new Padding(0, 0, 0, 14);
        tarjeta.Controls.Add(interno);
        return tarjeta;
    }

    private static Label Subtitulo(string texto) => new()
    {
        Text = texto,
        AutoSize = true,
        Font = Tema.FuenteSemibold,
        ForeColor = Tema.Texto,
        Margin = new Padding(0, 10, 0, 2),
    };

    private static FlowLayoutPanel Fila(params Control[] controles)
    {
        var f = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        foreach (var c in controles)
        {
            if (c is not Label && c is not Button) c.Margin = new Padding(0, 4, 8, 2);
            f.Controls.Add(c);
        }
        return f;
    }
}
