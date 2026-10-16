// cc-P128: ServUO pub57 Scripts/Items/Consumables/ExplodingTarPotion.cs -> ours. The exploding tar potion: a 20-tile
// burst that slows everyone it reaches. ServUO's LabelNumber 1095147 ("a Greater Confusion Blast potion") is kept as
// it is there; the potion is not craftable in ServUO either (loot only).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExplodingTarPotion : BaseExplodingTarPotion
{
    [Constructible]
    public ExplodingTarPotion() : base(PotionEffect.ExplodingTarPotion)
    {
    }

    public override int Radius => 20;

    public override int LabelNumber => 1095147; // a Greater Confusion Blast potion
}
