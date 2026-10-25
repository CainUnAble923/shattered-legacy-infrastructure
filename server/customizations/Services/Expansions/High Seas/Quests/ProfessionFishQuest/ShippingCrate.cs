// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Quests/ProfessionFishQuest/ShippingCrate.cs -> ours, High
// Seas, second half. The crate a profession fish order puts in the hold of the player's boat, and here also the order
// itself: who took it (Owner), from which fish monger (Quester), to which port (TurnIn), on which boat, and the fish it
// asks for (one to six lines: a fish and how many). Only the fish the order needs go in; once in, they cannot be taken
// out; the tooltip shows each line's count, the port of delivery and NO-TRADE. The crate cannot be traded, dropped on
// the ground or put in another container, and only its owner may lift it. Destroying it forfeits the order.
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
// - ServUO's order is a quest in its Mondain's Legacy quest framework (ProfessionalFisherQuest : BaseQuest, a quest log
//   entry pointing at the crate). That framework is in neither pinned nor ours, so the crate carries the order and the
//   fish mongers read it from the crate (ProfessionalFisherQuest.cs); there is no quest log entry. A player's open
//   orders are the crates they own (OrdersOf).
// - The port of delivery is the turn-in monger's port name (FishMonger.PortName), not only its region's name.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.Quests;
using Server.Mobiles;
using Server.Multis;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ShippingCrate : SmallCrate
{
    private static readonly HashSet<ShippingCrate> _orders = new();

    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(0)]
    private Mobile _owner;

    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(1)]
    private FishMonger _quester;

    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(2)]
    private FishMonger _turnIn;

    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(3)]
    private BaseBoat _boat;

    [SerializableField(4)]
    private int _title;

    // The order's lines, in step: a FishQuestHelper.Fish index, how many are in, how many are needed.
    [SerializableField(5, setter: "private")]
    private List<int> _orderFish = new();

    [SerializableField(6, setter: "private")]
    private List<int> _orderHave = new();

    [SerializableField(7, setter: "private")]
    private List<int> _orderNeed = new();

    [SerializableField(8)]
    private bool _rewarded;

    public ShippingCrate(Mobile owner, FishMonger quester, FishMonger turnIn, BaseBoat boat, int title, Dictionary<Type, int> lines)
    {
        _owner = owner;
        _quester = quester;
        _turnIn = turnIn;
        _boat = boat;
        _title = title;

        foreach (var (type, amount) in lines)
        {
            _orderFish.Add(FishQuestHelper.GetIndexForType(type));
            _orderHave.Add(0);
            _orderNeed.Add(amount);
        }

        _orders.Add(this);
    }

    public override int DefaultMaxWeight => 1200;
    public override bool DisplaysContent => false;
    public override double DefaultWeight => 1.0;

    public int LineCount => _orderFish.Count;

    public Type LineType(int line) => FishQuestHelper.GetTypeFromIndex(_orderFish[line]);

    public int LineHave(int line) => _orderHave[line];

    public int LineNeeded(int line) => _orderNeed[line];

    [CommandProperty(AccessLevel.GameMaster)]
    public bool Completed
    {
        get
        {
            for (var i = 0; i < _orderFish.Count; i++)
            {
                if (_orderHave[i] < _orderNeed[i])
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>The open orders (crates) a player owns.</summary>
    public static List<ShippingCrate> OrdersOf(Mobile owner)
    {
        var list = new List<ShippingCrate>();

        foreach (var crate in _orders)
        {
            if (crate.Owner == owner && !crate.Deleted)
            {
                list.Add(crate);
            }
        }

        return list;
    }

    /// <summary>The line this item fills, or -1.</summary>
    public int LineFor(Item item)
    {
        var type = item.GetType();

        for (var i = 0; i < _orderFish.Count; i++)
        {
            if (LineType(i) == type)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>ServUO FishQuestObjective.Update: count the fish in, and tell the owner when the order is full.</summary>
    public bool Update(Item item)
    {
        var line = LineFor(item);

        if (line < 0)
        {
            return false;
        }

        _orderHave[line] += item.Amount;
        this.MarkDirty();
        InvalidateProperties();

        if (_owner != null)
        {
            if (Completed)
            {
                _owner.SendLocalizedMessage(1072273, null, 0x23); // You've completed a quest!  Don't forget to collect your reward.
                _owner.SendSound(0x5B5);
            }
            else
            {
                _owner.SendSound(0x5B6);
            }
        }

        return true;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        var loc = 1116453; // ~1_val~: ~2_val~/~3_val~

        for (var i = 0; i < _orderFish.Count; i++)
        {
            var idx = Math.Clamp(_orderFish[i], 0, FishQuestHelper.Labels.Length - 1);
            list.Add(loc++, $"{FishQuestHelper.Labels[idx]:#}\t{_orderHave[i]}\t{_orderNeed[i]}");
        }

        var delivery = GetDeliveryInfo();

        if (delivery is int number)
        {
            list.Add(number);
        }
        else
        {
            list.Add(delivery as string);
        }

        list.Add(1076255); // NO-TRADE
        list.Add(1072241, $"{TotalItems}\t{MaxItems}\t{TotalWeight}\t{MaxWeight}"); // Contents: ~1_COUNT~/~2_MAXCOUNT~ items, ~3_WEIGHT~/~4_MAXWEIGHT~ stones
    }

    /// <summary>The delivery line: ServUO's clilocs for the eight ports, by the turn-in monger's port.</summary>
    public object GetDeliveryInfo() =>
        _turnIn?.PortName switch
        {
            "Sea Market" => 1116496,
            "Britain"    => 1116497,
            "Trinsic"    => 1116498,
            "Moonglow"   => 1116499,
            "Skara Brae" => 1116500,
            "Vesper"     => 1116501,
            "Jhelom"     => 1116502,
            "Papua"      => 1116503,
            _            => "Unknown Delivery"
        };

    public override bool OnDragLift(Mobile from) => _owner == null || _owner == from;

    public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => false;

    public override bool OnDroppedToMobile(Mobile from, Mobile target)
    {
        from.SendLocalizedMessage(1076256); // That item cannot be traded.
        return false;
    }

    public override bool DropToItem(Mobile from, Item target, Point3D p)
    {
        from.SendLocalizedMessage(1076254); // That item cannot be dropped.
        return false;
    }

    public override bool OnDroppedToWorld(Mobile from, Point3D p)
    {
        from.SendLocalizedMessage(1076254); // That item cannot be dropped.
        return false;
    }

    public override bool OnStackAttempt(Mobile from, Item stack, Item dropped)
    {
        if (dropped is BaseHighseasFish && LineFor(dropped) >= 0 && base.OnStackAttempt(from, stack, dropped))
        {
            Update(dropped);
            return true;
        }

        from.SendLocalizedMessage(1116461); // This is not needed to fill the order.
        return false;
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        if (dropped is BaseHighseasFish && LineFor(dropped) >= 0 && base.OnDragDrop(from, dropped))
        {
            Update(dropped);
            return true;
        }

        from.SendLocalizedMessage(1116461); // This is not needed to fill the order.
        return false;
    }

    public override bool OnDragDropInto(Mobile from, Item dropped, Point3D p)
    {
        if (dropped is BaseHighseasFish && LineFor(dropped) >= 0 && base.OnDragDropInto(from, dropped, p))
        {
            Update(dropped);
            return true;
        }

        from.SendLocalizedMessage(1116461); // This is not needed to fill the order.
        return false;
    }

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
    {
        if (LineFor(item) >= 0)
        {
            reject = LRReason.CannotLift;
            return false;
        }

        return base.CheckLift(from, item, ref reject);
    }

    /// <summary>Hands the order in: the ordered fish are used up, anything else goes to the hold, the crate is gone.</summary>
    public void DeleteQuestItems()
    {
        var hold = RootParent as Container ?? Parent as Container;

        for (var i = 0; i < _orderFish.Count; i++)
        {
            ConsumeTotal(LineType(i), _orderNeed[i]);
        }

        if (hold != null && hold != this)
        {
            foreach (var item in new List<Item>(Items))
            {
                hold.DropItem(item);
            }
        }

        _rewarded = true;
        Delete();
    }

    public override void OnDelete()
    {
        // ServUO: destroying the crate forfeits an order not yet handed in.
        if (!_rewarded && !Completed && _owner != null)
        {
            var entry = PlayerFishingEntry.GetEntry(_owner);

            for (var i = 0; i < _orderFish.Count && entry != null; i++)
            {
                entry.OnQuestResign(LineType(i));
            }
        }

        base.OnDelete();
    }

    public override void OnAfterDelete()
    {
        _orders.Remove(this);
        base.OnAfterDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _orders.Add(this);
    }
}
