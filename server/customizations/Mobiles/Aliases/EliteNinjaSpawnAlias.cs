// CC6 batch 6's spike, kept by Q-055 (answered yes, 2026-09-21); batch 7 rolled the route out to eight more names in
// this directory. See notes/cc6-batch6-name-mapping.md §3 and notes/cc6-batch7.md §2.
//
// WHAT IT RECONCILES. Pinned ModernUO's own spawn data (Distribution/Data/Spawns, a converted Nerun's Distro, commit
// 0d1fffd9f) asks for "eliteninjawarrior" at seven Citadel spawners (shared/malas/Citadel.json), and no assembly
// declares that name, so BaseSpawner runs the entry with EntryFlags.InvalidType (BaseSpawner.cs:1130) and the rooms
// stay empty. The type the data means is stock EliteNinja (Mobiles/Monsters/SE/EliteNinja.cs): ServUO's own Citadel
// XML (Spawns/malas.xml) names EliteNinja at the same rooms, and the JSON's other Citadel names (serpentsfangassassin,
// tigersclawthief, dragonsflamemage) are the ServUO types batch 2 ported. Pinned's data never names eliteninja in the
// Citadel, so under stock ModernUO the Citadel has no elite ninjas at all.
//
// WHAT THE DEFECT IS. Pinned's data disagreeing with pinned's assembly: upstream's JSON names upstream's own type under
// a spelling upstream never declared. The spawn files are pinned upstream data and we do not patch data, so this second
// partial-class part is the only additive route: AssemblyHandler.TypeCache registers every [TypeAlias] string in the
// lookup FindTypeByName reads (AssemblyHandler.cs:251-263), and EliteNinja is a partial class (the serialization
// generator requires it). Aliases are read, never written: a save still records "Server.Mobiles.EliteNinja", and
// nothing a player sees changes. The name itself is Nerun's Distro's, not OSI's.
//
// EXPECTED TO BE DELETED once upstream fixes the data. The misnames are reported upstream (notes/upstream-spawn-
// misnames.md); when a pinned bump carries the corrected JSON, delete this file, move the orphan count the CC6 batch
// tests pin, and regenerate tools/spawn-orphans.txt. Nothing else references it except
// server/tests/Mobiles/Normal/CC6Batch6SpawnAliasVerification.cs.

namespace Server.Mobiles;

[TypeAlias("Server.Mobiles.EliteNinjaWarrior")]
public partial class EliteNinja
{
}
