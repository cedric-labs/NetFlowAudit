> **Language / Langue**: [English](diagnose-name-resolution.md) · **Français**

# Diagnostiquer un nom d'hôte manquant

Objectif : comprendre pourquoi une adresse apparaît sans nom dans l'audit, et y remédier.

## Lancer le diagnostic

```bash
NetFlowAudit.Cli lookup 192.0.2.25
```

Le rapport compte six étapes : classification de l'adresse, entrée du cache local, réglages DNS du système, requête DNS inverse avec sa durée et son erreur, requête RDAP (adresses publiques uniquement), et l'entrée de cache qui en résulte.

## Quand le serveur DNS est lent

L'étape 4 affiche la durée de l'appel et le délai utilisé, le même que celui de l'audit (`DnsResolutionTimeoutMs`, 3000 ms par défaut). Si elle indique `TIMEOUT`, ou une durée proche du délai, augmentez le délai dans le fichier de configuration ou en ligne de commande :

```bash
NetFlowAudit.Cli audit --dns-timeout-ms 6000
```

Vérifiez la nouvelle valeur avec `NetFlowAudit.Cli lookup 192.0.2.25 --dns-timeout-ms 6000`. La résolution tourne en arrière-plan : une attente plus longue ne retarde pas les balayages. Une adresse dont la requête a expiré est reposée une minute plus tard ; une adresse mise en cache sans nom (l'étape 2 affiche un `HostName` vide avec `Source: None`) est reposée 24 heures après sa date `Resolved at`.

## Pourquoi ping -a trouve un nom et pas NetFlowAudit

Sous Windows, `ping -a` interroge le fichier hosts, le cache DNS, NetBIOS, LLMNR et les serveurs DNS avec les suffixes du domaine. NetFlowAudit n'envoie qu'une requête DNS inverse (un enregistrement `PTR` dans `in-addr.arpa`). Une machine connue par NetBIOS ou LLMNR mais sans enregistrement `PTR` reste donc sans nom. L'étape DNS inverse de `lookup` affiche l'erreur renvoyée.

## Y remédier

- Ajoutez l'enregistrement `PTR` manquant dans la zone DNS inverse (box, routeur, Pi-hole…).
- Ou déclarez le nom dans le fichier de configuration :

  ```json
  {
    "CustomHostAliases": { "192.0.2.25": "nas" }
  }
  ```

  ou en ligne de commande : `NetFlowAudit.Cli audit --alias 192.0.2.25=nas`.

Les alias passent avant le DNS. Les adresses jamais résolues (diffusion, multidiffusion, lien local) sont listées dans `DnsExcludedNetworks`.
