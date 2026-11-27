namespace CuotasClub.Models;

public class Pago
{
    public int Id { get; set; }
    public int AlumnoId { get; set; }
    public int Anio { get; set; }
    /// <summary>1 a 12.</summary>
    public int Mes { get; set; }
    /// <summary>Precio vigente del mes al momento de registrar el pago.</summary>
    public decimal Monto { get; set; }
    /// <summary>% del club que rige para este pago (se fija al registrarlo, no cambia después).</summary>
    public decimal PorcentajeClub { get; set; }
    public DateOnly FechaPago { get; set; }
    public MedioPago MedioPago { get; set; }
    public bool Anulado { get; set; }
}
