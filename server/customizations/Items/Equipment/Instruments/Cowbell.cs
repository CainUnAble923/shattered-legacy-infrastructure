// ServUO: Items/Equipment/Instruments/Cowbell.cs (CC9 close, 2026-09-29). A King's Collection instrument: a one-tile
// addon whose component plays sound 0x66E on double-click (InstrumentedAddonComponent), and its deed, which
// keeps its hue on the addon. Reached by carpentry (Engines/Craft/KingsCollectionCarpentryRecipes.cs, ServUO
// DefCarpentry.cs). [Flipable] is pinned's [Flippable], same body. ServUO's component has no [Constructable], nor
// does this one.

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0x4C5A, 0x4C5B)]
[SerializationGenerator(0, false)]
public partial class CowBellComponent : InstrumentedAddonComponent
{
    public CowBellComponent() : base(0x4C5A, 0x66E)
    {
    }

    public override int LabelNumber => 1098418; // cowbell
}

[SerializationGenerator(0, false)]
public partial class CowBellDeed : BaseAddonDeed
{
    [Constructible]
    public CowBellDeed()
    {
    }

    public override int LabelNumber => 1098418; // cowbell

    public override BaseAddon Addon => new CowBellAddon();
}

[SerializationGenerator(0, false)]
public partial class CowBellAddon : BaseAddon
{
    [Constructible]
    public CowBellAddon()
    {
        AddComponent(new CowBellComponent(), 0, 0, 0);
    }

    public override BaseAddonDeed Deed => new CowBellDeed();
    public override bool RetainDeedHue => true;
}
