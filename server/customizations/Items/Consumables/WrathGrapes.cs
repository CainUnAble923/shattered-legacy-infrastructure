// cc-P128: ServUO pub57 Scripts/Items/Consumables/WrathGrapes.cs -> ours. The grapes of wrath (ML magical food; cooked
// from grapes and a greater strength potion, Cooking 95): 20 seconds of +35 weapon damage and +15 spell damage, then two
// minutes before the next bunch. The damage lines are server/patches/AOS-grapes-of-wrath.patch (BaseMagicalFood header).

using System;
using ModernUO.Serialization;
using Server.Engines.BuffIcons;
using Server.Mobiles;

namespace Server.Items;

[TypeAlias("Server.Items.WrathGrapes")]
[SerializationGenerator(0, false)]
public partial class GrapesOfWrath : BaseMagicalFood, ICommodity
{
    [Constructible]
    public GrapesOfWrath() : base(0x2FD7)
    {
        Weight = 1.0;
        Hue = 0x482;
        Stackable = true;
    }

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;

    public override MagicalFood FoodID => MagicalFood.GrapesOfWrath;
    public override TimeSpan Cooldown => TimeSpan.FromMinutes(2);
    public override TimeSpan Duration => TimeSpan.FromSeconds(20);
    public override int EatMessage => 1074847; // The grapes of wrath invigorate you for a short time, allowing you to deal extra damage.

    public override bool Eat(Mobile from)
    {
        if (!base.Eat(from))
        {
            return false;
        }

        (from as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.GrapesOfWrath, 1032247, 1153762, Duration, "15\t35"));
        return true;
    }
}
