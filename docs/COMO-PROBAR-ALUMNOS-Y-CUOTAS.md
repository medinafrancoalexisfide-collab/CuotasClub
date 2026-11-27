# Cómo probar Alumnos, Cuotas y recibos en Windows

## Abrir y ejecutar

1. Copiá la carpeta `CuotasClub` a tu PC y abrí `CuotasClub.slnx` con Visual Studio 2026 (necesita el SDK de .NET 10).
2. Elegí `CuotasClub` (el proyecto WinForms) como proyecto de inicio y apretá F5. La primera vez descarga los paquetes NuGet. Con una base nueva aparece la **Configuración inicial**: cargá el club, el taller, el precio (por ejemplo 20000), el % del club y desde qué mes se cobra.
3. La base `cuotasclub.db` se crea sola en `%LOCALAPPDATA%\CuotasClub` (pegá esa ruta en la barra del Explorador). Para empezar de cero, cerrá el programa y borrá ese archivo.

## Prueba 1: Alumnos

En la pantalla principal → **Alumnos**.

- **Ctrl+N** (o el botón Nuevo): cargá un alumno usando solo el teclado. Tab sigue el orden de los campos y **Enter** guarda. Las fechas se escriben como `15032016`, sin barras.
- Al escribir la fecha de nacimiento aparecen la edad y la categoría (por ejemplo, nacido en 2016: 10 años · Sub-12 en 2026).
- Probá **Guardar y cargar otro**: se reabre el formulario vacío.
- Validaciones: DNI con 6 dígitos, DNI repetido y fecha de nacimiento futura tienen que mostrar un mensaje claro y no guardar.
- Buscá por apellido sin acento (`perez` encuentra a "Pérez") o por DNI. La lista siempre va ordenada por apellido.
- **Dar de baja** con fecha: el alumno desaparece de la lista. Marcá "Mostrar también los dados de baja" y aparece en gris. **Reactivar** pide la fecha en que vuelve: los meses que estuvo afuera quedan en "—" y se cobra desde el mes de regreso. En la columna Baja se ve la ausencia ("15/02/2027 → 03/05/2027"). Si la baja fue un error, tildá "La baja fue un error" y se borra sin hueco.
- Doble clic o Enter sobre un alumno abre la edición.

## Prueba 2: Grilla de cuotas

Pantalla principal → **Cuotas**. Abre en el año actual, con el mes de hoy seleccionado.

- El mes en curso aparece en amarillo (pendiente): hay todo el mes para pagar. Recién cuando termina, si no se pagó, pasa a rojo.
- Los meses anteriores al inicio de cobros aparecen "—". El inicio es el mes en que se creó la base y se cambia en **Configuración → Inicio de cobros** (por ejemplo, poné enero 2027 cuando empieces a usarlo en serio).
- Doble clic (o Enter) en una celda: se abre "Registrar pago" con el monto vigente (el precio que cargaste), la fecha y el medio de pago. Con "Generar recibo en PDF" tildado, al registrar se crea el recibo en `Documentos\CuotasClub\Recibos` y el programa ofrece abrir la carpeta o el PDF.
- La celda queda verde con ✓. Si pasás el mouse por encima, muestra la fecha, el monto y el medio de pago.
- Doble clic en una celda verde: podés **volver a generar el recibo** o **anular el pago**, con confirmación. El mes vuelve a quedar sin pagar.
- Clic derecho sobre una celda: el mismo menú (registrar, recibo, anular).
- Abajo se ve el resumen del mes seleccionado: cuántos pagaron, cuántos deben y cuánto ingresó.
- Si das de baja a un alumno, por ejemplo el 10 de marzo, desde marzo aparece "—" en gris. Doble clic en marzo pregunta si querés cobrar ese mes igual. Desde abril no deja.

### Ver los colores rojo (debe)

Cargá un alumno con alta varios meses atrás y, en **Configuración → Inicio de cobros**, elegí un mes anterior (por ejemplo enero 2026). Los meses terminados sin pago salen en rojo y el mes actual en amarillo. Cuando termines de probar, borrá `cuotasclub.db` para arrancar limpio.

## Atajos

| Pantalla | Tecla | Acción |
|---|---|---|
| Alumnos | Ctrl+N / Insert | Nuevo alumno |
| Alumnos | Enter / doble clic | Editar |
| Ambas | Ctrl+F | Ir al buscador |
| Cuotas | Enter / doble clic | Registrar pago o ver el pago |
| Cuotas | F5 | Actualizar |
| Ambas | Esc | Cerrar |
