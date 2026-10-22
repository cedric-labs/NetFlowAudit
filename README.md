# NetFlowAudit - Agent d'Audit Continu des Flux Réseau

<p align="center">
  <img src="assets/netflowaudit-icon.png" alt="NetFlowAudit Logo" width="200" />
</p>

**NetFlowAudit** est un agent d'audit réseau multiplateforme (Windows et Linux) conçu pour fonctionner en continu sous la forme d'un service ou d'un daemon d'arrière-plan. Il inspecte l'intégralité du trafic réseau entrant et sortant (TCP et UDP), identifie les processus locaux responsables des communications et journalise ces informations dans un fichier CSV horodaté et dédupliqué.

---

## 1. Caractéristiques Principales

- **Zéro pilote de capture tiers** : N'utilise ni Npcap, ni WinPcap, ni libpcap. 
  - **Sur Windows** : interrogation de l'API native IP Helper Win32 (`iphlpapi.dll`).
  - **Sur Linux / WSL** : lecture directe du pseudo-système de fichiers `/proc/net/{tcp,tcp6,udp,udp6}` et corrélation des descripteurs de fichiers `/proc/<pid>/fd`.
- **Détection des cibles sous-jacentes (Wrappers)** : Ne se contente pas d'identifier le binaire lanceur (`java.exe`, `python`, `cmd.exe`, `powershell.exe`, `bash`), mais extrait la ligne de commande complète et isole la charge utile réelle (`.jar`, `.py`, `.bat`, `.cmd`, `.ps1`, `.sh`).
- **Déduplication intelligente** :
  - Pour les flux sortants, neutralise l'impact des ports clients éphémères afin de ne pas multiplier les lignes identiques pour une même destination.
  - Met à jour le compteur d'observations (`ObservationCount`) et l'horodatage de dernière vue (`LastSeenUtc`).
  - Reprise automatique : recharge le fichier CSV existant au démarrage pour conserver l'historique sans doublon.
- **Filtrage avancé des processus** : Exclusion configurable par identifiant (PID), nom exact, préfixe, suffixe, sous-chaîne ou comptes système (`SYSTEM`, `root`).
- **Multiplateforme & Autonome** : Fonctionne sous Windows et Linux (x64 / ARM64), diffusable sous forme de binaire autonome (*self-contained single-file*) sans prérequis d'installation du SDK/Runtime .NET.

---

## 2. Guide de Démarrage Rapide

### 2.1. Exécution sous Windows

#### Option A : Depuis les sources .NET
```powershell
dotnet run --project NetFlowAudit.Cli/NetFlowAudit.Cli.csproj
```

#### Option B : Publication en exécutable autonome
```powershell
dotnet publish NetFlowAudit.Cli/NetFlowAudit.Cli.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o bin/publish/win-x64
.\bin\publish\win-x64\NetFlowAudit.Cli.exe
```

---

### 2.2. Exécution sous Linux / WSL

#### Publication du binaire autonome Linux
```powershell
dotnet publish NetFlowAudit.Cli/NetFlowAudit.Cli.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o bin/publish/linux-x64
```

#### Lancement sous Linux / WSL
Sur Linux, l'inspection des descripteurs de sockets `/proc/<pid>/fd` appartenant à d'autres utilisateurs ou démons système nécessite les privilèges root ou la capacité `CAP_NET_ADMIN` :
```bash
chmod +x ./NetFlowAudit.Cli
sudo ./NetFlowAudit.Cli
```

---

### 2.3. Arrêt de l'agent

L'arrêt coopératif assure la fermeture et la synchronisation propre du fichier CSV sur le disque :
- **En mode interactif (console)** : Appuyez sur la touche `q` ou `Ctrl+C`.
- **En mode service / arrière-plan (Linux)** : Envoyez un signal POSIX standard :
  ```bash
  kill -TERM <PID>
  # ou
  kill -INT <PID>
  ```

---

## 3. Configuration (`netflowaudit.json`)

Au démarrage, l'application recherche un fichier optionnel nommé `netflowaudit.json` dans le répertoire d'exécution. Si ce fichier est absent, les valeurs par défaut sont appliquées.

### Exemple de fichier `netflowaudit.json` :

```json
{
  "ScanIntervalSeconds": 5,
  "OutputCsvPath": "netflow_audit.csv",
  "ExcludedPids": [0, 4],
  "ExcludedExactNames": [
    "System",
    "Idle",
    "Registry"
  ],
  "ExcludedPrefixes": [
    "svchost"
  ],
  "ExcludedSuffixes": [],
  "ExcludedContains": [],
  "ExcludeSystemAccounts": false
}
```

### Description des paramètres :

| Paramètre | Type | Valeur par défaut | Description |
| :--- | :--- | :--- | :--- |
| `ScanIntervalSeconds` | Entier | `5` | Fréquence en secondes entre deux balayages de la table des sockets actives. |
| `OutputCsvPath` | Chaîne | `"netflow_audit.csv"` | Chemin relatif ou absolu du fichier CSV de sortie. |
| `ExcludedPids` | Tableau d'entiers | `[0, 4]` | Liste des identifiants de processus (PID) à ignorer systématiquement. |
| `ExcludedExactNames` | Tableau de chaînes | `["System", "Idle"]` | Noms exacts de processus exclus (insensible à la casse). |
| `ExcludedPrefixes` | Tableau de chaînes | `[]` | Préfixes de noms de processus à exclure. |
| `ExcludedSuffixes` | Tableau de chaînes | `[]` | Suffixes de noms de processus à exclure. |
| `ExcludedContains` | Tableau de chaînes | `[]` | Sous-chaînes contenues dans le nom du processus à exclure. |
| `ExcludeSystemAccounts` | Booléen | `true` | Exclut automatiquement les processus exécutés sous des comptes système (`SYSTEM`, `LOCAL SERVICE`, `NETWORK SERVICE`, `root`). |

---

## 4. Structure du Journal d'Audit CSV

Le fichier généré est encodé en UTF-8 conforme RFC 4180 avec séparateur virgule `,`. Il est partagé en lecture/écriture (`FileShare.ReadWrite`), ce qui permet de le consulter ou de l'importer en temps réel dans un outil tiers (SIEM, Excel, PowerBI) sans bloquer l'agent.

### Liste des 19 colonnes :

1. `FirstSeenUtc` : Date et heure ISO 8601 (UTC) de la première détection du flux.
2. `LastSeenUtc` : Date et heure ISO 8601 (UTC) du dernier balayage ayant observé ce flux.
3. `ObservationCount` : Nombre cumulé de balayages ayant constaté la présence active de ce flux.
4. `ServerName` : Nom d'hôte de la machine locale auditée.
5. `ServerOsVersion` : Version détaillée du système d'exploitation hôte.
6. `ServerPrimaryIp` : Adresse IP principale attribuée à la machine hôte.
7. `Protocol` : Protocole réseau (`Tcp` ou `Udp`).
8. `Direction` : Direction déduite du flux (`Listening`, `Inbound`, `Outbound`).
9. `TcpState` : État TCP (`Listen`, `Established`, `TimeWait`, `CloseWait`, `SynSent`, etc.).
10. `LocalIp` : Adresse IP locale associée à la socket.
11. `LocalPort` : Port local associé à la socket.
12. `RemoteIp` : Adresse IP distante (ou `0.0.0.0` / `::` si écoute).
13. `RemotePort` : Port distant (ou `0` si écoute).
14. `ProcessId` : PID numérique du processus propriétaire.
15. `ProcessName` : Nom court du processus hôte (`java`, `python3`, `node`, `nginx`).
16. `ExecutablePath` : Chemin d'accès absolu au fichier binaire sur le disque.
17. `CommandLine` : Ligne de commande complète ayant lancé le processus.
18. `TargetExecutable` : Chemin ou nom du script / JAR cible extrait de la ligne de commande (ex: `/app/gateway.jar`, `server.py`, `backup.sh`).
19. `UserName` : Compte utilisateur propriétaire du processus (si accessible).

---

## 5. Déploiement en Service d'Arrière-Plan

### 5.1. Déploiement sous Linux avec systemd

1. Copier le binaire autonome dans `/usr/local/bin/` :
   ```bash
   sudo cp bin/publish/linux-x64/NetFlowAudit.Cli /usr/local/bin/netflowaudit
   sudo chmod +x /usr/local/bin/netflowaudit
   ```

2. Créer le fichier d'unité `/etc/systemd/system/netflowaudit.service` :
   ```ini
   [Unit]
   Description=NetFlowAudit - Agent d'audit des flux reseau
   After=network.target

   [Service]
   Type=simple
   WorkingDirectory=/var/log/netflowaudit
   ExecStart=/usr/local/bin/netflowaudit
   Restart=always
   RestartSec=10
   KillSignal=SIGTERM

   [Install]
   WantedBy=multi-user.target
   ```

3. Créer le dossier de journalisation, activer et démarrer le service :
   ```bash
   sudo mkdir -p /var/log/netflowaudit
   sudo systemctl daemon-reload
   sudo systemctl enable --now netflowaudit.service
   ```

4. Suivre l'activité :
   ```bash
   sudo systemctl status netflowaudit.service
   tail -f /var/log/netflowaudit/netflow_audit.csv
   ```

---

### 5.2. Déploiement sous Windows avec NSSM ou Service Windows

Le binaire `NetFlowAudit.Cli.exe` peut être enregistré comme service Windows à l'aide de l'utilitaire standard `NSSM` (Non-Sucking Service Manager) :
```cmd
nssm install NetFlowAudit "C:\NetFlowAudit\NetFlowAudit.Cli.exe"
nssm set NetFlowAudit AppDirectory "C:\NetFlowAudit"
nssm start NetFlowAudit
```

---

## 6. Architecture & Dépendances Partagées

Le projet repose sur 3 bibliothèques partagées modulaires conformes aux règles de Clean Architecture et de SonarQube Quality Gate :

- [**FileForge.Core**](file:///D:/Dev/Framework/FileForge/FRAMEWORK.md) : Moteur d'I/O streaming thread-safe, écrivain délimité RFC 4180 avec verrouillage partagé non bloquant.
- [**ProcessForge**](file:///D:/Dev/Framework/ProcessForge/FRAMEWORK.md) : Résolution des processus multiplateforme, analyseurs de lignes de commande et moteur de règles d'exclusion.
- [**SocketForge**](file:///D:/Dev/Framework/SocketForge/FRAMEWORK.md) : Scanneur de sockets multiplateforme (Win32 IP Helper & Linux `/proc/net`).

Pour lancer la chaîne complète de validation qualité (formatage, compilation Release, couverture de code et SonarQube) :
```powershell
.\verify.ps1
```
