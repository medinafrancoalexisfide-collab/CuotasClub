using System.Globalization;
using CuotasClub.Data;
using CuotasClub.Models;
using Microsoft.Data.Sqlite;
using static CuotasClub.Data.Conversiones;

namespace CuotasClub.Repositories;

public class AlumnoRepository(Database db)
{
    private const string Columnas = """
        Id, Nombre, Apellido, Dni, FechaNacimiento, IdFide, Celular, TipoSocio,
        ResponsableNombre, ResponsableCelular, FechaAlta, FechaBaja, Observaciones
        """;

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-AR");
    private const CompareOptions SinAcentosNiMayusculas = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>Orden alfabético en español, sin distinguir mayúsculas ni acentos (Álvarez antes que Benítez).</summary>
    public static readonly StringComparer Comparador = StringComparer.Create(Cultura, SinAcentosNiMayusculas);

    private static bool Contiene(string texto, string parte) =>
        Cultura.CompareInfo.IndexOf(texto, parte, SinAcentosNiMayusculas) >= 0;

    /// <summary>
    /// Lista ordenada por apellido y nombre.
    /// </summary>
    /// <param name="soloActivos">true: solo los que no tienen baja o la tienen después de <paramref name="hoy"/>.</param>
    /// <param name="texto">Filtra por nombre, apellido o DNI (contiene, sin distinguir mayúsculas ni acentos).</param>
    /// <param name="hoy">Fecha de referencia para "activos" (por defecto, hoy).</param>
    public List<Alumno> Listar(bool soloActivos = false, string? texto = null, DateOnly? hoy = null)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        var where = new List<string>();
        if (soloActivos)
        {
            where.Add("(FechaBaja IS NULL OR FechaBaja > $hoy)");
            cmd.Parameters.AddWithValue("$hoy", FechaATexto(hoy ?? DateOnly.FromDateTime(DateTime.Today)));
        }
        cmd.CommandText = $"SELECT {Columnas} FROM Alumnos {(where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "")}";
        // Orden y búsqueda en C#: SQLite (NOCASE) no entiende acentos ni la Ñ, y con decenas de alumnos no hay costo.
        IEnumerable<Alumno> lista = Leer(cmd);
        if (soloActivos)
        {
            var h = hoy ?? DateOnly.FromDateTime(DateTime.Today);
            lista = lista.Where(a => !a.Ausencias.Any(x => x.FechaBaja < h && h < x.FechaRegreso)); // ausente hoy, vuelve más adelante
        }
        if (!string.IsNullOrWhiteSpace(texto))
        {
            // Cada palabra tiene que aparecer en nombre, apellido o DNI: "perez juan" encuentra a "Pérez, Juan".
            var palabras = texto.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            lista = lista.Where(a => palabras.All(p => Contiene(a.Nombre, p) || Contiene(a.Apellido, p) || a.Dni.Contains(p)));
        }
        return lista.OrderBy(a => a.Apellido, Comparador).ThenBy(a => a.Nombre, Comparador).ToList();
    }

    public Alumno? ObtenerPorId(int id)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {Columnas} FROM Alumnos WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        return Leer(cmd).FirstOrDefault();
    }

    public bool ExisteDni(string dni, int? excluirId = null)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Alumnos WHERE Dni = $dni AND Id <> $id";
        cmd.Parameters.AddWithValue("$dni", dni.Trim());
        cmd.Parameters.AddWithValue("$id", excluirId ?? 0);
        return (long)cmd.ExecuteScalar()! > 0;
    }

    /// <summary>Inserta y devuelve el Id generado (también lo asigna en <paramref name="a"/>).</summary>
    public int Insertar(Alumno a)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Alumnos (Nombre, Apellido, Dni, FechaNacimiento, IdFide, Celular, TipoSocio,
                                 ResponsableNombre, ResponsableCelular, FechaAlta, FechaBaja, Observaciones)
            VALUES ($Nombre, $Apellido, $Dni, $FechaNacimiento, $IdFide, $Celular, $TipoSocio,
                    $ResponsableNombre, $ResponsableCelular, $FechaAlta, $FechaBaja, $Observaciones);
            SELECT last_insert_rowid();
            """;
        CargarParametros(cmd, a);
        a.Id = (int)(long)cmd.ExecuteScalar()!;
        return a.Id;
    }

    public void Actualizar(Alumno a)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            UPDATE Alumnos SET Nombre = $Nombre, Apellido = $Apellido, Dni = $Dni, FechaNacimiento = $FechaNacimiento,
                IdFide = $IdFide, Celular = $Celular, TipoSocio = $TipoSocio, ResponsableNombre = $ResponsableNombre,
                ResponsableCelular = $ResponsableCelular, FechaAlta = $FechaAlta, FechaBaja = $FechaBaja,
                Observaciones = $Observaciones
            WHERE Id = $Id
            """;
        CargarParametros(cmd, a);
        cmd.Parameters.AddWithValue("$Id", a.Id);
        if (cmd.ExecuteNonQuery() == 0) throw new ReglaNegocioException("El alumno no existe.");
    }

    public void DarDeBaja(int id, DateOnly fechaBaja)
    {
        if (ObtenerPorId(id) is { } actual && actual.Ausencias.Count > 0 && fechaBaja < actual.Ausencias.Max(x => x.FechaRegreso))
            throw new ReglaNegocioException(
                $"La fecha de baja no puede ser anterior a su último regreso ({actual.Ausencias.Max(x => x.FechaRegreso):dd/MM/yyyy}).");
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "UPDATE Alumnos SET FechaBaja = $f WHERE Id = $id";
        cmd.Parameters.AddWithValue("$f", FechaATexto(fechaBaja));
        cmd.Parameters.AddWithValue("$id", id);
        try
        {
            if (cmd.ExecuteNonQuery() == 0) throw new ReglaNegocioException("El alumno no existe.");
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new ReglaNegocioException("La fecha de baja no puede ser anterior a la fecha de alta.");
        }
    }

    /// <summary>
    /// El alumno vuelve después de una baja. La baja no se borra: queda guardada como ausencia
    /// (de la baja al regreso), así esos meses siguen en "—" y se cobra desde el mes de regreso.
    /// </summary>
    public void Reactivar(int id, DateOnly fechaRegreso)
    {
        var a = ObtenerPorId(id) ?? throw new ReglaNegocioException("El alumno no existe.");
        if (a.FechaBaja is not { } baja) throw new ReglaNegocioException($"{a.NombreCompleto} no está dado de baja.");
        if (fechaRegreso <= baja)
            throw new ReglaNegocioException($"La fecha de regreso tiene que ser posterior a la baja ({baja:dd/MM/yyyy}).");

        using var cn = db.AbrirConexion();
        using var tx = cn.BeginTransaction();
        using (var cmd = cn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO Ausencias (AlumnoId, FechaBaja, FechaRegreso) VALUES ($id, $b, $r)";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$b", FechaATexto(baja));
            cmd.Parameters.AddWithValue("$r", FechaATexto(fechaRegreso));
            cmd.ExecuteNonQuery();
        }
        using (var cmd = cn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE Alumnos SET FechaBaja = NULL WHERE Id = $id";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }

    /// <summary>La baja fue un error: se borra y el alumno figura activo desde su alta original, sin hueco.</summary>
    public void QuitarBaja(int id)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "UPDATE Alumnos SET FechaBaja = NULL WHERE Id = $id";
        cmd.Parameters.AddWithValue("$id", id);
        if (cmd.ExecuteNonQuery() == 0) throw new ReglaNegocioException("El alumno no existe.");
    }

    /// <summary>Borra una ausencia cargada por error (los meses vuelven a corresponder).</summary>
    public void BorrarAusencia(int alumnoId, Ausencia ausencia)
    {
        using var cn = db.AbrirConexion();
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "DELETE FROM Ausencias WHERE AlumnoId = $id AND FechaBaja = $b AND FechaRegreso = $r";
        cmd.Parameters.AddWithValue("$id", alumnoId);
        cmd.Parameters.AddWithValue("$b", FechaATexto(ausencia.FechaBaja));
        cmd.Parameters.AddWithValue("$r", FechaATexto(ausencia.FechaRegreso));
        cmd.ExecuteNonQuery();
    }

    private static void CargarParametros(SqliteCommand cmd, Alumno a)
    {
        cmd.Parameters.AddWithValue("$Nombre", a.Nombre.Trim());
        cmd.Parameters.AddWithValue("$Apellido", a.Apellido.Trim());
        cmd.Parameters.AddWithValue("$Dni", a.Dni.Trim());
        cmd.Parameters.AddWithValue("$FechaNacimiento", FechaATexto(a.FechaNacimiento));
        cmd.Parameters.AddWithValue("$IdFide", ANull(a.IdFide));
        cmd.Parameters.AddWithValue("$Celular", ANull(a.Celular));
        cmd.Parameters.AddWithValue("$TipoSocio", a.TipoSocio.ATexto());
        cmd.Parameters.AddWithValue("$ResponsableNombre", ANull(a.ResponsableNombre));
        cmd.Parameters.AddWithValue("$ResponsableCelular", ANull(a.ResponsableCelular));
        cmd.Parameters.AddWithValue("$FechaAlta", FechaATexto(a.FechaAlta));
        cmd.Parameters.AddWithValue("$FechaBaja", ANull(a.FechaBaja));
        cmd.Parameters.AddWithValue("$Observaciones", ANull(a.Observaciones));
    }

    private static List<Alumno> Leer(SqliteCommand cmd)
    {
        var lista = new List<Alumno>();
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            lista.Add(new Alumno
            {
                Id = r.GetInt32(r.GetOrdinal("Id")),
                Nombre = r.GetString(r.GetOrdinal("Nombre")),
                Apellido = r.GetString(r.GetOrdinal("Apellido")),
                Dni = r.GetString(r.GetOrdinal("Dni")),
                FechaNacimiento = r.Fecha("FechaNacimiento"),
                IdFide = r.TextoONull("IdFide"),
                Celular = r.TextoONull("Celular"),
                TipoSocio = EnumTextos.ParseTipoSocio(r.GetString(r.GetOrdinal("TipoSocio"))),
                ResponsableNombre = r.TextoONull("ResponsableNombre"),
                ResponsableCelular = r.TextoONull("ResponsableCelular"),
                FechaAlta = r.Fecha("FechaAlta"),
                FechaBaja = r.FechaONull("FechaBaja"),
                Observaciones = r.TextoONull("Observaciones"),
            });
        }
        r.Close();
        if (lista.Count > 0) CargarAusencias(cmd.Connection!, lista);
        return lista;
    }

    private static void CargarAusencias(SqliteConnection cn, List<Alumno> alumnos)
    {
        var porId = alumnos.ToDictionary(a => a.Id);
        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT AlumnoId, FechaBaja, FechaRegreso FROM Ausencias ORDER BY FechaBaja";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            if (porId.TryGetValue(r.GetInt32(0), out var a))
                a.Ausencias.Add(new Ausencia(r.Fecha("FechaBaja"), r.Fecha("FechaRegreso")));
    }
}
