# Mi trabajo — el tablero

El tablero es la pantalla que más vas a usar. Muestra el trabajo como tarjetas en cinco columnas, y
mover una tarjeta es la forma de registrar qué pasó.

## Las cinco columnas

| Columna | Qué va ahí |
|---|---|
| **Por hacer** | Sin empezar |
| **En curso** | Alguien la tomó |
| **En espera** | Aparcada, esperando a alguien o a algo. El motivo es obligatorio |
| **Incumplidas** | El período terminó sin completarse. Aquí solo pone tarjetas Everdue |
| **Hechas** | Completadas en los últimos 7 días |

El número junto al título de cada columna es cuántas tarjetas hay.

## Leer una tarjeta

| En la tarjeta | Qué significa |
|---|---|
| Título | Qué hay que hacer |
| Línea gris debajo | La entidad sobre la que trata el trabajo, si la tiene |
| Etiqueta de color | El estado |
| Etiqueta roja **Vencida** | Pasó su fecha y no está terminada |
| Etiqueta naranja | Por qué está en espera |
| Etiqueta **3/7** | Avance de la lista de verificación: tres de siete pasos marcados |
| Fecha a la derecha | Cuándo vence; en rojo si esa fecha ya pasó |

Pulsa cualquier parte de la tarjeta para abrir el detalle completo. Ver
[Trabajar en un elemento](05-work-item).

## Mover el trabajo

**Arrastra la tarjeta** a otra columna, o usa el menú **⋮** de su esquina. El menú es más cómodo en el
móvil y hace exactamente lo mismo.

| Movimiento | Qué ocurre |
|---|---|
| Por hacer → En curso | La marca como que se está trabajando |
| Por hacer → Hechas | La completa. No hace falta pasar por *En curso* |
| En curso → Hechas | La completa |
| Cualquiera → En espera | Pide primero un motivo |
| En espera → Por hacer / En curso | Levanta la espera |
| Incumplidas → Hechas | La completa **tarde**. El incumplimiento permanece |
| Hechas → Por hacer | Deshace la finalización. Solo el responsable o un administrador |

Algunos movimientos se rechazan, y Everdue lo dice:

- **Nada puede arrastrarse a *Incumplidas*.** Solo Everdue registra un incumplimiento, cuando termina un
  período.
- **Una tarjeta incumplida no puede pasar a *En curso*.** Sigue visible como incumplida hasta que se
  complete tarde. Si no, desaparecería en silencio de los reportes mientras alguien trabaja en ella.
- **Una ocurrencia no se puede cancelar.** Cancelar es para tareas puntuales. Para detener una
  obligación recurrente, un administrador pausa o desactiva la responsabilidad.

## Poner algo en espera

Elige el motivo que sea cierto. La lista es corta a propósito, para que el reporte que sale de ella
merezca la pena leerse:

| Motivo | Úsalo cuando |
|---|---|
| Esperando cliente | Necesitas algo del cliente |
| Esperando proveedor | Necesitas algo de un proveedor |
| Esperando aprobación | Alguien dentro de la organización tiene que aprobar |
| Falta información | No tienes lo que necesitas para hacer el trabajo |
| Otro | Cualquier otra cosa; entonces la explicación escrita es **obligatoria** |

Dos cosas que conviene saber:

- **Una espera nunca evita un incumplimiento.** Si el período termina mientras el trabajo está en
  espera, queda igualmente incumplido. La espera explica el retraso; no excusa el período.
- Everdue mide cuánto duró cada espera. Eso convierte «siempre estamos esperando a ese proveedor» en un
  número sobre el que tu responsable puede actuar.

## Ver el trabajo de otra persona

Arriba del tablero hay un selector **Mostrando**:

- **Tu nombre** (lo predeterminado): tu trabajo.
- **Vacío**: el trabajo de todo el equipo.
- **Un compañero**: qué está haciendo y qué tiene en cola.

No es vigilancia: es cómo se cubre a quien está de baja. Cualquiera puede trabajar en el elemento de
cualquiera, y cada cambio queda registrado con un nombre al lado.

## Nueva tarea

**Nueva tarea** crea un trabajo puntual:

1. **Título**: qué hay que hacer. Sé concreto: *«Enviar a Acme la lista de precios actualizada»* es
   mejor que *«Acme»*.
2. **Responsable**: quién se encarga. Por defecto, tú.
3. **Entidad** y **Departamento**: opcionales, pero son lo que hace útiles los reportes después.
4. **Vence**: la tarea vence al final de ese día.

Para crear trabajo que se repite necesitas una **responsabilidad**: ver
[Responsabilidades](07-responsibilities).

## Si el tablero está vacío

O está todo terminado —posible en un buen viernes— o todavía no te han asignado trabajo. Vacía el
selector **Mostrando** para ver si el equipo tiene trabajo.
