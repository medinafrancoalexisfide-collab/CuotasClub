namespace CuotasClub.Models;

/// <summary>Gasto personal del instructor. Solo se guarda en SQLite, nunca se publica.</summary>
public class Gasto
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public CategoriaGasto Categoria { get; set; } = CategoriaGasto.Transporte;
    public string Descripcion { get; set; } = "";
    public decimal Monto { get; set; }
}
