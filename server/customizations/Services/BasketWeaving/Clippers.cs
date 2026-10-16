// cc-P128: ServUO pub57 Scripts/Services/BasketWeaving/Clippers.cs -> ours. Clippers (Tinkering 50, four ingots): a
// tool used on a decorative plant that, by the mode set from its context menu, cuts the plant into plant clippings or dry
// reeds of the plant's colour, or (hedges and juniper bushes) trims it into one of nine topiaries, one charge each.
//
// ServUO keeps the three mode toggles as saved PlayerMobile flags (ToggleCutClippings, ToggleCutReeds,
// ToggleCutTopiaries); pinned's PlayerMobile has none, so the mode is a per-player table here, clippings by default, and
// a restart puts everyone back to clippings. Recorded in the notes.

using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Craft;
using Server.Engines.Plants;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;
using System.Collections.Generic;

namespace Server.Items;

public enum ClipMode
{
    Clippings,
    Reeds,
    Topiaries
}

[Flippable(0x0DFC, 0x0DFD)]
[SerializationGenerator(0, false)]
public partial class Clippers : BaseTool
{
    private static readonly Dictionary<Mobile, ClipMode> _modes = new();

    [Constructible]
    public Clippers() : base(0x0DFC)
    {
        Weight = 1.0;
        Hue = 1168;
    }

    [Constructible]
    public Clippers(int uses) : base(uses, 0x0DFC)
    {
        Weight = 1.0;
        Hue = 1168;
    }

    public override int LabelNumber => 1112117; // clippers

    public override CraftSystem CraftSystem => DefTinkering.CraftSystem;

    public virtual PlantHue PlantHue => PlantHue.None;

    public static ClipMode GetMode(Mobile m) => _modes.TryGetValue(m, out var mode) ? mode : ClipMode.Clippings;

    public static void SetMode(Mobile m, ClipMode mode)
    {
        var old = GetMode(m);
        _modes[m] = mode;

        m.SendLocalizedMessage(mode switch
        {
            ClipMode.Reeds     => old == mode ? 1112287 : 1112286, // You are already set to cut reeds. / You are now set to cut reeds.
            ClipMode.Topiaries => old == mode ? 1150653 : 1150652, // You are already set to cut topiaries! / You are now set to cut topiaries.
            _                  => old == mode ? 1112284 : 1112285  // You are already set to make plant clippings / You are now set to make plant clippings
        });
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        AddContextMenuEntries(from, this, ref list);
    }

    public static void AddContextMenuEntries(Mobile from, Item item, ref PooledRefList<ContextMenuEntry> list)
    {
        if (!item.IsChildOf(from.Backpack) && item.Parent != from)
        {
            return;
        }

        if (from is not PlayerMobile)
        {
            return;
        }

        list.Add(new ToggleClippings(ClipMode.Clippings, 1112282)); // Set to clip plants
        list.Add(new ToggleClippings(ClipMode.Reeds, 1112283));     // Set to cut reeds
        list.Add(new ToggleClippings(ClipMode.Topiaries, 1150660)); // Set to cut topiaries
    }

    private class ToggleClippings : ContextMenuEntry
    {
        private readonly ClipMode _mode;

        public ToggleClippings(ClipMode mode, int number) : base(number) => _mode = mode;

        public override void OnClick(Mobile from, IEntity target) => SetMode(from, _mode);
    }

    public void ConsumeUse(Mobile from)
    {
        if (UsesRemaining > 1)
        {
            --UsesRemaining;
        }
        else
        {
            from?.SendLocalizedMessage(1112126); // Your clippers break as you use up the last charge..
            Delete();
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1112118); // What plant do you wish to use these clippers on?
            from.Target = new InternalTarget(this);
        }
    }

    /// <summary>What the target does: the message sent, or 0 when the plant was clipped (or the topiary gump opened).</summary>
    public int Clip(Mobile from, object targeted)
    {
        if (from is not PlayerMobile pm || Deleted)
        {
            return 0;
        }

        if (targeted is not PlantItem plant || plant.PlantStatus != PlantStatus.DecorativePlant)
        {
            return 1112119; // You may only use these clippers on decorative plants.
        }

        switch (GetMode(pm))
        {
            case ClipMode.Clippings:
                from.PlaySound(0x248);
                from.AddToBackpack(new PlantClippings(plant.PlantHue) { Hue = plant.Hue });
                plant.Delete();
                ConsumeUse(from);
                break;
            case ClipMode.Reeds:
                from.PlaySound(0x248);
                from.AddToBackpack(new DryReeds(plant.PlantHue) { Hue = plant.Hue });
                plant.Delete();
                ConsumeUse(from);
                break;
            case ClipMode.Topiaries:
                if (plant.PlantType is PlantType.HedgeTall or PlantType.HedgeShort or PlantType.JuniperBush)
                {
                    from.CloseGump<TopiaryGump>();
                    from.SendGump(new TopiaryGump(plant, this));
                }

                break;
        }

        return 0;
    }

    private class InternalTarget : Target
    {
        private readonly Clippers _item;

        public InternalTarget(Clippers item) : base(2, false, TargetFlags.None) => _item = item;

        protected override void OnTarget(Mobile from, object targeted)
        {
            var message = _item.Clip(from, targeted);

            if (message != 0)
            {
                from.SendLocalizedMessage(message);
            }
        }
    }
}

public class TopiaryGump : Gump
{
    // button id -> the topiary's item id (ServUO's table, including its button 7 and 8 duplicates)
    public static readonly int[] Topiaries = { 0, 18713, 18714, 18715, 18736, 18813, 18814, 18814, 18713, 19369 };

    private readonly PlantItem _plant;
    private readonly Clippers _clippers;

    public TopiaryGump(PlantItem plant, Clippers clippers) : base(0, 0)
    {
        _plant = plant;
        _clippers = clippers;

        AddPage(0);

        AddBackground(50, 89, 508, 195, 2600);

        AddLabel(103, 114, 0, "Choose a Topiary:");

        AddButton(92, 155, 1209, 1210, 1);
        AddItem(75, 178, 18713);

        AddButton(133, 155, 1209, 1210, 2);
        AddItem(119, 178, 18714);

        AddButton(177, 155, 1209, 1210, 3);
        AddItem(165, 182, 18715);

        AddButton(217, 155, 1209, 1210, 4);
        AddItem(205, 182, 18736);

        AddButton(267, 155, 1209, 1210, 5);
        AddItem(220, 133, 18813);

        AddButton(333, 155, 1209, 1210, 6);
        AddItem(272, 133, 18814);

        AddButton(388, 155, 1209, 1210, 7);
        AddItem(374, 178, 18784);

        AddButton(426, 155, 1209, 1210, 8);
        AddItem(413, 175, 18713);

        AddButton(480, 155, 1209, 1210, 9);
        AddItem(463, 176, 19369);
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var from = sender.Mobile;
        var button = info.ButtonID;

        if (button < 1 || button >= Topiaries.Length || _plant?.Deleted != false || _clippers?.Deleted != false)
        {
            return;
        }

        from.PlaySound(0x248);
        _plant.ItemID = Topiaries[button];
        _clippers.ConsumeUse(from);
    }
}
