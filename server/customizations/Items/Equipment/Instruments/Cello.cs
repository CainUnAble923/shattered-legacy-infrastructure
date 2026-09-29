// ServUO: Items/Equipment/Instruments/Cello.cs (CC9 close, 2026-09-29). A King's Collection instrument: a one-tile
// addon whose component plays sound 0x66D on double-click (InstrumentedAddonComponent), and its deed, which
// keeps its hue on the addon. Reached by carpentry (Engines/Craft/KingsCollectionCarpentryRecipes.cs, ServUO
// DefCarpentry.cs). [Flipable] is pinned's [Flippable], same body. ServUO's component has no [Constructable], nor
// does this one.

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0x4C3E, 0x4C3F)]
[SerializationGenerator(0, false)]
public partial class CelloComponent : InstrumentedAddonComponent
{
    public CelloComponent() : base(0x4C3E, 0x66D)
    {
    }

    public override int LabelNumber => 1098390; // cello
}

[SerializationGenerator(0, false)]
public partial class CelloDeed : BaseAddonDeed
{
    [Constructible]
    public CelloDeed()
    {
    }

    public override int LabelNumber => 1098390; // cello

    public override BaseAddon Addon => new CelloAddon();
}

[SerializationGenerator(0, false)]
public partial class CelloAddon : BaseAddon
{
    [Constructible]
    public CelloAddon()
    {
        AddComponent(new CelloComponent(), 0, 0, 0);
    }

    public override BaseAddonDeed Deed => new CelloDeed();
    public override bool RetainDeedHue => true;
}
