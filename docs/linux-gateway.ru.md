# Многоузловой вход LinuxGateway

`LinuxGateway` — опциональная центральная точка входа, подходящая для развёртывания на Linux-сервере с публичным доменом. Он не запускает Unity, не хранит Unity-проекты и не содержит Apple-сертификатов; он отвечает только за веб-вход, регистрацию узлов сборки Mac/Windows, выбор узлов и перенаправление задач в `BuildServer` каждого узла.

LinuxGateway поддерживает два режима подключения узлов: прямое подключение (LinuxGateway проактивно обращается к узлу) и обратное подключение (узел проактивно подключается к LinuxGateway, подходит для NAT/интранет-сред). Он также включает встроенную функцию онлайн-самообновления, скачивающую пакеты обновлений из Gitee/GitHub Releases без необходимости установки .NET SDK на сервере.

Без LinuxGateway экземпляры `BuildServer` Mac/Windows по-прежнему могут использоваться независимо для входа, конфигурации и сборок.

## Архитектура

```text
Внешние пользователи
  -> LinuxGateway Web/API
      -> Mac BuildServer /api/gateway/*    iOS + Android
      -> Windows BuildServer /api/gateway/* Android APK/AAB
```

Каждый узел Mac/Windows продолжает работать с существующим `BuildServer`, просто дополнительно активируя API, защищённый токеном, для вызовов LinuxGateway.

## Настройка узлов Mac/Windows

Установите перед запуском `BuildServer` на каждом узле:

```bash
export BUILD_SERVER_GATEWAY_TOKEN="надёжный случайный токен для этого узла"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"   # Обычно для Mac
```

Windows Android-узел:

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="надёжный случайный токен для этого узла"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

Если `BUILD_SERVER_GATEWAY_TOKEN` оставлен пустым, endpoints `/api/gateway/*` узла не будут активированы.

LinuxGateway должен иметь доступ к адресу узла, например:

```text
https://mac-build.example.com
https://win-build.example.com
```

Это могут быть адреса туннелей, VPN/интранет-адреса или публичные HTTPS endpoints. Рекомендуется HTTPS.

## Запуск LinuxGateway

Разработка:

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Отладка на Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
```

Если `LINUX_GATEWAY_ADMIN_PASSWORD` не задан, при первом запуске генерируется начальный пароль:

```text
linuxgateway-data/initial-admin.txt
```

Рекомендуется для продакшена:

```bash
export LINUX_GATEWAY_ADMIN_PASSWORD="strong-password"
export LINUX_GATEWAY_PUBLIC_BASE_URL="https://build.example.com"
export LINUX_GATEWAY_ALLOWED_ORIGINS="https://build.example.com"
export LINUX_GATEWAY_DATA_ROOT="/opt/unity-build-gateway/data"
```

## Публикация на Linux

Публикация Linux x64 из Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-linux-gateway-linux.ps1
```

Вывод по умолчанию:

```text
publish/linux-gateway
```

Скопируйте на Linux и запустите:

```bash
chmod +x ./LinuxGateway
./LinuxGateway --urls http://127.0.0.1:5090
```

Для публичного доступа используйте Nginx/Caddy для HTTPS и обратный прокси к `127.0.0.1:5090`.

## Рабочий процесс

1. Запустите `BuildServer` на узлах Mac/Windows и установите `BUILD_SERVER_GATEWAY_TOKEN`.
2. Запустите `LinuxGateway` на Linux.
3. Войдите в веб-интерфейс LinuxGateway.
4. Добавьте устройство:
   - Имя устройства: например `Mac Build`
   - URL BuildServer: например `https://mac-build.example.com`
   - Gateway Token: `BUILD_SERVER_GATEWAY_TOKEN` узла
   - Платформы: Mac: `iOS + Android`, Windows: `Android`
5. Обновите устройство для подтверждения видимости проектов и конфигураций узла.
6. При отправке сборки выберите целевое устройство, проект и конфигурацию.

## Замечания по безопасности

- Каталог данных LinuxGateway хранит Gateway Tokens узлов — ограничьте системные права.
- LinuxGateway следует открывать только через HTTPS; прямой доступ по открытому HTTP не рекомендуется.
- `/api/gateway/*` узла принимает только `X-Gateway-Token` — не помещайте токены в URL.
- Узлы не должны выставлять регулярный админ-бэкенд в публичный интернет; лучше ограничить доступ только для LinuxGateway.
- Задачи iOS можно отправлять только на Mac-узлы, поддерживающие `ios`; Windows-узлы подходят только для Android APK/AAB.

## Обратное подключение

Обратное подключение подходит, когда узлы находятся за NAT, домашними сетями или корпоративными интранетами, где LinuxGateway не может напрямую получить доступ к адресу узла. В этом случае BuildServer проактивно подключается к LinuxGateway — публичное exposition портов на стороне узла не требуется.

### Шаги настройки

1. Сгенерируйте Enrollment Token в веб-интерфейсе LinuxGateway.
2. Установите переменные окружения на узле BuildServer:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

3. Запустите BuildServer — он автоматически подключится к LinuxGateway и зарегистрируется как узел с обратным подключением.
4. После подключения узел появится в веб-интерфейсе LinuxGateway.
5. После отзыва узла необходимо сгенерировать новый Enrollment Token для повторной регистрации.

Обратное подключение реализовано в `LinuxGateway/Reverse/` и `BuildServer/Reverse/`.

## Онлайн-самообновление

LinuxGateway включает `SelfUpdateService`, который может проверять и скачивать пакеты обновлений из Gitee или GitHub Releases без необходимости установки .NET SDK на сервере.

### API endpoints

| Endpoint | Метод | Описание |
|------|------|------|
| `/api/system/version` | GET | Получить текущую версию |
| `/api/system/update/check` | GET | Проверить последнюю версию |
| `/api/system/update/apply` | POST | Применить обновление (только Admin) |

### Процесс обновления

1. Параллельный запрос последней версии из API Gitee/GitHub Release.
2. Скачивание пакета обновления tar.gz.
3. Генерация скрипта `apply-update.sh` для завершения резервного копирования + замены + перезапуска.

### Настройка

| Переменная | Описание |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | Источник обновлений: `gitee` или `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | Владелец репозитория |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | Имя репозитория |

## Docker-развёртывание

LinuxGateway поддерживает Docker-развёртывание, особенно подходящее для старых систем, таких как CentOS 7, где нативный runtime `libstdc++` может быть слишком старым. См. [Руководство по Docker-развёртыванию](linux-gateway-docker.md).
