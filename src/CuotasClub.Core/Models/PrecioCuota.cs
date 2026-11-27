namespace CuotasClub.Models;

/// <summary>Precio de la cuota a partir de un mes. Rige hasta que aparece otro precio con fecha posterior.</summary>
public class PrecioCuota
{
    public int Id { get; set; }
    public int VigenteDesdeAnio { get; set; }
    public int VigenteDesdeMes { get; set; }
    public decimal Monto { get; set; }

    /// <summary>Clave ordenable anio*100+mes (ej. 202701).</summary>
    public int ClaveDesde => VigenteDesdeAnio * 100 + VigenteDesdeMes;
}

/// <summary>% del club a partir de un mes. Rige hasta que aparece otro con fecha posterior.</summary>
public class PorcentajeClub
{
    public int Id { get; set; }
    public int VigenteDesdeAnio { get; set; }
    public int VigenteDesdeMes { get; set; }
    public decimal Porcentaje { get; set; }

    public int ClaveDesde => VigenteDesdeAnio * 100 + VigenteDesdeMes;
}
