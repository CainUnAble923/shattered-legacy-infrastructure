// ServUO: Mobiles/Normal/AcidElemental.cs:6-8 (CC6 batch 3). An empty marker interface ServUO declares inline above
// AcidElemental; its only consumer is the Vernix quest objective (Quests/Vernix.cs:12, `AcidCreaturesObjective`),
// which is on the spawn-orphan list and will be ported. Declared here so AcidSlug can carry it now. Stock ModernUO's
// AcidElemental (Mobiles/Monsters/Elemental/Magic/AcidElemental.cs) does not implement it yet; it is a partial class,
// so an additive partial declaration adds the marker with no patch when the quest lands (batch 3 note §9).

namespace Server.Mobiles;

public interface IAcidCreature
{
}
