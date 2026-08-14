# Guia de uso

Este documento cobre todos os caminhos de uso do AutomationUnityBuildIOS: CLI local, builds iOS, builds Android, builds TikTok Mini-Game, uploads para stores, cliente desktop DesktopApp, plataforma web BuildServer, notificações por email, gestão de armazenamento, gestão de templates, entrada MCP/Agent e agendamento multi-nó LinuxGateway.

Se você é novo, recomendamos seguir esta ordem:

1. Prepare seu ambiente de build Mac/Windows.
2. Copie os scripts de build do Unity para seu projeto Unity.
3. Gere uma configuração e faça um dry-run no Mac com o CLI.
4. Faça um build real.
5. Implante o BuildServer quando sua equipe precisar de um ponto de entrada web.
6. Implante o LinuxGateway quando várias máquinas de build precisarem de um ponto de entrada unificado.

---

## Seleção de modo

| Cenário | Modo recomendado | Notas |
|------|----------|------|
| Build de pacotes iOS no seu próprio Mac | CLI | Componentes mínimos, executar `./AutomationUnityBuildIOS 06` |
| iOS + Android automatizados | CLI ou BuildServer | CLI para individual, BuildServer para equipes |
| Build e upload WebGL TikTok Mini-Game | CLI | Usar atalho `12` para gerar configuração TikTok |
| Gestão de configuração offline e builds no Windows | DesktopApp | Cliente desktop nativo, editor de configuração completo, execução de builds, navegação de artefatos |
| QA/ops precisa de build por clique | BuildServer | Login no navegador, envio de tarefas, visualização de logs, download de artefatos |
| Múltiplas máquinas de build Mac/Windows | LinuxGateway + BuildServer | LinuxGateway como entrada unificada; os builds são executados no BuildServer de cada nó |
| Nós atrás de NAT/intranet, inacessíveis externamente | LinuxGateway conexão reversa | Os nós se conectam ao LinuxGateway, sem IP público ou mapeamento de portas |
| AI Agent participa do processo de build | BuildServer MCP | Agent por padrão faz dry-run; builds reais exigem autorização |

---

## Configuração do ambiente

### Máquina de desenvolvimento

Para compilar e publicar esta ferramenta é necessário:

- .NET 8 SDK.
- Windows, macOS ou Linux podem compilar este projeto.
- Se usar Visual Studio, recomenda-se VS 2022 ou superior.

Verificação básica:

```powershell
dotnet --version
dotnet build .\AutomationUnityBuildIOS.sln
```

### Máquina de build iOS

O build final de iOS deve ser executado em macOS, pois Unity iOS Build Support e Xcode estão disponíveis apenas no Mac.

Requisitos do Mac:

- Xcode, aberto pelo menos uma vez para aceitar a licença e instalar componentes.
- Unity Hub, a versão correspondente do Unity Editor e o módulo iOS Build Support.
- Git CLI, com o Mac podendo acessar seu repositório Unity. Recomenda-se configurar chave SSH.
- Conta Apple Developer, certificados, perfis de provisioning ou assinatura automática do Xcode.
- Se não usar um pacote de publicação self-contained, .NET 8 SDK também deve estar instalado no Mac.

Comandos de verificação:

```bash
git --version
xcodebuild -version
/Applications/Unity/Hub/Editor/<UnityVersion>/Unity.app/Contents/MacOS/Unity -version
```

### Máquina de build Android

Builds Android podem ser executados em macOS ou Windows.

Requisitos:

- Unity Hub, a versão correspondente do Unity Editor e Android Build Support.
- Android SDK, NDK, OpenJDK incluídos com Unity, ou sua própria cadeia de ferramentas Android.
- Um keystore Android para assinar pacotes release.
- Um JSON de Service Account do Google Play Console com permissões de publicação para o app alvo, se fizer upload para Google Play.

---

## Preparação do projeto Unity

Esta ferramenta invoca scripts do Unity Editor via `-executeMethod`, portanto seu repositório de jogo Unity deve conter os scripts de build fornecidos por este projeto.

iOS:

```text
UnityBuildScripts/Ios/BuildIOS.cs
```

Copiar para o projeto Unity:

```text
Assets/Editor/BuildIOS.cs
```

Método fornecido:

```text
BuildAutomation.IOSBuilder.Build
```

Android:

```text
UnityBuildScripts/Android/BuildAndroid.cs
```

Copiar para o projeto Unity:

```text
Assets/Editor/BuildAndroid.cs
```

Método fornecido:

```text
BuildAutomation.AndroidBuilder.Build
```

Após atualizar o AutomationUnityBuildIOS, se estes scripts foram alterados, sincronize-os com seu repositório de jogo Unity.

---

## Início rápido CLI local

### Publicação do CLI Mac a partir de uma máquina de desenvolvimento

Mac Apple Silicon:

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-arm64
```

Mac Intel:

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-x64
```

A saída publicada estará em:

```text
publish/osx-arm64
publish/osx-x64
```

Copie todo o diretório para o seu Mac, por exemplo:

```text
~/Downloads/publish_m1
```

### Primeira execução no Mac

Se o macOS avisar sobre um desenvolvedor não identificado ou software não verificado, execute o seguinte no diretório de publicação:

```bash
cd ~/Downloads/publish_m1
xattr -cr .
chmod +x ./AutomationUnityBuildIOS
codesign --force --deep --sign - ./AutomationUnityBuildIOS
./AutomationUnityBuildIOS 00
```

`00` exibe a ajuda e a tabela de comandos abreviados.

### Criação de configuração

Assistente de configuração iOS interativo:

```bash
./AutomationUnityBuildIOS 01
```

Comando completo equivalente:

```bash
./AutomationUnityBuildIOS init-config
```

Gerar um template iOS vazio:

```bash
./AutomationUnityBuildIOS init-config --config build-ios.json --template
```

Gerar um template Android vazio:

```bash
./AutomationUnityBuildIOS 11
```

Comando completo equivalente:

```bash
./AutomationUnityBuildIOS init-config --config build-android.json --template --platform android
```

Recomenda-se armazenar configurações de produção em `configs/`, por exemplo:

```text
configs/build-ios.dev.json
configs/build-ios.testflight.json
configs/build-android.internal.json
```

### Verificação de ambiente

Selecionar uma configuração e verificar o ambiente:

```bash
./AutomationUnityBuildIOS 04
```

Especificar uma configuração:

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

Ao depurar configurações ou fazer dry-runs no Windows, adicione:

```bash
--allow-non-mac
```

Builds de produção iOS ainda devem ser executados em macOS.

### Pré-visualização de comandos

Pré-visualização do pipeline sem execução:

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
```

Comando completo equivalente:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --dry-run --verbose --allow-non-mac
```

### Build real

Selecionar uma configuração existente e executar o pipeline completo:

```bash
./AutomationUnityBuildIOS 06
```

Especificar uma configuração:

```bash
./AutomationUnityBuildIOS 06 --config configs/build-ios.dev.json
```

Comando completo:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json
```

### Flags de omissão comuns

| Flag | Efeito |
|------|------|
| `--skip-git` | Omitir pull/reset do Git, usar o projeto existente no workspace |
| `--skip-unity` | Omitir exportação do Unity ou build do Android |
| `--skip-xcode` | Omitir Xcode archive/export (apenas iOS; ignorado para Android) |
| `--dry-run` | Imprimir comandos sem executar builds ou uploads |
| `--verbose` | Saída de caminhos e comandos mais detalhada |
| `--allow-non-mac` | Permitir dry-run de iOS ou depuração de configuração em não-macOS |

### Tabela de comandos abreviados

| Código | Descrição |
|------|------|
| `00` | Exibir ajuda e tabela de comandos abreviados |
| `01` | Assistente de configuração interativo, gera um arquivo de configuração pronto para uso |
| `02` | Gerar template de configuração iOS vazio `build-ios.json` |
| `03` | Listar arquivos de configuração existentes |
| `04` | Selecionar uma configuração e verificar o ambiente |
| `05` | Selecionar uma configuração e pré-visualizar o comando de build completo (dry-run) |
| `06` | Selecionar uma configuração e executar o pipeline de build completo |
| `07` | Selecionar uma configuração e construir, omitindo a sincronização Git |
| `08` | Selecionar uma configuração e construir, omitindo a exportação do Unity |
| `09` | Selecionar uma configuração e construir, omitindo a compilação/exportação do Xcode |
| `10` | Selecionar uma configuração e editar seu conteúdo |
| `11` | Gerar template de configuração Android APK/AAB `build-android.json` |
| `12` | Gerar template de configuração TikTok Mini-Game `build-tiktok.json` |

Comandos abreviados podem ser seguidos por argumentos adicionais:

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
./AutomationUnityBuildIOS 06 --config configs/build-ios.release.json
./AutomationUnityBuildIOS 10 --config configs/build-android.internal.json
```

---

## Referência de arquivos de configuração

Arquivos de configuração são JSON. Veja `build-ios.sample.json` para iOS, `build-android.sample.json` para Android e `build-tiktok.sample.json` para TikTok.

### Campos comuns

| Campo | Descrição |
|------|------|
| `configName` | Nome de exibição da configuração, mostrado em listas de seleção |
| `buildPlatform` | `ios`, `android` ou `tiktok` |
| `repositoryUrl` | URL de clone Git para o repositório Unity, suporta HTTPS/SSH |
| `allowedRepositoryUrls` | Lista branca de repositórios, recomendado para produção |
| `branch` | Ramo de build |
| `workspaceRoot` | Diretório raiz do workspace Git |
| `allowedWorkspaceRoots` | Diretórios raiz de workspace permitidos, previne escape de caminhos |
| `projectDirectoryName` | Nome do diretório após clonar o repositório |
| `unityProjectRelativePath` | Caminho para o projeto Unity relativo à raiz do repositório; use `.` se a raiz do repositório for o projeto Unity |
| `unityVersion` | Versão instalada do Unity Hub, usada para deduzir o caminho do executável Unity |
| `unityExecutablePath` | Caminho completo para o executável do Unity; tem prioridade sobre `unityVersion` |
| `unityBuildMethod` | Nome do método estático do Unity Editor |
| `artifactsRoot` | Diretório raiz de artefatos de build |
| `allowedArtifactsRoots` | Diretórios raiz de artefatos permitidos |
| `productName` | Unity Product Name |
| `bundleIdentifier` | iOS Bundle ID ou Android Package Name |
| `bundleVersion` | Número de versão |
| `syncBundleVersionFromUnity` | Sincronizar versão do Unity PlayerSettings |
| `buildNumber` | iOS Build Number ou Android versionCode |
| `autoIncrementBuildNumber` | Auto-incrementar o build number após um build bem-sucedido |
| `saveConfigSnapshot` | Salvar um snapshot de configuração no diretório de logs |

Os três valores mais comumente mal configurados:

```text
repositoryUrl: Use a URL de clone do git, não o título da página web.
unityProjectRelativePath: Normalmente ".", não build, Builds ou XcodeProject.
teamId: iOS usa o Apple Developer Team ID de 10 caracteres, não o nome da empresa.
```

### Campos iOS

| Campo | Descrição |
|------|------|
| `scheme` | Padrão `Unity-iPhone` |
| `configuration` | Padrão `Release` |
| `exportMethod` | `development`, `ad-hoc`, `app-store`, etc. (método de exportação Xcode) |
| `teamId` | Apple Developer Team ID, deve ser 10 caracteres alfanuméricos |
| `signingStyle` | `automatic` ou `manual` |
| `iosDeploymentTarget` | Versão mínima do iOS, por exemplo `13.0` |
| `allowProvisioningUpdates` | Permitir ao Xcode lidar com atualizações de assinatura automaticamente |
| `generateExportOptionsPlist` | Gerar automaticamente `ExportOptions.plist` |
| `copyArchiveToOrganizer` | Copiar `.xcarchive` para o Xcode Organizer |
| `appStoreConnectUploadEnabled` | Fazer upload automaticamente para App Store Connect/TestFlight |

### Campos Android

| Campo | Descrição |
|------|------|
| `androidBuildFormat` | `apk`, `aab` ou `both` |
| `androidOutputDirectory` | Diretório de saída Android, auto-gerado se vazio |
| `apkOutputPath` | Caminho de saída APK, auto-gerado se vazio |
| `aabOutputPath` | Caminho de saída AAB, auto-gerado se vazio |
| `androidMinSdkVersion` | Opcional, sobrescreve Min SDK |
| `androidTargetSdkVersion` | Opcional, sobrescreve Target SDK |
| `androidKeystoreName` | Caminho ou nome do keystore |
| `androidKeystorePass` | Senha do keystore |
| `androidKeyaliasName` | Key alias |
| `androidKeyaliasPass` | Senha do key alias |
| `googlePlayUploadEnabled` | Fazer upload para Google Play |
| `googlePlayTrack` | `internal`, `alpha`, `beta`, `production` |
| `googlePlayReleaseStatus` | `draft`, `inProgress`, `halted`, `completed` |
| `googlePlayUploadArtifact` | Upload `apk`, `aab` ou `both` |

Nunca faça commit de certificados, chaves privadas ou tokens de longa duração no repositório. Quando configurações precisam referenciar segredos, prefira caminhos locais na máquina de build e proteja as permissões de arquivo.

### Campos TikTok

| Campo | Descrição |
|------|------|
| `tiktokAppId` | TikTok Open Platform App ID |
| `tiktokAccessToken` | TikTok Open Platform Access Token |
| `tiktokGameName` | Nome do TikTok Mini-Game |
| `tiktokWebglOutputDirectory` | Diretório de saída WebGL, auto-gerado se vazio |
| `tiktokUploadEnabled` | Fazer upload automaticamente para TikTok Open Platform |
| `tiktokApiEndpoint` | URL da API TikTok Open Platform, padrão `https://open-api.tiktokglobalshop.com` |

---

## Build iOS

### Pipeline básico

O pipeline completo de iOS:

1. Validação dos perímetros de segurança da configuração e da política de repositório Git.
2. Verificação de `git`, Unity, `xcodebuild`.
3. Criação do diretório de execução e diretório de logs.
4. Escrita de `build-config-snapshot.json`.
5. Pull ou atualização do repositório Unity.
6. Invocação do Unity BatchMode para exportar o projeto Xcode de iOS.
7. Execução de `xcodebuild archive`.
8. Execução de `xcodebuild -exportArchive`.
9. Cópia opcional de `.xcarchive` para o Xcode Organizer.
10. Upload opcional para App Store Connect/TestFlight.

### Upload para App Store Connect / TestFlight

Habilitar o upload automático requer `exportMethod` definido como `app-store` e uma App Store Connect API Key configurada.

Exemplo:

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

- O arquivo `.p8` deve existir localmente na máquina de build Mac.
- Key ID e Issuer ID vêm da página App Store Connect API Key.
- Após um upload bem-sucedido, o build entra na fila de processamento do App Store Connect/TestFlight.
- O envio para review ou release em produção segue as políticas de versão do App Store Connect.

### Métodos comuns de depuração iOS

Sincronizar apenas Git e Unity, omitir Xcode:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-xcode
```

Omitir Unity, reutilizar o projeto Xcode existente para archive/export:

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-unity
```

Verificar apenas configuração e ambiente:

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

---

## Build Android

### Pipeline básico

O pipeline completo de Android:

1. Validação dos perímetros de segurança da configuração e da política de repositório Git.
2. Verificação de `git` e Unity.
3. Criação do diretório de execução e diretório de logs.
4. Escrita de `build-config-snapshot.json`.
5. Pull ou atualização do repositório Unity.
6. Invocação do Unity BatchMode para construir APK/AAB.
7. Upload opcional para Google Play.

Android não requer Xcode; `--skip-xcode` é ignorado.

### Build APK/AAB

Configuração:

```json
{
  "buildPlatform": "android",
  "unityBuildMethod": "BuildAutomation.AndroidBuilder.Build",
  "androidBuildFormat": "both"
}
```

Opções de `androidBuildFormat`:

| Valor | Resultado |
|-------|--------|
| `apk` | Apenas APK |
| `aab` | Apenas AAB |
| `both` | APK e AAB |

### Upload para Google Play

Você precisa criar um Service Account no Google Play Console e conceder permissões de publicação para o app alvo.

Exemplo:

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

Recomendado: primeiro dry-run:

```bash
./AutomationUnityBuildIOS run --config configs/build-android.internal.json --dry-run --verbose
```

Verifique caminhos, nome do pacote, versão e artefato de upload antes de executar o build real.

---

## Build TikTok Mini-Game

### Pipeline básico

O pipeline de build TikTok Mini-Game:

1. Validação dos perímetros de segurança da configuração e da política de repositório Git.
2. Verificação de `git` e Unity.
3. Criação do diretório de execução e diretório de logs.
4. Escrita de `build-config-snapshot.json`.
5. Pull ou atualização do repositório Unity.
6. Invocação do Unity BatchMode para construir WebGL.
7. Upload opcional para TikTok Open Platform.

Builds TikTok não requerem Xcode; `--skip-xcode` é ignorado.

### Geração de configuração

```bash
./AutomationUnityBuildIOS 12
```

Comando completo equivalente:

```bash
./AutomationUnityBuildIOS init-config --config build-tiktok.json --template --platform tiktok
```

### Exemplo de configuração

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

O código relacionado ao TikTok está em `Modules/Tiktok/`, completamente independente de iOS/Android e sem afetar os fluxos de build existentes.

---

## Cliente desktop

DesktopApp é um cliente desktop Windows nativo baseado em Avalonia UI 11 + .NET 8, reutilizando toda a lógica central do projeto principal (AutomationWorkflow / BuildConfig / ConfigFileSelector / SampleFiles). Integra as capacidades de CLI, BuildServer e gestão de templates em um único aplicativo desktop com suporte offline completo.

### Páginas de funcionalidades

| Página | Funcionalidades |
|------|----------|
| **Gestão de configuração** | Edição completa de campos iOS/Android/TikTok, auto-sincronização do nome do arquivo de configuração, preenchimento de template com um clique |
| **Tarefa de build** | Tail de logs em tempo real, cronômetro, limpar logs, auto-scroll |
| **Verificação de ambiente** | Verificar Unity, Git, Xcode e outras dependências |
| **Navegador de artefatos** | Lista de arquivos, seleção, duplo clique para abrir, pré-visualização |
| **Gestão de armazenamento** | Exclusão em massa com caixas de seleção, exclusão simples, selecionar tudo, visão geral |
| **Notificações por email** | Configuração SMTP (incluindo 465 SSL implícito), lista de contatos, templates |
| **Perfil de projeto** | Template ProjectProfile, gerencia repositório/diretórios de workspace |
| **Perfil Unity** | Template UnityProfile, gerencia versão/caminho Unity/BuildMethod/ProductName/BundleID |
| **Perfil de assinatura** | Template SigningProfile, gerencia iOS TeamID/ExportMethod/SigningStyle/Android Keystore |
| **Perfil de certificado** | Template CertificateProfile, gerencia ASC API Key/Google Play/TikTok Token |
| **Sincronização com servidor** | Conexão à BuildServer REST API, sincronização bidirecional de templates e arquivos de configuração |
| **Gerenciador BuildServer** | Detecção automática ou seleção manual do caminho BuildServer.exe, iniciar/parar com um clique, health check |
| **Gestão de dados** | Exportar tipos de dados para JSON, importar JSON com fusão deduplicada por ID |
| **Ajuda** | Guia de uso e referência de comandos abreviados |

### Publicação do DesktopApp

```powershell
dotnet publish DesktopApp/DesktopApp.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -o DesktopApp/bin/publish-vN
```

Se o exe anterior ainda estiver em execução, você obterá uma `UnauthorizedAccessException`. Pare-o primeiro:

```powershell
Stop-Process -Name DesktopApp -Force
```

Então publique em um novo diretório. A saída de arquivo único é de aproximadamente 89 MB.

Você também pode usar o script de publicação:

```powershell
.\scripts\publish-desktop.ps1
```

### Gestão de templates

DesktopApp fornece quatro tipos de templates de configuração, armazenados no diretório `profiles/`:

| Template | Arquivo | Propósito |
|------|------|------|
| Perfil de projeto | `projects.json` | URL do repositório, diretórios de workspace e artefatos, etc. |
| Perfil Unity | `unity-profiles.json` | Versão Unity, caminho, BuildMethod, ProductName, BundleID |
| Perfil de assinatura | `signing-profiles.json` | iOS TeamID, ExportMethod, SigningStyle, Android Keystore |
| Perfil de certificado | `certificates.json` | ASC API Key, Google Play Service Account, TikTok Token |

No topo do formulário de edição da página de gestão de configuração, há quatro seletores de templates. Escolha um de cada e clique em «Aplicar» para preencher os campos correspondentes com um clique. Após aplicar um template, as seções de campos preenchidas são ocultadas automaticamente para reduzir a desordem.

### Sincronização com servidor

DesktopApp pode se conectar à BuildServer REST API para sincronização bidirecional:

- **Templates de projeto**: Pull / push
- **Templates de certificado**: Pull / push
- **Arquivos de configuração**: Navegar lista de configurações do servidor + download para o diretório `configs/` local

As informações de conexão são persistidas em `profiles/server-settings.json`.

A página de gestão de configuração também fornece um botão «Importar arquivo de configuração» para importar JSON de qualquer caminho local para `configs/`.

---

## Notificações por email

BuildServer suporta notificações por email automáticas após a conclusão de tarefas de build, cobrindo tanto sucesso quanto falha.

### Configuração

Configurar no backend web do BuildServer ou na página de notificações por email do DesktopApp:

| Campo | Descrição |
|------|------|
| Servidor SMTP | por exemplo `smtp.gmail.com`, `smtp.qq.com` |
| Porta SMTP | Comuns: 25 (texto plano), 465 (SSL implícito), 587 (STARTTLS) |
| Email do remetente | Endereço de email que envia as notificações |
| Senha do remetente | Código de autorização ou senha de email |
| Habilitar SSL | Porta 465 usa SSL implícito |
| Contatos de notificação | Lista de emails de destinatários, separados por vírgulas ou quebras de linha |
| Template de email | Assunto e corpo de email personalizados |

### Gatilhos de notificação

- **Build bem-sucedido**: O email inclui caminhos de artefatos, tempo decorrido e resumo da configuração.
- **Build falhou**: O email inclui a etapa falha, resumo de erros e caminho do log para rápida resolução de problemas.

O serviço de notificações por email está implementado em `BuildServer/Services/EmailNotificationService.cs`.

---

## Gestão de armazenamento

À medida que as tarefas de build se acumulam, os artefatos consomem gradualmente o espaço em disco. BuildServer fornece dois mecanismos de gestão de armazenamento:

### Limpeza automática

`MaintenanceService` limpa automaticamente as tarefas e artefatos concluídos com base nos `RetentionDays` e `MaxArtifactBytes` configurados.

### Limpeza manual

No backend web ou na página de gestão de armazenamento do DesktopApp você pode:

- Ver a visão geral do armazenamento (espaço total, usado, número de tarefas, distribuição de tamanho de artefatos).
- Selecionar múltiplas tarefas históricas para exclusão em massa.
- Excluir artefatos de uma única tarefa.
- Selecionar tudo para limpar todos os artefatos históricos.

O serviço de limpeza de armazenamento está implementado em `BuildServer/Services/StorageCleanupService.cs`.

---

## Logs e artefatos

Cada execução cria um diretório independente em `artifactsRoot`, por exemplo:

```text
~/UnityBuildArtifacts/YourUnityGame/20260625-153000/
```

Conteúdos comuns:

| Arquivo ou diretório | Descrição |
|------------|------|
| `Logs/automation.log` | Log principal do pipeline, inclui etapas, comandos, tempo decorrido e erros |
| `Logs/unity-editor.log` | Log de build do próprio Unity Editor |
| `Logs/unity-process.log` | stdout/stderr capturado do processo Unity |
| `Logs/build-config-snapshot.json` | Snapshot de configuração para esta execução, com mascaramento básico |
| `Logs/xcode-archive.log` | Log de archive iOS |
| `Logs/xcode-export.log` | Log de export iOS |
| `Logs/xcode-upload.log` | Log de upload para App Store Connect |
| `.xcarchive` | Artefato de arquivo iOS |
| Diretório de exportação `.ipa` | Artefato de exportação iOS |
| `.apk` / `.aab` | Artefatos de build Android |

Ordem de resolução de problemas:

1. Primeiro verificar o final de `automation.log` para a etapa falha.
2. Se a etapa Unity falhou, verificar `unity-editor.log`.
3. Se a etapa Xcode iOS falhou, verificar `xcode-archive.log` ou `xcode-export.log`.
4. Se o upload para store falhou, verificar `xcode-upload.log` ou o erro de upload Google Play no log principal.

O sistema de logging aplica mascaramento básico a informações sensíveis comuns, como credenciais/tokens em URLs, tokens `Bearer` e valores para chaves como `password/token/secret/apiKey`.

---

## Plataforma web BuildServer

BuildServer é o ponto de entrada Web/Agent para o CLI. Fornece:

- Login web.
- Gestão de projetos.
- Gestão de configuração.
- Fila de tarefas de build.
- Logs em tempo real.
- Download de artefatos.
- Permissões de usuário.
- Logs de auditoria.
- Ferramentas MCP/Agent.
- API de nó LinuxGateway.

A primeira versão usa fila serial de máquina única e worker único para evitar contenção concorrente entre Unity, Xcode, Gradle, ambientes de assinatura e diretórios de cache.

### Início local

Depuração no Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

Depuração em macOS/Linux:

```bash
./scripts/run-build-server.sh
```

Endereço padrão:

```text
http://127.0.0.1:5088
```

Conta padrão:

```text
admin
```

Se `BUILD_SERVER_ADMIN_PASSWORD` não estiver definido, uma senha aleatória é gerada no primeiro início:

```text
<DataRoot>/initial-admin.txt
```

Se `BUILD_SERVER_AGENT_TOKEN` não estiver definido, um token MCP Agent padrão é gerado no primeiro início:

```text
<DataRoot>/initial-agent-token.txt
```

### Variáveis de ambiente de produção

Recomendado para produção:

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

Variáveis comuns:

| Variável | Descrição |
|------|------|
| `BUILD_SERVER_DATA_ROOT` | Diretório de dados, armazena usuários, projetos, configurações, tarefas, JSON de auditoria |
| `BUILD_SERVER_ADMIN_PASSWORD` | Senha de administrador |
| `BUILD_SERVER_AGENT_TOKEN` | Token MCP Agent |
| `BUILD_SERVER_PUBLIC_BASE_URL` | URL pública |
| `BUILD_SERVER_ALLOWED_ORIGINS` | Origins web permitidos; recomendado atrás de um proxy reverso |
| `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS` | Diretórios raiz de workspace permitidos |
| `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS` | Diretórios raiz de artefatos permitidos |
| `BUILD_SERVER_ALLOWED_CONFIG_ROOTS` | Diretórios raiz de arquivos de configuração permitidos |
| `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` | Hosts Git permitidos para registro |
| `BUILD_SERVER_GATEWAY_TOKEN` | Token de API de nó; auto-gera `initial-gateway-token.txt` no primeiro início se vazio |
| `BUILD_SERVER_NODE_PLATFORMS` | Capacidades do nó atual, por exemplo `ios,android` ou `android` |

### Fluxo de uso web

Após o primeiro login no backend:

1. Adicionar um projeto: nome do projeto, repositório Git, ramo padrão, ramos permitidos, workspace e diretório de artefatos.
2. Adicionar uma configuração: selecionar iOS ou Android.
3. Configurações podem apontar para um arquivo JSON existente ou ser geradas a partir do formulário web.
4. Iniciar um build: selecionar projeto, configuração, ramo e parâmetros opcionais.
5. Ver o status, logs em tempo real e artefatos na lista de tarefas.

BuildServer gera um snapshot de configuração independente para cada tarefa e invoca o CLI:

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

### Publicação do BuildServer para Mac

Mac Apple Silicon:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

Mac Intel:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-x64
```

O diretório de publicação inclui tanto o BuildServer quanto o CLI AutomationUnityBuildIOS. Para produção, usar:

```text
deploy/launchd/com.automationunity.buildserver.plist
```

Recomenda-se designar um usuário macOS dedicado para executar o BuildServer, com Unity License, assinatura Xcode, certificados, perfis de provisioning e chaves SSH Git todos configurados sob esse usuário.

### MCP / Agent

Endpoint MCP:

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

Ferramentas suportadas:

| Ferramenta | Descrição |
|------|------|
| `list_projects` | Listar projetos disponíveis |
| `list_configs` | Listar configurações de build em um projeto |
| `start_build` | Enviar uma tarefa de build iOS ou Android |
| `start_ios_build` | Nome herdado, novas integrações devem usar `start_build` |
| `get_build_status` | Consultar o status de uma tarefa de build |
| `tail_build_log` | Ler as últimas linhas de log |
| `list_build_artifacts` | Listar artefatos de uma tarefa |

Por padrão, Agents só têm permissão para `dryRun=true`. Para permitir builds reais, habilite `allowFullBuild` para o MCP Client correspondente e recomende autorizar apenas projetos específicos.

Não coloque Agent Tokens em parâmetros de URL. Use `X-Agent-Token` ou `Authorization: Bearer`.

---

## Entrada multi-nó LinuxGateway

LinuxGateway é adequado para implantação em um servidor Linux com um domínio público. Não executa Unity, não armazena projetos Unity e não contém certificados Apple; apenas gerencia login, registro de nós, seleção de nós, encaminhamento de tarefas e proxy de logs/artefatos.

Arquitetura típica:

```text
Usuários externos
  -> LinuxGateway Web/API
      -> Mac BuildServer       iOS + Android
      -> Windows BuildServer   Android
```

Sem LinuxGateway, cada Mac/Windows BuildServer ainda pode ser usado de forma independente.

### Início do LinuxGateway

Desenvolvimento:

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Depuração no Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
```

Endereço padrão:

```text
http://127.0.0.1:5090
```

Se `LINUX_GATEWAY_ADMIN_PASSWORD` não estiver definido, uma senha inicial é gerada no primeiro início:

```text
linuxgateway-data/initial-admin.txt
```

Recomendado para produção:

```bash
export LINUX_GATEWAY_ADMIN_PASSWORD="strong-password"
export LINUX_GATEWAY_PUBLIC_BASE_URL="https://build.example.com"
export LINUX_GATEWAY_ALLOWED_ORIGINS="https://build.example.com"
export LINUX_GATEWAY_DATA_ROOT="/opt/unity-build-gateway/data"
```

### Publicação do LinuxGateway para Linux

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-linux-gateway-linux.ps1
```

Saída padrão:

```text
publish/linux-gateway
```

Copiar para Linux e executar:

```bash
chmod +x ./LinuxGateway
./LinuxGateway --urls http://127.0.0.1:5090
```

Para acesso público, usar Nginx/Caddy para HTTPS e proxy reverso para `127.0.0.1:5090`.

### Modo 1: Conexão direta ao nó

A conexão direta é adequada quando LinuxGateway pode alcançar o BuildServer Mac/Windows, por exemplo via VPN, intranet, túnel ou HTTPS público.

Definir antes de iniciar cada nó BuildServer:

```bash
export BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"
```

Nó Android Windows:

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

Você também pode não definir manualmente `BUILD_SERVER_GATEWAY_TOKEN`. BuildServer o auto-gerará no primeiro início e o salvará em:

```text
<DataRoot>/initial-gateway-token.txt
```

BuildServer habilitará:

```text
/api/gateway/*
```

LinuxGateway chama o nó com:

```text
Header: X-Gateway-Token: <BUILD_SERVER_GATEWAY_TOKEN>
```

Adicionar um dispositivo na interface web do LinuxGateway:

| Campo | Exemplo |
|------|------|
| Nome do dispositivo | `Mac Build` |
| URL BuildServer | `https://mac-build.example.com` |
| Gateway Token | O `BUILD_SERVER_GATEWAY_TOKEN` do nó |
| Plataformas | Mac: `iOS + Android`, Windows: `Android` |

Após salvar, atualizar o dispositivo para confirmar que os projetos e configurações do nó estão visíveis.

### Modo 2: Conexão reversa ao nó

A conexão reversa é adequada quando os nós estão atrás de NAT, redes domésticas ou intranets corporativos onde LinuxGateway não pode acessar diretamente o endereço do nó. Neste caso, BuildServer inicia a conexão para LinuxGateway.

Gerar um Enrollment Token na interface web do LinuxGateway, depois preencher a página de conexão Gateway no BuildServer:

```text
Gateway URL: https://build.example.com
Enrollment Token: <token>
```

Você também pode configurar via variáveis de ambiente para que BuildServer se conecte automaticamente no início:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

Após conectar, LinuxGateway exibe o nó de conexão reversa. As credenciais do nó são salvas no diretório de dados do BuildServer; após revogar um nó, você deve gerar um novo Enrollment Token para registrar novamente.

A conexão reversa está implementada em `LinuxGateway/Reverse/` e `BuildServer/Reverse/`.

### Atualização online do LinuxGateway

LinuxGateway inclui `SelfUpdateService`, que pode verificar e baixar pacotes de atualização do Gitee ou GitHub Releases sem necessidade de .NET SDK no servidor.

Verificar atualizações:

```text
GET /api/system/version
GET /api/system/update/check
```

Aplicar atualização (apenas Admin):

```text
POST /api/system/update/apply
```

O processo de atualização faz backup automaticamente da versão atual, baixa um pacote de atualização tar.gz e gera um script `apply-update.sh` para completar a substituição e o reinício.

Configuração:

| Variável | Descrição |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | Fonte de atualização: `gitee` ou `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | Proprietário do repositório |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | Nome do repositório |

### Envio de builds via LinuxGateway

1. Fazer login no LinuxGateway.
2. Confirmar que o nó está online na página de dispositivos.
3. Atualizar o nó para garantir que projetos e configurações estão sincronizados.
4. Na página de tarefas de build, selecionar dispositivo, projeto, configuração e ramo.
5. Enviar a tarefa.
6. Ver o status, logs e artefatos retornados pelo nó remoto.

Tarefas iOS só podem ser enviadas para nós Mac que suportem `ios`; nós Windows geralmente são adequados apenas para Android APK/AAB.

---

## Recomendações de segurança

- Sempre defina senhas fortes em produção; não dependa de arquivos de senha iniciais a longo prazo.
- Não coloque `BUILD_SERVER_AGENT_TOKEN`, `BUILD_SERVER_GATEWAY_TOKEN` ou Enrollment Tokens em URLs. Use headers ou armazenamento no servidor.
- Diretórios de dados do LinuxGateway e BuildServer armazenam usuários, tarefas, credenciais de nós ou tokens — restrinja as permissões do sistema.
- Configure `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS`, `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS`, `BUILD_SERVER_ALLOWED_CONFIG_ROOTS` e `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` para o BuildServer.
- Se um backend de nó for usado apenas pelo LinuxGateway, evite expor o backend de administração regular à internet pública.
- Certificados iOS, perfis de provisioning, arquivos `.p8` do App Store Connect, keystores Android e JSON de Service Account do Google Play devem ser armazenados apenas em diretórios locais seguros na máquina de build.
- Nunca faça commit de certificados, chaves privadas ou tokens de longa duração no Git.
- Ao acessar a interface web via um proxy reverso, configure `PUBLIC_BASE_URL` e `ALLOWED_ORIGINS` para evitar rejeição de requisições cross-origin ou falha de validação de origem.

---

## FAQ

| Problema | Resolução |
|------|------|
| Build de iOS no Windows indica que macOS é necessário | Builds de produção iOS devem ser executados em Mac; Windows só suporta `--dry-run --allow-non-mac` para depuração de configuração |
| Executável do Unity não encontrado | Defina `unityExecutablePath` ou verifique se `unityVersion` corresponde a um caminho instalado do Unity Hub |
| Falha no Git pull | Faça um `git clone` manual na máquina de build para verificar a chave SSH ou credenciais HTTPS |
| Falha na validação do Team ID | `teamId` deve ser um Apple Developer Team ID de 10 caracteres, não um nome de empresa |
| Falha no upload para App Store Connect | Verifique `exportMethod=app-store`, existência do caminho `.p8`, Key ID e Issuer ID corretos |
| Erro de Android versionCode | `buildNumber` deve ser um inteiro positivo |
| Falha no upload para Google Play | Verifique o caminho do JSON Service Account, permissões do app, packageName, track e formato do artefato de upload |
| Falha no login do BuildServer | A conta é `admin`; copie apenas o valor após `admin password:` em `initial-admin.txt` |
| Operações de escrita web rejeitadas | Verifique se `BUILD_SERVER_ALLOWED_ORIGINS` ou `LINUX_GATEWAY_ALLOWED_ORIGINS` corresponde ao domínio de acesso |
| Nó LinuxGateway 401 | O Gateway Token está incorreto ou o nó não habilitou `BUILD_SERVER_GATEWAY_TOKEN` |
| Timeout do nó LinuxGateway | Verifique o endereço, porta, firewall, túnel ou proxy reverso do nó |
| Falha no download de artefato | Confirme que o caminho do artefato está dentro dos artifacts roots permitidos do BuildServer |

---

## Testes de regressão

Desenvolvedores podem executar:

```powershell
.\scripts\verify.ps1
```

Ele realiza:

- Compilação da solução.
- Compilação do projeto CLI.
- Compilação do BuildServer.
- Compilação do LinuxGateway.
- Entrada de ajuda `00`.
- Dry-run do exemplo iOS.
- Dry-run do exemplo Android.
- Abertura-fechamento do editor de configuração.

A suíte de testes cobre 256+ casos de teste, abrangendo parsing de argumentos CLI, modelos de configuração, segurança de caminhos, políticas Git, construção de comandos Unity, API Google Play, configurações TikTok, rotas de API BuildServer, comunicação de nós LinuxGateway, conexão reversa, notificações por email e todos os outros módulos.

Executar a suíte completa de testes:

```powershell
dotnet test .\AutomationUnityBuildIOS.Tests\AutomationUnityBuildIOS.Tests.csproj
```

Verificação rápida de impacto na compilação:

```powershell
dotnet build .\AutomationUnityBuildIOS.sln
```
