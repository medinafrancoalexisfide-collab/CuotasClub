using Microsoft.Data.Sqlite;

namespace CuotasClub.Data;

/// <summary>
/// Definición de las tablas. Convenciones:
/// - Fechas como TEXT 'yyyy-MM-dd'.
/// - Montos como INTEGER en centavos (evita errores de redondeo de REAL). Los modelos usan decimal en pesos.
/// - Enums como TEXT legible ("Activo", "Mercado Pago", "Transporte").
/// </summary>
internal static class Esquema
{
    public const int VersionActual = 3;

    public const string Sql = """
        CREATE TABLE IF NOT EXISTS Alumnos (
            Id                 INTEGER PRIMARY KEY AUTOINCREMENT,
            Nombre             TEXT    NOT NULL,
            Apellido           TEXT    NOT NULL,
            Dni                TEXT    NOT NULL UNIQUE,
            FechaNacimiento    TEXT    NOT NULL,
            IdFide             TEXT    NULL,
            Celular            TEXT    NULL,
            TipoSocio          TEXT    NOT NULL DEFAULT 'Activo' CHECK (TipoSocio IN ('Activo','Cadete')),
            ResponsableNombre  TEXT    NULL,
            ResponsableCelular TEXT    NULL,
            FechaAlta          TEXT    NOT NULL,
            FechaBaja          TEXT    NULL,
            Observaciones      TEXT    NULL,
            CHECK (FechaBaja IS NULL OR FechaBaja >= FechaAlta)
        );
        CREATE INDEX IF NOT EXISTS IX_Alumnos_Apellido ON Alumnos (Apellido COLLATE NOCASE, Nombre COLLATE NOCASE);

        -- Ausencias de alumnos que se fueron y volvieron (desde la versión 0.9). Los meses del medio no se cobran.
        -- Las bases viejas solo suman esta tabla vacía; no se pierde nada.
        CREATE TABLE IF NOT EXISTS Ausencias (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            AlumnoId     INTEGER NOT NULL REFERENCES Alumnos (Id),
            FechaBaja    TEXT    NOT NULL,
            FechaRegreso TEXT    NOT NULL,
            CHECK (FechaRegreso > FechaBaja)
        );
        CREATE INDEX IF NOT EXISTS IX_Ausencias_Alumno ON Ausencias (AlumnoId);

        CREATE TABLE IF NOT EXISTS Pagos (
            Id         INTEGER PRIMARY KEY AUTOINCREMENT,
            AlumnoId   INTEGER NOT NULL REFERENCES Alumnos (Id),
            Anio       INTEGER NOT NULL,
            Mes        INTEGER NOT NULL CHECK (Mes BETWEEN 1 AND 12),
            Monto      INTEGER NOT NULL CHECK (Monto > 0),
            -- % del club vigente para ese mes al registrar el pago (histórico: no cambia si después cambia el %).
            PorcentajeClub REAL NOT NULL CHECK (PorcentajeClub BETWEEN 0 AND 100),
            FechaPago  TEXT    NOT NULL,
            MedioPago  TEXT    NOT NULL CHECK (MedioPago IN ('Efectivo','Transferencia','Mercado Pago')),
            Anulado    INTEGER NOT NULL DEFAULT 0 CHECK (Anulado IN (0,1))
        );
        -- Un solo pago no anulado por alumno y mes. Los anulados quedan como historial.
        CREATE UNIQUE INDEX IF NOT EXISTS UX_Pagos_AlumnoMes ON Pagos (AlumnoId, Anio, Mes) WHERE Anulado = 0;
        CREATE INDEX IF NOT EXISTS IX_Pagos_AnioMes ON Pagos (Anio, Mes);

        CREATE TABLE IF NOT EXISTS Gastos (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
            Fecha       TEXT    NOT NULL,
            Categoria   TEXT    NOT NULL CHECK (Categoria IN ('Transporte','Materiales','Otros')),
            Descripcion TEXT    NOT NULL DEFAULT '',
            Monto       INTEGER NOT NULL CHECK (Monto > 0)
        );
        CREATE INDEX IF NOT EXISTS IX_Gastos_Fecha ON Gastos (Fecha);

        CREATE TABLE IF NOT EXISTS PreciosCuota (
            Id                INTEGER PRIMARY KEY AUTOINCREMENT,
            VigenteDesdeAnio  INTEGER NOT NULL,
            VigenteDesdeMes   INTEGER NOT NULL CHECK (VigenteDesdeMes BETWEEN 1 AND 12),
            Monto             INTEGER NOT NULL CHECK (Monto > 0),
            UNIQUE (VigenteDesdeAnio, VigenteDesdeMes)
        );

        -- Historial del % del club, igual que los precios: rige desde un mes hasta que aparece otro.
        CREATE TABLE IF NOT EXISTS PorcentajesClub (
            Id                INTEGER PRIMARY KEY AUTOINCREMENT,
            VigenteDesdeAnio  INTEGER NOT NULL,
            VigenteDesdeMes   INTEGER NOT NULL CHECK (VigenteDesdeMes BETWEEN 1 AND 12),
            Porcentaje        REAL    NOT NULL CHECK (Porcentaje BETWEEN 0 AND 100),
            UNIQUE (VigenteDesdeAnio, VigenteDesdeMes)
        );

        CREATE TABLE IF NOT EXISTS Configuracion (
            Clave TEXT PRIMARY KEY,
            Valor TEXT NULL
        );
        """;

    /// <summary>
    /// Valores iniciales. Una base nueva arranca vacía: sin precio, sin % del club y sin nombres, para que
    /// quien use el programa los cargue en el asistente de primer uso. Las bases que ya existían no se tocan,
    /// salvo completar el nombre del club y del taller con los que se mostraban hasta la versión 0.7.
    /// <paramref name="hoy"/> solo se pasa en las pruebas.
    /// </summary>
    public static void CargarValoresIniciales(SqliteConnection cn, SqliteTransaction tx, DateOnly hoy)
    {
        Ejecutar(cn, tx, "INSERT OR IGNORE INTO Configuracion (Clave, Valor) VALUES ($c, $v)",
            ("$c", ClavesConfiguracion.SincronizacionPendiente), ("$v", "0"));
        Ejecutar(cn, tx, "INSERT OR IGNORE INTO Configuracion (Clave, Valor) VALUES ($c, $v)",
            ("$c", ClavesConfiguracion.VersionEsquema), ("$v", VersionActual.ToString()));
        Ejecutar(cn, tx, "UPDATE Configuracion SET Valor = $v WHERE Clave = $c",
            ("$c", ClavesConfiguracion.VersionEsquema), ("$v", VersionActual.ToString()));

        var mesActual = hoy.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        bool baseNueva = Escalar(cn, tx, "SELECT COUNT(*) FROM Configuracion WHERE Clave = $c", ("$c", ClavesConfiguracion.InicioCobros)) == 0;
        Ejecutar(cn, tx, "INSERT OR IGNORE INTO Configuracion (Clave, Valor) VALUES ($c, $v)",
            ("$c", ClavesConfiguracion.InicioCobros), ("$v", mesActual));

        if (!baseNueva && Escalar(cn, tx, "SELECT COUNT(*) FROM PreciosCuota") > 0)
        {
            // Bases creadas hasta la 0.7: tenían el club y el taller fijos en el programa. Se guardan una vez
            // para que se sigan viendo igual (después se cambian en Configuración → Datos del club).
            Ejecutar(cn, tx, "INSERT OR IGNORE INTO Configuracion (Clave, Valor) VALUES ($c, $v)",
                ("$c", ClavesConfiguracion.NombreClub), ("$v", "Cuotas Club"));
            Ejecutar(cn, tx, "INSERT OR IGNORE INTO Configuracion (Clave, Valor) VALUES ($c, $v)",
                ("$c", ClavesConfiguracion.NombreTaller), ("$v", "Taller de Ajedrez"));
        }

        if (!baseNueva) PasarArranque2027AlMesActual(cn, tx, hoy);
        CompletarTelefonoSegunTipo(cn, tx);
        Ejecutar(cn, tx, "INSERT OR IGNORE INTO Configuracion (Clave, Valor) VALUES ($c, '1')", ("$c", ClavesConfiguracion.ArranqueRevisado));
    }

    /// <summary>
    /// Las versiones hasta la 0.4 creaban la base con todo fijo en enero 2027. Una sola vez, si la base
    /// todavía tiene ese arranque, no hay pagos y estamos antes de 2027, lo pasa al mes actual.
    /// </summary>
    private static void PasarArranque2027AlMesActual(SqliteConnection cn, SqliteTransaction tx, DateOnly hoy)
    {
        if (Escalar(cn, tx, "SELECT COUNT(*) FROM Configuracion WHERE Clave = $c", ("$c", ClavesConfiguracion.ArranqueRevisado)) > 0) return;
        if (hoy >= new DateOnly(2027, 1, 1)) return;
        if (Escalar(cn, tx, "SELECT COUNT(*) FROM Configuracion WHERE Clave = $c AND Valor = '2027-01'", ("$c", ClavesConfiguracion.InicioCobros)) == 0) return;
        if (Escalar(cn, tx, "SELECT COUNT(*) FROM Pagos") > 0) return;

        Ejecutar(cn, tx, "UPDATE Configuracion SET Valor = $v WHERE Clave = $c",
            ("$c", ClavesConfiguracion.InicioCobros), ("$v", hoy.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)));
        foreach (var tabla in new[] { "PreciosCuota", "PorcentajesClub" })
            Ejecutar(cn, tx, $"""
                UPDATE {tabla} SET VigenteDesdeAnio = $a, VigenteDesdeMes = $m
                WHERE VigenteDesdeAnio = 2027 AND VigenteDesdeMes = 1 AND (SELECT COUNT(*) FROM {tabla}) = 1
                """, ("$a", hoy.Year), ("$m", hoy.Month));
    }

    /// <summary>
    /// Desde la v0.10 el Cadete muestra solo el teléfono del tutor y el Activo solo el suyo. Si un alumno tenía
    /// cargado únicamente el otro teléfono, se copia al que ahora se ve. No se borra nada (es idempotente).
    /// </summary>
    private static void CompletarTelefonoSegunTipo(SqliteConnection cn, SqliteTransaction tx)
    {
        Ejecutar(cn, tx, """
            UPDATE Alumnos SET Celular = ResponsableCelular
            WHERE TipoSocio = 'Activo' AND TRIM(IFNULL(Celular, '')) = '' AND TRIM(IFNULL(ResponsableCelular, '')) <> ''
            """);
        Ejecutar(cn, tx, """
            UPDATE Alumnos SET ResponsableCelular = Celular
            WHERE TipoSocio = 'Cadete' AND TRIM(IFNULL(ResponsableCelular, '')) = '' AND TRIM(IFNULL(Celular, '')) <> ''
            """);
    }

    private static long Escalar(SqliteConnection cn, SqliteTransaction tx, string sql, params (string, object)[] ps)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    private static void Ejecutar(SqliteConnection cn, SqliteTransaction tx, string sql, params (string, object)[] ps)
    {
        using var cmd = cn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
        cmd.ExecuteNonQuery();
    }
}

public static class ClavesConfiguracion
{
    public const string RutaCredenciales = "RutaCredenciales";
    public const string IdPlanillaTesoreria = "IdPlanillaTesoreria";
    public const string UltimaSincronizacion = "UltimaSincronizacion";
    public const string SincronizacionPendiente = "SincronizacionPendiente";
    public const string VersionEsquema = "VersionEsquema";
    /// <summary>Momento (ticks UTC) del último cambio que hay que subir a la planilla.</summary>
    public const string UltimoCambio = "UltimoCambio";
    /// <summary>Primer mes que se cobra con el sistema, "yyyy-MM" (por defecto, el mes en que se creó la base). Antes, todo es "—".</summary>
    public const string InicioCobros = "InicioCobros";
    /// <summary>Marca de que ya se revisó el arranque fijo en enero 2027 de las versiones viejas.</summary>
    public const string ArranqueRevisado = "ArranqueRevisado";
    /// <summary>Nombre del club o institución (encabezado y recibos).</summary>
    public const string NombreClub = "NombreClub";
    /// <summary>Nombre del taller o actividad, por ejemplo "Taller de Ajedrez".</summary>
    public const string NombreTaller = "NombreTaller";
    /// <summary>Nombre del profesor, opcional. Si está, sale en el recibo como quien recibió el pago.</summary>
    public const string NombreProfesor = "NombreProfesor";
    /// <summary>Carpeta de las copias de seguridad. Vacía = Documentos\CuotasClub\Copias.</summary>
    public const string CarpetaCopias = "CarpetaCopias";
    /// <summary>Fecha y hora local de la última copia de seguridad que salió bien.</summary>
    public const string UltimaCopia = "UltimaCopia";
    /// <summary>Motivo por el que falló la última copia (vacío si salió bien).</summary>
    public const string ErrorUltimaCopia = "ErrorUltimaCopia";
}
