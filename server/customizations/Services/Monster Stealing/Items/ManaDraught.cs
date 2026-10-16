// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Items/ManaDraught.cs -> ours. Restores 25-40 mana, once per ten
// minutes per player.
//
// Two changes from ServUO (notes/cc-P127-port-treasure-maps-monster-stealing.md): ServUO's OnUsed is never called
// (no OnDoubleClick), so the draught was inert; it answers a double-click here. And ServUO's DoCleanup (run after every
// world save) removes every entry whose time is before now + cooldown, which is all of them, resetting every cooldown on
// each save; expired entries are dropped here as they are met instead.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[TypeAlias("drNO.ThieveItems.ManaDraught")]
[SerializationGenerator(0, false)]
public partial class ManaDraught : Item
{
    private static readonly Dictionary<Mobile, DateTime> _usage = new();
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    [Constructible]
    public ManaDraught() : base(0xFFB)
    {
        Hue = 0x48A;
        Weight = 1.0;
    }

    public override int LabelNumber => 1094938; // Mana Draught

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        if (!IsChildOf(pm.Backpack))
        {
            pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        OnUsed(pm);
    }

    public static bool CheckUse(PlayerMobile pm)
    {
        if (!_usage.TryGetValue(pm, out var last))
        {
            return true;
        }

        if (last + Cooldown >= Core.Now)
        {
            return false;
        }

        _usage.Remove(pm);
        return true;
    }

    private void OnUsed(PlayerMobile by)
    {
        if (CheckUse(by))
        {
            DoHeal(by);
        }
        else
        {
            var left = (int)(_usage[by] + Cooldown - Core.Now).TotalSeconds;
            by.SendLocalizedMessage(1079263, left.ToString()); // You must wait ~1_seconds~ seconds before using this item.
        }
    }

    private void DoHeal(PlayerMobile pm)
    {
        var toHeal = Utility.RandomMinMax(25, 40);
        var diff = pm.ManaMax - pm.Mana;

        if (diff == 0)
        {
            pm.SendLocalizedMessage(1095127); // You are already at full mana
            return;
        }

        toHeal = Math.Min(toHeal, diff);

        pm.Mana += toHeal;
        Consume();

        _usage[pm] = Core.Now;

        pm.SendLocalizedMessage(1095128); // The sour draught instantly restores some of your mana!
    }
}
