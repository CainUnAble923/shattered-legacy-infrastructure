using ModernUO.Serialization;

namespace Server.Items;

// -----------------------------------------------------------------------------
// Shattered Legacy - Extended Ore and Ingot types
//
// Tiers 10-17 in the CraftResource enum (Platinum through Celestial).
// These ores are mined via normal Mining once GM skill is reached, with
// probability weights set in ClusterFMiningExtension.
//
// Ore classes inherit BaseOre - hue is auto-set from CraftResources.GetHue().
// Ingot classes inherit BaseIngot - same hue behaviour.
// Both display their name via GetProperties (BaseOre/BaseIngot calls
//   CraftResources.GetName() when Number == 0, which it is for extended ores).
//
// All classes use SerializationGenerator(0, false) - no migration files needed
// because these are brand-new types with no existing world saves.
// -----------------------------------------------------------------------------

// -- Ore classes --------------------------------------------------------------

[SerializationGenerator(0, false)]
public partial class PlatinumOre : BaseOre
{
    [Constructible]
    public PlatinumOre(int amount = 1) : base(CraftResource.Platinum, amount) { }

    public override BaseIngot GetIngot() => new PlatinumIngot();
}

[SerializationGenerator(0, false)]
public partial class ToxicOre : BaseOre
{
    [Constructible]
    public ToxicOre(int amount = 1) : base(CraftResource.Toxic, amount) { }

    public override BaseIngot GetIngot() => new ToxicIngot();
}

[SerializationGenerator(0, false)]
public partial class BlazeOre : BaseOre
{
    [Constructible]
    public BlazeOre(int amount = 1) : base(CraftResource.Blaze, amount) { }

    public override BaseIngot GetIngot() => new BlazeIngot();
}

[SerializationGenerator(0, false)]
public partial class FrostOre : BaseOre
{
    [Constructible]
    public FrostOre(int amount = 1) : base(CraftResource.Frost, amount) { }

    public override BaseIngot GetIngot() => new FrostIngot();
}

[SerializationGenerator(0, false)]
public partial class ObsidianOre : BaseOre
{
    [Constructible]
    public ObsidianOre(int amount = 1) : base(CraftResource.Obsidian, amount) { }

    public override BaseIngot GetIngot() => new ObsidianIngot();
}

[SerializationGenerator(0, false)]
public partial class MythrilOre : BaseOre
{
    [Constructible]
    public MythrilOre(int amount = 1) : base(CraftResource.Mythril, amount) { }

    public override BaseIngot GetIngot() => new MythrilIngot();
}

[SerializationGenerator(0, false)]
public partial class AdamantiumOre : BaseOre
{
    [Constructible]
    public AdamantiumOre(int amount = 1) : base(CraftResource.Adamantium, amount) { }

    public override BaseIngot GetIngot() => new AdamantiumIngot();
}

[SerializationGenerator(0, false)]
public partial class CelestialOre : BaseOre
{
    [Constructible]
    public CelestialOre(int amount = 1) : base(CraftResource.Celestial, amount) { }

    public override BaseIngot GetIngot() => new CelestialIngot();
}

// -- Ingot classes -------------------------------------------------------------

[SerializationGenerator(0, false)]
public partial class PlatinumIngot : BaseIngot
{
    [Constructible]
    public PlatinumIngot(int amount = 1) : base(CraftResource.Platinum, amount) { }
}

[SerializationGenerator(0, false)]
public partial class ToxicIngot : BaseIngot
{
    [Constructible]
    public ToxicIngot(int amount = 1) : base(CraftResource.Toxic, amount) { }
}

[SerializationGenerator(0, false)]
public partial class BlazeIngot : BaseIngot
{
    [Constructible]
    public BlazeIngot(int amount = 1) : base(CraftResource.Blaze, amount) { }
}

[SerializationGenerator(0, false)]
public partial class FrostIngot : BaseIngot
{
    [Constructible]
    public FrostIngot(int amount = 1) : base(CraftResource.Frost, amount) { }
}

[SerializationGenerator(0, false)]
public partial class ObsidianIngot : BaseIngot
{
    [Constructible]
    public ObsidianIngot(int amount = 1) : base(CraftResource.Obsidian, amount) { }
}

[SerializationGenerator(0, false)]
public partial class MythrilIngot : BaseIngot
{
    [Constructible]
    public MythrilIngot(int amount = 1) : base(CraftResource.Mythril, amount) { }
}

[SerializationGenerator(0, false)]
public partial class AdamantiumIngot : BaseIngot
{
    [Constructible]
    public AdamantiumIngot(int amount = 1) : base(CraftResource.Adamantium, amount) { }
}

[SerializationGenerator(0, false)]
public partial class CelestialIngot : BaseIngot
{
    [Constructible]
    public CelestialIngot(int amount = 1) : base(CraftResource.Celestial, amount) { }
}

// -- Granite classes (cc-P36 Part A, F1) ----------------------------------------
//
// Pinned Mining.GetResourceType hands a GM miner with stone mining on resource.Types[1], 10% of the time
// (patches/Mining.cs:284-288, unchanged from pinned). These ores had one type each, so that roll threw
// IndexOutOfRangeException inside the harvest timer. Every stock colored ore has a matching granite as Types[1]
// (pinned Mining.cs:115-187), so these follow it; Masonry takes each at its metal's tier, as stock's granites
// (DefMasonry.cs:139-147) sit at their ores' requirements (ClusterFMiningExtension.ExtendMasonrySubResources).

[SerializationGenerator(0, false)]
public partial class PlatinumGranite : BaseGranite
{
    [Constructible]
    public PlatinumGranite() : base(CraftResource.Platinum) { }
}

[SerializationGenerator(0, false)]
public partial class ToxicGranite : BaseGranite
{
    [Constructible]
    public ToxicGranite() : base(CraftResource.Toxic) { }
}

[SerializationGenerator(0, false)]
public partial class BlazeGranite : BaseGranite
{
    [Constructible]
    public BlazeGranite() : base(CraftResource.Blaze) { }
}

[SerializationGenerator(0, false)]
public partial class FrostGranite : BaseGranite
{
    [Constructible]
    public FrostGranite() : base(CraftResource.Frost) { }
}

[SerializationGenerator(0, false)]
public partial class ObsidianGranite : BaseGranite
{
    [Constructible]
    public ObsidianGranite() : base(CraftResource.Obsidian) { }
}

[SerializationGenerator(0, false)]
public partial class MythrilGranite : BaseGranite
{
    [Constructible]
    public MythrilGranite() : base(CraftResource.Mythril) { }
}

[SerializationGenerator(0, false)]
public partial class AdamantiumGranite : BaseGranite
{
    [Constructible]
    public AdamantiumGranite() : base(CraftResource.Adamantium) { }
}

[SerializationGenerator(0, false)]
public partial class CelestialGranite : BaseGranite
{
    [Constructible]
    public CelestialGranite() : base(CraftResource.Celestial) { }
}
