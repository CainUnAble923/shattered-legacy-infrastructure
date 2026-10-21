// ServUO: Services/Underworld/ExperimentalRoom/ExperimentalRoomChest.cs (cc-P197). The reward chest in the last
// room (984,1064,-37): a player who double-clicks it, or drops a COMPLETE Experimental Gem on it, loses the gem and
// finds one of the eight ExperimentalRoomRewards inside, visible to that player alone (instancing) and decaying ten
// minutes after it was placed. The instancing table is never saved (ServUO recreates it empty on load; a restart
// makes every lingering reward visible to all, ServUO's behaviour). Pinned's TryDropItem(from, dropped, message),
// IsChildVisibleTo, OnDecay, RemoveItem and SetLastMoved are the same hooks ServUO's overrides.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExperimentalRoomChest : MetalBox
{
    private Dictionary<Item, Mobile> _instancing = new();

    [Constructible]
    public ExperimentalRoomChest()
    {
        Movable = false;
        LiftOverride = true;
    }

    public override bool DisplayWeight => false;
    public override bool DisplaysContent => false;
    public override bool Decays => true;
    public override TimeSpan DecayTime => TimeSpan.FromMinutes(10.0);

    public override void OnDoubleClick(Mobile from)
    {
        var pack = from.Backpack;

        if (pack != null)
        {
            var gem = pack.FindItemByType<ExperimentalGem>();

            if (gem?.Complete == true)
            {
                gem.Delete();

                var toDrop = GetRandomDrop();

                if (toDrop != null)
                {
                    AddItemFor(toDrop, from);
                }
            }
        }

        base.OnDoubleClick(from);
    }

    public override bool TryDropItem(Mobile from, Item dropped, bool message)
    {
        if (dropped is ExperimentalGem { Complete: true } && from.InRange(Location, 2))
        {
            dropped.Delete();

            var toDrop = GetRandomDrop();

            if (toDrop != null)
            {
                AddItemFor(toDrop, from);
            }

            base.OnDoubleClick(from);
        }

        return false;
    }

    public void AddItemFor(Item item, Mobile mob)
    {
        if (item == null || mob == null)
        {
            return;
        }

        DropItem(item);
        item.SetLastMoved();

        _instancing ??= new Dictionary<Item, Mobile>();
        _instancing[item] = mob;
    }

    public override bool IsChildVisibleTo(Mobile m, Item child)
    {
        if (m.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        if (_instancing == null)
        {
            return true;
        }

        return !_instancing.TryGetValue(child, out var owner) || owner == m;
    }

    public override bool OnDecay()
    {
        var items = new List<Item>(Items);

        foreach (var i in items)
        {
            if (i.Decays && i.LastMoved.Add(DecayTime) < Core.Now)
            {
                i.Delete();
                _instancing.Remove(i);
            }
        }

        return false;
    }

    public override void RemoveItem(Item item)
    {
        _instancing?.Remove(item);

        base.RemoveItem(item);
    }

    /// <summary>Test support: whether a reward is instanced to a player.</summary>
    public bool IsInstancedTo(Item item, Mobile m) => _instancing.TryGetValue(item, out var owner) && owner == m;

    public Item GetRandomDrop()
    {
        Item item = null;

        switch (Utility.Random(17))
        {
            case 0:
            case 1:
            case 2:
            case 3:
            case 4:
            case 5:
            case 6:
                item = new Stalagmite();
                break;
            case 7:
            case 8:
            case 9:
            case 10:
                item = new Flowstone();
                break;
            case 11:
                item = new CanvaslessEasel();
                break;
            case 12:
                item = new HangingChainmailLegs();
                break;
            case 13:
                item = new HangingRingmailTunic();
                break;
            case 14:
                item = new PluckedChicken();
                break;
            case 15:
                item = new ColorfulTapestry();
                break;
            case 16:
                item = new TwoStoryBanner();
                break;
        }

        return item;
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _instancing = new Dictionary<Item, Mobile>();
    }
}
