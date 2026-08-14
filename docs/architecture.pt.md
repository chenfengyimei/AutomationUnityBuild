# Arquitetura

Este projeto usa um design modular em camadas, com desacoplamento completo entre o motor de build central e os pontos de entrada da plataforma. CLI, BuildServer, DesktopApp e LinuxGateway compartilham a mesma lógica central — as diferenças residem apenas na camada de entrada e no método de interação.

## Responsabilidades de diretórios

A ferramenta é organizada nos seguintes diretórios por responsabilidade:

- `Cli/`: Ponto de entrada de comandos, parsing de argumentos de linha de comando, mapeamento de comandos abreviados (`ShortcutCommands`).
- `ConsoleUi/`: UI de console interativo, incluindo o assistente de inicialização, o editor de configuração e as solicitações de entrada.
- `Configuration/`: Modelos de configuração, leitura/escrita de arquivos de configuração, seleção de arquivos de configuração, resolução de caminhos, configurações de exemplo. Suporta configurações de plataforma `ios`, `android` e `tiktok`.
- `Workflow/`: Orquestração do pipeline de build, contexto de execução, atualização de configuração em tempo de execução, snapshots de configuração.
- `Services/`: Capacidades de negócio compartilhadas multiplataforma, incluindo sincronização Git, verificações de ambiente, preparação de diretórios, validação de projeto Unity e validação de segurança de caminhos.
- `Modules/Common/`: Capacidades compartilhadas de módulos de plataforma, incluindo a interface de Pipeline de plataforma, a construção de argumentos de comandos Unity, o diagnóstico de logs Unity e a leitura de metadados Unity.
- `Modules/Ios/`: Capacidades de build específicas de iOS, incluindo exportação de projeto Xcode do Unity, localização de project/workspace do Xcode, `xcodebuild archive/export`.
- `Modules/Android/`: Capacidades de build específicas de Android, incluindo builds APK/AAB do Unity, upload via API Google Play Publishing; o subdiretório `GooglePlay/` lida com os detalhes de HTTP API, OAuth e Service Account.
- `Modules/Tiktok/`: Capacidades específicas de TikTok Mini-Game, incluindo o pipeline de build WebGL (`TiktokBuildPipeline`), o serviço de build (`TiktokBuildService`) e o upload via API TikTok Open Platform (`TiktokUploadService`). Completamente independente de iOS/Android — não afeta os fluxos existentes.
- `Infrastructure/`: Infraestrutura comum, incluindo logging (`BuildLogger`), execução de processos (`ProcessRunner`), ferramentas de caminho (`PathTools`), perímetros de segurança de caminho (`PathSafety`) e mascaramento de dados sensíveis. Estas capacidades são compartilhadas por CLI, BuildServer e DesktopApp.
- `UnityBuildScripts/Ios/`: Script de build do Unity Editor iOS para copiar em `Assets/Editor` do projeto Unity.
- `UnityBuildScripts/Android/`: Script de build do Unity Editor Android para copiar em `Assets/Editor` do projeto Unity.
- `BuildServer/`: Plataforma web de build, incluindo API (`ApiRoutes`), frontend integrado (`wwwroot/`), worker em segundo plano (`BuildWorkerService`), entrada MCP/Agent (`McpEndpoint`), API de nó Gateway (`GatewayEndpoint`), notificações por email (`EmailNotificationService`), gestão de armazenamento (`StorageCleanupService`), escaneamento de artefatos (`ArtifactScanner`), manutenção (`MaintenanceService`), conexão reversa (`Reverse/`) e persistência JSON (`Persistence/`).
- `LinuxGateway/`: Entrada unificada multi-dispositivo, incluindo API (`ApiRoutes`), frontend integrado (`wwwroot/`), cliente de gateway de nó (`NodeGatewayClient`), atualização de nós (`NodeRefreshService`), atualização de jobs (`JobRefreshService`), gestão de conexão reversa (`Reverse/`), atualização online (`SelfUpdateService`) e persistência JSON (`Persistence/`).
- `DesktopApp/`: Cliente desktop Avalonia UI 11, incluindo Views (14 páginas), ViewModels (15 view models), Services (`BuildRunner` / `ProfileStore` / `ServerSyncService`), Controls (controles personalizados) e Styles (recursos de estilo). Referencia o projeto principal via `InternalsVisibleTo` + `Compile Remove` para reutilizar toda a lógica central.
- `deploy/`: Templates de implantação de produção, como plist `launchd` do macOS e arquivos de implantação Docker.

## Princípios de design chave

### Orquestração do pipeline separada das capacidades de plataforma

`AutomationWorkflow` apenas orquestra as etapas — ele não lida diretamente com os detalhes de Git, Unity, Xcode, Google Play ou TikTok. Ao adicionar capacidades de plataforma, elas devem ser colocadas no diretório `Modules/<Platform>/` correspondente e chamadas pelo workflow; capacidades multiplataforma vão em `Services/`. Três Pipelines de plataforma são atualmente suportados:

- `IosBuildPipeline` — Git → Unity → Xcode archive/export → upload ASC
- `AndroidBuildPipeline` — Git → Unity → APK/AAB → upload Google Play
- `TiktokBuildPipeline` — Git → Unity → WebGL → upload TikTok Open Platform

### Editor de configuração baseado em campos

O editor de configuração usa uma lista de descritores de campos para dirigir o menu e a lógica de modificação. Ao adicionar campos de configuração, adicione primeiro uma entrada à lista de campos de `ConfigEditor`, evitando a dispersão da exibição do menu e da lógica de modificação switch-case.

### Fundamentos de segurança

Ao conectar-se a backends web, workers ou MCP/Agent, todos os pontos de entrada devem reutilizar as capacidades pré-existentes já implementadas no CLI:

- `PathSafetyValidator`: Valida que workspace, diretórios de repositório, projetos Unity, artefatos, logs, saídas Xcode e archive/export estão todos dentro dos diretórios raiz permitidos.
- `GitRepositoryPolicyValidator`: Valida o formato de URL do Git e a lista branca `allowedRepositoryUrls`.
- `BuildConfigSnapshotWriter`: Gera `Logs/build-config-snapshot.json` em cada execução real, registrando o snapshot de configuração, os caminhos resolvidos e os argumentos do CLI.
- `SensitiveText`: Mascara uniformemente tokens/senhas comuns em logs, comandos, stdout/stderr e snapshots de configuração.

Estas capacidades não devem ser limitadas à camada Web/API. O Worker também deve invocá-las antes de executar builds, para evitar contornar os pontos de entrada e acionar configurações perigosas diretamente.

## Arquitetura BuildServer

BuildServer é o ponto de entrada Web/Agent para o CLI, com o seguinte design:

### Fila serial

O design de máquina única, worker único e fila serial é intencional: Unity, Xcode, Gradle, certificados de assinatura e diretórios de cache geralmente não toleram contenção concorrente na mesma máquina. A escalabilidade multi-máquina é gerenciada pelo LinuxGateway.

### Camada de serviços

| Serviço | Arquivo | Responsabilidade |
|------|------|------|
| Fila de tarefas | `BuildQueueService.cs` | Gerencia o enqueue, dequeue e as transições de estado das tarefas de build |
| Worker em segundo plano | `BuildWorkerService.cs` | Consome a fila em série, invoca o CLI para builds |
| Notificações por email | `EmailNotificationService.cs` | Envia notificações por email de sucesso/falha após os builds |
| Scanner de artefatos | `ArtifactScanner.cs` | Escaneia os diretórios de artefatos de tarefas, gera listas de artefatos |
| Leitor de logs | `LogFileReader.cs` | Lê e faz tail dos logs de tarefas |
| Limpeza de armazenamento | `StorageCleanupService.cs` | Limpeza manual e automática de artefatos históricos |
| Manutenção | `MaintenanceService.cs` | Auto-limpeza por RetentionDays/MaxArtifactBytes |
| Localizador automático | `AutomationToolLocator.cs` | Localiza o executável do CLI AutomationUnityBuildIOS |

### Conexão reversa

O diretório `BuildServer/Reverse/` implementa a capacidade do BuildServer de se conectar proativamente ao LinuxGateway, permitindo que nós atrás de NAT/intranet sejam agendados pelo LinuxGateway sem exposição pública.

## Arquitetura LinuxGateway

LinuxGateway não executa Unity, não armazena projetos Unity e não contém certificados Apple. Ele apenas:

1. Fornece login web e gestão de dispositivos.
2. Registra nós (conexão direta ou reversa).
3. Encaminha tarefas para o BuildServer de cada nó.
4. Faz proxy de logs e artefatos.

### Camada de serviços

| Serviço | Arquivo | Responsabilidade |
|------|------|------|
| Cliente de gateway de nó | `NodeGatewayClient.cs` | Chama os endpoints `/api/gateway/*` do BuildServer do nó |
| Atualização de nós | `NodeRefreshService.cs` | Atualiza periodicamente o status dos nós e a sincronização de projetos/configurações |
| Atualização de jobs | `JobRefreshService.cs` | Atualiza periodicamente o status, logs e artefatos de tarefas remotas |
| Atualização online | `SelfUpdateService.cs` | Verifica e baixa pacotes de atualização do Gitee/GitHub Releases |

### Conexão reversa

O diretório `LinuxGateway/Reverse/` gerencia a geração de Enrollment Tokens para conexões iniciadas pelo BuildServer, o registro de nós e a manutenção de conexões longas WebSocket.

### Atualização online

`SelfUpdateService` suporta:
- Detecção de fonte dupla (consultas paralelas de última versão Gitee + GitHub).
- Download de pacotes de atualização tar.gz.
- Geração de um script `apply-update.sh` para completar backup + substituição + reinício.
- Nenhum .NET SDK é necessário no servidor — apenas binários pré-compilados são baixados.

## Arquitetura DesktopApp

DesktopApp usa Avalonia UI 11 + .NET 8 e reutiliza toda a lógica central do projeto principal via referência de projeto:

- **InternalsVisibleTo** + **Compile Remove**: O csproj do projeto principal adiciona declarações para permitir que DesktopApp acesse membros internal enquanto exclui arquivos de ponto de entrada como Program.cs.
- **ProfileStore**: Gerencia uniformemente a persistência de quatro tipos de templates de configuração (projeto/Unity/assinatura/certificado), armazenados no diretório `profiles/`.
- **ServerSyncService**: Conecta-se à BuildServer REST API via HttpClient para sincronização bidirecional de templates e arquivos de configuração.
- **BuildRunner**: Encapsula a invocação do CLI, fornecendo saída de logs em tempo real e progresso do build.
- **AvaloniaUseCompiledBindingsByDefault=false**: Usa bindings em tempo de execução, evitando a necessidade de declarar x:DataType em cada arquivo .axaml.

Execute `scripts/verify.ps1` para verificação de regressão básica: compilação, entrada de ajuda, dry-run, abertura-fechamento do editor de configuração.
