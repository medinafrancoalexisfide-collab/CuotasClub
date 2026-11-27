using CuotasClub.Data;
using CuotasClub.Views;

namespace CuotasClub;

internal static class Program
{
    /// <summary>Se pone en true al restaurar una copia: el programa se reinicia sin hacer la copia de cierre.</summary>
    public static bool OmitirCopiaAlCerrar { get; set; }

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => MostrarError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => MostrarError(e.ExceptionObject as Exception);

        Database db;
        try
        {
            // Versiones anteriores guardaban la base junto al .exe: si hay una ahí y todavía no hay
            // ninguna en la carpeta de datos, se copia sola. Si la copia falla, se sigue con una base nueva.
            try { Database.MigrarBaseAntigua(Database.RutaAntigua, Database.RutaPorDefecto); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException) { }

            db = new Database(Database.RutaPorDefecto);
            db.Inicializar();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo abrir la base de datos:{Environment.NewLine}{ex.Message}", "CuotasClub",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Primera vez (o base sin precio / % del club): se piden los datos antes de abrir el menú.
        // Si en el asistente se trae una base anterior que ya tiene todo, no se vuelve a preguntar.
        while (new Services.ConfiguracionService(db).FaltaConfiguracionInicial)
        {
            using var primerUso = new PrimerUsoForm(db);
            if (primerUso.ShowDialog() != DialogResult.OK) return;
        }

        Services.Sincronizador.Inicializar(db);
        Application.Run(new MainForm(db));

        // Copia de seguridad al cerrar. Si falla (por ejemplo, Google Drive cerrado), no molesta:
        // queda anotado y se ve en Configuración → Copias de seguridad.
        if (!OmitirCopiaAlCerrar) new Services.CopiaSeguridadService(db).HacerCopiaSilenciosa();
    }

    private static void MostrarError(Exception? ex)
    {
        var mensaje = ex is ReglaNegocioException ? ex.Message : $"Ocurrió un error inesperado:{Environment.NewLine}{ex?.Message}";
        MessageBox.Show(mensaje, "CuotasClub", MessageBoxButtons.OK,
            ex is ReglaNegocioException ? MessageBoxIcon.Warning : MessageBoxIcon.Error);
    }
}
