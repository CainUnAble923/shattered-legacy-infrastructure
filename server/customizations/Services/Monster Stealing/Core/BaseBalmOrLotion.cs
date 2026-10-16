// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Core/BaseBalmOrLotion.cs -> ours (ICommodity in pinned's shape).

using System;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public abstract partial class BaseBalmOrLotion : BaseThieveConsumable, ICommodity
{
    public BaseBalmOrLotion(int itemId) : base(itemId)
    {
        EffectDuration = TimeSpan.FromMinutes(30);
        Weight = 1.0;
    }

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;

    protected override void OnUse(PlayerMobile by)
    {
        if (EffectType == ThieveConsumableEffect.None)
        {
            by.SendMessage("This balm or lotion is corrupted. Please contact a game master");
            return;
        }

        if (CanUse(by, this))
        {
            ApplyEffect(by);
        }
        else
        {
            by.SendLocalizedMessage(1095133); // You are already under the effect of a balm or lotion.
        }
    }
}
