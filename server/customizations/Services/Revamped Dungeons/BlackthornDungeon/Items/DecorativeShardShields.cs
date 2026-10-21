// ServUO: Services/Revamped Dungeons/BlackthornDungeon/Items/DecorativeShardShields.cs (cc-P196).
//
// The Deed for a Decorative Shard Shield (1153729, an Agent of the Crown reward for 10 points) opens a picker of the
// 27 shard shields (items 25472 to 25498, tooltips 1104344 to 1104370, the shard names); the chosen shield is placed
// against a wall of the player's house, facing east (the id as picked) or south (the id plus 27), with a facing
// choice when both walls are there. A placed shield is a house addon that the owner can re-deed by double-clicking.
//
// Conversion notes: pinned's RewardDemolitionGump is reached through its static DisplayTo (its constructor is
// private); BaseHouse.Addons is a List<Item> here (ServUO keys a dictionary by placer); IChopable is pinned's
// IChoppable (Server namespace); the gumps are pinned's legacy Gump with the same Add* calls, OnResponse takes an
// "in RelayInfo"; TileData.ItemTable and MaxItemValue are the same. ServUO's picker accepts a button when
// "(id & 1) == 0 && id < 0x6380 || 0x6380 <= id <= 0x639A": the first half can never be true for a button in the
// Start..End range, so every shard button is accepted, which is what the player sees; copied as written.
// IsArtifact dropped (no pinned reader).

using ModernUO.Serialization;
using Server.Gumps;
using Server.Multis;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class DecorativeShardShield : Item, IAddon, IChoppable
{
    [Constructible]
    public DecorativeShardShield(int itemID = 0x6380) : base(itemID) => Movable = false;

    public override bool ForceShowProperties => ObjectPropertyList.Enabled;

    public Item Deed => new DecorativeShardShieldDeed();

    public bool FacingEast => ItemID <= 0x639A;

    public void OnChop(Mobile user) => OnDoubleClick(user);

    public override void OnDoubleClick(Mobile from)
    {
        if (from.InRange(Location, 2))
        {
            var house = BaseHouse.FindHouseAt(this);

            if (house != null && house.IsOwner(from))
            {
                RewardDemolitionGump.DisplayTo(from, this, 1049783); // Do you wish to re-deed this decoration?
            }
            else
            {
                from.SendLocalizedMessage(1049784); // You can only re-deed this decoration if you are the house owner or originally placed the decoration.
            }
        }
        else
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
        }
    }

    public bool CouldFit(IPoint3D p, Map map)
    {
        if (map == null || !map.CanFit(p.X, p.Y, p.Z, ItemData.Height))
        {
            return false;
        }

        if (FacingEast)
        {
            return BaseAddon.IsWall(p.X - 1, p.Y, p.Z, map); // west wall
        }

        return BaseAddon.IsWall(p.X, p.Y - 1, p.Z, map); // north wall
    }
}

[SerializationGenerator(0, false)]
public partial class DecorativeShardShieldDeed : Item
{
    public const int Start = 25472;
    public const int End = 25498;

    [Constructible]
    public DecorativeShardShieldDeed() : base(0x14F0)
    {
        LootType = LootType.Blessed;
        Weight = 1.0;
    }

    public override int LabelNumber => 1153729; // Deed for a Decorative Shard Shield

    public static int GetSouthItemID(int south) => south + 27;

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack))
        {
            from.CloseGump<InternalGump>();
            from.SendGump(new InternalGump(this));
        }
        else
        {
            from.SendLocalizedMessage(1042038); // You must have the object in your backpack to use it.
        }
    }

    private class InternalGump : Gump
    {
        private readonly DecorativeShardShieldDeed _shield;

        public InternalGump(DecorativeShardShieldDeed shield) : base(150, 50)
        {
            _shield = shield;

            Closable = true;
            Disposable = true;
            Draggable = true;
            Resizable = false;

            AddPage(0);

            AddBackground(50, 89, 647, 505, 2600);
            AddLabel(103, 114, 0, "Choose from the following:");

            var itemID = Start;

            for (var i = 0; i < 27; i++)
            {
                if (i <= 8)
                {
                    AddItem(82 + i * 65, 180, itemID);
                    AddTooltip(1104344 + i);
                    AddButton(92 + i * 65, 155, 0x845, 0x846, itemID);
                    AddTooltip(1104344 + i);
                }
                else if (i < 18)
                {
                    AddItem(82 + (i - 9) * 65, 330, itemID);
                    AddTooltip(1104344 + i);
                    AddButton(92 + (i - 9) * 65, 305, 0x845, 0x846, itemID);
                    AddTooltip(1104344 + i);
                }
                else
                {
                    AddItem(82 + (i - 18) * 65, 480, itemID);
                    AddTooltip(1104344 + i);
                    AddButton(92 + (i - 18) * 65, 455, 0x845, 0x846, itemID);
                    AddTooltip(1104344 + i);
                }

                itemID++;
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (_shield?.Deleted != false)
            {
                return;
            }

            var m = sender.Mobile;
            var id = info.ButtonID;

            if (id >= Start && id <= End)
            {
                if ((id & 0x1) == 0 && id < 0x6380 || id >= 0x6380 && id <= 0x639A)
                {
                    m.SendLocalizedMessage(1049780); // Where would you like to place this decoration?
                    m.Target = new InternalTarget(_shield, id);
                }
            }
        }
    }

    private class InternalTarget : Target
    {
        private readonly int _itemID;
        private readonly DecorativeShardShieldDeed _shield;

        public InternalTarget(DecorativeShardShieldDeed shield, int itemID) : base(-1, true, TargetFlags.None)
        {
            _shield = shield;
            _itemID = itemID;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_shield?.Deleted != false)
            {
                return;
            }

            if (!_shield.IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042038); // You must have the object in your backpack to use it.
                return;
            }

            var house = BaseHouse.FindHouseAt(from);

            if (house == null || !house.IsOwner(from))
            {
                from.SendLocalizedMessage(502092); // You must be in your house to do this.
                return;
            }

            var map = from.Map;

            if (targeted is not IPoint3D p || map == null)
            {
                return;
            }

            var p3d = new Point3D(p);
            var id = TileData.ItemTable[_itemID & TileData.MaxItemValue];

            if (!map.CanFit(p3d, id.Height))
            {
                from.SendLocalizedMessage(500269); // You cannot build that there.
                return;
            }

            house = BaseHouse.FindHouseAt(p3d, map, id.Height);

            if (house == null || !house.IsCoOwner(from))
            {
                from.SendLocalizedMessage(1042036); // That location is not in your house.
                return;
            }

            var north = BaseAddon.IsWall(p3d.X, p3d.Y - 1, p3d.Z, map);
            var west = BaseAddon.IsWall(p3d.X - 1, p3d.Y, p3d.Z, map);

            if (north && west)
            {
                from.CloseGump<FacingGump>();
                from.SendGump(new FacingGump(_shield, _itemID, p3d, house));
            }
            else if (north || west)
            {
                var shield = new DecorativeShardShield(north ? GetSouthItemID(_itemID) : _itemID);

                house.Addons.Add(shield);
                shield.MoveToWorld(p3d, map);

                _shield.Delete();
            }
            else
            {
                from.SendLocalizedMessage(1049781); // This decoration must be placed next to a wall.
            }
        }

        private class FacingGump : Gump
        {
            private readonly BaseHouse _house;
            private readonly int _itemID;
            private readonly Point3D _location;
            private readonly DecorativeShardShieldDeed _shield;

            public FacingGump(DecorativeShardShieldDeed shield, int itemID, Point3D location, BaseHouse house)
                : base(150, 50)
            {
                _shield = shield;
                _itemID = itemID;
                _location = location;
                _house = house;

                AddBackground(0, 0, 300, 150, 0xA28);

                AddItem(90, 30, itemID);
                AddItem(180, 30, GetSouthItemID(itemID));

                AddButton(50, 35, 0x867, 0x869, (int)Buttons.East);
                AddButton(145, 35, 0x867, 0x869, (int)Buttons.South);
            }

            private enum Buttons
            {
                Cancel,
                South,
                East
            }

            public override void OnResponse(NetState sender, in RelayInfo info)
            {
                if (_shield?.Deleted != false || _house == null)
                {
                    return;
                }

                DecorativeShardShield shield = null;

                if (info.ButtonID == (int)Buttons.East)
                {
                    shield = new DecorativeShardShield(_itemID);
                }
                else if (info.ButtonID == (int)Buttons.South)
                {
                    shield = new DecorativeShardShield(GetSouthItemID(_itemID));
                }

                if (shield != null)
                {
                    _house.Addons.Add(shield);
                    shield.MoveToWorld(_location, sender.Mobile.Map);

                    _shield.Delete();
                }
            }
        }
    }
}
