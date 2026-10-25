// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Artifacts/CorgulsEnchantedSash.cs -> ours, High Seas,
// second half. One of Corgul the Soulbinder's own artifacts: a body sash, +1 stamina and 5% defense chance.
//
// No deliberate differences from ServUO.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class CorgulsEnchantedSash : BodySash
{
    [Constructible]
    public CorgulsEnchantedSash()
    {
        Attributes.BonusStam = 1;
        Attributes.DefendChance = 5;
    }

    public override int LabelNumber => 1149781;
}
