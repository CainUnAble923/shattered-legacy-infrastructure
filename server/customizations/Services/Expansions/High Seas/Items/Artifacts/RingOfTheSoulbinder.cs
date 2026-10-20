// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Artifacts/RingOfTheSoulbinder.cs -> ours, the High
// Seas fishing port. A Charybdis shared artifact (Corgul's too, when Corgul is ported).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class RingOfTheSoulbinder : SilverRing
{
    [Constructible]
    public RingOfTheSoulbinder()
    {
        Hue = 288;
        Attributes.RegenMana = 2;
        Attributes.DefendChance = 15;
        Attributes.CastSpeed = 1;
        Attributes.CastRecovery = 3;
        Attributes.SpellDamage = 10;
        Attributes.LowerRegCost = 10;
    }

    public override int LabelNumber => 1116620; // Ring of the Soulbinder
}
