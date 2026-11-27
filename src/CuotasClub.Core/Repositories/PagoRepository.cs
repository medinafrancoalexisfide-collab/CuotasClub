using CuotasClub.Data;
using CuotasClub.Models;
using Microsoft.Data.Sqlite;
using static CuotasClub.Data.Conversiones;

namespace CuotasClub.Repositories;

public class PagoRepository(Database db)
{
    private const string Columnas = "Id, AlumnoId, Anio, Mes, Monto, FechaPago, MedioPago, Anulado, PorcentajeClub";

    public Pago? ObtenerPorId(int id)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {Columnas} FROM Pagos WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return Leer(cmd).FirstOrDefault();
    }

    /// <summary>El pago vigente (no anulado) de un alumno en un mes, o null.</summary>
    public Pago? ObtenerNoAnulado(int alumnoId, int anio, int mes)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {Columnas} FROM Pagos WHERE AlumnoId = $a AND Anio = $anio AND Mes = $mes AND Anulado = 0";
        cmd.Parameters.AddWithValue("$a", alumnoId);
        cmd.Parameters.AddWithValue("$anio", anio);
        cmd.Parameters.AddWithValue("$mes", mes);
        return Leer(cmd).FirstOrDefault();
    }

    public List<Pago> ListarNoAnuladosDelAnio(int anio)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {Columnas} FROM Pagos WHERE Anio = $anio AND Anulado = 0 ORDER BY Mes, AlumnoId";
        cmd.Parameters.AddWithValue("$anio", anio);
        return Leer(cmd);
    }

    public List<Pago> ListarNoAnuladosDelMes(int anio, int mes)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {Columnas} FROM Pagos WHERE Anio = $anio AND Mes = $mes AND Anulado = 0 ORDER BY AlumnoId";
        cmd.Parameters.AddWithValue("$anio", anio);
        cmd.Parameters.AddWithValue("$mes", mes);
        return Leer(cmd);
    }

    /// <summary>Todos los pagos no anulados (para calcular deudas de varios años).</summary>
    public List<Pago> ListarNoAnulados()
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {Columnas} FROM Pagos WHERE Anulado = 0 ORDER BY Anio, Mes, AlumnoId";
        return Leer(cmd);
    }

    /// <summary>Historial completo de un alumno, incluidos los anulados.</summary>
    public List<Pago> ListarPorAlumno(int alumnoId)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {Columnas} FROM Pagos WHERE AlumnoId = $a ORDER BY Anio, Mes, Id";
        cmd.Parameters.AddWithValue("$a", alumnoId);
        return Leer(cmd);
    }

    /// <summary>Inserta el pago. Si ya hay uno no anulado para ese alumno y mes lanza <see cref="ReglaNegocioException"/>.</summary>
    public int Insertar(Pago p)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Pagos (AlumnoId, Anio, Mes, Monto, PorcentajeClub, FechaPago, MedioPago, Anulado)
            VALUES ($AlumnoId, $Anio, $Mes, $Monto, $Porcentaje, $FechaPago, $MedioPago, 0);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$AlumnoId", p.AlumnoId);
        cmd.Parameters.AddWithValue("$Anio", p.Anio);
        cmd.Parameters.AddWithValue("$Mes", p.Mes);
        cmd.Parameters.AddWithValue("$Monto", PesosACentavos(p.Monto));
        cmd.Parameters.AddWithValue("$Porcentaje", (double)p.PorcentajeClub);
        cmd.Parameters.AddWithValue("$FechaPago", FechaATexto(p.FechaPago));
        cmd.Parameters.AddWithValue("$MedioPago", p.MedioPago.ATexto());
        try
        {
            p.Id = (int)(long)cmd.ExecuteScalar()!;
            p.Anulado = false;
            return p.Id;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19 && ex.Message.Contains("UNIQUE"))
        {
            throw new ReglaNegocioException("Ese mes ya tiene un pago registrado para este alumno.");
        }
    }

    /// <summary>Marca el pago como anulado (no se borra, queda en el historial).</summary>
    public void Anular(int pagoId)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "UPDATE Pagos SET Anulado = 1 WHERE Id = $id AND Anulado = 0";
        cmd.Parameters.AddWithValue("$id", pagoId);
        if (cmd.ExecuteNonQuery() == 0) throw new ReglaNegocioException("El pago no existe o ya estaba anulado.");
    }

    private static List<Pago> Leer(SqliteCommand cmd)
    {
        var lista = new List<Pago>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            lista.Add(new Pago
            {
                Id = r.GetInt32(0),
                AlumnoId = r.GetInt32(1),
                Anio = r.GetInt32(2),
                Mes = r.GetInt32(3),
                Monto = r.Pesos("Monto"),
                FechaPago = r.Fecha("FechaPago"),
                MedioPago = EnumTextos.ParseMedioPago(r.GetString(6)),
                Anulado = r.GetInt64(7) == 1,
                PorcentajeClub = (decimal)r.GetDouble(8),
            });
        }
        return lista;
    }
}
