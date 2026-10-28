> **Language / Langue**: [English](diagnose-name-resolution.md) · **Français**

# Diagnostiquer un nom d'hôte manquant

Objectif : comprendre pourquoi une adresse apparaît sans nom dans l'audit, et y remédier.

## Lancer le diagnostic

```bash
NetFlowAudit.Cli lookup 192.0.2.25
```

Le rapport compte six étapes : classification de l'adresse, entrée du cache local, réglages DNS du système, requête DNS inverse avec sa durée et son erreur, requête RDAP (adresses publiques uniquement), et l'entrée de cache qui en résulte.

## Pourquoi ping -a trouve un nom et pas NetFlowAudit

Sous Windows, `ping -a` interroge le fichier hosts, le cache DNS, NetBIOS, LLMNR et les serveurs DNS avec les suffixes du domaine. NetFlowAudit n'envoie qu'une requête DNS inverse (un enregistrement `PTR` dans `in-addr.arpa`). Une machine connue par NetBIOS ou LLMNR mais sans enregistrement `PTR` reste donc sans nom. L'étape DNS inverse de `lookup` affiche l'erreur renvoyée.

## Y remédier

- Demandez l'enregistrement `PTR` manquant dans la zone DNS inverse.
- Ou déclarez le nom dans le fichier de configuration :

  ```json
  {
    "CustomHostAliases": { "192.0.2.25": "srv-db-01" }
  }
  ```

  ou en ligne de commande : `NetFlowAudit.Cli audit --alias 192.0.2.25=srv-db-01`.

Les alias passent avant le DNS. Les adresses jamais résolues (diffusion, multidiffusion, lien local) sont listées dans `DnsExcludedNetworks`.
