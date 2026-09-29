// ServUO: Items/Equipment/Instruments/WallMountedBell.cs (CC9 close, 2026-09-29). The King's Collection wall bell,
// south- and east-facing: a one-tile addon, 10 above the floor, whose component is a plain InstrumentedAddonComponent
// playing sound 0x66C. Each deed keeps its hue on the addon. Reached by carpentry
// (Engines/Craft/KingsCollectionCarpentryRecipes.cs, ServUO DefCarpentry.cs:618 and :623).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class WallMountedBellSouthDeed : BaseAddonDeed
{
    [Constructible]
    public WallMountedBellSouthDeed()
    {
    }

    public override int LabelNumber => 1154162; // Wall Mounted Bell (South)

    public override BaseAddon Addon => new WallMountedBellSouthAddon();
}

[SerializationGenerator(0, false)]
public partial class WallMountedBellSouthAddon : BaseAddon
{
    [Constructible]
    public WallMountedBellSouthAddon()
    {
        AddComponent(new InstrumentedAddonComponent(0x4C5C, 0x66C), 0, 0, 10);
    }

    public override BaseAddonDeed Deed => new WallMountedBellSouthDeed();
    public override bool RetainDeedHue => true;
}

[SerializationGenerator(0, false)]
public partial class WallMountedBellEastDeed : BaseAddonDeed
{
    [Constructible]
    public WallMountedBellEastDeed()
    {
    }

    public override int LabelNumber => 1154163; // Wall Mounted Bell (East)

    public override BaseAddon Addon => new WallMountedBellEastAddon();
}

[SerializationGenerator(0, false)]
public partial class WallMountedBellEastAddon : BaseAddon
{
    [Constructible]
    public WallMountedBellEastAddon()
    {
        AddComponent(new InstrumentedAddonComponent(0x4C5D, 0x66C), 0, 0, 10);
    }

    public override BaseAddonDeed Deed => new WallMountedBellEastDeed();
    public override bool RetainDeedHue => true;
}
