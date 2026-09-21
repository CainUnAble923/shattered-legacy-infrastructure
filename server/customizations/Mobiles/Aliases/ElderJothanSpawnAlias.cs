// CC6 batch 7, Q-055 (answered yes, 2026-09-21): a spawn-name alias. See notes/cc6-batch7.md §2 for the evidence.
//
// WHAT IT RECONCILES. Pinned ModernUO's own spawn data (Distribution/Data/Spawns, a converted Nerun's Distro, commit
// 0d1fffd9f) asks for "jothan" at Heartwood [7048,382] on Trammel and Felucca (shared/*/TownsPeople.json), and no
// assembly declares that name, so BaseSpawner runs the entry with EntryFlags.InvalidType (BaseSpawner.cs:1130) and the
// spot stays empty. The type the data means is stock ElderJothan (Engines/ML Quests/Definitions/Heartwood.cs:3417). Its
// DefaultName is "Elder Jothan", title "the wise", a male elf with AI_Vendor; ServUO's Mobiles/NPCs/Jothan.cs is the
// same NPC by name, title and sex (a BaseVendor that sells nothing and offers no quest). Pinned's data names
// elderjothan nowhere, so under stock ModernUO Heartwood has no Elder Jothan.
//
// WHAT THE DEFECT IS. Pinned's data disagreeing with pinned's assembly: upstream's JSON names upstream's own type under
// a spelling upstream never declared. The spawn files are pinned upstream data and we do not patch data, so this second
// partial-class part is the only additive route: AssemblyHandler.TypeCache registers every [TypeAlias] string in the
// lookup FindTypeByName reads (AssemblyHandler.cs:251-263). Aliases are read, never written: a save still records
// "Server.Engines.MLQuests.Definitions.ElderJothan", and nothing a player sees changes.
//
// EXPECTED TO BE DELETED once upstream fixes the data. The misnames are reported upstream (notes/upstream-spawn-
// misnames.md); when a pinned bump carries the corrected JSON, delete this file, move the orphan count the CC6 batch
// tests pin, and regenerate tools/spawn-orphans.txt. Nothing else references it.

namespace Server.Engines.MLQuests.Definitions;

[TypeAlias("Server.Engines.MLQuests.Definitions.Jothan")]
public partial class ElderJothan
{
}
