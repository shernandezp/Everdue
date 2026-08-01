# Trabajar en un elemento

Al pulsar cualquier tarjeta o fila se abre el panel de detalle. Todo lo que puedes hacerle a un trabajo
está aquí.

## La parte de arriba

El título, las etiquetas de estado, y si esto es una **ocurrencia** (parte de algo recurrente) o una
**tarea puntual**. Debajo, una tarjeta con los datos: responsable, entidad, departamento, vencimiento,
el período al que pertenece una ocurrencia, y quién la completó, si alguien lo hizo.

## Los botones de acción

Solo aparecen las acciones que ahora mismo son legales, así que nunca llegas a un callejón sin salida.

| Botón | Qué hace |
|---|---|
| **Completar** | Termina el trabajo. Cuando el período ya cerró, dice *Completar (tarde)* |
| **Empezar** | Lo marca como en curso. Es opcional: puedes completar directamente desde *Por hacer* |
| **Poner en espera** | Lo aparca. El motivo es obligatorio |
| **Reabrir** | Levanta una espera, o deshace una finalización (responsable o administrador) |
| **Reprogramar** | Mueve la fecha de vencimiento |
| **Editar** | Cambia título, descripción, responsable, entidad o departamento |
| **Cancelar tarea** | Solo tareas puntuales |

Si **Completar** está en gris, pasa el ratón por encima: el aviso dice exactamente qué falta — pasos
obligatorios sin marcar, o la foto que exige la responsabilidad.

## Lista de verificación

Algunos trabajos llevan pasos. Una responsabilidad puede definir una **plantilla**, y cada ocurrencia
recibe su propia copia al crearse, de modo que mejorar la plantilla nunca reescribe cómo eran las
ocurrencias pasadas.

- Marca un paso cuando lo hayas hecho. Everdue registra **quién** lo marcó y **cuándo**; pasa el ratón
  por la línea para verlo.
- **Añadir un paso** pone una línea extra *solo en este elemento*. Las líneas extra nunca son
  obligatorias.
- Una línea marcada como **Obligatorio** debe estar marcada antes de poder completar, pero solo cuando
  la responsabilidad exige la verificación.
- Puedes borrar un paso que añadiste tú; no puedes borrar uno que viene de la plantilla, porque forma
  parte de lo que se pidió para esa ocurrencia.

## Adjuntos y prueba

**Adjuntar archivo** sube un documento o una foto. **Tomar una foto** abre directamente la cámara en el
móvil: dos toques, sin instalar nada.

**Tomar una foto** solo aparece en el móvil o la tableta. Un ordenador no tiene ninguna cámara que el
navegador pueda abrir así, de modo que allí sería un segundo botón haciendo lo que ya hace **Adjuntar
archivo**: sube la foto desde el ordenador.

Algunas responsabilidades **exigen** una foto o archivo antes de poder completar el trabajo. Cuando es
así, el panel lo dice antes de que lo intentes, no después.

Quien subió un archivo puede borrarlo; un administrador también.

## Comentarios y menciones

Los comentarios son la historia del trabajo: qué encontraste, qué dijo el cliente, por qué hubo que ir
dos veces.

1. Escribe en el cuadro de abajo.
2. Para meter a alguien, usa **Mencionar** y elígelo de la lista.
3. Pulsa **Agregar comentario**.

Quien sea mencionado recibe un aviso con un enlace directo a este elemento. Los comentarios no se
pueden editar —son un registro, no un documento— pero puedes borrar los tuyos, y un administrador
cualquiera.

## Reprogramar

**Reprogramar** mueve la fecha de vencimiento y pide una nota opcional que explique por qué. La nota
queda en el historial.

Una regla: **una ocurrencia solo puede moverse dentro de su propio período.** La inspección de marzo
puede pasar del día 1 al 6 de marzo; no puede pasar a abril, porque abril ya tiene su propia ocurrencia
esperando. Una tarea puntual puede moverse a donde sea.

## Historial

La línea de tiempo del final es el registro completo del elemento:

| Entrada | Significa |
|---|---|
| **Creada** | Nació: a mano o desde una responsabilidad |
| **Cambio de estado** | De un estado a otro, con los dos nombres |
| **Editada** | Cambió un campo, y cuáles |
| **Reasignada** | Cambió el responsable |
| **Reprogramada** | Con la fecha anterior y la nueva |
| **Comentario** | Alguien escribió algo |

Cada entrada nombra a la persona, o dice **por el motor de ocurrencias** cuando lo hizo el propio
Everdue: así se ve un incumplimiento registrado.

Marcar pasos de la lista de verificación no aparece aquí a propósito: quince marcas enterrarían el
historial para el que existe la línea de tiempo. Los pasos guardan por sí mismos quién los marcó.

## Por qué cualquiera puede editar el trabajo de cualquiera

En un equipo pequeño importa más cubrirse que el territorio: si alguien está de baja, un compañero
tiene que poder terminar su trabajo. Por eso Everdue deja que cualquiera edite o complete el elemento
de cualquiera, y a cambio hace que **todo cambio sea trazable**. Dos excepciones, porque ambas borran
un registro en vez de añadirlo: deshacer una finalización y cancelar una tarea quedan reservadas al
responsable y a los administradores.
