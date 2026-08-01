# Responsabilidades

*Solo administradores.*

Una **responsabilidad** es una obligación permanente: el trabajo que vuelve cada día, semana, mes o
año. La configuras una vez y, a partir de ahí, Everdue produce el trabajo real.

## Crear una

**Responsabilidades → Nueva responsabilidad.**

| Campo | Qué poner |
|---|---|
| **Título** | Qué tiene que pasar, con las palabras del equipo: *«Seguimiento semanal con Acme»* |
| **Descripción** | Opcional. Lo que necesite saber quien la ejecuta |
| **Responsable** | Quién se encarga. Cada ocurrencia nace en su tablero |
| **Entidad** | Sobre qué trata: el cliente, la máquina. Opcional pero valioso |
| **Departamento** | Qué equipo la ejecuta. Opcional |
| **Se repite** | Todos los días, semanal en días elegidos, mensual en un día, o anual |
| **Comienza el** | La primera fecha desde la que aplica — **lee el aviso de abajo** |

### Cómo funciona cada repetición

| Se repite | Eliges | Ejemplo |
|---|---|---|
| **Todos los días** | nada | El arqueo diario de caja |
| **Semanal en días elegidos** | uno o varios días de la semana | Entregas de lunes y jueves |
| **Mensual en un día** | un número del 1 al 31 | Facturación el día 5 |
| **Anual** | un mes y un día | La renovación del seguro |

Un día que no existe en un mes corto cae en el último día de ese mes: *el 31* es el 28 de febrero en un
año normal y el 29 en uno bisiesto.

## ⚠ La fecha de inicio decide cuánto historial creas

Si pones **Comienza el** en una fecha pasada, Everdue rellena todos los períodos entre esa fecha y hoy;
y todos esos períodos ya terminaron, así que **todos quedan registrados como incumplidos**.

Para los reportes esos incumplimientos son reales, porque un incumplimiento en el registro es un
incumplimiento. Esa es la garantía sobre la que se sostiene todo el producto, y no se suaviza para
trabajo retroactivo.

**Pon la fecha de inicio en hoy** salvo que quieras ese historial a propósito — por ejemplo porque
vienes de una hoja de cálculo y los incumplimientos ocurrieron de verdad.

## Qué pasa después

Desde la fecha de inicio, Everdue crea una ocurrencia por período en el tablero del responsable, con
vencimiento al final del día previsto. Lo hace se haya completado o no la anterior: el trabajo no se
acumula en un único elemento imposible.

Cuando un período termina sin finalizarse, esa ocurrencia queda **incumplida** y la siguiente aparece
igualmente.

## Plantilla de verificación

Una responsabilidad puede llevar una lista ordenada de pasos.

1. Añade los pasos en el orden en que deben hacerse.
2. Marca **Obligatorio** en los que realmente tienen que ocurrir.
3. Usa las flechas para reordenar y la papelera para quitar.

Cada ocurrencia recibe **su propia copia** de la plantilla en el momento de crearse. Mejorar la
plantilla afecta solo a las ocurrencias futuras: el historial nunca se reescribe, que es lo que hace
que una inspección antigua siga mostrando lo que se pedía entonces.

## Reglas para completar

Dos reglas opcionales, ambas impuestas por el servidor y no meramente sugeridas por la pantalla:

| Regla | Efecto |
|---|---|
| **Exigir la lista de verificación para completar** | Todos los pasos *obligatorios* deben estar marcados |
| **Exigir una foto o archivo para completar** | Debe existir al menos un adjunto |

Ambas se aplican **a partir de la próxima finalización**. Nada de lo ya completado se reabre, y una
ocurrencia abierta no se bloquea hasta que alguien intenta terminarla.

Úsalas donde valgan la pena —una inspección de seguridad, una entrega que necesita prueba
fotográfica— y no en todas partes, o la gente aprende a marcar sin mirar.

## Pausar

**Pausar** detiene las nuevas ocurrencias hasta la fecha que elijas. Los períodos que caen enteros
dentro de la pausa se omiten al reanudar: una pausa autorizada **no** es un incumplimiento.

Úsalo para un cierre de planta, un cliente de vacaciones, una máquina fuera de servicio. Usa
**Reanudar** para terminarla antes.

## Reasignar una responsabilidad

**Reasignar** cambia el responsable. Las ocurrencias futuras siguen automáticamente al nuevo. Tú
decides si también se mueve el trabajo que ya está en el plato del anterior: normalmente sí cuando
alguien se va, y a menudo no cuando alguien falta una semana.

Cada cambio de responsable queda registrado en los elementos afectados, y el tablero de excepciones
cuenta cuántas cosas cambiaron de manos en el período.

## Desactivar

La papelera desactiva una responsabilidad: no habrá ocurrencias nuevas y las existentes quedan
exactamente como están en el registro. No se borra nada, y nada de lo que ya pasó cambia.

Desactiva cuando la obligación termina de verdad. Pausa cuando va a volver.

## Leer la lista

| Columna | Notas |
|---|---|
| Título | Con etiquetas: en pausa, cuántos pasos de verificación, si las reglas para completar están activas |
| Se repite | La regla en palabras, p. ej. *Semanal · lun, jue* |
| Responsable · Entidad | Quién y sobre qué |
| Próxima ocurrencia | Cuándo aparecerá la siguiente |
| Estado | Activa o inactiva |
