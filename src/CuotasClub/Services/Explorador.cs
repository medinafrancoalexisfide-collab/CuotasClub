using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CuotasClub.Services;

/// <summary>
/// Abre carpetas en el Explorador de Windows sin repetir ventanas: si la carpeta ya está
/// abierta, trae esa ventana al frente (y selecciona el archivo) en vez de abrir otra.
/// Lo usan los recibos y las copias de seguridad; sirve igual para cualquier otro botón.
/// </summary>
internal static class Explorador
{
    // Seleccionar + quitar las otras selecciones + hacerlo visible + darle el foco.
    private const int SeleccionCompleta = 1 | 4 | 8 | 16;
    private const int SW_RESTORE = 9;

    /// <summary>Muestra la carpeta del archivo con el archivo seleccionado.</summary>
    public static void MostrarArchivo(string rutaArchivo)
    {
        var carpeta = Path.GetDirectoryName(rutaArchivo);
        if (string.IsNullOrEmpty(carpeta)) return;
        if (TraerVentanaExistente(carpeta, Path.GetFileName(rutaArchivo))) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{rutaArchivo}\"") { UseShellExecute = true });
    }

    /// <summary>Muestra la carpeta (la crea si no existe).</summary>
    public static void AbrirCarpeta(string carpeta)
    {
        Directory.CreateDirectory(carpeta);
        if (TraerVentanaExistente(carpeta, null)) return;
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{carpeta}\"") { UseShellExecute = true });
    }

    /// <summary>Busca una ventana del Explorador ya abierta en esa carpeta. Si algo falla, devuelve false y se abre una nueva.</summary>
    private static bool TraerVentanaExistente(string carpeta, string? archivo)
    {
        try
        {
            var tipo = Type.GetTypeFromProgID("Shell.Application");
            if (tipo == null) return false;
            dynamic shell = Activator.CreateInstance(tipo)!;
            dynamic ventanas = shell.Windows();
            int cantidad = ventanas.Count;
            for (int i = 0; i < cantidad; i++)
            {
                try
                {
                    dynamic? ventana = ventanas.Item(i);
                    if (ventana == null) continue;
                    dynamic doc = ventana.Document;
                    string ruta = doc.Folder.Self.Path;
                    if (!Rutas.MismaCarpeta(ruta, carpeta)) continue;

                    if (archivo != null) Seleccionar(doc, archivo);
                    var hwnd = new IntPtr((long)ventana.HWND);
                    if (IsIconic(hwnd)) ShowWindow(hwnd, SW_RESTORE);
                    SetForegroundWindow(hwnd);
                    return true;
                }
                catch (Exception) { /* ventana que no es una carpeta (o se cerró): se sigue con la próxima */ }
            }
        }
        catch (Exception) { /* sin acceso al Shell: se abre una ventana nueva como siempre */ }
        return false;
    }

    private static void Seleccionar(dynamic doc, string archivo)
    {
        // El archivo recién creado puede tardar un instante en aparecer en la ventana.
        for (int intento = 0; intento < 6; intento++)
        {
            dynamic? item = doc.Folder.ParseName(archivo);
            if (item != null)
            {
                doc.SelectItem(item, SeleccionCompleta);
                return;
            }
            Thread.Sleep(150);
        }
    }

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
}
