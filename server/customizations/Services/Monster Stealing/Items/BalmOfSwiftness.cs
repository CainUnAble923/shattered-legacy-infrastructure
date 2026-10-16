// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Items/BalmOfSwiftness.cs -> ours.

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[TypeAlias("drNO.ThieveItems.BalmOfSwiftness")]
[SerializationGenerator(0, false)]
public partial class BalmOfSwiftness : BaseBalmOrLotion
{
    [Constructible]
    public BalmOfSwiftness() : base(0x1848)
    {
        EffectType = ThieveConsumableEffect.BalmOfSwiftnessEffect;
    }

    public override int LabelNumber => 1094942; // Balm of Swiftness

    protected override void ApplyEffect(PlayerMobile pm)
    {
        pm.AddStatMod(new StatMod(StatType.Dex, "Balm", 10, EffectDuration));
        pm.SendLocalizedMessage(1095138); // You apply the balm and suddenly feel more agile!
        base.ApplyEffect(pm);
    }
}
