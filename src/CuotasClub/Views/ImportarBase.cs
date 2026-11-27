using CuotasClub.Data;
using CuotasClub.Services;

namespace CuotasClub.Views;

/// <summary>
/// "Traer mis datos": reemplaza la base actual por otra elegida por el usuario (la cuotasclub.db de la versión
/// anterior, la de otra PC o una copia de seguridad). Se usa en el asistente de primer uso y en Configuración.
/// </summary>
internal static class ImportarBase
{
    /// <summary>Devuelve true si trajo los datos. Antes guarda una copia de la base actual.</summary>
    public static bool Elegir(IWin32Window owner, Database db)
    {
        var documentos = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CuotasClub");
        using var dlg = new OpenFileDialog
        {
            Title = "Elegí el archivo con tus datos (cuotasclub.db o una copia de seguridad)",
            Filter = "Datos de CuotasClub (*.db)|*.db",
            InitialDirectory = Directory.Exists(documentos) ? documentos : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        };
        if (dlg.ShowDialog(owner) != DialogResult.OK) return false;

        if (Rutas.MismaCarpeta(dlg.FileName, db.RutaArchivo))
        {
            VistasComun.Error(owner, "Ese archivo es la base que el programa ya está usando.");
            return false;
        }
        if (CopiaSeguridadService.ValidarCopia(dlg.FileName) is { } motivo)
        {
            VistasComun.Error(owner, motivo);
            return false;
        }
        var fecha = File.GetLastWriteTime(dlg.FileName).ToString("dd/MM/yyyy HH:mm", VistasComun.Cultura);
        if (!VistasComun.Confirmar(owner, $"¿Traer los datos de {Path.GetFileName(dlg.FileName)} (guardado el {fecha})?{Environment.NewLine}{Environment.NewLine}" +
                                          "Lo que hay ahora en el programa se reemplaza por lo de ese archivo. Antes se guarda una copia de cómo está todo " +
                                          "(termina en \"antes-de-restaurar\"), por si te arrepentís. El archivo elegido no se modifica."))
            return false;
        try
        {
            new CopiaSeguridadService(db).Restaurar(dlg.FileName);
            return true;
        }
        catch (Exception ex) when (ex is ReglaNegocioException or IOException or UnauthorizedAccessException)
        {
            VistasComun.Error(owner, "No se pudieron traer los datos: " + ex.Message);
            return false;
        }
    }
}
