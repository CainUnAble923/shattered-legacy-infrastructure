// cc-P128: ServUO pub57 Scripts/Items/Consumables/FruitBowl.cs -> ours. A bowl of fruit (ML; Cooking 55 from an empty
// wooden bowl, three pears, three apples and three bananas): fills 20 and gives +5 to every stat for two minutes.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class FruitBowl : Food, ICommodity
{
    [Constructible]
    public FruitBowl() : base(0x2D4F)
    {
        Weight = 1.0;
        FillFactor = 20;
    }

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;

    public override int LabelNumber => 1072950; // fruit bowl

    public override bool Eat(Mobile from)
    {
        if (!FillHunger(from, FillFactor))
        {
            return false;
        }

        var modName = Serial.ToString();

        from.AddStatMod(new StatMod(StatType.Str, modName + "Str", 5, TimeSpan.FromSeconds(120)));
        from.AddStatMod(new StatMod(StatType.Dex, modName + "Dex", 5, TimeSpan.FromSeconds(120)));
        from.AddStatMod(new StatMod(StatType.Int, modName + "Int", 5, TimeSpan.FromSeconds(120)));

        from.PlaySound(0x1EA);
        from.FixedParticles(0x373A, 10, 15, 5018, EffectLayer.Waist);

        Consume();

        return true;
    }
}
