using ModernUO.Serialization;

namespace Server.Items;

// ─────────────────────────────────────────────────────────────────────────────
// Shattered Legacy — Extended Ore and Ingot types
//
// Tiers 10–17 in the CraftResource enum (Platinum through Celestial).
// These ores are mined via normal Mining once GM skill is reached, with
// probability weights set in ClusterFMiningExtension.
//
// Ore classes inherit BaseOre — hue is auto-set from CraftResources.GetHue().
// Ingot classes inherit BaseIngot — same hue behaviour.
// Both display their name via GetProperties (BaseOre/BaseIngot calls
//   CraftResources.GetName() when Number == 0, which it is for extended ores).
//
// All classes use SerializationGenerator(0, false) — no migration files needed
// because these are brand-new types with no existing world saves.
// ─────────────────────────────────────────────────────────────────────────────

// ── Ore classes ──────────────────────────────────────────────────────────────

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

// ── Ingot classes ─────────────────────────────────────────────────────────────

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
