# Architecture

Ce projet utilise une conception modulaire en couches, avec un découplage complet entre le moteur de build central et les points d'entrée de plateforme. CLI, BuildServer, DesktopApp et LinuxGateway partagent la même logique centrale — les différences résident uniquement dans la couche d'entrée et la méthode d'interaction.

## Responsabilités des répertoires

L'outil est organisé dans les répertoires suivants par responsabilité :

- `Cli/` : Entrée de commande, parsing d'arguments en ligne de commande, mappage des commandes raccourcies (`ShortcutCommands`).
- `ConsoleUi/` : UI console interactive, incluant l'assistant d'initialisation, l'éditeur de config et les invites de saisie.
- `Configuration/` : Modèles de config, lecture/écriture des fichiers de config, sélection de fichiers de config, résolution de chemins, configs d'exemple. Supporte les configs de plateforme `ios`, `android` et `tiktok`.
- `Workflow/` : Orchestration du pipeline de build, contexte d'exécution, mise à jour de config à l'exécution, snapshots de config.
- `Services/` : Capacités métier partagées multiplateformes, incluant la sync Git, les vérifications d'environnement, la préparation de répertoires, la validation de projet Unity et la validation de sécurité de chemins.
- `Modules/Common/` : Capacités partagées des modules de plateforme, incluant l'interface Pipeline de plateforme, la construction d'arguments de commandes Unity, le diagnostic des logs Unity et la lecture des métadonnées Unity.
- `Modules/Ios/` : Capacités de build spécifiques iOS, incluant l'export du projet Xcode Unity, la localisation project/workspace Xcode, `xcodebuild archive/export`.
- `Modules/Android/` : Capacités de build spécifiques Android, incluant les builds APK/AAB Unity, l'upload via l'API Google Play Publishing ; le sous-répertoire `GooglePlay/` gère les détails de l'API HTTP, OAuth et Service Account.
- `Modules/Tiktok/` : Capacités spécifiques TikTok Mini-Game, incluant le pipeline de build WebGL (`TiktokBuildPipeline`), le service de build (`TiktokBuildService`) et l'upload via l'API TikTok Open Platform (`TiktokUploadService`). Complètement indépendant d'iOS/Android — n'affecte pas les flux existants.
- `Infrastructure/` : Infrastructure commune, incluant le logging (`BuildLogger`), l'exécution de processus (`ProcessRunner`), les outils de chemin (`PathTools`), les périmètres de sécurité de chemin (`PathSafety`) et le masquage des données sensibles. Ces capacités sont partagées par CLI, BuildServer et DesktopApp.
- `UnityBuildScripts/Ios/` : Script de build Unity Editor iOS à copier dans `Assets/Editor` du projet Unity.
- `UnityBuildScripts/Android/` : Script de build Unity Editor Android à copier dans `Assets/Editor` du projet Unity.
- `BuildServer/` : Plateforme web de build, incluant l'API (`ApiRoutes`), le frontend intégré (`wwwroot/`), le worker en arrière-plan (`BuildWorkerService`), l'entrée MCP/Agent (`McpEndpoint`), l'API de nœud Gateway (`GatewayEndpoint`), les notifications email (`EmailNotificationService`), la gestion du stockage (`StorageCleanupService`), le scan d'artefacts (`ArtifactScanner`), le nettoyage de maintenance (`MaintenanceService`), la connexion inverse (`Reverse/`) et la persistance JSON (`Persistence/`).
- `LinuxGateway/` : Entrée unifiée multi-appareils, incluant l'API (`ApiRoutes`), le frontend intégré (`wwwroot/`), le client gateway de nœud (`NodeGatewayClient`), le rafraîchissement de nœuds (`NodeRefreshService`), le rafraîchissement de jobs (`JobRefreshService`), la gestion de connexion inverse (`Reverse/`), la mise à jour en ligne (`SelfUpdateService`) et la persistance JSON (`Persistence/`).
- `DesktopApp/` : Client desktop Avalonia UI 11, incluant Views (14 pages), ViewModels (15 view models), Services (`BuildRunner` / `ProfileStore` / `ServerSyncService`), Controls (contrôles personnalisés) et Styles (ressources de style). Référence le projet principal via `InternalsVisibleTo` + `Compile Remove` pour réutiliser toute la logique centrale.
- `deploy/` : Templates de déploiement de production, tels que plist `launchd` macOS et fichiers de déploiement Docker.

## Principes de conception clés

### Orchestration du pipeline séparée des capacités de plateforme

`AutomationWorkflow` orchestre uniquement les étapes — il ne gère pas directement les détails de Git, Unity, Xcode, Google Play ou TikTok. Lors de l'ajout de capacités de plateforme, elles doivent être placées dans le répertoire `Modules/<Platform>/` correspondant et appelées par le workflow ; les capacités multiplateformes vont dans `Services/`. Trois pipelines de plateforme sont actuellement supportés :

- `IosBuildPipeline` — Git → Unity → Xcode archive/export → upload ASC
- `AndroidBuildPipeline` — Git → Unity → APK/AAB → upload Google Play
- `TiktokBuildPipeline` — Git → Unity → WebGL → upload TikTok Open Platform

### Éditeur de config piloté par les champs

L'éditeur de config utilise une liste de descripteurs de champs pour piloter le menu et la logique de modification. Lors de l'ajout de champs de config, ajouter d'abord une entrée à la liste de champs de `ConfigEditor`, évitant la dispersion de l'affichage du menu et de la logique de modification switch-case.

### Fondations de sécurité

Lors de la connexion aux backends web, workers ou MCP/Agent, tous les points d'entrée doivent réutiliser les capacités préexistantes déjà implémentées dans le CLI :

- `PathSafetyValidator` : Valide que le workspace, les répertoires de dépôt, les projets Unity, les artefacts, les logs, les sorties Xcode et archive/export sont tous dans des répertoires racines autorisés.
- `GitRepositoryPolicyValidator` : Valide le format d'URL Git et la liste blanche `allowedRepositoryUrls`.
- `BuildConfigSnapshotWriter` : Génère `Logs/build-config-snapshot.json` à chaque exécution réelle, enregistrant le snapshot de config, les chemins résolus et les arguments CLI.
- `SensitiveText` : Masque uniformément les tokens/mots de passe courants dans les logs, commandes, stdout/stderr et snapshots de config.

Ces capacités ne doivent pas être limitées à la couche Web/API. Le Worker doit aussi les invoquer avant d'exécuter les builds, pour empêcher de contourner les points d'entrée et de déclencher des configs dangereuses directement.

## Architecture BuildServer

BuildServer est le point d'entrée Web/Agent du CLI, avec la conception suivante :

### File série

Le design mono-machine, mono-worker, file série est intentionnel : Unity, Xcode, Gradle, les certificats de signature et les répertoires de cache ne tolèrent généralement pas la contention concurrente sur la même machine. L'extension multi-machines est gérée par LinuxGateway.

### Couche de services

| Service | Fichier | Responsabilité |
|------|------|------|
| File de tâches | `BuildQueueService.cs` | Gère l'enqueue, dequeue et les transitions d'état des tâches de build |
| Worker en arrière-plan | `BuildWorkerService.cs` | Consomme la file en série, invoque le CLI pour les builds |
| Notifications email | `EmailNotificationService.cs` | Envoie les notifications email succès/échec après les builds |
| Scanner d'artefacts | `ArtifactScanner.cs` | Scanne les répertoires d'artefacts de tâche, génère des listes d'artefacts |
| Lecteur de logs | `LogFileReader.cs` | Lit et tail les logs de tâche |
| Nettoyage de stockage | `StorageCleanupService.cs` | Nettoyage manuel et automatique des artefacts historiques |
| Maintenance | `MaintenanceService.cs` | Auto-nettoyage par RetentionDays/MaxArtifactBytes |
| Localisateur auto | `AutomationToolLocator.cs` | Localise l'exécutable CLI AutomationUnityBuildIOS |

### Connexion inverse

Le répertoire `BuildServer/Reverse/` implémente la capacité de BuildServer à se connecter proactivement à LinuxGateway, permettant aux nœuds derrière NAT/intranet d'être planifiés par LinuxGateway sans exposition publique.

## Architecture LinuxGateway

LinuxGateway n'exécute pas Unity, ne stocke pas de projets Unity et ne détient pas de certificats Apple. Il ne fait que :

1. Fournir le login web et la gestion de devices.
2. Enregistrer des nœuds (connexion directe ou inverse).
3. Transférer des tâches au BuildServer de chaque nœud.
4. Proxyer les logs et artefacts.

### Couche de services

| Service | Fichier | Responsabilité |
|------|------|------|
| Client gateway de nœud | `NodeGatewayClient.cs` | Appelle les endpoints `/api/gateway/*` du BuildServer de nœud |
| Rafraîchissement de nœuds | `NodeRefreshService.cs` | Rafraîchit périodiquement le statut des nœuds et la sync projets/configs |
| Rafraîchissement de jobs | `JobRefreshService.cs` | Rafraîchit périodiquement le statut, les logs et les artefacts des tâches distantes |
| Mise à jour en ligne | `SelfUpdateService.cs` | Vérifie et télécharge les packages de mise à jour depuis Gitee/GitHub Releases |

### Connexion inverse

Le répertoire `LinuxGateway/Reverse/` gère la génération d'Enrollment Tokens pour les connexions initiées par BuildServer, l'enregistrement des nœuds et la maintenance des connexions longues WebSocket.

### Mise à jour en ligne

`SelfUpdateService` supporte :
- La détection double source (requêtes parallèles Gitee + GitHub pour la dernière version).
- Le téléchargement de packages de mise à jour tar.gz.
- La génération d'un script `apply-update.sh` pour compléter sauvegarde + remplacement + redémarrage.
- Aucun .NET SDK requis sur le serveur — seuls les binaires précompilés sont téléchargés.

## Architecture DesktopApp

DesktopApp utilise Avalonia UI 11 + .NET 8 et réutilise toute la logique centrale du projet principal via une référence de projet :

- **InternalsVisibleTo** + **Compile Remove** : Le csproj du projet principal ajoute des déclarations pour permettre à DesktopApp d'accéder aux membres internal tout en excluant les fichiers de point d'entrée comme Program.cs.
- **ProfileStore** : Gère uniformément la persistance de quatre types de templates de config (projet/Unity/signature/certificat), stockés dans le répertoire `profiles/`.
- **ServerSyncService** : Se connecte à l'API REST BuildServer via HttpClient pour la sync bidirectionnelle des templates et fichiers de config.
- **BuildRunner** : Encapsule l'invocation du CLI, fournissant une sortie de log en temps réel et la progression du build.
- **AvaloniaUseCompiledBindingsByDefault=false** : Utilise les bindings à l'exécution, évitant la nécessité de déclarer x:DataType sur chaque fichier .axaml.

Exécuter `scripts/verify.ps1` pour une vérification de régression de base : compilation, entrée d'aide, dry-run, ouverture-fermeture de l'éditeur de config.
