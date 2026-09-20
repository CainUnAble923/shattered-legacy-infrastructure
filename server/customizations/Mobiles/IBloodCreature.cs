// ServUO: Mobiles/Normal/BloodWorm.cs:8-10 (CC6 batch 3). An empty marker interface ServUO declares inline above
// BloodWorm; its only consumer is the A Tangled Web quest objective (Quests/ATangledWeb.cs:12,
// `BloodCreaturesObjective`). Declared here so BloodWorm can carry it now. Stock ModernUO's BloodElemental
// (Mobiles/Monsters/Elemental/Magic/BloodElemental.cs) does not implement it yet; it is a partial class, so an
// additive partial declaration adds the marker with no patch when the quest lands (batch 3 note §9).

namespace Server.Mobiles;

public interface IBloodCreature
{
}
