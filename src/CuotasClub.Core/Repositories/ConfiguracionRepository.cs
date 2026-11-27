using CuotasClub.Data;

namespace CuotasClub.Repositories;

/// <summary>Tabla clave/valor. Para valores con tipo usar <see cref="Services.ConfiguracionService"/>.</summary>
public class ConfiguracionRepository(Database db)
{
    public string? Obtener(string clave)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT Valor FROM Configuracion WHERE Clave = $c";
        cmd.Parameters.AddWithValue("$c", clave);
        return cmd.ExecuteScalar() as string;
    }

    public void Guardar(string clave, string? valor)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Configuracion (Clave, Valor) VALUES ($c, $v)
            ON CONFLICT (Clave) DO UPDATE SET Valor = excluded.Valor
            """;
        cmd.Parameters.AddWithValue("$c", clave);
        cmd.Parameters.AddWithValue("$v", (object?)valor ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }
}
