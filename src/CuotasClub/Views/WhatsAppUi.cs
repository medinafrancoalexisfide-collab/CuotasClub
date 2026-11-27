using System.Diagnostics;
using CuotasClub.Models;
using CuotasClub.Services;

namespace CuotasClub.Views;

/// <summary>Abre WhatsApp con el chat del alumno (o del tutor) y el mensaje ya escrito.</summary>
internal static class WhatsAppUi
{
    /// <summary>Devuelve false si no hay un teléfono usable (y avisa por qué) o si no se pudo abrir.</summary>
    public static bool AbrirChat(IWin32Window owner, Alumno alumno, string mensaje)
    {
        if (WhatsApp.ProblemaConTelefono(alumno) is { } problema)
        {
            VistasComun.Error(owner, problema);
            return false;
        }
        var numero = WhatsApp.NormalizarNumero(alumno.CelularContacto)!;
        try
        {
            Process.Start(new ProcessStartInfo(WhatsApp.Enlace(numero, mensaje)) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            VistasComun.Error(owner, "No se pudo abrir WhatsApp: " + ex.Message);
            return false;
        }
    }
}
