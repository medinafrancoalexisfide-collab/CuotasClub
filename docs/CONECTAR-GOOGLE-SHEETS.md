# Conectar la planilla del tesorero (Google Sheets)

El programa funciona igual sin esto: todo se guarda en la computadora y la planilla queda "pendiente de sincronizar" hasta que esté configurada.

**Qué se publica:** por cada mes, cuántos alumnos pagaron, el total ingresado y lo que corresponde al club. **Nunca** se publican gastos, tu parte ni tu ganancia.

Primero creá una planilla vacía en Google Sheets (por ejemplo **"Cuotas – Tesorería"**, se crea rápido entrando a https://sheets.new) y guardá su enlace: lo vas a usar en los pasos 7 y 8.

## Lo que tenés que hacer vos (una sola vez, unos 10 minutos)

Las credenciales tienen que ser tuyas, por eso estos pasos los hacés vos con tu cuenta de Google.

1. Entrá a https://console.cloud.google.com con tu cuenta de Google. Arriba, en el selector de proyectos, **Proyecto nuevo** → nombre `CuotasClub` → **Crear**. Asegurate de que quede seleccionado.
2. Menú ☰ → **APIs y servicios → Biblioteca** → buscá **Google Sheets API** → **Habilitar**.
3. Menú ☰ → **IAM y administración → Cuentas de servicio** → **Crear cuenta de servicio**. Nombre: `cuotasclub-sync` → **Crear y continuar** → **Listo** (no hace falta asignarle roles).
4. Entrá a la cuenta que creaste → pestaña **Claves** → **Agregar clave → Crear clave nueva → JSON → Crear**. Se descarga un archivo.
5. Renombralo a `credenciales-google.json` y guardalo en una carpeta tuya, por ejemplo `Documentos\CuotasClub`.
6. Copiá el **email** de la cuenta de servicio (termina en `.iam.gserviceaccount.com`).
7. Abrí tu planilla → **Compartir** → pegá ese email → permiso **Editor** → desmarcá "Notificar" → **Enviar**.
8. Abrí CuotasClub → **Configuración**:
   - **Archivo de credenciales** → **Examinar…** → elegí `credenciales-google.json`. Abajo aparece el email de la cuenta de servicio (y queda copiado).
   - **Planilla** → pegá el enlace de la planilla (sirve el enlace completo).
   - **Guardar y sincronizar ahora**. Tiene que decir "Planilla del tesorero actualizada." y en la planilla aparece la pestaña del año actual (por ejemplo **2026**).
9. Compartí la planilla con el email del tesorero con permiso **Lector**.

## Cómo funciona después

- Cada vez que registrás o anulás un pago, la planilla se actualiza sola en segundo plano (no congela la pantalla).
- En la pantalla principal, abajo, ves "Última sincronización: fecha y hora" y un enlace **Sincronizar ahora**.
- Sin internet: no pasa nada, queda "pendiente de sincronizar" y se sube sola la próxima vez que abras el programa o registres un pago.
- La planilla se sobrescribe completa cada vez: si alguien la edita a mano, se pierde. La fuente de verdad es el programa.

## Seguridad

`credenciales-google.json` es como una contraseña: no lo subas a GitHub (ya está en el `.gitignore`) ni lo mandes por WhatsApp. Si se filtra, en Google Cloud → cuenta de servicio → Claves, borrá esa clave y creá otra.

## Si algo falla

| Mensaje | Qué hacer |
|---|---|
| "La planilla no está compartida con …" | Paso 7: compartila con ese email como Editor. |
| "La Google Sheets API no está habilitada…" | Paso 2, y esperá unos minutos. |
| "No existe una planilla con ese ID" | Volvé a pegar el enlace de la planilla en Configuración. |
| "El archivo de credenciales no es válido" | Tiene que ser la clave JSON de la cuenta de servicio (paso 4), no otro archivo. |
| "No hay conexión con Google (¿sin internet?)" | Nada: se reintenta sola. |
