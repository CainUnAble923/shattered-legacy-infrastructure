# The Siege of Sosaria swap: what players are told on the go-live day (cc-P689, SoS swap S16)

Held here, not in CHANGELOG.md, because the CHANGELOG is the patch notes: the server shows every player line in the
login bulletin and the wiki publishes them. While `switch.txt` says `off` no player may read about land they do not
have yet. The release that turns the switch on copies the lines below into its own section of CHANGELOG.md, checks
the bracketed conditions first, then does the wiki steps. Every claim was checked against the merged swap prompts
(cc-P674 to cc-P688) on 2026-10-30; re-check any line whose prompt changed since.

## Lines for the go-live section of CHANGELOG.md

### Changed
- Trammel and Felucca are a new land: the Siege of Sosaria map by Zigholtul. The towns you know stand where they did, and there is much more to find: six new towns (Valunstead, Hammerhill, Kameron Kove, Elmhaven, Kettleburg and Fort Yondall), keeps such as Fort Whitestone, Castle Dunbain and Tigerclaw Fortress, and new islands. The wiki's world map page has the new routes. The map's place names are Zigholtul's; the stories are ours.
- New Haven is now in the old Haven town, with its bank, docks, shops and Uzeraan's Mansion. New characters who start in New Haven arrive on its square, and its moongate stands east of the square.
- Moonglow, Magincia and Nujel'm are one land now, joined by a desert. Buccaneer's Den and Serpent's Hold can be reached on foot from the mainland. Valunstead is an island with no dock or moongate yet: sail there or use magic.
- Some of the new land is not finished. Five unfinished areas turn you back in thick fog, and you cannot recall, gate or build a house there; boats can sail past. The new map's own dungeons stay sealed until each has its monsters, and each is announced when it opens.
- Felucca stands on the same land as Trammel and keeps its own rules: Buccaneer's Den has no guards, and Haven's ground is called Ocllo there. Felucca's old way to Ter Mur is gone with its old land; Trammel's is still there.
- [Only if cc-P691 has not shipped] Blackthorn's dungeon is closed for now: the new map has other dungeons where it stood. It will come back in a new place.

### Staff
- The Siege of Sosaria map ships in the player package (switch.txt on). Credits: the package README, player-package/vendor/sos-trammel-map/SOURCE.txt, the wiki's world map page.

Conditions to check before copying:
- **Blackthorn** (the last Changed line): drop it if cc-P691 moved Blackthorn into the Fold in this release or an
  earlier one, and write its own line instead.
- **Rootdeep**: the Burrows are still a draft (not open to players), so no line mentions the cellar hatch. If the
  Burrows open in the same release, add: "The way into the Rootdeep Burrows is a cellar hatch in the Second Bank of
  Haven."
- **Valunstead**: if a later prompt gives it a dock or a moongate, change the third line.
- **Opened dungeons**: if any of the author's dungeons opens in the same release (S18), name it in its own line.

## The wiki on the go-live day

The page is written and waits in the drafts namespace (`docker/wiki/content/pages/uo/drafts/world_map.txt`,
`uo:drafts:world_map`), so it is visible only to someone who looks for it. On the go-live day:

1. Move it to `docker/wiki/content/pages/uo/world/world_map.txt` (`uo:world:world_map`); take out its "draft" box at
   the top, and the drafts list's line for it (`uo/drafts/start.txt`).
2. Link it first in `uo/world/start.txt` under Places, and from `uo/world/maps.txt`'s Related Pages.
3. `uo/world/new_haven.txt`: New Haven is the old Haven town now. Rewrite the lines that send players to "Old Haven,
   the ruins to the south-east" (the training ground, Cleansing Old Haven, Drelgor): on this map Old Haven's undead
   live in the woods just north of town (cc-P678). Rootdeep's line points to the cellar hatch once the Burrows open.
   `uo/drafts/rootdeep_burrows.txt` describes the old way in (the hole behind the bank's collapsed south wall):
   on this map it is the cellar hatch in the Second Bank of Haven, with the ladder up three tiles away.
4. `uo/world/dungeons.txt`: Blackthorn as the first condition above says; add one line that the new map's own
   dungeons are sealed until each opens, linking the world map page.
5. Publish: the seed sync (`sync-dokuwiki-content.sh`) does not overwrite pages that exist, so the moved and edited
   pages need `SYNC_MODE=overwrite` for those files, as the wiki README says. Then `scripts/Publish-PatchNotes.ps1`.
