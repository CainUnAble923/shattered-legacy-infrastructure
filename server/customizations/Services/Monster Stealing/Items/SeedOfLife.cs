// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Items/SeedOflife.cs -> ours. Restores 25-40 hits, once per ten
// minutes per player. The same two changes as ManaDraught (it answers a double-click; expired cooldowns drop as met).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[TypeAlias("drNO.ThieveItems.SeedOflife")]
[SerializationGenerator(0, false)]
public partial class SeedOfLife : Item
{
    private static readonly Dictionary<Mobile, DateTime> _usage = new();
    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    [Constructible]
    public SeedOfLife(int amount = 1) : base(0x1727)
    {
        Hue = 0x491;
        Weight = 1.0;
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1094937; // seed of life

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
        var diff = pm.HitsMax - pm.Hits;

        if (diff == 0)
        {
            pm.SendLocalizedMessage(1049547); // You are already at full health
            return;
        }

        toHeal = Math.Min(toHeal, diff);

        pm.Hits += toHeal;
        Consume();

        _usage[pm] = Core.Now;

        pm.SendLocalizedMessage(1095126); // The bitter seed instantly restores some of your health!
    }
}
