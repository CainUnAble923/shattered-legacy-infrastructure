// cc-P199: ServUO pub57 Scripts/Items/Functional/StrangeContraption.cs. The strange contraption beside a tainted pool
// (ServUO places one at 5668,1925,0 in both Trammel and Felucca, Core.T2A, from StrangeContraptionAddon.Initialize,
// :127-142): a table, a book, a pool and a machine of posts, display cases and a flour mill. Reading the book (within 2
// tiles) shows its note (gump 500, cliloc 1055141). The flour mill part (6434) has a context entry (3006190): with a
// plague beast mutation core, a piece of obsidian (the Collector quest's) and a moonfire brew (the witch apprentice
// quest's) in the pack, the contraption eats the three, gives +500 karma and cannot be used again by that player for an
// hour (message 1060001 meanwhile); without them it rumbles (1055142).
//
// Differences from ServUO:
//   - Initialize() is not ported: placing anything at world load is never done by code here. The two coordinates are in
//     the report. For a GM to place one, the addon is [Constructible]; ServUO's has no [Constructable] (it was only
//     made by Initialize).
//   - The book's note is a DynamicGump (ServUO builds a bare Gump at :188-191); same image and text at the same place.
//   - The component's Name = "a strange contraption" is DefaultName; ServUO's ContextMenuEntry(from, component) is
//     pinned's OnClick(Mobile from, IEntity target) shape (Server/ContextMenus/ContextMenuEntry.cs).
//   - InstanceTram/InstanceFel are set after load as ServUO's Deserialize sets them (:209-223), and an addon loaded
//     without Core.T2A is deleted, as there.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Quests.Collector;
using Server.Engines.Quests.Hag;
using Server.Gumps;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class StrangeContraptionComponent : AddonComponent
{
    public StrangeContraptionComponent(int id, int hue = 0) : base(id) => Hue = hue;

    public override bool ForceShowProperties => true;

    public override string DefaultName => "a strange contraption";

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (ItemID == 6434)
        {
            list.Add(new DeviceEntry());
        }
    }

    private class DeviceEntry : ContextMenuEntry
    {
        public DeviceEntry() : base(3006190, 16) // Appraise for Cleanup
        {
        }

        public static bool CanDrop(Mobile m)
        {
            if (!StrangeContraptionAddon.Table.TryGetValue(m, out var next))
            {
                return true;
            }

            if (next < Core.Now)
            {
                StrangeContraptionAddon.Table.Remove(m);
                return true;
            }

            m.SendLocalizedMessage(1060001); // You throw the switch, but the mechanism cannot be engaged again so soon.

            return false;
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is not AddonComponent component)
            {
                return;
            }

            if (from.InRange(component.Location, 2))
            {
                if (CanDrop(from))
                {
                    var pack = from.Backpack;

                    if (pack == null)
                    {
                        return;
                    }

                    var pmc = pack.FindItemByType(typeof(PlagueBeastMutationCore));
                    var obs = pack.FindItemByType(typeof(Obsidian));
                    var mb = pack.FindItemByType(typeof(MoonfireBrew));

                    if (pmc != null && obs != null && mb != null)
                    {
                        from.PlaySound(0x21E);
                        // You add the required ingredients and activate the contraption. It rumbles and smokes and then
                        // falls silent. The water shines for a brief moment, and you feel confident that it is now much
                        // less tainted then before.
                        from.SendLocalizedMessage(1055143);
                        from.Karma += 500;

                        pmc.Delete();
                        obs.Delete();
                        mb.Delete();

                        StrangeContraptionAddon.Table[from] = Core.Now + TimeSpan.FromHours(1);
                    }
                    else
                    {
                        // You do not have the necessary ingredients. The contraptions rumbles angrily but does nothing.
                        from.SendLocalizedMessage(1055142, "", 0x59);
                    }
                }
            }
            else
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            }
        }
    }
}

[SerializationGenerator(0, false)]
public partial class StrangeContraptionAddon : BaseAddon
{
    public static Dictionary<Mobile, DateTime> Table = new();

    [Constructible]
    public StrangeContraptionAddon()
    {
        // Table
        AddComponent(new AddonComponent(2923), 1, 2, 1);
        AddComponent(new AddonComponent(2924), 1, 1, 1);

        // Book
        AddComponent(new AddonComponent(0x0FF4), 1, 2, 7);

        // Water
        AddComponent(new AddonComponent(6063), -1, 0, 1);
        AddComponent(new AddonComponent(6066), 0, 0, 1);
        AddComponent(new AddonComponent(6064), -1, 1, 1);
        AddComponent(new AddonComponent(6065), 0, 1, 1);

        AddComponent(new StrangeContraptionComponent(4758), 0, -1, 1);
        AddComponent(new StrangeContraptionComponent(4758), 0, 0, 1);
        AddComponent(new StrangeContraptionComponent(2813), 2, 0, 19);
        AddComponent(new StrangeContraptionComponent(2815), 1, 0, 19);
        AddComponent(new StrangeContraptionComponent(2816), 2, 0, 16);
        AddComponent(new StrangeContraptionComponent(2818), 1, 0, 16);
        AddComponent(new StrangeContraptionComponent(4715), 1, 0, 1);
        AddComponent(new StrangeContraptionComponent(2643), 1, 0, 3);
        AddComponent(new StrangeContraptionComponent(6434), 2, 0, 1);
        AddComponent(new StrangeContraptionComponent(4758, 1545), -1, 0, 0);
        AddComponent(new StrangeContraptionComponent(4272, 1545), -1, 0, 1);
        AddComponent(new StrangeContraptionComponent(6039, 999), 2, 0, 20);
        AddComponent(new StrangeContraptionComponent(6040, 999), 1, 0, 20);
    }

    public static StrangeContraptionAddon InstanceTram { get; set; }
    public static StrangeContraptionAddon InstanceFel { get; set; }

    public override void OnComponentUsed(AddonComponent comp, Mobile from)
    {
        if (comp.ItemID == 0x0FF4)
        {
            if (!from.InRange(comp.GetWorldLocation(), 2))
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
            }
            else
            {
                from.SendGump(new NoteGump());
            }
        }
    }

    [AfterDeserialization(false)]
    private void AfterDeserialization()
    {
        if (Core.T2A)
        {
            if (Map == Map.Trammel)
            {
                InstanceTram = this;
            }
            else if (Map == Map.Felucca)
            {
                InstanceFel = this;
            }
        }
        else
        {
            Delete();
        }
    }

    // ServUO StrangeContraption.cs:188-191: a bare Gump(35, 70) with image 500 and cliloc 1055141.
    private class NoteGump : DynamicGump
    {
        public NoteGump() : base(35, 70)
        {
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddImage(0, 0, 500);
            builder.AddHtmlLocalized(40, 17, 150, 220, 1055141);
        }
    }
}
