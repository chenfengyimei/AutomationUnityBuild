# Entrée multi-nœuds LinuxGateway

`LinuxGateway` est un point d'entrée central optionnel, adapté au déploiement sur un serveur Linux avec un domaine public. Il n'exécute pas Unity, ne stocke pas de projets Unity et ne détient pas de certificats Apple ; il gère uniquement le login web, l'enregistrement des nœuds de build Mac/Windows, la sélection des nœuds et le transfert de tâches vers le `BuildServer` de chaque nœud.

LinuxGateway supporte deux modes de connexion de nœud : connexion directe (LinuxGateway accède proactivement au nœud) et connexion inverse (le nœud se connecte proactivement à LinuxGateway, adapté aux environnements NAT/intranet). Il inclut une fonctionnalité de mise à jour en ligne intégrée qui télécharge des packages de mise à jour depuis Gitee/GitHub Releases, sans nécessiter de .NET SDK sur le serveur.

Sans LinuxGateway, les instances `BuildServer` Mac/Windows peuvent toujours être utilisées indépendamment pour le login, la configuration et les builds.

## Architecture

```text
Utilisateurs externes
  -> LinuxGateway Web/API
      -> Mac BuildServer /api/gateway/*    iOS + Android
      -> Windows BuildServer /api/gateway/* Android APK/AAB
```

Chaque nœud Mac/Windows continue d'exécuter le `BuildServer` existant, avec juste une API protégée par token supplémentaire activée pour les appels de LinuxGateway.

## Configuration des nœuds Mac/Windows

Définir avant le démarrage de `BuildServer` sur chaque nœud :

```bash
export BUILD_SERVER_GATEWAY_TOKEN="token aléatoire fort pour ce nœud"
export BUILD_SERVER_NODE_PLATFORMS="ios,android"   # Courant pour Mac
```

Nœud Android Windows :

```powershell
$env:BUILD_SERVER_GATEWAY_TOKEN="token aléatoire fort pour ce nœud"
$env:BUILD_SERVER_NODE_PLATFORMS="android"
```

Si `BUILD_SERVER_GATEWAY_TOKEN` est laissé vide, les endpoints `/api/gateway/*` du nœud ne seront pas activés.

LinuxGateway doit pouvoir atteindre l'adresse du nœud, par exemple :

```text
https://mac-build.example.com
https://win-build.example.com
```

Ces adresses peuvent être des adresses de tunnel, des adresses VPN/intranet ou des endpoints HTTPS publics. HTTPS est recommandé.

## Démarrage de LinuxGateway

Développement :

```bash
./scripts/run-linux-gateway.sh http://127.0.0.1:5090
```

Débogage Windows :

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-linux-gateway.ps1
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

## Publication vers Linux

Publier Linux x64 depuis Windows :

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

## Flux d'utilisation

1. Démarrer `BuildServer` sur les nœuds Mac/Windows et définir `BUILD_SERVER_GATEWAY_TOKEN`.
2. Démarrer `LinuxGateway` sur Linux.
3. Se connecter à l'UI web LinuxGateway.
4. Ajouter un device :
   - Nom du device : par exemple `Mac Build`
   - URL BuildServer : par exemple `https://mac-build.example.com`
   - Gateway Token : le `BUILD_SERVER_GATEWAY_TOKEN` du nœud
   - Plateformes : Mac : `iOS + Android`, Windows : `Android`
5. Rafraîchir le device pour confirmer que les projets et configs du nœud sont visibles.
6. Lors de la soumission d'un build, sélectionner le device cible, le projet et la config.

## Notes de sécurité

- Le répertoire de données de LinuxGateway stocke les Gateway Tokens des nœuds — restreindre les permissions système.
- LinuxGateway ne doit être exposé qu'en HTTPS ; le HTTP en clair n'est pas recommandé.
- Les `/api/gateway/*` du nœud n'acceptent que `X-Gateway-Token` — ne pas mettre de tokens dans les URLs.
- Les nœuds ne doivent pas exposer le backend admin régulier à l'internet public ; restreindre l'accès à LinuxGateway uniquement.
- Les tâches iOS ne peuvent être envoyées qu'aux nœuds Mac supportant `ios` ; les nœuds Windows ne conviennent qu'aux APK/AAB Android.

## Connexion inverse

La connexion inverse convient quand les nœuds sont derrière NAT, des réseaux domestiques ou des intranets d'entreprise où LinuxGateway ne peut pas accéder directement à l'adresse du nœud. Dans ce cas, BuildServer se connecte proactivement à LinuxGateway — aucune exposition de port public n'est nécessaire côté nœud.

### Étapes de configuration

1. Générer un Enrollment Token dans l'UI web LinuxGateway.
2. Définir les variables d'environnement sur le nœud BuildServer :

```bash
export BUILD_SERVER_REVERSE_GATEWAY_ENABLED=true
export BUILD_SERVER_REVERSE_GATEWAY_URL="https://build.example.com"
export BUILD_SERVER_REVERSE_GATEWAY_ENROLLMENT_TOKEN="<token>"
export BUILD_SERVER_REVERSE_NODE_NAME="Mac Build"
```

3. Démarrer BuildServer — il se connectera automatiquement à LinuxGateway et s'enregistrera comme nœud à connexion inverse.
4. Après connexion, le nœud apparaît dans l'UI web LinuxGateway.
5. Après révocation d'un nœud, un nouvel Enrollment Token doit être généré pour réenregistrer.

La connexion inverse est implémentée dans `LinuxGateway/Reverse/` et `BuildServer/Reverse/`.

## Mise à jour en ligne

LinuxGateway inclut `SelfUpdateService`, qui peut vérifier et télécharger des packages de mise à jour depuis Gitee ou GitHub Releases sans nécessiter de .NET SDK sur le serveur.

### Endpoints API

| Endpoint | Méthode | Description |
|------|------|------|
| `/api/system/version` | GET | Obtenir la version actuelle |
| `/api/system/update/check` | GET | Vérifier la dernière version |
| `/api/system/update/apply` | POST | Appliquer la mise à jour (Admin uniquement) |

### Processus de mise à jour

1. Interroger la dernière version depuis l'API Gitee/GitHub Release en parallèle.
2. Télécharger le package de mise à jour tar.gz.
3. Générer un script `apply-update.sh` pour compléter sauvegarde + remplacement + redémarrage.

### Configuration

| Variable | Description |
|------|------|
| `LINUX_GATEWAY_UPDATE_SOURCE` | Source de mise à jour : `gitee` ou `github` |
| `LINUX_GATEWAY_UPDATE_REPO_OWNER` | Propriétaire du dépôt |
| `LINUX_GATEWAY_UPDATE_REPO_NAME` | Nom du dépôt |

## Déploiement Docker

LinuxGateway supporte le déploiement Docker, particulièrement adapté aux systèmes plus anciens comme CentOS 7 où le runtime `libstdc++` natif peut être trop ancien. Voir le [Guide de déploiement Docker](linux-gateway-docker.md).
