// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/LobsterTrap.cs -> ours, the High Seas fishing port.
//
// The lobster trap: a container a player throws into water (double-click, target water within six tiles). It becomes a
// buoy that bobs once a minute; each bob trains Fishing (deep water only from 75), may lose the trap (5% per bob so far),
// else rolls FishInfo.GetSpecialItem for a crab or lobster (a rare one is stamped with the trapper, the date and a weight)
// or half the time a plain crab or lobster. Double-clicking the buoy (or Telekinesis on it) pulls it back with its catch;
// double-clicking the trap in the pack dumps the catch. Owner, guild and party may pull it; in Felucca anyone may, as a
// criminal act; elsewhere strangers are refused. Bait is kept for the Bait item (next prompt): BaitType, EnhancedBait
// and BaitUses are serialized and read by FishInfo, and a catch uses one bait.
//
// Changes from ServUO (shard-migration notes/cc-P193-port-high-seas-fishing.md):
// - ServUO's party test (Party.Get(from) == Party.Get(owner)) is true for two unpartied strangers, so anyone with no
//   party could pull anyone's trap; here the party must exist (a bug that makes something broken is fixed, D-4 rule).
// - The skill-gain test read SpecialFishingNet.ValidateDeepWater, private in pinned; the same deep-water land tiles
//   (0xA8-0xAB, 0x136-0x137, SpecialFishingNet._waterTiles) are checked here.
// - The bait index is stored as an int and resolved through FishInfo, as ServUO does; ServUO's GetTypeFromIndex gives
//   the table's first row for -1, so "no bait" is kept as a flag of its own here (BaitIndex -1 means none).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.PartySystem;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

[Flippable(17615, 17616)]
[SerializationGenerator(0, false)]
public partial class LobsterTrap : Container, ITelekinesisable
{
    public static readonly int TrapID = Utility.RandomMinMax(17615, 17616);
    public static readonly int BuoyID = 17611;
    public static readonly int MaxCatch = 5;

    // The generator writes these six and their properties (BaitIndex, Bobs, InUse, Owner, BaitUses, EnhancedBait);
    // [InvalidateProperties] makes each setter refresh the tooltip, as ServUO's hand-written setters did.
    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(0)]
    private int _baitIndex = -1;

    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(1)]
    private int _bobs;

    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(2)]
    private bool _inUse;

    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(3)]
    private Mobile _owner;

    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(4)]
    private int _baitUses;

    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(5)]
    private bool _enhancedBait;

    private TimerExecutionToken _timer;

    [Constructible]
    public LobsterTrap() : base(17615)
    {
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public Type BaitType
    {
        get => _baitIndex < 0 ? null : FishInfo.GetTypeFromIndex(_baitIndex);
        set
        {
            BaitIndex = value == null ? -1 : FishInfo.GetIndexFromType(value);

            if (_baitIndex < 0)
            {
                EnhancedBait = false;
                BaitUses = 0;
            }

            InvalidateProperties();
        }
    }

    public override int LabelNumber => _owner == null ? 1096487 : 0;
    public override bool DisplaysContent => false;

    public void OnTelekinesis(Mobile from)
    {
        if (_inUse && ItemID == BuoyID && CanUseTrap(from))
        {
            EndTimer(from);
        }
    }

    public override bool OnDragDropInto(Mobile from, Item item, Point3D p) => false;

    public override bool OnDragDrop(Mobile from, Item dropped) => false;

    public override bool TryDropItem(Mobile from, Item dropped, bool sendFullMessage) => false;

    public override void AddNameProperty(IPropertyList list)
    {
        if (ItemID == TrapID)
        {
            if (Items.Count == 0)
            {
                list.Add(1116389); // empty lobster trap
            }
            else if (Items.Count >= MaxCatch)
            {
                list.Add(1149599); // full lobster trap
            }
            else
            {
                list.Add(1096487); // lobster trap
            }
        }
        else if (_owner == null)
        {
            list.Add(1096487); // lobster trap
        }
        else
        {
            list.Add(1116390, _owner.Name); // ~1_NAME~'s lobster trap
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        var bait = BaitType;

        if (bait != null)
        {
            var label = FishInfo.GetFishLabel(bait);

            if (label is int number)
            {
                list.Add(1116468, $"#{number}"); // baited to attract: ~1_val~
            }
            else if (label is string text)
            {
                list.Add(1116468, text);
            }

            list.Add(1116466, _baitUses.ToString()); // amount: ~1_val~
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack))
        {
            if (ItemID == BuoyID)
            {
                ItemID = TrapID;
            }

            if (Items.Count > 0)
            {
                DumpContents(from);
                from.SendMessage("You dump the contents of the lobster trap into your pack.");
            }
            else
            {
                from.SendLocalizedMessage(500974); // What water do you want to fish in?
                from.BeginTarget(-1, true, TargetFlags.None, OnTarget);
            }
        }
        else if (ItemID == BuoyID)
        {
            if (RootParent != null)
            {
                ItemID = TrapID;
            }

            InvalidateProperties();

            if (CanUseTrap(from))
            {
                EndTimer(from);
            }

            InvalidateProperties();
        }
        else
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
        }
    }

    private void DumpContents(Mobile from)
    {
        var pack = from.Backpack;

        foreach (var item in new List<Item>(Items))
        {
            if (item == null)
            {
                continue;
            }

            if (pack == null || !pack.TryDropItem(from, item, false))
            {
                item.MoveToWorld(from.Location, from.Map);
            }

            from.SendLocalizedMessage(1116386, $"#{item.LabelNumber}"); // You remove ~1_val~ from the trap.
        }
    }

    public void OnTarget(Mobile from, object targeted)
    {
        if (Deleted || _inUse)
        {
            return;
        }

        if (targeted is not IPoint3D pnt)
        {
            return;
        }

        var map = from.Map;

        if (map == null || map == Map.Internal)
        {
            return;
        }

        int x = pnt.X, y = pnt.Y, z = pnt.Z;

        if (!from.InRange(pnt, 6))
        {
            from.SendLocalizedMessage(1116388); // The trap is too cumbersome to deploy that far away.
        }
        else if (!IsValidTile(targeted, map))
        {
            from.SendMessage("You cannot deploy a trap there!"); // ServUO: TODO cliloc
        }
        else if (!IsValidLocation(x, y, z, map))
        {
            from.SendLocalizedMessage(1116393); // The location is too close to another trap.
        }
        else
        {
            Owner = from;
            ItemID = BuoyID;
            InvalidateProperties();
            Movable = false;
            MoveToWorld(new Point3D(x, y, z), map);
            Bobs = 0;
            InUse = true;
            StartTimer();

            Effects.PlaySound(this, Utility.Random(0x025, 3));
            Effects.SendMovingEffect(from, this, TrapID, 7, 0);
        }
    }

    public void StartTimer()
    {
        _timer.Cancel();
        Timer.StartTimer(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), OnTick, out _timer);
    }

    public void EndTimer(Mobile from)
    {
        _timer.Cancel();

        from ??= _owner;

        if (from == null)
        {
            return;
        }

        Movable = true;
        ItemID = TrapID;
        InvalidateProperties();
        InUse = false;

        if (RootParent == null)
        {
            if (from.Backpack == null || !from.Backpack.TryDropItem(from, this, false))
            {
                MoveToWorld(from.Location, from.Map);
            }
        }
    }

    /// <summary>Deep water as SpecialFishingNet's private ValidateDeepWater reads it: the land tile under the trap.</summary>
    public static bool IsDeepWaterTile(Map map, int x, int y)
    {
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        var tileID = map.Tiles.GetLandTile(x, y).ID;

        return tileID is >= 0x00A8 and <= 0x00AB or >= 0x0136 and <= 0x0137;
    }

    public void OnTick()
    {
        Bobs = _bobs + 1;

        PublicOverheadMessage(MessageType.Regular, 0, 1116364); // **bob**

        if (_owner != null && (!IsDeepWaterTile(Map, X, Y) || _owner.Skills[SkillName.Fishing].Base >= 75.0))
        {
            _owner.CheckSkill(SkillName.Fishing, 0, _owner.Skills[SkillName.Fishing].Cap);
        }

        if (!_inUse)
        {
            EndTimer(null);
            return;
        }

        if (_bobs * 5 > Utility.Random(100))
        {
            OnTrapLost();
            return;
        }

        var bump = _bobs / 100.0;

        var type = _owner == null ? null : FishInfo.GetSpecialItem(_owner, this, Location, bump, this is LavaLobsterTrap);

        if (type != null)
        {
            var item = Loot.Construct(type);

            if (item == null)
            {
                return;
            }

            DropItem(item);

            if (item is RareCrabAndLobster fish)
            {
                fish.Fisher = _owner;
                fish.DateCaught = Core.Now;
                fish.Weight = Utility.RandomMinMax(10, 200);
                fish.Stackable = false;
            }

            _owner?.SendMessage("It looks like you caught something!");

            CheckBait();
        }
        else if (Utility.RandomBool())
        {
            Item item = Utility.RandomBool() ? new Crab() : new Lobster();

            _owner?.SendMessage("It looks like you caught something!");

            DropItem(item);
            CheckBait();
        }
    }

    private void CheckBait()
    {
        if (_baitIndex >= 0)
        {
            BaitUses--;

            if (_baitUses <= 0)
            {
                BaitType = null;
                EnhancedBait = false;

                _owner?.SendMessage("You have used up the bait on your lobster trap.");
            }
        }
    }

    public void OnTrapLost()
    {
        _timer.Cancel();

        Effects.PlaySound(this, Utility.Random(0x025, 3));

        if (_owner != null)
        {
            foreach (var mob in GetMobilesInRange(12))
            {
                if (mob is PlayerMobile)
                {
                    mob.SendLocalizedMessage(1116385, _owner.Name); // ~1_NAME~'s trap bouy is pulled beneath the waves.
                }
            }
        }

        Delete();
    }

    private bool CanUseTrap(Mobile from)
    {
        if (_owner == null || RootParent != null)
        {
            return false;
        }

        if (!from.InRange(Location, 6))
        {
            from.SendLocalizedMessage(500295); // You are too far away to do that.
            return false;
        }

        // the owner, or the owner's guild
        if (_owner == from || from.Guild != null && from.Guild == _owner.Guild)
        {
            return true;
        }

        // the owner's party
        var party = Party.Get(from);

        if (party != null && party == Party.Get(_owner))
        {
            return true;
        }

        // Felucca rules
        if (from.Map?.Rules == MapRules.FeluccaRules)
        {
            from.CriminalAction(true);
            from.SendLocalizedMessage(1149823); // The owner of the lobster trap notices you committing a criminal act!

            _owner.SendMessage($"You notice {from.Name} taking your lobster trap out of the water!");

            return true;
        }

        from.SendLocalizedMessage(1116391); // You realize that the trap isn't yours so you leave it alone.
        return false;
    }

    public virtual bool IsValidTile(object targeted, Map map)
    {
        var tileID = targeted switch
        {
            LandTarget land => land.TileID,
            StaticTarget st => st.ItemID,
            _ => 0
        };

        var tiles = UseableTiles;

        for (var i = 0; i < tiles.Length; i++)
        {
            if (tiles[i] == tileID)
            {
                return true;
            }
        }

        return false;
    }

    public bool IsValidLocation(int x, int y, int z, Map map)
    {
        foreach (var item in map.GetItemsInRange(new Point3D(x, y, z), 1))
        {
            if (item is LobsterTrap)
            {
                return false;
            }
        }

        return true;
    }

    public virtual int[] UseableTiles => WaterTiles;

    public static readonly int[] WaterTiles =
    {
        // Deep water
        0x00AA, 0x00A9,
        0x00A8, 0x00AB,
        0x0136, 0x0137,
        // Shallow water
        0x5797, 0x579C,
        0x746E, 0x7485,
        0x7490, 0x74AB,
        0x74B5, 0x75D5,
        // Static tiles
        0x1797, 0x1798,
        0x1799, 0x179A,
        0x179B, 0x179C
    };

    /// <summary>A deleted trap stops bobbing (ServUO leaves its timer running; a tick on a deleted item would throw).</summary>
    public override void OnDelete()
    {
        _timer.Cancel();
        base.OnDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (_baitIndex >= 0 && _baitUses <= 0)
        {
            BaitType = null;
        }

        if (_inUse)
        {
            StartTimer();
        }
    }
}
