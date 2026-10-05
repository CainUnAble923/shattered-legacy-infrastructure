// OreSmeltDifficultyVerification.cs
//
// cc-P56 Part B (bug-list D78, Chase 2026-10-05): a post-Valorite ore smelts at its metal's tier (ClusterFMetalTiers:
// Platinum 112.5, Toxic 125, Blaze 137.5, Frost 150, Obsidian 162.5, Mythril 175, Adamantium 187.5, Celestial 200), not
// the 50 pinned's forge gives every ore it does not list (Ore.cs's difficulty switch). The change is a third hunk of
// server/patches/Ore-smelt-at-forge.patch, so the ore satchels, which smelt through the forge's own step, inherit it.
// Notes: shard-migration notes/cc-P56-smith-economy-and-labels.md, Part B.
//
// Facts:
//   B1. At the forge, for each of the eight ores: one tenth of a point under the tier refuses the pile untouched; at the
//       tier the forge's usual window applies (difficulty - 25 to + 25, so a chance of one half): a 0.49 roll smelts a
//       large pile of 10 into 20 ingots, a 0.51 roll burns it to 5.
//   B2. Through the satchel, the same: 112.4 Mining leaves a Platinum pile whole, 112.5 smelts it into the satchel.
//   B3. Stock ores are unchanged: iron smelts at any skill, Dull Copper and Valorite are refused a tenth under 65 and
//       99 and smelt at them.

using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Random;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class OreSmeltDifficultyVerification
{
    private readonly ITestOutputHelper _out;

    public OreSmeltDifficultyVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureSkillChecks();
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

    private static readonly Point3D Spot = new(1470, 1760, 0);

    private sealed class Miner : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly Item Forge;

        public Miner(double mining)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true };
            Pm.AddItem(new Backpack());
            Pm.RawStr = Pm.RawDex = Pm.RawInt = 100;
            Pm.MoveToWorld(Spot, Map.Trammel);
            Pm.Skills.Mining.Cap = 200.0;
            Pm.Skills.Mining.Base = mining;

            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            Forge = new Item(4017) { Movable = false };
            Forge.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        }

        public void Dispose()
        {
            foreach (var item in Pm.Backpack.Items.ToList())
            {
                item.Delete();
            }

            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Forge.Delete();
            BuiltInRng.Reset();
        }
    }

    private static int Ingots(Container c) => c.GetAmount(typeof(BaseIngot));

    // The stock forge path, as a player drives it: double-click the ore, then target the forge.
    private static void SmeltAtTheForge(Mobile from, BaseOre ore, Item forge)
    {
        ore.OnDoubleClick(from);
        Assert.NotNull(from.Target);
        from.Target.Invoke(from, forge);
    }

    private static BaseOre LargePile(Type type, int amount)
    {
        var ore = (BaseOre)Activator.CreateInstance(type, amount)!;
        ore.ItemID = 0x19B9; // large: two ingots an ore
        return ore;
    }

    // Smelts a large pile of 10 at the forge with the given skill and roll; returns the ingots made and the ore left.
    private static (int Ingots, int Left) Forge(Type ore, double mining, double roll)
    {
        using var m = new Miner(mining);
        var pile = LargePile(ore, 10);
        m.Pm.Backpack.DropItem(pile);
        BuiltInRng.Generator = new ConstantRandom { Value = roll };
        SmeltAtTheForge(m.Pm, pile, m.Forge);
        return (Ingots(m.Pm.Backpack), pile.Deleted ? 0 : pile.Amount);
    }

    // ---------------------------------------------------------------- B1

    [Theory]
    [InlineData(typeof(PlatinumOre), ClusterFMetalTiers.Platinum)]
    [InlineData(typeof(ToxicOre), ClusterFMetalTiers.Toxic)]
    [InlineData(typeof(BlazeOre), ClusterFMetalTiers.Blaze)]
    [InlineData(typeof(FrostOre), ClusterFMetalTiers.Frost)]
    [InlineData(typeof(ObsidianOre), ClusterFMetalTiers.Obsidian)]
    [InlineData(typeof(MythrilOre), ClusterFMetalTiers.Mythril)]
    [InlineData(typeof(AdamantiumOre), ClusterFMetalTiers.Adamantium)]
    [InlineData(typeof(CelestialOre), ClusterFMetalTiers.Celestial)]
    public void EachPostValoriteOreSmeltsAtItsTier(Type ore, double tier)
    {
        var under = Forge(ore, tier - 0.1, 0.0);
        var pass = Forge(ore, tier, 0.49);
        var fail = Forge(ore, tier, 0.51);
        _out.WriteLine($"{ore.Name} (tier {tier}): at {tier - 0.1} {under.Ingots} ingots, {under.Left} ore left; " +
                       $"at {tier} a 0.49 roll {pass.Ingots} ingots, a 0.51 roll {fail.Ingots} ingots and {fail.Left} ore left");

        Assert.Equal((0, 10), under);  // refused: "You have no idea how to smelt this strange ore!" (501986)
        Assert.Equal((20, 0), pass);   // chance (tier - (tier - 25)) / 50 = 0.5 >= 0.49
        Assert.Equal((0, 5), fail);    // 0.5 < 0.51: the forge's failure halves the pile
    }

    // ---------------------------------------------------------------- B2

    [Theory]
    [InlineData(112.4, false)]
    [InlineData(112.5, true)]
    public void TheSatchelInheritsTheForgesDifficulty(double mining, bool smelts)
    {
        using var m = new Miner(mining);
        BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };
        var satchel = new ReinforcedOreSatchel();
        m.Pm.Backpack.DropItem(satchel);
        var pile = LargePile(typeof(PlatinumOre), 10);
        satchel.DropItem(pile);

        satchel.SmeltAllOre(m.Pm);
        _out.WriteLine($"satchel at {mining} Mining: {Ingots(satchel)} Platinum ingots, {(pile.Deleted ? 0 : pile.Amount)} ore left");

        if (smelts)
        {
            Assert.Equal(20, Ingots(satchel));
            Assert.True(pile.Deleted);
        }
        else
        {
            Assert.Equal(0, Ingots(m.Pm.Backpack));
            Assert.False(pile.Deleted);
            Assert.Equal(10, pile.Amount);
        }
    }

    // ---------------------------------------------------------------- B3

    [Theory]
    [InlineData(typeof(IronOre), 0.0, true)]       // difficulty 50, never refused (Ore.cs: only above 50 refuses)
    [InlineData(typeof(IronOre), 50.0, true)]
    [InlineData(typeof(DullCopperOre), 64.9, false)]
    [InlineData(typeof(DullCopperOre), 65.0, true)]
    [InlineData(typeof(ValoriteOre), 98.9, false)]
    [InlineData(typeof(ValoriteOre), 99.0, true)]
    public void StockOresAreUnchanged(Type ore, double mining, bool smelts)
    {
        // Iron at 0 Mining: the check's chance is below zero (min 25), so it fails and burns the pile, but is not refused.
        var (ingots, left) = Forge(ore, mining, 0.0);
        _out.WriteLine($"{ore.Name} at {mining}: {ingots} ingots, {left} ore left");

        if (!smelts)
        {
            Assert.Equal((0, 10), (ingots, left));
        }
        else if (mining < 25.0)
        {
            Assert.Equal((0, 5), (ingots, left)); // attempted and failed: not refused
        }
        else
        {
            Assert.Equal((20, 0), (ingots, left));
        }
    }
}
