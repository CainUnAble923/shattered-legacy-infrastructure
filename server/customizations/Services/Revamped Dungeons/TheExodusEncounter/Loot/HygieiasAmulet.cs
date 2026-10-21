// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Loot/HygieiasAmulet.cs (cc-P195): one of Clockwork Exodus's ten artifact
// replicas (one kill in five hands one to a player with looting rights). ServUO's IsArtifact has no pinned reader
// (the recipe); the rest is the recipe's transformation.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class HygieiasAmulet : GoldNecklace
{
    [Constructible]
    public HygieiasAmulet()
    {
        SkillBonuses.SetValues(0, SkillName.Alchemy, 10);
    }

    // ServUO's CanFortify => false has no pinned counterpart on a jewel (BaseJewel has no fortify).

    public override int LabelNumber => 1153524; // Hygieia's Amulet [Replica]
}
