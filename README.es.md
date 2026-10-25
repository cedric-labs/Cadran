# Cadran - Gestión de Finanzas Personales y Patrimonio

<p align="center">
  <strong>Idiomas:</strong>
  <a href="README.md">English</a> |
  <a href="README.fr.md">Français</a> |
  <strong>Español</strong> |
  <a href="README.de.md">Deutsch</a>
</p>

<p align="center">
  <img src="assets/cadran-logo.png" alt="Logotipo de Cadran" width="220" />
  <br/>
  <a href="https://ko-fi.com/curvecoder"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Apoyar a curvecoder en Ko-fi" /></a>
</p>

<p align="center">
  <strong>Una plataforma moderna de gestión financiera personal, control presupuestario y patrimonio, local y con privacidad garantizada.</strong>
  <br/>
  Desarrollada con ASP.NET Core 9, C# 12, Entity Framework Core y SQLite cifrado (SQLCipher).
</p>

---

## Documentación

- 🗺️ **[Guía Visual Paso a Paso (docs/README.md)](docs/README.md)**: Manual interactivo con capturas de pantalla de alta resolución, llamadas de acción numeradas y guías paso a paso para las 24 pantallas en los 6 módulos funcionales.
- 📖 **[Guía del Usuario y Operaciones (docs/es/user-guide.md)](docs/es/user-guide.md)**: Manual integral que cubre inicio rápido, tutoriales paso a paso, el supervisor Windows Tray, servicio Windows (NSSM), binario autónomo para Linux, archivo de distribución Linux (`.tar.gz`), integración con `systemd`, despliegues aislados (air-gapped) y conceptos financieros.
- 🏗️ **[Especificación de Arquitectura e Ingeniería (docs/es/architecture.md)](docs/es/architecture.md)**: Referencia técnica en profundidad que detalla la arquitectura de componentes, los frameworks compartidos desacoplados (`StatementForge`, `SqliteForge`, `LocaleForge`, `FileForge`), modelos de concurrencia y memoria, y seguridad en dos niveles.

---

## ¿Por qué Cadran?

La mayoría de las aplicaciones de finanzas personales requieren vincular credenciales bancarias con agregadores en la nube, exponiendo transacciones confidenciales a terceros, o dependen de servicios en la nube bajo suscripción que pueden descontinuarse o sufrir filtraciones de datos.

**Cadran se basa en principios fundamentales opuestos:**
1. **Soberanía de datos local (Local-First)**: Todas sus transacciones, cuentas bancarias, categorías y reglas se almacenan localmente en una única base de datos SQLite (`cadran.db`). Ningún dato financiero sale de su máquina.
2. **Cero scrapers remotos ni agregadores externos**: Conserve el control total sobre sus registros bancarios importando directamente extractos estándar **OFX**, **QFX** o **CSV** emitidos por su entidad bancaria.
3. **Aislamiento multiusuario**: Aloje múltiples espacios independientes en la misma instancia (miembros de la familia o gestión empresarial vs. personal) con libros, reglas y paneles completamente compartimentados.
4. **Seguridad en dos niveles**: Protección mediante hash de contraseñas por cuenta combinada con cifrado integral en reposo independiente del hardware (**AES-256 / SQLCipher**). Para acceso remoto, se recomienda conectar mediante una VPN privada (como WireGuard).
5. **100 % autónomo y sin conexión (Air-Gapped)**: Todos los recursos web, iconos, fuentes y scripts están integrados localmente. No se requieren CDN, píxeles de seguimiento ni acceso a internet.

---

## Características Principales

| Dominio | Capacidades |
|---|---|
| **Patrimonio y Cuentas** | Vista consolidada del patrimonio neto (Efectivo disponible, Ahorro e Inversiones, Deuda / Descubiertos), 16 tipos de cuentas preconfigurados y estados personalizables, compatibilidad multidivisa (EUR, USD, GBP, CHF, CAD, etc.). |
| **Importación y Deduplicación de Extractos** | Ingesta optimizada de archivos **OFX / QFX** (asociación automática por `<ACCTID>`) y **CSV** personalizables (perfiles de mapeo reutilizables). Prevención estricta de duplicados y conciliación interactiva por coincidencia difusa. |
| **Libro Mayor y Desgloses** | Búsqueda multicriterio instantánea, desglose de transacciones entre múltiples categorías y beneficiarios, desplazamiento presupuestario (imputación de nóminas de fin de mes a M+1) y conciliación por lotes. |
| **Categorías y Beneficiarios** | Jerarquía de dos niveles (categorías principales y subcategorías), clasificaciones Ingreso / Gasto / Salario, bilingüismo nativo (inglés / francés) y normalización de comercios/beneficiarios. |
| **Motor de Reglas Automatizadas** | Reglas de categorización priorizadas mediante comodines (`*`, `%`, `?`) o expresiones regulares, filtros por cuenta e importe, asignación automática de beneficiarios y retroactividad. |
| **Gastos Periódicos y Margen Disponible** | Detección automática de suscripciones y facturas periódicas con puntuación de confianza, seguimiento de estado (Cargado, Pendiente, Vencido) y previsión dinámica del **Margen Disponible** (*Safe-to-Spend*). |
| **Paneles y Tablas Dinámicas (TCD)** | Widgets visuales configurables (gráficos, barras, indicadores, KPIs) y **tablas dinámicas** interactivas con inspección detallada de movimientos y exportación a CSV. |
| **Apariencia y Confort** | Interfaz bilingüe nativa (inglés / francés), ajuste tipográfico con vista previa en directo, modos de tabla *Compacto* vs. *Confort* y personalización del día de inicio del ciclo presupuestario. |

---

## Inicio Rápido

### 1. Escritorio Windows (Supervisor en la Bandeja del Sistema)

Cadran incluye un supervisor nativo de escritorio Windows (`Cadran.Launcher.exe`) accesible desde el área de notificaciones:
- Controla el ciclo de vida del servidor web Kestrel.
- Abre su navegador predeterminado en `http://localhost:5000` con un solo clic.
- Genera automáticamente copias de seguridad con marca temporal en `app/backups/` al iniciar y al cerrar.

```powershell
# Ejecución desde el código fuente
dotnet run --project Cadran.Launcher/Cadran.Launcher.csproj

# O generar el paquete de distribución autónomo
./scripts/publish-windows.ps1
```

### 2. Binario Autónomo Único para Linux (`Cadran-*-linux-x64`)

**Cuándo elegirlo:** Ideal para evaluaciones rápidas, equipos personales de desarrollo, scripts portátiles o servidores ad-hoc. Es un ejecutable autónomo sin descompresión previa que incluye el runtime completo de .NET 9 y el motor SQLCipher.

```bash
# 1. Descargar el binario de release y otorgar permisos de ejecución
chmod +x Cadran-1.0.0-linux-x64

# 2. Iniciar directamente (crea automáticamente ./data/cadran.db en el primer arranque)
./Cadran-1.0.0-linux-x64 --urls http://0.0.0.0:5000

# 3. Acceder a la interfaz web
xdg-open http://localhost:5000 # o visitar http://<ip-servidor>:5000
```

### 3. Archivo de Distribución Linux (`Cadran-*-linux-x64.tar.gz`) y `systemd`

**Cuándo elegirlo:** Recomendado para servidores de producción, servidores domésticos y ejecución prolongada como servicio bajo un usuario del sistema sin privilegios. Se descomprime en una estructura estándar (`/opt/cadran`) con directorios `data/` y `backups/` listos para usar.

```bash
# 1. Crear un usuario de sistema dedicado sin privilegios
sudo useradd --system --shell /usr/sbin/nologin --home-dir /opt/cadran cadran
sudo mkdir -p /opt/cadran
sudo chown -R cadran:cadran /opt/cadran

# 2. Descomprimir el archivo de distribución
sudo tar -xzf Cadran-1.0.0-linux-x64.tar.gz -C /opt/cadran
sudo chown -R cadran:cadran /opt/cadran
sudo chmod +x /opt/cadran/Cadran

# 3. Iniciar directamente o implementar como servicio systemd seguro
sudo -u cadran /opt/cadran/Cadran --urls http://0.0.0.0:5000
```
*(Consulte las [Operaciones de Producción](docs/es/user-guide.md#33-running-on-linux-production-deployments) para obtener la unidad de servicio systemd completa y el arranque automático).*

---

## Arquitectura y Frameworks Compartidos

`Cadran` está construido sobre librerías modulares reutilizables del ecosistema `curvecoder`, compatibles con los principios de Clean Architecture y validadas por las Quality Gates de SonarQube:

- **[StatementForge](https://github.com/curvecoder/StatementForge)** (`v1.0.0`): Analizador de extractos bancarios de alto rendimiento en streaming (OFX, QFX, CSV) con deduplicación de transacciones, perfiles de mapeo CSV y detección de gastos periódicos.
- **[SqliteForge](https://github.com/curvecoder/SqliteForge)** (`v1.0.0`): Infraestructura SQLite de alta resiliencia con control de concurrencia WAL, cifrado AES-256 SQLCipher/SQLiteMC y proveedores dinámicos de configuración.
- **[LocaleForge](https://github.com/curvecoder/LocaleForge)** (`v1.0.0`): Librería ligera de localización jerárquica JSON y formateador de divisas ISO-4217.

Todas las dependencias se restauran automáticamente desde GitHub Packages mediante el archivo `nuget.config` incluido.

Para ejecutar la verificación de calidad completa (formateo, compilación Release, pruebas unitarias y de arquitectura con cobertura OpenCover, y Quality Gate de SonarQube):
```powershell
.\verify.ps1
```

---

## Soporte y Patrocinio

Cadran es un software libre, gratuito y desarrollado de forma independiente. Si Cadran le resulta útil para administrar sus finanzas y proteger su soberanía digital, considere apoyar su desarrollo continuo:

[![Apoyar en Ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/curvecoder)

Sus contribuciones financian directamente el mantenimiento continuo, las optimizaciones de rendimiento y la infraestructura de código abierto.

---

## Licencia

Este proyecto está bajo la [Licencia MIT](LICENSE) &copy; 2026 curvecoder.
