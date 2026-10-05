// OreSatchelVerification.cs
//
// cc-P55 Parts B, C, D and E (bug-list D70, D74, D71, D72; Chase 2026-10-05).
//
//   B (D70): the satchel's Smelt Ore made Amount / 2 ingots from every pile with no skill roll. It now smelts each pile
//      through the forge's own step (pinned Ore.cs, reached by Ore-smelt-at-forge.patch): the forge's ratio by pile size
//      (small Amount / 2, mediums Amount, large Amount x 2), its Mining check and its loss on failure, still only near a
//      forge, with the ingots back in the satchel.
//   C (D74): the entry sent 6277, shown as 3006277 "Salvage Ingots"; the ore satchel now sends 3006143 "Smelt" and the
//      lumber satchel 1158775 "* Magically Chops Logs into Boards *" (client Cliloc.enu, EA 7.0.117.0).
//   D (D71): tiers 3 to 5 lost the entry; T2 to T5 now share it (CompactOreSatchel), T1 has none, saved satchels load.
//   E (D72): satchel hues follow the ore ladder (T1 Iron = none, T2 Gold, T3 Verite, T4 Valorite, T5 Platinum), read
//      from CraftResources; the Jacob's pickaxes take their satchel tier's hue; exhausted pickaxes stay exhausted.
// Notes: shard-migration notes/cc-P55-bug-batch-5.md, Parts B to E.
//
// Facts:
//   B1. Each ore size gives what the forge itself gives for the same pile (the stock target, run beside it).
//   B2. A failed Mining check loses what the forge loses: half the pile, or a large single ore shrinks.
//   B3. No forge in range: refused, nothing smelted.
//   B4. Mixed piles: each smelts or is refused on its own, and the ingots land in the satchel.
//   C1. The labels on the wire: ore 3006143, lumber 1158775; neither is 3006277.
//   D1. T2 to T5 show the entry and smelt; T1 shows none and smelts nothing.
//   D2. A saved T3 satchel (old hue, ore inside) loads with its contents, the new hue and the entry.
//   E1. Each satchel's hue is its metal's hue and carries no 0x8000 bit.
//   E2. Each Jacob's pickaxe takes its tier's hue; a saved working one in the old hue takes the new one, a saved
//       exhausted one stays exhausted, and a staff-set hue is kept.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Random;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class OreSatchelVerification
{
    private readonly ITestOutputHelper _out;

    public OreSatchelVerification(ITestOutputHelper output)
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

    private static readonly Point3D Spot = new(1460, 1750, 0);

    private sealed class Miner : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly Item Forge;

        public Miner(double mining, bool forge = true, int dx = 0)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true };
            Pm.AddItem(new Backpack());
            Pm.RawStr = Pm.RawDex = Pm.RawInt = 100;
            Pm.MoveToWorld(new Point3D(Spot.X + dx, Spot.Y, Spot.Z), Map.Trammel);
            Pm.Skills.Mining.Cap = 200.0;
            Pm.Skills.Mining.Base = mining;

            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            if (forge)
            {
                Forge = new Item(4017) { Movable = false };
                Forge.MoveToWorld(new Point3D(Spot.X + dx + 1, Spot.Y, Spot.Z), Map.Trammel);
            }
        }

        public T Satchel<T>(T satchel) where T : CompactOreSatchel
        {
            Pm.Backpack.DropItem(satchel);
            return satchel;
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
            Forge?.Delete();
            BuiltInRng.Reset();
        }
    }

    private static T Ore<T>(T ore, int itemId) where T : BaseOre
    {
        ore.ItemID = itemId;
        return ore;
    }

    private static int Ingots(Container c) => c.GetAmount(typeof(BaseIngot));

    private static ContextMenuEntry[] Entries(Item item, Mobile from)
    {
        var list = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            item.GetContextMenuEntries(from, ref list);
            var result = new ContextMenuEntry[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                result[i] = list[i];
            }

            return result;
        }
        finally
        {
            list.Dispose();
        }
    }

    // The stock forge path, as a player drives it: double-click the ore, then target the forge.
    private static void SmeltAtTheForge(Mobile from, BaseOre ore, Item forge)
    {
        ore.OnDoubleClick(from);
        Assert.NotNull(from.Target);
        from.Target.Invoke(from, forge);
    }

    // ---------------------------------------------------------------- B1

    [Theory]
    [InlineData(0x19B9, 10)] // large
    [InlineData(0x19BA, 10)] // medium
    [InlineData(0x19B8, 10)] // medium clump
    [InlineData(0x19B7, 10)] // small
    [InlineData(0x19B7, 11)] // small, odd
    [InlineData(0x19B9, 1)]  // one large ore
    public void EachOreSizeGivesTheForgesCount(int itemId, int amount)
    {
        // The forge, alone: a loose pile in the pack, through the stock target.
        int forgeIngots, forgeLeft;
        using (var f = new Miner(100.0))
        {
            BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };
            var loose = Ore(new IronOre(amount), itemId);
            f.Pm.Backpack.DropItem(loose);
            SmeltAtTheForge(f.Pm, loose, f.Forge);
            forgeIngots = Ingots(f.Pm.Backpack);
            forgeLeft = loose.Deleted ? 0 : loose.Amount;
        }

        // The satchel, with the same pile.
        using var s = new Miner(100.0, dx: 6);
        BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };
        var satchel = s.Satchel(new ReinforcedOreSatchel());
        var pile = Ore(new IronOre(amount), itemId);
        satchel.DropItem(pile);
        satchel.SmeltAllOre(s.Pm);

        var satchelIngots = Ingots(satchel);
        var left = pile.Deleted ? 0 : pile.Amount;
        _out.WriteLine($"0x{itemId:X4} x{amount}: forge {forgeIngots} ingots ({forgeLeft} ore left), " +
                       $"satchel {satchelIngots} ingots ({left} ore left)");

        Assert.Equal(forgeIngots, satchelIngots);
        Assert.Equal(forgeLeft, left);
        Assert.Equal(satchelIngots, Ingots(s.Pm.Backpack)); // every ingot is in the satchel
        var expected = itemId switch { 0x19B7 => amount / 2, 0x19B9 => amount * 2, _ => amount };
        Assert.Equal(expected, satchelIngots);
    }

    // ---------------------------------------------------------------- B2

    [Fact]
    public void AFailedCheckLosesWhatTheForgeLoses()
    {
        // Valorite: difficulty 99, check 74 to 124; at 99 Mining the chance is 0.5, and a 0.99 roll fails it.
        using var s = new Miner(99.0);
        BuiltInRng.Generator = new ConstantRandom { Value = 0.99 };
        var satchel = s.Satchel(new SurveyorsSatchel());
        var pile = Ore(new ValoriteOre(10), 0x19BA);
        var single = Ore(new ValoriteOre(1), 0x19B9);
        satchel.DropItem(pile);
        satchel.DropItem(single);

        satchel.SmeltAllOre(s.Pm);
        _out.WriteLine($"after a failed check: pile {pile.Amount}, single large 0x{single.ItemID:X4}, ingots {Ingots(satchel)}");

        Assert.Equal(0, Ingots(s.Pm.Backpack));
        Assert.Equal(5, pile.Amount);           // Ore.cs:251, Amount /= 2
        Assert.Equal(0x19B8, single.ItemID);    // Ore.cs:247, one large ore shrinks to a medium clump
    }

    // ---------------------------------------------------------------- B3

    [Fact]
    public void WithNoForgeNothingIsSmelted()
    {
        using var s = new Miner(100.0, forge: false);
        var satchel = s.Satchel(new DeepdelversSatchel());
        var pile = Ore(new IronOre(10), 0x19B9);
        satchel.DropItem(pile);

        satchel.SmeltAllOre(s.Pm);

        Assert.False(pile.Deleted);
        Assert.Equal(10, pile.Amount);
        Assert.Equal(0, Ingots(s.Pm.Backpack));
        Assert.Null(CompactOreSatchel.FindForge(s.Pm, 2));
    }

    // ---------------------------------------------------------------- B4

    [Fact]
    public void MixedPilesSmeltOrAreRefusedEachOnItsOwn()
    {
        using var s = new Miner(90.0);
        BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };
        var satchel = s.Satchel(new MasterExpeditionSatchel());
        var ironLarge = Ore(new IronOre(10), 0x19B9);       // 20
        var ironSmall = Ore(new IronOre(7), 0x19B7);        // 3, one ore left
        var dullCopper = Ore(new DullCopperOre(5), 0x19BA); // 5
        var valorite = Ore(new ValoriteOre(4), 0x19B9);     // 99 needed, refused at 90
        foreach (var ore in new BaseOre[] { ironLarge, ironSmall, dullCopper, valorite })
        {
            satchel.DropItem(ore);
        }

        satchel.SmeltAllOre(s.Pm);

        var ingots = satchel.Items.OfType<BaseIngot>().ToList();
        _out.WriteLine($"ingots in the satchel: {string.Join(", ", ingots.Select(i => $"{i.Amount} {i.Resource}"))}; " +
                       $"valorite left {valorite.Amount}, small iron left {(ironSmall.Deleted ? 0 : ironSmall.Amount)}");
        Assert.Equal(23, ingots.Where(i => i.Resource == CraftResource.Iron).Sum(i => i.Amount));
        Assert.Equal(5, ingots.Where(i => i.Resource == CraftResource.DullCopper).Sum(i => i.Amount));
        Assert.False(valorite.Deleted);
        Assert.Equal(4, valorite.Amount);
        Assert.Equal(1, ironSmall.Amount);
        Assert.Equal(28, Ingots(s.Pm.Backpack));
    }

    // ---------------------------------------------------------------- C1

    [Fact]
    public void TheEntriesSayWhatTheyDoOnTheWire()
    {
        var ore = new CompactOreSatchel.SmeltOreEntry(true);
        var lumber = new SeasonedLumberSatchel.ProcessAllLogsEntry(true);
        _out.WriteLine($"ore satchel {ore.Number}, lumber satchel {lumber.Number}");

        Assert.Equal(1900001, ore.Number);     // "Smelt Ore", ours since cc-P57 Part D (was 3006143 "Smelt")
        Assert.Equal(1158775, lumber.Number);  // "* Magically Chops Logs into Boards *"
        Assert.NotEqual(3006277, ore.Number);  // "Salvage Ingots"
        Assert.NotEqual(3006277, lumber.Number);

        using var s = new Miner(100.0);
        var seasoned = new SeasonedLumberSatchel();
        try
        {
            s.Pm.Backpack.DropItem(seasoned);
            Assert.Contains(Entries(seasoned, s.Pm), e => e.Number == 1158775);
            Assert.DoesNotContain(Entries(seasoned, s.Pm), e => e.Number == 3006277);
        }
        finally
        {
            seasoned.Delete();
        }
    }

    // ---------------------------------------------------------------- D1

    public static IEnumerable<object[]> Tiers() =>
    [
        [typeof(CompactOreSatchel), false],
        [typeof(ReinforcedOreSatchel), true],
        [typeof(SurveyorsSatchel), true],
        [typeof(DeepdelversSatchel), true],
        [typeof(MasterExpeditionSatchel), true]
    ];

    [Theory]
    [MemberData(nameof(Tiers))]
    public void TiersTwoToFiveSmeltAndTierOneDoesNot(Type tier, bool smelts)
    {
        using var s = new Miner(100.0);
        BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };
        var satchel = s.Satchel((CompactOreSatchel)Activator.CreateInstance(tier));
        var pile = Ore(new IronOre(10), 0x19B9);
        satchel.DropItem(pile);

        var entries = Entries(satchel, s.Pm).OfType<CompactOreSatchel.SmeltOreEntry>().ToList();
        Assert.Equal(smelts ? 1 : 0, entries.Count);
        Assert.Equal(smelts, satchel.CanSmeltOre);

        if (smelts)
        {
            entries[0].OnClick(s.Pm, satchel);
        }
        else
        {
            satchel.SmeltAllOre(s.Pm); // no menu entry; the method itself also does nothing for T1
        }

        _out.WriteLine($"{tier.Name}: entry {entries.Count}, ingots {Ingots(satchel)}");
        Assert.Equal(smelts ? 20 : 0, Ingots(satchel));
        Assert.Equal(smelts, pile.Deleted);
    }

    // ---------------------------------------------------------------- D2

    [Fact]
    public void ASavedTierThreeSatchelLoadsWithItsContents()
    {
        using var s = new Miner(100.0);
        var original = new SurveyorsSatchel();
        SurveyorsSatchel copy = null;

        try
        {
            original.Hue = 0x026C; // as every T3 satchel was saved before cc-P55
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            original.Serialize(writer);
            writer.Flush();

            copy = new SurveyorsSatchel(original.Serial);
            copy.Deserialize(new BufferReader(buffer));
            s.Pm.Backpack.DropItem(copy);
            var ore = Ore(new IronOre(4), 0x19B9);
            copy.DropItem(ore);

            _out.WriteLine($"loaded T3: hue 0x{copy.Hue:X4}, name {copy.Name}, entry {Entries(copy, s.Pm).OfType<CompactOreSatchel.SmeltOreEntry>().Count()}");
            Assert.Equal(CraftResources.GetHue(CraftResource.Verite), copy.Hue);
            Assert.Equal("Surveyor's Ore Satchel", copy.Name);
            Assert.Single(Entries(copy, s.Pm).OfType<CompactOreSatchel.SmeltOreEntry>());
            Assert.Equal(800, copy.DefaultMaxWeight);
        }
        finally
        {
            original.Delete();
            copy?.Delete();
        }
    }

    // ---------------------------------------------------------------- E1

    public static IEnumerable<object[]> Hues() =>
    [
        [typeof(CompactOreSatchel), CraftResource.Iron, 0x0000, 0x0482],
        [typeof(ReinforcedOreSatchel), CraftResource.Gold, 0x08A5, 0x8A5C],
        [typeof(SurveyorsSatchel), CraftResource.Verite, 0x089F, 0x026C],
        [typeof(DeepdelversSatchel), CraftResource.Valorite, 0x08AB, 0x0455],
        [typeof(MasterExpeditionSatchel), CraftResource.Platinum, 0x0481, 0x0B2A]
    ];

    [Theory]
    [MemberData(nameof(Hues))]
    public void EachSatchelWearsItsMetalsHue(Type tier, CraftResource metal, int hue, int legacyHue)
    {
        var fresh = (CompactOreSatchel)Activator.CreateInstance(tier);
        var saved = (CompactOreSatchel)Activator.CreateInstance(tier);
        var staff = (CompactOreSatchel)Activator.CreateInstance(tier);
        var copies = new List<Item>();

        try
        {
            Assert.Equal(metal, fresh.TierMetal);
            Assert.Equal(CraftResources.GetHue(metal), fresh.Hue);
            Assert.Equal(hue, fresh.Hue);
            Assert.Equal(0, fresh.Hue & 0x8000);

            // On load: the tier's old hue becomes the new one; a hue staff set is kept.
            foreach (var (item, before, after) in new[] { (saved, legacyHue, hue), (staff, 0x0021, 0x0021) })
            {
                item.Hue = before;
                var buffer = new byte[65536];
                var writer = new BufferWriter(buffer, true);
                item.Serialize(writer);
                writer.Flush();
                var copy = (CompactOreSatchel)Activator.CreateInstance(tier, item.Serial);
                copies.Add(copy);
                copy.Deserialize(new BufferReader(buffer));
                _out.WriteLine($"{tier.Name}: saved 0x{before:X4}, loaded 0x{copy.Hue:X4}");
                Assert.Equal(after, copy.Hue);
            }
        }
        finally
        {
            fresh.Delete();
            saved.Delete();
            staff.Delete();
            copies.ForEach(c => c.Delete());
        }
    }

    // ---------------------------------------------------------------- E2

    public static IEnumerable<object[]> Pickaxes() =>
    [
        [typeof(JacobsReinforcedPickaxe), CraftResource.Gold, 0x8A5C],
        [typeof(JacobsProspectorPickaxe), CraftResource.Verite, 0x026C],
        [typeof(JacobsDeepdelverPickaxe), CraftResource.Valorite, 0x0455],
        [typeof(JacobsWorldbreakerPickaxe), CraftResource.Platinum, 0x0B2A]
    ];

    private static bool Exhausted(Item pickaxe) => (bool)pickaxe.GetType().GetProperty("Exhausted")!.GetValue(pickaxe)!;

    [Theory]
    [MemberData(nameof(Pickaxes))]
    public void EachPickaxeWearsItsTiersHueAndExhaustionSurvivesALoad(Type type, CraftResource metal, int legacyHue)
    {
        var made = new List<Item>();
        Item Make() { var i = (Item)Activator.CreateInstance(type); made.Add(i); return i; }

        try
        {
            var fresh = Make();
            Assert.Equal(CraftResources.GetHue(metal), fresh.Hue);
            Assert.Equal(0, fresh.Hue & 0x8000);
            Assert.False(Exhausted(fresh));

            // Saved working in the old hue; saved exhausted (0x0415); a hue staff set.
            foreach (var (before, afterHue, afterExhausted) in new[]
                     {
                         (legacyHue, CraftResources.GetHue(metal), false),
                         (0x0415, 0x0415, true),
                         (0x0021, 0x0021, false)
                     })
            {
                var item = Make();
                item.Hue = before;
                var buffer = new byte[65536];
                var writer = new BufferWriter(buffer, true);
                item.Serialize(writer);
                writer.Flush();
                var copy = (Item)Activator.CreateInstance(type, item.Serial);
                made.Add(copy);
                copy.Deserialize(new BufferReader(buffer));

                _out.WriteLine($"{type.Name}: saved 0x{before:X4}, loaded 0x{copy.Hue:X4}, exhausted {Exhausted(copy)}");
                Assert.Equal(afterHue, copy.Hue);
                Assert.Equal(afterExhausted, Exhausted(copy));
            }
        }
        finally
        {
            made.ForEach(i => i.Delete());
        }
    }
}
