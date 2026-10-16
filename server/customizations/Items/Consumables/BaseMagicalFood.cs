// cc-P128: ServUO pub57 Scripts/Items/Consumables/BaseMagicalFood.cs -> ours. The base of the two Mondain's Legacy
// magical foods (the enchanted apple and the grapes of wrath): one influence flag per food per mobile, a duration, then a
// cooldown, both in memory (ServUO keeps them in static Hashtables too; nothing is saved).
//
// Pinned has no consumer for GrapesOfWrath's influence; server/patches/AOS-grapes-of-wrath.patch adds ServUO's two
// lines to AosAttributes.GetValue (Misc/AOS.cs:629-631, :668-669): +35 weapon damage and +15 spell damage while it lasts.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;

namespace Server.Items;

[Flags]
public enum MagicalFood
{
    None = 0x0,
    GrapesOfWrath = 0x1,
    EnchantedApple = 0x2
}

[SerializationGenerator(0, false)]
public abstract partial class BaseMagicalFood : Food
{
    private static readonly Dictionary<Mobile, MagicalFood> _influence = new();
    private static readonly Dictionary<Mobile, MagicalFood> _cooldown = new();

    public BaseMagicalFood(int itemID) : base(itemID)
    {
        Weight = 1.0;
        FillFactor = 0;
        Stackable = false;
    }

    public virtual MagicalFood FoodID => MagicalFood.None;
    public virtual TimeSpan Cooldown => TimeSpan.Zero;
    public virtual TimeSpan Duration => TimeSpan.Zero;
    public virtual int EatMessage => 0;

    public static bool IsUnderInfluence(Mobile mob, MagicalFood id) =>
        mob != null && _influence.TryGetValue(mob, out var flags) && (flags & id) != 0;

    public static bool CoolingDown(Mobile mob, MagicalFood id) =>
        mob != null && _cooldown.TryGetValue(mob, out var flags) && (flags & id) != 0;

    public static void StartInfluence(Mobile mob, MagicalFood id, TimeSpan duration, TimeSpan cooldown)
    {
        _influence[mob] = (_influence.TryGetValue(mob, out var flags) ? flags : MagicalFood.None) | id;

        Timer.DelayCall(duration, () => EndInfluence(mob, id, cooldown));
    }

    public static void EndInfluence(Mobile mob, MagicalFood id, TimeSpan cooldown)
    {
        if (_influence.TryGetValue(mob, out var flags))
        {
            flags &= ~id;

            if (flags == MagicalFood.None)
            {
                _influence.Remove(mob);
            }
            else
            {
                _influence[mob] = flags;
            }
        }

        if (cooldown != TimeSpan.Zero)
        {
            _cooldown[mob] = (_cooldown.TryGetValue(mob, out var cd) ? cd : MagicalFood.None) | id;

            Timer.DelayCall(cooldown, () => EndCooldown(mob, id));
        }
    }

    public static void EndCooldown(Mobile mob, MagicalFood id)
    {
        if (!_cooldown.TryGetValue(mob, out var flags))
        {
            return;
        }

        flags &= ~id;

        if (flags == MagicalFood.None)
        {
            _cooldown.Remove(mob);
        }
        else
        {
            _cooldown[mob] = flags;
        }
    }

    public override bool Eat(Mobile from)
    {
        if (IsUnderInfluence(from, FoodID))
        {
            return false;
        }

        if (CoolingDown(from, FoodID))
        {
            from.SendLocalizedMessage(1070772); // You must wait a few seconds before you can use that item.
            return false;
        }

        from.SendLocalizedMessage(EatMessage);

        StartInfluence(from, FoodID, Duration, Cooldown);
        Consume();

        return true;
    }
}
