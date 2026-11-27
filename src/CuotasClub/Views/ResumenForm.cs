using System.Globalization;
using CuotasClub.Data;
using CuotasClub.Models;
using CuotasClub.Services;

namespace CuotasClub.Views;

/// <summary>
/// Resumen anual mes por mes: alumnos activos, que pagaron y que deben, total ingresado,
/// parte del club, parte del profesor, gastos y ganancia neta, con fila de Total y de Promedio.
/// Solo se muestra en el programa: los gastos y la ganancia nunca van a Google Sheets.
/// Doble clic en un mes abre los gastos de ese mes.
/// </summary>
public class ResumenForm : VentanaBase
{
    private static readonly Color ColorNegativo = Tema.DebeTexto;
    private static readonly Color ColorFuturo = Tema.NoCorrespondeTexto;
    private static readonly Color FondoMesActual = Color.FromArgb(255, 249, 225);
    private static readonly Color FondoTotales = Tema.PrimarioSuave;

    private readonly Database _db;
    private readonly ResumenService _servicio;

    private readonly NumericUpDown _numAnio = Tema.Campo(new NumericUpDown { Minimum = 2020, Maximum = 2100, TextAlign = HorizontalAlignment.Center }, 76);
    private readonly Button _btnAnterior = Tema.BotonFlecha("◀");
    private readonly Button _btnSiguiente = Tema.BotonFlecha("▶");
    private readonly Button _btnActualizar = Tema.BotonSecundario("Actualizar");
    private readonly DataGridView _grilla = new();
    private readonly Label _lblNota = new() { AutoSize = true, ForeColor = Tema.TextoSuave, Font = Tema.FuenteChica };

    // Tarjetas del año
    private readonly Label _lblIngresado = Tarjeta();
    private readonly Label _lblClub = Tarjeta();
    private readonly Label _lblMio = Tarjeta();
    private readonly Label _lblGastos = Tarjeta();
    private readonly Label _lblGanancia = Tarjeta();

    private readonly Font _fuenteNegrita = new("Segoe UI Semibold", 10F);
    private bool _cargando;

    public ResumenForm(Database db) : this(db, DateTime.Today.Year) { }

    public ResumenForm(Database db, int anio)
    {
        _db = db;
        _servicio = new ResumenService(db);

        Text = "Resumen mensual y anual";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(980, 600);
        Size = new Size(1180, 760);

        ArmarPantalla();

        _cargando = true;
        _numAnio.Value = Math.Clamp(anio, (int)_numAnio.Minimum, (int)_numAnio.Maximum);
        _cargando = false;
        Recargar();
    }

    private static Label Tarjeta() => new TarjetaDato();

    private static Panel Envolver(Label l)
    {
        var p = Tema.TarjetaPanel();
        p.Size = new Size(204, 70);
        p.Margin = new Padding(0, 0, 10, 0);
        p.Controls.Add(l);
        return p;
    }

    private void ArmarPantalla()
    {
        var barra = Tema.Barra();
        _btnAnterior.Margin = new Padding(0, 2, 4, 2);
        _numAnio.Margin = new Padding(0, 4, 4, 2);
        _btnSiguiente.Margin = new Padding(0, 2, 12, 2);
        barra.Controls.AddRange([Tema.Etiqueta("Año"), _btnAnterior, _numAnio, _btnSiguiente, _btnActualizar]);

        var tarjetas = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(16, 6, 16, 6), BackColor = Tema.Fondo };
        tarjetas.Controls.AddRange([Envolver(_lblIngresado), Envolver(_lblClub), Envolver(_lblMio), Envolver(_lblGastos), Envolver(_lblGanancia)]);

        Tema.EstilarGrilla(_grilla);
        _grilla.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grilla.MultiSelect = false;
        _grilla.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grilla.ColumnHeadersHeight = 44;
        _grilla.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        _grilla.RowTemplate.Height = 30;

        AgregarColumna("Mes", "Mes", 110, izquierda: true);
        AgregarColumna("Activos", "Alumnos activos", 80);
        AgregarColumna("Pagaron", "Pagaron", 75);
        AgregarColumna("Deben", "Deben", 70);
        AgregarColumna("Ingresado", "Total ingresado", 120);
        AgregarColumna("Club", "Club", 110);
        AgregarColumna("Mio", "Mío", 110);
        AgregarColumna("Gastos", "Gastos", 110);
        AgregarColumna("Ganancia", "Ganancia neta", 120);
        foreach (DataGridViewColumn c in _grilla.Columns) c.SortMode = DataGridViewColumnSortMode.NotSortable;

        _lblNota.MaximumSize = new Size(1100, 0);

        Controls.Add(Tema.Contenedor(_grilla));
        Controls.Add(Tema.Pie(_lblNota));
        Controls.Add(tarjetas);
        Controls.Add(barra);
        Controls.Add(Tema.Encabezado("Resumen", "Mes por mes: alumnos, ingresos, parte del club, tu parte, gastos y ganancia · Doble clic en un mes para ver sus gastos"));

        _numAnio.ValueChanged += (_, _) => { if (!_cargando) Recargar(); };
        _btnAnterior.Click += (_, _) => { if (_numAnio.Value > _numAnio.Minimum) _numAnio.Value--; };
        _btnSiguiente.Click += (_, _) => { if (_numAnio.Value < _numAnio.Maximum) _numAnio.Value++; };
        _btnActualizar.Click += (_, _) => Recargar();
        _grilla.CellDoubleClick += (_, e) => AbrirGastosDelMes(e.RowIndex);
    }

    private void AgregarColumna(string nombre, string titulo, int ancho, bool izquierda = false)
    {
        _grilla.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = nombre,
            HeaderText = titulo,
            FillWeight = ancho,
            DefaultCellStyle =
            {
                Alignment = izquierda ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight
            },
            HeaderCell =
            {
                Style = { Alignment = izquierda ? DataGridViewContentAlignment.MiddleLeft : DataGridViewContentAlignment.MiddleRight }
            }
        });
    }

    private void Recargar()
    {
        int anio = (int)_numAnio.Value;
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        ResumenAnual resumen;
        try
        {
            resumen = _servicio.ObtenerResumenAnual(anio, hoy);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo calcular el resumen:\n" + ex.Message, "Resumen",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // El % del club es histórico: cada mes usa el que regía en ese momento.
        // Si en el año hubo un solo %, se muestra en los títulos; si cambió, se aclara "% de cada mes".
        var porcentajes = resumen.Meses.Select(m => m.PorcentajeClub).Distinct().ToList();
        string tituloClub, tituloMio;
        if (porcentajes.Count == 1)
        {
            tituloClub = $"Club ({Porcentaje(porcentajes[0])})";
            tituloMio = $"Mío ({Porcentaje(100 - porcentajes[0])})";
        }
        else
        {
            tituloClub = "Club (% de cada mes)";
            tituloMio = "Mío (% de cada mes)";
        }
        _grilla.Columns["Club"]!.HeaderText = tituloClub;
        _grilla.Columns["Mio"]!.HeaderText = tituloMio;

        var inicioMesActual = new DateOnly(hoy.Year, hoy.Month, 1);
        _grilla.Rows.Clear();
        foreach (var m in resumen.Meses)
        {
            var inicioMes = new DateOnly(m.Anio, m.Mes, 1);
            bool esFuturo = inicioMes > inicioMesActual;
            bool esActual = inicioMes == inicioMesActual;

            string nombreMes = Formato.NombreMes(m.Mes) + (esActual ? " (en curso)" : "");

            // En el mes en curso nadie "debe" todavía (hay todo el mes para pagar):
            // se muestran los pendientes, es decir activos que aún no pagaron.
            string deben = esFuturo ? ""
                : esActual ? $"{Math.Max(0, m.AlumnosActivos - m.AlumnosQuePagaron)} pend."
                : m.AlumnosQueDeben.ToString(CultureInfo.InvariantCulture);

            int i = _grilla.Rows.Add(
                nombreMes,
                esFuturo ? "" : m.AlumnosActivos.ToString(CultureInfo.InvariantCulture),
                esFuturo ? "" : m.AlumnosQuePagaron.ToString(CultureInfo.InvariantCulture),
                deben,
                Formato.Moneda(m.TotalIngresado),
                Formato.Moneda(m.ParaElClub),
                Formato.Moneda(m.ParaMi),
                Formato.Moneda(m.Gastos),
                Formato.Moneda(m.GananciaNeta));

            var fila = _grilla.Rows[i];
            fila.Tag = m;
            if (porcentajes.Count > 1)
                fila.Cells["Club"].ToolTipText = $"{Porcentaje(m.PorcentajeClub)} para el club en {Formato.MesAnio(m.Anio, m.Mes)}";
            if (esFuturo && m.TotalIngresado == 0 && m.Gastos == 0)
                fila.DefaultCellStyle.ForeColor = ColorFuturo;
            if (esActual)
                fila.DefaultCellStyle.BackColor = FondoMesActual;
            if (m.GananciaNeta < 0)
                fila.Cells["Ganancia"].Style.ForeColor = ColorNegativo;
        }

        // Fila Total: "activos" y "deben" no se suman (el mismo alumno aparece en varios meses);
        // "Pagaron" sí, como cantidad de cuotas cobradas en el año.
        var t = resumen.Total;
        AgregarFilaResumen("Total del año", "", $"{t.AlumnosQuePagaron} cuotas", "",
            t.TotalIngresado, t.ParaElClub, t.ParaMi, t.Gastos, t.GananciaNeta);

        var p = resumen.Promedio;
        AgregarFilaResumen("Promedio mensual",
            Formato.Decimal1(p.AlumnosActivos), Formato.Decimal1(p.AlumnosQuePagaron), Formato.Decimal1(p.AlumnosQueDeben),
            p.TotalIngresado, p.ParaElClub, p.ParaMi, p.Gastos, p.GananciaNeta);

        _grilla.ClearSelection();

        string sufijoClub = porcentajes.Count == 1 ? $" ({Porcentaje(porcentajes[0])})" : "";
        string sufijoMio = porcentajes.Count == 1 ? $" ({Porcentaje(100 - porcentajes[0])})" : "";
        _lblIngresado.Text = $"Ingresado {anio}\n{Formato.Moneda(t.TotalIngresado)}";
        _lblClub.Text = $"Para el club{sufijoClub}\n{Formato.Moneda(t.ParaElClub)}";
        _lblMio.Text = $"Para mí{sufijoMio}\n{Formato.Moneda(t.ParaMi)}";
        _lblGastos.Text = $"Gastos\n{Formato.Moneda(t.Gastos)}";
        _lblGanancia.Text = $"Ganancia neta\n{Formato.Moneda(t.GananciaNeta)}";
        _lblGanancia.ForeColor = t.GananciaNeta < 0 ? ColorNegativo : Tema.Exito;

        string meses = resumen.MesesConActividad switch
        {
            0 => "todavía no hay meses con clases",
            1 => "1 mes",
            var n => $"{n} meses"
        };
        _lblNota.Text =
            $"Promedio sobre los meses ya transcurridos que tuvieron clases ({meses}). " +
            "Ganancia neta = parte mía − gastos. Doble clic en un mes para ver sus gastos.\n" +
            "Privado: los gastos, la parte mía y la ganancia no se publican en la planilla del tesorero.";
    }

    private static string Porcentaje(decimal pct) => Formato.Decimal1(pct) + "%";

    private void AgregarFilaResumen(string titulo, string activos, string pagaron, string deben,
        decimal ingresado, decimal club, decimal mio, decimal gastos, decimal ganancia)
    {
        int i = _grilla.Rows.Add(
            titulo, activos, pagaron, deben,
            Formato.Moneda(ingresado),
            Formato.Moneda(club),
            Formato.Moneda(mio),
            Formato.Moneda(gastos),
            Formato.Moneda(ganancia));
        var fila = _grilla.Rows[i];
        fila.DefaultCellStyle.BackColor = FondoTotales;
        fila.DefaultCellStyle.Font = _fuenteNegrita;
        if (ganancia < 0)
            fila.Cells["Ganancia"].Style.ForeColor = ColorNegativo;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _fuenteNegrita.Dispose();
        base.Dispose(disposing);
    }

    private void AbrirGastosDelMes(int fila)
    {
        if (fila < 0 || _grilla.Rows[fila].Tag is not ResumenMensual m) return;
        using var form = new GastosForm(_db, m.Anio, m.Mes);
        form.ShowDialog(this);
        Recargar(); // por si se cargaron o editaron gastos
    }
}
