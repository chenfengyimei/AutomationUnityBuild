# Plataforma BuildServer

BuildServer é o ponto de entrada Web/Agent da ferramenta de build automatizada, suportando iOS, Android APK/AAB e upload para Google Play. A primeira versão usa um único Mac, um único Worker e uma fila serial para evitar contenção concorrente entre Unity, Xcode, Gradle, ambientes de assinatura e o estado de cache/certificados.

## Módulos

- `BuildServer.Api`: ASP.NET Core Minimal API para login, projetos, configurações, tarefas, artefatos e auditoria.
- `BuildServer.Worker`: Worker serial em segundo plano que retira tarefas da fila e invoca o CLI `AutomationUnityBuildIOS`.
- `BuildServer.Web`: Frontend estático integrado para login web e envio de builds.
- `BuildServer.Mcp`: Endpoint de ferramentas JSON-RPC `/mcp` para Agent/AI.
- `BuildServer.Reverse`: Módulo de conexão reversa que permite ao BuildServer se conectar proativamente ao LinuxGateway, adequado para ambientes NAT/intranet.
- `buildserver-data`: Diretório de persistência JSON, armazenando usuários, projetos, configurações, tarefas, artefatos, registros de auditoria e nós Worker.

## Início local

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
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

Se `BUILD_SERVER_AGENT_TOKEN` não estiver definido, uma Agent API Key aleatória é gerada no primeiro início:

```text
<DataRoot>/initial-agent-token.txt
```

Recomendado para produção:

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

Valores de segurança padrão:

- O workspace é restrito por padrão a `~/UnityBuildWorkspace`.
- Os artefatos são restritos por padrão a `~/UnityBuildArtifacts`.
- Os arquivos de configuração são restritos por padrão ao subdiretório `configs` no diretório de dados do BuildServer e ao diretório `configs` do programa.
- Os repositórios Git permitem URLs HTTPS/SSH por padrão; em produção, defina `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS`, por exemplo `github.com` ou o domínio do servidor Git corporativo.
- Se acessar a interface web via Nginx/Caddy ou outros proxies reversos, defina `BUILD_SERVER_PUBLIC_BASE_URL` e `BUILD_SERVER_ALLOWED_ORIGINS`, caso contrário a proteção cross-site rejeitará escritas com origens não correspondentes.

## Publicação no Mac

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

Após a publicação, usar `deploy/launchd/com.automationunity.buildserver.plist` para executar como usuário `buildbot`. Certificados, perfis de provisioning, Unity License e chaves SSH Git devem ser instalados sob este usuário macOS dedicado.

## Dados obrigatórios

Após o primeiro login:

1. Adicionar um projeto: nome do projeto, repositório Git, ramo padrão, ramos permitidos, workspace e diretório de artefatos.
2. Adicionar uma configuração: selecionar iOS ou Android. Você pode referenciar um arquivo JSON de configuração existente ou marcar «Gerar novo arquivo de configuração», preencher a versão do Unity, Bundle ID e os campos específicos da plataforma no formulário web, e o servidor gerará automaticamente o JSON e o registrará.
   - Campos iOS incluem Team ID, Deployment Target, Export Method, Signing Style, cópia de archive para Organizer, upload para App Store Connect/TestFlight.
   - Campos Android incluem APK/AAB/both, versões SDK, keystore, Google Play Service Account, track, release status, artefato de upload.
3. Iniciar um build: selecionar o projeto e a configuração, enviar a tarefa.

BuildServer gera um snapshot de configuração independente para cada tarefa, reserva o Build Number e invoca o CLI:

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

## MCP/Agent

Endpoint MCP:

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

Ferramentas:

- `list_projects`
- `list_configs`
- `start_build`
- `start_ios_build` (nome herdado, novas integrações devem usar `start_build`)
- `get_build_status`
- `tail_build_log`
- `list_build_artifacts`

Por padrão, Agents só têm permissão para `dryRun=true`. Para permitir builds reais, defina o `McpClientRecord.allowFullBuild` correspondente para `true` nos dados e recomende autorizar apenas projetos específicos. MCP envia tarefas apenas por ID de projeto e configuração — não aceita repositórios Git ou caminhos arbitrários.

Novas configurações não são habilitadas para MCP por padrão; você deve marcar explicitamente «Permitir MCP» na interface web.

## Notificações por email

BuildServer inclui um serviço de notificações por email integrado (`EmailNotificationService`) que envia automaticamente emails após a conclusão de tarefas de build:

- **Build bem-sucedido**: O email inclui caminhos de artefatos, tempo decorrido e resumo da configuração.
- **Build falhou**: O email inclui a etapa falha, resumo de erros e caminho do log.

Suporta SMTP 465 SSL implícito, listas de contatos e templates de email personalizados. Configure o servidor SMTP, porta, credenciais do remetente e lista de contatos no backend web ou na página de notificações por email do DesktopApp.

## Gestão de armazenamento

À medida que as tarefas de build se acumulam, os artefatos consomem gradualmente o espaço em disco. BuildServer fornece dois mecanismos de gestão de armazenamento:

- **Limpeza automática**: `MaintenanceService` limpa automaticamente as tarefas e artefatos concluídos com base em `RetentionDays` e `MaxArtifactBytes`.
- **Limpeza manual**: Ver a visão geral do armazenamento no backend web ou na página de gestão de armazenamento do DesktopApp, exclusão em massa ou simples de artefatos históricos.

`StorageCleanupService` lida com a verificação e exclusão reais dos diretórios de artefatos.

## Conexão reversa

Se o nó BuildServer estiver atrás de NAT, uma rede doméstica ou um intranet corporativo onde LinuxGateway não pode acessá-lo diretamente, você pode usar a conexão reversa para que BuildServer se conecte proativamente ao LinuxGateway.

Gerar um Enrollment Token na interface web do LinuxGateway, depois configurar BuildServer via variáveis de ambiente:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

Após a conexão, as credenciais do nó são salvas no diretório de dados do BuildServer. O diretório `BuildServer/Reverse/` implementa a lógica do cliente de conexão reversa.

## Perímetros de segurança

- Web/MCP apenas criam tarefas — eles não executam comandos shell arbitrários.
- O Worker executa em série — apenas uma tarefa por vez.
- Projetos podem restringir os ramos permitidos.
- O CLI valida internamente as listas brancas do Git e os perímetros de caminhos.
- O download de artefatos de tarefas requer autenticação de login.
- Os logs de auditoria registram logins, criações de projetos, criações de configurações, envio/cancelamento de tarefas e registro de Workers.
- O serviço de manutenção limpa as tarefas e artefatos concluídos conforme `RetentionDays` e `MaxArtifactBytes`.
- Informações sensíveis (senhas, tokens) nas notificações por email não são exibidas — usadas apenas para autenticação SMTP.

## Extensão multi-Mac

`WorkerNodeRecord` já é persistido, e `/api/workers` e `/api/workers/register` são fornecidos. O Worker integrado da primeira versão é adequado para um único Mac; ao escalar para múltiplos Macs, a evolução recomendada é:

```text
BuildServer.Api central + Banco de dados
Mac Worker A/B/C como processos independentes
Os Workers buscam as tarefas que lhes convêm
Agendamento por versão Unity/Xcode, autorização de projeto, carga atual
```

Nesse ponto, a persistência JSON deve ser substituída por SQLite/PostgreSQL para evitar escritas de arquivo concorrentes entre máquinas.
