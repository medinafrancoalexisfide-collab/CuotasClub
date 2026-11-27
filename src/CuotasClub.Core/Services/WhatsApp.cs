using System.Text;
using CuotasClub.Models;

namespace CuotasClub.Services;

/// <summary>
/// Números y mensajes para escribir por WhatsApp (Argentina).
/// El teléfono se toma de <see cref="Alumno.CelularContacto"/>: Cadete → tutor, Activo → alumno.
/// </summary>
public static class WhatsApp
{
    /// <summary>
    /// Lleva un celular argentino al formato de WhatsApp: 549 + código de área + número (13 dígitos).
    /// Acepta "3764 15-123456", "03764 123456", "+54 9 3764 123456", "11 1512345678", etc.
    /// Devuelve null si no se puede (vacío, o sin código de área: hacen falta 10 dígitos en total).
    /// </summary>
    public static string? NormalizarNumero(string? telefono)
    {
        if (string.IsNullOrWhiteSpace(telefono)) return null;
        var d = new string(telefono.Where(char.IsAsciiDigit).ToArray());

        if (d.StartsWith("00")) d = d[2..];
        if (d.StartsWith("54") && d.Length >= 12)
        {
            d = d[2..];
            if (d.StartsWith('9')) d = d[1..];
        }
        if (d.StartsWith('0')) d = d[1..];

        // "15" de celular después del código de área (de 4, 3 o 2 dígitos).
        if (d.Length == 12)
        {
            foreach (int area in new[] { 4, 3, 2 })
                if (d.Substring(area, 2) == "15") { d = d.Remove(area, 2); break; }
        }

        return d.Length == 10 ? "549" + d : null;
    }

    /// <summary>Enlace que abre el chat con el mensaje ya escrito.</summary>
    public static string Enlace(string numero, string mensaje) =>
        $"https://wa.me/{numero}?text={Uri.EscapeDataString(mensaje)}";

    /// <summary>Por qué no se puede escribir, o null si se puede.</summary>
    public static string? ProblemaConTelefono(Alumno a)
    {
        var quien = a.TieneTutor ? "del tutor de " + a.Nombre : "de " + a.Nombre;
        if (a.CelularContacto is null)
            return $"No hay teléfono cargado {quien}. Cargalo en Alumnos.";
        if (NormalizarNumero(a.CelularContacto) is null)
            return $"El teléfono {quien} ({a.CelularContacto}) no parece un celular completo. " +
                   "Cargalo con el código de área, por ejemplo 3764 123456.";
        return null;
    }

    /// <summary>"Hola Ana! Te paso el recibo de la cuota de noviembre 2026 de Juan. ¡Gracias!"</summary>
    public static string MensajeRecibo(Alumno a, Pago pago)
    {
        var mes = MesTexto(pago.Anio, pago.Mes);
        return a.TieneTutor
            ? $"{Saludo(a)} Te paso el recibo de la cuota de {mes} de {a.Nombre.Trim()}. ¡Gracias!"
            : $"{Saludo(a)} Te paso el recibo de tu cuota de {mes}. ¡Gracias!";
    }

    /// <summary>Recordatorio amable de las cuotas vencidas, con los meses y el total.</summary>
    public static string MensajeRecordatorio(Deudor d)
    {
        var a = d.Alumno;
        var meses = ListaMeses(d.MesesAdeudados);
        var monto = Formato.Moneda(d.MontoAdeudado);
        var cuotas = d.CantidadMeses == 1
            ? $"quedó pendiente la cuota de {meses}"
            : $"quedaron pendientes las cuotas de {meses}";
        var de = a.TieneTutor ? $" de {a.Nombre.Trim()}" : "";
        var total = d.CantidadMeses == 1 ? $"({monto})" : $"(en total {monto})";
        return $"{Saludo(a)} ¿Cómo estás? Te escribo para recordarte que {cuotas}{de} {total}. " +
               "Cuando puedas, avisame. ¡Gracias!";
    }

    private static string Saludo(Alumno a)
    {
        var nombre = a.TieneTutor ? PrimerNombre(a.Tutor) : PrimerNombre(a.Nombre);
        return nombre is null ? "¡Hola!" : $"¡Hola {nombre}!";
    }

    private static string? PrimerNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return null;
        return nombre.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
    }

    private static string MesTexto(int anio, int mes) => $"{Formato.NombreMes(mes).ToLowerInvariant()} {anio}";

    /// <summary>"octubre 2026", "septiembre y octubre 2026", "diciembre 2026 y enero 2027".</summary>
    private static string ListaMeses(IReadOnlyList<(int Anio, int Mes)> meses)
    {
        var grupos = meses.GroupBy(m => m.Anio)
            .Select(g => UnirConY(g.Select(m => Formato.NombreMes(m.Mes).ToLowerInvariant()).ToList()) + " " + g.Key)
            .ToList();
        return UnirConY(grupos);
    }

    private static string UnirConY(IReadOnlyList<string> partes)
    {
        if (partes.Count <= 1) return partes.FirstOrDefault() ?? "";
        var sb = new StringBuilder(string.Join(", ", partes.Take(partes.Count - 1)));
        return sb.Append(" y ").Append(partes[^1]).ToString();
    }
}
