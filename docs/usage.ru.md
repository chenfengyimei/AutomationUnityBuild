# Руководство по использованию

Этот документ охватывает все пути использования AutomationUnityBuildIOS: локальный CLI, сборки iOS, сборки Android, сборки TikTok Mini-Game, загрузку в сторы, десктопный клиент DesktopApp, веб-платформу BuildServer, email-уведомления, управление хранилищем, управление шаблонами, вход MCP/Agent и многоузловое планирование LinuxGateway.

Если вы новичок, рекомендуем следовать этому порядку:

1. Подготовьте среду сборки Mac/Windows.
2. Скопируйте скрипты сборки Unity в ваш Unity-проект.
3. Сгенерируйте конфигурацию и выполните dry-run на Mac через CLI.
4. Выполните реальную сборку.
5. Развёртывайте BuildServer, когда команде нужна веб-точка входа.
6. Развёртывайте LinuxGateway, когда нескольким машинам сборки нужна единая точка входа.

---

## Выбор режима

| Сценарий | Рекомендуемый режим | Примечания |
|------|----------|------|
| Сборка iOS-пакетов на своём Mac | CLI | Минимум компонентов, просто `./AutomationUnityBuildIOS 06` |
| iOS + Android автоматизация | CLI или BuildServer | CLI для соло, BuildServer для команд |
| WebGL-сборка и загрузка TikTok Mini-Game | CLI | Ярлык `12` для генерации TikTok-конфигурации |
| Офлайн-управление конфигурациями и сборки на Windows | DesktopApp | Нативный десктоп-клиент, полный редактор конфигураций, выполнение сборок, просмотр артефактов |
| QA/ops нужен сборка по клику | BuildServer | Вход через браузер, отправка задач, просмотр логов, скачивание артефактов |
| Несколько Mac/Windows машин сборки | LinuxGateway + BuildServer | LinuxGateway как единый вход; сборки выполняются на BuildServer каждого узла |
| Узлы за NAT/интранетом, недоступны извне | LinuxGateway обратное подключение | Узлы подключаются к LinuxGateway, публичный IP или проброс портов не нужны |
| AI Agent участвует в процессе сборки | BuildServer MCP | Agent по умолчанию dry-run; реальные сборки требуют авторизации |

---

## Настройка окружения

### Машина разработчика

Для сборки и публикации этого инструмента требуется:

- .NET 8 SDK.
- Windows, macOS или Linux — любая ОС может компилировать этот проект.
- При использовании Visual Studio рекомендуется VS 2022 или новее.

Базовая проверка:

```powershell
dotnet --version
dotnet build .\AutomationUnityBuildIOS.sln
```

### Машина сборки iOS

Финальная сборка iOS должна выполняться на macOS, так как Unity iOS Build Support и Xcode доступны только на Mac.

Требования к Mac:

- Xcode, открытый хотя бы один раз для принятия лицензии и установки компонентов.
- Unity Hub, соответствующая версия Unity Editor и модуль iOS Build Support.
- Git CLI, Mac должен иметь доступ к вашему Unity-репозиторию. Рекомендуется настроить SSH-ключ.
- Учётная запись Apple Developer, сертификаты, профили подготовки или автоматическая подпись Xcode.
- Если не используется self-contained пакет публикации, на Mac также нужен .NET 8 SDK.

Команды проверки:

```bash
git --version
xcodebuild -version
/Applications/Unity/Hub/Editor/<UnityVersion>/Unity.app/Contents/MacOS/Unity -version
```

### Машина сборки Android

Сборки Android могут выполняться на macOS или Windows.

Требования:

- Unity Hub, соответствующая версия Unity Editor и Android Build Support.
- Android SDK, NDK, OpenJDK из состава Unity или собственный Android-тулчейн.
- Android keystore для подписи release-пакетов.
- Google Play Console Service Account JSON с правами публикации для целевого приложения при загрузке в Google Play.

---

## Подготовка Unity-проекта

Этот инструмент вызывает скрипты Unity Editor через `-executeMethod`, поэтому ваш Unity-репозиторий игры должен содержать скрипты сборки, предоставляемые этим проектом.

iOS:

```text
UnityBuildScripts/Ios/BuildIOS.cs
```

Скопировать в Unity-проект:

```text
Assets/Editor/BuildIOS.cs
```

Предоставляемый метод:

```text
BuildAutomation.IOSBuilder.Build
```

Android:

```text
UnityBuildScripts/Android/BuildAndroid.cs
```

Скопировать в Unity-проект:

```text
Assets/Editor/BuildAndroid.cs
```

Предоставляемый метод:

```text
BuildAutomation.AndroidBuilder.Build
```

После обновления AutomationUnityBuildIOS, если эти скрипты изменились, синхронизируйте их с вашим Unity-репозиторием игры.

---

## Быстрый старт локального CLI

### Публикация Mac CLI с машины разработчика

Mac Apple Silicon:

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-arm64
```

Intel Mac:

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-x64
```

Результат публикации будет в:

```text
publish/osx-arm64
publish/osx-x64
```

Скопируйте весь каталог на Mac, например:

```text
~/Downloads/publish_m1
```

### Первый запуск на Mac

Если macOS предупреждает о неидентифицированном разработчике или невозможности проверки ПО, выполните в каталоге публикации:

```bash
cd ~/Downloads/publish_m1
xattr -cr .
chmod +x ./AutomationUnityBuildIOS
codesign --force --deep --sign - ./AutomationUnityBuildIOS
./AutomationUnityBuildIOS 00
```

`00` отображает help и таблицу быстрых команд.

### Создание конфигурации

Интерактивный мастер конфигурации iOS:

```bash
./AutomationUnityBuildIOS 01
```

Эквивалентная полная команда:

```bash
./AutomationUnityBuildIOS init-config
```

Генерация пустого iOS-шаблона:

```bash
./AutomationUnityBuildIOS init-config --config build-ios.json --template
```

Генерация пустого Android-шаблона:

```bash
./AutomationUnityBuildIOS 11
```

Эквивалентная полная команда:

```bash
./AutomationUnityBuildIOS init-config --config build-android.json --template --platform android
```

Рекомендуется хранить production-конфигурации в `configs/`, например:

```text
configs/build-ios.dev.json
configs/build-ios.testflight.json
configs/build-android.internal.json
```

### Проверка окружения

Выберите конфигурацию и проверьте окружение:

```bash
./AutomationUnityBuildIOS 04
```

Указать конфигурацию:

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

При отладке конфигурации или dry-run на Windows добавьте:

```bash
--allow-non-mac
```

Production-сборки iOS должны выполняться на macOS.

### Предпросмотр команд

Предпросмотр конвейера без выполнения:

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
```

Эквивалентная полная команда:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --dry-run --verbose --allow-non-mac
```

### Реальная сборка

Выберите существующую конфигурацию и выполните полный конвейер:

```bash
./AutomationUnityBuildIOS 06
```

Указать конфигурацию:

```bash
./AutomationUnityBuildIOS 06 --config configs/build-ios.dev.json
```

Полная команда:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json
```

### Часто используемые флаги пропуска

| Флаг | Эффект |
|------|------|
| `--skip-git` | Пропустить Git pull/reset, использовать существующий проект в workspace |
| `--skip-unity` | Пропустить экспорт Unity или сборку Android |
| `--skip-xcode` | Пропустить Xcode archive/export (только iOS; игнорируется для Android) |
| `--dry-run` | Печатать команды без выполнения сборок или загрузок |
| `--verbose` | Более детальный вывод путей и команд |
| `--allow-non-mac` | Разрешить iOS dry-run или отладку конфигурации на не-macOS |

### Таблица быстрых команд

| Код | Описание |
|------|------|
| `00` | Показать help и таблицу быстрых команд |
| `01` | Интерактивный мастер конфигурации, генерирует готовый к использованию файл конфигурации |
| `02` | Генерация пустого шаблона iOS-конфигурации `build-ios.json` |
| `03` | Список существующих файлов конфигурации |
| `04` | Выбрать конфигурацию и проверить окружение |
| `05` | Выбрать конфигурацию и предпросмотр полной команды сборки (dry-run) |
| `06` | Выбрать конфигурацию и выполнить полный конвейер сборки |
| `07` | Выбрать конфигурацию и собрать, пропустив синхронизацию Git |
| `08` | Выбрать конфигурацию и собрать, пропустив экспорт Unity |
| `09` | Выбрать конфигурацию и собрать, пропустив компиляцию/экспорт Xcode |
| `10` | Выбрать конфигурацию и редактировать её содержимое |
| `11` | Генерация шаблона Android APK/AAB `build-android.json` |
| `12` | Генерация шаблона TikTok Mini-Game `build-tiktok.json` |

Быстрые команды могут сопровождаться дополнительными аргументами:

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
./AutomationUnityBuildIOS 06 --config configs/build-ios.release.json
./AutomationUnityBuildIOS 10 --config configs/build-android.internal.json
```

---

## Справочник по файлам конфигурации

Файлы конфигурации — JSON. См. `build-ios.sample.json` для iOS, `build-android.sample.json` для Android и `build-tiktok.sample.json` для TikTok.

### Общие поля

| Поле | Описание |
|------|------|
| `configName` | Отображаемое имя конфигурации, показывается в списках выбора |
| `buildPlatform` | `ios`, `android` или `tiktok` |
| `repositoryUrl` | Git clone URL для Unity-репозитория, поддержка HTTPS/SSH |
| `allowedRepositoryUrls` | Белый список репозиториев, рекомендуется для продакшена |
| `branch` | Ветка сборки |
| `workspaceRoot` | Корневой каталог Git workspace |
| `allowedWorkspaceRoots` | Разрешённые корневые каталоги workspace, предотвращает escape путей |
| `projectDirectoryName` | Имя каталога после клонирования репозитория |
| `unityProjectRelativePath` | Путь к Unity-проекту относительно корня репозитория; используйте `.`, если корень репозитория является Unity-проектом |
| `unityVersion` | Установленная версия Unity Hub, используется для вывода пути к исполняемому файлу Unity |
| `unityExecutablePath` | Полный путь к исполняемому файлу Unity; приоритет над `unityVersion` |
| `unityBuildMethod` | Имя статического метода Unity Editor |
| `artifactsRoot` | Корневой каталог артефактов сборки |
| `allowedArtifactsRoots` | Разрешённые корневые каталоги артефактов |
| `productName` | Unity Product Name |
| `bundleIdentifier` | iOS Bundle ID или Android Package Name |
| `bundleVersion` | Номер версии |
| `syncBundleVersionFromUnity` | Синхронизировать версию из Unity PlayerSettings |
| `buildNumber` | iOS Build Number или Android versionCode |
| `autoIncrementBuildNumber` | Автоматически инкрементировать build number после успешной сборки |
| `saveConfigSnapshot` | Сохранять снимок конфигурации в каталоге логов |

Три наиболее часто неверно настраиваемых значения:

```text
repositoryUrl: используйте git clone URL, а не заголовок веб-страницы.
unityProjectRelativePath: обычно ".", а не build, Builds или XcodeProject.
teamId: iOS использует 10-символьный Apple Developer Team ID, а не название компании.
```

### Поля iOS

| Поле | Описание |
|------|------|
| `scheme` | По умолчанию `Unity-iPhone` |
| `configuration` | По умолчанию `Release` |
| `exportMethod` | `development`, `ad-hoc`, `app-store` и т.д. (метод экспорта Xcode) |
| `teamId` | Apple Developer Team ID, должен быть 10 буквенно-цифровых символов |
| `signingStyle` | `automatic` или `manual` |
| `iosDeploymentTarget` | Минимальная версия iOS, например `13.0` |
| `allowProvisioningUpdates` | Разрешить Xcode автоматически обрабатывать обновления подписи |
| `generateExportOptionsPlist` | Автоматически генерировать `ExportOptions.plist` |
| `copyArchiveToOrganizer` | Копировать `.xcarchive` в Xcode Organizer |
| `appStoreConnectUploadEnabled` | Автоматически загружать в App Store Connect/TestFlight |

### Поля Android

| Поле | Описание |
|------|------|
| `androidBuildFormat` | `apk`, `aab` или `both` |
| `androidOutputDirectory` | Выходной каталог Android, авто-генерация если пусто |
| `apkOutputPath` | Выходной путь APK, авто-генерация если пусто |
| `aabOutputPath` | Выходной путь AAB, авто-генерация если пусто |
| `androidMinSdkVersion` | Опционально, переопределяет Min SDK |
| `androidTargetSdkVersion` | Опционально, переопределяет Target SDK |
| `androidKeystoreName` | Путь или имя keystore |
| `androidKeystorePass` | Пароль keystore |
| `androidKeyaliasName` | Key alias |
| `androidKeyaliasPass` | Пароль key alias |
| `googlePlayUploadEnabled` | Загружать в Google Play |
| `googlePlayTrack` | `internal`, `alpha`, `beta`, `production` |
| `googlePlayReleaseStatus` | `draft`, `inProgress`, `halted`, `completed` |
| `googlePlayUploadArtifact` | Загружать `apk`, `aab` или `both` |

Никогда не коммитьте сертификаты, приватные ключи или долгоживущие токены в репозиторий. Когда конфигурации должны ссылаться на секреты, предпочитайте локальные пути на машине сборки и защищайте права доступа к файлам.

### Поля TikTok

| Поле | Описание |
|------|------|
| `tiktokAppId` | TikTok Open Platform App ID |
| `tiktokAccessToken` | TikTok Open Platform Access Token |
| `tiktokGameName` | Название TikTok Mini-Game |
| `tiktokWebglOutputDirectory` | Выходной каталог WebGL, авто-генерация если пусто |
| `tiktokUploadEnabled` | Автоматически загружать в TikTok Open Platform |
| `tiktokApiEndpoint` | URL API TikTok Open Platform, по умолчанию `https://open-api.tiktokglobalshop.com` |

---

## Сборка iOS

### Базовый конвейер

Полный конвейер iOS:

1. Валидация периметров безопасности конфигурации и политики Git-репозитория.
2. Проверка `git`, Unity, `xcodebuild`.
3. Создание каталога выполнения и каталога логов.
4. Запись `build-config-snapshot.json`.
5. Pull или обновление Unity-репозитория.
6. Вызов Unity BatchMode для экспорта iOS Xcode-проекта.
7. Выполнение `xcodebuild archive`.
8. Выполнение `xcodebuild -exportArchive`.
9. Опциональное копирование `.xcarchive` в Xcode Organizer.
10. Опциональная загрузка в App Store Connect/TestFlight.

### Загрузка в App Store Connect / TestFlight

Включение автоматической загрузки требует `exportMethod` = `app-store` и настроенного App Store Connect API Key.

Пример:

```json
{
  "exportMethod": "app-store",
  "appStoreConnectUploadEnabled": true,
  "appStoreConnectApiKeyPath": "~/Secrets/AuthKey_XXXXXXXXXX.p8",
  "appStoreConnectApiKeyId": "XXXXXXXXXX",
  "appStoreConnectApiIssuerId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

Примечания:

- Файл `.p8` должен существовать локально на Mac-машине сборки.
- Key ID и Issuer ID берутся со страницы App Store Connect API Key.
- После успешной загрузки сборка попадает в очередь обработки App Store Connect/TestFlight.
- Отправка на review или релиз в продакшен следует политикам версий App Store Connect.

### Распространённые методы отладки iOS

Синхронизация только Git и Unity, пропуск Xcode:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-xcode
```

Пропуск Unity, повторное использование существующего Xcode-проекта для archive/export:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-unity
```

Только проверка конфигурации и окружения:

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

---

## Сборка Android

### Базовый конвейер

Полный конвейер Android:

1. Валидация периметров безопасности конфигурации и политики Git-репозитория.
2. Проверка `git` и Unity.
3. Создание каталога выполнения и каталога логов.
4. Запись `build-config-snapshot.json`.
5. Pull или обновление Unity-репозитория.
6. Вызов Unity BatchMode для сборки APK/AAB.
7. Опциональная загрузка в Google Play.

Android не требует Xcode; `--skip-xcode` игнорируется.

### Сборка APK/AAB

Конфигурация:

```json
{
  "buildPlatform": "android",
  "unityBuildMethod": "BuildAutomation.AndroidBuilder.Build",
  "androidBuildFormat": "both"
}
```

Опции `androidBuildFormat`:

| Значение | Результат |
|-------|--------|
| `apk` | Только APK |
| `aab` | Только AAB |
| `both` | APK и AAB |

### Загрузка в Google Play

Необходимо создать Service Account в Google Play Console и предоставить права публикации для целевого приложения.

Пример:

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

Рекомендуется: сначала dry-run:

```bash
./AutomationUnityBuildIOS run --config configs/build-android.internal.json --dry-run --verbose
```

Проверьте пути, имя пакета, версию и артефакт загрузки перед реальной сборкой.

---

## Сборка TikTok Mini-Game

### Базовый конвейер

Конвейер сборки TikTok Mini-Game:

1. Валидация периметров безопасности конфигурации и политики Git-репозитория.
2. Проверка `git` и Unity.
3. Создание каталога выполнения и каталога логов.
4. Запись `build-config-snapshot.json`.
5. Pull или обновление Unity-репозитория.
6. Вызов Unity BatchMode для сборки WebGL.
7. Опциональная загрузка в TikTok Open Platform.

Сборки TikTok не требуют Xcode; `--skip-xcode` игнорируется.

### Генерация конфигурации

```bash
./AutomationUnityBuildIOS 12
```

Эквивалентная полная команда:

```bash
./AutomationUnityBuildIOS init-config --config build-tiktok.json --template --platform tiktok
```

### Пример конфигурации

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

### Реальная сборка

```bash
./AutomationUnityBuildIOS run --config configs/build-tiktok.release.json
```

Код, связанный с TikTok, находится в `Modules/Tiktok/`, полностью независимо от iOS/Android и не влияет на существующие потоки сборки.

---

## Десктопный клиент

DesktopApp — нативный Windows-клиент на базе Avalonia UI 11 + .NET 8, повторно использующий всю основную логику главного проекта (AutomationWorkflow / BuildConfig / ConfigFileSelector / SampleFiles). Он интегрирует возможности CLI, BuildServer и управления шаблонами в одно десктопное приложение с полной офлайн-поддержкой.

### Страницы функций

| Страница | Функции |
|------|----------|
| **Управление конфигурацией** | Полное редактирование полей iOS/Android/TikTok, авто-синхронизация имени файла конфигурации, заполнение шаблона в один клик |
| **Задача сборки** | Tail логов в реальном времени, таймер, очистка логов, авто-прокрутка |
| **Проверка окружения** | Проверка Unity, Git, Xcode и других зависимостей |
| **Просмотр артефактов** | Список файлов, выбор, двойной клик для открытия, предпросмотр |
| **Управление хранилищем** | Массовое удаление с чекбоксами, одиночное удаление, выбрать все, обзор хранилища |
| **Email-уведомления** | Настройка SMTP (включая 465 неявный SSL), список контактов, шаблоны |
| **Профиль проекта** | Шаблон ProjectProfile, управление репозиторием/каталогами workspace |
| **Профиль Unity** | Шаблон UnityProfile, управление версией/путём Unity/BuildMethod/ProductName/BundleID |
| **Профиль подписи** | Шаблон SigningProfile, управление iOS TeamID/ExportMethod/SigningStyle/Android Keystore |
| **Профиль сертификата** | Шаблон CertificateProfile, управление ASC API Key/Google Play/TikTok Token |
| **Синхронизация с сервером** | Подключение к BuildServer REST API, двусторонняя синхронизация шаблонов и файлов конфигурации |
| **Менеджер BuildServer** | Авто-определение или ручной выбор пути BuildServer.exe, запуск/остановка в один клик, health check |
| **Управление данными** | Экспорт типов данных в JSON, импорт JSON с дедупликацией по ID |
| **Справка** | Руководство и справочник быстрых команд |

### Публикация DesktopApp

```powershell
dotnet publish DesktopApp/DesktopApp.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -o DesktopApp/bin/publish-vN
```

Если предыдущий exe ещё запущен, будет `UnauthorizedAccessException`. Сначала остановите его:

```powershell
Stop-Process -Name DesktopApp -Force
```

Затем публикуйте в новый каталог. Однофайловый вывод — около 89 МБ.

Можно также использовать скрипт публикации:

```powershell
.\scripts\publish-desktop.ps1
```

### Управление шаблонами

DesktopApp предоставляет четыре типа шаблонов конфигурации, данные хранятся в каталоге `profiles/`:

| Шаблон | Файл | Назначение |
|------|------|------|
| Профиль проекта | `projects.json` | URL репозитория, каталоги workspace и артефактов и т.д. |
| Профиль Unity | `unity-profiles.json` | Версия, путь Unity, BuildMethod, ProductName, BundleID |
| Профиль подписи | `signing-profiles.json` | iOS TeamID, ExportMethod, SigningStyle, Android Keystore |
| Профиль сертификата | `certificates.json` | ASC API Key, Google Play Service Account, TikTok Token |

В верхней части формы редактирования на странице управления конфигурацией есть четыре селектора шаблонов. Выберите по одному из каждого и нажмите «Применить» для заполнения соответствующих полей в один клик. После применения шаблона заполненные секции полей автоматически скрываются для уменьшения загромождения.

### Синхронизация с сервером

DesktopApp может подключаться к BuildServer REST API для двусторонней синхронизации:

- **Шаблоны проектов**: Pull / push
- **Шаблоны сертификатов**: Pull / push
- **Файлы конфигурации**: Просмотр списка серверных конфигураций + скачивание в локальный каталог `configs/`

Информация о подключении сохраняется в `profiles/server-settings.json`.

На странице управления конфигурацией также есть кнопка «Импортировать файл конфигурации» для импорта JSON из любого локального пути в `configs/`.

---

## Email-уведомления

BuildServer поддерживает автоматические email-уведомления после завершения задач сборки, охватывая как успех, так и провал.

### Настройка

Настраивается в веб-бэкенде BuildServer или на странице email-уведомлений DesktopApp:

| Поле | Описание |
|------|------|
| SMTP-сервер | например `smtp.gmail.com`, `smtp.qq.com` |
| SMTP-порт | Распространённые: 25 (plaintext), 465 (неявный SSL), 587 (STARTTLS) |
| Email отправителя | Адрес email, отправляющий уведомления |
| Пароль отправителя | Код авторизации или пароль email |
| Включить SSL | Порт 465 использует неявный SSL |
| Контакты для уведомлений | Список email получателей, разделённый запятыми или переносами строк |
| Шаблон email | Персонализированная тема и тело письма |

### Триггеры уведомлений

- **Успех сборки**: Email включает пути артефактов, затраченное время и сводку конфигурации.
- **Провал сборки**: Email включает провалившийся шаг, сводку ошибок и путь к логу для быстрого устранения неполадок.

Сервис email-уведомлений реализован в `BuildServer/Services/EmailNotificationService.cs`.

---

## Управление хранилищем

По мере накопления задач сборки артефакты постепенно потребляют дисковое пространство. BuildServer предоставляет два механизма управления хранилищем:

### Автоматическая очистка

`MaintenanceService` автоматически очищает завершённые задачи и артефакты на основе настроенных `RetentionDays` и `MaxArtifactBytes`.

### Ручная очистка

В веб-бэкенде или на странице управления хранилищем DesktopApp можно:

- Просмотреть обзор хранилища (общее пространство, использованное, количество задач, распределение размеров артефактов).
- Выбрать несколько исторических задач для массового удаления.
- Удалить артефакты одной задачи.
- Выбрать все для очистки всех исторических артефактов.

Сервис очистки хранилища реализован в `BuildServer/Services/StorageCleanupService.cs`.

---

## Логи и артефакты

Каждый запуск создаёт независимый каталог под `artifactsRoot`, например:

```text
~/UnityBuildArtifacts/YourUnityGame/20260625-153000/
```

Типичное содержимое:

| Файл или каталог | Описание |
|------------|------|
| `Logs/automation.log` | Основной лог конвейера, включает шаги, команды, затраченное время и ошибки |
| `Logs/unity-editor.log` | Собственный лог сборки Unity Editor |
| `Logs/unity-process.log` | stdout/stderr, перехваченный из процесса Unity |
| `Logs/build-config-snapshot.json` | Снимок конфигурации для этого запуска, с базовым маскированием |
| `Logs/xcode-archive.log` | Лог archive iOS |
| `Logs/xcode-export.log` | Лог export iOS |
| `Logs/xcode-upload.log` | Лог загрузки в App Store Connect |
| `.xcarchive` | Артефакт архивации iOS |
| Каталог экспорта `.ipa` | Артефакт экспорта iOS |
| `.apk` / `.aab` | Артефакты сборки Android |

Порядок устранения неполадок:

1. Сначала проверьте конец `automation.log` на провалившийся шаг.
2. Если шаг Unity провалился, проверьте `unity-editor.log`.
3. Если шаг Xcode iOS провалился, проверьте `xcode-archive.log` или `xcode-export.log`.
4. Если загрузка в store провалилась, проверьте `xcode-upload.log` или ошибку загрузки Google Play в основном логе.

Система логирования применяет базовое маскирование к общим чувствительным данным, таким как учётные данные/токены в URL, `Bearer` токены и значения для ключей вроде `password/token/secret/apiKey`.

---

## Веб-платформа BuildServer

BuildServer — это точка входа Web/Agent для CLI. Она предоставляет:

- Веб-вход.
- Управление проектами.
- Управление конфигурациями.
- Очередь задач сборки.
- Логи в реальном времени.
- Скачивание артефактов.
- Права пользователей.
- Журнал аудита.
- Инструменты MCP/Agent.
- API узла LinuxGateway.

Первая версия использует одну машину, один Worker, последовательную очередь для предотвращения конкуренции между Unity, Xcode, Gradle, средами подписи и каталогами кэша.

### Локальный запуск

Отладка на Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

Отладка на macOS/Linux:

```bash
./scripts/run-build-server.sh
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

Если `BUILD_SERVER_AGENT_TOKEN` не задан, при первом запуске генерируется токен MCP Agent по умолчанию:

```text
<DataRoot>/initial-agent-token.txt
```

### Переменные окружения для продакшена

Рекомендуется для продакшена:

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

Основные переменные:

| Переменная | Описание |
|------|------|
| `BUILD_SERVER_DATA_ROOT` | Каталог данных, хранит пользователей, проекты, конфигурации, задачи, JSON аудита |
| `BUILD_SERVER_ADMIN_PASSWORD` | Пароль администратора |
| `BUILD_SERVER_AGENT_TOKEN` | Токен MCP Agent |
| `BUILD_SERVER_PUBLIC_BASE_URL` | Публичный URL |
| `BUILD_SERVER_ALLOWED_ORIGINS` | Разрешённые web Origin; рекомендуется за обратным прокси |
| `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS` | Разрешённые корневые каталоги workspace |
| `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS` | Разрешённые корневые каталоги артефактов |
| `BUILD_SERVER_ALLOWED_CONFIG_ROOTS` | Разрешённые корневые каталоги файлов конфигурации |
| `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` | Разрешённые Git-хосты для регистрации |
| `BUILD_SERVER_GATEWAY_TOKEN` | Токен API узла; авто-генерирует `initial-gateway-token.txt` при первом запуске если пуст |
| `BUILD_SERVER_NODE_PLATFORMS` | Текущие возможности узла, например `ios,android` или `android` |

### Веб-рабочий процесс

После первого входа в бэкенд:

1. Добавить проект: имя проекта, Git-репозиторий, ветка по умолчанию, разрешённые ветки, workspace и каталог артефактов.
2. Добавить конфигурацию: выбрать iOS или Android.
3. Конфигурации могут указывать на существующий JSON-файл или генерироваться из веб-формы.
4. Запустить сборку: выбрать проект, конфигурацию, ветку и опциональные параметры.
5. Просмотреть статус, логи в реальном времени и артефакты в списке задач.

BuildServer генерирует независимый снимок конфигурации для каждой задачи и вызывает CLI:

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

### Публикация BuildServer на Mac

Mac Apple Silicon:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

Intel Mac:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-x64
```

Каталог публикации включает как BuildServer, так и CLI AutomationUnityBuildIOS. Для продакшена используйте:

```text
deploy/launchd/com.automationunity.buildserver.plist
```

Рекомендуется назначить выделенного пользователя macOS для запуска BuildServer, с Unity License, подписью Xcode, сертификатами, профилями подготовки и SSH-ключами Git, настроенными под этим пользователем.

### MCP / Agent

MCP endpoint:

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

Поддерживаемые инструменты:

| Инструмент | Описание |
|------|------|
| `list_projects` | Список доступных проектов |
| `list_configs` | Список конфигураций сборки в проекте |
| `start_build` | Отправить задачу сборки iOS или Android |
| `start_ios_build` | Устаревшее имя, новые интеграции должны использовать `start_build` |
| `get_build_status` | Запрос статуса задачи сборки |
| `tail_build_log` | Чтение последних строк лога |
| `list_build_artifacts` | Список артефактов задачи |

По умолчанию Agent разрешён только с `dryRun=true`. Для разрешения реальных сборок включите `allowFullBuild` для соответствующего MCP Client и рекомендуйте авторизовать только конкретные проекты.

Не помещайте Agent Token в параметры URL. Используйте `X-Agent-Token` или `Authorization: Bearer`.

---

## Многоузловой вход LinuxGateway

LinuxGateway подходит для развёртывания на Linux-сервере с публичным доменом. Он не запускает Unity, не хранит Unity-проекты и не содержит Apple-сертификатов; он отвечает только за вход, регистрацию узлов, выбор узлов, перенаправление задач и проксирование логов/артефактов.

Типичная архитектура:

```text
Внешние пользователи
  -> LinuxGateway Web/API
      -> Mac BuildServer       iOS + Android
      -> Windows BuildServer   Android
```

Без LinuxGateway каждый Mac/Windows BuildServer может использоваться независимо.

### Запуск LinuxGateway

Разработка:

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Отладка на Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
```

Адрес по умолчанию:

```text
http://127.0.0.1:5090
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

### Публикация LinuxGateway на Linux

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

### Режим 1: Прямое подключение к узлу

Прямое подключение подходит, когда LinuxGateway может достичь Mac/Windows BuildServer, например через VPN, интранет, туннель или публичный HTTPS.

Установите перед запуском каждого узла BuildServer:

```bash
export BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"
```

Windows Android-узел:

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

Можно также не задавать `BUILD_SERVER_GATEWAY_TOKEN` вручную. BuildServer автоматически сгенерирует его при первом запуске и сохранит в:

```text
<DataRoot>/initial-gateway-token.txt
```

BuildServer активирует:

```text
/api/gateway/*
```

LinuxGateway вызывает узел с:

```text
Header: X-Gateway-Token: <BUILD_SERVER_GATEWAY_TOKEN>
```

Добавьте устройство в веб-интерфейсе LinuxGateway:

| Поле | Пример |
|------|------|
| Имя устройства | `Mac Build` |
| URL BuildServer | `https://mac-build.example.com` |
| Gateway Token | `BUILD_SERVER_GATEWAY_TOKEN` узла |
| Платформы | Mac: `iOS + Android`, Windows: `Android` |

После сохранения обновите устройство, чтобы подтвердить видимость проектов и конфигураций узла.

### Режим 2: Обратное подключение к узлу

Обратное подключение подходит, когда узлы находятся за NAT, домашними сетями или корпоративными интранетами, где LinuxGateway не может напрямую получить доступ к адресу узла. В этом случае BuildServer инициирует подключение к LinuxGateway.

Сгенерируйте Enrollment Token в веб-интерфейсе LinuxGateway, затем заполните страницу подключения Gateway в BuildServer:

```text
Gateway URL: https://build.example.com
Enrollment Token: <token>
```

Можно также настроить через переменные окружения, чтобы BuildServer автоматически подключался при запуске:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

После подключения LinuxGateway отображает узел с обратным подключением. Учётные данные узла сохраняются в каталоге данных BuildServer; после отзыва узла необходимо сгенерировать новый Enrollment Token для повторной регистрации.

Обратное подключение реализовано в `LinuxGateway/Reverse/` и `BuildServer/Reverse/`.

### Онлайн-самообновление LinuxGateway

LinuxGateway включает `SelfUpdateService`, который может проверять и скачивать пакеты обновлений из Gitee или GitHub Releases без установки .NET SDK на сервере.

Проверка обновлений:

```text
GET /api/system/version
GET /api/system/update/check
```

Применить обновление (только Admin):

```text
POST /api/system/update/apply
```

Процесс обновления автоматически резервирует текущую версию, скачивает tar.gz пакет обновления и генерирует скрипт `apply-update.sh` для завершения замены и перезапуска.

Настройка:

| Переменная | Описание |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | Источник обновлений: `gitee` или `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | Владелец репозитория |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | Имя репозитория |

### Отправка сборок через LinuxGateway

1. Войдите в LinuxGateway.
2. Подтвердите, что узел онлайн на странице устройств.
3. Обновите узел, чтобы убедиться, что проекты и конфигурации синхронизированы.
4. На странице задач сборки выберите устройство, проект, конфигурацию и ветку.
5. Отправьте задачу.
6. Просмотрите статус, логи и артефакты, возвращённые удалённым узлом.

Задачи iOS можно отправлять только на Mac-узлы, поддерживающие `ios`; Windows-узлы обычно подходят только для Android APK/AAB.

---

## Рекомендации по безопасности

- Всегда устанавливайте надёжные пароли в продакшене; не полагайтесь на файлы с начальными паролями долгосрочно.
- Не помещайте `BUILD_SERVER_AGENT_TOKEN`, `BUILD_SERVER_GATEWAY_TOKEN` или Enrollment Token в URL. Используйте заголовки или серверное хранение.
- Каталоги данных LinuxGateway и BuildServer хранят пользователей, задачи, учётные данные узлов или токены — ограничьте системные права.
- Настройте `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS`, `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS`, `BUILD_SERVER_ALLOWED_CONFIG_ROOTS` и `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` для BuildServer.
- Если бэкенд узла используется только LinuxGateway, избегайте exposition регулярного админ-бэкенда в публичный интернет.
- iOS-сертификаты, профили подготовки, файлы `.p8` App Store Connect, Android keystore и JSON Service Account Google Play должны храниться только в защищённых локальных каталогах на машине сборки.
- Никогда не коммитьте сертификаты, приватные ключи или долгоживущие токены в Git.
- При доступе к веб-интерфейсу через обратный прокси настройте `PUBLIC_BASE_URL` и `ALLOWED_ORIGINS` во избежание отклонения cross-origin запросов или сбоя проверки источника.

---

## FAQ

| Проблема | Решение |
|------|------|
| Сборка iOS на Windows требует macOS | Продакшен-сборки iOS должны выполняться на Mac; Windows поддерживает только `--dry-run --allow-non-mac` для отладки конфигурации |
| Unity-исполняемый файл не найден | Установите `unityExecutablePath` или проверьте, что `unityVersion` соответствует пути установки Unity Hub |
| Ошибка Git pull | Выполните ручной `git clone` на машине сборки для проверки SSH-ключа или HTTPS-учётных данных |
| Ошибка валидации Team ID | `teamId` должен быть 10-символьным Apple Developer Team ID, а не названием компании |
| Ошибка загрузки в App Store Connect | Проверьте `exportMethod=app-store`, существование пути `.p8`, правильность Key ID и Issuer ID |
| Ошибка Android versionCode | `buildNumber` должен быть положительным целым числом |
| Ошибка загрузки в Google Play | Проверьте путь JSON Service Account, права приложения, packageName, track и формат артефакта загрузки |
| Ошибка входа в BuildServer | Учётная запись `admin`; копируйте только значение после `admin password:` из `initial-admin.txt` |
| Веб-операции записи отклонены | Проверьте, что `BUILD_SERVER_ALLOWED_ORIGINS` или `LINUX_GATEWAY_ALLOWED_ORIGINS` совпадают с доменом доступа |
| Узел LinuxGateway 401 | Gateway Token неверен или узел не включил `BUILD_SERVER_GATEWAY_TOKEN` |
| Таймаут узла LinuxGateway | Проверьте адрес, порт, брандмауэр, туннель или обратный прокси узла |
| Ошибка скачивания артефакта | Подтвердите, что путь артефакта находится в разрешённых artifacts roots BuildServer |

---

## Регрессионное тестирование

Разработчики могут запустить:

```powershell
.\scripts\verify.ps1
```

Он выполняет:

- Компиляцию решения.
- Компиляцию проекта CLI.
- Компиляцию BuildServer.
- Компиляцию LinuxGateway.
- Точку входа help `00`.
- Dry-run примера iOS.
- Dry-run примера Android.
- Открытие-закрытие редактора конфигурации.

Набор тестов покрывает 256+ тестовых случаев, охватывая парсинг аргументов CLI, модели конфигурации, безопасность путей, политики Git, построение команд Unity, Google Play API, конфигурации TikTok, маршруты API BuildServer, взаимодействие узлов LinuxGateway, обратное подключение, email-уведомления и все остальные модули.

Запуск полного набора тестов:

```powershell
dotnet test .\AutomationUnityBuildIOS.Tests\AutomationUnityBuildIOS.Tests.csproj
```

Быстрая проверка влияния на компиляцию:

```powershell
dotnet build .\AutomationUnityBuildIOS.sln
```
