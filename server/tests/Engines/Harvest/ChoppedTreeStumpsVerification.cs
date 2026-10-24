// P359: run through real harvest completion, banks, timers and serialization.
// P412: map trees (base Z, the client message, login), lumber tools, two players, re-marking and restart.
using System;
using System.Buffers.Binary;
using System.Linq;
using System.Reflection;
using Server;
using Server.Engines.Harvest;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ChoppedTreeStumpsVerification : IDisposable
{
    private readonly bool _enabled = ClusterFLumberStumps.Enabled;
    private readonly int _itemId = ClusterFLumberStumps.StumpItemId;
    private readonly Func<Map, int, int, System.Collections.Generic.IEnumerable<(int ItemID, int Z)>> _staticsAt =
        ClusterFLumberStumps.StaticsAt;
    private readonly Lumberjacking _system = (Lumberjacking)Activator.CreateInstance(typeof(Lumberjacking), true)!;
    private readonly PlayerMobile _player;
    private readonly Static _tree;

    public ChoppedTreeStumpsVerification()
    {
        ShardTestClock.Arm();
        _player = new PlayerMobile { Player = true, Race = Race.Human };
        _tree = new Static(0xCCA);
        ClusterFLumberStumps.Enabled = true;
        ClusterFLumberStumps.StumpItemId = 0xE56;
        // Player and target straddle the bank boundary; target Z must be preserved.
        _player.MoveToWorld(new Point3D(1239, 1230, 0), Map.Trammel);
        _tree.MoveToWorld(new Point3D(1240, 1230, 7), Map.Trammel);
        var def = Definition;
        def.MinTotal = def.MaxTotal = 20;
        def.MinRespawn = def.MaxRespawn = TimeSpan.FromSeconds(10);
    }

    private HarvestDefinition Definition => _system.Definitions[0];
    private HarvestBank Bank => Definition.GetBank(_tree.Map, _tree.X, _tree.Y);
    private static ChoppedTreeStump[] Stumps => World.Items.Values.OfType<ChoppedTreeStump>()
        .Where(s => !s.Deleted).ToArray();

    private void Finish(HarvestBank bank) => _system.OnHarvestFinished(
        _player, null, Definition, bank.Vein, bank, null, _tree);

    private ChoppedTreeStump Empty()
    {
        Bank.Consume(20, _player);
        Finish(Bank);
        return Assert.Single(Stumps);
    }

    [Fact]
    public void SuccessfulHarvestCompletionPlacesTheStumpOnTheFinalSwing()
    {
        var axe = new Hatchet { Layer = Layer.TwoHanded };
        var backpack = new Backpack();
        _player.AddItem(backpack);
        _player.AddItem(axe);
        _player.Skills.Lumberjacking.Base = 200;
        Definition.RaceBonus = false; // Remove the random human extra-log roll.
        var skillCheck = Mobile.SkillCheckLocationHandler;
        Mobile.SkillCheckLocationHandler = (_, _, _, _) => true;
        try
        {
            _system.FinishHarvesting(_player, axe, Definition, _tree, _system);
            Assert.Equal(10, Bank.Current);
            Assert.Empty(Stumps);
            _system.FinishHarvesting(_player, axe, Definition, _tree, _system);
            Assert.Equal(0, Bank.Current);
            Assert.Equal(_tree.Location, Assert.Single(Stumps).Location);
        }
        finally
        {
            Mobile.SkillCheckLocationHandler = skillCheck;
            axe.Delete();
            backpack.Delete();
        }
    }

    [Fact]
    public void EmptyBankMarksOnlyTheTargetAndDoesNotDuplicateTheStump()
    {
        var other = new Static(0xCCD);
        try
        {
            other.MoveToWorld(new Point3D(_tree.X + 1, _tree.Y, _tree.Z), _tree.Map);
            Bank.Consume(10, _player);
            Finish(Bank);
            Assert.Empty(Stumps);
            Bank.Consume(10, _player);
            Finish(Bank);
            var stump = Assert.Single(Stumps);
            Assert.Equal(0, Bank.Current);
            Assert.Equal(_tree.Location, stump.Location);
            Assert.Equal(_tree.Map, stump.Map);
            Assert.False(stump.Movable);
            Assert.Equal("This tree has been chopped clean.", stump.DefaultName);
            Assert.InRange(stump.ItemID, 0xE56, 0xE59);
            Assert.Equal(20, Definition.GetBank(_player.Map, _player.X, _player.Y).Current);
            Finish(Bank);
            Assert.Same(stump, Assert.Single(Stumps));
        }
        finally { other.Delete(); }
    }

    [Fact]
    public void UnharvestableRemainderAlsoMarksTheTree()
    {
        Bank.Consume(15, _player);
        Finish(Bank);
        Assert.Equal(5, Bank.Current);
        Assert.Single(Stumps);
    }

    [Fact]
    public void BankRefillOnTouchRemovesTheStumpImmediately()
    {
        var stump = Empty();
        // Make the real bank due before its original marker timer runs.
        typeof(HarvestBank).GetField("m_NextRespawn", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(Bank, Core.Now - TimeSpan.FromSeconds(1));
        Assert.Equal(20, Bank.Current);
        Assert.True(stump.Deleted);
        Assert.Empty(Stumps);
    }

    [Fact]
    public void IdleBankMarkerExpiresAtFirstChopsDeadlineRatherThanLastChop()
    {
        Bank.Consume(10, _player);
        var due = Bank.NextRespawn;
        ShardTestClock.Advance(TimeSpan.FromSeconds(6));
        Bank.Consume(10, _player);
        Finish(Bank);
        var stump = Assert.Single(Stumps);
        Assert.Equal(due, Bank.NextRespawn);
        ShardTestClock.Advance(TimeSpan.FromSeconds(3));
        Assert.False(stump.Deleted);
        ShardTestClock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(stump.Deleted);
        Assert.Empty(Stumps);
    }

    [Fact]
    public void DisabledSettingPlacesNoStump()
    {
        ClusterFLumberStumps.Enabled = false;
        Bank.Consume(20, _player);
        Finish(Bank);
        Assert.Empty(Stumps);
    }

    [Fact]
    public void StumpIsExcludedFromSavesAndDefensivelyDeletesOnLoad()
    {
        var stump = Empty();
        Assert.True(stump.SkipSerialization);
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        stump.Serialize(writer);
        writer.Flush();
        var serial = stump.Serial;
        stump.Delete();
        var copy = new ChoppedTreeStump(serial);
        try
        {
            copy.Deserialize(new BufferReader(buffer));
            Assert.True(copy.Deleted);
            Assert.Empty(Stumps);
        }
        finally { copy.Delete(); }
    }

    [Theory]
    [InlineData(0xE56, 0xE56)]
    [InlineData(0xE59, 0xE59)]
    [InlineData(-1, 0xE56)]
    [InlineData(0xE55, 0xE56)]
    [InlineData(0xE5A, 0xE59)]
    public void StumpArtSettingStaysInStockRange(int setting, int expected)
    {
        ClusterFLumberStumps.StumpItemId = setting;
        Assert.Equal(expected, Empty().ItemID);
    }

    [Fact]
    public void AutomaticArtPicksAStockStump()
    {
        ClusterFLumberStumps.StumpItemId = 0;
        Assert.Equal(0, ClusterFLumberStumps.StumpItemId);
        Assert.InRange(Empty().ItemID, 0xE56, 0xE59);
    }

    [Fact]
    public void MiningBankAndMismatchedTargetBankDoNotPlaceStumps()
    {
        var ore = ((Mining)Activator.CreateInstance(typeof(Mining), true)!).OreAndStone;
        var oreBank = ore.GetBank(_tree.Map, _tree.X, _tree.Y);
        oreBank.Consume(oreBank.Current, _player);
        _system.OnHarvestFinished(_player, null, ore, oreBank.Vein, oreBank, null, _tree);
        var wrongBank = Definition.GetBank(_player.Map, _player.X, _player.Y);
        wrongBank.Consume(20, _player);
        Finish(wrongBank);
        Assert.Empty(Stumps);
    }

    // ---- P412 ---------------------------------------------------------------------------------------------------

    // A map tree away from _tree's bank: trunk 0x0CCD and its leaves 0x0CCE, both at Z 7, as the client map has them.
    private static readonly Point3D MapTree = new(1252, 1240, 7);

    private void SeedMapTree(params (int ItemID, int Z)[] statics) =>
        ClusterFLumberStumps.StaticsAt = (map, x, y) =>
            map == Map.Trammel && x == MapTree.X && y == MapTree.Y ? statics : [];

    private HarvestBank MapTreeBank => Definition.GetBank(Map.Trammel, MapTree.X, MapTree.Y);

    private void FinishOnMapTree(int itemId)
    {
        var bank = MapTreeBank;
        bank.Consume(20, _player);
        // Built inside the fact's tile scope: StaticTarget reads the row's height in its constructor.
        var target = new StaticTarget(MapTree, itemId);
        _system.OnHarvestFinished(_player, null, Definition, bank.Vein, bank, null, target);
    }

    private static NetState Online(PlayerMobile pm)
    {
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        return ns;
    }

    private static void Offline(PlayerMobile pm)
    {
        if (pm.NetState != null)
        {
            pm.NetState.Mobile = null;
            pm.NetState = null;
        }
    }

    private static bool SentLocalized(NetState ns, int from, int cliloc)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        for (var i = 0; i + 18 <= span.Length; i++)
        {
            if (span[i] == 0xC1 && BinaryPrimitives.ReadInt32BigEndian(span[(i + 14)..]) == cliloc)
            {
                return true;
            }
        }

        return false;
    }

    private record SentTree(int Facet, int X, int Y, int Seconds, int[] Ids);

    // Reads one 0xF4 sub 0x05 packet the way client patch 0006 does.
    private static SentTree[] ReadTrees(ReadOnlySpan<byte> packet)
    {
        Assert.Equal(0xF4, packet[0]);
        Assert.Equal(packet.Length, BinaryPrimitives.ReadUInt16BigEndian(packet[1..]));
        Assert.Equal(0x05, packet[3]);
        int facet = packet[4];
        var payload = packet[9..];
        Assert.Equal(payload.Length, (int)BinaryPrimitives.ReadUInt32BigEndian(packet[5..]));
        var trees = new System.Collections.Generic.List<SentTree>();
        var o = 0;
        while (o < payload.Length)
        {
            int n = payload[o + 8];
            var ids = new int[n];
            for (var i = 0; i < n; i++)
            {
                ids[i] = BinaryPrimitives.ReadUInt16BigEndian(payload[(o + 9 + 2 * i)..]);
            }

            trees.Add(new SentTree(
                facet, BinaryPrimitives.ReadUInt16BigEndian(payload[o..]), BinaryPrimitives.ReadUInt16BigEndian(payload[(o + 2)..]),
                (int)BinaryPrimitives.ReadUInt32BigEndian(payload[(o + 4)..]), ids
            ));
            o += 9 + 2 * n;
        }

        return trees.ToArray();
    }

    [Fact]
    public void MapTreeStumpSitsOnTheTreesBaseZNotItsTop()
    {
        using var tiles = TestTileRows.Seed(TileRows.Tree, TileRows.TreeLeaves);
        SeedMapTree((0xCCD, 7), (0xCCE, 7));
        Assert.Equal(27, new StaticTarget(MapTree, 0xCCD).Location.Z); // what pinned hands the harvest: 7 + 20
        FinishOnMapTree(0xCCD);
        var stump = Assert.Single(Stumps);
        Assert.Equal(MapTree, stump.Location);
    }

    [Fact]
    public void TargetingTheLeavesPutsTheStumpAtTheTrunk()
    {
        using var tiles = TestTileRows.Seed(TileRows.Tree, TileRows.TreeLeaves);
        SeedMapTree((0xCCD, 7), (0xCCE, 7));
        FinishOnMapTree(0xCCE);
        Assert.Equal(MapTree, Assert.Single(Stumps).Location);
    }

    [Fact]
    public void WithoutMapDataTheTreesHeightStillComesOff()
    {
        using var tiles = TestTileRows.Seed(TileRows.Tree, TileRows.TreeLeaves);
        SeedMapTree();
        FinishOnMapTree(0xCCD);
        var stump = Assert.Single(Stumps);
        Assert.Equal(MapTree, stump.Location);
        Assert.Empty(stump.TreeIds); // nothing on the map to hide
    }

    [Fact]
    public void ClientsAreToldTheTreeItsIdsAndDeadlineWhenItEmptiesAndWhenItRegrows()
    {
        using var tiles = TestTileRows.Seed(TileRows.Tree, TileRows.TreeLeaves);
        // A roof tile far above and an unrelated static on the same tile are not part of the tree.
        SeedMapTree((0xCCD, 7), (0xCCE, 7), (0x0080, 60));
        var sent = new System.Collections.Generic.List<byte[]>();
        Action<byte[]> listen = sent.Add;
        ClusterFExhaustedTrees.Broadcasting += listen;
        try
        {
            FinishOnMapTree(0xCCD);
            var stump = Assert.Single(Stumps);
            var hidden = Assert.Single(ReadTrees(Assert.Single(sent)));
            Assert.Equal(new[] { 0xCCD, 0xCCE }, hidden.Ids);
            Assert.Equal((Map.Trammel.MapID, MapTree.X, MapTree.Y), (hidden.Facet, hidden.X, hidden.Y));
            Assert.Equal(10, hidden.Seconds); // the bank's refill deadline: MinRespawn = MaxRespawn = 10 s
            Assert.Equal(MapTreeBank.NextRespawn, stump.Deadline);

            ShardTestClock.Advance(TimeSpan.FromSeconds(11));
            Assert.True(stump.Deleted);
            Assert.Equal(2, sent.Count);
            var regrown = Assert.Single(ReadTrees(sent[1]));
            Assert.Equal((MapTree.X, MapTree.Y, 0), (regrown.X, regrown.Y, regrown.Seconds));
            Assert.Equal(new[] { 0xCCD, 0xCCE }, regrown.Ids);
        }
        finally
        {
            ClusterFExhaustedTrees.Broadcasting -= listen;
        }
    }

    [Fact]
    public void ALoginGetsEveryExhaustedTreeWithTheTimeLeftAndNothingOnceItRegrew()
    {
        using var tiles = TestTileRows.Seed(TileRows.Tree, TileRows.TreeLeaves);
        SeedMapTree((0xCCD, 7), (0xCCE, 7));
        // First: a test NetState sets Core.Now to the wall clock (D16, ShardTestClock.cs), which would move the deadline.
        var ns = Online(_player);
        try
        {
            FinishOnMapTree(0xCCD);
            ShardTestClock.Advance(TimeSpan.FromSeconds(4));
            var from = ns.SendBuffer.GetReadSpan().Length;
            ClusterFExhaustedTrees.OnLogin(_player);
            var span = ns.SendBuffer.GetReadSpan()[from..];
            Assert.True(span.Length > 9 && span[0] == 0xF4 && span[3] == 0x05, "no 0xF4 sub 0x05 at login");
            var tree = Assert.Single(ReadTrees(span[..BinaryPrimitives.ReadUInt16BigEndian(span[1..])]));
            Assert.Equal((MapTree.X, MapTree.Y, 6), (tree.X, tree.Y, tree.Seconds));

            ShardTestClock.Advance(TimeSpan.FromSeconds(7));
            Assert.Empty(ClusterFExhaustedTrees.BuildLoginPackets());
        }
        finally
        {
            Offline(_player);
        }
    }

    [Fact]
    public void AnEquippedPolearmGetsTheNoWoodMessageNotEquipAnAxe()
    {
        var ns = Online(_player); // before the stump: it sets Core.Now (D16)
        var stump = Empty();
        var bardiche = new Bardiche { Layer = Layer.TwoHanded };
        try
        {
            Assert.Equal(500487, stump.RefusalFor(_player)); // nothing equipped
            _player.AddItem(bardiche);
            Assert.Same(bardiche, _player.Weapon);
            Assert.Equal(500493, stump.RefusalFor(_player));
            var from = ns.SendBuffer.GetReadSpan().Length;
            stump.OnDoubleClick(_player);
            Assert.True(SentLocalized(ns, from, 500493), "double-click: no 500493");
            from = ns.SendBuffer.GetReadSpan().Length;
            ((IChoppable)stump).OnChop(_player); // the route HarvestTarget takes for a tool aimed at the stump
            Assert.True(SentLocalized(ns, from, 500493), "targeted: no 500493");
            Assert.False(SentLocalized(ns, 0, 500487), "told to equip an axe");
        }
        finally
        {
            Offline(_player);
            bardiche.Delete();
        }
    }

    [Fact]
    public void TwoPlayersFinishingOnTheLastBatchMakeOneStumpAndKeepTheDeadline()
    {
        var second = new PlayerMobile { Player = true, Race = Race.Human };
        var axes = new[] { new Hatchet { Layer = Layer.TwoHanded }, new Hatchet { Layer = Layer.TwoHanded } };
        var packs = new[] { new Backpack(), new Backpack() };
        var skillCheck = Mobile.SkillCheckLocationHandler;
        try
        {
            second.MoveToWorld(new Point3D(1241, 1230, 0), Map.Trammel);
            var players = new[] { _player, second };
            for (var i = 0; i < 2; i++)
            {
                players[i].AddItem(packs[i]);
                players[i].AddItem(axes[i]);
                players[i].Skills.Lumberjacking.Base = 200;
            }

            Definition.RaceBonus = false;
            Mobile.SkillCheckLocationHandler = (_, _, _, _) => true;
            Bank.Consume(10, _player); // one batch left; the first chop set the deadline
            var due = Bank.NextRespawn;
            ShardTestClock.Advance(TimeSpan.FromSeconds(2));

            _system.FinishHarvesting(_player, axes[0], Definition, _tree, _system);
            _system.FinishHarvesting(second, axes[1], Definition, _tree, _system);

            Assert.Equal(0, Bank.Current);
            var stump = Assert.Single(Stumps);
            Assert.Equal(due, Bank.NextRespawn);
            Assert.Equal(due, stump.Deadline);
        }
        finally
        {
            Mobile.SkillCheckLocationHandler = skillCheck;
            foreach (var axe in axes) { axe.Delete(); }
            foreach (var pack in packs) { pack.Delete(); }
            second.Delete();
        }
    }

    [Theory]
    [InlineData("expiry")]
    [InlineData("refill")]
    [InlineData("delete")]
    public void TheSameBankIsMarkedAgainAfterItsStumpGoes(string how)
    {
        var first = Empty();
        switch (how)
        {
            case "expiry":
                ShardTestClock.Advance(TimeSpan.FromSeconds(11)); // the idle timer removes the visual only
                break;
            case "refill":
                typeof(HarvestBank).GetField("m_NextRespawn", BindingFlags.NonPublic | BindingFlags.Instance)!
                    .SetValue(Bank, Core.Now - TimeSpan.FromSeconds(1));
                Assert.Equal(20, Bank.Current);
                break;
            default:
                first.Delete();
                break;
        }

        Assert.True(first.Deleted);
        Assert.DoesNotContain(first, ClusterFLumberStumps.Stumps);

        if (Bank.Current > 0)
        {
            Bank.Consume(Bank.Current, _player);
        }

        Finish(Bank);
        var again = Assert.Single(Stumps);
        Assert.NotSame(first, again);
        Assert.Same(again, Assert.Single(ClusterFLumberStumps.Stumps));
    }

    [Fact]
    public void AfterASaveAndRestartNoStumpIsInTheWorldAndTheBankIsFull()
    {
        var stump = Empty();
        // The world save skips it (pinned GenericEntityPersistence.cs:256 and :289) ...
        Assert.True(stump.SkipSerialization);
        // ... a forced load of its bytes deletes it ...
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        stump.Serialize(writer);
        writer.Flush();
        var serial = stump.Serial;
        stump.Delete();
        var copy = new ChoppedTreeStump(serial);
        try
        {
            copy.Deserialize(new BufferReader(buffer));
            Assert.True(copy.Deleted);
        }
        finally { copy.Delete(); }

        // ... and a fresh server's banks start full, with no marker and nothing for a login to hide.
        var restarted = (Lumberjacking)Activator.CreateInstance(typeof(Lumberjacking), true)!;
        var bank = restarted.Definitions[0].GetBank(_tree.Map, _tree.X, _tree.Y);
        Assert.True(bank.Current >= restarted.Definitions[0].ConsumedPerHarvest);
        Assert.Empty(Stumps);
        Assert.Empty(ClusterFLumberStumps.Stumps);
        Assert.Empty(ClusterFExhaustedTrees.BuildLoginPackets());
    }

    public void Dispose()
    {
        foreach (var stump in Stumps) { stump.Delete(); }
        _tree.Delete();
        _player.Delete();
        ClusterFLumberStumps.Enabled = _enabled;
        ClusterFLumberStumps.StumpItemId = _itemId;
        ClusterFLumberStumps.StaticsAt = _staticsAt;
    }
}
