# Cómo piensa Everdue

Seis ideas. Cuando estas encajan, todas las pantallas del producto encajan.

## 1. Una responsabilidad nunca termina

Una **responsabilidad** es una obligación que vuelve: *«llamar a Acme todos los lunes»*, *«inspeccionar
la línea 2 el día 1 de cada mes»*, *«cuadrar la caja cada día»*.

Nunca completas una responsabilidad. Simplemente sigue produciendo trabajo.

## 2. Cada período produce una ocurrencia

Cada vez que la responsabilidad vence, Everdue crea una **ocurrencia**: un trabajo real, con fecha de
vencimiento, en el tablero de alguien.

*«Llamar a Acme todos los lunes»* produce una ocurrencia cada lunes. La llamada de esta semana es un
trabajo distinto al de la semana pasada, y cada una se juzga por sí sola.

Everdue crea las ocurrencias automáticamente. Nadie tiene que acordarse, y nadie puede olvidarse.

## 3. Un período que termina sin completarse queda **incumplido**, y el siguiente llega igual

Este es el corazón del producto.

Si la llamada del lunes no ocurrió antes de que empiece el lunes siguiente, la ocurrencia del lunes
queda registrada como **incumplida**. Y queda así para siempre. El lunes nuevo aparece de todas formas,
así que el trabajo no se acumula en un único elemento imposible.

Todavía puedes completar una ocurrencia incumplida: queda como **completada tarde**. El registro dice
entonces las dos cosas: se incumplió, y finalmente se hizo. Ninguna borra a la otra.

> La mayoría de las herramientas hace lo contrario: mueven la fecha hacia adelante y el incumplimiento
> desaparece. Por eso nunca pueden contarte qué pasó de verdad el trimestre pasado.

## 4. Una tarea puntual es otra cosa

Una **tarea puntual** es trabajo que ocurre una sola vez: *«enviar la nueva lista de precios a Acme»*.

| | Ocurrencia | Tarea puntual |
|---|---|---|
| Viene de | una responsabilidad | alguien que la crea |
| Se repite | sí, cada período | no |
| Puede incumplirse | **sí** | no: no tiene período que termine |
| Puede cancelarse | no (pausa la responsabilidad) | sí |
| Cuenta en los reportes de cumplimiento | sí | no |

Cualquiera puede crear una tarea puntual. Solo un administrador puede crear una responsabilidad.

## 5. El trabajo trata *sobre* algo y lo ejecuta *alguien*

Dos campos distintos que el primer día se confunden:

- **Entidad**: sobre qué trata el trabajo — un cliente, un proveedor, una máquina, un departamento, una
  empresa. Es una etiqueta, no un expediente: Everdue guarda el *nombre*, no sus contratos ni sus
  facturas.
- **Departamento**: qué equipo *ejecuta* el trabajo.

Ambos son opcionales. Rellenarlos es lo que permite que los reportes respondan «¿cómo vamos con Acme?»
y «¿cuán cargado está Operaciones?».

## 6. El estado es una lista corta y cerrada

| Estado | Significa |
|---|---|
| **Por hacer / Abierta** | Nadie la ha empezado |
| **En curso** | Alguien la tomó. Es solo una señal de coordinación: no cambia ningún reporte ni protege de un incumplimiento |
| **En espera** | Aparcada, con un motivo obligatorio |
| **Incumplida** | El período terminó sin completarse. Solo lo marca Everdue |
| **Completada** | Hecha dentro de su período |
| **Completada tarde** | Hecha después de que el período terminara |
| **Cancelada** | Una tarea puntual que ya no aplica |

**Vencida** no está en la lista a propósito: no es un estado sino un hecho. Todo lo que no esté
terminado y haya pasado su fecha muestra una etiqueta roja *Vencida*, sea cual sea su estado.

## Todo junto

> *«Inspeccionar el generador el día 1 de cada mes»* es una **responsabilidad**, cuyo responsable es
> Marta, sobre la **entidad** *Generador n.º 3*, ejecutada por el **departamento** *Mantenimiento*.
>
> El 1 de marzo Everdue crea la **ocurrencia** de marzo, que vence el 1 de marzo. Marta la pone **en
> espera** el día 2 — *esperando proveedor*, la pieza no ha llegado. La pieza se retrasa; el 1 de abril
> el período termina y marzo queda **incumplido**. La ocurrencia de abril aparece esa misma mañana.
>
> La pieza llega el 3 de abril. Marta completa la ocurrencia de marzo: queda **completada tarde**. El
> incumplimiento de marzo permanece en el registro, y el reporte de tiempo en espera muestra
> exactamente cuántos días se esperó a ese proveedor.

Cada número de Everdue está construido con historias como esa. No hay que escribir nada más para que
los reportes funcionen.
