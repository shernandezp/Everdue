# La lista, los filtros y las vistas guardadas

El tablero muestra lo que tienes delante. **Lista** muestra todo y te deja cortarlo como necesites,
incluido trabajo de hace meses.

También es donde aterriza cada número de los reportes: pulsa una cifra en un reporte y llegas aquí, ya
filtrado exactamente al trabajo que hay detrás.

## Filtrar

La tarjeta de filtros está sobre la tabla. Cada filtro estrecha la lista; varios filtros se aplican a
la vez.

| Filtro | Sirve para encontrar |
|---|---|
| **Buscar** | Palabras del título o la descripción |
| **Responsable** | El trabajo de una persona |
| **Entidad** | Todo lo relativo a un cliente, una máquina, un proveedor… |
| **Departamento** | Todo lo que ejecuta un equipo |
| **Estado** | Uno o varios estados a la vez: p. ej. *Incumplida* + *En espera* |
| **Motivo** | Trabajo aparcado por un motivo concreto |
| **Solo vencidas** | Todo lo que pasó su fecha y no está terminado |
| **Mostrar canceladas** | Las tareas canceladas, ocultas por defecto |

**Limpiar** quita todos los filtros de una vez.

> Tus filtros viven en la barra de direcciones, así que puedes copiar el enlace y mandárselo a un
> compañero. Verá la misma lista, dentro de lo que su cuenta tenga permitido ver.

## Vistas guardadas

Una combinación de filtros que preparas todos los lunes merece guardarse.

1. Pon los filtros que quieras.
2. Abre **Vistas guardadas → Guardar esta vista**.
3. Ponle un nombre reconocible: *«Acme, incumplidas y en espera»*.

Después, elígela en el mismo menú para aplicarla. Guardar con un nombre que ya existe lo reemplaza, y
la papelera borra una. Las vistas guardadas son **personales**: nadie más ve las tuyas.

## Seleccionar varios elementos

La casilla a la izquierda de cada fila la selecciona. En cuanto hay algo seleccionado aparece una barra
de acciones sobre la tabla:

| Acción | Qué hace |
|---|---|
| **Completar** | Completa todo lo seleccionado. Lo que ya cerró su período queda *completado tarde* |
| **Reasignar** | Cambia el responsable de todo lo seleccionado |
| **Reprogramar** | Mueve la fecha de vencimiento de todo lo seleccionado, con una nota opcional |
| **Limpiar** | Quita la selección |

Las acciones en bloque siguen exactamente las mismas reglas que las individuales, así que puede haber
rechazos: verás un resumen como *«28 actualizados, 2 rechazados»* con el primer motivo, en lugar de un
cambio parcial silencioso.

## Exportar a una hoja de cálculo

**Exportar CSV** descarga exactamente las filas de la pantalla, con los filtros que tengas puestos. El
archivo coincide con la pantalla por construcción: ejecuta la misma consulta.

- Se abre bien en Excel, tildes incluidas.
- Por encima de 50 000 filas Everdue se niega y te pide estrechar los filtros. Nunca te dará un archivo
  incompleto en silencio.

## Leer la tabla

| Columna | Notas |
|---|---|
| Título | Con la etiqueta de verificación si el elemento tiene pasos |
| Estado | Más las etiquetas *Vencida* y de espera cuando aplican |
| Entidad | Sobre qué trata el trabajo, o — |
| Responsable | De quién es ahora |
| Vence | En rojo cuando la fecha pasó y el trabajo no está terminado |

Pulsa cualquier fila para abrir el detalle del elemento.

## Un ejemplo resuelto

*«¿Qué trabajo de Acme incumplimos en los últimos meses, y de quién era?»*

1. **Entidad** → Acme.
2. **Estado** → Incumplida.
3. Lee la columna **Responsable**.
4. Guárdalo como *«Incumplidas de Acme»* si vas a preguntarlo otra vez el mes que viene.
5. **Exportar CSV** si alguien lo quiere en una hoja de cálculo.
