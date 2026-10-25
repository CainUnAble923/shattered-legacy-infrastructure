// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Artifacts/CorgulsHandbookOnTheUndead.cs -> ours,
// High Seas, second half. One of Corgul the Soulbinder's own artifacts: a necromancer spellbook, 3 mana regeneration,
// 5% defense chance, 10% lower mana cost and 20% lower reagent cost.
//
// No deliberate differences from ServUO. Its twin, Corgul's Handbook on Mysticism, is not ported: the shard has no
// Mysticism spellbook (MysticBook).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class CorgulsHandbookOnTheUndead : NecromancerSpellbook
{
    [Constructible]
    public CorgulsHandbookOnTheUndead()
    {
        Hue = 2953;
        Attributes.RegenMana = 3;
        Attributes.DefendChance = 5;
        Attributes.LowerManaCost = 10;
        Attributes.LowerRegCost = 20;
    }

    public override int LabelNumber => 1149780;
}
