// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Items/BalmOfStrength.cs -> ours.

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[TypeAlias("drNO.ThieveItems.BalmOfStrength")]
[SerializationGenerator(0, false)]
public partial class BalmOfStrength : BaseBalmOrLotion
{
    [Constructible]
    public BalmOfStrength() : base(0xEFB)
    {
        EffectType = ThieveConsumableEffect.BalmOfStrengthEffect;
    }

    public override int LabelNumber => 1094940; // Balm of Strength

    protected override void ApplyEffect(PlayerMobile pm)
    {
        pm.AddStatMod(new StatMod(StatType.Str, "Balm", 10, EffectDuration));
        pm.SendLocalizedMessage(1095136); // You apply the balm and suddenly feel stronger!
        base.ApplyEffect(pm);
    }
}
