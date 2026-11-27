# Instalar CuotasClub en la PC

No hace falta Visual Studio ni nada más. Son 3 pasos.

## 1. Instalar

1. Descargá `Instalar-CuotasClub-1.0.zip`.
2. Clic derecho sobre el ZIP → **Extraer todo…** → **Extraer**. (No lo abras con doble clic: hay que descomprimirlo.)
3. En la carpeta que se abre, doble clic en **Instalar CuotasClub**.
   - Si Windows muestra "Windows protegió su PC", tocá **Más información** → **Ejecutar de todas formas**. Pasa porque el programa no está "firmado" por una empresa; es normal.
4. Aparece una ventana negra unos segundos y se abre CuotasClub. Listo: ya tenés el ícono en el **escritorio** y en el **menú Inicio** (escribí "Cuotas").

Después podés borrar el ZIP y la carpeta descomprimida.

## 2. Traer tus datos (solo si ya venías usando el programa)

La primera vez se abre la ventana **Configuración inicial**. Si ya tenías alumnos y pagos cargados:

1. Tocá **Traer mis datos…** (no hace falta completar lo de arriba).
2. Buscá tu archivo `cuotasclub.db`. Si lo usabas desde Visual Studio, está en la carpeta del proyecto:
   `CuotasClub\src\CuotasClub\bin\Debug\net10.0-windows\cuotasclub.db`
   También sirve cualquier copia de seguridad de `Documentos\CuotasClub\Copias` (la más nueva es la de arriba).
3. Confirmá. El programa queda con todos tus alumnos, pagos y configuración.

Si te salteaste este paso, lo mismo está en **Configuración → Copias de seguridad → Traer datos de otra PC…**.

## 3. Pasárselo a otro profesor

Mandale el mismo `Instalar-CuotasClub-1.0.zip` (por Drive, pendrive, etc.) y estas instrucciones. Cuando lo abra, arranca vacío y le pide los datos de su club. Tus datos no viajan en el ZIP.

## Dónde quedan las cosas

| Qué | Dónde |
|---|---|
| El programa | `%LOCALAPPDATA%\Programs\CuotasClub` |
| Tus datos (alumnos, pagos, configuración) | `%LOCALAPPDATA%\CuotasClub\cuotasclub.db` (Configuración → **Abrir carpeta de datos**) |
| Copias de seguridad y recibos | `Documentos\CuotasClub` |

## Actualizar a una versión nueva

Cerrá CuotasClub y hacé lo mismo del paso 1 con el ZIP nuevo. Tus datos no se tocan.

## Desinstalar

Configuración de Windows → **Aplicaciones** → buscá **CuotasClub** → **Desinstalar**. Tus datos y copias **no** se borran: si lo instalás de nuevo, aparecen solos.

---

### Para quien sepa de programación: armar un instalador .exe

En `instalador/` está `CuotasClub.iss` (Inno Setup 6). En una PC con el SDK de .NET 10 e [Inno Setup 6](https://jrsoftware.org/isdl.php), doble clic en `instalador\crear-instalador.bat`: publica el programa y deja `salida\Instalar-CuotasClub-1.0.0.exe`. Instala en el mismo lugar que el ZIP, sin pedir administrador.
