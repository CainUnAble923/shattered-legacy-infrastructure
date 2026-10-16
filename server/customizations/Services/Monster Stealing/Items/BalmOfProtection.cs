// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Items/BalmOfProtection.cs -> ours. HandleDamage is the
// 50-100% cut ServUO's AOS.Damage applies while the balm is on; nothing in pinned calls it yet (see the notes).

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[TypeAlias("drNO.ThieveItems.BalmOfProtection")]
[SerializationGenerator(0, false)]
public partial class BalmOfProtection : BaseBalmOrLotion
{
    [Constructible]
    public BalmOfProtection() : base(0x1C18)
    {
        EffectType = ThieveConsumableEffect.BalmOfProtectionEffect;
        Hue = 0x499;
    }

    public override int LabelNumber => 1094943; // Balm of Protection

    public static double HandleDamage(PlayerMobile pm, double damage)
    {
        if (IsUnderThieveConsumableEffect(pm, ThieveConsumableEffect.BalmOfProtectionEffect))
        {
            var rnd = 50 + Utility.Random(51);
            return damage - damage * (rnd / 100.0);
        }

        return damage;
    }

    protected override void ApplyEffect(PlayerMobile pm)
    {
        base.ApplyEffect(pm);
        pm.SendLocalizedMessage(1095143); // You apply the ointment and suddenly feel less vulnerable!
    }
}
