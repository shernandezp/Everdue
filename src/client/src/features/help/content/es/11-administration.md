# Administración

*Solo administradores.* Todo lo de esta sección es configuración: se hace una vez y casi no se toca.

## Personas y roles

**Administración → Usuarios.**

| Rol | Puede |
|---|---|
| **Miembro** | Trabajar en el tablero y la lista, leer entidades, comentar, adjuntar y marcar pasos, incluso en el trabajo de compañeros |
| **Administrador** | Todo eso, más responsabilidades, reportes, análisis y toda esta sección |

**No hay registro autogestionado.** Tú creas la cuenta; a la persona se le pide elegir una contraseña
nueva la primera vez que entra.

| Acción | Notas |
|---|---|
| **Nuevo usuario** | Correo, una primera contraseña, nombre visible, rol e idioma |
| **Editar** | Nombre, rol, idioma, número de WhatsApp y si está activo |
| **Restablecer contraseña** | Pone una nueva y obliga a cambiarla al entrar |
| **Reasignar** | La salida de alguien; ver abajo |

**Desactiva en lugar de borrar.** Desactivar retira el acceso en un par de minutos y conserva todos los
registros. Everdue nunca borra a una persona que hizo algo, porque el historial la nombra.

### Cuando alguien se va

Usa **Reasignar** en su fila. Elige quién toma el relevo y si se mueven sus responsabilidades, su
trabajo pendiente, o ambos. Es una sola acción en vez de una lista de elementos, porque esta es una
pantalla que se abre con prisa.

El historial se queda con el responsable anterior: «quién hizo qué» nunca se reescribe.

## Configuración de la organización

**Administración → Configuración.**

| Ajuste | Notas |
|---|---|
| **Nombre** | Se muestra en la cabecera |
| **Zona horaria** | Un nombre IANA como `America/Bogota`. **Todos los vencimientos y períodos se calculan en esa zona**: configúrala antes de que nadie cree responsabilidades |
| **Hora del resumen** | Cuándo sale el resumen para responsables, hora local |
| **Hora del recordatorio** | Cuándo salen los avisos de «vence hoy». Más tarde que el resumen por defecto: los responsables leen antes de que empiece el día, y quien ejecuta lo quiere cuando ya ha empezado |
| **Idioma por defecto** | Para quien no haya elegido el suyo |
| **Puede usar las credenciales del sistema** | Si esta organización puede apoyarse en la configuración de correo de la instalación |

## Canales de aviso

**Administración → Canales de aviso.** Una tarjeta por canal, cada una con su configuración, su botón
**Enviarme una prueba** y su estado de salud.

| Canal | Necesita | Notas |
|---|---|---|
| **Correo** | Servidor SMTP, dirección remitente, credenciales | El más sencillo de configurar |
| **Telegram** | Un token de bot de @BotFather | Recomendado para quien está en campo: gratis y sin abrir nada en tu red |
| **WhatsApp** | Una cuenta de WhatsApp Business y plantillas aprobadas por Meta | Los mensajes se facturan. «Enviado» significa que Meta lo aceptó: no hay confirmación de lectura |

Los secretos se guardan cifrados y no se vuelven a mostrar. Dejar un secreto en blanco al editar
conserva el que ya está guardado, así puedes cambiar el nombre de un bot sin volver a escribir su
token.

La tabla de **salud** de abajo muestra pendientes, fallidos en las últimas 24 horas y omitidos por
canal. Es el primer sitio donde mirar cuando alguien dice que no le llegó nada.

## Campos personalizados

**Administración → Configuración → pestaña Campos personalizados.** Hasta diez campos extra por tipo de
entidad: texto, número, fecha o una lista para elegir.

Son **solo para mostrar**: nada filtra, ordena, reporta ni envía webhooks con ellos. Borrar una
definición deja los valores ignorados en vez de borrar nada.

## Importar desde una hoja de cálculo

**Administración → Importar.** Tres pasos, y no se escribe nada hasta el último.

1. **Archivo.** Elige *Entidades* o *Tareas puntuales* y luego el CSV. Separado por comas o punto y
   coma: lo que exporta Excel en español funciona.
2. **Columnas.** Everdue propone una correspondencia y muestra filas reales de tu archivo para que la
   compruebes.
3. **Resultado.** Cuántas se crearon, cuántas se omitieron y cada fallo con su número de fila. La lista
   de fallos se puede descargar en CSV.

Dos garantías: **una importación nunca sobrescribe** —una fila que ya existe se omite y se informa— y
las ocurrencias no se pueden importar, porque las crea Everdue a partir de las responsabilidades.

## Claves de API

**Administración → Configuración → pestaña Claves de API.** Para un script o una plataforma de automatización
que necesite leer o escribir trabajo.

- El token se muestra **una sola vez**. Guárdalo en un sitio seguro; Everdue solo conserva una huella.
- **Solo lectura** o **lectura y escritura**.
- Una clave actúa como una persona, así que lo que escriba queda atribuido a alguien real.
- Una clave solo puede llegar a los endpoints de trabajo. Ni siquiera una clave creada por un
  administrador **puede** crear usuarios ni leer el secreto de un canal.
- **Revocar** surte efecto de inmediato.

## Webhooks

**Administración → Configuración → pestaña Webhooks.** Everdue llama a tus sistemas cuando pasa algo.

Seis eventos: elemento creado, completado, incumplido, puesto en espera, reasignado, y entidad creada.

- El secreto de firma se muestra **una sola vez** al crear la suscripción. Tu receptor lo usa para
  verificar que cada llamada viene de verdad de Everdue.
- **Enviar una prueba** encola un `ping` para comprobar el receptor antes de depender de él.
- Las entregas se reintentan con pausas crecientes. Tras diez fallos seguidos la suscripción se
  **desactiva automáticamente** y un aviso lo dice; arregla tu endpoint y vuelve a activarla.
- Everdue solo llama **hacia fuera**. No hay que abrir nada en tu red.

Detalle técnico para quien construya el receptor: `docs/api.md` en el repositorio.

## Modo demostración

**Administración → Configuración**, al final de la página.

Everdue es difícil de valorar vacío. El registro, la franja de cumplimiento y la tabla de salud no se
ven hasta que hay historial detrás, así que el modo demostración llena el espacio de trabajo con **seis
meses de historial inventado** —una docena de responsabilidades, clientes y máquinas creíbles, listas de
verificación, esperas, cumplimientos e incumplimientos— y de golpe todas las pantallas de informes y
análisis tienen algo que mostrar.

> **Borra todo, en los dos sentidos.**
>
> Activarlo borra el espacio de trabajo y escribe encima los datos de demostración. Desactivarlo lo
> borra otra vez y lo deja vacío, listo para uso real.
>
> En ambos casos pierdes todas las tareas, ocurrencias, responsabilidades, entidades, departamentos,
> adjuntos y cuentas de usuario **excepto la tuya**. Habrá que volver a dar de alta a tus compañeros.
> **No se puede deshacer**: la única vuelta atrás es una copia de seguridad del directorio de datos.

Por eso Everdue pide dos cosas antes de hacerlo: el **nombre del espacio de trabajo escrito
exactamente** y **tu propia contraseña**. Solo los administradores ven la tarjeta, y ninguna clave de
API puede llegar a ella.

Mientras el modo demostración está activo, todo el mundo ve una etiqueta **Datos de demostración** junto
al nombre del espacio en la cabecera. El historial inventado se ve igual que el real —esa es la idea—,
así que la etiqueta es lo único que le dice a tu equipo que no registre trabajo real aquí.

**¿Estás probando Everdue?** Activa el modo demostración, recorre los informes y análisis todo lo que
quieras, luego desactívalo y empieza de verdad. **¿Ya usas Everdue?** No lo actives. Borrará tu trabajo.

Quien gestione tu instalación puede eliminar esto por completo (`Demo:AllowReset`); en ese caso la
tarjeta no aparece.

## Un orden sensato para configurar

1. **Configuración**: primero la zona horaria, luego idioma y horas.
2. **Departamentos**: los equipos que hacen el trabajo.
3. **Entidades**: clientes, proveedores, máquinas. Impórtalas si tienes una lista.
4. **Usuarios**: las personas, con el rol correcto.
5. **Responsabilidades**: el trabajo recurrente, **empezando hoy** salvo que quieras historial a
   propósito.
6. **Canales**: correo o Telegram, y envíate una prueba.
7. Deja pasar una semana y abre **Análisis**. Habrá algo dentro.
