namespace CuotasClub;

/// <summary>
/// Error esperable por una regla del negocio (DNI repetido, mes ya pagado, etc.).
/// El mensaje está en español y se puede mostrar tal cual al usuario.
/// </summary>
public class ReglaNegocioException(string mensaje) : InvalidOperationException(mensaje);
