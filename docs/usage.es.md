# Guía de uso

Este documento cubre todas las rutas de uso de AutomationUnityBuildIOS: CLI local, builds iOS, builds Android, builds TikTok Mini-Game, subidas a stores, cliente de escritorio DesktopApp, plataforma web BuildServer, notificaciones por email, gestión de almacenamiento, gestión de plantillas, entrada MCP/Agent y planificación multi-nodo LinuxGateway.

Si es nuevo, recomendamos seguir este orden:

1. Prepare su entorno de build Mac/Windows.
2. Copie los scripts de build de Unity en su proyecto Unity.
3. Genere una configuración y haga un dry-run en Mac con el CLI.
4. Haga un build real.
5. Despliegue BuildServer cuando su equipo necesite un punto de entrada web.
6. Despliegue LinuxGateway cuando varias máquinas de build necesiten un punto de entrada unificado.

---

## Selección de modo

| Escenario | Modo recomendado | Notas |
|------|----------|------|
| Build de paquetes iOS en su propio Mac | CLI | Componentes mínimos, ejecutar `./AutomationUnityBuildIOS 06` |
| iOS + Android automatizados | CLI o BuildServer | CLI para individual, BuildServer para equipos |
| Build y subida WebGL TikTok Mini-Game | CLI | Usar atajo `12` para generar configuración TikTok |
| Gestión de configuración offline y builds en Windows | DesktopApp | Cliente de escritorio nativo, editor de configuración completo, ejecución de builds, exploración de artefactos |
| QA/ops necesita build por clic | BuildServer | Login en navegador, envío de tareas, visualización de logs, descarga de artefactos |
| Múltiples máquinas de build Mac/Windows | LinuxGateway + BuildServer | LinuxGateway como entrada unificada; los builds se ejecutan en el BuildServer de cada nodo |
| Nodos detrás de NAT/intranet, inalcanzables externamente | LinuxGateway conexión inversa | Los nodos se conectan a LinuxGateway, sin IP pública ni mapeo de puertos |
| AI Agent participa en el proceso de build | BuildServer MCP | Agent por defecto hace dry-run; los builds reales requieren autorización |

---

## Configuración del entorno

### Máquina de desarrollo

Para construir y publicar esta herramienta se necesita:

- .NET 8 SDK.
- Windows, macOS o Linux pueden compilar este proyecto.
- Si usa Visual Studio, se recomienda VS 2022 o superior.

Verificación básica:

```powershell
dotnet --version
dotnet build .\AutomationUnityBuildIOS.sln
```

### Máquina de build iOS

El build final de iOS debe ejecutarse en macOS, ya que Unity iOS Build Support y Xcode solo están disponibles en Mac.

Requisitos de Mac:

- Xcode, abierto al menos una vez para aceptar la licencia e instalar componentes.
- Unity Hub, la versión correspondiente de Unity Editor y el módulo iOS Build Support.
- Git CLI, con el Mac pudiendo acceder a su repositorio Unity. Se recomienda configurar clave SSH.
- Cuenta Apple Developer, certificados, perfiles de provisioning o firma automática de Xcode.
- Si no usa un paquete de publicación self-contained, .NET 8 SDK también debe estar instalado en el Mac.

Comandos de verificación:

```bash
git --version
xcodebuild -version
/Applications/Unity/Hub/Editor/<UnityVersion>/Unity.app/Contents/MacOS/Unity -version
```

### Máquina de build Android

Los builds de Android pueden ejecutarse en macOS o Windows.

Requisitos:

- Unity Hub, la versión correspondiente de Unity Editor y Android Build Support.
- Android SDK, NDK, OpenJDK incluidos con Unity, o su propia cadena de herramientas Android.
- Un keystore de Android para firmar paquetes release.
- Un JSON de Service Account de Google Play Console con permisos de publicación para la app objetivo, si se sube a Google Play.

---

## Preparación del proyecto Unity

Esta herramienta invoca scripts de Unity Editor vía `-executeMethod`, por lo que su repositorio de juego Unity debe contener los scripts de build proporcionados por este proyecto.

iOS:

```text
UnityBuildScripts/Ios/BuildIOS.cs
```

Copiar al proyecto Unity:

```text
Assets/Editor/BuildIOS.cs
```

Método proporcionado:

```text
BuildAutomation.IOSBuilder.Build
```

Android:

```text
UnityBuildScripts/Android/BuildAndroid.cs
```

Copiar al proyecto Unity:

```text
Assets/Editor/BuildAndroid.cs
```

Método proporcionado:

```text
BuildAutomation.AndroidBuilder.Build
```

Tras actualizar AutomationUnityBuildIOS, si estos scripts han cambiado, sincronícelos con su repositorio de juego Unity.

---

## Inicio rápido CLI local

### Publicación del CLI Mac desde una máquina de desarrollo

Mac Apple Silicon:

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-arm64
```

Mac Intel:

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-x64
```

La salida publicada estará en:

```text
publish/osx-arm64
publish/osx-x64
```

Copie todo el directorio a su Mac, por ejemplo:

```text
~/Downloads/publish_m1
```

### Primera ejecución en Mac

Si macOS advierte sobre un desarrollador no identificado o software no verificado, ejecute lo siguiente en el directorio de publicación:

```bash
cd ~/Downloads/publish_m1
xattr -cr .
chmod +x ./AutomationUnityBuildIOS
codesign --force --deep --sign - ./AutomationUnityBuildIOS
./AutomationUnityBuildIOS 00
```

`00` muestra la ayuda y la tabla de comandos abreviados.

### Creación de configuración

Asistente de configuración iOS interactivo:

```bash
./AutomationUnityBuildIOS 01
```

Comando completo equivalente:

```bash
./AutomationUnityBuildIOS init-config
```

Generar una plantilla iOS vacía:

```bash
./AutomationUnityBuildIOS init-config --config build-ios.json --template
```

Generar una plantilla Android vacía:

```bash
./AutomationUnityBuildIOS 11
```

Comando completo equivalente:

```bash
./AutomationUnityBuildIOS init-config --config build-android.json --template --platform android
```

Se recomienda almacenar las configuraciones de producción bajo `configs/`, por ejemplo:

```text
configs/build-ios.dev.json
configs/build-ios.testflight.json
configs/build-android.internal.json
```

### Verificación de entorno

Seleccionar una configuración y verificar el entorno:

```bash
./AutomationUnityBuildIOS 04
```

Especificar una configuración:

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

Al depurar configuraciones o hacer dry-runs en Windows, añada:

```bash
--allow-non-mac
```

Los builds de producción iOS deben seguir ejecutándose en macOS.

### Vista previa de comandos

Vista previa del pipeline sin ejecución:

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
```

Comando completo equivalente:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --dry-run --verbose --allow-non-mac
```

### Build real

Seleccionar una configuración existente y ejecutar el pipeline completo:

```bash
./AutomationUnityBuildIOS 06
```

Especificar una configuración:

```bash
./AutomationUnityBuildIOS 06 --config configs/build-ios.dev.json
```

Comando completo:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json
```

### Flags de omisión comunes

| Flag | Efecto |
|------|------|
| `--skip-git` | Omitir pull/reset de Git, usar el proyecto existente en el workspace |
| `--skip-unity` | Omitir exportación de Unity o build de Android |
| `--skip-xcode` | Omitir Xcode archive/export (solo iOS; ignorado para Android) |
| `--dry-run` | Imprimir comandos sin ejecutar builds ni subidas |
| `--verbose` | Salida de rutas y comandos más detallada |
| `--allow-non-mac` | Permitir dry-run de iOS o depuración de configuración en no-macOS |

### Tabla de comandos abreviados

| Código | Descripción |
|------|------|
| `00` | Mostrar ayuda y tabla de comandos abreviados |
| `01` | Asistente de configuración interactivo, genera un archivo de configuración listo para usar |
| `02` | Generar plantilla de configuración iOS vacía `build-ios.json` |
| `03` | Listar archivos de configuración existentes |
| `04` | Seleccionar una configuración y verificar el entorno |
| `05` | Seleccionar una configuración y vista previa del comando de build completo (dry-run) |
| `06` | Seleccionar una configuración y ejecutar el pipeline de build completo |
| `07` | Seleccionar una configuración y construir, omitiendo la sincronización Git |
| `08` | Seleccionar una configuración y construir, omitiendo la exportación Unity |
| `09` | Seleccionar una configuración y construir, omitiendo la compilación/exportación Xcode |
| `10` | Seleccionar una configuración y editar su contenido |
| `11` | Generar plantilla de configuración Android APK/AAB `build-android.json` |
| `12` | Generar plantilla de configuración TikTok Mini-Game `build-tiktok.json` |

Los comandos abreviados pueden ir seguidos de argumentos adicionales:

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
./AutomationUnityBuildIOS 06 --config configs/build-ios.release.json
./AutomationUnityBuildIOS 10 --config configs/build-android.internal.json
```

---

## Referencia de archivos de configuración

Los archivos de configuración son JSON. Ver `build-ios.sample.json` para iOS, `build-android.sample.json` para Android y `build-tiktok.sample.json` para TikTok.

### Campos comunes

| Campo | Descripción |
|------|------|
| `configName` | Nombre a mostrar de la configuración, se muestra en listas de selección |
| `buildPlatform` | `ios`, `android` o `tiktok` |
| `repositoryUrl` | URL de clonación de Git para el repositorio Unity, soporta HTTPS/SSH |
| `allowedRepositoryUrls` | Lista blanca de repositorios, recomendado para producción |
| `branch` | Rama de build |
| `workspaceRoot` | Directorio raíz del workspace de Git |
| `allowedWorkspaceRoots` | Directorios raíz de workspace permitidos, previene escape de rutas |
| `projectDirectoryName` | Nombre del directorio tras clonar el repositorio |
| `unityProjectRelativePath` | Ruta al proyecto Unity relativa a la raíz del repositorio; usar `.` si la raíz del repositorio es el proyecto Unity |
| `unityVersion` | Versión instalada de Unity Hub, usada para deducir la ruta del ejecutable Unity |
| `unityExecutablePath` | Ruta completa al ejecutable de Unity; tiene prioridad sobre `unityVersion` |
| `unityBuildMethod` | Nombre del método estático de Unity Editor |
| `artifactsRoot` | Directorio raíz de artefactos de build |
| `allowedArtifactsRoots` | Directorios raíz de artefactos permitidos |
| `productName` | Unity Product Name |
| `bundleIdentifier` | iOS Bundle ID o Android Package Name |
| `bundleVersion` | Número de versión |
| `syncBundleVersionFromUnity` | Sincronizar versión desde Unity PlayerSettings |
| `buildNumber` | iOS Build Number o Android versionCode |
| `autoIncrementBuildNumber` | Auto-incrementar el build number tras un build exitoso |
| `saveConfigSnapshot` | Guardar un snapshot de configuración en el directorio de logs |

Los tres valores más comúnmente mal configurados:

```text
repositoryUrl: Usar la URL de clonación de git, no el título de la página web.
unityProjectRelativePath: Normalmente ".", no build, Builds o XcodeProject.
teamId: iOS usa el Apple Developer Team ID de 10 caracteres, no el nombre de la empresa.
```

### Campos iOS

| Campo | Descripción |
|------|------|
| `scheme` | Por defecto `Unity-iPhone` |
| `configuration` | Por defecto `Release` |
| `exportMethod` | `development`, `ad-hoc`, `app-store`, etc. (método de exportación Xcode) |
| `teamId` | Apple Developer Team ID, debe ser 10 caracteres alfanuméricos |
| `signingStyle` | `automatic` o `manual` |
| `iosDeploymentTarget` | Versión mínima de iOS, por ejemplo `13.0` |
| `allowProvisioningUpdates` | Permitir a Xcode gestionar actualizaciones de firma automáticamente |
| `generateExportOptionsPlist` | Generar automáticamente `ExportOptions.plist` |
| `copyArchiveToOrganizer` | Copiar `.xcarchive` a Xcode Organizer |
| `appStoreConnectUploadEnabled` | Subir automáticamente a App Store Connect/TestFlight |

### Campos Android

| Campo | Descripción |
|------|------|
| `androidBuildFormat` | `apk`, `aab` o `both` |
| `androidOutputDirectory` | Directorio de salida Android, auto-generado si está vacío |
| `apkOutputPath` | Ruta de salida APK, auto-generada si está vacía |
| `aabOutputPath` | Ruta de salida AAB, auto-generada si está vacía |
| `androidMinSdkVersion` | Opcional, sobrescribe Min SDK |
| `androidTargetSdkVersion` | Opcional, sobrescribe Target SDK |
| `androidKeystoreName` | Ruta o nombre del keystore |
| `androidKeystorePass` | Contraseña del keystore |
| `androidKeyaliasName` | Key alias |
| `androidKeyaliasPass` | Contraseña del key alias |
| `googlePlayUploadEnabled` | Subir a Google Play |
| `googlePlayTrack` | `internal`, `alpha`, `beta`, `production` |
| `googlePlayReleaseStatus` | `draft`, `inProgress`, `halted`, `completed` |
| `googlePlayUploadArtifact` | Subir `apk`, `aab` o `both` |

Nunca haga commit de certificados, claves privadas o tokens de larga duración en el repositorio. Cuando las configuraciones necesiten referenciar secretos, prefiera rutas locales en la máquina de build y proteja los permisos de archivo.

### Campos TikTok

| Campo | Descripción |
|------|------|
| `tiktokAppId` | TikTok Open Platform App ID |
| `tiktokAccessToken` | TikTok Open Platform Access Token |
| `tiktokGameName` | Nombre del TikTok Mini-Game |
| `tiktokWebglOutputDirectory` | Directorio de salida WebGL, auto-generado si está vacío |
| `tiktokUploadEnabled` | Subir automáticamente a TikTok Open Platform |
| `tiktokApiEndpoint` | URL de la API de TikTok Open Platform, por defecto `https://open-api.tiktokglobalshop.com` |

---

## Build iOS

### Pipeline básico

El pipeline completo de iOS:

1. Validación de perímetros de seguridad de configuración y política de repositorio Git.
2. Verificación de `git`, Unity, `xcodebuild`.
3. Creación del directorio de ejecución y directorio de logs.
4. Escritura de `build-config-snapshot.json`.
5. Pull o actualización del repositorio Unity.
6. Invocación de Unity BatchMode para exportar el proyecto Xcode de iOS.
7. Ejecución de `xcodebuild archive`.
8. Ejecución de `xcodebuild -exportArchive`.
9. Copia opcional de `.xcarchive` a Xcode Organizer.
10. Subida opcional a App Store Connect/TestFlight.

### Subida a App Store Connect / TestFlight

Habilitar la subida automática requiere `exportMethod` establecido en `app-store` y una App Store Connect API Key configurada.

Ejemplo:

```json
{
  "exportMethod": "app-store",
  "appStoreConnectUploadEnabled": true,
  "appStoreConnectApiKeyPath": "~/Secrets/AuthKey_XXXXXXXXXX.p8",
  "appStoreConnectApiKeyId": "XXXXXXXXXX",
  "appStoreConnectApiIssuerId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

Notas:

- El archivo `.p8` debe existir localmente en la máquina de build Mac.
- Key ID e Issuer ID provienen de la página de App Store Connect API Key.
- Tras una subida exitosa, el build entra en la cola de procesamiento de App Store Connect/TestFlight.
- El envío a revisión o la publicación en producción sigue las políticas de versión de App Store Connect.

### Métodos de depuración iOS comunes

Sincronizar solo Git y Unity, omitir Xcode:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-xcode
```

Omitir Unity, reutilizar el proyecto Xcode existente para archive/export:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-unity
```

Verificar solo configuración y entorno:

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

---

## Build Android

### Pipeline básico

El pipeline completo de Android:

1. Validación de perímetros de seguridad de configuración y política de repositorio Git.
2. Verificación de `git` y Unity.
3. Creación del directorio de ejecución y directorio de logs.
4. Escritura de `build-config-snapshot.json`.
5. Pull o actualización del repositorio Unity.
6. Invocación de Unity BatchMode para construir APK/AAB.
7. Subida opcional a Google Play.

Android no requiere Xcode; `--skip-xcode` se ignora.

### Build APK/AAB

Configuración:

```json
{
  "buildPlatform": "android",
  "unityBuildMethod": "BuildAutomation.AndroidBuilder.Build",
  "androidBuildFormat": "both"
}
```

Opciones de `androidBuildFormat`:

| Valor | Resultado |
|-------|--------|
| `apk` | Solo APK |
| `aab` | Solo AAB |
| `both` | APK y AAB |

### Subida a Google Play

Debe crear un Service Account en Google Play Console y conceder permisos de publicación para la app objetivo.

Ejemplo:

```json
{
  "googlePlayUploadEnabled": true,
  "googlePlayPackageName": "com.company.game",
  "googlePlayServiceAccountJsonPath": "~/Secrets/google-play-service-account.json",
  "googlePlayTrack": "internal",
  "googlePlayReleaseStatus": "draft",
  "googlePlayUploadArtifact": "aab",
  "googlePlayChangesNotSentForReview": false,
  "googlePlayUserFraction": null
}
```

Recomendado: primero dry-run:

```bash
./AutomationUnityBuildIOS run --config configs/build-android.internal.json --dry-run --verbose
```

Verifique rutas, nombre del paquete, versión y artefacto de subida antes de ejecutar el build real.

---

## Build TikTok Mini-Game

### Pipeline básico

El pipeline de build TikTok Mini-Game:

1. Validación de perímetros de seguridad de configuración y política de repositorio Git.
2. Verificación de `git` y Unity.
3. Creación del directorio de ejecución y directorio de logs.
4. Escritura de `build-config-snapshot.json`.
5. Pull o actualización del repositorio Unity.
6. Invocación de Unity BatchMode para construir WebGL.
7. Subida opcional a TikTok Open Platform.

Los builds de TikTok no requieren Xcode; `--skip-xcode` se ignora.

### Generación de configuración

```bash
./AutomationUnityBuildIOS 12
```

Comando completo equivalente:

```bash
./AutomationUnityBuildIOS init-config --config build-tiktok.json --template --platform tiktok
```

### Ejemplo de configuración

```json
{
  "buildPlatform": "tiktok",
  "unityBuildMethod": "BuildAutomation.TiktokBuilder.Build",
  "tiktokAppId": "your-app-id",
  "tiktokAccessToken": "your-access-token",
  "tiktokGameName": "Your Game",
  "tiktokUploadEnabled": true
}
```

### Build real

```bash
./AutomationUnityBuildIOS run --config configs/build-tiktok.release.json
```

El código relacionado con TikTok se encuentra en `Modules/Tiktok/`, completamente independiente de iOS/Android y sin afectar los flujos de build existentes.

---

## Cliente de escritorio

DesktopApp es un cliente de escritorio Windows nativo basado en Avalonia UI 11 + .NET 8, que reutiliza toda la lógica central del proyecto principal (AutomationWorkflow / BuildConfig / ConfigFileSelector / SampleFiles). Integra las capacidades de CLI, BuildServer y gestión de plantillas en una sola aplicación de escritorio con soporte offline completo.

### Páginas de funcionalidades

| Página | Funcionalidades |
|------|----------|
| **Gestión de configuración** | Edición completa de campos iOS/Android/TikTok, auto-sincronización del nombre de archivo de configuración, relleno de plantilla con un clic |
| **Tarea de build** | Tail de logs en tiempo real, temporizador, borrado de logs, auto-scroll |
| **Verificación de entorno** | Verificar Unity, Git, Xcode y otras dependencias |
| **Explorador de artefactos** | Lista de archivos, selección, doble clic para abrir, vista previa |
| **Gestión de almacenamiento** | Eliminación masiva con casillas, eliminación simple, seleccionar todo, vista general |
| **Notificaciones por email** | Configuración SMTP (incluyendo 465 SSL implícito), lista de contactos, plantillas |
| **Perfil de proyecto** | Plantilla ProjectProfile, gestiona repositorio/directorios de workspace |
| **Perfil Unity** | Plantilla UnityProfile, gestiona versión/ruta Unity/BuildMethod/ProductName/BundleID |
| **Perfil de firma** | Plantilla SigningProfile, gestiona iOS TeamID/ExportMethod/SigningStyle/Android Keystore |
| **Perfil de certificado** | Plantilla CertificateProfile, gestiona ASC API Key/Google Play/TikTok Token |
| **Sincronización con servidor** | Conexión a BuildServer REST API, sincronización bidireccional de plantillas y archivos de configuración |
| **Gestor BuildServer** | Detección automática o selección manual de la ruta BuildServer.exe, inicio/parada con un clic, health check |
| **Gestión de datos** | Exportar tipos de datos a JSON, importar JSON con fusión deduplicada por ID |
| **Ayuda** | Guía de uso y referencia de comandos abreviados |

### Publicación de DesktopApp

```powershell
dotnet publish DesktopApp/DesktopApp.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -o DesktopApp/bin/publish-vN
```

Si el exe anterior sigue en ejecución, obtendrá una `UnauthorizedAccessException`. Deténgalo primero:

```powershell
Stop-Process -Name DesktopApp -Force
```

Luego publique en un nuevo directorio. La salida de archivo único es de aproximadamente 89 MB.

También puede usar el script de publicación:

```powershell
.\scripts\publish-desktop.ps1
```

### Gestión de plantillas

DesktopApp proporciona cuatro tipos de plantillas de configuración, almacenadas en el directorio `profiles/`:

| Plantilla | Archivo | Propósito |
|------|------|------|
| Perfil de proyecto | `projects.json` | URL del repositorio, directorios de workspace y artefactos, etc. |
| Perfil Unity | `unity-profiles.json` | Versión Unity, ruta, BuildMethod, ProductName, BundleID |
| Perfil de firma | `signing-profiles.json` | iOS TeamID, ExportMethod, SigningStyle, Android Keystore |
| Perfil de certificado | `certificates.json` | ASC API Key, Google Play Service Account, TikTok Token |

En la parte superior del formulario de edición de la página de gestión de configuración, hay cuatro selectores de plantillas. Elija uno de cada uno y haga clic en «Aplicar» para rellenar los campos correspondientes con un clic. Tras aplicar una plantilla, las secciones de campos rellenados se ocultan automáticamente para reducir el desorden.

### Sincronización con servidor

DesktopApp puede conectarse a la BuildServer REST API para sincronización bidireccional:

- **Plantillas de proyecto**: Pull / push
- **Plantillas de certificado**: Pull / push
- **Archivos de configuración**: Explorar lista de configuraciones del servidor + descargar al directorio `configs/` local

La información de conexión se persiste en `profiles/server-settings.json`.

La página de gestión de configuración también proporciona un botón «Importar archivo de configuración» para importar JSON desde cualquier ruta local a `configs/`.

---

## Notificaciones por email

BuildServer soporta notificaciones por email automáticas tras la finalización de tareas de build, cubriendo tanto éxito como fallo.

### Configuración

Configurar en el backend web de BuildServer o en la página de notificaciones por email de DesktopApp:

| Campo | Descripción |
|------|------|
| Servidor SMTP | por ejemplo `smtp.gmail.com`, `smtp.qq.com` |
| Puerto SMTP | Comunes: 25 (texto plano), 465 (SSL implícito), 587 (STARTTLS) |
| Email del remitente | Dirección de email que envía las notificaciones |
| Contraseña del remitente | Código de autorización o contraseña de email |
| Habilitar SSL | El puerto 465 usa SSL implícito |
| Contactos de notificación | Lista de emails destinatarios, separados por comas o saltos de línea |
| Plantilla de email | Asunto y cuerpo de email personalizados |

### Disparadores de notificación

- **Build exitoso**: El email incluye rutas de artefactos, tiempo transcurrido y resumen de configuración.
- **Build fallido**: El email incluye el paso fallido, resumen de errores y ruta de log para rápida resolución de problemas.

El servicio de notificaciones por email está implementado en `BuildServer/Services/EmailNotificationService.cs`.

---

## Gestión de almacenamiento

A medida que las tareas de build se acumulan, los artefactos consumen gradualmente espacio en disco. BuildServer proporciona dos mecanismos de gestión de almacenamiento:

### Limpieza automática

`MaintenanceService` limpia automáticamente las tareas y artefactos completados basándose en los `RetentionDays` y `MaxArtifactBytes` configurados.

### Limpieza manual

En el backend web o en la página de gestión de almacenamiento de DesktopApp puede:

- Ver la vista general de almacenamiento (espacio total, usado, número de tareas, distribución de tamaño de artefactos).
- Seleccionar múltiples tareas históricas para eliminación masiva.
- Eliminar artefactos de una sola tarea.
- Seleccionar todo para borrar todos los artefactos históricos.

El servicio de limpieza de almacenamiento está implementado en `BuildServer/Services/StorageCleanupService.cs`.

---

## Logs y artefactos

Cada ejecución crea un directorio independiente bajo `artifactsRoot`, por ejemplo:

```text
~/UnityBuildArtifacts/YourUnityGame/20260625-153000/
```

Contenidos comunes:

| Archivo o directorio | Descripción |
|------------|------|
| `Logs/automation.log` | Log principal del pipeline, incluye pasos, comandos, tiempo transcurrido y errores |
| `Logs/unity-editor.log` | Log de build del propio Unity Editor |
| `Logs/unity-process.log` | stdout/stderr capturado del proceso Unity |
| `Logs/build-config-snapshot.json` | Snapshot de configuración para esta ejecución, con enmascaramiento básico |
| `Logs/xcode-archive.log` | Log de archive iOS |
| `Logs/xcode-export.log` | Log de export iOS |
| `Logs/xcode-upload.log` | Log de subida a App Store Connect |
| `.xcarchive` | Artefacto de archivo iOS |
| Directorio de exportación `.ipa` | Artefacto de exportación iOS |
| `.apk` / `.aab` | Artefactos de build Android |

Orden de resolución de problemas:

1. Primero verificar el final de `automation.log` para el paso fallido.
2. Si la etapa Unity falló, verificar `unity-editor.log`.
3. Si la etapa Xcode iOS falló, verificar `xcode-archive.log` o `xcode-export.log`.
4. Si la subida a store falló, verificar `xcode-upload.log` o el error de subida a Google Play en el log principal.

El sistema de logging aplica enmascaramiento básico a información sensible común, como credenciales/tokens en URLs, tokens `Bearer` y valores para claves como `password/token/secret/apiKey`.

---

## Plataforma web BuildServer

BuildServer es el punto de entrada Web/Agent para el CLI. Proporciona:

- Login web.
- Gestión de proyectos.
- Gestión de configuración.
- Cola de tareas de build.
- Logs en tiempo real.
- Descarga de artefactos.
- Permisos de usuario.
- Logs de auditoría.
- Herramientas MCP/Agent.
- API de nodo LinuxGateway.

La primera versión usa una cola serie de máquina única y worker único para evitar la contención concurrente entre Unity, Xcode, Gradle, entornos de firma y directorios de caché.

### Inicio local

Depuración en Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

Depuración en macOS/Linux:

```bash
./scripts/run-build-server.sh
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

Si `BUILD_SERVER_AGENT_TOKEN` no está establecido, se genera un token MCP Agent por defecto en el primer inicio:

```text
<DataRoot>/initial-agent-token.txt
```

### Variables de entorno de producción

Recomendado para producción:

```bash
export BUILD_SERVER_ADMIN_PASSWORD="strong-password"
export BUILD_SERVER_AGENT_TOKEN="strong-agent-token"
export BUILD_SERVER_PUBLIC_BASE_URL="https://mac-build.example.com"
export BUILD_SERVER_ALLOWED_ORIGINS="https://mac-build.example.com"
export BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS="/Users/build/UnityBuildWorkspace"
export BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS="/Users/build/UnityBuildArtifacts"
export BUILD_SERVER_ALLOWED_CONFIG_ROOTS="/Users/build/BuildServerData/configs"
export BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS="gitee.com,github.com"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"
```

Variables comunes:

| Variable | Descripción |
|------|------|
| `BUILD_SERVER_DATA_ROOT` | Directorio de datos, almacena usuarios, proyectos, configuraciones, tareas, JSON de auditoría |
| `BUILD_SERVER_ADMIN_PASSWORD` | Contraseña de administrador |
| `BUILD_SERVER_AGENT_TOKEN` | Token MCP Agent |
| `BUILD_SERVER_PUBLIC_BASE_URL` | URL pública |
| `BUILD_SERVER_ALLOWED_ORIGINS` | Origins web permitidos; recomendado detrás de un proxy inverso |
| `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS` | Directorios raíz de workspace permitidos |
| `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS` | Directorios raíz de artefactos permitidos |
| `BUILD_SERVER_ALLOWED_CONFIG_ROOTS` | Directorios raíz de archivos de configuración permitidos |
| `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` | Hosts Git permitidos para registro |
| `BUILD_SERVER_GATEWAY_TOKEN` | Token de API de nodo; auto-genera `initial-gateway-token.txt` en el primer inicio si está vacío |
| `BUILD_SERVER_NODE_PLATFORMS` | Capacidades del nodo actual, por ejemplo `ios,android` o `android` |

### Flujo de uso web

Tras el primer inicio de sesión en el backend:

1. Añadir un proyecto: nombre del proyecto, repositorio Git, rama por defecto, ramas permitidas, workspace y directorio de artefactos.
2. Añadir una configuración: seleccionar iOS o Android.
3. Las configuraciones pueden apuntar a un archivo JSON existente o ser generadas desde el formulario web.
4. Iniciar un build: seleccionar proyecto, configuración, rama y parámetros opcionales.
5. Ver el estado, logs en tiempo real y artefactos en la lista de tareas.

BuildServer genera un snapshot de configuración independiente para cada tarea e invoca el CLI:

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

### Publicación de BuildServer en Mac

Mac Apple Silicon:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

Mac Intel:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-x64
```

El directorio de publicación incluye tanto BuildServer como el CLI AutomationUnityBuildIOS. Para producción, usar:

```text
deploy/launchd/com.automationunity.buildserver.plist
```

Se recomienda designar un usuario macOS dedicado para ejecutar BuildServer, con Unity License, firma Xcode, certificados, perfiles de provisioning y claves SSH de Git todos configurados bajo ese usuario.

### MCP / Agent

Endpoint MCP:

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

Herramientas soportadas:

| Herramienta | Descripción |
|------|------|
| `list_projects` | Listar proyectos disponibles |
| `list_configs` | Listar configuraciones de build bajo un proyecto |
| `start_build` | Enviar una tarea de build iOS o Android |
| `start_ios_build` | Nombre heredado, las nuevas integraciones deben usar `start_build` |
| `get_build_status` | Consultar el estado de una tarea de build |
| `tail_build_log` | Leer las últimas líneas de log |
| `list_build_artifacts` | Listar artefactos de una tarea |

Por defecto, los Agents solo tienen permitido `dryRun=true`. Para permitir builds reales, habilitar `allowFullBuild` para el MCP Client correspondiente y recomendar autorizar solo proyectos específicos.

No poner Agent Tokens en parámetros de URL. Usar `X-Agent-Token` o `Authorization: Bearer`.

---

## Entrada multi-nodo LinuxGateway

LinuxGateway es adecuado para despliegue en un servidor Linux con un dominio público. No ejecuta Unity, no almacena proyectos Unity y no contiene certificados de Apple; solo gestiona login, registro de nodos, selección de nodos, reenvío de tareas y proxy de logs/artefactos.

Arquitectura típica:

```text
Usuarios externos
  -> LinuxGateway Web/API
      -> Mac BuildServer       iOS + Android
      -> Windows BuildServer   Android
```

Sin LinuxGateway, cada Mac/Windows BuildServer puede seguir utilizándose de forma independiente.

### Inicio de LinuxGateway

Desarrollo:

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Depuración en Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
```

Dirección por defecto:

```text
http://127.0.0.1:5090
```

Si `LINUX_GATEWAY_ADMIN_PASSWORD` no está establecido, se genera una contraseña inicial en el primer inicio:

```text
linuxgateway-data/initial-admin.txt
```

Recomendado para producción:

```bash
export LINUX_GATEWAY_ADMIN_PASSWORD="strong-password"
export LINUX_GATEWAY_PUBLIC_BASE_URL="https://build.example.com"
export LINUX_GATEWAY_ALLOWED_ORIGINS="https://build.example.com"
export LINUX_GATEWAY_DATA_ROOT="/opt/unity-build-gateway/data"
```

### Publicación de LinuxGateway en Linux

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-linux-gateway-linux.ps1
```

Salida por defecto:

```text
publish/linux-gateway
```

Copiar a Linux y ejecutar:

```bash
chmod +x ./LinuxGateway
./LinuxGateway --urls http://127.0.0.1:5090
```

Para acceso público, usar Nginx/Caddy para HTTPS y proxy inverso a `127.0.0.1:5090`.

### Modo 1: Conexión directa al nodo

La conexión directa es adecuada cuando LinuxGateway puede alcanzar el BuildServer Mac/Windows, por ejemplo vía VPN, intranet, túnel o HTTPS público.

Establecer antes de iniciar cada nodo BuildServer:

```bash
export BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"
```

Nodo Android Windows:

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

También puede no establecer manualmente `BUILD_SERVER_GATEWAY_TOKEN`. BuildServer lo auto-generará en el primer inicio y lo guardará en:

```text
<DataRoot>/initial-gateway-token.txt
```

BuildServer habilitará:

```text
/api/gateway/*
```

LinuxGateway llama al nodo con:

```text
Header: X-Gateway-Token: <BUILD_SERVER_GATEWAY_TOKEN>
```

Añadir un dispositivo en la interfaz web de LinuxGateway:

| Campo | Ejemplo |
|------|------|
| Nombre del dispositivo | `Mac Build` |
| URL BuildServer | `https://mac-build.example.com` |
| Gateway Token | El `BUILD_SERVER_GATEWAY_TOKEN` del nodo |
| Plataformas | Mac: `iOS + Android`, Windows: `Android` |

Tras guardar, actualizar el dispositivo para confirmar que los proyectos y configuraciones del nodo son visibles.

### Modo 2: Conexión inversa al nodo

La conexión inversa es adecuada cuando los nodos están detrás de NAT, redes domésticas o intranets corporativos donde LinuxGateway no puede acceder directamente a la dirección del nodo. En este caso, BuildServer inicia la conexión a LinuxGateway.

Generar un Enrollment Token en la interfaz web de LinuxGateway, luego completar la página de conexión Gateway en BuildServer:

```text
Gateway URL: https://build.example.com
Enrollment Token: <token>
```

También puede configurar vía variables de entorno para que BuildServer se conecte automáticamente al inicio:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

Tras conectar, LinuxGateway muestra el nodo de conexión inversa. Las credenciales del nodo se guardan en el directorio de datos de BuildServer; tras revocar un nodo, debe generar un nuevo Enrollment Token para volver a registrarlo.

La conexión inversa está implementada en `LinuxGateway/Reverse/` y `BuildServer/Reverse/`.

### Actualización en línea de LinuxGateway

LinuxGateway incluye `SelfUpdateService`, que puede verificar y descargar paquetes de actualización desde Gitee o GitHub Releases sin necesidad de .NET SDK en el servidor.

Verificar actualizaciones:

```text
GET /api/system/version
GET /api/system/update/check
```

Aplicar actualización (solo Admin):

```text
POST /api/system/update/apply
```

El proceso de actualización respalda automáticamente la versión actual, descarga un paquete de actualización tar.gz y genera un script `apply-update.sh` para completar el reemplazo y el reinicio.

Configuración:

| Variable | Descripción |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | Fuente de actualización: `gitee` o `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | Propietario del repositorio |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | Nombre del repositorio |

### Envío de builds vía LinuxGateway

1. Iniciar sesión en LinuxGateway.
2. Confirmar que el nodo está en línea en la página de dispositivos.
3. Actualizar el nodo para asegurar que los proyectos y configuraciones están sincronizados.
4. En la página de tareas de build, seleccionar dispositivo, proyecto, configuración y rama.
5. Enviar la tarea.
6. Ver el estado, logs y artefactos devueltos por el nodo remoto.

Las tareas de iOS solo pueden enviarse a nodos Mac que soporten `ios`; los nodos Windows generalmente solo son adecuados para Android APK/AAB.

---

## Recomendaciones de seguridad

- Establezca siempre contraseñas fuertes en producción; no dependa de los archivos de contraseñas iniciales a largo plazo.
- No ponga `BUILD_SERVER_AGENT_TOKEN`, `BUILD_SERVER_GATEWAY_TOKEN` ni Enrollment Tokens en URLs. Use headers o almacenamiento del lado del servidor.
- Los directorios de datos de LinuxGateway y BuildServer almacenan usuarios, tareas, credenciales de nodos o tokens — restrinja los permisos del sistema.
- Configure `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS`, `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS`, `BUILD_SERVER_ALLOWED_CONFIG_ROOTS` y `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` para BuildServer.
- Si un backend de nodo solo lo usa LinuxGateway, evite exponer el backend de administración regular al internet público.
- Los certificados iOS, perfiles de provisioning, archivos `.p8` de App Store Connect, keystores de Android y JSON de Service Account de Google Play solo deben almacenarse en directorios locales seguros en la máquina de build.
- Nunca haga commit de certificados, claves privadas o tokens de larga duración en Git.
- Al acceder a la interfaz web vía un proxy inverso, configure `PUBLIC_BASE_URL` y `ALLOWED_ORIGINS` para evitar el rechazo de peticiones cross-origin o el fallo de validación de origen.

---

## FAQ

| Problema | Resolución |
|------|------|
| El build de iOS en Windows indica que se requiere macOS | Los builds de producción iOS deben ejecutarse en Mac; Windows solo soporta `--dry-run --allow-non-mac` para depuración de configuración |
| Ejecutable de Unity no encontrado | Establezca `unityExecutablePath` o verifique que `unityVersion` coincide con una ruta instalada de Unity Hub |
| Fallo de Git pull | Haga un `git clone` manual en la máquina de build para verificar la clave SSH o las credenciales HTTPS |
| Fallo de validación de Team ID | `teamId` debe ser un Apple Developer Team ID de 10 caracteres, no un nombre de empresa |
| Fallo de subida a App Store Connect | Verifique `exportMethod=app-store`, existencia de la ruta `.p8`, Key ID e Issuer ID correctos |
| Error de Android versionCode | `buildNumber` debe ser un entero positivo |
| Fallo de subida a Google Play | Verifique la ruta del JSON de Service Account, permisos de la app, packageName, track y formato del artefacto de subida |
| Fallo de inicio de sesión en BuildServer | La cuenta es `admin`; copie solo el valor después de `admin password:` en `initial-admin.txt` |
| Operaciones de escritura web rechazadas | Verifique que `BUILD_SERVER_ALLOWED_ORIGINS` o `LINUX_GATEWAY_ALLOWED_ORIGINS` coincide con el dominio de acceso |
| Nodo LinuxGateway 401 | El Gateway Token es incorrecto o el nodo no ha habilitado `BUILD_SERVER_GATEWAY_TOKEN` |
| Timeout del nodo LinuxGateway | Verifique la dirección, puerto, firewall, túnel o proxy inverso del nodo |
| Fallo de descarga de artefacto | Confirme que la ruta del artefacto está dentro de los artifacts roots permitidos de BuildServer |

---

## Pruebas de regresión

Los desarrolladores pueden ejecutar:

```powershell
.\scripts\verify.ps1
```

Realiza:

- Compilación de la solución.
- Compilación del proyecto CLI.
- Compilación de BuildServer.
- Compilación de LinuxGateway.
- Entrada de ayuda `00`.
- Dry-run del ejemplo iOS.
- Dry-run del ejemplo Android.
- Apertura-cierre del editor de configuración.

El conjunto de pruebas cubre 256+ casos de test, abarcando el parsing de argumentos CLI, los modelos de configuración, la seguridad de rutas, las políticas Git, la construcción de comandos Unity, la API de Google Play, las configuraciones TikTok, las rutas de la API BuildServer, la comunicación de nodos LinuxGateway, la conexión inversa, las notificaciones por email y todos los demás módulos.

Ejecutar el conjunto completo de pruebas:

```powershell
dotnet test .\AutomationUnityBuildIOS.Tests\AutomationUnityBuildIOS.Tests.csproj
```

Verificación rápida de impacto en compilación:

```powershell
dotnet build .\AutomationUnityBuildIOS.sln
```
