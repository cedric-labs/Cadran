# Guía del Usuario: Cifrado de Base de Datos

[← Anterior: 03 - Inicio de Sesión](03-login.md) | [Volver al Índice General](../README.md) | [Siguiente: 05 - Desbloqueo en Frío →](05-database-unlock.md)

---

La pantalla de **Cifrado de Base de Datos** le permite proteger el archivo local de su base de datos con una frase de contraseña maestra mediante cifrado AES-256. Esto garantiza que nadie podrá leer su información financiera, incluso si copia directamente el archivo físico del disco de su equipo.

---

## 1. Descripción Visual y Puntos de Acción

![Pantalla de Cifrado de Base de Datos](../../images/screens/04-database-encryption-screen-annotated.png)

---

## 2. Tabla de Referencia de Acciones

| Marca | Elemento de Interfaz | Tipo de Acción | Qué hacer |
| :---: | :--- | :---: | :--- |
| <kbd>1</kbd> | **Frase de Contraseña Maestra** | **RELLENAR** | Elija una clave maestra robusta (mínimo 8 caracteres). Esta contraseña será obligatoria para desbloquear la base de datos. |
| <kbd>2</kbd> | **Confirmar Contraseña Maestra** | **RELLENAR** | Vuelva a escribir con exactitud la misma clave para confirmar. |
| <kbd>3</kbd> | **Recordar en este dispositivo** | **OPCIONAL** | Marque esta casilla si desea que Cadran guarde la clave en este equipo para desbloquearlo automáticamente al arrancar. |
| <kbd>4</kbd> | **Botón "Activar cifrado"** | **HACER CLIC AQUÍ** | Haga clic en este botón para cifrar inmediatamente la base de datos con protección AES-256. |

---

## 3. Procedimiento Paso a Paso

1. **Elegir su clave maestra**:
   - Escriba su contraseña secreta en **Frase de contraseña maestra** (**[1]**).
   - Confírmela en **Confirmar frase de contraseña** (**[2]**).
2. **Definir la preferencia de arranque**:
   - Marque **Recordar en este dispositivo** (**[3]**) para un desbloqueo fluido en su equipo personal.
   - Déjelo desmarcado si prefiere que Cadran solicite la clave maestra en cada inicio.
3. **Activar**:
   - Haga clic en el botón azul **Activar cifrado ahora** (**[4]**).
   - Sus datos quedarán completamente cifrados en disco.

> [!CAUTION]
> Si olvida su frase de contraseña maestra y no activó "Recordar en este dispositivo", **sus datos no se podrán recuperar de ninguna manera**. Conserve su clave en un gestor de contraseñas de confianza.
