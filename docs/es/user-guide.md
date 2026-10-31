# Cadran - Guía del Usuario y Operaciones en Producción

<p align="center">
  <strong>Idiomas:</strong>
  <a href="../user-guide.md">English</a> |
  <a href="../fr/user-guide.md">Français</a> |
  <strong>Español</strong> |
  <a href="../de/user-guide.md">Deutsch</a>
</p>

Bienvenido al manual oficial de usuario de **Cadran**, una plataforma moderna de gestión de finanzas personales, control presupuestario y administración de patrimonio que prioriza la privacidad y el funcionamiento local.

Esta guía se organiza según el marco documental **Diátaxis**:
- 🎓 **[1. Tutorial de Inicio Rápido](#1-tutorial-de-inicio-r%C3%A1pido)** *(Aprendizaje)*: Paso a paso para configurar su espacio, importar su primer extracto y evaluar su salud financiera en 10 minutos.
- 🛠️ **[2. Guías Prácticas y Procedimientos](#2-gu%C3%ADas-pr%C3%A1cticas-y-procedimientos)** *(Orientado a tareas)*: Instrucciones para operaciones cotidianas (creación de perfiles CSV, desglose de tickets, reglas de automatización, copias cifradas).
- 🚀 **[3. Despliegue y Operaciones de Producción](#3-despliegue-y-operaciones-de-producci%C3%B3n)** *(Operativo)*: Uso del supervisor Windows Tray, ejecución como servicio de Windows con NSSM, binario autónomo para Linux, archivo de distribución con unidad systemd segura y entornos aislados (air-gapped).
- 💡 **[4. Conceptos Clave y Lógica Financiera](#4-conceptos-clave-y-l%C3%B3gica-financiera)** *(Comprensión)*: Principios contables, Fecha Real vs. Fecha de Imputación Presupuestaria, cálculo del Margen Disponible y modelo de seguridad en dos niveles.
- 📖 **[5. Referencia Funcional](#5-referencia-funcional)** *(Informativo)*: Sintaxis de comodines, formatos de importación y navegación mediante teclado.

---

## 1. Tutorial de Inicio Rápido

### 1.1. Iniciar la Aplicación
1. **Ejecutar el binario**:
   - En Windows: Haga doble clic en `Cadran.Launcher.exe` (o ejecute `dotnet run --project Ledger/Ledger.csproj`).
   - En Linux: Ejecute `./Cadran-1.0.0-linux-x64` o inicie el demonio systemd `cadran.service`.
2. **Acceder a la Interfaz Web**: Abra su navegador y visite `http://localhost:5000`.
3. **Seleccionar Idioma**: Haga clic en el selector de idioma (`EN` / `FR`) en la barra de navegación superior.
4. **Registrar su Cuenta**:
   - Haga clic en **Crear una cuenta**.
   - Introduzca su nombre completo, dirección de correo electrónico y una contraseña segura (mínimo 8 caracteres, combinando letras y números).
   - Al registrarse, Cadran inicializa automáticamente un catálogo predeterminado completo de categorías de ingresos y gastos.

### 1.2. Crear su Primera Cuenta Bancaria
1. En la barra de navegación, acceda a **Cuentas** (`/Accounts`).
2. Haga clic en **Nueva Cuenta**.
3. Indique la entidad financiera (ej. `Santander`, `BBVA`, `CaixaBank`, `Chase`).
4. Asigne el **Nombre de la cuenta** (ej. `Cuenta Corriente Principal`).
5. Seleccione el **Tipo de cuenta** (Corriente, Ahorro, Inversión, Tarjeta de Crédito) y la **Divisa** (ej. `EUR`, `USD`, `GBP`, `MXN`).
6. Introduzca su **Saldo Inicial** previo al primer extracto que vaya a importar y haga clic en **Guardar**.

### 1.3. Importar su Primer Extracto
1. Vaya a **Importaciones** (`/Import`).
2. Seleccione la cuenta de destino en la lista desplegable.
3. Elija el formato de archivo:
   - **OFX / QFX**: Formato bancario nativo con validación automática del número de cuenta `<ACCTID>`.
   - **CSV**: Formato de texto delimitado configurable con perfiles de mapeo.
4. Arrastre y suelte su archivo bancario.
5. Revise la vista previa de las transacciones y haga clic en **Confirmar Importación**. Cadran descartará automáticamente los posibles duplicados.

---

## 2. Guías Prácticas y Procedimientos

### 2.1. Desglosar una Transacción en Varias Categorías
Cuando un único recibo o ticket incluye compras pertenecientes a diferentes partidas presupuestarias (ej. alimentación y material de oficina):
1. Vaya a **Transacciones** (`/Transactions`).
2. Haga clic en la fila de la transacción para abrir el panel lateral de edición.
3. Haga clic en **Desglosar Transacción**.
4. Defina el importe de cada línea, su categoría y un sub-beneficiario opcional.
5. La suma de todas las líneas del desglose debe coincidir con el importe total original.
6. Haga clic en **Guardar Desglose**.

### 2.2. Automatizar la Categorización con el Motor de Reglas
1. Acceda a **Configuración > Reglas de Automatización** (`/Rules`).
2. Haga clic en **Nueva Regla**.
3. Defina los criterios:
   - **Patrón de Descripción**: Use comodines (`*AMZN*`, `NETFLIX%`) o expresiones regulares (`^UBER\s*EATS`).
   - **Filtro de Cuenta**: Limite la regla a una cuenta específica o aplíquela globalmente.
   - **Rango de Importes**: Límites mínimo y máximo opcionales.
4. Defina la acción objetivo:
   - Asignar categoría y subcategoría.
   - Asignar beneficiario / comercio normalizado.
   - Opcional: Activar **Mover al Mes Presupuestario Siguiente** (+1 mes de compensación).
5. Marque **Aplicar retroactivamente a transacciones existentes** para reclasificar su historial.
6. Haga clic en **Guardar Regla**.

### 2.3. Gestionar Gastos Periódicos y Margen Disponible
1. Acceda a **Gastos Periódicos** (`/Recurring`).
2. Revise las suscripciones y facturas fijas detectadas automáticamente (gimnasio, streaming, alquiler, suministros).
3. Confirme o ajuste la periodicidad (Mensual, Trimestral, Anual) y el día de cobro previsto.
4. La métrica **Margen Disponible** (*Safe-to-Spend*) descuenta de forma dinámica los compromisos periódicos pendientes de su saldo actual a medida que avanza el mes.

### 2.4. Generar Tablas Dinámicas (TCD)
1. Acceda a **Informes > Tabla Dinámica** (`/Reports/Pivot`).
2. Seleccione sus dimensiones:
   - **Eje de Filas**: Categorías, Comercios o Cuentas.
   - **Eje de Columnas**: Mes, Trimestre o Año.
   - **Valores**: Total Neto, Ingresos, Gastos o Número de Transacciones.
3. Haga clic en cualquier celda para examinar las transacciones subyacentes en una ventana modal detallada.
4. Exporte la agregación resultante a CSV en cualquier momento.

---

### 2.5. Introducir una operación a mano y conciliarla con el extracto

El botón **Nueva operación** del registro de operaciones añade una operación antes de que llegue el extracto. Las entradas manuales llevan la etiqueta «Entrada manual» y se aíslan con el filtro **Origen**. Tras importar el extracto, abrir **Conciliar entradas manuales** (o seguir el enlace del resultado de la importación): cada par propuesto comparte cuenta, tipo e importe, con fechas dentro de la tolerancia de duplicados. **Fusionar** conserva la operación importada, completa sus campos vacíos (categoría, comercio, fecha presupuestaria, estado, notas, desglose) con los de la entrada y elimina esta última.

## 3. Despliegue y Operaciones de Producción

### 3.1. Supervisor Windows Tray (`Cadran.Launcher.exe`)
Para puestos de escritorio Windows, `Cadran.Launcher.exe` ofrece una integración silenciosa:
- Permanece en el área de notificaciones (junto al reloj del sistema).
- Inicia automáticamente el servidor Kestrel y abre su navegador predeterminado.
- Realiza copias de seguridad de la base de datos con marca temporal al iniciar y al cerrar.
- Incluye un menú contextual con opciones directas para **Ver registros** y **Reiniciar servidor**.

### 3.2. Ejecución como Servicio Seguro de Windows (NSSM)
Para ejecutar Cadran como servicio permanente en segundo plano sin necesidad de mantener abierta una sesión interactiva:

1. **Instalar NSSM**:
   ```powershell
   # Vía winget
   winget install NSSM.NSSM

   # Vía Chocolatey
   choco install nssm

   # Vía Scoop
   scoop install nssm
   ```

2. **Configurar e Instalar el Servicio**:
   ```powershell
   # Definir rutas
   $serviceName = "Cadran"
   $binaryPath = "C:\Cadran\app\Cadran.exe"
   $appDir = "C:\Cadran\app"

   # Instalar servicio
   nssm install $serviceName $binaryPath
   nssm set $serviceName AppDirectory $appDir
   nssm set $serviceName AppParameters "--urls http://0.0.0.0:5000"
   nssm set $serviceName Start SERVICE_AUTO_START

   # Variables de entorno
   nssm set $serviceName AppEnvironmentExtra "ASPNETCORE_ENVIRONMENT=Production" "Server__AllowNetworkAccess=true"

   # Iniciar servicio
   nssm start $serviceName
   ```

3. **Verificar Estado**:
   ```powershell
   Get-Service -Name Cadran
   ```

### 3.3. Ejecución en Linux: Binario Autónomo vs. Archivo de Distribución

Cadran se distribuye en dos formatos Linux x64 complementarios. Ambos son 100 % autónomos (self-contained) y agrupan el runtime .NET 9 junto con el motor SQLCipher, sin necesidad de dependencias adicionales en el sistema.

#### Comparativa de Formatos

| Criterio | Binario Autónomo (`Cadran-*-linux-x64`) | Archivo de Distribución (`Cadran-*-linux-x64.tar.gz`) |
|---|---|---|
| **Empaquetado** | Binario ELF único independiente | Archivo tarball comprimido |
| **Extracción Necesaria** | No (Ejecución directa) | Sí (`tar -xzf ...`) |
| **Estructura Incluida** | Solo ejecutable (crea `data/` al iniciar) | Directorios completos (`data/`, `backups/`, `README.md`, `LICENSE`) |
| **Uso Recomendado** | Pruebas rápidas, uso personal, máquinas de dev | Servidores de producción, homelabs, servicios systemd |
| **Gestión en Disco** | Creación automática en el directorio actual | Estructura con permisos preconfigurados en `/opt/cadran` |

---

#### Método A: Binario Único Autónomo (`Cadran-*-linux-x64`)

**Cuándo elegirlo:** Busca la vía más rápida para ejecutar Cadran en Linux sin descompresión previa ni configuración de carpetas.

1. **Descargar y otorgar permisos de ejecución**:
   ```bash
   chmod +x Cadran-1.0.0-linux-x64
   ```

2. **Iniciar directamente**:
   ```bash
   ./Cadran-1.0.0-linux-x64 --urls http://0.0.0.0:5000
   ```
   *Nota:* En su primer arranque, Cadran comprueba la ausencia del directorio de datos, crea automáticamente `./data/` e inicializa `cadran.db`.

---

#### Método B: Archivo de Distribución en Producción (`Cadran-*-linux-x64.tar.gz`) con `systemd`

**Cuándo elegirlo:** Despliegue permanente como servicio sin supervisión en un servidor doméstico o VPS con inicio automático en el arranque del sistema.

1. **Crear usuario y directorio dedicados sin privilegios**:
   ```bash
   sudo useradd --system --shell /usr/sbin/nologin --home-dir /opt/cadran cadran
   sudo mkdir -p /opt/cadran
   ```

2. **Descomprimir archivo de distribución y configurar permisos**:
   ```bash
   sudo tar -xzf Cadran-1.0.0-linux-x64.tar.gz -C /opt/cadran
   sudo chown -R cadran:cadran /opt/cadran
   sudo chmod +x /opt/cadran/Cadran
   ```

3. **Desplegar la unidad de servicio systemd protegida (`/etc/systemd/system/cadran.service`)**:
   ```ini
   [Unit]
   Description=Cadran Personal Finance Web Application
   After=network.target

   [Service]
   Type=simple
   User=cadran
   Group=cadran
   WorkingDirectory=/opt/cadran
   ExecStart=/opt/cadran/Cadran --urls http://127.0.0.1:5000
   Restart=always
   RestartSec=10

   # Protección de seguridad
   NoNewPrivileges=true
   ProtectSystem=full
   ProtectHome=true
   PrivateTmp=true
   ProtectControlGroups=true

   # Entorno
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

   [Install]
   WantedBy=multi-user.target
   ```

4. **Habilitar e iniciar el servicio**:
   ```bash
   sudo systemctl daemon-reload
   sudo systemctl enable --now cadran.service
   sudo systemctl status cadran.service
   ```

5. **Consultar registros de la aplicación**:
   ```bash
   journalctl -u cadran.service -f
   ```

### 3.4. Entornos Aislados sin Conexión (Air-Gapped)
Cadran es 100 % autónomo:
- Todos los estilos, iconos y tipografías (Inter, Bootstrap Icons) están embebidos localmente en `wwwroot`.
- No realiza peticiones a CDNs ni a servidores de recursos externos.
- Opera con total fiabilidad en puestos de trabajo desconectados de internet o redes cerradas.

---

## 4. Conceptos Clave y Lógica Financiera

### 4.1. Fecha Real vs. Fecha de Imputación Presupuestaria
Un conflicto presupuestario común ocurre cuando los ingresos de nómina recibidos a final de mes (ej. cobrados el 28 de abril) distorsionan el flujo de caja de abril en lugar de financiar los costes de mayo.
- **Fecha Real**: Fecha bancaria efectiva del movimiento (utilizada para conciliar extractos).
- **Fecha de Imputación Presupuestaria**: Período contable que la transacción financia realmente. Las reglas pueden desplazar automáticamente ingresos a `+1 mes` para preservar comparativas presupuestarias fieles.

### 4.2. Cálculo del Margen Disponible (Safe-to-Spend)
$$\text{Margen Disponible} = \text{Saldo Actual en Cuenta} - \sum \text{Compromisos Periódicos Pendientes hasta el Próximo Ingreso}$$

En vez de esperar a que se carguen las facturas programadas, Cadran reserva fondos para los débitos esperados desde el primer día del ciclo mensual.

### 4.3. Modelo de Seguridad y Acceso Remoto (VPN vs. 2FA TOTP No Probada)
Cadran opera mediante un modelo de defensa en dos niveles (aislamiento por cuenta de usuario y cifrado opcional en reposo de la base SQLite con SQLCipher).

> [!WARNING]
> **Aviso de Seguridad Crítico sobre Acceso Remoto y 2FA TOTP**:
> La autenticación de dos factores TOTP con contraseñas de un solo uso fue incorporada inicialmente con claves de recuperación sin conexión. No obstante, **esta funcionalidad no ha sido probada y será descontinuada y eliminada en una versión futura**.
> Se desaconseja terminantemente exponer Cadran directamente a internet. Cadran está estrictamente concebida como una aplicación de ámbito local. Si necesita acceder a su instancia de forma remota fuera de su red doméstica, **no debe** abrir puertos web directamente a internet; utilice en su lugar un túnel VPN privado y seguro como **WireGuard** o Tailscale.

---

## 5. Referencia Funcional

### 5.1. Sintaxis de Coincidencias en Reglas
| Patrón | Sintaxis | Ejemplo | Coincide con |
|---|---|---|---|
| **Exacto** | Texto plano | `NETFLIX` | Etiqueta exacta `NETFLIX` |
| **Comodín prefijo/sufijo** | `*` o `%` | `*AMAZON*` | Cualquier transacción que contenga `AMAZON` |
| **Carácter único** | `?` | `TAXI ?` | `TAXI 1`, `TAXI A` |
| **Expresión regular** | `^regex$` | `^(UBER\|BOLT)\s*VIAJE` | Descripciones que comiencen por `UBER VIAJE` o `BOLT VIAJE` |

### 5.2. Formatos de Importación Compatibles
- **OFX 1.x / 2.x & QFX**: Compatibilidad integral con las etiquetas `<STMTTRN>`, `<FITID>`, `<TRNAMT>`, `<NAME>`, `<MEMO>`.
- **CSV**: Perfiles de mapeo flexibles compatibles con delimitadores personalizados (`;`, `,`, `\t`), formatos de fecha (`dd/MM/yyyy`, `yyyy-MM-dd`, `MM/dd/yyyy`) y notaciones decimales (`.` o `,`).
