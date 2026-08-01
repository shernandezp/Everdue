# Reportes

*Solo administradores.*

Los reportes responden **«¿qué requiere atención hoy?»**. Se construyen enteramente a partir del propio
trabajo: nadie rellena nada para que un reporte exista.

Cada número es un enlace. Púlsalo y llegas a la lista, filtrada exactamente al trabajo que hay detrás
de esa cifra. Si un número parece raro, púlsalo y mira las filas.

## La barra de filtros

Todos los reportes comparten tres filtros: **responsable**, **departamento** y **tipo de entidad**.
Ponlos una vez arriba y toda la pantalla se estrecha.

## Excepciones — la pantalla diaria

Cinco tarjetas arriba:

| Tarjeta | Significa |
|---|---|
| **Vencen hoy** | Vencen antes de que acabe hoy y no están terminadas |
| **Completadas hoy** | Terminadas hoy: el único número de buenas noticias de la pantalla |
| **Vencidas** | Pasó la fecha, sin terminar, y el período todavía no cerró |
| **Incumplidas** | Períodos que terminaron sin completarse, en el rango |
| **En espera** | Aparcadas ahora mismo |

Debajo:

- **En espera por motivo**: dónde está la espera, con la más antigua de cada grupo. Un grupo grande de
  *esperando proveedor* con una espera de tres semanas es una conversación, no una estadística.
- **Retrasos crónicos**: responsabilidades que se incumplen una y otra vez, no las que se incumplieron
  una vez. Por defecto: tres incumplimientos en los últimos ocho períodos cerrados.
- **Elementos reasignados en el periodo**: cuánto trabajo cambió de manos. Un número alto explica un mal
  cumplimiento mejor que cualquier persona.

## Salud por entidad

Una fila por cliente, proveedor o máquina:

| Columna | Significa |
|---|---|
| **Abiertas** | Trabajo pendiente ahora mismo |
| **Vencidas** | De ese, cuánto pasó su fecha |
| **Incumplidas 30d / 60d / 90d** | Períodos incumplidos en los últimos 30, 60 y 90 días |
| **En espera** | Aparcadas ahora mismo |
| **Última actividad** | El último trabajo *completado*; nada más cuenta |
| **Días desde** | Cuánto hace de eso |

Lee las tres columnas de incumplimientos juntas: 5 / 5 / 5 es un problema viejo que ya paró; 5 / 2 / 1
es un problema que está empeorando.

## Sin atención

Entidades **sin trabajo completado** durante más de N días — 90 por defecto; cambia el número arriba.

«Última actividad» significa el último trabajo completado y nada más. Ni un correo abierto, ni una
llamada registrada, ni un contacto automático. Eso es lo que hace fiable esta lista donde el registro
de actividad de un CRM no lo es.

Una entidad que nunca ha tenido trabajo completado muestra **∞** en vez de un número: no lleva esperando
cierta cantidad de días, lleva esperando todo el tiempo.

## Bloqueado por entidad

Todo el trabajo en espera ahora mismo, agrupado por entidad y motivo, con la espera más antigua de cada
grupo.

Úsalo antes de una llamada: *«tenemos cuatro cosas esperándote, la más antigua desde el día 3»* es
mucho mejor apertura que *«seguimos esperando»*.

## Línea de tiempo de la entidad

Se llega pulsando una entidad en cualquier parte del producto. Todas sus ocurrencias y tareas puntuales
de la más reciente a la más antigua, con estado, fechas y avance de verificación. La relación entera,
en orden.

## Exportar

Salud por entidad, Sin atención y Bloqueado por entidad tienen un botón **Exportar CSV**. El archivo
contiene exactamente las filas de la pantalla con los filtros que pusiste. Por encima de 50 000 filas
Everdue se niega y te pide estrechar los filtros en vez de darte un archivo incompleto.
