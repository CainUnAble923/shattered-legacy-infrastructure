// cc-P128: ServUO pub57 Scripts/Services/Plants/Raised Garden Beds/RaisedGardenPlantItem.cs -> ours. The mound of dirt
// on a raised garden plot: a plant that needs no upkeep (no water, no maladies), cannot be picked up, grows while its
// plot exists, and is tended only by the house's owner and co-owners. The hooks it overrides (RequiresUpkeep, the dirt
// and green art, the three messages, IsUsableBy) are server/patches/PlantItem-raised-garden-hooks.patch and
// PlantSystem-raised-garden-upkeep.patch, ServUO's own virtuals added to pinned's PlantItem and PlantSystem.

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Multis;
using Server.Network;

namespace Server.Engines.Plants;

[SerializationGenerator(0, false)]
public partial class RaisedGardenPlantItem : PlantItem
{
    // The saved link to the plot; Component below is the checked view of it (the generator names this one ComponentLink).
    [SerializableField(0)]
    private GardenAddonComponent _componentLink;

    [Constructible]
    public RaisedGardenPlantItem(bool fertileDirt = false) : base(fertileDirt)
    {
        Movable = false;
        ItemID = BowlOfDirtID;
    }

    public override bool RequiresUpkeep => false;
    public override int BowlOfDirtID => 2323;
    public override int GreenBowlID => PlantStatus <= PlantStatus.Stage3 ? 0xC7E : 0xC62;

    public override int ContainerLocalization => 1150436; // mound
    public override int OnPlantLocalization => 1150442; // You plant the seed in the mound of dirt.
    public override int CantUseLocalization => 1150511; // That is not your gardening plot.

    public override int LabelNumber
    {
        get
        {
            var label = base.LabelNumber;

            if (label == 1029913)
            {
                label = 1022321; // dirt patch
            }

            return label;
        }
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public GardenAddonComponent Component
    {
        get
        {
            if (_componentLink != null && (_componentLink.X != X || _componentLink.Y != Y || _componentLink.Map != Map || _componentLink.Deleted))
            {
                _componentLink = null;
            }

            return _componentLink;
        }
        set
        {
            _componentLink = value;
            this.MarkDirty();

            if (_componentLink != null && (_componentLink.X != X || _componentLink.Y != Y || _componentLink.Map != Map || _componentLink.Deleted))
            {
                _componentLink = null;
            }
        }
    }

    public override bool ValidGrowthLocation => RootParent == null && Component != null && !Movable && !Deleted;

    [CommandProperty(AccessLevel.GameMaster)]
    public DateTime NextGrowth => PlantSystem?.NextGrowth ?? DateTime.MinValue;

    [CommandProperty(AccessLevel.GameMaster)]
    public PlantGrowthIndicator GrowthIndicator => PlantSystem?.GrowthIndicator ?? PlantGrowthIndicator.None;

    [CommandProperty(AccessLevel.GameMaster)]
    public bool ForceGrow
    {
        get => false;
        set
        {
            if (value && PlantSystem != null)
            {
                PlantSystem.NextGrowth = Core.Now;
                PlantSystem.DoGrowthCheck();
            }
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        if (_componentLink?.Plant == this)
        {
            _componentLink.Plant = null;
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (PlantStatus >= PlantStatus.DecorativePlant)
        {
            return;
        }

        var loc = GetWorldLocation();

        if (!from.InLOS(loc) || !from.InRange(loc, 4))
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3E9, 1019045); // I can't reach that.
            return;
        }

        if (!IsUsableBy(from))
        {
            LabelTo(from, 1150327); // Only the house owner and co-owners can use the raised garden bed.
            return;
        }

        MainPlantGump.DisplayTo(from, this);
    }

    public override bool IsUsableBy(Mobile from)
    {
        var house = BaseHouse.FindHouseAt(this);
        return house != null && house.IsCoOwner(from) && IsAccessibleTo(from);
    }
}
