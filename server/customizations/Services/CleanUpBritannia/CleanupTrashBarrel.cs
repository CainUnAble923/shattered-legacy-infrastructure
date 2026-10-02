// cc-P33 (F-3): ServUO pub57 Items/Containers/CleanupTrashBarrel.cs, "Trash - Keep Britannia Clean". Unlike a plain
// trash barrel it takes only items with Clean Up value, and empties (and pays) at once. Pinned has no such type.
// The shared Clean Up bookkeeping is CleanUpTrash (ServUO's BaseTrash). Placed by [ClusterFPlaceCleanUp, never at load.

using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class CleanupTrashBarrel : Container
{
    [Constructible]
    public CleanupTrashBarrel() : base(0xFAE)
    {
        Hue = 2500;
        Movable = false;
        Name = "Trash - Keep Britannia Clean";
    }

    public override int DefaultMaxWeight => 0; // A value of 0 signals unlimited weight

    public override bool IsDecoContainer => false;

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);
        CleanUpTrash.AddAppraiseEntry(from, ref list);
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (!base.OnDragDrop(from, dropped))
        {
            return false;
        }

        return Accept(from, dropped);
    }

    public override bool OnDragDropInto(Mobile from, Item item, Point3D p)
    {
        if (!base.OnDragDropInto(from, item, p))
        {
            return false;
        }

        return Accept(from, item);
    }

    // ServUO CleanupTrashBarrel.cs:56-79: refused unless it has Clean Up value; otherwise emptied at once.
    private bool Accept(Mobile from, Item item)
    {
        if (!CleanUpTrash.AddCleanupItem(this, from, item))
        {
            if (item.LootType == LootType.Blessed)
            {
                PublicOverheadMessage(MessageType.Regular, 0x3B2, 1075256); // That is blessed; you cannot throw it away.
            }
            else
            {
                PublicOverheadMessage(MessageType.Regular, 0x3B2, 1151271); // This item has no turn-in value for Clean Up Britannia.
            }

            return false;
        }

        PublicOverheadMessage(MessageType.Regular, 0x3B2, Utility.Random(1042891, 8));
        Empty();

        return true;
    }

    public void Empty()
    {
        var items = Items;

        if (items.Count > 0)
        {
            for (var i = items.Count - 1; i >= 0; --i)
            {
                if (i >= items.Count)
                {
                    continue;
                }

                CleanUpTrash.ConfirmCleanupItem(this, items[i]);
                items[i].Delete();
            }

            CleanUpTrash.Award(this);
        }
    }
}
