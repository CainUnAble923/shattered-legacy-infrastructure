using ModernUO.Serialization;

namespace Server.Items;

// ─────────────────────────────────────────────────────────────────────────────
// Shattered Legacy — Extended Lumber (Log and Board) types
//
// Tiers 308–315 in the CraftResource enum (Ironwood through Starwood).
// These logs are chopped via normal Lumberjacking at GM skill, with probability
// weights set in ClusterFLumberjackingExtension.
//
// Log subclasses inherit Log — hue is auto-set from CraftResources.GetHue().
// Board subclasses inherit Board — same hue behaviour.
// GetProperties falls back to CraftResources.GetName() because number=0 in
// the CraftResourceInfo entries (same pattern as extended ores).
//
// All classes use SerializationGenerator(0, false) — no migration files needed
// because these are brand-new types with no existing world saves.
// ─────────────────────────────────────────────────────────────────────────────

// ── Log classes ───────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class IronwoodLog : Log
{
    [Constructible]
    public IronwoodLog(int amount = 1) : base(CraftResource.Ironwood, amount) { }

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 100, new IronwoodBoard());
}

[SerializationGenerator(0, false)]
public partial class GhostwoodLog : Log
{
    [Constructible]
    public GhostwoodLog(int amount = 1) : base(CraftResource.Ghostwood, amount) { }

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 100, new GhostwoodBoard());
}

[SerializationGenerator(0, false)]
public partial class EmberbarkLog : Log
{
    [Constructible]
    public EmberbarkLog(int amount = 1) : base(CraftResource.Emberbark, amount) { }

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 100, new EmberbarkBoard());
}

[SerializationGenerator(0, false)]
public partial class FrostbarkLog : Log
{
    [Constructible]
    public FrostbarkLog(int amount = 1) : base(CraftResource.Frostbark, amount) { }

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 100, new FrostbarkBoard());
}

[SerializationGenerator(0, false)]
public partial class ShadowbarkLog : Log
{
    [Constructible]
    public ShadowbarkLog(int amount = 1) : base(CraftResource.Shadowbark, amount) { }

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 100, new ShadowbarkBoard());
}

[SerializationGenerator(0, false)]
public partial class RunewoodLog : Log
{
    [Constructible]
    public RunewoodLog(int amount = 1) : base(CraftResource.Runewood, amount) { }

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 100, new RunewoodBoard());
}

[SerializationGenerator(0, false)]
public partial class VoidwoodLog : Log
{
    [Constructible]
    public VoidwoodLog(int amount = 1) : base(CraftResource.Voidwood, amount) { }

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 100, new VoidwoodBoard());
}

[SerializationGenerator(0, false)]
public partial class StarwoodLog : Log
{
    [Constructible]
    public StarwoodLog(int amount = 1) : base(CraftResource.Starwood, amount) { }

    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 100, new StarwoodBoard());
}

// ── Board classes ─────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class IronwoodBoard : Board
{
    [Constructible]
    public IronwoodBoard(int amount = 1) : base(CraftResource.Ironwood, amount) { }
}

[SerializationGenerator(0, false)]
public partial class GhostwoodBoard : Board
{
    [Constructible]
    public GhostwoodBoard(int amount = 1) : base(CraftResource.Ghostwood, amount) { }
}

[SerializationGenerator(0, false)]
public partial class EmberbarkBoard : Board
{
    [Constructible]
    public EmberbarkBoard(int amount = 1) : base(CraftResource.Emberbark, amount) { }
}

[SerializationGenerator(0, false)]
public partial class FrostbarkBoard : Board
{
    [Constructible]
    public FrostbarkBoard(int amount = 1) : base(CraftResource.Frostbark, amount) { }
}

[SerializationGenerator(0, false)]
public partial class ShadowbarkBoard : Board
{
    [Constructible]
    public ShadowbarkBoard(int amount = 1) : base(CraftResource.Shadowbark, amount) { }
}

[SerializationGenerator(0, false)]
public partial class RunewoodBoard : Board
{
    [Constructible]
    public RunewoodBoard(int amount = 1) : base(CraftResource.Runewood, amount) { }
}

[SerializationGenerator(0, false)]
public partial class VoidwoodBoard : Board
{
    [Constructible]
    public VoidwoodBoard(int amount = 1) : base(CraftResource.Voidwood, amount) { }
}

[SerializationGenerator(0, false)]
public partial class StarwoodBoard : Board
{
    [Constructible]
    public StarwoodBoard(int amount = 1) : base(CraftResource.Starwood, amount) { }
}
