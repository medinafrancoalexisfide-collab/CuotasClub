# Notas técnicas de CuotasClub

Detalle de las reglas y decisiones de diseño, versión por versión. La presentación del proyecto está en el [README](../README.md).

Programa de escritorio (C# .NET 10, Windows Forms, SQLite) para las cuotas de las clases de un taller en un club. Cada profesor carga sus propios datos la primera vez que lo abre, sin tocar el código.

## Instalar (sin Visual Studio)

Pasos para usarlo como un programa más: [docs/INSTALAR.md](INSTALAR.md). Resumen: descomprimir `Instalar-CuotasClub-1.0.zip` y doble clic en **Instalar CuotasClub**. Se instala en `%LOCALAPPDATA%\Programs\CuotasClub` (sin pedir administrador), con acceso directo en el menú Inicio y el escritorio, y aparece en Configuración de Windows → Aplicaciones para desinstalarlo.

- **Datos** (v1.0): `%LOCALAPPDATA%\CuotasClub\cuotasclub.db`, ya no junto al ejecutable. Si al arrancar no hay base ahí y sí hay una `cuotasclub.db` junto al .exe (versiones anteriores), se copia sola. Reinstalar o desinstalar no toca los datos.
- **Traer mis datos**: en el asistente de primer uso y en Configuración → Copias de seguridad → "Traer datos de otra PC…" se elige una `cuotasclub.db` o una copia y reemplaza la base actual (antes guarda una copia "antes-de-restaurar").
- **Armar el paquete**: `instalador/` tiene `Instalar CuotasClub.bat` + `instalar.ps1` / `desinstalar.ps1` (van dentro de `programa/` con la publicación self-contained win-x64). También `CuotasClub.iss` + `crear-instalador.bat` para generar un `Instalar-CuotasClub-1.0.0.exe` con Inno Setup 6 en Windows.

## Abrir y ejecutar (desarrollo)

1. Abrir `CuotasClub.slnx` en Visual Studio 2026 (o 2022 17.14+ con el SDK de .NET 10).
2. Proyecto de inicio: **CuotasClub**. F5.
3. La primera vez se crea `cuotasclub.db` en `%LOCALAPPDATA%\CuotasClub`, **vacía**, y aparece el asistente de **Configuración inicial**: nombre del club, del taller o actividad, del profesor (opcional, sale en el recibo), precio de la cuota, % para el club y mes desde el que se cobra. Hasta completarlo el programa no abre el menú (con "Salir" se cierra y lo vuelve a pedir la próxima vez). Todo se cambia después en **Configuración**. Para entregarle el programa a otro profesor, pasale el ZIP del instalador: arranca con su base vacía. Todas las pantallas abren en la fecha de hoy (alta, pagos, gastos, cuotas, resumen).

Pruebas: Explorador de pruebas de VS, o `dotnet test` desde la carpeta de la solución.

Conectar la planilla del tesorero (Google Sheets), paso a paso: [docs/CONECTAR-GOOGLE-SHEETS.md](CONECTAR-GOOGLE-SHEETS.md).

Guía para probar a mano Alumnos y Cuotas: [docs/COMO-PROBAR-ALUMNOS-Y-CUOTAS.md](COMO-PROBAR-ALUMNOS-Y-CUOTAS.md).

## Pantallas (v0.3)

Menú principal con seis módulos: Alumnos, Cuotas, Deudores, Gastos, Resumen y Configuración. El aspecto común (colores, tipografía Segoe UI, botones, grillas) está en `Views/Tema.cs`; todas las ventanas heredan de `VentanaBase`, que las agranda según la escala de Windows (125 %, 150 %) y las ajusta al tamaño de la pantalla. Colores de estado: Pagó verde, Pendiente amarillo, Debe rojo, "—" gris.

## Estructura

| Proyecto | Qué tiene |
|---|---|
| `src/CuotasClub.Core` (net10.0) | Models, Data (conexión y creación de tablas), Repositories (SQL con parámetros, sin ORM), Services (reglas y cálculos). No depende de Windows Forms, por eso se puede probar. |
| `src/CuotasClub` (net10.0-windows) | Windows Forms: `Program.cs`, `Views/` (pantallas), servicios que usan UI, PDF o Google. |
| `tests/CuotasClub.Tests` | Pruebas xUnit de las reglas y de la base (cada prueba usa un archivo SQLite temporal). |

## Reglas implementadas (decisiones importantes)

- **Estado de cada cuota** (`CalculoCuotas.CalcularEstado`):
  1. Con un pago no anulado → **Pagó** (verde).
  2. Mes anterior al inicio de cobros, anterior al alta, el mes de la baja o posterior → **—** (gris).
  3. Mes ya terminado sin pago → **Debe** (rojo).
  4. Mes en curso o futuro sin pago → **Pendiente**: hay todo el mes para pagar, recién el día 1 del mes siguiente pasa a Debe.
- **Mes de la baja**: no se cobra (queda "—" y no se puede registrar un pago). Si el profesor quiere cobrar el mes en que el alumno se fue, pone la fecha de baja el último día de ese mes: así el mes queda cobrable.
- **Alumnos que se ausentan y vuelven (v0.9)**: se le da de baja cuando deja de venir y, cuando vuelve, **Reactivar** pide la fecha de regreso. La baja no se borra: queda guardada como ausencia (tabla `Ausencias`, de la baja al regreso). Los meses del medio quedan en "—": no son deuda, no se cobran y no cuentan en deudores ni en el resumen. Se cobra desde el mes de regreso (aunque vuelva el día 20). El mes en que se fue sigue la regla de la baja. Se puede ir y volver varias veces. Si la baja fue un error, en la misma ventana se marca "La baja fue un error" y se borra sin dejar hueco. Las bases anteriores suman la tabla vacía al abrirse, sin perder nada (los que ya se habían reactivado antes no tienen hueco guardado: si hace falta, se les vuelve a cargar baja y regreso).
- **Tutor solo para cadetes (v0.10)**: en la ficha del alumno, si el tipo de socio es **Cadete** aparecen *Tutor* y *Teléfono del tutor*; si es **Activo** (adulto) aparece solo *Teléfono*. Los campos cambian al elegir el tipo. La lista de alumnos, deudores y el recibo usan el teléfono que corresponde (el recibo dice "Recibí de" el tutor en los cadetes). *Observaciones* queda para notas libres (horario, nivel, alergias, lo que sea). Bases anteriores: al abrirse, si un alumno tenía cargado solo el otro teléfono, se copia al que ahora se ve; no se borra nada.
- **Base nueva vacía (v0.8)**: no se carga ningún precio ni % por defecto. `ConfiguracionService.FaltaConfiguracionInicial` (no hay precio o no hay %) dispara `PrimerUsoForm`, que guarda todo con vigencia desde el inicio de cobros. Las bases que ya existían no cambian: conservan precio, %, pagos y alumnos, y la primera vez que se abren con la 0.8 se les guarda "Cuotas Club" / "Taller de Ajedrez" como nombres (los que antes estaban fijos en el código), editables en Configuración → Datos del club.
- **Inicio de cobros** (`Configuracion.InicioCobros`, por defecto el mes en que se creó la base; se cambia en Configuración → Inicio de cobros): podés cargar la fecha de alta real de un alumno (aunque sea de 2024) sin que aparezcan deudas anteriores al uso del programa.
- **Histórico**: el precio y el % del club tienen vigencia desde un mes (`PreciosCuota`, `PorcentajesClub`). Al registrar un pago se copian al pago el precio y el % **vigentes para el mes que se paga**. Un cambio posterior nunca altera los meses ya cobrados; un pago atrasado de febrero cobrado en abril usa los valores de febrero.
- **Cadete** paga lo mismo que Activo (un único precio).
- **Montos** se guardan en centavos (INTEGER) para evitar errores de redondeo; los modelos usan `decimal` en pesos. Se muestran como `$ 20.000` (`Formato.Moneda`).
- **Un pago no anulado por alumno y mes**: lo garantiza un índice único parcial en SQLite. Anular no borra: el pago queda en el historial con `Anulado = 1`.
- **Categoría**: edad al 31/12 del año (`año − año de nacimiento`); Sub-N si la edad es menor que N, 18 o más → Mayor.
- **Promedio anual**: sobre los meses ya iniciados del año que tuvieron actividad (alumnos activos, pagos o gastos). Meses sin clases y futuros no bajan el promedio.
- **Orden y búsqueda de alumnos**: en español, sin distinguir mayúsculas ni acentos ("alvarez" encuentra a "Álvarez").
- Registrar o anular un pago marca `SincronizacionPendiente = 1` para la planilla del tesorero.

## Copias de seguridad (v0.11)

- **Al cerrar el programa** se guarda una copia completa de la base en la carpeta elegida en **Configuración → Copias de seguridad** (por defecto `Documentos\CuotasClub\Copias`). Nombre: `cuotasclub-AAAA-MM-DD_HH-MM-SS.db`. Se hace con `VACUUM INTO`, que da un archivo consistente aunque la base esté en uso.
- Se conservan las **últimas 30**; las más viejas se borran solas. Solo se tocan los archivos `cuotasclub-*.db` de la carpeta.
- **En la nube con Google Drive**: instalar *Google Drive para escritorio* (https://www.google.com/drive/download/), iniciar sesión con la cuenta de Google personal y elegir como carpeta de copias una dentro de "Mi unidad" (por ejemplo `G:\Mi unidad\CuotasClub-Copias`). Drive sube cada copia solo. No se usa la cuenta de servicio de la planilla (no tiene espacio en Drive).
- Si una copia falla (por ejemplo, Drive cerrado o la carpeta no existe), el programa cierra igual sin molestar y el motivo aparece en rojo en Configuración.
- **Hacer copia ahora**: guarda una copia en el momento.
- **Restaurar copia…**: se elige el archivo, se revisa que sea una base de CuotasClub sana, se pide confirmación, se guarda antes una copia de la base actual (termina en `-antes-de-restaurar`), se reemplaza y el programa se reinicia. Si la copia es de una versión anterior, al abrirse suma lo que le falte. La carpeta de copias elegida se mantiene.
- Código: `Core/Services/CopiaSeguridadService.cs` (probado en `CopiaSeguridadTests`), copia de cierre en `Program.cs`, tarjeta en `Views/ConfiguracionForm.cs`.

## Carpetas sin ventanas repetidas (v0.12)

Al generar un recibo o tocar "Abrir carpeta" en las copias, si esa carpeta ya está abierta en el Explorador se trae esa ventana al frente (con el recibo seleccionado) en vez de abrir otra. La lógica está en `src/CuotasClub/Services/Explorador.cs` (`MostrarArchivo` y `AbrirCarpeta`), para reusarla en otros botones.

## WhatsApp (v0.13)

- Después de generar un recibo aparece un diálogo con **Enviar por WhatsApp**, **Mostrar en carpeta**, **Abrir PDF** y **Cerrar**. "Enviar por WhatsApp" abre el chat (wa.me) con el mensaje escrito ("¡Hola Ana! Te paso el recibo de la cuota de noviembre 2026 de Juan. ¡Gracias!") y la carpeta con el PDF seleccionado para arrastrarlo al chat.
- En **Deudores**, "Recordar por WhatsApp" abre el chat del alumno elegido con un recordatorio amable de los meses que debe y el total.
- Teléfono: Cadete → tutor, Activo → alumno. Se convierte a 549 + área + número (acepta 0, 15, +54, espacios y guiones). Si no hay teléfono o le falta el código de área, avisa.
- Lógica en `CuotasClub.Core/Services/WhatsApp.cs` (con pruebas en `WhatsAppTests.cs`).

## Planilla del tesorero (Google Sheets)

- `SincronizacionService` (Core, sin dependencia de Google) arma una pestaña por año, desde el inicio de cobros hasta el año actual: Mes, Alumnos que pagaron, Total ingresado, Corresponde al club (%), fila Total y "Última actualización". **Solo ingresos y parte del club**: gastos, la parte del profesor y la ganancia nunca salen de SQLite. Los meses futuros quedan vacíos.
- `GoogleSheetsDestino` (proyecto Windows) usa `Google.Apis.Sheets.v4` con cuenta de servicio (`CredentialFactory.FromFile<ServiceAccountCredential>`): crea la pestaña si no existe, la limpia (`Values.Clear`), escribe desde A1 y da formato de moneda, negrita en encabezado y total.
- `Sincronizador` corre en segundo plano, una sincronización a la vez: después de registrar o anular un pago, al abrir el programa si quedó algo pendiente, y con "Sincronizar ahora" (pantalla principal y Configuración).
- Sin internet o sin configurar no falla: queda `SincronizacionPendiente = 1` y se reintenta. Si se registra un pago mientras se sube, también queda pendiente (clave `UltimoCambio`).
- Credenciales: la ruta elegida en Configuración, o `credenciales-google.json` en `Documentos\\CuotasClub` (si no está ahí, se busca junto al ejecutable como antes). El ID de la planilla acepta el enlace completo.

## Base de datos

Tablas: `Alumnos`, `Pagos`, `Gastos`, `PreciosCuota`, `PorcentajesClub`, `Configuracion`. Se crean solas al iniciar (`Database.Inicializar`). Fechas como texto `yyyy-MM-dd`.
