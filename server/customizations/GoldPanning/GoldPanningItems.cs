// cc-P781: gold panning's items. The prospector's pan is a stock harvest tool (pinned BaseHarvestTool: its uses, its
// double-click and its saved fields) on our GoldPanning system; the nuggets are plain stackable finds the Compact
// assayer buys (for gold) or takes for the Compact's ledger (for standing). Stock art only: a pewter plate for the pan,
// the ore piles in the gold metal hue for the nuggets (the source used fruit and ingot art). Notes in shard-migration
// notes/cc-P781-gold-panning.md. Every type here is new at version 0 (D40: no older data to read).

using ModernUO.Serialization;
using Server.Engines.GoldPanning;
using Server.Engines.Harvest;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ProspectorsPan : BaseHarvestTool
{
    public const int PanItemID = 0x9D7; // a pewter plate

    [Constructible]
    public ProspectorsPan() : this(GoldPanningTable.Current.Pan.Uses)
    {
    }

    [Constructible]
    public ProspectorsPan(int uses) : base(PanItemID, uses)
    {
        Weight = 2.0;
        Hue = 0x973; // dull iron
    }

    public override string DefaultName => "a prospector's pan";

    public override HarvestSystem HarvestSystem => GoldPanning.System;
}

/// <summary>A gold nugget from the pan: small, medium or large (GoldPanningTable.Sizes), stackable, sold to the assayer.</summary>
[SerializationGenerator(0, false)]
public abstract partial class GoldNugget : Item
{
    public const int GoldHue = 0x8A5; // the gold metal hue (CraftResource Gold)

    protected GoldNugget(int itemID, int amount) : base(itemID)
    {
        Stackable = true;
        Amount = amount;
        Hue = GoldHue;
        Weight = 0.1;
    }

    /// <summary>The size's key in gold-panning.json: small, medium or large.</summary>
    public abstract string SizeKey { get; }
}

[SerializationGenerator(0, false)]
public partial class SmallGoldNugget : GoldNugget
{
    [Constructible]
    public SmallGoldNugget() : this(1)
    {
    }

    [Constructible]
    public SmallGoldNugget(int amount) : base(0x19B7, amount)
    {
    }

    public override string SizeKey => "small";

    public override string DefaultName => "small gold nugget";
}

[SerializationGenerator(0, false)]
public partial class MediumGoldNugget : GoldNugget
{
    [Constructible]
    public MediumGoldNugget() : this(1)
    {
    }

    [Constructible]
    public MediumGoldNugget(int amount) : base(0x19BA, amount)
    {
        Weight = 0.3;
    }

    public override string SizeKey => "medium";

    public override string DefaultName => "medium gold nugget";
}

[SerializationGenerator(0, false)]
public partial class LargeGoldNugget : GoldNugget
{
    [Constructible]
    public LargeGoldNugget() : this(1)
    {
    }

    [Constructible]
    public LargeGoldNugget(int amount) : base(0x19B9, amount)
    {
        Weight = 1.0;
    }

    public override string SizeKey => "large";

    public override string DefaultName => "large gold nugget";
}
