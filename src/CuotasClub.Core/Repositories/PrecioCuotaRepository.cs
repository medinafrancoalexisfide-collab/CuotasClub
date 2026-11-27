using CuotasClub.Data;
using CuotasClub.Models;
using Microsoft.Data.Sqlite;
using static CuotasClub.Data.Conversiones;

namespace CuotasClub.Repositories;

/// <summary>Historial de precios. El mismo precio rige para Activos y Cadetes.</summary>
public class PrecioCuotaRepository(Database db)
{
    public List<PrecioCuota> Listar()
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT Id, VigenteDesdeAnio, VigenteDesdeMes, Monto FROM PreciosCuota ORDER BY VigenteDesdeAnio, VigenteDesdeMes";
        var lista = new List<PrecioCuota>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            lista.Add(new PrecioCuota
            {
                Id = r.GetInt32(0),
                VigenteDesdeAnio = r.GetInt32(1),
                VigenteDesdeMes = r.GetInt32(2),
                Monto = r.Pesos("Monto"),
            });
        }
        return lista;
    }

    /// <summary>
    /// Guarda un precio desde un mes. Si ya había un precio que empezaba ese mismo mes, lo reemplaza.
    /// No toca los pagos ya registrados (cada pago guarda su monto).
    /// </summary>
    public void Guardar(int desdeAnio, int desdeMes, decimal monto)
    {
        if (desdeMes is < 1 or > 12) throw new ReglaNegocioException("El mes tiene que estar entre 1 y 12.");
        if (monto <= 0) throw new ReglaNegocioException("El precio tiene que ser mayor a cero.");
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO PreciosCuota (VigenteDesdeAnio, VigenteDesdeMes, Monto) VALUES ($a, $m, $monto)
            ON CONFLICT (VigenteDesdeAnio, VigenteDesdeMes) DO UPDATE SET Monto = excluded.Monto
            """;
        cmd.Parameters.AddWithValue("$a", desdeAnio);
        cmd.Parameters.AddWithValue("$m", desdeMes);
        cmd.Parameters.AddWithValue("$monto", PesosACentavos(monto));
        cmd.ExecuteNonQuery();
    }

    public void Eliminar(int id)
    {
        using var cn = db.AbrirConexion();
        using var tx = cn.BeginTransaction();
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(*) FROM PreciosCuota";
        if ((long)cmd.ExecuteScalar()! <= 1) throw new ReglaNegocioException("Tiene que quedar al menos un precio cargado.");
        cmd.CommandText = "DELETE FROM PreciosCuota WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    /// <summary>Precio que rige en ese mes (el último con vigencia ≤ al mes), o null si no hay ninguno.</summary>
    public decimal? ObtenerVigente(int anio, int mes) => PrecioCuotaCalculo.Vigente(Listar(), anio, mes);
}

/// <summary>Cálculo puro del precio vigente, separado para poder probarlo y reusarlo sin ir a la base por cada mes.</summary>
public static class PrecioCuotaCalculo
{
    public static decimal? Vigente(IEnumerable<PrecioCuota> precios, int anio, int mes)
    {
        var clave = anio * 100 + mes;
        return precios.Where(p => p.ClaveDesde <= clave).OrderByDescending(p => p.ClaveDesde).FirstOrDefault()?.Monto;
    }

    /// <summary>% que rige en ese mes; 0 si todavía no se cargó ninguno para esa fecha.</summary>
    public static decimal PorcentajeVigente(IEnumerable<PorcentajeClub> porcentajes, int anio, int mes) =>
        PorcentajeVigenteONulo(porcentajes, anio, mes) ?? 0m;

    public static decimal? PorcentajeVigenteONulo(IEnumerable<PorcentajeClub> porcentajes, int anio, int mes)
    {
        var clave = anio * 100 + mes;
        return porcentajes.Where(p => p.ClaveDesde <= clave).OrderByDescending(p => p.ClaveDesde).FirstOrDefault()?.Porcentaje;
    }
}

/// <summary>Historial del % del club (misma lógica de vigencia que los precios).</summary>
public class PorcentajeClubRepository(Database db)
{
    public List<PorcentajeClub> Listar()
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT Id, VigenteDesdeAnio, VigenteDesdeMes, Porcentaje FROM PorcentajesClub ORDER BY VigenteDesdeAnio, VigenteDesdeMes";
        var lista = new List<PorcentajeClub>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            lista.Add(new PorcentajeClub
            {
                Id = r.GetInt32(0),
                VigenteDesdeAnio = r.GetInt32(1),
                VigenteDesdeMes = r.GetInt32(2),
                Porcentaje = (decimal)r.GetDouble(3),
            });
        }
        return lista;
    }

    /// <summary>Guarda un % desde un mes (si ya había uno ese mismo mes, lo reemplaza). No modifica pagos ya registrados.</summary>
    public void Guardar(int desdeAnio, int desdeMes, decimal porcentaje)
    {
        if (desdeMes is < 1 or > 12) throw new ReglaNegocioException("El mes tiene que estar entre 1 y 12.");
        if (porcentaje is < 0 or > 100) throw new ReglaNegocioException("El porcentaje del club tiene que estar entre 0 y 100.");
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO PorcentajesClub (VigenteDesdeAnio, VigenteDesdeMes, Porcentaje) VALUES ($a, $m, $p)
            ON CONFLICT (VigenteDesdeAnio, VigenteDesdeMes) DO UPDATE SET Porcentaje = excluded.Porcentaje
            """;
        cmd.Parameters.AddWithValue("$a", desdeAnio);
        cmd.Parameters.AddWithValue("$m", desdeMes);
        cmd.Parameters.AddWithValue("$p", (double)porcentaje);
        cmd.ExecuteNonQuery();
    }

    public void Eliminar(int id)
    {
        using var cn = db.AbrirConexion();
        using var tx = cn.BeginTransaction();
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT COUNT(*) FROM PorcentajesClub";
        if ((long)cmd.ExecuteScalar()! <= 1) throw new ReglaNegocioException("Tiene que quedar al menos un porcentaje cargado.");
        cmd.CommandText = "DELETE FROM PorcentajesClub WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
        tx.Commit();
    }

    /// <summary>% que rige en ese mes; 0 si no hay ninguno cargado para esa fecha.</summary>
    public decimal ObtenerVigente(int anio, int mes) => PrecioCuotaCalculo.PorcentajeVigente(Listar(), anio, mes);

    /// <summary>% que rige en ese mes, o null si no hay ninguno cargado para esa fecha.</summary>
    public decimal? ObtenerVigenteONulo(int anio, int mes) => PrecioCuotaCalculo.PorcentajeVigenteONulo(Listar(), anio, mes);
}
