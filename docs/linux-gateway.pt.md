# Entrada multi-nó LinuxGateway

`LinuxGateway` é um ponto de entrada central opcional, adequado para implantação em um servidor Linux com um domínio público. Não executa Unity, não armazena projetos Unity e não contém certificados Apple; apenas gerencia login web, registro de nós de build Mac/Windows, seleção de nós e encaminhamento de tarefas para o `BuildServer` de cada nó.

LinuxGateway suporta dois modos de conexão de nó: conexão direta (LinuxGateway acessa proativamente o nó) e conexão reversa (o nó se conecta proativamente ao LinuxGateway, adequado para ambientes NAT/intranet). Inclui uma função de atualização online integrada que baixa pacotes de atualização do Gitee/GitHub Releases, sem necessidade de .NET SDK no servidor.

Sem LinuxGateway, as instâncias de `BuildServer` Mac/Windows ainda podem ser usadas de forma independente para login, configuração e builds.

## Arquitetura

```text
Usuários externos
  -> LinuxGateway Web/API
      -> Mac BuildServer /api/gateway/*    iOS + Android
      -> Windows BuildServer /api/gateway/* Android APK/AAB
```

Cada nó Mac/Windows continua executando o `BuildServer` existente, com apenas uma API adicional protegida por token ativada para as chamadas do LinuxGateway.

## Configuração de nós Mac/Windows

Definir antes de iniciar o `BuildServer` em cada nó:

```bash
export BUILD_SERVER_GATEWAY_TOKEN="token aleatório forte para este nó"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"   # Comum para Mac
```

Nó Android Windows:

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="token aleatório forte para este nó"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

Se `BUILD_SERVER_GATEWAY_TOKEN` for deixado vazio, os endpoints `/api/gateway/*` do nó não serão ativados.

LinuxGateway deve poder alcançar o endereço do nó, por exemplo:

```text
https://mac-build.example.com
https://win-build.example.com
```

Estes podem ser endereços de túnel, endereços VPN/intranet ou endpoints HTTPS públicos. HTTPS é recomendado.

## Início do LinuxGateway

Desenvolvimento:

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Depuração no Windows:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
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

## Publicação no Linux

Publicar Linux x64 a partir do Windows:

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

## Fluxo de uso

1. Iniciar `BuildServer` nos nós Mac/Windows e definir `BUILD_SERVER_GATEWAY_TOKEN`.
2. Iniciar `LinuxGateway` no Linux.
3. Fazer login na interface web do LinuxGateway.
4. Adicionar um dispositivo:
   - Nome do dispositivo: por exemplo `Mac Build`
   - URL BuildServer: por exemplo `https://mac-build.example.com`
   - Gateway Token: o `BUILD_SERVER_GATEWAY_TOKEN` do nó
   - Plataformas: Mac: `iOS + Android`, Windows: `Android`
5. Atualizar o dispositivo para confirmar que os projetos e configurações do nó estão visíveis.
6. Ao enviar um build, selecionar o dispositivo alvo, o projeto e a configuração.

## Notas de segurança

- O diretório de dados do LinuxGateway armazena os Gateway Tokens dos nós — restrinja as permissões do sistema.
- LinuxGateway deve ser exposto apenas via HTTPS; HTTP em texto plano não é recomendado.
- Os `/api/gateway/*` do nó apenas aceitam `X-Gateway-Token` — não coloque tokens em URLs.
- Os nós não devem expor o backend de administração regular à internet pública; restringir o acesso apenas ao LinuxGateway é o melhor.
- Tarefas iOS só podem ser enviadas para nós Mac que suportem `ios`; nós Windows são adequados apenas para Android APK/AAB.

## Conexão reversa

A conexão reversa é adequada quando os nós estão atrás de NAT, redes domésticas ou intranets corporativos onde LinuxGateway não pode acessar diretamente o endereço do nó. Neste caso, BuildServer se conecta proativamente ao LinuxGateway — nenhuma exposição de porta pública é necessária no lado do nó.

### Passos de configuração

1. Gerar um Enrollment Token na interface web do LinuxGateway.
2. Definir as variáveis de ambiente no nó BuildServer:

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

3. Iniciar BuildServer — ele se conectará automaticamente ao LinuxGateway e se registrará como nó de conexão reversa.
4. Após a conexão, o nó aparece na interface web do LinuxGateway.
5. Após revogar um nó, você deve gerar um novo Enrollment Token para registrar novamente.

A conexão reversa está implementada em `LinuxGateway/Reverse/` e `BuildServer/Reverse/`.

## Atualização online

LinuxGateway inclui `SelfUpdateService`, que pode verificar e baixar pacotes de atualização do Gitee ou GitHub Releases sem necessidade de .NET SDK no servidor.

### Endpoints da API

| Endpoint | Método | Descrição |
|------|------|------|
| `/api/system/version` | GET | Obter a versão atual |
| `/api/system/update/check` | GET | Verificar a última versão |
| `/api/system/update/apply` | POST | Aplicar atualização (apenas Admin) |

### Processo de atualização

1. Consultar a última versão da API Gitee/GitHub Release em paralelo.
2. Baixar o pacote de atualização tar.gz.
3. Gerar um script `apply-update.sh` para completar backup + substituição + reinício.

### Configuração

| Variável | Descrição |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | Fonte de atualização: `gitee` ou `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | Proprietário do repositório |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | Nome do repositório |

## Implantação Docker

LinuxGateway suporta implantação Docker, particularmente adequada para sistemas mais antigos como CentOS 7 onde o runtime nativo `libstdc++` pode ser muito antigo. Veja [Guia de implantação Docker](linux-gateway-docker.md).
