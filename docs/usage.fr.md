# Guide d'utilisation

Ce document couvre tous les chemins d'utilisation d'AutomationUnityBuildIOS : CLI local, builds iOS, builds Android, builds TikTok Mini-Game, uploads vers les stores, client desktop DesktopApp, plateforme web BuildServer, notifications email, gestion du stockage, gestion des templates, entrée MCP/Agent et planification multi-nœuds LinuxGateway.

Si vous êtes nouveau, nous recommandons de suivre cet ordre :

1. Préparez votre environnement de build Mac/Windows.
2. Copiez les scripts de build Unity dans votre projet Unity.
3. Générez une config et faites un dry-run sur Mac avec le CLI.
4. Faites un vrai build.
5. Déployez BuildServer quand votre équipe a besoin d'une entrée web.
6. Déployez LinuxGateway quand plusieurs machines de build nécessitent une entrée unifiée.

---

## Choix du mode

| Scénario | Mode recommandé | Notes |
|------|----------|------|
| Build de packages iOS sur votre propre Mac | CLI | Composants minimaux, exécuter `./AutomationUnityBuildIOS 06` |
| iOS + Android automatisés | CLI ou BuildServer | CLI pour solo, BuildServer pour équipes |
| Build & upload WebGL TikTok Mini-Game | CLI | Utiliser le raccourci `12` pour générer une config TikTok |
| Gestion de config hors-ligne et builds sur Windows | DesktopApp | Client desktop natif, édition de config complète, exécution de builds, navigation d'artefacts |
| Besoin QA/ops d'un build par clic | BuildServer | Login navigateur, soumission de tâches, visualisation des logs, téléchargement d'artefacts |
| Plusieurs machines de build Mac/Windows | LinuxGateway + BuildServer | LinuxGateway comme entrée unifiée ; les builds s'exécutent sur chaque nœud BuildServer |
| Nœuds derrière NAT/intranet, inaccessibles de l'extérieur | LinuxGateway connexion inverse | Les nœuds se connectent à LinuxGateway, pas d'IP publique ni de mappage de port requis |
| Laisser les AI Agents participer au build | BuildServer MCP | Les Agents font des dry-runs par défaut ; les builds réels nécessitent une autorisation |

---

## Configuration de l'environnement

### Machine de développement

Construire et publier cet outil nécessite :

- .NET 8 SDK.
- Windows, macOS ou Linux peuvent tous compiler ce projet.
- Si vous utilisez Visual Studio, VS 2022 ou ultérieur est recommandé.

Vérification de base :

```powershell
dotnet --version
dotnet build .\AutomationUnityBuildIOS.sln
```

### Machine de build iOS

Le build iOS final doit s'exécuter sur macOS, car Unity iOS Build Support et Xcode ne sont disponibles que sur Mac.

Prérequis Mac :

- Xcode, ouvert au moins une fois pour accepter la licence et installer les composants.
- Unity Hub, la version Unity Editor correspondante, et le module iOS Build Support.
- Git CLI, avec le Mac capable d'accéder à votre dépôt Unity. Clé SSH recommandée.
- Compte Apple Developer, certificats, profils de provisioning, ou signature automatique Xcode.
- Si vous n'utilisez pas un package de publication self-contained, .NET 8 SDK doit aussi être installé sur le Mac.

Commandes de vérification :

```bash
git --version
xcodebuild -version
/Applications/Unity/Hub/Editor/<UnityVersion>/Unity.app/Contents/MacOS/Unity -version
```

### Machine de build Android

Les builds Android peuvent s'exécuter sur macOS ou Windows.

Prérequis :

- Unity Hub, la version Unity Editor correspondante, et Android Build Support.
- Android SDK, NDK, OpenJDK intégrés à Unity, ou votre propre chaîne d'outils Android.
- Un keystore Android pour signer les packages release.
- Un Service Account JSON Google Play Console avec permissions de publication pour l'app cible, si upload vers Google Play.

---

## Préparation du projet Unity

Cet outil invoque les scripts Unity Editor via `-executeMethod`, votre dépôt de jeu Unity doit donc contenir les scripts de build fournis par ce projet.

iOS :

```text
UnityBuildScripts/Ios/BuildIOS.cs
```

Copier dans votre projet Unity :

```text
Assets/Editor/BuildIOS.cs
```

Méthode fournie :

```text
BuildAutomation.IOSBuilder.Build
```

Android :

```text
UnityBuildScripts/Android/BuildAndroid.cs
```

Copier dans votre projet Unity :

```text
Assets/Editor/BuildAndroid.cs
```

Méthode fournie :

```text
BuildAutomation.AndroidBuilder.Build
```

Après la mise à jour d'AutomationUnityBuildIOS, si ces scripts ont changé, synchronisez-les avec votre dépôt de jeu Unity.

---

## Démarrage rapide CLI local

### Publication du CLI Mac depuis une machine de dev

Mac Apple Silicon :

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-arm64
```

Mac Intel :

```powershell
.\scripts\publish-mac.ps1 -Runtime osx-x64
```

La sortie publiée sera dans :

```text
publish/osx-arm64
publish/osx-x64
```

Copiez tout le répertoire sur votre Mac, par exemple :

```text
~/Downloads/publish_m1
```

### Première exécution sur Mac

Si macOS avertit d'un développeur non identifié ou d'un logiciel non vérifié, exécutez ce qui suit dans le répertoire de publication :

```bash
cd ~/Downloads/publish_m1
xattr -cr .
chmod +x ./AutomationUnityBuildIOS
codesign --force --deep --sign - ./AutomationUnityBuildIOS
./AutomationUnityBuildIOS 00
```

`00` affiche l'aide et la table des commandes raccourcies.

### Création d'une config

Assistant de config iOS interactif :

```bash
./AutomationUnityBuildIOS 01
```

Commande complète équivalente :

```bash
./AutomationUnityBuildIOS init-config
```

Générer un template iOS vide :

```bash
./AutomationUnityBuildIOS init-config --config build-ios.json --template
```

Générer un template Android vide :

```bash
./AutomationUnityBuildIOS 11
```

Commande complète équivalente :

```bash
./AutomationUnityBuildIOS init-config --config build-android.json --template --platform android
```

Il est recommandé de stocker les configs de production sous `configs/`, par exemple :

```text
configs/build-ios.dev.json
configs/build-ios.testflight.json
configs/build-android.internal.json
```

### Vérification de l'environnement

Sélectionner une config et vérifier l'environnement :

```bash
./AutomationUnityBuildIOS 04
```

Spécifier une config :

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

Pour le débogage de config ou les dry-runs sur Windows, ajouter :

```bash
--allow-non-mac
```

Les builds iOS de production doivent toujours s'exécuter sur macOS.

### Aperçu des commandes

Aperçu du pipeline sans exécution :

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
```

Commande complète équivalente :

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --dry-run --verbose --allow-non-mac
```

### Build réel

Sélectionner une config existante et exécuter le pipeline complet :

```bash
./AutomationUnityBuildIOS 06
```

Spécifier une config :

```bash
./AutomationUnityBuildIOS 06 --config configs/build-ios.dev.json
```

Commande complète :

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json
```

### Flags de saut courants

| Flag | Effet |
|------|------|
| `--skip-git` | Sauter le pull/reset Git, utiliser le projet existant dans le workspace |
| `--skip-unity` | Sauter l'export Unity ou le build Android |
| `--skip-xcode` | Sauter Xcode archive/export (iOS uniquement ; ignoré pour Android) |
| `--dry-run` | Afficher les commandes sans exécuter les builds ou uploads |
| `--verbose` | Sortie de chemins et commandes plus détaillée |
| `--allow-non-mac` | Autoriser les dry-runs iOS ou le débogage de config sur non-macOS |

### Table des commandes raccourcies

| Code | Description |
|------|------|
| `00` | Afficher l'aide et la table des raccourcis |
| `01` | Assistant de config interactif, génère un fichier de config prêt à l'emploi |
| `02` | Générer un template de config iOS vide `build-ios.json` |
| `03` | Lister les fichiers de config existants |
| `04` | Sélectionner une config et vérifier l'environnement |
| `05` | Sélectionner une config et prévisualiser la commande de build complète (dry-run) |
| `06` | Sélectionner une config et exécuter le pipeline de build complet |
| `07` | Sélectionner une config et builder, en sautant la sync Git |
| `08` | Sélectionner une config et builder, en sautant l'export Unity |
| `09` | Sélectionner une config et builder, en sautant la compilation/export Xcode |
| `10` | Sélectionner une config et éditer son contenu |
| `11` | Générer un template de config Android APK/AAB `build-android.json` |
| `12` | Générer un template de config TikTok Mini-Game `build-tiktok.json` |

Les raccourcis peuvent être suivis d'arguments supplémentaires :

```bash
./AutomationUnityBuildIOS 05 --config configs/build-ios.dev.json
./AutomationUnityBuildIOS 06 --config configs/build-ios.release.json
./AutomationUnityBuildIOS 10 --config configs/build-android.internal.json
```

---

## Référence des fichiers de config

Les fichiers de config sont en JSON. Voir `build-ios.sample.json` pour iOS, `build-android.sample.json` pour Android, et `build-tiktok.sample.json` pour TikTok.

### Champs communs

| Champ | Description |
|------|------|
| `configName` | Nom d'affichage de la config, montré dans les listes de sélection |
| `buildPlatform` | `ios`, `android`, ou `tiktok` |
| `repositoryUrl` | URL de clone Git pour le dépôt Unity, supporte HTTPS/SSH |
| `allowedRepositoryUrls` | Liste blanche de dépôts, recommandé pour la production |
| `branch` | Branche de build |
| `workspaceRoot` | Répertoire racine du workspace Git |
| `allowedWorkspaceRoots` | Répertoires racines de workspace autorisés, prévient l'évasion de chemin |
| `projectDirectoryName` | Nom du répertoire après le clone du dépôt |
| `unityProjectRelativePath` | Chemin du projet Unity relatif à la racine du dépôt ; utiliser `.` si la racine du dépôt est le projet Unity |
| `unityVersion` | Version installée de Unity Hub, utilisée pour déduire le chemin de l'exécutable Unity |
| `unityExecutablePath` | Chemin complet de l'exécutable Unity ; prioritaire sur `unityVersion` |
| `unityBuildMethod` | Nom de la méthode statique Unity Editor |
| `artifactsRoot` | Répertoire racine des artefacts de build |
| `allowedArtifactsRoots` | Répertoires racines d'artefacts autorisés |
| `productName` | Unity Product Name |
| `bundleIdentifier` | iOS Bundle ID ou Android Package Name |
| `bundleVersion` | Numéro de version |
| `syncBundleVersionFromUnity` | Synchroniser la version depuis Unity PlayerSettings |
| `buildNumber` | iOS Build Number ou Android versionCode |
| `autoIncrementBuildNumber` | Incrémenter automatiquement le build number après un build réussi |
| `saveConfigSnapshot` | Sauvegarder un snapshot de config dans le répertoire de logs |

Les trois valeurs les plus souvent mal configurées :

```text
repositoryUrl : Utiliser l'URL de clone git, pas le titre de la page web.
unityProjectRelativePath : Généralement ".", pas build, Builds ou XcodeProject.
teamId : iOS utilise le Team ID Apple Developer à 10 caractères, pas le nom de l'entreprise.
```

### Champs iOS

| Champ | Description |
|------|------|
| `scheme` | Défaut `Unity-iPhone` |
| `configuration` | Défaut `Release` |
| `exportMethod` | `development`, `ad-hoc`, `app-store`, etc. (méthode d'export Xcode) |
| `teamId` | Apple Developer Team ID, doit être 10 caractères alphanumériques |
| `signingStyle` | `automatic` ou `manual` |
| `iosDeploymentTarget` | Version iOS minimale, par exemple `13.0` |
| `allowProvisioningUpdates` | Autoriser Xcode à gérer les mises à jour de signature automatiquement |
| `generateExportOptionsPlist` | Générer automatiquement `ExportOptions.plist` |
| `copyArchiveToOrganizer` | Copier `.xcarchive` vers Xcode Organizer |
| `appStoreConnectUploadEnabled` | Uploader automatiquement vers App Store Connect/TestFlight |

### Champs Android

| Champ | Description |
|------|------|
| `androidBuildFormat` | `apk`, `aab`, ou `both` |
| `androidOutputDirectory` | Répertoire de sortie Android, auto-généré si vide |
| `apkOutputPath` | Chemin de sortie APK, auto-généré si vide |
| `aabOutputPath` | Chemin de sortie AAB, auto-généré si vide |
| `androidMinSdkVersion` | Optionnel, écrase Min SDK |
| `androidTargetSdkVersion` | Optionnel, écrase Target SDK |
| `androidKeystoreName` | Chemin ou nom du keystore |
| `androidKeystorePass` | Mot de passe du keystore |
| `androidKeyaliasName` | Key alias |
| `androidKeyaliasPass` | Mot de passe du key alias |
| `googlePlayUploadEnabled` | Uploader vers Google Play |
| `googlePlayTrack` | `internal`, `alpha`, `beta`, `production` |
| `googlePlayReleaseStatus` | `draft`, `inProgress`, `halted`, `completed` |
| `googlePlayUploadArtifact` | Uploader `apk`, `aab`, ou `both` |

Ne committez jamais de certificats, clés privées ou tokens à long terme dans le dépôt. Quand les configs doivent référencer des secrets, privilégiez les chemins locaux sur la machine de build et protégez les permissions des fichiers.

### Champs TikTok

| Champ | Description |
|------|------|
| `tiktokAppId` | TikTok Open Platform App ID |
| `tiktokAccessToken` | TikTok Open Platform Access Token |
| `tiktokGameName` | Nom du TikTok Mini-Game |
| `tiktokWebglOutputDirectory` | Répertoire de sortie WebGL, auto-généré si vide |
| `tiktokUploadEnabled` | Uploader automatiquement vers TikTok Open Platform |
| `tiktokApiEndpoint` | URL de l'API TikTok Open Platform, défaut `https://open-api.tiktokglobalshop.com` |

---

## Build iOS

### Pipeline de base

Le pipeline iOS complet :

1. Validation des périmètres de sécurité de config et de la politique de dépôt Git.
2. Vérification de `git`, Unity, `xcodebuild`.
3. Création du répertoire d'exécution et du répertoire de logs.
4. Écriture de `build-config-snapshot.json`.
5. Pull ou mise à jour du dépôt Unity.
6. Invocation de Unity BatchMode pour exporter le projet Xcode iOS.
7. Exécution de `xcodebuild archive`.
8. Exécution de `xcodebuild -exportArchive`.
9. Copie optionnelle de `.xcarchive` vers Xcode Organizer.
10. Upload optionnel vers App Store Connect/TestFlight.

### Upload App Store Connect / TestFlight

Activer l'upload automatique nécessite `exportMethod` réglé sur `app-store` et une App Store Connect API Key configurée.

Exemple :

```json
{
  "exportMethod": "app-store",
  "appStoreConnectUploadEnabled": true,
  "appStoreConnectApiKeyPath": "~/Secrets/AuthKey_XXXXXXXXXX.p8",
  "appStoreConnectApiKeyId": "XXXXXXXXXX",
  "appStoreConnectApiIssuerId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
}
```

Notes :

- Le fichier `.p8` doit exister localement sur la machine de build Mac.
- Key ID et Issuer ID proviennent de la page App Store Connect API Key.
- Après un upload réussi, le build entre dans la file de traitement App Store Connect/TestFlight.
- La soumission pour review ou la release en production suit les politiques de version d'App Store Connect.

### Méthodes de débogage iOS courantes

Sync Git et Unity uniquement, sauter Xcode :

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-xcode
```

Sauter Unity, réutiliser le projet Xcode existant pour archive/export :

```bash
./AutomationUnityBuildIOS run --config configs/build-ios.dev.json --skip-unity
```

Vérifier uniquement la config et l'environnement :

```bash
./AutomationUnityBuildIOS doctor --config configs/build-ios.dev.json
```

---

## Build Android

### Pipeline de base

Le pipeline Android complet :

1. Validation des périmètres de sécurité de config et de la politique de dépôt Git.
2. Vérification de `git` et Unity.
3. Création du répertoire d'exécution et du répertoire de logs.
4. Écriture de `build-config-snapshot.json`.
5. Pull ou mise à jour du dépôt Unity.
6. Invocation de Unity BatchMode pour build APK/AAB.
7. Upload optionnel vers Google Play.

Android ne nécessite pas Xcode ; `--skip-xcode` est ignoré.

### Build APK/AAB

Config :

```json
{
  "buildPlatform": "android",
  "unityBuildMethod": "BuildAutomation.AndroidBuilder.Build",
  "androidBuildFormat": "both"
}
```

Options `androidBuildFormat` :

| Valeur | Résultat |
|-------|--------|
| `apk` | Générer APK uniquement |
| `aab` | Générer AAB uniquement |
| `both` | Générer à la fois APK et AAB |

### Upload Google Play

Vous devez créer un Service Account dans Google Play Console et accorder les permissions de publication pour l'app cible.

Exemple :

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

Recommandé : dry-run d'abord :

```bash
./AutomationUnityBuildIOS run --config configs/build-android.internal.json --dry-run --verbose
```

Vérifiez les chemins, le nom de package, la version et l'artefact d'upload avant d'exécuter le build réel.

---

## Build TikTok Mini-Game

### Pipeline de base

Le pipeline de build TikTok Mini-Game :

1. Validation des périmètres de sécurité de config et de la politique de dépôt Git.
2. Vérification de `git` et Unity.
3. Création du répertoire d'exécution et du répertoire de logs.
4. Écriture de `build-config-snapshot.json`.
5. Pull ou mise à jour du dépôt Unity.
6. Invocation de Unity BatchMode pour build WebGL.
7. Upload optionnel vers TikTok Open Platform.

Les builds TikTok ne nécessitent pas Xcode ; `--skip-xcode` est ignoré.

### Génération de config

```bash
./AutomationUnityBuildIOS 12
```

Commande complète équivalente :

```bash
./AutomationUnityBuildIOS init-config --config build-tiktok.json --template --platform tiktok
```

### Exemple de config

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

### Build réel

```bash
./AutomationUnityBuildIOS run --config configs/build-tiktok.release.json
```

Le code lié à TikTok se trouve dans `Modules/Tiktok/`, complètement indépendant d'iOS/Android et n'affectant pas les flux de build existants.

---

## Client desktop

DesktopApp est un client desktop Windows natif basé sur Avalonia UI 11 + .NET 8, réutilisant toute la logique centrale du projet principal (AutomationWorkflow / BuildConfig / ConfigFileSelector / SampleFiles). Il intègre les capacités CLI, BuildServer et gestion de templates dans une seule application desktop avec support hors-ligne complet.

### Pages de fonctionnalités

| Page | Fonctionnalités |
|------|----------|
| **Gestion de config** | Édition complète des champs iOS/Android/TikTok, sync auto du nom de fichier de config, remplissage de template en un clic |
| **Tâche de build** | Tail de logs en temps réel, minuteur, effacement des logs, défilement automatique |
| **Vérification d'environnement** | Vérifier Unity, Git, Xcode et autres dépendances |
| **Navigateur d'artefacts** | Liste de fichiers, sélection, double-clic pour ouvrir, aperçu |
| **Gestion du stockage** | Suppression en lot avec cases à cocher, suppression simple, tout sélectionner, vue d'ensemble |
| **Notifications email** | Config SMTP (incluant 465 SSL implicite), liste de contacts, templates |
| **Profil de projet** | Template ProjectProfile, gère dépôt/répertoires de workspace |
| **Profil Unity** | Template UnityProfile, gère version/chemin Unity/BuildMethod/ProductName/BundleID |
| **Profil de signature** | Template SigningProfile, gère iOS TeamID/ExportMethod/SigningStyle/Android Keystore |
| **Profil de certificat** | Template CertificateProfile, gère ASC API Key/Google Play/TikTok Token |
| **Sync serveur** | Connexion à l'API REST BuildServer, sync bidirectionnelle des templates et configs |
| **Gestionnaire BuildServer** | Détection auto ou sélection manuelle du chemin BuildServer.exe, démarrage/arrêt en un clic, health check |
| **Gestion de données** | Export des types de données en JSON, import JSON avec fusion dédupliquée par ID |
| **Aide** | Guide d'utilisation et référence des raccourcis |

### Publication de DesktopApp

```powershell
dotnet publish DesktopApp/DesktopApp.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:IncludeAllContentForSelfExtract=true -o DesktopApp/bin/publish-vN
```

Si l'exe précédent est encore en cours d'exécution, vous obtiendrez une `UnauthorizedAccessException`. Arrêtez-le d'abord :

```powershell
Stop-Process -Name DesktopApp -Force
```

Puis publiez dans un nouveau répertoire. La sortie en fichier unique fait environ 89 Mo.

Vous pouvez aussi utiliser le script de publication :

```powershell
.\scripts\publish-desktop.ps1
```

### Gestion des templates

DesktopApp fournit quatre types de templates de configuration, stockés dans le répertoire `profiles/` :

| Template | Fichier | Utilité |
|------|------|------|
| Profil de projet | `projects.json` | URL du dépôt, répertoires de workspace et d'artefacts, etc. |
| Profil Unity | `unity-profiles.json` | Version Unity, chemin, BuildMethod, ProductName, BundleID |
| Profil de signature | `signing-profiles.json` | iOS TeamID, ExportMethod, SigningStyle, Android Keystore |
| Profil de certificat | `certificates.json` | ASC API Key, Google Play Service Account, TikTok Token |

En haut du formulaire d'édition de la page de gestion de config, il y a quatre sélecteurs de templates. Choisissez-en un de chaque et cliquez sur « Appliquer » pour remplir les champs correspondants en un clic. Après l'application d'un template, les sections de champs remplies sont automatiquement masquées pour réduire l'encombrement.

### Sync serveur

DesktopApp peut se connecter à l'API REST BuildServer pour une sync bidirectionnelle :

- **Templates de projet** : Pull / push
- **Templates de certificat** : Pull / push
- **Fichiers de config** : Parcourir la liste des configs serveur + télécharger vers le répertoire `configs/` local

Les infos de connexion sont persistées dans `profiles/server-settings.json`.

La page de gestion de config fournit aussi un bouton « Importer un fichier de config » pour importer un JSON depuis n'importe quel chemin local vers `configs/`.

---

## Notifications email

BuildServer supporte les notifications email automatiques après l'achèvement des tâches de build, couvrant à la fois les succès et les échecs.

### Configuration

Configurer dans le backend web BuildServer ou la page notifications email de DesktopApp :

| Champ | Description |
|------|------|
| Serveur SMTP | par exemple `smtp.gmail.com`, `smtp.qq.com` |
| Port SMTP | Commun : 25 (plaintext), 465 (SSL implicite), 587 (STARTTLS) |
| Email expéditeur | Adresse email envoyant les notifications |
| Mot de passe expéditeur | Code d'autorisation ou mot de passe email |
| Activer SSL | Le port 465 utilise SSL implicite |
| Contacts de notification | Liste d'emails destinataires, séparés par virgules ou retours à la ligne |
| Template email | Sujet et corps d'email personnalisés |

### Déclencheurs de notification

- **Succès du build** : L'email inclut les chemins d'artefacts, le temps écoulé et le résumé de config.
- **Échec du build** : L'email inclut l'étape échouée, le résumé d'erreur et le chemin de log pour un dépannage rapide.

Le service de notifications email est implémenté dans `BuildServer/Services/EmailNotificationService.cs`.

---

## Gestion du stockage

À mesure que les tâches de build s'accumulent, les artefacts consomment progressivement l'espace disque. BuildServer fournit deux mécanismes de gestion du stockage :

### Nettoyage automatique

`MaintenanceService` nettoie automatiquement les tâches et artefacts terminés selon les `RetentionDays` et `MaxArtifactBytes` configurés.

### Nettoyage manuel

Dans le backend web ou la page de gestion du stockage de DesktopApp, vous pouvez :

- Voir la vue d'ensemble du stockage (espace total, utilisé, nombre de tâches, distribution de taille des artefacts).
- Sélectionner plusieurs tâches historiques pour suppression en lot.
- Supprimer les artefacts d'une seule tâche.
- Tout sélectionner pour effacer tous les artefacts historiques.

Le service de nettoyage du stockage est implémenté dans `BuildServer/Services/StorageCleanupService.cs`.

---

## Logs et artefacts

Chaque exécution crée un répertoire indépendant sous `artifactsRoot`, par exemple :

```text
~/UnityBuildArtifacts/YourUnityGame/20260625-153000/
```

Contenus courants :

| Fichier ou répertoire | Description |
|------------|------|
| `Logs/automation.log` | Log principal du pipeline, inclut étapes, commandes, temps écoulé et erreurs |
| `Logs/unity-editor.log` | Log de build de Unity Editor lui-même |
| `Logs/unity-process.log` | stdout/stderr capturé depuis le processus Unity |
| `Logs/build-config-snapshot.json` | Snapshot de config pour cette exécution, avec masquage de base |
| `Logs/xcode-archive.log` | Log d'archive iOS |
| `Logs/xcode-export.log` | Log d'export iOS |
| `Logs/xcode-upload.log` | Log d'upload App Store Connect |
| `.xcarchive` | Artefact d'archive iOS |
| Répertoire d'export `.ipa` | Artefact d'export iOS |
| `.apk` / `.aab` | Artefacts de build Android |

Ordre de dépannage :

1. Vérifier d'abord la fin de `automation.log` pour l'étape échouée.
2. Si l'étape Unity a échoué, vérifier `unity-editor.log`.
3. Si l'étape Xcode iOS a échoué, vérifier `xcode-archive.log` ou `xcode-export.log`.
4. Si l'upload store a échoué, vérifier `xcode-upload.log` ou l'erreur d'upload Google Play dans le log principal.

Le système de logging applique un masquage de base aux informations sensibles courantes, telles que les identifiants/tokens dans les URLs, les tokens `Bearer`, et les valeurs pour les clés comme `password/token/secret/apiKey`.

---

## Plateforme web BuildServer

BuildServer est le point d'entrée Web/Agent du CLI. Il fournit :

- Login web.
- Gestion de projets.
- Gestion de config.
- File de tâches de build.
- Logs en temps réel.
- Téléchargement d'artefacts.
- Permissions utilisateur.
- Logs d'audit.
- Outils MCP/Agent.
- API de nœud LinuxGateway.

La première version utilise une file série mono-machine, mono-worker pour éviter la contention concurrente entre Unity, Xcode, Gradle, les environnements de signature et les répertoires de cache.

### Démarrage local

Débogage Windows :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-build-server.ps1
```

Débogage macOS/Linux :

```bash
./scripts/run-build-server.sh
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

Si `BUILD_SERVER_AGENT_TOKEN` n'est pas défini, un token MCP Agent par défaut est généré au premier démarrage :

```text
<DataRoot>/initial-agent-token.txt
```

### Variables d'environnement de production

Recommandé pour la production :

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

Variables courantes :

| Variable | Description |
|------|------|
| `BUILD_SERVER_DATA_ROOT` | Répertoire de données, stocke utilisateurs, projets, configs, tâches, audit JSON |
| `BUILD_SERVER_ADMIN_PASSWORD` | Mot de passe admin |
| `BUILD_SERVER_AGENT_TOKEN` | Token MCP Agent |
| `BUILD_SERVER_PUBLIC_BASE_URL` | URL publique |
| `BUILD_SERVER_ALLOWED_ORIGINS` | Origins web autorisés ; recommandé derrière un proxy inverse |
| `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS` | Répertoires racines de workspace autorisés |
| `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS` | Répertoires racines d'artefacts autorisés |
| `BUILD_SERVER_ALLOWED_CONFIG_ROOTS` | Répertoires racines de fichiers de config autorisés |
| `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` | Hôtes Git autorisés à l'enregistrement |
| `BUILD_SERVER_GATEWAY_TOKEN` | Token API de nœud ; auto-génère `initial-gateway-token.txt` au premier démarrage si vide |
| `BUILD_SERVER_NODE_PLATFORMS` | Capacités du nœud actuel, par exemple `ios,android` ou `android` |

### Flux d'utilisation web

Après la première connexion au backend :

1. Ajouter un projet : nom du projet, dépôt Git, branche par défaut, branches autorisées, workspace et répertoire d'artefacts.
2. Ajouter une config : sélectionner iOS ou Android.
3. Les configs peuvent pointer vers un fichier JSON existant ou être générées depuis le formulaire web.
4. Démarrer un build : sélectionner projet, config, branche et paramètres optionnels.
5. Voir le statut, les logs en temps réel et les artefacts dans la liste des tâches.

BuildServer génère un snapshot de config indépendant pour chaque tâche et invoque le CLI :

```text
AutomationUnityBuildIOS run --config <job-config.json>
```

### Publication de BuildServer vers Mac

Mac Apple Silicon :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-arm64
```

Mac Intel :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-build-server-mac.ps1 -Runtime osx-x64
```

Le répertoire de publication inclut à la fois BuildServer et le CLI AutomationUnityBuildIOS. Pour la production, utiliser :

```text
deploy/launchd/com.automationunity.buildserver.plist
```

Il est recommandé de désigner un utilisateur macOS dédié pour exécuter BuildServer, avec Unity License, signature Xcode, certificats, profils de provisioning et clés SSH Git tous configurés sous cet utilisateur.

### MCP / Agent

Endpoint MCP :

```text
POST /mcp
Header: X-Agent-Token: <BUILD_SERVER_AGENT_TOKEN>
```

Outils supportés :

| Outil | Description |
|------|------|
| `list_projects` | Lister les projets disponibles |
| `list_configs` | Lister les configs de build sous un projet |
| `start_build` | Soumettre une tâche de build iOS ou Android |
| `start_ios_build` | Ancien nom, les nouvelles intégrations devraient utiliser `start_build` |
| `get_build_status` | Interroger le statut d'une tâche de build |
| `tail_build_log` | Lire les dernières lignes de log |
| `list_build_artifacts` | Lister les artefacts d'une tâche |

Par défaut, les Agents ne sont autorisés qu'avec `dryRun=true`. Pour autoriser les builds réels, activer `allowFullBuild` pour le MCP Client correspondant, et recommander d'autoriser uniquement des projets spécifiques.

Ne pas mettre de tokens Agent dans les paramètres d'URL. Utiliser `X-Agent-Token` ou `Authorization: Bearer`.

---

## Entrée multi-nœuds LinuxGateway

LinuxGateway convient au déploiement sur un serveur Linux avec un domaine public. Il n'exécute pas Unity, ne stocke pas de projets Unity et ne détient pas de certificats Apple ; il gère uniquement le login, l'enregistrement de nœuds, la sélection de nœuds, le transfert de tâches et le proxy de logs/artefacts.

Architecture typique :

```text
Utilisateurs externes
  -> LinuxGateway Web/API
      -> Mac BuildServer       iOS + Android
      -> Windows BuildServer   Android
```

Sans LinuxGateway, chaque BuildServer Mac/Windows peut toujours être utilisé indépendamment.

### Démarrage de LinuxGateway

Développement :

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Débogage Windows :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
```

Adresse par défaut :

```text
http://127.0.0.1:5090
```

Si `LINUX_GATEWAY_ADMIN_PASSWORD` n'est pas défini, un mot de passe initial est généré au premier démarrage :

```text
linuxgateway-data/initial-admin.txt
```

Recommandé pour la production :

```bash
export LINUX_GATEWAY_ADMIN_PASSWORD="strong-password"
export LINUX_GATEWAY_PUBLIC_BASE_URL="https://build.example.com"
export LINUX_GATEWAY_ALLOWED_ORIGINS="https://build.example.com"
export LINUX_GATEWAY_DATA_ROOT="/opt/unity-build-gateway/data"
```

### Publication de LinuxGateway vers Linux

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-linux-gateway-linux.ps1
```

Sortie par défaut :

```text
publish/linux-gateway
```

Copier sur Linux et exécuter :

```bash
chmod +x ./LinuxGateway
./LinuxGateway --urls http://127.0.0.1:5090
```

Pour un accès public, utiliser Nginx/Caddy pour HTTPS et proxy inverse vers `127.0.0.1:5090`.

### Mode 1 : Connexion directe au nœud

La connexion directe convient quand LinuxGateway peut atteindre le BuildServer Mac/Windows, par exemple via VPN, intranet, tunnel ou HTTPS public.

Définir avant le démarrage de chaque nœud BuildServer :

```bash
export BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"
```

Nœud Android Windows :

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="strong-random-token"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

Vous pouvez aussi ne pas définir manuellement `BUILD_SERVER_GATEWAY_TOKEN`. BuildServer l'auto-générera au premier démarrage et le sauvegardera dans :

```text
<DataRoot>/initial-gateway-token.txt
```

BuildServer activera :

```text
/api/gateway/*
```

LinuxGateway appelle le nœud avec :

```text
Header: X-Gateway-Token: <BUILD_SERVER_GATEWAY_TOKEN>
```

Ajouter un device dans l'UI web LinuxGateway :

| Champ | Exemple |
|------|------|
| Nom du device | `Mac Build` |
| URL BuildServer | `https://mac-build.example.com` |
| Gateway Token | Le `BUILD_SERVER_GATEWAY_TOKEN` du nœud |
| Plateformes | Mac : `iOS + Android`, Windows : `Android` |

Après sauvegarde, rafraîchir le device pour confirmer que les projets et configs du nœud sont visibles.

### Mode 2 : Connexion inverse au nœud

La connexion inverse convient quand les nœuds sont derrière NAT, réseaux domestiques ou intranets d'entreprise où LinuxGateway ne peut pas accéder directement à l'adresse du nœud. Dans ce cas, BuildServer initie la connexion vers LinuxGateway.

Générer un Enrollment Token dans l'UI web LinuxGateway, puis remplir la page de connexion Gateway de BuildServer :

```text
Gateway URL: https://build.example.com
Enrollment Token: <token>
```

Vous pouvez aussi configurer via des variables d'environnement pour que BuildServer se connecte automatiquement au démarrage :

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

Une fois connecté, LinuxGateway affiche le nœud à connexion inverse. Les identifiants du nœud sont sauvegardés dans le répertoire de données BuildServer ; après révocation d'un nœud, vous devez générer un nouvel Enrollment Token pour réenregistrer.

La connexion inverse est implémentée dans `LinuxGateway/Reverse/` et `BuildServer/Reverse/`.

### Mise à jour en ligne de LinuxGateway

LinuxGateway inclut `SelfUpdateService`, qui peut vérifier et télécharger des packages de mise à jour depuis Gitee ou GitHub Releases sans nécessiter de .NET SDK sur le serveur.

Vérifier les mises à jour :

```text
GET /api/system/version
GET /api/system/update/check
```

Appliquer la mise à jour (Admin uniquement) :

```text
POST /api/system/update/apply
```

Le processus de mise à jour sauvegarde automatiquement la version actuelle, télécharge un package de mise à jour tar.gz et génère un script `apply-update.sh` pour compléter le remplacement et le redémarrage.

Configuration :

| Variable | Description |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | Source de mise à jour : `gitee` ou `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | Propriétaire du dépôt |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | Nom du dépôt |

### Soumission de builds via LinuxGateway

1. Se connecter à LinuxGateway.
2. Confirmer que le nœud est en ligne sur la page des devices.
3. Rafraîchir le nœud pour s'assurer que les projets et configs sont synchronisés.
4. Sur la page des tâches de build, sélectionner device, projet, config et branche.
5. Soumettre la tâche.
6. Voir le statut, les logs et les artefacts retournés par le nœud distant.

Les tâches iOS ne peuvent être envoyées qu'aux nœuds Mac supportant `ios` ; les nœuds Windows ne conviennent généralement qu'aux APK/AAB Android.

---

## Recommandations de sécurité

- Toujours définir des mots de passe forts en production ; ne pas dépendre longuement des fichiers de mots de passe initiaux.
- Ne pas mettre `BUILD_SERVER_AGENT_TOKEN`, `BUILD_SERVER_GATEWAY_TOKEN` ou les Enrollment Tokens dans les URLs. Utiliser les headers ou le stockage côté serveur.
- Les répertoires de données LinuxGateway et BuildServer stockent des utilisateurs, tâches, identifiants de nœuds ou tokens — restreindre les permissions système.
- Configurer `BUILD_SERVER_ALLOWED_WORKSPACE_ROOTS`, `BUILD_SERVER_ALLOWED_ARTIFACTS_ROOTS`, `BUILD_SERVER_ALLOWED_CONFIG_ROOTS` et `BUILD_SERVER_ALLOWED_REPOSITORY_HOSTS` pour BuildServer.
- Si un backend de nœud est uniquement utilisé par LinuxGateway, éviter d'exposer le backend admin régulier à l'internet public.
- Les certificats iOS, profils de provisioning, fichiers `.p8` App Store Connect, keystores Android et JSON Service Account Google Play ne doivent être stockés que dans des répertoires locaux sécurisés sur la machine de build.
- Ne jamais committer de certificats, clés privées ou tokens à long terme dans Git.
- Lors de l'accès à l'UI web via un proxy inverse, configurer `PUBLIC_BASE_URL` et `ALLOWED_ORIGINS` pour éviter le rejet de requêtes cross-origin ou l'échec de validation d'origine.

---

## FAQ

| Problème | Résolution |
|------|------|
| Le build iOS sur Windows indique macOS requis | Les builds iOS de production doivent s'exécuter sur Mac ; Windows ne supporte que `--dry-run --allow-non-mac` pour le débogage de config |
| Exécutable Unity introuvable | Définir `unityExecutablePath`, ou vérifier que `unityVersion` correspond à un chemin installé de Unity Hub |
| Échec du pull Git | Faire un `git clone` manuel sur la machine de build pour vérifier la clé SSH ou les identifiants HTTPS |
| Échec de validation Team ID | `teamId` doit être un Apple Developer Team ID à 10 caractères, pas un nom d'entreprise |
| Échec de l'upload App Store Connect | Vérifier `exportMethod=app-store`, l'existence du chemin `.p8`, Key ID et Issuer ID corrects |
| Erreur Android versionCode | `buildNumber` doit être un entier positif |
| Échec de l'upload Google Play | Vérifier le chemin du JSON Service Account, les permissions de l'app, packageName, track et le format de l'artefact d'upload |
| Échec de connexion BuildServer | Le compte est `admin` ; copier uniquement la valeur après `admin password:` dans `initial-admin.txt` |
| Opérations d'écriture web rejetées | Vérifier que `BUILD_SERVER_ALLOWED_ORIGINS` ou `LINUX_GATEWAY_ALLOWED_ORIGINS` correspond au domaine d'accès |
| Nœud LinuxGateway 401 | Le Gateway Token est incorrect ou le nœud n'a pas activé `BUILD_SERVER_GATEWAY_TOKEN` |
| Timeout du nœud LinuxGateway | Vérifier l'adresse, le port, le pare-feu, le tunnel ou le proxy inverse du nœud |
| Échec du téléchargement d'artefact | Confirmer que le chemin de l'artefact est dans les artifacts roots autorisés de BuildServer |

---

## Tests de régression

Les développeurs peuvent exécuter :

```powershell
.\scripts\verify.ps1
```

Il effectue :

- La compilation de la solution.
- La compilation du projet CLI.
- La compilation de BuildServer.
- La compilation de LinuxGateway.
- L'entrée d'aide `00`.
- Le dry-run de l'exemple iOS.
- Le dry-run de l'exemple Android.
- L'ouverture-fermeture de l'éditeur de config.

La suite de tests couvre 256+ cas de test, englobant le parsing d'arguments CLI, les modèles de config, la sécurité des chemins, les politiques Git, la construction de commandes Unity, l'API Google Play, les configs TikTok, les routes API BuildServer, la communication de nœuds LinuxGateway, la connexion inverse, les notifications email et tous les autres modules.

Exécuter la suite de tests complète :

```powershell
dotnet test .\AutomationUnityBuildIOS.Tests\AutomationUnityBuildIOS.Tests.csproj
```

Pour vérifier rapidement si les changements affectent la compilation :

```powershell
dotnet build .\AutomationUnityBuildIOS.sln
```
