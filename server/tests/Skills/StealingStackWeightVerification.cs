// StealingStackWeightVerification.cs
//
// cc-P57 Part G (bug-list D82, from cc-P54 headline 2). Pinned's stack path lets a thief take up to Stealing / 10 stones
// of a pile (Skills/Stealing.cs:272-276), and its weight test reads one unit's weight (:261-263), so on the shard's 200
// cap a 150 thief could lift a 15-stone pile whole with a skill window of 127.5 to 177.5 (:282-289): a gain at every skill.
// Patched (server/patches/Stealing-stack-weight.patch): what is taken weighs no more than MaxWeightToSteal (10 stones),
// so no window passes 127.5, the single-item path's own top. Notes: shard-migration notes/cc-P57-batch-6.md, Part G.
//
// The steal itself is pinned's private StealingTarget.TryStealItem, called by reflection; the skill check is recorded by
// swapping Mobile.SkillCheckTargetHandler for one that notes the window and succeeds. The roll for the amount is held at
// the top (the thief takes as much as allowed).
//
// Facts:
//   G1. (The probe.) A 150 thief and a 15-stone pile of 150 ingots, a 200 thief and a 20-stone pile of 200: the window
//       stays at or below 127.5 and what is taken weighs at most 10 stones.
//   G2. A normal steal at each skill band (30, 60, 90, 100) behaves as pinned: the same window and the same amount from a
//       5-stone pile, and the same window for a single 5-stone item.

using System;
using System.Collections.Generic;
using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Server.SkillHandlers;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class StealingStackWeightVerification
{
    private readonly ITestOutputHelper _out;

    public StealingStackWeightVerification(ITestOutputHelper output)
    {
        _out = output;
        Stealing.Configure(); // MaxWeightToSteal 10 (the test host runs no Configure)
    }

    private sealed class ConstantRandom : System.Random
    {
        public double Value;

        protected override double Sample() => Value;
        public override double NextDouble() => Value;
        public override int Next() => (int)(Value * int.MaxValue);
        public override int Next(int maxValue) => (int)(Value * maxValue);
        public override int Next(int minValue, int maxValue) => minValue + (int)(Value * (maxValue - minValue));
        public override long NextInt64(long maxValue) => (long)(Value * maxValue);
    }

    private static readonly Point3D Spot = new(1560, 1560, 0);

    // One steal: (the skill window asked for, the amount taken).
    private static (double Min, double Max, int Taken) Steal(double skill, Item toSteal)
    {
        var thief = new PlayerMobile { Player = true, Race = Race.Human };
        thief.AddItem(new Backpack());
        thief.RawStr = 100;
        thief.Skills.Stealing.Cap = 200.0;
        thief.Skills.Stealing.Base = skill;
        thief.MoveToWorld(Spot, Map.Trammel);

        var pack = new Backpack();
        pack.MoveToWorld(Spot, Map.Trammel);
        pack.DropItem(toSteal);

        var windows = new List<(double, double)>();
        var handler = Mobile.SkillCheckTargetHandler;
        Mobile.SkillCheckTargetHandler = (from, s, target, min, max) =>
        {
            windows.Add((min, max));
            return true;
        };
        BuiltInRng.Generator = new ConstantRandom { Value = 0.999 };

        try
        {
            var type = typeof(Stealing).GetNestedType("StealingTarget", BindingFlags.NonPublic)!;
            var target = Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, [thief], null);
            var method = type.GetMethod("TryStealItem", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var args = new object[] { toSteal, false };
            var stolen = (Item)method.Invoke(target, args);

            Assert.Single(windows);
            Assert.NotNull(stolen);
            var taken = stolen.Amount;
            if (stolen != toSteal)
            {
                stolen.Delete();
            }

            return (windows[0].Item1, windows[0].Item2, taken);
        }
        finally
        {
            Mobile.SkillCheckTargetHandler = handler;
            BuiltInRng.Reset();
            toSteal.Delete();
            pack.Delete();
            thief.Delete();
        }
    }

    // ---------------------------------------------------------------- G1

    [Theory]
    [InlineData(150.0, 150)]
    [InlineData(200.0, 200)]
    public void AStackStealNeverTakesMoreThanTenStonesOrPassesTheSingleItemWindow(double skill, int ingots)
    {
        var pile = new IronIngot(ingots);
        var unit = pile.Weight;
        var (min, max, taken) = Steal(skill, pile);
        _out.WriteLine($"Stealing {skill}, a pile of {ingots} ingots ({unit * ingots:F1} stones): window {min} to {max}, " +
                       $"took {taken} ({unit * taken:F1} stones)");

        Assert.True(max <= 127.5, $"window up to {max}");
        Assert.True(unit * taken <= 10.0 + 1e-9, $"took {unit * taken:F1} stones");
    }

    // ---------------------------------------------------------------- G2

    [Theory]
    [InlineData(30.0)]
    [InlineData(60.0)]
    [InlineData(90.0)]
    [InlineData(100.0)]
    public void NormalStealsAreAsPinned(double skill)
    {
        // A 5-stone pile: pinned's own amount and window (Stealing.cs:272-304).
        var pile = new IronIngot(50);
        var unit = pile.Weight;
        var stockMax = Math.Clamp((int)(skill / 10.0 / unit), 1, 50);
        var stockWeight = (int)Math.Ceiling(unit * stockMax) * 10;
        var (min, max, taken) = Steal(skill, pile);
        _out.WriteLine($"Stealing {skill}, 50 ingots: took {taken} (pinned {stockMax}), window {min} to {max} " +
                       $"(pinned {stockWeight - 22.5} to {stockWeight + 27.5})");
        Assert.Equal(stockMax, taken);
        Assert.Equal((stockWeight - 22.5, stockWeight + 27.5), (min, max));

        // A single 5-stone item: pinned's window (Stealing.cs:313-316).
        var item = new Item(0x1BF2) { Weight = 5.0 };
        var (imin, imax, _) = Steal(skill, item);
        Assert.Equal((27.5, 77.5), (imin, imax));
    }
}
