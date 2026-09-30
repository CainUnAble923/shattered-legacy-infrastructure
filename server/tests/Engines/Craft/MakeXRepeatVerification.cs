// MakeXRepeatVerification.cs
//
// cc-P17 PT-04 (report only; F-1 is P18's): "Make X, 5 items" made one item and, by the player's account, wrote
// nothing to the journal. These facts reproduce every way a run stops after one success, through the real entry
// point (CraftCountGump's "5 items" button) and the real timer chain. Notes in shard-migration
// notes/cc-P17-playtest-bugs-1.md, PT-04.
//
// What they pin:
//   1. The loop itself works: at a skill that cannot fail, five rings, and no craft gump between them.
//   2. A failed skill check on the second attempt ends the run after one item, and its message goes to the craft
//      gump's notice box, not the journal: PlayEndingEffect returns the cliloc (DefTinkering.cs:111) and
//      CraftItem.cs:1354-1361 hands it to a new CraftGump, which draws it inside the gump (CraftGump.cs:113). So "no
//      failure line in the journal" does not rule this out.
//   3. Running out of material ends the run after one item the same way, and leaves the repeat armed (RepeatCount 3
//      of 5): Craft()'s pre-checks (CraftItem.cs:969-996) never clear it. A defect, recorded, not fixed here.
//
// Random numbers are pinned by swapping BuiltInRng.Generator (internal setter, visible to this assembly) for a
// constant source, restored in finally.

using System;
using System.Reflection;
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
public class MakeXRepeatVerification
{
    private readonly ITestOutputHelper _out;

    public MakeXRepeatVerification(ITestOutputHelper output) => _out = output;

    // Every draw returns Value: 0.0 makes every chance succeed, 0.999 makes a 50% chance fail.
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

    private static CraftSystem Tinkering()
    {
        if (DefTinkering.CraftSystem == null)
        {
            DefTinkering.Initialize();
        }

        TinkeringCraftRegistrations.Register();
        return DefTinkering.CraftSystem;
    }

    private sealed class Run : IDisposable
    {
        public PlayerMobile Tinker;
        public NetState Ns;
        public TinkerTools Tools;
        public CraftSystem System;
        public CraftItem Entry;

        public int Rings => Tinker.Backpack.GetAmount(typeof(GargishRing));
        public CraftContext Context => System.GetContext(Tinker);

        public TextDefinition Notice =>
            Tinker.FindGump<CraftGump>() is { } gump
                ? (TextDefinition)typeof(CraftGump).GetField("_notice", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(gump)
                : null;

        public void Dispose()
        {
            Tinker.NetState = null;
            Ns.Mobile = null;
            Tinker.Delete();
            BuiltInRng.Reset();
        }
    }

    // A tinker with the given skill and ingots, online, pressing "5 items" on GargishRing (3 ingots, 65-115).
    private static Run Start(double skill, int ingots)
    {
        ShardTestClock.Arm();

        // A craft below its maximum skill runs the gain check, which reads these; the host never configures them.
        if (Server.Misc.AntiMacroSystem.Settings == null)
        {
            Server.Misc.AntiMacroSystem.Configure();
        }

        var run = new Run { System = Tinkering(), Tools = new TinkerTools() };
        run.Entry = run.System.CraftItems.SearchFor(typeof(GargishRing));

        run.Tinker = new PlayerMobile { Player = true };
        run.Tinker.AddItem(new Backpack());
        run.Tinker.MoveToWorld(new Point3D(1400, 1724, 0), Map.Trammel);
        run.Tinker.Skills.Tinkering.Base = skill;
        run.Tinker.Backpack.DropItem(run.Tools);
        run.Tinker.Backpack.DropItem(new IronIngot(ingots));

        run.Ns = PacketTestUtilities.CreateTestNetState();
        run.Tinker.NetState = run.Ns;
        run.Ns.Mobile = run.Tinker;

        // No maker's mark prompt in the middle of a run.
        run.System.GetContext(run.Tinker).MarkOption = CraftMarkOption.DoNotMark;

        new CraftCountGump(run.Tinker, run.System, run.Entry, run.Tools)
            .OnResponse(run.Ns, new RelayInfo(10, default, default, default, default)); // "5 items"

        return run;
    }

    // Advances in 250 ms steps until `done` or 20 s; returns whether a craft gump was ever open while fewer than
    // `until` rings existed.
    private static bool AdvanceWatchingGump(Run run, int until)
    {
        var gumpMidRun = false;
        for (var i = 0; i < 80; i++)
        {
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(250));

            if (run.Rings < until && run.Tinker.FindGump<CraftGump>() != null)
            {
                gumpMidRun = true;
            }
        }

        return gumpMidRun;
    }

    [Fact]
    public void AtASkillThatCannotFailFiveItemsMakesFive()
    {
        using var run = Start(120.0, 15); // chance (120 - 65) / (115 - 65) > 1

        var gumpMidRun = AdvanceWatchingGump(run, 5);
        _out.WriteLine($"rings {run.Rings}, notice {run.Notice?.Number}, repeat {run.Context.RepeatCount}");

        Assert.Equal(5, run.Rings);
        Assert.False(gumpMidRun);
        Assert.Equal(0, run.Context.RepeatCount);
        Assert.Null(run.Context.RepeatItem);
    }

    [Fact]
    public void AFailedSecondAttemptEndsTheRunAfterOneAndSaysSoOnlyInTheGump()
    {
        var rng = new ConstantRandom { Value = 0.0 };
        BuiltInRng.Generator = rng;

        using var run = Start(90.0, 15); // chance (90 - 65) / 50 = 0.5, and under 100: no maker's mark

        // Succeed until the first ring exists, then fail every draw.
        for (var i = 0; i < 80 && run.Rings == 0; i++)
        {
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(250));
        }

        Assert.Equal(1, run.Rings);
        rng.Value = 0.999;

        var journalBefore = run.Ns.SendBuffer.GetReadSpan().Length;
        AdvanceWatchingGump(run, 1);

        var notice = run.Notice;
        _out.WriteLine($"rings {run.Rings}, notice {notice?.Number}, repeat {run.Context.RepeatCount}");

        Assert.Equal(1, run.Rings);
        Assert.NotNull(notice);
        Assert.Contains(notice.Number, new[] { 1044043, 1044157 }); // "You failed to create the item..."
        Assert.Equal(0, run.Context.RepeatCount);
        Assert.False(SentLocalized(run.Ns, journalBefore, notice.Number), "the failure reached the journal");
    }

    [Fact]
    public void RunningOutOfMaterialEndsTheRunAfterOneAndLeavesTheRepeatArmed()
    {
        using var run = Start(120.0, 3); // one ring's worth

        AdvanceWatchingGump(run, 1);

        var notice = run.Notice;
        _out.WriteLine($"rings {run.Rings}, notice {notice?.Number} {notice?.String}, repeat {run.Context.RepeatCount}");

        Assert.Equal(1, run.Rings);
        Assert.NotNull(notice);
        Assert.Equal(3, run.Context.RepeatCount); // the defect: Craft()'s pre-checks leave it armed
        Assert.Same(run.Entry, run.Context.RepeatItem);
    }

    // A 0xC1 localized message with this cliloc anywhere on the wire since `from`.
    private static bool SentLocalized(NetState ns, int from, int cliloc)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        for (var i = 0; i + 18 <= span.Length; i++)
        {
            if (span[i] == 0xC1 && System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(span[(i + 14)..]) == cliloc)
            {
                return true;
            }
        }

        return false;
    }
}
