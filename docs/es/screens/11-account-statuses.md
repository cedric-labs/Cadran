# Guía del Usuario: Estados de Transacción Personalizados y Conciliación

[← Volver a Tipos de Cuentas](10-account-types.md) | [Volver al Índice General](../README.md)

---

La pantalla **Estados de Transacción** permite configurar el flujo de verificación y conciliación bancaria. Los estados acompañan a las operaciones a lo largo de su ciclo de vida: pendiente de cobro, liquidado por el banco, conciliado contra el extracto mensual o en disputa.

---

## 1. Descripción Visual y Puntos de Acción

![Pantalla de Estados de Transacciones](../../images/screens/11-account-statuses-screen-annotated.png)

---

## 2. Tabla de Referencia de Acciones

| Marca | Elemento de Interfaz | Tipo de Acción | Qué hacer |
| :---: | :--- | :---: | :--- |
| <kbd>1</kbd> | **Botón "Nuevo estado"** | **HACER CLIC AQUÍ** | Haga clic para abrir el diálogo y registrar una nueva etiqueta de estado (ej. `En disputa`, `Por verificar`). |
| <kbd>2</kbd> | **Distintivo de Estado** | **SELECCIONAR** | Revise la apariencia visual (icono y color) que se mostrará en el libro de movimientos. |
| <kbd>3</kbd> | **Indicador de Predeterminado** | **SELECCIONAR** | Identifica el estado asignado automáticamente a los movimientos importados de nuevos extractos. |
| <kbd>4</kbd> | **Botón "Editar"** | **HACER CLIC AQUÍ** | Haga clic para modificar el nombre, color, icono o condición de predeterminado de un estado. |

---

## 3. Flujo de Conciliación Recomendado

Un flujo de trabajo riguroso se estructura habitualmente en tres pasos:

1. **Pendiente**: Operación emitida o comprometida (cheque, pago con tarjeta pendiente) que aún no figura en el extracto.
2. **Liquidado**: El apunte se refleja en la web de su banco con fecha e importe confirmados.
3. **Conciliado**: El movimiento ha sido cotejado formalmente con el extracto PDF mensual oficial de la entidad.

---

## 4. Procedimiento Paso a Paso

1. **Crear un estado personalizado**:
   - Haga clic en **Nuevo estado** (<kbd>1</kbd>) arriba a la derecha.
   - Indique un nombre descriptivo (ej. `Por verificar`), escoja icono y color, y decida si será el predeterminado para nuevas importaciones.
2. **Modificar estados existentes**:
   - Haga clic en el botón lápiz **Editar** (<kbd>4</kbd>) en la fila que desee ajustar.
3. **Cambiar el estado predeterminado de importación**:
   - Edite el estado elegido y active la casilla **Estado predeterminado** (<kbd>3</kbd>).
