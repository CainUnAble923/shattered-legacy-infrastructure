// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Items/LifeShieldLotion.cs -> ours. HandleLifeDrain is the
// 50-99% cut ServUO applies to life drain while the lotion is on; nothing in pinned calls it yet (see the notes).

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[TypeAlias("drNO.ThieveItems.LifeShieldLotion")]
[SerializationGenerator(0, false)]
public partial class LifeShieldLotion : BaseBalmOrLotion
{
    [Constructible]
    public LifeShieldLotion() : base(0xEFC)
    {
        EffectType = ThieveConsumableEffect.LifeShieldLotionEffect;
    }

    public override int LabelNumber => 1094945; // Life Shield Lotion

    protected override void ApplyEffect(PlayerMobile pm)
    {
        base.ApplyEffect(pm);
        pm.SendMessage("You applied Life Shield Lotion");
    }

    public static double HandleLifeDrain(PlayerMobile pm, double damage)
    {
        if (IsUnderThieveConsumableEffect(pm, ThieveConsumableEffect.LifeShieldLotionEffect))
        {
            var rnd = 50 + Utility.Random(50);
            var dmgMod = (int)(damage * (rnd / 100.0));
            return damage - dmgMod;
        }

        return damage;
    }
}
