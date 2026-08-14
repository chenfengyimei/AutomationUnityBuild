# Entrada multi-nodo LinuxGateway

`LinuxGateway` es un punto de entrada central opcional, adecuado para despliegue en un servidor Linux con un dominio público. No ejecuta Unity, no almacena proyectos Unity y no contiene certificados de Apple; solo gestiona login web, registro de nodos de build Mac/Windows, selección de nodos y reenvío de tareas al `BuildServer` de cada nodo.

LinuxGateway soporta dos modos de conexión de nodo: conexión directa (LinuxGateway accede proactivamente al nodo) y conexión inversa (el nodo se conecta proactivamente a LinuxGateway, adecuado para entornos NAT/intranet). Incluye una función de actualización en línea integrada que descarga paquetes de actualización desde Gitee/GitHub Releases, sin necesidad de .NET SDK en el servidor.

Sin LinuxGateway, las instancias de `BuildServer` Mac/Windows pueden seguir utilizándose de forma independiente para login, configuración y builds.

## Arquitectura

```text
Usuarios externos
  -> LinuxGateway Web/API
      -> Mac BuildServer /api/gateway/*    iOS + Android
      -> Windows BuildServer /api/gateway/* Android APK/AAB
```

Cada nodo Mac/Windows sigue ejecutando el `BuildServer` existente, con solo una API adicional protegida por token activada para las llamadas de LinuxGateway.

## Configuración de nodos Mac/Windows

Establecer antes de iniciar `BuildServer` en cada nodo:

```bash
export BUILD_SERVER_GATEWAY_TOKEN="token aleatorio fuerte para este nodo"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"   # Común para Mac
```

Nodo Android Windows:

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="token aleatorio fuerte para este nodo"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

Si `BUILD_SERVER_GATEWAY_TOKEN` se deja vacío, los endpoints `/api/gateway/*` del nodo no se activarán.

LinuxGateway debe poder alcanzar la dirección del nodo, por ejemplo:

```text
https://mac-build.example.com
https://win-build.example.com
```

Estas pueden ser direcciones de túnel, direcciones VPN/intranet o endpoints HTTPS públicos. Se recomienda HTTPS.

## Inicio de LinuxGateway

Desarrollo:

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Depuración en Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
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

## Publicación en Linux

Publicar Linux x64 desde Windows:

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

## Flujo de uso

1. Iniciar `BuildServer` en los nodos Mac/Windows y establecer `BUILD_SERVER_GATEWAY_TOKEN`.
2. Iniciar `LinuxGateway` en Linux.
3. Iniciar sesión en la interfaz web de LinuxGateway.
4. Añadir un dispositivo:
   - Nombre del dispositivo: por ejemplo `Mac Build`
   - URL BuildServer: por ejemplo `https://mac-build.example.com`
   - Gateway Token: el `BUILD_SERVER_GATEWAY_TOKEN` del nodo
   - Plataformas: Mac: `iOS + Android`, Windows: `Android`
5. Actualizar el dispositivo para confirmar que los proyectos y configuraciones del nodo son visibles.
6. Al enviar un build, seleccionar el dispositivo objetivo, el proyecto y la configuración.

## Notas de seguridad

- El directorio de datos de LinuxGateway almacena los Gateway Tokens de los nodos — restrinja los permisos del sistema.
- LinuxGateway solo debe exponerse vía HTTPS; no se recomienda exponer HTTP en texto plano directamente.
- Los `/api/gateway/*` del nodo solo aceptan `X-Gateway-Token` — no ponga tokens en URLs.
- Los nodos no deben exponer el backend de administración regular al internet público; restringir el acceso solo a LinuxGateway es lo mejor.
- Las tareas de iOS solo pueden enviarse a nodos Mac que soporten `ios`; los nodos Windows solo son adecuados para Android APK/AAB.

## Conexión inversa

La conexión inversa es adecuada cuando los nodos están detrás de NAT, redes domésticas o intranets corporativos donde LinuxGateway no puede acceder directamente a la dirección del nodo. En este caso, BuildServer se conecta proactivamente a LinuxGateway — no se necesita exposición de puerto público en el lado del nodo.

### Pasos de configuración

1. Generar un Enrollment Token en la interfaz web de LinuxGateway.
2. Establecer las variables de entorno en el nodo BuildServer:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

3. Iniciar BuildServer — se conectará automáticamente a LinuxGateway y se registrará como nodo de conexión inversa.
4. Tras la conexión, el nodo aparece en la interfaz web de LinuxGateway.
5. Tras revocar un nodo, debe generar un nuevo Enrollment Token para volver a registrarlo.

La conexión inversa está implementada en `LinuxGateway/Reverse/` y `BuildServer/Reverse/`.

## Actualización en línea

LinuxGateway incluye `SelfUpdateService`, que puede verificar y descargar paquetes de actualización desde Gitee o GitHub Releases sin necesidad de .NET SDK en el servidor.

### Endpoints API

| Endpoint | Método | Descripción |
|------|------|------|
| `/api/system/version` | GET | Obtener la versión actual |
| `/api/system/update/check` | GET | Verificar la última versión |
| `/api/system/update/apply` | POST | Aplicar actualización (solo Admin) |

### Proceso de actualización

1. Consultar la última versión desde la API de Gitee/GitHub Release en paralelo.
2. Descargar el paquete de actualización tar.gz.
3. Generar un script `apply-update.sh` para completar respaldo + reemplazo + reinicio.

### Configuración

| Variable | Descripción |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | Fuente de actualización: `gitee` o `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | Propietario del repositorio |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | Nombre del repositorio |

## Despliegue Docker

LinuxGateway soporta el despliegue Docker, particularmente adecuado para sistemas más antiguos como CentOS 7 donde el runtime nativo `libstdc++` puede ser demasiado antiguo. Ver [Guía de despliegue Docker](linux-gateway-docker.md).
