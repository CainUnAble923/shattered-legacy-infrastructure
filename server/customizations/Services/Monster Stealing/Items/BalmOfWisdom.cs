// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Items/BalmOfWisdom.cs -> ours.

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[TypeAlias("drNO.ThieveItems.BalmOfWisdom")]
[SerializationGenerator(0, false)]
public partial class BalmOfWisdom : BaseBalmOrLotion
{
    [Constructible]
    public BalmOfWisdom() : base(0x1847)
    {
        EffectType = ThieveConsumableEffect.BalmOfWisdomEffect;
    }

    public override int LabelNumber => 1094941; // Balm of Wisdom

    protected override void ApplyEffect(PlayerMobile pm)
    {
        pm.AddStatMod(new StatMod(StatType.Int, "Balm", 10, EffectDuration));
        pm.SendLocalizedMessage(1095137); // You apply the balm and suddenly feel wiser!
        base.ApplyEffect(pm);
    }
}
