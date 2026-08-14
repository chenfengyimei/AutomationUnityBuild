# Plateforme BuildServer

BuildServer est le point d'entrée Web/Agent de l'outil de build automatisé, supportant iOS, Android APK/AAB, et l'upload Google Play. La première version utilise un seul Mac, un seul Worker et une file série pour éviter la contention concurrente entre Unity, Xcode, Gradle, les environnements de signature et l'état du cache/certificats.

## Modules

- `BuildServer.Api` : ASP.NET Core Minimal API pour le login, les projets, configs, tâches, artefacts et l'audit.
- `BuildServer.Worker` : Worker série en arrière-plan qui dépile les tâches et invoque le CLI `AutomationUnityBuildIOS`.
- `BuildServer.Web` : Frontend statique intégré pour le login web et la soumission de builds.
- `BuildServer.Mcp` : Endpoint d'outils JSON-RPC `/mcp` pour Agent/AI.
- `BuildServer.Reverse` : Module de connexion inverse permettant à BuildServer de se connecter proactivement à LinuxGateway, adapté aux environnements NAT/intranet.
- `buildserver-data` : Répertoire de persistance JSON, stockant les utilisateurs, projets, configs, tâches, artefacts, enregistrements d'audit et nœuds Worker.

## Démarrage local

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

Adresse par défaut :

```text
http://127.0.0.1:5088
```

Compte par défaut :

```text
admin
```

Si `BUILD_SERVER_ADMIN_PASSWORD` n'est pas défini, un mot de passe aléatoire est généré au premier démarrage :

```text
<DataRoot>/initial-admin.txt
```

Si `BUILD_SERVER_AGENT_TOKEN` n'est pas défini, une clé API Agent aléatoire est générée au premier démarrage :

```text
<DataRoot>/initial-agent-token.txt
```

Recommandé pour la production :

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

Valeurs de sécurité par défaut :

- Le workspace est restreint à `~/UnityBuildWorkspace` par défaut.
- Les artefacts sont restreints à `~/UnityBuildArtifacts` par défaut.
- Les fichiers de config sont restreints au sous-répertoire `configs` du répertoire de données BuildServer et au répertoire `configs` du programme.
- Les dépôts Git autorisent les URLs HTTPS/SSH par défaut ; en production, définir `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS`, par exemple `github.com` ou le domaine du serveur Git de l'entreprise.
- Si l'accès à l'UI web se fait via Nginx/Caddy ou autres proxies inverses, définir `BUILD_SERVER_PUBLIC_BASE_URL` et `BUILD_SERVER_ALLOWED_ORIGINS`, sinon la protection cross-site rejettera les écritures avec des origines incompatibles.

## Publication Mac

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

Après publication, utiliser `deploy/launchd/com.automationunity.buildserver.plist` pour exécuter en tant qu'utilisateur `buildbot`. Les certificats, profils de provisioning, Unity License et clés SSH Git doivent tous être installés sous cet utilisateur macOS dédié.

## Données requises

Après la première connexion :

1. Ajouter un projet : nom du projet, dépôt Git, branche par défaut, branches autorisées, workspace et répertoire d'artefacts.
2. Ajouter une config : sélectionner iOS ou Android. Vous pouvez référencer un fichier JSON existant, ou cocher « Générer un nouveau fichier de config », remplir la version Unity, le Bundle ID et les champs spécifiques à la plateforme dans le formulaire web, et le serveur générera le JSON et l'enregistrera.
   - Les champs iOS incluent Team ID, Deployment Target, Export Method, Signing Style, la copie d'archive vers Organizer, l'upload App Store Connect/TestFlight.
   - Les champs Android incluent APK/AAB/both, versions SDK, keystore, Google Play Service Account, track, release status, artefact d'upload.
3. Démarrer un build : sélectionner le projet et la config, soumettre la tâche.

BuildServer génère un snapshot de config indépendant pour chaque tâche, réserve le Build Number et invoque le CLI :

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

## MCP/Agent

Endpoint MCP :

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

Outils :

- `list_projects`
- `list_configs`
- `start_build`
- `start_ios_build` (ancien nom, les nouvelles intégrations devraient utiliser `start_build`)
- `get_build_status`
- `tail_build_log`
- `list_build_artifacts`

Par défaut, les Agents ne sont autorisés qu'avec `dryRun=true`. Pour autoriser les builds réels, définir le `McpClientRecord.allowFullBuild` correspondant à `true` dans les données, et recommander d'autoriser uniquement des projets spécifiques. MCP soumet les tâches uniquement par ID de projet et de config — il n'accepte pas de dépôts Git ou de chemins arbitraires.

Les nouvelles configs ne sont pas activées pour MCP par défaut ; vous devez explicitement cocher « Autoriser MCP » dans l'UI web.

## Notifications email

BuildServer inclut un service de notifications email intégré (`EmailNotificationService`) qui envoie automatiquement des emails après l'achèvement des tâches de build :

- **Succès du build** : L'email inclut les chemins d'artefacts, le temps écoulé et le résumé de config.
- **Échec du build** : L'email inclut l'étape échouée, le résumé d'erreur et le chemin de log.

Supporte SMTP 465 SSL implicite, listes de contacts et templates email personnalisés. Configurer le serveur SMTP, le port, les identifiants expéditeur et la liste de contacts dans le backend web ou la page notifications email de DesktopApp.

## Gestion du stockage

À mesure que les tâches de build s'accumulent, les artefacts consomment progressivement l'espace disque. BuildServer fournit deux mécanismes de gestion du stockage :

- **Nettoyage automatique** : `MaintenanceService` nettoie les tâches et artefacts terminés selon `RetentionDays` et `MaxArtifactBytes`.
- **Nettoyage manuel** : Voir la vue d'ensemble du stockage dans le backend web ou la page gestion du stockage de DesktopApp, suppression en lot ou suppression simple des artefacts historiques.

`StorageCleanupService` gère le scan et la suppression réels des répertoires d'artefacts.

## Connexion inverse

Si le nœud BuildServer est derrière NAT, un réseau domestique ou un intranet d'entreprise où LinuxGateway ne peut pas y accéder directement, vous pouvez utiliser la connexion inverse pour que BuildServer se connecte proactivement à LinuxGateway.

Générer un Enrollment Token dans l'UI web LinuxGateway, puis configurer BuildServer via des variables d'environnement :

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

Après connexion, les identifiants du nœud sont sauvegardés dans le répertoire de données BuildServer. Le répertoire `BuildServer/Reverse/` implémente la logique cliente de connexion inverse.

## Périmètres de sécurité

- Web/MCP créent uniquement des tâches — ils n'exécutent pas de commandes shell arbitraires.
- Le Worker s'exécute en série — une seule tâche à la fois.
- Les projets peuvent restreindre les branches autorisées.
- Le CLI valide en interne les listes blanches Git et les périmètres de chemins.
- Le téléchargement d'artefacts de tâche nécessite une authentification de login.
- Les logs d'audit enregistrent les logins, créations de projets, créations de configs, soumissions/annulations de tâches et enregistrements de Worker.
- Le service de maintenance nettoie les tâches et artefacts terminés selon `RetentionDays` et `MaxArtifactBytes`.
- Les informations sensibles (mots de passe, tokens) dans les notifications email ne sont pas affichées — utilisées uniquement pour l'authentification SMTP.

## Extension multi-Mac

`WorkerNodeRecord` est déjà persisté, et `/api/workers` et `/api/workers/register` sont fournis. Le Worker intégré de la première version convient à un seul Mac ; pour l'extension multi-Mac, l'évolution recommandée est :

```text
BuildServer.Api central + Base de données
Mac Worker A/B/C en processus indépendants
Les Workers tirent les tâches qui leur conviennent
Planification par version Unity/Xcode, autorisation de projet, charge actuelle
```

À ce stade, la persistance JSON devrait être remplacée par SQLite/PostgreSQL pour éviter les écritures de fichiers concurrentes inter-machines.
