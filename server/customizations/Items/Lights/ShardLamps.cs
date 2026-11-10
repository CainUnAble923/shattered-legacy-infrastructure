// ShardLamps.cs (cc-P858): eleven lamps that really light, after Drafted's "New Lamps, Candle Tray, Jack-o-Lanterns
// (flip-able)" (servuo.dev, 2014), with Milva's dragon lantern, stained glass lamp and two tall lamps folded in (the same
// art). Rewritten for ModernUO as our own code: one base and a table of looks, every art ID read from our client's
// tiledata and rendered (shard-migration notes/cc-P858-tools/lampnames.txt, cc-P858-review/lamp-art-*.jpg); the
// downloads' own tables had wrong IDs (two lamps started on wooden logs 0x04C1, one lantern's lit art was a different
// lamp's animation frame).
//
// A lamp: double-click within 2 tiles to light it or put it out (stock BaseLight); it never burns out. Flipping it (the
// house's interior decorator or [Flip) turns it to its other facing, or for the jack-o-lantern to its next carving, and
// keeps it lit or unlit. Sold in the Achievement store and, the jack-o-lantern, in the Festival's Halloween stock.
// Notes: shard-migration notes/cc-P858-lamps-and-peddler-wagon.md.

#nullable enable

using System;
using ModernUO.Serialization;

namespace Server.Items;

/// <summary>One look of a lamp: its unlit and lit art.</summary>
public readonly record struct LampLook(int Unlit, int Lit);

// [Flippable] goes on each lamp with two looks or more, not here: the interior decorator and [Flip read the type's own
// attribute, not an inherited one (InteriorDecorator.Turn, FlipCommandHandlers; caught by cc-P858's TazUO run).
[SerializationGenerator(0, false)]
public abstract partial class ShardLamp : BaseLight
{
    protected ShardLamp(int unlit, LightType light, double weight) : base(unlit)
    {
        Light = light;
        Weight = weight;
        Duration = TimeSpan.Zero; // never burns out
    }

    /// <summary>The lamp's looks: its facings (south first), or for the jack-o-lantern its carvings.</summary>
    public abstract LampLook[] Looks { get; }

    /// <summary>Who to thank, shown on the lamp.</summary>
    public virtual string Credit => "Drafted";

    /// <summary>Which look the lamp shows now (its art, lit or unlit, matched in the table; the first if neither).</summary>
    public int LookIndex
    {
        get
        {
            var looks = Looks;
            for (var i = 0; i < looks.Length; i++)
            {
                if (looks[i].Unlit == ItemID || looks[i].Lit == ItemID)
                {
                    return i;
                }
            }

            return 0;
        }
    }

    public override int LitItemID => Looks[LookIndex].Lit;

    public override int UnlitItemID => Looks[LookIndex].Unlit;

    /// <summary>Turns it to its next look, lit or unlit as it was. A lamp with one look does not turn.</summary>
    public void Flip()
    {
        var looks = Looks;
        if (looks.Length < 2)
        {
            return;
        }

        var next = looks[(LookIndex + 1) % looks.Length];
        ItemID = Burning ? next.Lit : next.Unlit;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(Burning ? "Lit (double-click to put it out)" : "Double-click to light it");
        list.Add($"After {Credit}");
    }
}

[Flippable]
[SerializationGenerator(0, false)]
public partial class DragonLantern : ShardLamp
{
    private static readonly LampLook[] _looks = [new(0x4C40, 0x4C41), new(0x4C42, 0x4C43)];

    [Constructible]
    public DragonLantern() : base(0x4C40, LightType.Circle300, 40.0)
    {
    }

    public override LampLook[] Looks => _looks;
    public override string DefaultName => "a dragon lantern";
    public override string Credit => "Drafted and Milva";
}

[Flippable]
[SerializationGenerator(0, false)]
public partial class BirdLamp : ShardLamp
{
    private static readonly LampLook[] _looks = [new(0x4C44, 0x4C45), new(0x4C46, 0x4C47)];

    [Constructible]
    public BirdLamp() : base(0x4C44, LightType.Circle300, 40.0)
    {
    }

    public override LampLook[] Looks => _looks;
    public override string DefaultName => "a bird lamp";
}

[Flippable]
[SerializationGenerator(0, false)]
public partial class KoiLamp : ShardLamp
{
    private static readonly LampLook[] _looks = [new(0x4C48, 0x4C49), new(0x4C4A, 0x4C4B)];

    [Constructible]
    public KoiLamp() : base(0x4C48, LightType.Circle300, 40.0)
    {
    }

    public override LampLook[] Looks => _looks;
    public override string DefaultName => "a koi lamp";
}

[Flippable]
[SerializationGenerator(0, false)]
public partial class DragonEggLamp : ShardLamp
{
    private static readonly LampLook[] _looks = [new(0x4C4C, 0x4C4D), new(0x4C4E, 0x4C4F)];

    [Constructible]
    public DragonEggLamp() : base(0x4C4C, LightType.Circle300, 40.0)
    {
    }

    public override LampLook[] Looks => _looks;
    public override string DefaultName => "a dragon egg lamp";
}

[SerializationGenerator(0, false)]
public partial class StainedGlassLamp : ShardLamp
{
    private static readonly LampLook[] _looks = [new(0x4C50, 0x4C51)];

    [Constructible]
    public StainedGlassLamp() : base(0x4C50, LightType.Circle225, 20.0)
    {
    }

    public override LampLook[] Looks => _looks;
    public override string DefaultName => "a stained glass lamp";
    public override string Credit => "Drafted and Milva";
}

[Flippable]
[SerializationGenerator(0, false)]
public partial class TallAntiqueLamp : ShardLamp
{
    private static readonly LampLook[] _looks = [new(0x4C52, 0x4C53), new(0x4C54, 0x4C55)];

    [Constructible]
    public TallAntiqueLamp() : base(0x4C52, LightType.Circle300, 40.0)
    {
    }

    public override LampLook[] Looks => _looks;
    public override string DefaultName => "a tall antique lamp";
    public override string Credit => "Drafted and Milva";
}

[Flippable]
[SerializationGenerator(0, false)]
public partial class TallModernLamp : ShardLamp
{
    private static readonly LampLook[] _looks = [new(0x4C56, 0x4C57), new(0x4C58, 0x4C59)];

    [Constructible]
    public TallModernLamp() : base(0x4C56, LightType.Circle300, 40.0)
    {
    }

    public override LampLook[] Looks => _looks;
    public override string DefaultName => "a tall modern lamp";
    public override string Credit => "Drafted and Milva";
}

[SerializationGenerator(0, false)]
public partial class OrnateTableLamp : ShardLamp
{
    private static readonly LampLook[] _looks = [new(0x49C2, 0x49C1)];

    [Constructible]
    public OrnateTableLamp() : base(0x49C2, LightType.Circle150, 20.0)
    {
    }

    public override LampLook[] Looks => _looks;
    public override string DefaultName => "an ornate table lamp";
}

[SerializationGenerator(0, false)]
public partial class DragonBrazier : ShardLamp
{
    private static readonly LampLook[] _looks = [new(0x194E, 0x194D)];

    [Constructible]
    public DragonBrazier() : base(0x194E, LightType.Circle150, 20.0)
    {
    }

    public override LampLook[] Looks => _looks;
    public override string DefaultName => "an ancient dragon brazier";
}

[Flippable]
[SerializationGenerator(0, false)]
public partial class CandleTray : ShardLamp
{
    private static readonly LampLook[] _looks = [new(0x983E, 0x983F), new(0x983A, 0x983B)];

    [Constructible]
    public CandleTray() : base(0x983E, LightType.Circle150, 10.0)
    {
    }

    public override LampLook[] Looks => _looks;
    public override string DefaultName => "a gold tray of candles";
}

[Flippable]
[SerializationGenerator(0, false)]
public partial class JackLantern : ShardLamp
{
    // Two round pumpkins and nine tall carvings (witch, UO, ankh, spider, bat, skull, demon, spirit, scarecrow face).
    private static readonly LampLook[] _looks =
    [
        new(0x4694, 0x4691), new(0x4698, 0x4695), new(0x9934, 0x9931), new(0x9935, 0x9936), new(0x9939, 0x993A),
        new(0x993D, 0x993E), new(0x9941, 0x9942), new(0x9945, 0x9946), new(0x9949, 0x994A), new(0x994D, 0x994E),
        new(0x9951, 0x9952)
    ];

    [Constructible]
    public JackLantern() : base(0x4694, LightType.Circle150, 10.0)
    {
    }

    public override LampLook[] Looks => _looks;
    public override string DefaultName => "a jack-o-lantern";
}

public static class ShardLampStock
{
    /// <summary>Every lamp kind, for the stores and the facts: its key, its store label, its lit art (the store's picture).</summary>
    public static readonly (string Key, string Label, int Art, Func<ShardLamp> Make)[] All =
    [
        ("dragonlantern", "Dragon lantern", 0x4C41, () => new DragonLantern()),
        ("birdlamp", "Bird lamp", 0x4C45, () => new BirdLamp()),
        ("koilamp", "Koi lamp", 0x4C49, () => new KoiLamp()),
        ("dragonegglamp", "Dragon egg lamp", 0x4C4D, () => new DragonEggLamp()),
        ("stainedglasslamp", "Stained glass lamp", 0x4C51, () => new StainedGlassLamp()),
        ("tallantiquelamp", "Tall antique lamp", 0x4C53, () => new TallAntiqueLamp()),
        ("tallmodernlamp", "Tall modern lamp", 0x4C57, () => new TallModernLamp()),
        ("ornatetablelamp", "Ornate table lamp", 0x49C1, () => new OrnateTableLamp()),
        ("dragonbrazier", "Ancient dragon brazier", 0x194D, () => new DragonBrazier()),
        ("candletray", "Gold tray of candles", 0x983F, () => new CandleTray()),
        ("jacklantern", "Jack-o-lantern (11 carvings, flip to change)", 0x4691, () => new JackLantern())
    ];

    /// <summary>The Festival's Halloween stock takes the jack-o-lantern; the Achievement store the rest.</summary>
    public const string HalloweenKey = "jacklantern";
}
