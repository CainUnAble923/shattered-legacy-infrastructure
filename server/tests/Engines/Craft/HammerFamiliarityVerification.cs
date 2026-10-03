// HammerFamiliarityVerification.cs
//
// cc-P46 Part G (bug-list D57, Chase 2026-10-03): the Hammer of Hephaestus gained no Metal Familiarity until the player
// picked Iron by hand in the craft menu. The craft gump passes no resource type while the craft context's
// LastResourceIndex is -1, i.e. the player never picked a material (pinned CraftGump.cs:441-455, CraftContext.cs:18),
// and the craft then uses its collection's default, iron ingots (CraftItem.cs:615-626). The familiarity hook
// (CraftItem-shard-hooks.patch) handed that null to RecordFamiliarity, and HammerMetal.InferResource(null) is -1, so
// nothing was recorded. A null now counts as the craft's default resource (HammerMetal.CraftedResource).
// Notes: shard-migration notes/cc-P46-smith-orders-2.md, Part G.
//
// Facts (a plate chest at an anvil and forge, from a fresh craft context, as the craft gump sends it):
//   1. Hammer of Hephaestus: one craft without touching the material menu gives Iron familiarity 1.
//   2. Reinforced Hammer of Hephaestus: the same.
//   3. A Craft X run of 3 with no material picked gives Iron familiarity 3.
//   4. Picking Dull Copper still counts Dull Copper, not Iron.

using System;
using System.Linq;
using Server;
using Server.Engines.Craft;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Random;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HammerFamiliarityVerification
{
    private readonly ITestOutputHelper _out;

    public HammerFamiliarityVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureCraftSystems();
        ShardTestHost.EnsureSkillChecks();
    }

    // Every roll reads Value: 0.0 makes every craft succeed (as CraftXVerification does).
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

    private static readonly Point3D Spot = new(1410, 1750, 0);

    private sealed class Smith : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly BaseTool Tool;
        public readonly Item[] Smithy;
        public readonly CraftSystem System = DefBlacksmithy.CraftSystem;
        public readonly CraftItem Entry;

        public Smith(BaseTool tool)
        {
            ShardTestClock.Arm();
            BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };

            Entry = System.CraftItems.SearchFor(typeof(PlateChest));
            Assert.NotNull(Entry);

            Pm = new PlayerMobile { Player = true };
            Pm.AddItem(new Backpack());
            Pm.RawStr = Pm.RawDex = Pm.RawInt = 100;
            Pm.MoveToWorld(Spot, Map.Trammel);
            Pm.Skills.Blacksmith.Cap = 120.0;
            Pm.Skills.Blacksmith.Base = 120.0;
            Tool = tool;
            Pm.Backpack.DropItem(tool);
            Pm.Backpack.DropItem(new IronIngot(500));
            Pm.Backpack.DropItem(new DullCopperIngot(100));

            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            // An anvil and a forge beside Spot: DefBlacksmithy.CheckAnvilAndForge takes their item ids.
            var anvil = new Item(4015) { Movable = false };
            var forge = new Item(4017) { Movable = false };
            anvil.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
            forge.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, Spot.Z), Map.Trammel);
            Smithy = new[] { anvil, forge };

            var context = System.GetContext(Pm);
            context.MarkOption = CraftMarkOption.DoNotMark;
            Assert.Equal(-1, context.LastResourceIndex); // never picked a material
        }

        // One craft as the craft gump sends it: the type it read from the context (null when nothing was picked).
        public void Craft(Type typeRes)
        {
            Pm.CloseGump<CraftGump>();
            System.CreateItem(Pm, Entry.ItemType, typeRes, Tool, Entry);
            for (var i = 0; i < 200 && Pm.FindGump<CraftGump>() == null; i++)
            {
                ShardTestClock.Advance(TimeSpan.FromMilliseconds(250));
            }

            Assert.NotNull(Pm.FindGump<CraftGump>());
        }

        public void Dispose()
        {
            System.GetContext(Pm).Run = null;
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            foreach (var item in Smithy)
            {
                item.Delete();
            }

            BuiltInRng.Reset();
        }
    }

    private static int Familiarity(BaseTool tool, CraftResource metal)
    {
        var snapshot = tool switch
        {
            HammerOfHephaestus h => h.GetFamiliaritySnapshot(),
            ReinforcedHammerOfHephaestus r => r.GetFamiliaritySnapshot(),
            _ => throw new ArgumentException(tool.GetType().Name)
        };

        return snapshot.TryGetValue((int)metal, out var v) ? v : 0;
    }

    // ---------------------------------------------------------------- 1, 2

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ACraftWithNoMaterialPickedCountsIron(bool reinforced)
    {
        using var s = new Smith(reinforced ? new ReinforcedHammerOfHephaestus() : new HammerOfHephaestus());

        s.Craft(null);

        var chests = s.Pm.Backpack.Items.OfType<PlateChest>().ToList();
        _out.WriteLine($"{s.Tool.GetType().Name}: made {chests.Count} plate chest ({chests.FirstOrDefault()?.Resource}), " +
                       $"Iron familiarity {Familiarity(s.Tool, CraftResource.Iron)}");
        Assert.Single(chests);
        Assert.Equal(CraftResource.Iron, chests[0].Resource);
        Assert.Equal(1, Familiarity(s.Tool, CraftResource.Iron));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ACraftXRunWithNoMaterialPickedCountsIronForEachItem()
    {
        using var s = new Smith(new HammerOfHephaestus());

        ClusterFCraftRun.Begin(s.Pm, s.System, s.Entry, s.Tool, null, 3, false, false, null);
        for (var i = 0; i < 2000 && s.System.GetContext(s.Pm).Run != null; i++)
        {
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(250));
        }

        var last = s.System.GetContext(s.Pm).LastRun;
        Assert.NotNull(last);
        _out.WriteLine($"{last.Summary}; Iron familiarity {Familiarity(s.Tool, CraftResource.Iron)}");
        Assert.Equal(3, last.Made);
        Assert.Equal(3, Familiarity(s.Tool, CraftResource.Iron));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void PickingDullCopperStillCountsDullCopper()
    {
        using var s = new Smith(new HammerOfHephaestus());

        s.Craft(typeof(DullCopperIngot));

        Assert.Equal(1, Familiarity(s.Tool, CraftResource.DullCopper));
        Assert.Equal(0, Familiarity(s.Tool, CraftResource.Iron));
    }
}
