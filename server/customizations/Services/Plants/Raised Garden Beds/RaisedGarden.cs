// cc-P128: ServUO pub57 Scripts/Services/Plants/Raised Garden Beds/RaisedGarden.cs -> ours. The raised garden bed
// (the "Rustic" theme pack): one deed that asks for a direction (south, east, large or small) and places the matching
// addon, whose components are garden plots. A seed used on an empty plot (server/patches/Seed-raised-garden-plot.patch)
// raises a mound (RaisedGardenPlantItem) on it; a bed cannot be chopped while any plot holds a plant.

using ModernUO.Serialization;
using Server.Engines.Plants;
using Server.Gumps;
using Server.Network;

namespace Server.Items;

public enum RaisedGardenDirection
{
    South = 1,
    East,
    Large,
    Small
}

[TypeAlias("Server.Items.RaisedGardenSmallAddon", "Server.Items.RaisedGardenSouthAddon", "Server.Items.RaisedGardenEastAddon", "Server.Items.RaisedGardenLargeAddon")]
[SerializationGenerator(0, false)]
public partial class RaisedGardenAddon : BaseAddon
{
    [Constructible]
    public RaisedGardenAddon(RaisedGardenDirection direction = RaisedGardenDirection.South)
    {
        switch (direction)
        {
            case RaisedGardenDirection.Large:
                AddComponent(new GardenAddonComponent(19234), 0, 0, 0);
                AddComponent(new GardenAddonComponent(19240), 1, 0, 0);
                AddComponent(new GardenAddonComponent(19235), 2, 0, 0);
                AddComponent(new GardenAddonComponent(19237), 2, 1, 0);
                AddComponent(new GardenAddonComponent(19239), 2, 2, 0);
                AddComponent(new GardenAddonComponent(19242), 1, 2, 0);
                AddComponent(new GardenAddonComponent(19238), 0, 2, 0);
                AddComponent(new GardenAddonComponent(19236), 0, 1, 0);
                AddComponent(new GardenAddonComponent(19241), 1, 1, 0);
                break;
            case RaisedGardenDirection.East:
                AddComponent(new GardenAddonComponent(19234), 0, 0, 0);
                AddComponent(new GardenAddonComponent(19235), 1, 0, 0);
                AddComponent(new GardenAddonComponent(19237), 1, 1, 0);
                AddComponent(new GardenAddonComponent(19239), 1, 2, 0);
                AddComponent(new GardenAddonComponent(19238), 0, 2, 0);
                AddComponent(new GardenAddonComponent(19236), 0, 1, 0);
                break;
            case RaisedGardenDirection.South:
                AddComponent(new GardenAddonComponent(19234), 0, 0, 0);
                AddComponent(new GardenAddonComponent(19240), 1, 0, 0);
                AddComponent(new GardenAddonComponent(19235), 2, 0, 0);
                AddComponent(new GardenAddonComponent(19239), 2, 1, 0);
                AddComponent(new GardenAddonComponent(19242), 1, 1, 0);
                AddComponent(new GardenAddonComponent(19238), 0, 1, 0);
                break;
            case RaisedGardenDirection.Small:
                AddComponent(new GardenAddonComponent(19234), 0, 0, 0);
                AddComponent(new GardenAddonComponent(19235), 1, 0, 0);
                AddComponent(new GardenAddonComponent(19239), 1, 1, 0);
                AddComponent(new GardenAddonComponent(19238), 0, 1, 0);
                break;
        }
    }

    public override BaseAddonDeed Deed => new RaisedGardenDeed();

    public bool HasPlants
    {
        get
        {
            foreach (var comp in Components)
            {
                if (comp is GardenAddonComponent { Plant: not null })
                {
                    return true;
                }
            }

            return false;
        }
    }

    public override void OnChop(Mobile from)
    {
        if (HasPlants)
        {
            from.SendLocalizedMessage(1150383); // You need to remove all plants through the plant menu before destroying this.
            return;
        }

        base.OnChop(from);
    }
}

[TypeAlias("Server.Items.RaisedGardenSmallAddonDeed", "Server.Items.RaisedGardenEastAddonDeed", "Server.Items.RaisedGardenSouthAddonDeed", "Server.Items.RaisedGardenLargeAddonDeed")]
[SerializationGenerator(0, false)]
public partial class RaisedGardenDeed : BaseAddonDeed
{
    private RaisedGardenDirection _direction = RaisedGardenDirection.South;

    [Constructible]
    public RaisedGardenDeed() => LootType = LootType.Blessed;

    public override int LabelNumber => 1150359; // Raised Garden Bed

    public override BaseAddon Addon => new RaisedGardenAddon(_direction);

    [CommandProperty(AccessLevel.GameMaster)]
    public RaisedGardenDirection Direction
    {
        get => _direction;
        set => _direction = value;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack))
        {
            from.CloseGump<InternalGump>();
            from.SendGump(new InternalGump(this));
        }
        else
        {
            from.SendLocalizedMessage(1062334); // This item must be in your backpack to be used.
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1150651); // * Requires the "Rustic" theme pack
    }

    /// <summary>The chosen direction, then pinned's placement target.</summary>
    public void Place(Mobile from, RaisedGardenDirection direction)
    {
        _direction = direction;
        base.OnDoubleClick(from);
    }

    private class InternalGump : Gump
    {
        private readonly RaisedGardenDeed _deed;

        public InternalGump(RaisedGardenDeed deed) : base(60, 36)
        {
            _deed = deed;

            AddPage(0);

            AddBackground(0, 0, 273, 324, 0x13BE);
            AddImageTiled(10, 10, 253, 20, 0xA40);
            AddImageTiled(10, 40, 253, 244, 0xA40);
            AddImageTiled(10, 294, 253, 20, 0xA40);
            AddAlphaRegion(10, 10, 253, 304);
            AddButton(10, 294, 0xFB1, 0xFB2, 0);
            AddHtmlLocalized(45, 296, 450, 20, 1060051, 0x7FFF); // CANCEL
            AddHtmlLocalized(14, 12, 273, 20, 1076170, 0x7FFF); // Choose Direction

            AddPage(1);

            AddButton(19, 49, 0x845, 0x846, 1);
            AddHtmlLocalized(44, 47, 213, 20, 1150381, 0x7FFF); // Raised Garden Bed (South)

            AddButton(19, 73, 0x845, 0x846, 2);
            AddHtmlLocalized(44, 71, 213, 20, 1150382, 0x7FFF); // Raised Garden Bed (East)

            AddButton(19, 97, 0x845, 0x846, 3);
            AddHtmlLocalized(44, 95, 213, 20, 1150620, 0x7FFF); // Raised Garden Bed (Large)

            AddButton(19, 121, 0x845, 0x846, 4);
            AddHtmlLocalized(44, 119, 213, 20, 1150621, 0x7FFF); // Raised Garden Bed (Small)
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (_deed?.Deleted != false || info.ButtonID is < 1 or > 4)
            {
                return;
            }

            _deed.Place(sender.Mobile, (RaisedGardenDirection)info.ButtonID);
        }
    }
}

[SerializationGenerator(0, false)]
public partial class GardenAddonComponent : AddonComponent
{
    // The saved link to the mound; Plant below is the checked view of it (the generator names this one PlantLink).
    [SerializableField(0)]
    private PlantItem _plantLink;

    public GardenAddonComponent(int itemID) : base(itemID)
    {
    }

    public override int LabelNumber => 1150359; // Raised Garden Bed

    [CommandProperty(AccessLevel.GameMaster)]
    public PlantItem Plant
    {
        get
        {
            if (_plantLink != null && (_plantLink.X != X || _plantLink.Y != Y || _plantLink.Map != Map || _plantLink.Deleted))
            {
                _plantLink = null;
            }

            return _plantLink;
        }
        set
        {
            _plantLink = value;
            this.MarkDirty();

            if (_plantLink != null && (_plantLink.X != X || _plantLink.Y != Y || _plantLink.Map != Map || _plantLink.Deleted))
            {
                _plantLink = null;
            }
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        if (_plantLink?.Deleted == false)
        {
            _plantLink.Z -= 5;
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (_plantLink is RaisedGardenPlantItem { Component: null } mound)
        {
            mound.Component = this;
        }
    }
}
