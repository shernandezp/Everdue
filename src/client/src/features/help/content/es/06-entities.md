# Entidades

Una **entidad** es aquello *sobre lo que trata* un trabajo: un cliente, un proveedor, una máquina, un
departamento o una empresa.

## Qué es una entidad, y qué no

Una entidad es una **etiqueta**, no un expediente. Everdue guarda su nombre, su tipo y si sigue activa,
más unos pocos campos que tu administrador haya definido y que son solo para mostrar. **No** guarda
contratos, facturas, oportunidades, historiales de contacto ni documentos. Es una frontera deliberada:
Everdue gestiona trabajo, y en cuanto empieza a guardar datos del negocio se convierte en una mala
versión de otro producto.

Lo que se gana con esa contención es que cada reporte se puede cortar por entidad sin que nadie escriba
nada dos veces.

## Los cinco tipos

| Tipo | Normalmente |
|---|---|
| **Cliente** | A quién sirves |
| **Proveedor** | Quién te sirve |
| **Equipo** | Una máquina, un vehículo, una instalación |
| **Departamento** | Cuando el trabajo trata *sobre* un departamento, no lo ejecuta uno |
| **Empresa** | Una empresa del grupo o una sucursal |

> La **entidad de tipo Departamento** y el campo **Departamento** son cosas distintas a propósito. El
> campo departamento dice *quién ejecuta el trabajo*; una entidad de tipo departamento dice *el trabajo
> trata sobre ese departamento* — una auditoría interna, por ejemplo.

## Crear una

1. **Entidades → Nueva entidad.**
2. **Nombre**: como la gente la llama de verdad. Dos entidades del mismo tipo no pueden llamarse igual.
3. **Tipo**: uno de los cinco de arriba.
4. Rellena los **campos personalizados** que tu administrador haya definido para ese tipo.
5. **Crear**.

Los miembros pueden leer la lista de entidades; los administradores crean y editan.

## Campos personalizados

Un administrador puede añadir hasta diez campos extra por tipo de entidad: un gestor de cuenta en un
cliente, un número de serie en una máquina. Cuatro clases: texto, número, fecha o una lista para
elegir.

Son **solo para mostrar**. No filtran, no ordenan, no salen en reportes y no disparan nada. Existen
porque esa columna de más suele ser la última razón por la que un equipo sigue manteniendo una hoja de
cálculo.

## Desactivar una entidad

La papelera desactiva la entidad; nunca la borra. Su historial permanece, todo el trabajo pasado sigue
apuntando a ella y deja de aparecer en los selectores para trabajo nuevo. **Mostrar inactivas** las
devuelve a la lista.

## La línea de tiempo de una entidad

Pulsa el nombre de una entidad para abrir su **línea de tiempo**: todas sus ocurrencias y tareas
puntuales, de la más reciente a la más antigua, con estado, fechas y avance de verificación.

Es la memoria de atención al cliente. Antes de devolverle la llamada a Acme, abre Acme: *«semana 29
hecha, semana 30 incumplida, semana 31 esperándote a ti»* está en una sola pantalla y en orden.

## Importar una lista que ya tienes

Si tus clientes están en una hoja de cálculo, no los escribas otra vez: ver
[Administración](11-administration#importar-desde-una-hoja-de-clculo). La pantalla vacía enlaza
directamente al importador.

## Ejemplos del día a día

| Quieres | Haz esto |
|---|---|
| Ver todo lo de un cliente | Entidades → pulsa el nombre |
| Encontrar clientes que nadie toca desde hace meses | Reportes → Sin atención |
| Saber qué clientes absorben más trabajo | Análisis → Trabajo completado |
| Registrar sobre qué máquina fue un trabajo | Pon **Entidad** en el elemento |
| Registrar qué equipo lo hace | Pon **Departamento** en el elemento |
