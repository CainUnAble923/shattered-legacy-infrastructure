// CC6 batch 7, Q-055 (answered yes, 2026-09-21): a spawn-name alias. See notes/cc6-batch7.md §2 for the evidence.
//
// WHAT IT RECONCILES. Pinned ModernUO's own spawn data (Distribution/Data/Spawns, a converted Nerun's Distro, commit
// 0d1fffd9f) asks for "DryadA" and "Dryada" (one name to FindTypeByName, which ignores case) at two Twisted Weald
// spawners on Ilshenar, [2171,1181] maxCount 8 and [2207,1180] maxCount 12, and no assembly declares that name, so
// BaseSpawner runs the entry with EntryFlags.InvalidType (BaseSpawner.cs:1130) and the spot stays empty. The type the
// data means is stock MLDryad (Mobiles/Monsters/ML/Humanoid/Magic/MLDryad.cs, body 266). ServUO's
// Spawns/twistedweald.xml names MLDryad/mldryad at the same rooms with the same siblings (CuSidhe, Changeling, Satyr,
// DireWolf), and pinned's data names mldryad nowhere, so under stock ModernUO the Weald has no dryads. Pinned's other
// Dryad (Engines/Quests/Uzeraan Turmoil) is the Haven quester Anwin Brenna and is not it.
//
// WHAT THE DEFECT IS. Pinned's data disagreeing with pinned's assembly: upstream's JSON names upstream's own type under
// a spelling upstream never declared. The spawn files are pinned upstream data and we do not patch data, so this second
// partial-class part is the only additive route: AssemblyHandler.TypeCache registers every [TypeAlias] string in the
// lookup FindTypeByName reads (AssemblyHandler.cs:251-263). Aliases are read, never written: a save still records
// "Server.Mobiles.MLDryad", and nothing a player sees changes.
//
// EXPECTED TO BE DELETED once upstream fixes the data. The misnames are reported upstream (notes/upstream-spawn-
// misnames.md); when a pinned bump carries the corrected JSON, delete this file, move the orphan count the CC6 batch
// tests pin, and regenerate tools/spawn-orphans.txt. Nothing else references it.

namespace Server.Mobiles;

[TypeAlias("Server.Mobiles.DryadA")]
public partial class MLDryad
{
}
