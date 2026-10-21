// P359: run through real harvest completion, banks, timers and serialization.
using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Engines.Harvest;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ChoppedTreeStumpsVerification : IDisposable
{
    private readonly bool _enabled = ClusterFLumberStumps.Enabled;
    private readonly int _itemId = ClusterFLumberStumps.StumpItemId;
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

    public void Dispose()
    {
        foreach (var stump in Stumps) { stump.Delete(); }
        _tree.Delete();
        _player.Delete();
        ClusterFLumberStumps.Enabled = _enabled;
        ClusterFLumberStumps.StumpItemId = _itemId;
    }
}
