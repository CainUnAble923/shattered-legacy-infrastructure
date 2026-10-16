// cc-P127: ServUO pub57 Scripts/Services/Monster Stealing/Items/GemOfSalvation.cs -> ours. A blessed gem in the pack
// offers a self-resurrection two seconds after death, then is destroyed; six hours between uses.
//
// Changes from ServUO (notes/cc-P127-port-treasure-maps-monster-stealing.md): the six-hour timer lives on ServUO's
// PlayerMobile (NextGemOfSalvationUse, serialized); pinned's PlayerMobile is not patched for it, so it is a table here
// that a restart clears. ServUO reuses its ResurrectGump with a GemOfSalvation message id; pinned's ResurrectMessage has
// no such member, so the gump is drawn here with the same layout and cliloc 1095129.

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

[TypeAlias("drNO.ThieveItems.GemOfSalvation")]
[SerializationGenerator(0, false)]
public partial class GemOfSalvation : Item
{
    private static readonly Dictionary<Mobile, DateTime> _nextUse = new();
    public static readonly TimeSpan Cooldown = TimeSpan.FromHours(6);

    [Constructible]
    public GemOfSalvation() : base(0x1F13)
    {
        Hue = 286;
        LootType = LootType.Blessed;
    }

    public override int LabelNumber => 1094939; // Gem of Salvation

    public static DateTime NextUse(Mobile m) => _nextUse.TryGetValue(m, out var next) ? next : DateTime.MinValue;

    public static void SetNextUse(Mobile m, DateTime next) => _nextUse[m] = next;

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void PlayerDeath(Mobile killed)
    {
        if (killed is not PlayerMobile pm || pm.Backpack == null)
        {
            return;
        }

        var gem = pm.Backpack.FindItemByType<GemOfSalvation>();

        if (gem == null)
        {
            return;
        }

        Timer.DelayCall(TimeSpan.FromSeconds(2), () => Offer(pm, gem));
    }

    public static void Offer(PlayerMobile pm, GemOfSalvation gem)
    {
        if (pm.Deleted || gem.Deleted || pm.Alive)
        {
            return;
        }

        var next = NextUse(pm);

        if (Core.Now < next)
        {
            var left = next - Core.Now;

            if (left >= TimeSpan.FromMinutes(1.0))
            {
                // Your spirit lacks cohesion. You must wait ~1_minutes~ minutes before invoking the power of a Gem of Salvation.
                pm.SendLocalizedMessage(1095131, (left.Hours * 60 + left.Minutes).ToString());
            }
            else
            {
                // Your spirit lacks cohesion. You must wait ~1_seconds~ seconds before invoking the power of a Gem of Salvation.
                pm.SendLocalizedMessage(1095130, left.Seconds.ToString());
            }

            return;
        }

        pm.CloseGump<ResurrectGump>();
        GemResurrectGump.DisplayTo(pm, gem);
    }
}

public class GemResurrectGump : DynamicGump
{
    private readonly GemOfSalvation _gem;

    public override bool Singleton => true;

    private GemResurrectGump(GemOfSalvation gem) : base(100, 0) => _gem = gem;

    public static void DisplayTo(PlayerMobile pm, GemOfSalvation gem)
    {
        if (pm?.NetState == null || gem?.Deleted != false)
        {
            return;
        }

        pm.SendGump(new GemResurrectGump(gem));
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();

        builder.AddBackground(0, 0, 400, 350, 2600);

        builder.AddHtmlLocalized(0, 20, 400, 35, 1011022); // <center>Resurrection</center>

        // It is possible for you to be resurrected here by the Gem of Salvation. Do you wish to try?
        builder.AddHtmlLocalized(50, 55, 300, 140, 1095129, true, true);

        builder.AddButton(200, 227, 4005, 4007, 0);
        builder.AddHtmlLocalized(235, 230, 110, 35, 1011012); // CANCEL

        builder.AddButton(65, 227, 4005, 4007, 1);
        builder.AddHtmlLocalized(100, 230, 110, 35, 1011011); // CONTINUE
    }

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        if (state.Mobile is not PlayerMobile pm)
        {
            return;
        }

        if (info.ButtonID != 1 || _gem.Deleted || !_gem.IsChildOf(pm.Backpack))
        {
            return;
        }

        if (pm.Map == null || !pm.Map.CanFit(pm.Location, 16, false, false))
        {
            pm.SendLocalizedMessage(502391); // Thou can not be resurrected there!
            return;
        }

        pm.PlaySound(0x214);
        pm.Resurrect();

        pm.SendLocalizedMessage(1095132); // The gem infuses you with its power and is destroyed in the process.

        _gem.Delete();

        GemOfSalvation.SetNextUse(pm, Core.Now + GemOfSalvation.Cooldown);
    }
}
