// CC6 batch 6 SPIKE - one name, pending Q-055. Delete this file to revert it; nothing else depends on it except
// server/tests/Mobiles/Normal/CC6Batch6SpawnAliasVerification.cs and the orphan count in the batch-1 test (40 -> 41).
//
// Pinned ModernUO's own spawn data asks for "eliteninjawarrior" at seven Citadel spawners (shared/malas/Citadel.json)
// and no assembly declares that name, so those entries run with EntryFlags.InvalidType and the rooms stay empty. The
// stock type is EliteNinja (Mobiles/Monsters/SE/EliteNinja.cs); ServUO's own Citadel XML (Spawns/malas.xml) names
// EliteNinja at the same rooms, and the JSON's other Citadel names (serpentsfangassassin, tigersclawthief,
// dragonsflamemage) are the ServUO types batch 2 ported. The spawn files are pinned upstream data and we do not
// patch data, so this is the only additive route: AssemblyHandler.TypeCache registers every [TypeAlias] string in
// the lookup FindTypeByName reads (AssemblyHandler.cs:251-263), and EliteNinja is a partial class (the serialization
// generator requires it), so this second part merges the attribute onto the stock type with no patch. Aliases are
// read, never written: a save still records "Server.Mobiles.EliteNinja". The name itself is Nerun's Distro's, not
// OSI's; that is the fidelity argument in notes/cc6-batch6-name-mapping.md §4, and it is why this stops at one.

namespace Server.Mobiles;

[TypeAlias("Server.Mobiles.EliteNinjaWarrior")]
public partial class EliteNinja
{
}
