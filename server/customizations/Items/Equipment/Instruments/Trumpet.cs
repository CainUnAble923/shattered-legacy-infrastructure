// ServUO: Items/Equipment/Instruments/Trumpet.cs (CC9 close, 2026-09-29). A King's Collection instrument: a one-tile
// addon whose component plays sound 0x66F on double-click (InstrumentedAddonComponent), and its deed, which
// keeps its hue on the addon. Reached by carpentry (Engines/Craft/KingsCollectionCarpentryRecipes.cs, ServUO
// DefCarpentry.cs). [Flipable] is pinned's [Flippable], same body. ServUO's component has no [Constructable], nor
// does this one.

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0x4C3C, 0x4C3D)]
[SerializationGenerator(0, false)]
public partial class TrumpetComponent : InstrumentedAddonComponent
{
    public TrumpetComponent() : base(0x4C3C, 0x66F)
    {
    }

    public override int LabelNumber => 1098388; // trumpet
}

[SerializationGenerator(0, false)]
public partial class TrumpetDeed : BaseAddonDeed
{
    [Constructible]
    public TrumpetDeed()
    {
    }

    public override int LabelNumber => 1098388; // trumpet

    public override BaseAddon Addon => new TrumpetAddon();
}

[SerializationGenerator(0, false)]
public partial class TrumpetAddon : BaseAddon
{
    [Constructible]
    public TrumpetAddon()
    {
        AddComponent(new TrumpetComponent(), 0, 0, 0);
    }

    public override BaseAddonDeed Deed => new TrumpetDeed();
    public override bool RetainDeedHue => true;
}
