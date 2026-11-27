using CuotasClub.Data;
using CuotasClub.Models;
using Microsoft.Data.Sqlite;
using static CuotasClub.Data.Conversiones;

namespace CuotasClub.Repositories;

public class GastoRepository(Database db)
{
    private const string Columnas = "Id, Fecha, Categoria, Descripcion, Monto";

    public int Insertar(Gasto g)
    {
        Validar(g);
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Gastos (Fecha, Categoria, Descripcion, Monto) VALUES ($Fecha, $Categoria, $Descripcion, $Monto);
            SELECT last_insert_rowid();
            """;
        CargarParametros(cmd, g);
        g.Id = (int)(long)cmd.ExecuteScalar()!;
        return g.Id;
    }

    public void Actualizar(Gasto g)
    {
        Validar(g);
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "UPDATE Gastos SET Fecha = $Fecha, Categoria = $Categoria, Descripcion = $Descripcion, Monto = $Monto WHERE Id = $Id";
        CargarParametros(cmd, g);
        cmd.Parameters.AddWithValue("$Id", g.Id);
        if (cmd.ExecuteNonQuery() == 0) throw new ReglaNegocioException("El gasto no existe.");
    }

    public void Eliminar(int id)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "DELETE FROM Gastos WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        cmd.ExecuteNonQuery();
    }

    public List<Gasto> ListarPorMes(int anio, int mes)
    {
        var (desde, hasta) = RangoMes(anio, mes);
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {Columnas} FROM Gastos WHERE Fecha >= $d AND Fecha < $h ORDER BY Fecha, Id";
        cmd.Parameters.AddWithValue("$d", desde);
        cmd.Parameters.AddWithValue("$h", hasta);
        return Leer(cmd);
    }

    public decimal SumarPorMes(int anio, int mes)
    {
        var (desde, hasta) = RangoMes(anio, mes);
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(SUM(Monto), 0) FROM Gastos WHERE Fecha >= $d AND Fecha < $h";
        cmd.Parameters.AddWithValue("$d", desde);
        cmd.Parameters.AddWithValue("$h", hasta);
        return CentavosAPesos((long)cmd.ExecuteScalar()!);
    }

    /// <summary>Suma de gastos de cada mes del año. Índice 0 = enero.</summary>
    public decimal[] SumarPorMesDelAnio(int anio)
    {
        var totales = new decimal[12];
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT CAST(substr(Fecha, 6, 2) AS INTEGER) AS Mes, SUM(Monto)
            FROM Gastos WHERE Fecha >= $d AND Fecha < $h GROUP BY Mes
            """;
        cmd.Parameters.AddWithValue("$d", $"{anio:0000}-01-01");
        cmd.Parameters.AddWithValue("$h", $"{anio + 1:0000}-01-01");
        using var r = cmd.ExecuteReader();
        while (r.Read()) totales[r.GetInt32(0) - 1] = CentavosAPesos(r.GetInt64(1));
        return totales;
    }

    /// <summary>Descripciones más usadas, para autocompletar ("Uber al club", "Colectivo").</summary>
    public List<string> DescripcionesFrecuentes(int max = 50)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT Descripcion FROM Gastos WHERE trim(Descripcion) <> ''
            GROUP BY Descripcion ORDER BY COUNT(*) DESC, MAX(Fecha) DESC LIMIT $max
            """;
        cmd.Parameters.AddWithValue("$max", max);
        var lista = new List<string>();
        using var r = cmd.ExecuteReader();
        while (r.Read()) lista.Add(r.GetString(0));
        return lista;
    }

    private static void Validar(Gasto g)
    {
        if (g.Monto <= 0) throw new ReglaNegocioException("El monto del gasto tiene que ser mayor a cero.");
    }

    private static (string desde, string hasta) RangoMes(int anio, int mes)
    {
        var d = new DateOnly(anio, mes, 1);
        return (FechaATexto(d), FechaATexto(d.AddMonths(1)));
    }

    private static void CargarParametros(SqliteCommand cmd, Gasto g)
    {
        cmd.Parameters.AddWithValue("$Fecha", FechaATexto(g.Fecha));
        cmd.Parameters.AddWithValue("$Categoria", g.Categoria.ATexto());
        cmd.Parameters.AddWithValue("$Descripcion", (g.Descripcion ?? "").Trim());
        cmd.Parameters.AddWithValue("$Monto", PesosACentavos(g.Monto));
    }

    private static List<Gasto> Leer(SqliteCommand cmd)
    {
        var lista = new List<Gasto>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            lista.Add(new Gasto
            {
                Id = r.GetInt32(0),
                Fecha = r.Fecha("Fecha"),
                Categoria = EnumTextos.ParseCategoriaGasto(r.GetString(2)),
                Descripcion = r.GetString(3),
                Monto = r.Pesos("Monto"),
            });
        }
        return lista;
    }
}
