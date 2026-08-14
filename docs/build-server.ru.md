# Платформа BuildServer

BuildServer — точка входа Web/Agent для инструмента автоматической сборки, поддерживающая iOS, Android APK/AAB и загрузку в Google Play. Первая версия использует один Mac, один Worker и последовательную очередь для предотвращения конкуренции между Unity, Xcode, Gradle, средами подписи и состоянием кэша/сертификатов.

## Модули

- `BuildServer.Api`: ASP.NET Core Minimal API для входа, проектов, конфигураций, задач, артефактов и аудита.
- `BuildServer.Worker`: Фоновый последовательный Worker, забирающий задачи из очереди и вызывающий CLI `AutomationUnityBuildIOS`.
- `BuildServer.Web`: Встроенный статический фронтенд для веб-входа и отправки сборок.
- `BuildServer.Mcp`: Endpoint инструментов JSON-RPC `/mcp` для Agent/AI.
- `BuildServer.Reverse`: Модуль обратного подключения, позволяющий BuildServer проактивно подключаться к LinuxGateway, подходит для NAT/интранет-сред.
- `buildserver-data`: Каталог JSON-персистентности, хранящий пользователей, проекты, конфигурации, задачи, артефакты, аудиторские записи и узлы Worker.

## Локальный запуск

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

Адрес по умолчанию:

```text
http://127.0.0.1:5088
```

Учётная запись по умолчанию:

```text
admin
```

Если `BUILD_SERVER_ADMIN_PASSWORD` не задан, при первом запуске генерируется случайный пароль:

```text
<DataRoot>/initial-admin.txt
```

Если `BUILD_SERVER_AGENT_TOKEN` не задан, при первом запуске генерируется случайный Agent API Key:

```text
<DataRoot>/initial-agent-token.txt
```

Рекомендуется для продакшена:

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

Значения безопасности по умолчанию:

- Workspace по умолчанию ограничен `~/UnityBuildWorkspace`.
- Артефакты по умолчанию ограничены `~/UnityBuildArtifacts`.
- Файлы конфигурации по умолчанию ограничены подкаталогом `configs` в каталоге данных BuildServer и каталогом `configs` программы.
- Git-репозитории по умолчанию разрешают HTTPS/SSH URL; в продакшене рекомендуется установить `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS`, например `github.com` или домен корпоративного Git-сервера.
- При доступе к веб-интерфейсу через Nginx/Caddy или другие обратные прокси, установите `BUILD_SERVER_PUBLIC_BASE_URL` и `BUILD_SERVER_ALLOWED_ORIGINS`, иначе защита от cross-site запросов отклонит записи с несовпадающими источниками.

## Публикация на Mac

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

После публикации используйте `deploy/launchd/com.automationunity.buildserver.plist` для запуска под пользователем `buildbot`. Сертификаты, профили подготовки, Unity License и SSH-ключи Git должны быть установлены под этим выделенным пользователем macOS.

## Обязательные данные

После первого входа:

1. Добавить проект: имя проекта, Git-репозиторий, ветка по умолчанию, разрешённые ветки, workspace и каталог артефактов.
2. Добавить конфигурацию: выбрать iOS или Android. Можно указать существующий путь JSON-файла конфигурации или отметить «Сгенерировать новый файл конфигурации», заполнить версию Unity, Bundle ID и специфичные для платформы поля в веб-форме, и сервер автоматически сгенерирует JSON и зарегистрирует его.
   - Поля iOS включают Team ID, Deployment Target, Export Method, Signing Style, копирование archive в Organizer, загрузку в App Store Connect/TestFlight.
   - Поля Android включают APK/AAB/both, версии SDK, keystore, Google Play Service Account, track, release status, артефакт загрузки.
3. Запустить сборку: выбрать проект и конфигурацию, отправить задачу.

BuildServer генерирует независимый снимок конфигурации для каждой задачи, резервирует Build Number и вызывает CLI:

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

## MCP/Agent

MCP endpoint:

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

Инструменты:

- `list_projects`
- `list_configs`
- `start_build`
- `start_ios_build` (устаревшее имя, новые интеграции должны использовать `start_build`)
- `get_build_status`
- `tail_build_log`
- `list_build_artifacts`

По умолчанию Agent разрешён только с `dryRun=true`. Для разрешения реальных сборок установите соответствующий `McpClientRecord.allowFullBuild` в `true` в данных и рекомендуйте авторизовать только конкретные проекты. MCP отправляет задачи только по ID проекта и конфигурации — он не принимает произвольные Git-репозитории или пути.

Новые конфигурации по умолчанию не разрешены для MCP; необходимо явно отметить «Разрешить MCP» в веб-интерфейсе.

## Email-уведомления

BuildServer включает встроенный сервис email-уведомлений (`EmailNotificationService`), который автоматически отправляет email после завершения задач сборки:

- **Успех сборки**: Email включает пути артефактов, затраченное время и сводку конфигурации.
- **Провал сборки**: Email включает провалившийся шаг, сводку ошибок и путь к логу.

Поддерживает SMTP 465 неявный SSL, списки контактов и персонализированные шаблоны email. Настройте SMTP-сервер, порт, учётные данные отправителя и список контактов в веб-бэкенде или на странице email-уведомлений DesktopApp.

## Управление хранилищем

По мере накопления задач сборки артефакты постепенно потребляют дисковое пространство. BuildServer предоставляет два механизма управления хранилищем:

- **Автоматическая очистка**: `MaintenanceService` автоматически очищает завершённые задачи и артефакты на основе `RetentionDays` и `MaxArtifactBytes`.
- **Ручная очистка**: Просмотр обзора хранилища в веб-бэкенде или на странице управления хранилищем DesktopApp, массовое или одиночное удаление исторических артефактов.

`StorageCleanupService` обрабатывает фактическое сканирование и удаление каталогов артефактов.

## Обратное подключение

Если узел BuildServer находится за NAT, домашней сетью или корпоративным интранетом, где LinuxGateway не может напрямую получить к нему доступ, можно использовать обратное подключение, чтобы BuildServer проактивно подключался к LinuxGateway.

Сгенерируйте Enrollment Token в веб-интерфейсе LinuxGateway, затем настройте BuildServer через переменные окружения:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

После подключения учётные данные узла сохраняются в каталоге данных BuildServer. Каталог `BuildServer/Reverse/` реализует клиентскую логику обратного подключения.

## Периметры безопасности

- Web/MCP только создают задачи — они не выполняют произвольные shell-команды.
- Worker выполняется последовательно — одновременно выполняется только одна задача.
- Проекты могут ограничивать разрешённые ветки.
- CLI внутри продолжает проверять белые списки Git и периметры путей.
- Скачивание артефактов задач требует аутентификации входа.
- Журнал аудита записывает входы, создание проектов, создание конфигураций, отправку/отмену задач и регистрацию Worker.
- Сервис обслуживания очищает завершённые задачи и артефакты по `RetentionDays` и `MaxArtifactBytes`.
- Чувствительная информация (пароли, токены) в email-уведомлениях не отображается — используется только для SMTP-аутентификации.

## Расширение на несколько Mac

`WorkerNodeRecord` уже персистирован, и предоставляются `/api/workers` и `/api/workers/register`. Встроенный Worker первой версии подходит для одного Mac; при масштабировании на несколько Mac рекомендуемая эволюция:

```text
Центральный BuildServer.Api + База данных
Mac Worker A/B/C как независимые процессы
Worker забирает подходящие ему задачи
Планирование по версии Unity/Xcode, авторизации проекта, текущей нагрузке
```

На этом этапе JSON-персистентность следует заменить на SQLite/PostgreSQL для предотвращения конкурирующей записи файлов между машинами.
