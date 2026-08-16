# Header Forge - dette ouverte

**Point d'entree.** Ce qui reste ouvert sur ce paquet est ici, et nulle part
ailleurs. Chaque entree dit ce qui est en dette, ce que ca empeche aujourd'hui,
ce qu'il faudrait pour la traiter. Une entree qui ne bloque rien n'est pas de la
dette : c'est une note, et elle n'est pas dans cette liste.

Derniere relecture contre le CHANGELOG : 0.1.0.

---

## 1. Le cache de textures de degrade est duplique

**En dette.** `ComponentSeparatorEditor` et `HierarchyHeaderDrawer` construisent
chacun leur propre `Dictionary` de `Texture2D` indexe par cle de couleur, avec
leur propre generation de degrade horizontal et leur propre liberation sur
`AssemblyReloadEvents.beforeAssemblyReload`. C'est le meme code deux fois, pour
le meme resultat visuel.

**Ce que ca empeche.**

1. **Une correction ne se propage pas.** Un changement de largeur de degrade, de
   `FilterMode` ou de politique de liberation doit etre applique aux deux
   endroits, et rien ne signale l'oubli.
2. **Deux textures identiques coexistent en memoire** des qu'un bandeau
   d'Inspector et une barre de Hierarchy partagent les memes deux couleurs, ce
   qui est le cas nominal quand un projet se tient a une palette. Le cout est
   negligeable en absolu, il est structurellement inutile.
3. **Les deux caches ont deja diverge.** Celui de la Hierarchy indexe sur la cle
   brute lue dans le nom du GameObject, celui de l'Inspector force l'alpha a un
   avant de construire sa cle. Deux regles pour une meme notion.

**Ce qu'il faudrait.** Un type interne unique dans
`Runtime/src/` ou `Editor/src/`, portant le dictionnaire, la generation et la
liberation, consomme par les deux dessinateurs. Le point delicat est la cle : il
faut retenir la regle de l'Inspector, alpha force a un, et faire converger la
Hierarchy dessus, sinon les deux caches restent distincts sous un nom commun.

**Explicitement hors scope** du lot d'extraction en package, par decision et non
par oubli.

---

## 2. `EditorUtility.InstanceIDToObject` est obsolete

**En dette.** `HierarchyHeaderDrawer.ResolveEntry` resout son GameObject par
`EditorUtility.InstanceIDToObject`, marquee obsolete depuis Unity 6000.3 au
profit de `EditorUtility.EntityIdToObject`. L'appel est aujourd'hui entoure d'un
`#pragma warning disable CS0618` limite a cette seule ligne.

**Ce que ca empeche.** Rien fonctionnellement : l'API marche et rien n'annonce sa
suppression. Ce qui est en dette est le pragma lui-meme. Tant qu'il est la, cette
ligne est la seule du paquet ou un avertissement de compilation est eteint, et un
paquet qui se veut a zero avertissement doit pouvoir dire pourquoi chacune de ses
exceptions existe encore.

**Ce qui bloque.** Le champ `unity` du `package.json` vaut `6000.0`, et
`EntityIdToObject` n'existe qu'a partir de `6000.3`. Migrer maintenant reviendrait
a monter le plancher, donc a sacrifier exactement la compatibilite que ce plancher
bas cherche a offrir aux postes qui ne sont pas sur la derniere mineure.

**Condition de sortie.** Le jour ou le plancher du paquet monte a `6000.3` ou
au-dela, remplacer l'appel par `EditorUtility.EntityIdToObject` et retirer le
pragma ainsi que son commentaire. Un seul appel, une seule ligne, aucune autre
adherence. Tant que le plancher reste a `6000.0`, cette entree reste ouverte et
c'est le comportement attendu, pas un oubli.

Le portee du pragma est deliberement limitee a l'appel. Un `NoWarn 0618` sur
l'asmdef eteindrait toutes les API obsoletes de l'assembly, y compris celles qui
apparaitront plus tard et qu'il faudra traiter, et la dette deviendrait
invisible.
