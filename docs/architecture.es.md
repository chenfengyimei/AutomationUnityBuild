# Arquitectura

Este proyecto utiliza un diseño modular en capas, con desacoplamiento completo entre el motor de build central y los puntos de entrada de la plataforma. CLI, BuildServer, DesktopApp y LinuxGateway comparten la misma lógica central — las diferencias residen únicamente en la capa de entrada y el método de interacción.

## Responsabilidades de directorios

La herramienta se organiza en los siguientes directorios por responsabilidad:

- `Cli/`: Punto de entrada de comandos, parsing de argumentos de línea de comandos, mapeo de comandos abreviados (`ShortcutCommands`).
- `ConsoleUi/`: UI de consola interactiva, incluyendo el asistente de inicialización, el editor de configuración y las indicaciones de entrada.
- `Configuration/`: Modelos de configuración, lectura/escritura de archivos de configuración, selección de archivos de configuración, resolución de rutas, configuraciones de ejemplo. Soporta configuraciones de plataforma `ios`, `android` y `tiktok`.
- `Workflow/`: Orquestación del pipeline de build, contexto de ejecución, actualización de configuración en tiempo de ejecución, snapshots de configuración.
- `Services/`: Capacidades de negocio compartidas multiplataforma, incluyendo sincronización Git, verificaciones de entorno, preparación de directorios, validación de proyecto Unity y validación de seguridad de rutas.
- `Modules/Common/`: Capacidades compartidas de módulos de plataforma, incluyendo la interfaz de Pipeline de plataforma, la construcción de argumentos de comandos Unity, el diagnóstico de logs Unity y la lectura de metadatos Unity.
- `Modules/Ios/`: Capacidades de build específicas de iOS, incluyendo exportación de proyecto Xcode de Unity, localización de project/workspace de Xcode, `xcodebuild archive/export`.
- `Modules/Android/`: Capacidades de build específicas de Android, incluyendo builds APK/AAB de Unity, subida vía API de Google Play Publishing; el subdirectorio `GooglePlay/` maneja los detalles de HTTP API, OAuth y Service Account.
- `Modules/Tiktok/`: Capacidades específicas de TikTok Mini-Game, incluyendo el pipeline de build WebGL (`TiktokBuildPipeline`), el servicio de build (`TiktokBuildService`) y la subida vía API de TikTok Open Platform (`TiktokUploadService`). Completamente independiente de iOS/Android — no afecta los flujos existentes.
- `Infrastructure/`: Infraestructura común, incluyendo logging (`BuildLogger`), ejecución de procesos (`ProcessRunner`), herramientas de ruta (`PathTools`), perímetros de seguridad de ruta (`PathSafety`) y enmascaramiento de datos sensibles. Estas capacidades son compartidas por CLI, BuildServer y DesktopApp.
- `UnityBuildScripts/Ios/`: Script de build de Unity Editor iOS para copiar en `Assets/Editor` del proyecto Unity.
- `UnityBuildScripts/Android/`: Script de build de Unity Editor Android para copiar en `Assets/Editor` del proyecto Unity.
- `BuildServer/`: Plataforma web de build, incluyendo API (`ApiRoutes`), frontend integrado (`wwwroot/`), worker en segundo plano (`BuildWorkerService`), entrada MCP/Agent (`McpEndpoint`), API de nodo Gateway (`GatewayEndpoint`), notificaciones por email (`EmailNotificationService`), gestión de almacenamiento (`StorageCleanupService`), escaneo de artefactos (`ArtifactScanner`), mantenimiento (`MaintenanceService`), conexión inversa (`Reverse/`) y persistencia JSON (`Persistence/`).
- `LinuxGateway/`: Entrada unificada multi-dispositivo, incluyendo API (`ApiRoutes`), frontend integrado (`wwwroot/`), cliente de pasarela de nodo (`NodeGatewayClient`), actualización de nodos (`NodeRefreshService`), actualización de jobs (`JobRefreshService`), gestión de conexión inversa (`Reverse/`), actualización en línea (`SelfUpdateService`) y persistencia JSON (`Persistence/`).
- `DesktopApp/`: Cliente de escritorio Avalonia UI 11, incluyendo Views (14 páginas), ViewModels (15 view models), Services (`BuildRunner` / `ProfileStore` / `ServerSyncService`), Controls (controles personalizados) y Styles (recursos de estilo). Referencia el proyecto principal vía `InternalsVisibleTo` + `Compile Remove` para reutilizar toda la lógica central.
- `deploy/`: Plantillas de despliegue de producción, como plist `launchd` de macOS y archivos de despliegue Docker.

## Principios de diseño clave

### Orquestación del pipeline separada de las capacidades de plataforma

`AutomationWorkflow` solo orquesta los pasos — no maneja directamente los detalles de Git, Unity, Xcode, Google Play o TikTok. Al añadir capacidades de plataforma, deben colocarse en el directorio `Modules/<Platform>/` correspondiente y ser llamadas por el workflow; las capacidades multiplataforma van en `Services/`. Actualmente se soportan tres Pipelines de plataforma:

- `IosBuildPipeline` — Git → Unity → Xcode archive/export → subida ASC
- `AndroidBuildPipeline` — Git → Unity → APK/AAB → subida a Google Play
- `TiktokBuildPipeline` — Git → Unity → WebGL → subida a TikTok Open Platform

### Editor de configuración basado en campos

El editor de configuración usa una lista de descriptores de campos para dirigir el menú y la lógica de modificación. Al añadir campos de configuración, añada primero una entrada a la lista de campos de `ConfigEditor`, evitando la dispersión de la visualización del menú y la lógica de modificación switch-case.

### Fundamentos de seguridad

Al conectar a backends web, workers o MCP/Agent, todos los puntos de entrada deben reutilizar las capacidades preexistentes ya implementadas en el CLI:

- `PathSafetyValidator`: Valida que el workspace, los directorios de repositorio, los proyectos Unity, los artefactos, los logs, las salidas de Xcode y archive/export están todos dentro de los directorios raíz permitidos.
- `GitRepositoryPolicyValidator`: Valida el formato de URL de Git y la lista blanca `allowedRepositoryUrls`.
- `BuildConfigSnapshotWriter`: Genera `Logs/build-config-snapshot.json` en cada ejecución real, registrando el snapshot de configuración, las rutas resueltas y los argumentos del CLI.
- `SensitiveText`: Enmascara uniformemente tokens/contraseñas comunes en logs, comandos, stdout/stderr y snapshots de configuración.

Estas capacidades no deben limitarse a la capa Web/API. El Worker también debe invocarlas antes de ejecutar builds, para evitar eludir los puntos de entrada y disparar configuraciones peligrosas directamente.

## Arquitectura BuildServer

BuildServer es el punto de entrada Web/Agent para el CLI, con el siguiente diseño:

### Cola serie

El diseño de máquina única, worker único y cola serie es intencional: Unity, Xcode, Gradle, los certificados de firma y los directorios de caché generalmente no toleran la contención concurrente en la misma máquina. La escalabilidad multi-máquina es gestionada por LinuxGateway.

### Capa de servicios

| Servicio | Archivo | Responsabilidad |
|------|------|------|
| Cola de tareas | `BuildQueueService.cs` | Gestiona el enqueue, dequeue y las transiciones de estado de las tareas de build |
| Worker en segundo plano | `BuildWorkerService.cs` | Consume la cola en serie, invoca el CLI para builds |
| Notificaciones por email | `EmailNotificationService.cs` | Envía notificaciones por email de éxito/fallo tras los builds |
| Escáner de artefactos | `ArtifactScanner.cs` | Escanea los directorios de artefactos de tareas, genera listas de artefactos |
| Lector de logs | `LogFileReader.cs` | Lee y hace tail de los logs de tareas |
| Limpieza de almacenamiento | `StorageCleanupService.cs` | Limpieza manual y automática de artefactos históricos |
| Mantenimiento | `MaintenanceService.cs` | Auto-limpieza por RetentionDays/MaxArtifactBytes |
| Localizador automático | `AutomationToolLocator.cs` | Localiza el ejecutable del CLI AutomationUnityBuildIOS |

### Conexión inversa

El directorio `BuildServer/Reverse/` implementa la capacidad de BuildServer de conectarse proactivamente a LinuxGateway, permitiendo que los nodos detrás de NAT/intranet sean planificados por LinuxGateway sin exposición pública.

## Arquitectura LinuxGateway

LinuxGateway no ejecuta Unity, no almacena proyectos Unity y no contiene certificados de Apple. Solo:

1. Proporciona login web y gestión de dispositivos.
2. Registra nodos (conexión directa o inversa).
3. Reenvía tareas al BuildServer de cada nodo.
4. Proxya logs y artefactos.

### Capa de servicios

| Servicio | Archivo | Responsabilidad |
|------|------|------|
| Cliente de pasarela de nodo | `NodeGatewayClient.cs` | Llama a los endpoints `/api/gateway/*` del BuildServer del nodo |
| Actualización de nodos | `NodeRefreshService.cs` | Actualiza periódicamente el estado de los nodos y la sincronización de proyectos/configuraciones |
| Actualización de jobs | `JobRefreshService.cs` | Actualiza periódicamente el estado, logs y artefactos de tareas remotas |
| Actualización en línea | `SelfUpdateService.cs` | Verifica y descarga paquetes de actualización desde Gitee/GitHub Releases |

### Conexión inversa

El directorio `LinuxGateway/Reverse/` gestiona la generación de Enrollment Tokens para conexiones iniciadas por BuildServer, el registro de nodos y el mantenimiento de conexiones largas WebSocket.

### Actualización en línea

`SelfUpdateService` soporta:
- Detección de doble fuente (consultas paralelas de última versión Gitee + GitHub).
- Descarga de paquetes de actualización tar.gz.
- Generación de un script `apply-update.sh` para completar respaldo + reemplazo + reinicio.
- No se requiere .NET SDK en el servidor — solo se descargan binarios precompilados.

## Arquitectura DesktopApp

DesktopApp usa Avalonia UI 11 + .NET 8 y reutiliza toda la lógica central del proyecto principal vía referencia de proyecto:

- **InternalsVisibleTo** + **Compile Remove**: El csproj del proyecto principal añade declaraciones para permitir que DesktopApp acceda a miembros internal mientras excluye archivos de punto de entrada como Program.cs.
- **ProfileStore**: Gestiona uniformemente la persistencia de cuatro tipos de plantillas de configuración (proyecto/Unity/firma/certificado), almacenadas en el directorio `profiles/`.
- **ServerSyncService**: Se conecta a la BuildServer REST API vía HttpClient para sincronización bidireccional de plantillas y archivos de configuración.
- **BuildRunner**: Envuelve la invocación del CLI, proporcionando salida de logs en tiempo real y progreso del build.
- **AvaloniaUseCompiledBindingsByDefault=false**: Usa bindings en tiempo de ejecución, evitando la necesidad de declarar x:DataType en cada archivo .axaml.

Ejecute `scripts/verify.ps1` para verificación de regresión básica: compilación, entrada de ayuda, dry-run, apertura-cierre del editor de configuración.
