# Análisis

*Solo administradores.*

Los reportes responden *«¿qué requiere atención hoy?»*. El análisis responde **«¿qué sigue pasando?»**:
los mismos registros, leídos en meses en lugar de en horas.

No hay que escribir nada extra para producirlo. Enciende Everdue hoy y el análisis funciona hacia atrás
sobre el historial que ya tengas.

## La ventana

Cada pantalla de análisis tiene **Agrupar por** (semana o mes) y **Ventana** (cuántos períodos hacia
atrás). La columna más reciente es siempre el período que sigue en curso, así que se ve baja; la
pantalla lo dice bajo el gráfico.

## Cumplimiento — por responsabilidad

**Finalizaciones a tiempo ÷ períodos que ya cerraron.**

| Columna | Significa |
|---|---|
| **Cumplimiento** | El porcentaje, con el par del que sale al lado: *87% · 26/30* |
| **A tiempo** | Completadas dentro de su período |
| **Tarde** | Completadas después de que el período cerrara; cuentan como incumplimiento |
| **Incumplidos** | Nunca completados |
| **En curso** | Períodos aún sin cerrar; no entran en el porcentaje |
| **Tendencia** | La forma de los últimos N períodos |

Dos reglas que mantienen honesto el número:

- **Completar tarde cuenta como incumplimiento.** Ocurrió, y ocurrió tarde; el porcentaje dice lo
  segundo.
- **Con menos de cinco períodos cerrados no se muestra porcentaje**, solo los recuentos. 95% de 200 no
  es 100% de 3, y una responsabilidad joven no debería parecer perfecta.

Pulsa una responsabilidad para ver su propia página: las mismas cifras, una línea en el tiempo, y la
tira de períodos individuales — ✅ semana 29, ❌ semana 30, ⏸ semana 31. Pulsa cualquier período para
abrir esa ocurrencia.

## Fiabilidad — por persona

La misma aritmética, por persona, y **solo sobre trabajo recurrente**. Una tarea puntual no puede
incumplirse, así que contarla favorecería a todos por igual; las puntuales completadas tienen su propia
columna.

Esta pantalla está hecha para leerse como *¿dónde ayudo?*, y su diseño lo dice:

- **Solo administradores.** Nadie ve los números de un compañero, y a nadie se le avisa de los suyos.
- **Sin ranking, sin tabla de clasificación, sin insignias y sin objetivos** — y no los habrá.
- **Un porcentaje nunca aparece sin su volumen**, y los denominadores flacos se ocultan del todo.
- **Las esperas externas cuentan en el porcentaje** y se muestran al lado. Sacarlas permitiría a
  cualquiera mejorar su número aparcando trabajo en espera, que es justo lo contrario de para lo que
  están los motivos de espera. Lo que costó «esperando al cliente» se ve en la misma fila que el
  incumplimiento que explica.
- Los números siguen al **responsable actual** de cada elemento, y la pantalla muestra cuántas cosas
  cambiaron de manos en la ventana, que a menudo es la explicación real.

## Trabajo completado por entidad

Cuánto trabajo terminado corresponde a cada cliente, proveedor o máquina, período a período, con un
gráfico apilado.

Dos límites honestos, dichos en la pantalla:

- Es un **recuento de elementos, no de horas**. Everdue no guarda datos de tiempo: una llamada de dos
  minutos y una inspección de un día entero cuentan una cada una. Mide *atención*, no esfuerzo.
- El trabajo que nadie vinculó a una entidad se reporta como su propio total, en lugar de quedarse
  fuera en silencio.

Solo se grafican las entidades principales; el resto se cuenta y se reporta como *«N más sin mostrar»*.

## Tiempo en espera

Adónde se va la espera: días totales, días de media y la espera más larga, por motivo y por entidad,
con cuántas esperas siguen abiertas.

- Las cifras son **días naturales**: noches y fines de semana incluidos. El horario laboral necesitaría
  un calendario de turnos y festivos que Everdue no tiene ni va a tener.
- El historial se reconstruye a partir del registro de lo que pasó, así que esto funciona para meses
  anteriores a que a nadie se le ocurriera medirlo.

*«Esperando proveedor: 214 días en 31 esperas, la más larga 22»* es una conversación con ese proveedor,
con un número al lado.

## Retrasos crónicos

También en el tablero de excepciones: responsabilidades que incumplieron **K de sus últimos N períodos
cerrados**, por defecto tres de ocho. Una mala semana es la vida; tres de ocho es un problema de
diseño: el responsable equivocado, el día equivocado, o una obligación que nadie aceptó nunca.

## Exportar

Todas las tablas de análisis se exportan a CSV con la misma ventana y los mismos filtros que estás
viendo.
