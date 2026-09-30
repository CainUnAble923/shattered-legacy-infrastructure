// CraftXVerification.cs
//
// cc-P18, F-1: Craft X, keep going on failure (ClusterFCraftRun.cs, ClusterFCraftRejects.cs, CraftCountGump.cs, the
// hooks in our CraftItem.cs). Notes in shard-migration notes/cc-P18-reset-stone-young-craftx.md section 3.
//
// Facts (the brief's numbering), every run through the real craft timer chain:
//   1. Items mode makes exactly X successes through failed attempts, and uses materials exactly as the same attempts
//      made by hand do.
//   2. Attempts mode makes exactly X attempts.
//   3. Caps: 101 in items mode and 501 in attempts mode are lowered to 100 and 500, and the summary says so.
//   4. Exceptional only stops at X exceptional; rejects are smelted (smithing), salvaged through the salvage bag, cut
//      (tailoring with scissors) or bagged (tailoring without); nothing else in the pack is touched.
//   5. Each hard stop ends the run with its message: out of materials, tool worn out, backpack full, overweight,
//      moved, died, and the progress gump closed.
//
// Random numbers are pinned by swapping BuiltInRng.Generator for a constant source (as MakeXRepeatVerification), and
// skills are held still by capping them at their value, so a gain cannot move the odds mid-run.

using System;
using System.Linq;
using System.Text;
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
public class CraftXVerification
{
    private readonly ITestOutputHelper _out;

    public CraftXVerification(ITestOutputHelper output) => _out = output;

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

    // Everything of type T in the container, at any depth.
    private static System.Collections.Generic.List<T> All<T>(Container c) where T : Item
    {
        var list = new System.Collections.Generic.List<T>();
        foreach (var item in c.FindItemsByType<T>())
        {
            list.Add(item);
        }

        return list;
    }

    private static readonly Point3D Spot = new(1400, 1740, 0);

    private sealed class Crafter : IDisposable
    {
        public PlayerMobile Pm;
        public NetState Ns;
        public CraftSystem System;
        public CraftItem Entry;
        public BaseTool Tool;
        public ConstantRandom Rng;
        public Item[] Placed = Array.Empty<Item>();

        public CraftContext Context => System.GetContext(Pm);
        public MakeXRun Run => Context.Run;
        public MakeXRun Last => Context.LastRun;

        public int Count<T>() where T : Item => All<T>(Pm.Backpack).Sum(i => i.Amount);

        public void Dispose()
        {
            Context.Run = null;
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            foreach (var item in Placed)
            {
                item.Delete();
            }

            BuiltInRng.Reset();
        }
    }

    private static CraftSystem Tinkering()
    {
        if (DefTinkering.CraftSystem == null)
        {
            DefTinkering.Initialize();
        }

        TinkeringCraftRegistrations.Register();
        return DefTinkering.CraftSystem;
    }

    private static CraftSystem Blacksmithy()
    {
        if (DefBlacksmithy.CraftSystem == null)
        {
            DefBlacksmithy.Initialize();
        }

        BlacksmithyCraftRegistrations.Register();
        return DefBlacksmithy.CraftSystem;
    }

    private static CraftSystem Tailoring()
    {
        if (DefTailoring.CraftSystem == null)
        {
            DefTailoring.Initialize();
        }

        TailoringCraftRegistrations.Register();
        return DefTailoring.CraftSystem;
    }

    // An online crafter at Spot, the skill held at `skill`, with the tool and the pack contents given.
    private static Crafter Make(CraftSystem system, Type itemType, SkillName skill, double value, BaseTool tool, params Item[] pack)
    {
        ShardTestClock.Arm();

        if (Server.Misc.AntiMacroSystem.Settings == null)
        {
            Server.Misc.AntiMacroSystem.Configure();
        }

        var c = new Crafter
        {
            System = system,
            Tool = tool,
            Entry = system.CraftItems.SearchFor(itemType),
            Rng = new ConstantRandom { Value = 0.0 }
        };
        Assert.NotNull(c.Entry);
        BuiltInRng.Generator = c.Rng;

        c.Pm = new PlayerMobile { Player = true };
        c.Pm.AddItem(new Backpack());
        c.Pm.RawStr = c.Pm.RawDex = c.Pm.RawInt = 100;
        c.Pm.MoveToWorld(Spot, Map.Trammel);
        c.Pm.Skills[skill].Base = value;
        c.Pm.Skills[skill].Cap = value;
        c.Pm.Backpack.DropItem(tool);
        foreach (var item in pack)
        {
            c.Pm.Backpack.DropItem(item);
        }

        c.Ns = PacketTestUtilities.CreateTestNetState();
        c.Pm.NetState = c.Ns;
        c.Ns.Mobile = c.Pm;

        system.GetContext(c.Pm).MarkOption = CraftMarkOption.DoNotMark;
        return c;
    }

    // An anvil and a forge beside Spot: DefBlacksmithy.CheckAnvilAndForge takes their item ids (DefBlacksmithy.cs:46-47).
    private static Item[] Smithy()
    {
        var anvil = new Item(4015) { Movable = false };
        var forge = new Item(4017) { Movable = false };
        anvil.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        forge.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, Spot.Z), Map.Trammel);
        return new[] { anvil, forge };
    }

    // Advances the clock until the run has ended, calling `each` whenever an attempt has ended.
    private static void RunToEnd(Crafter c, Action<MakeXRun> each = null, int maxSteps = 2000)
    {
        var seen = 0;
        for (var i = 0; i < maxSteps && c.Run != null; i++)
        {
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(250));

            if (c.Run != null && c.Run.Attempts != seen)
            {
                seen = c.Run.Attempts;
                each?.Invoke(c.Run);
            }
        }

        Assert.Null(c.Run);
    }

    // A single craft by hand; returns once the craft gump is back.
    private static void ByHand(Crafter c)
    {
        c.Pm.CloseGump<CraftGump>();
        c.System.CreateItem(c.Pm, c.Entry.ItemType, null, c.Tool, c.Entry);
        for (var i = 0; i < 200 && c.Pm.FindGump<CraftGump>() == null; i++)
        {
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(250));
        }

        Assert.NotNull(c.Pm.FindGump<CraftGump>());
    }

    private static MakeXRun Start(Crafter c, int count, bool attempts = false, bool exceptional = false, Container bag = null) =>
        ClusterFCraftRun.Begin(c.Pm, c.System, c.Entry, c.Tool, null, count, attempts, exceptional, bag);

    // The text went out on the wire, as an ASCII or a big-endian Unicode message.
    private static bool Sent(NetState ns, string text)
    {
        var span = ns.SendBuffer.GetReadSpan();
        return Contains(span, Encoding.ASCII.GetBytes(text)) || Contains(span, Encoding.BigEndianUnicode.GetBytes(text));
    }

    private static bool Contains(ReadOnlySpan<byte> hay, byte[] needle) => hay.IndexOf(needle) >= 0;

    // Alternate: odd attempts succeed, even attempts fail (Value is read by the next attempt's skill check).
    private static void Alternate(Crafter c, MakeXRun run) => c.Rng.Value = run.Attempts % 2 == 0 ? 0.0 : 0.999;

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ItemsModeMakesExactlyXThroughFailuresAndUsesMaterialsAsByHand()
    {
        using var c = Make(Tinkering(), typeof(GargishRing), SkillName.Tinkering, 90.0, new TinkerTools { UsesRemaining = 500 },
            new IronIngot(200));

        // By hand: one success, one failure, what each costs.
        var before = c.Count<IronIngot>();
        c.Rng.Value = 0.0;
        ByHand(c);
        var success = before - c.Count<IronIngot>();
        before = c.Count<IronIngot>();
        c.Rng.Value = 0.999;
        ByHand(c);
        var failure = before - c.Count<IronIngot>();
        _out.WriteLine($"by hand: success costs {success}, failure costs {failure}");
        Assert.Equal(1, c.Count<GargishRing>());

        // Four items, alternating success and failure: S F S F S F S.
        before = c.Count<IronIngot>();
        c.Rng.Value = 0.0;
        Start(c, 4);
        RunToEnd(c, run => Alternate(c, run));

        var last = c.Last;
        _out.WriteLine(last.Summary);
        Assert.Equal(MakeXStop.Done, last.StoppedBy);
        Assert.Equal(4, last.Made);
        Assert.Equal(3, last.Failed);
        Assert.Equal(5, c.Count<GargishRing>());
        Assert.Equal(4 * success + 3 * failure, before - c.Count<IronIngot>());
        Assert.StartsWith("Made 4 (3 failed, 0 exceptional).", last.Summary);
        Assert.True(Sent(c.Ns, "Craft X done. Made 4 (3 failed, 0 exceptional)."));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AttemptsModeMakesExactlyXAttempts()
    {
        using var c = Make(Tinkering(), typeof(GargishRing), SkillName.Tinkering, 90.0, new TinkerTools { UsesRemaining = 500 },
            new IronIngot(200));

        Start(c, 5, attempts: true);
        RunToEnd(c, run => Alternate(c, run));

        _out.WriteLine(c.Last.Summary);
        Assert.Equal(MakeXStop.Done, c.Last.StoppedBy);
        Assert.Equal(5, c.Last.Attempts);
        Assert.Equal(3, c.Last.Made);
        Assert.Equal(2, c.Last.Failed);
        Assert.Equal(3, c.Count<GargishRing>());
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void OverTheCapIsLoweredToTheCapAndSaid()
    {
        Assert.Equal(100, ClusterFCraftRun.ItemsCap);
        Assert.Equal(500, ClusterFCraftRun.AttemptsCap);

        // No ingots: each run is refused at once, which is enough to read what it was set to.
        using var c = Make(Tinkering(), typeof(GargishRing), SkillName.Tinkering, 90.0, new TinkerTools { UsesRemaining = 500 });

        var items = Start(c, 101);
        Assert.Equal(101, items.Requested);
        Assert.Equal(100, items.Target);
        Assert.True(items.Capped);
        Assert.Contains("Capped at 100.", c.Last.Summary);

        var attempts = Start(c, 501, attempts: true);
        Assert.Equal(500, attempts.Target);
        Assert.True(attempts.Capped);
        Assert.Contains("Capped at 500.", c.Last.Summary);

        var under = Start(c, 100);
        Assert.Equal(100, under.Target);
        Assert.False(under.Capped);
        _out.WriteLine(c.Last.Summary);
    }

    // ---------------------------------------------------------------- 4

    // A value between the exceptional chance and the success chance: a success that is not exceptional.
    private static double NotExceptional(Crafter c)
    {
        var chance = c.Entry.GetSuccessChance(c.Pm, null, c.System, false, out _);
        var exceptional = c.Entry.GetExceptionalChance(c.System, chance, c.Pm);
        Assert.True(exceptional > 0.0 && exceptional < Math.Min(chance, 1.0), $"chance {chance}, exceptional {exceptional}");
        return (exceptional + Math.Min(chance, 1.0)) / 2;
    }

    // Three rejects, then every attempt exceptional.
    private static Action<MakeXRun> ThreeRejectsThenExceptional(Crafter c, double reject) =>
        run => c.Rng.Value = run.Made - run.Exceptional >= 3 ? 0.0 : reject;

    [Fact]
    public void ExceptionalOnlySmeltsASmithsRejectsAndTouchesNothingElse()
    {
        var own = new Dagger();
        var pickaxe = new Pickaxe();
        using var c = Make(Blacksmithy(), typeof(Dagger), SkillName.Blacksmith, 40.0, new SmithHammer { UsesRemaining = 500 },
            new IronIngot(200), own, pickaxe);
        c.Placed = Smithy();

        var reject = NotExceptional(c);
        c.Rng.Value = reject;
        var ingots = c.Count<IronIngot>();
        Start(c, 2, exceptional: true);
        RunToEnd(c, ThreeRejectsThenExceptional(c, reject));

        var last = c.Last;
        _out.WriteLine(last.Summary);
        Assert.Equal(MakeXStop.Done, last.StoppedBy);
        Assert.Equal(2, last.Exceptional);
        Assert.Equal(3, last.Smelted);
        Assert.Contains("Rejects: 3 smelted.", last.Summary);

        var daggers = All<Dagger>(c.Pm.Backpack);
        Assert.Equal(3, daggers.Count); // two exceptional, and the player's own
        Assert.Equal(2, daggers.Count(d => d.Quality == WeaponQuality.Exceptional));
        Assert.False(own.Deleted);
        Assert.True(own.IsChildOf(c.Pm.Backpack));
        Assert.False(pickaxe.Deleted);

        // 5 daggers of 3 ingots; each reject smelted back to 1 (3 / 2).
        Assert.Equal(ingots - 5 * 3 + 3 * 1, c.Count<IronIngot>());
    }

    [Fact]
    public void ExceptionalOnlyPutsRejectsInTheSalvageBagWhichSalvagesItAll()
    {
        var bag = new SalvageBag();
        var inBag = new Dagger(); // already in the salvage bag: salvaged too (Chase, 2026-09-30)
        var pickaxe = new Pickaxe();
        using var c = Make(Blacksmithy(), typeof(Dagger), SkillName.Blacksmith, 40.0, new SmithHammer { UsesRemaining = 500 },
            new IronIngot(200), bag, pickaxe);
        c.Placed = Smithy();
        bag.DropItem(inBag);

        var reject = NotExceptional(c);
        c.Rng.Value = reject;
        Start(c, 1, exceptional: true);
        RunToEnd(c, ThreeRejectsThenExceptional(c, reject));

        var last = c.Last;
        _out.WriteLine(last.Summary);
        Assert.Equal(1, last.Exceptional);
        Assert.Equal(3, last.ToSalvageBag);
        Assert.Equal(0, last.Smelted);
        Assert.Contains("Rejects: 3 to the salvage bag.", last.Summary);

        // The bag's own Salvage All ran: nothing it can salvage is left in it, the player's dagger included.
        Assert.Empty(All<Dagger>(bag));
        Assert.True(inBag.Deleted);
        Assert.False(pickaxe.Deleted);
        Assert.Single(All<Dagger>(c.Pm.Backpack));
    }

    [Fact]
    public void ExceptionalOnlyBagsATailorsRejectsWithoutScissorsAndCutsThemWith()
    {
        var rejects = new Bag();
        var own = new Shirt();
        using var c = Make(Tailoring(), typeof(Shirt), SkillName.Tailoring, 40.0, new SewingKit { UsesRemaining = 500 },
            new Cloth(300), rejects, own);

        var reject = NotExceptional(c);
        c.Rng.Value = reject;
        Start(c, 1, exceptional: true, bag: rejects);
        RunToEnd(c, ThreeRejectsThenExceptional(c, reject));

        _out.WriteLine(c.Last.Summary);
        Assert.Equal(3, c.Last.Bagged);
        Assert.Equal(3, All<Shirt>(rejects).Count);
        Assert.Contains("Rejects: 3 to the rejects bag.", c.Last.Summary);
        Assert.True(own.IsChildOf(c.Pm.Backpack));
        Assert.False(own.IsChildOf(rejects));

        // With scissors in the pack, a reject is cut back to cloth on the spot.
        c.Pm.Backpack.DropItem(new Scissors());
        var cloth = c.Count<Cloth>();
        c.Rng.Value = reject;
        Start(c, 1, exceptional: true, bag: rejects);
        RunToEnd(c, ThreeRejectsThenExceptional(c, reject));

        _out.WriteLine(c.Last.Summary);
        Assert.Equal(3, c.Last.Cut);
        Assert.Equal(0, c.Last.Bagged);
        Assert.Equal(3, All<Shirt>(rejects).Count); // the first run's, untouched
        Assert.True(c.Count<Cloth>() > cloth - 4 * 8, "the cut shirts gave cloth back");
    }

    [Fact]
    public void ExceptionalOnlyIsNotOfferedForAnItemThatCannotBeExceptional()
    {
        var tinkering = Tinkering();
        var ring = tinkering.CraftItems.SearchFor(typeof(GargishRing));
        var dagger = Blacksmithy().CraftItems.SearchFor(typeof(Dagger));

        Assert.False(ClusterFCraftRun.CanBeExceptional(ring));
        Assert.True(ClusterFCraftRun.CanBeExceptional(dagger));

        // Asked for anyway, the run counts items as usual.
        using var c = Make(tinkering, typeof(GargishRing), SkillName.Tinkering, 120.0, new TinkerTools { UsesRemaining = 500 },
            new IronIngot(30));
        var run = Start(c, 2, exceptional: true);
        Assert.False(run.ExceptionalOnly);
        RunToEnd(c);
        Assert.Equal(2, c.Last.Made);
    }

    // ---------------------------------------------------------------- 5

    private Crafter Rings(int ingots, int uses = 500) =>
        Make(Tinkering(), typeof(GargishRing), SkillName.Tinkering, 120.0, new TinkerTools { UsesRemaining = uses },
            new IronIngot(ingots));

    private void Stops(Crafter c, MakeXStop expected, string message)
    {
        _out.WriteLine($"{expected}: {c.Last.Summary}");
        Assert.Equal(expected, c.Last.StoppedBy);
        Assert.True(Sent(c.Ns, message), $"not sent: {message}");
        Assert.Null(c.Pm.FindGump<MakeXProgressGump>());
    }

    [Fact]
    public void OutOfMaterialsStopsTheRun()
    {
        using var c = Rings(6); // two rings' worth
        Start(c, 10);
        RunToEnd(c);
        Assert.Equal(2, c.Last.Made);
        Stops(c, MakeXStop.Materials, "Craft X stopped: out of materials.");
        Assert.Equal(0, c.Context.RepeatCount); // P17's "stays armed" is gone
        Assert.Null(c.Context.RepeatItem);
    }

    [Fact]
    public void AWornOutToolStopsTheRun()
    {
        using var c = Rings(100, uses: 3);
        Start(c, 10);
        RunToEnd(c);
        Assert.Equal(3, c.Last.Made);
        Stops(c, MakeXStop.Tool, "Craft X stopped: your tool wore out.");
    }

    [Fact]
    public void AFullBackpackStopsTheRun()
    {
        using var c = Rings(100);
        c.Pm.Backpack.MaxItems = c.Pm.Backpack.TotalItems + 2;
        Start(c, 10);
        RunToEnd(c);
        Assert.Equal(2, c.Last.Made);
        Stops(c, MakeXStop.PackFull, "Craft X stopped: your backpack is full.");
    }

    [Fact]
    public void BeingOverweightStopsTheRun()
    {
        // Overloaded (StaminaSystem.IsOverloaded) while the pack still has room: weak, with a load the pack can hold.
        using var c = Rings(100);
        c.Pm.RawStr = 1;
        c.Pm.Backpack.DropItem(new Item(0x1BF2) { Weight = 150 });
        Assert.True(Server.Misc.StaminaSystem.IsOverloaded(c.Pm));
        Start(c, 10);
        RunToEnd(c);
        Assert.Equal(1, c.Last.Made);
        Stops(c, MakeXStop.Overweight, "Craft X stopped: you are carrying too much.");
    }

    [Fact]
    public void MovingStopsTheRun()
    {
        using var c = Rings(100);
        Start(c, 10);
        RunToEnd(c, run =>
        {
            if (run.Attempts == 2)
            {
                c.Pm.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
            }
        });
        Stops(c, MakeXStop.Moved, "Craft X stopped: you moved.");
        Assert.Equal(3, c.Last.Made); // the attempt in hand finishes
    }

    [Fact]
    public void DyingStopsTheRun()
    {
        using var c = Rings(100);
        Start(c, 10);
        RunToEnd(c, run =>
        {
            if (run.Attempts == 2)
            {
                c.Pm.Kill();
            }
        });
        Stops(c, MakeXStop.Died, "Craft X stopped: you died.");
    }

    [Fact]
    public void ClosingTheProgressGumpStopsTheRun()
    {
        using var c = Rings(100);
        Start(c, 10);
        RunToEnd(c, run =>
        {
            if (run.Attempts == 2)
            {
                var gump = c.Pm.FindGump<MakeXProgressGump>();
                Assert.NotNull(gump);
                gump.OnResponse(c.Ns, new RelayInfo(0, default, default, default, default)); // closed
            }
        });
        Stops(c, MakeXStop.Stopped, "Craft X stopped.");
        Assert.Equal(3, c.Last.Made);
    }
}
