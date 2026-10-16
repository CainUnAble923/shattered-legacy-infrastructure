// cc-P128: ServUO pub57 Scripts/Items/Consumables/EnchantedApple.cs -> ours. A bite lifts every curse (ML magical
// food; cooked from an apple and a greater heal potion, Cooking 60).
//
// ServUO's Eat has three branches by expansion. This shard is fixed at Endless Journey (Configuration/expansion.json),
// so Core.TOL is always true and only the Time of Legends branch can run: Cleansing Winds' RemoveCurses, with the
// cooldown lengthened by the number of curses removed (up to 15 seconds more). The pre-TOL branches (the SA strength
// roll and the spell-by-spell removal list, which also names SleepSpell, absent from pinned) are not carried: they are
// unreachable here, and the notes record it.

using System;
using ModernUO.Serialization;
using Server.Spells.Mysticism;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class EnchantedApple : BaseMagicalFood
{
    [Constructible]
    public EnchantedApple() : base(0x2FD8)
    {
        Weight = 1.0;
        Hue = 0x488;
        Stackable = true;
    }

    public override MagicalFood FoodID => MagicalFood.EnchantedApple;
    public override TimeSpan Cooldown => TimeSpan.FromSeconds(30);
    public override int EatMessage => 1074846; // A tasty bite of the enchanted apple lifts all curses from your soul.

    public override bool Eat(Mobile from)
    {
        if (IsUnderInfluence(from, FoodID))
        {
            return false;
        }

        if (CoolingDown(from, FoodID))
        {
            from.SendLocalizedMessage(1151180); // You must wait a while before eating another enchanted apple.
            return false;
        }

        from.PlaySound(0xF6);
        from.PlaySound(0x1F7);
        from.FixedParticles(0x3709, 1, 30, 9963, 13, 3, EffectLayer.Head);

        IEntity mfrom = new Entity(Serial.Zero, new Point3D(from.X, from.Y, from.Z - 10), from.Map);
        IEntity mto = new Entity(Serial.Zero, new Point3D(from.X, from.Y, from.Z + 50), from.Map);
        Effects.SendMovingParticles(mfrom, mto, 0x2255, 1, 0, false, false, 13, 3, 9501, 1, 0, EffectLayer.Head, 0x100);

        var power = Math.Min(CleansingWindsSpell.RemoveCurses(from), 15);

        from.SendLocalizedMessage(EatMessage);

        StartInfluence(from, FoodID, Duration, TimeSpan.FromSeconds(30 + power));
        Consume();

        return true;
    }
}
