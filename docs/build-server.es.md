# Plataforma BuildServer

BuildServer es el punto de entrada Web/Agent de la herramienta de build automatizada, soportando iOS, Android APK/AAB y subida a Google Play. La primera versión usa un solo Mac, un solo Worker y una cola serie para evitar la contención concurrente entre Unity, Xcode, Gradle, los entornos de firma y el estado de caché/certificados.

## Módulos

- `BuildServer.Api`: ASP.NET Core Minimal API para login, proyectos, configuraciones, tareas, artefactos y auditoría.
- `BuildServer.Worker`: Worker serie en segundo plano que desencola tareas e invoca el CLI `AutomationUnityBuildIOS`.
- `BuildServer.Web`: Frontend estático integrado para login web y envío de builds.
- `BuildServer.Mcp`: Endpoint de herramientas JSON-RPC `/mcp` para Agent/AI.
- `BuildServer.Reverse`: Módulo de conexión inversa que permite a BuildServer conectarse proactivamente a LinuxGateway, adecuado para entornos NAT/intranet.
- `buildserver-data`: Directorio de persistencia JSON, almacenando usuarios, proyectos, configuraciones, tareas, artefactos, registros de auditoría y nodos Worker.

## Inicio local

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

Dirección por defecto:

```text
http://127.0.0.1:5088
```

Cuenta por defecto:

```text
admin
```

Si `BUILD_SERVER_ADMIN_PASSWORD` no está establecido, se genera una contraseña aleatoria en el primer inicio:

```text
<DataRoot>/initial-admin.txt
```

Si `BUILD_SERVER_AGENT_TOKEN` no está establecido, se genera una Agent API Key aleatoria en el primer inicio:

```text
<DataRoot>/initial-agent-token.txt
```

Recomendado para producción:

```bash
export BUILD_SERVER_ADMIN_PASSWORD="strong-password"
export BUILD_SERVER_AGENT_TOKEN="strong-agent-token"
export BUILD_SERVER_PUBLIC_BASE_URL="https://build.example.com"
export BUILD_SERVER_ALLOWED_ORIGINS="https://build.example.com"
export BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS="/Users/build/UnityBuildWorkspace"
export BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS="/Users/build/UnityBuildArtifacts"
export BUILD_SERVER_ALLOWED_CONFIG_ROOTS="/Users/build/BuildServerData/configs"
export BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS="github.com"
```

Valores de seguridad por defecto:

- El workspace está restringido por defecto a `~/UnityBuildWorkspace`.
- Los artefactos están restringidos por defecto a `~/UnityBuildArtifacts`.
- Los archivos de configuración están restringidos por defecto al subdirectorio `configs` del directorio de datos de BuildServer y al directorio `configs` del programa.
- Los repositorios Git permiten URLs HTTPS/SSH por defecto; en producción, establecer `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS`, por ejemplo `github.com` o el dominio del servidor Git corporativo.
- Si se accede a la interfaz web vía Nginx/Caddy u otros proxies inversos, establecer `BUILD_SERVER_PUBLIC_BASE_URL` y `BUILD_SERVER_ALLOWED_ORIGINS`, de lo contrario la protección cross-site rechazará escrituras con orígenes no coincidentes.

## Publicación en Mac

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

Tras la publicación, usar `deploy/launchd/com.automationunity.buildserver.plist` para ejecutar como usuario `buildbot`. Los certificados, perfiles de provisioning, Unity License y claves SSH de Git deben instalarse bajo este usuario macOS dedicado.

## Datos obligatorios

Tras el primer inicio de sesión:

1. Añadir un proyecto: nombre del proyecto, repositorio Git, rama por defecto, ramas permitidas, workspace y directorio de artefactos.
2. Añadir una configuración: seleccionar iOS o Android. Puede referenciar un archivo JSON de configuración existente o marcar «Generar nuevo archivo de configuración», rellenar la versión de Unity, Bundle ID y los campos específicos de plataforma en el formulario web, y el servidor generará automáticamente el JSON y lo registrará.
   - Los campos iOS incluyen Team ID, Deployment Target, Export Method, Signing Style, copia de archive a Organizer, subida a App Store Connect/TestFlight.
   - Los campos Android incluyen APK/AAB/both, versiones SDK, keystore, Google Play Service Account, track, release status, artefacto de subida.
3. Iniciar un build: seleccionar el proyecto y la configuración, enviar la tarea.

BuildServer genera un snapshot de configuración independiente para cada tarea, reserva el Build Number e invoca el CLI:

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

## MCP/Agent

Endpoint MCP:

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

Herramientas:

- `list_projects`
- `list_configs`
- `start_build`
- `start_ios_build` (nombre heredado, las nuevas integraciones deben usar `start_build`)
- `get_build_status`
- `tail_build_log`
- `list_build_artifacts`

Por defecto, los Agents solo tienen permitido `dryRun=true`. Para permitir builds reales, establecer el `McpClientRecord.allowFullBuild` correspondiente a `true` en los datos y recomendar autorizar solo proyectos específicos. MCP envía tareas solo por ID de proyecto y configuración — no acepta repositorios Git o rutas arbitrarias.

Las nuevas configuraciones no están habilitadas para MCP por defecto; debe marcar explícitamente «Permitir MCP» en la interfaz web.

## Notificaciones por email

BuildServer incluye un servicio de notificaciones por email integrado (`EmailNotificationService`) que envía automáticamente emails tras la finalización de tareas de build:

- **Build exitoso**: El email incluye rutas de artefactos, tiempo transcurrido y resumen de configuración.
- **Build fallido**: El email incluye el paso fallido, resumen de errores y ruta de log.

Soporta SMTP 465 SSL implícito, listas de contactos y plantillas de email personalizadas. Configure el servidor SMTP, puerto, credenciales del remitente y lista de contactos en el backend web o en la página de notificaciones por email de DesktopApp.

## Gestión de almacenamiento

A medida que las tareas de build se acumulan, los artefactos consumen gradualmente espacio en disco. BuildServer proporciona dos mecanismos de gestión de almacenamiento:

- **Limpieza automática**: `MaintenanceService` limpia automáticamente las tareas y artefactos completados basándose en `RetentionDays` y `MaxArtifactBytes`.
- **Limpieza manual**: Ver la vista general de almacenamiento en el backend web o en la página de gestión de almacenamiento de DesktopApp, eliminación masiva o simple de artefactos históricos.

`StorageCleanupService` maneja el escaneo y eliminación reales de los directorios de artefactos.

## Conexión inversa

Si el nodo BuildServer está detrás de NAT, una red doméstica o un intranet corporativo donde LinuxGateway no puede acceder directamente, puede usar la conexión inversa para que BuildServer se conecte proactivamente a LinuxGateway.

Generar un Enrollment Token en la interfaz web de LinuxGateway, luego configurar BuildServer vía variables de entorno:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

Tras la conexión, las credenciales del nodo se guardan en el directorio de datos de BuildServer. El directorio `BuildServer/Reverse/` implementa la lógica del cliente de conexión inversa.

## Perímetros de seguridad

- Web/MCP solo crean tareas — no ejecutan comandos shell arbitrarios.
- El Worker se ejecuta en serie — solo una tarea a la vez.
- Los proyectos pueden restringir las ramas permitidas.
- El CLI valida internamente las listas blancas de Git y los perímetros de rutas.
- La descarga de artefactos de tareas requiere autenticación de login.
- Los logs de auditoría registran logins, creaciones de proyectos, creaciones de configuraciones, envío/cancelación de tareas y registro de Workers.
- El servicio de mantenimiento limpia las tareas y artefactos completados según `RetentionDays` y `MaxArtifactBytes`.
- La información sensible (contraseñas, tokens) en las notificaciones por email no se muestra — solo se usa para autenticación SMTP.

## Extensión multi-Mac

`WorkerNodeRecord` ya está persistido, y se proporcionan `/api/workers` y `/api/workers/register`. El Worker integrado de la primera versión es adecuado para un solo Mac; al escalar a múltiples Macs, la evolución recomendada es:

```text
BuildServer.Api central + Base de datos
Mac Worker A/B/C como procesos independientes
Los Workers toman las tareas que les convienen
Planificación por versión Unity/Xcode, autorización de proyecto, carga actual
```

En ese punto, la persistencia JSON debe reemplazarse por SQLite/PostgreSQL para evitar escrituras de archivos concurrentes entre máquinas.
