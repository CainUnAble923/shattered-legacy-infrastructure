// HammerCraftOrderVerification.cs
//
// cc-P168 (Chase, 2026-10-18): the Masterwork Hammer of Hephaestus (T3) and its Craft This Order use
// (HammerOfHephaestus.cs, ClusterFCraftOrder.cs, the order hooks in ClusterFCraftRun.cs and CraftItem-shard-hooks.patch).
// Notes: shard-migration notes/cc-P168-hammer-t3-craft-order.md.
//
// Facts, every craft through the real craft timer chain at an anvil and forge, the T3 held:
//   1. A 10-item small deed is crafted to full with enough ingots: 10 attempts, 10 hammer uses, the deed 10/10.
//   2. It stops when the ingots run out, when the hammer's uses run out, when the crafter moves, and when they are hurt
//      (even when regeneration has them back to full before the attempt ends), each with the deed holding what was made.
//   3. An exceptional-only deed takes only exceptional results: normal ones stay in the pack, the deed at 0.
//   4. Metal Familiarity still rolls during an order.
//   5. A large deed takes the complete small deed in the pack and crafts the rest of its lines.
//   6. Refused: a tailor deed, a deed in the bank, a hammer not in hand.
//   7. The T3 saving: at the cap a familiar metal's craft gives back 25% of its ingots (whole part sure, fraction rolled);
//      an unfamiliar metal gives back none.
//   8. T2 -> T3 at the guildmaster carries familiarity (capped at T3's cap), and the T3 survives a save and load.
//
// Random numbers are pinned with a constant source (as CraftXVerification): 0.0 makes every craft succeed exceptional;
// 0.7 at 100 Blacksmithy makes a plate gorget succeed (chance 0.87) but not exceptional (0.42).

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.BulkOrders;
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
public class HammerCraftOrderVerification
{
    private readonly ITestOutputHelper _out;

    public HammerCraftOrderVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureCraftSystems();
        ShardTestHost.EnsureSkillChecks();
        ClusterFGuildSystem.EnsureRegistered();
        ClusterFRecipeGate.Enabled = false; // about the run, not recipes (as CraftXVerification)
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

    private static readonly Point3D Spot = new(1430, 1770, 0);

    private static readonly SmallBulkEntry Gorget = LargeBulkEntry.LargePlate.Single(e => e.Type == typeof(PlateGorget));

    private sealed class Smith : IDisposable
    {
        public readonly Account Account = new($"p168{Guid.NewGuid():N}"[..16], "p168-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly MasterworkHammerOfHephaestus Hammer = new();
        public readonly Item[] Smithy;

        public Smith(double baseSkill = 120.0, double roll = 0.0, int ingots = 500)
        {
            ShardTestClock.Arm();
            BuiltInRng.Generator = new ConstantRandom { Value = roll };

            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.RawStr = Pm.RawDex = Pm.RawInt = 100;
            Pm.Hits = Pm.HitsMax;
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);
            Pm.Skills.Blacksmith.Cap = baseSkill; // held at its value: no gain moves the odds mid-run
            Pm.Skills.Blacksmith.Base = baseSkill;

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            Assert.True(Pm.EquipItem(Hammer));
            if (ingots > 0)
            {
                Pm.Backpack.DropItem(new IronIngot(ingots));
            }

            var anvil = new Item(4015) { Movable = false };
            var forge = new Item(4017) { Movable = false };
            anvil.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
            forge.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, Spot.Z), Map.Trammel);
            Smithy = [anvil, forge];

            DefBlacksmithy.CraftSystem.GetContext(Pm).MarkOption = CraftMarkOption.DoNotMark;
        }

        public CraftContext Context => DefBlacksmithy.CraftSystem.GetContext(Pm);

        public MakeXRun LastRun => Context.LastRun;

        public int Iron => Pm.Backpack.GetAmount(typeof(IronIngot));

        public T Put<T>(T item) where T : Item
        {
            Pm.Backpack.DropItem(item);
            return item;
        }

        public void Dispose()
        {
            Context.Run = null;
            foreach (var item in Smithy)
            {
                item.Delete();
            }

            Pm.NetState = null;
            Ns.Mobile = null;
            ClusterFAccountPersistence.Get(Account)?.ClearGuildData();
            Pm.Delete();
            Accounts.Remove(Account);
            BuiltInRng.Reset();
        }
    }

    private static SmallSmithBOD GorgetDeed(bool exceptional = false, int amountCur = 0) =>
        new(amountCur, 10, typeof(PlateGorget), Gorget.Number, Gorget.Graphic, exceptional, BulkMaterialType.None);

    // Advances the clock until the run has ended, calling `each` whenever an attempt has ended.
    private static void RunToEnd(Smith s, Action<MakeXRun> each = null, int maxSteps = 4000)
    {
        var seen = 0;
        for (var i = 0; i < maxSteps && s.Context.Run != null; i++)
        {
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(250));

            if (s.Context.Run is { } run && run.Attempts != seen)
            {
                seen = run.Attempts;
                each?.Invoke(run);
            }
        }

        Assert.Null(s.Context.Run);
    }

    private void Report(string what, Smith s, SmallBOD deed) =>
        _out.WriteLine($"{what}: {s.LastRun?.StoppedBy} after {s.LastRun?.Attempts} attempts, deed {deed.AmountCur}/{deed.AmountMax}, " +
                       $"uses {s.Hammer.UsesRemaining}, iron {s.Iron}; {s.LastRun?.Summary}");

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ATenItemDeedIsCraftedToFull()
    {
        using var s = new Smith();
        var deed = s.Put(GorgetDeed());
        var uses = s.Hammer.UsesRemaining;

        Assert.NotNull(ClusterFCraftOrder.Begin(s.Pm, s.Hammer, deed));
        RunToEnd(s);
        Report("enough ingots", s, deed);

        Assert.Equal(MakeXStop.Done, s.LastRun.StoppedBy);
        Assert.Equal(10, deed.AmountCur);
        Assert.True(deed.Complete);
        Assert.Equal(10, s.LastRun.Attempts);
        Assert.Equal(10, s.LastRun.Filled);
        Assert.Equal(uses - 10, s.Hammer.UsesRemaining);           // one hammer use each
        // Ten ingots each. The first craft is at 0 familiarity and saves nothing; each later one is at a little familiarity
        // (+3 a craft), whose tiny saving's fraction the 0.0 roll always wins: one ingot back from each of the other nine.
        Assert.Equal(500 - 10 * 10 + 9, s.Iron);
        Assert.Empty(s.Pm.Backpack.Items.OfType<PlateGorget>());    // every one went into the deed
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ItStopsWhenTheIngotsRunOut()
    {
        using var s = new Smith(ingots: 35);
        var deed = s.Put(GorgetDeed());

        ClusterFCraftOrder.Begin(s.Pm, s.Hammer, deed);
        RunToEnd(s);
        Report("35 ingots", s, deed);

        Assert.Equal(MakeXStop.Materials, s.LastRun.StoppedBy);
        Assert.Equal(3, deed.AmountCur);
        Assert.Equal(35 - 30 + 2, s.Iron); // the second and third gave one back (as above); 7 is short of a fourth
    }

    [Fact]
    public void ItStopsWhenTheHammerRunsOutOfUses()
    {
        using var s = new Smith();
        var deed = s.Put(GorgetDeed());
        s.Hammer.UsesRemaining = 4;

        ClusterFCraftOrder.Begin(s.Pm, s.Hammer, deed);
        RunToEnd(s);
        Report("4 uses", s, deed);

        Assert.Equal(MakeXStop.Tool, s.LastRun.StoppedBy);
        Assert.Equal(4, deed.AmountCur);
        Assert.Equal(0, s.Hammer.UsesRemaining);
    }

    [Fact]
    public void ItStopsWhenTheCrafterMoves()
    {
        using var s = new Smith();
        var deed = s.Put(GorgetDeed());

        ClusterFCraftOrder.Begin(s.Pm, s.Hammer, deed);
        RunToEnd(s, run =>
        {
            if (run.Attempts == 2)
            {
                s.Pm.MoveToWorld(new Point3D(Spot.X - 5, Spot.Y, Spot.Z), Map.Trammel); // away from the anvil and forge
            }
        });
        Report("moved", s, deed);

        Assert.Equal(MakeXStop.Moved, s.LastRun.StoppedBy);
        // The attempt in hand when they stepped away is refused at its end (no anvil and forge in reach, as a hand-made
        // craft is), so the deed holds the two made before.
        Assert.Equal(2, deed.AmountCur);
    }

    [Fact]
    public void ItStopsWhenTheCrafterIsHurt()
    {
        using var s = new Smith();
        var deed = s.Put(GorgetDeed());

        ClusterFCraftOrder.Begin(s.Pm, s.Hammer, deed);
        RunToEnd(s, run =>
        {
            if (run.Attempts == 2)
            {
                var hits = s.Pm.Hits;
                s.Pm.Damage(10); // no source: a fire field or a trap hurts too
                _out.WriteLine($"hit: {hits} -> {s.Pm.Hits} hits, blessed {s.Pm.Blessed}");
            }
        });
        Report("hurt", s, deed);

        Assert.Equal(MakeXStop.Damaged, s.LastRun.StoppedBy);
        Assert.Equal(3, deed.AmountCur);
        Assert.Contains("Craft order stopped: you were hurt.", ClusterFCraftOrder.StopText(MakeXStop.Damaged));
    }

    [Fact]
    public void TheStopButtonEndsIt()
    {
        using var s = new Smith();
        var deed = s.Put(GorgetDeed());

        ClusterFCraftOrder.Begin(s.Pm, s.Hammer, deed);
        RunToEnd(s, run =>
        {
            if (run.Attempts == 1)
            {
                run.StopRequested = true; // what the progress gump's Stop does
            }
        });

        Assert.Equal(MakeXStop.Stopped, s.LastRun.StoppedBy);
        Assert.Equal(2, deed.AmountCur);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AnExceptionalOnlyDeedTakesOnlyExceptionalResults()
    {
        // 85 Blacksmithy + 15 from the hammer = 100: a gorget succeeds at 0.87 and is exceptional at 0.42; the roll is 0.7.
        using var s = new Smith(baseSkill: 85.0, roll: 0.7, ingots: 30);
        var deed = s.Put(GorgetDeed(exceptional: true));

        ClusterFCraftOrder.Begin(s.Pm, s.Hammer, deed);
        RunToEnd(s);
        Report("exceptional only, normal results", s, deed);

        var normal = s.Pm.Backpack.Items.OfType<PlateGorget>().ToList();
        Assert.Equal(0, deed.AmountCur);
        Assert.Equal(3, normal.Count);
        Assert.All(normal, g => Assert.Equal(ArmorQuality.Regular, g.Quality));
        Assert.Equal(MakeXStop.Materials, s.LastRun.StoppedBy);

        // The same deed and exceptional results: they go in.
        BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };
        s.Put(new IronIngot(30));
        ClusterFCraftOrder.Begin(s.Pm, s.Hammer, deed);
        RunToEnd(s);
        Report("exceptional only, exceptional results", s, deed);
        Assert.Equal(3, deed.AmountCur);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void FamiliarityStillRolls()
    {
        using var s = new Smith();
        var deed = s.Put(GorgetDeed());

        ClusterFCraftOrder.Begin(s.Pm, s.Hammer, deed);
        RunToEnd(s);

        s.Hammer.GetFamiliaritySnapshot().TryGetValue((int)CraftResource.Iron, out var iron);
        _out.WriteLine($"iron familiarity after the order: {iron}");
        Assert.Equal(10 * HammerFamiliarityPace.ExceptionalGain, iron); // every roll 0.0: each exceptional craft gains 3
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void ALargeDeedTakesItsSmallDeedsAndCraftsTheRest()
    {
        using var s = new Smith();
        var large = new LargeSmithBOD(10, false, BulkMaterialType.None, null);
        large.Entries = LargeBulkEntry.ConvertEntries(large, LargeBulkEntry.LargePlate);
        foreach (var entry in large.Entries)
        {
            entry.Amount = entry.Details.Type == typeof(PlateGorget) ? 8
                : entry.Details.Type == typeof(PlateHelm) ? 0
                : large.AmountMax;
        }

        s.Put(large);
        var helm = large.Entries.Single(e => e.Details.Type == typeof(PlateHelm)).Details;
        var helmDeed = s.Put(new SmallSmithBOD(10, 10, typeof(PlateHelm), helm.Number, helm.Graphic, false, BulkMaterialType.None));

        ClusterFCraftOrder.Begin(s.Pm, s.Hammer, large);
        Assert.True(helmDeed.Deleted); // the complete small deed went in first
        RunToEnd(s);
        _out.WriteLine($"large: {s.LastRun.StoppedBy} after {s.LastRun.Attempts} attempts; {s.LastRun.Summary}");

        Assert.Equal(MakeXStop.Done, s.LastRun.StoppedBy);
        Assert.True(large.Complete);
        Assert.Equal(2, s.LastRun.Attempts); // only the two missing gorgets were crafted
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void NeverATailorDeedTheBankOrAHammerNotInHand()
    {
        using var s = new Smith();

        var tailor = s.Put(new SmallTailorBOD(0, 10, typeof(Shirt), 1025399, 0x1517, false, BulkMaterialType.None));
        Assert.Null(ClusterFCraftOrder.Begin(s.Pm, s.Hammer, tailor));

        var banked = GorgetDeed();
        s.Pm.BankBox.DropItem(banked);
        Assert.Null(ClusterFCraftOrder.Begin(s.Pm, s.Hammer, banked));

        var deed = s.Put(GorgetDeed());
        s.Pm.Backpack.DropItem(s.Hammer); // out of hand
        Assert.Null(ClusterFCraftOrder.Begin(s.Pm, s.Hammer, deed));

        Assert.Null(s.Context.Run);
        Assert.Equal(0, deed.AmountCur);
        Assert.Equal(0, banked.AmountCur);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void AFamiliarMetalSavesIngotsAndAnUnfamiliarOneDoesNot()
    {
        Assert.Equal(2.5, MasterworkHammerOfHephaestus.SavedIngots(10, MasterworkHammerOfHephaestus.FamCap));
        Assert.Equal(0.0, MasterworkHammerOfHephaestus.SavedIngots(10, 0));
        Assert.Equal(1.25, MasterworkHammerOfHephaestus.SavedIngots(10, MasterworkHammerOfHephaestus.FamCap / 2));

        using var s = new Smith();
        s.Hammer.LoadFamiliaritySnapshot(new Dictionary<int, int> { [(int)CraftResource.Iron] = MasterworkHammerOfHephaestus.FamCap },
            MasterworkHammerOfHephaestus.FamCap);
        s.Put(new DullCopperIngot(50));
        var entry = DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(PlateGorget));

        void Craft(Type res)
        {
            s.Pm.CloseGump<CraftGump>();
            DefBlacksmithy.CraftSystem.CreateItem(s.Pm, typeof(PlateGorget), res, s.Hammer, entry);
            for (var i = 0; i < 200 && s.Pm.FindGump<CraftGump>() == null; i++)
            {
                ShardTestClock.Advance(TimeSpan.FromMilliseconds(250));
            }

            Assert.NotNull(s.Pm.FindGump<CraftGump>());
        }

        var iron = s.Iron;
        Craft(typeof(IronIngot));
        _out.WriteLine($"iron at the cap: {iron} -> {s.Iron} (10 used, 2.5 saved, the fraction rolled 0.0)");
        Assert.Equal(iron - 10 + 3, s.Iron);

        var copper = s.Pm.Backpack.GetAmount(typeof(DullCopperIngot));
        Craft(typeof(DullCopperIngot));
        _out.WriteLine($"dull copper at 0: {copper} -> {s.Pm.Backpack.GetAmount(typeof(DullCopperIngot))}");
        Assert.Equal(copper - 10, s.Pm.Backpack.GetAmount(typeof(DullCopperIngot)));
    }

    // ---------------------------------------------------------------- 8

    private static readonly System.Reflection.MethodInfo GumpRemove = typeof(GumpSystem).GetMethod("Remove",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static, null,
        [typeof(NetState), typeof(BaseGump)], null);

    private static void Press(BaseGump gump, NetState ns, int buttonId)
    {
        Assert.NotNull(GumpRemove);
        GumpRemove.Invoke(null, [ns, gump]);
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void T2ToT3AtTheGuildmasterCarriesFamiliarity()
    {
        using var s = new Smith(baseSkill: 100.0, ingots: 0);
        s.Hammer.Delete();

        var t2 = s.Put(new ReinforcedHammerOfHephaestus());
        t2.LoadFamiliaritySnapshot(new Dictionary<int, int>
        {
            [(int)CraftResource.Iron] = 2_500,
            [(int)CraftResource.Verite] = 1_234
        }, ReinforcedHammerOfHephaestus.FamCap);

        var guild = ClusterFAccountPersistence.GetOrCreateGuild(s.Pm);
        guild.JoinedGuilds.Add("smithing");
        guild.AddReputation("smithing", HammerRestoreGump.MasterworkStandingReq);
        guild.AddCurrency("smithing", HammerRestoreGump.MasterworkSealCost);
        s.Put(new IronIngot(HammerRestoreGump.MasterworkIronCost));
        s.Put(new ValoriteIngot(HammerRestoreGump.MasterworkValoriteCost));
        s.Put(new Gold(HammerRestoreGump.MasterworkGoldCost));

        var gump = new HammerRestoreGump(s.Pm);
        s.Pm.SendGump(gump);
        Press(gump, s.Ns, HammerRestoreGump.BtnMasterwork);

        var t3 = s.Pm.Backpack.FindItemByType<MasterworkHammerOfHephaestus>();
        Assert.NotNull(t3);
        Assert.True(t2.Deleted);
        var fam = t3.GetFamiliaritySnapshot();
        _out.WriteLine($"T3 familiarity: {string.Join(", ", fam.Select(kv => $"{kv.Key}={kv.Value}"))}");
        Assert.Equal(2_500, fam[(int)CraftResource.Iron]);
        Assert.Equal(1_234, fam[(int)CraftResource.Verite]);
        Assert.Equal(0, guild.GetCurrency("smithing"));
        Assert.Equal(0, s.Iron);
    }

    [Fact]
    public void TheT3SurvivesASaveAndLoad()
    {
        var original = new MasterworkHammerOfHephaestus { UsesRemaining = 321 };
        original.LoadFamiliaritySnapshot(new Dictionary<int, int>
        {
            [(int)CraftResource.Iron] = 4_000,
            [(int)CraftResource.Celestial] = 7
        }, MasterworkHammerOfHephaestus.FamCap);

        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = new MasterworkHammerOfHephaestus(original.Serial);
        try
        {
            original.Delete();
            copy.Deserialize(new BufferReader(buffer));

            Assert.Equal(321, copy.UsesRemaining);
            Assert.False(copy.Exhausted);
            Assert.Equal(4_000, copy.GetFamiliaritySnapshot()[(int)CraftResource.Iron]);
            Assert.Equal(7, copy.GetFamiliaritySnapshot()[(int)CraftResource.Celestial]);
            Assert.Equal(2, copy.GetFamiliaritySnapshot().Count);
        }
        finally
        {
            copy.Delete();
        }
    }
}
