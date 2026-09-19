// ServUO: Services/Revamped Dungeons/Shame Revamped/Items/WhetstoneOfEnervation.cs (CC4 Shame).
//
// Made from the three guardian drops; used on an exceptional, unimbued weapon in the pack to strip its
// Damage Increase. ServUO's TimesImbued > 0 refusal is not reproduced: ModernUO has no Imbuing, so no
// weapon here can be imbued and that test could never fire (paper-only, notes/cc4-shame.md).
//
// ServUO's constructor sets Stackable true, Amount, then Stackable false, so a whetstone is never stackable
// and Amount is whatever was asked for; reproduced. The Deserialize ItemID fixer for old saves is dropped.

using ModernUO.Serialization;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class WhetstoneOfEnervation : Item
{
    [Constructible]
    public WhetstoneOfEnervation(int amount = 1) : base(0x1368)
    {
        Hue = 1458;

        Stackable = true;
        Amount = amount;
        Stackable = false;
    }

    public override int LabelNumber => 1151811; // Whetstone of Enervation
    public override double DefaultWeight => 1;

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack))
        {
            from.BeginTarget(-1, false, TargetFlags.None, (m, targeted) => Apply(m, targeted));
        }
        else
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
        }
    }

    /// <summary>
    ///     The body of ServUO's target callback. Public so the port's test can drive it without a client.
    /// </summary>
    public void Apply(Mobile m, object targeted)
    {
        if (!IsChildOf(m.Backpack))
        {
            m.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
        }
        else if (targeted is BaseWeapon wep)
        {
            if (!wep.IsChildOf(m.Backpack))
            {
                m.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            }
            else if (wep.Quality != WeaponQuality.Exceptional)
            {
                m.SendLocalizedMessage(1046439); // Invalid target.
            }
            else if (wep.Attributes.WeaponDamage > 0)
            {
                wep.Attributes.WeaponDamage = 0;
                m.SendLocalizedMessage(1151814); // You have removed the damage increase from this weapon.

                Consume();
            }
            else
            {
                m.SendLocalizedMessage(1046439); // Invalid target.
            }
        }
        else
        {
            m.SendLocalizedMessage(1046439); // Invalid target.
        }
    }
}
