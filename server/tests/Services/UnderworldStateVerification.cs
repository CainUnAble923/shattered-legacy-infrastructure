// cc-P610 (P533 A1, P491 #1-#4): the mini champion controllers and the Underworld puzzle and experimental rooms keep
// their state straight. Notes: shard-migration notes/cc-P610-fix-drafts-from-p533.md.
//
// Facts:
//   1. Two wrong guesses in a row on the same puzzle board, by the same player, give feedback both times: the Underworld
//      mastermind board and the cylinder trap training kit (pinned PuzzleChest.SubmitSolution added the player twice).
//   2. A controller switched off during its completion cooldown stays off; one saved switched off loads off; one saved
//      in its cooldown restarts at its saved time after a load; a save from before the restart store keeps ServUO's
//      restart of every inactive controller.
//   3. The magic key's expiry or use takes back the puzzle keys and its boards from nested bags too, and leaves
//      another session's board and everything else alone.
//   4. A gem deleted before its five-second start leaves no timer running.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Engines.MiniChamps;
using Server.Items;
using Server.Mobiles;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class UnderworldStateVerification
{
    private static PlayerMobile NewPlayer(Point3D at, Map map)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(at, map);
        pm.RawStr = pm.RawDex = pm.RawInt = 100;
        pm.Hits = pm.HitsMax;
        return pm;
    }

    private static T RoundTrip<T>(T original, bool deleteOriginal) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();
        var serial = original.Serial;
        if (deleteOriginal && original is Item item)
        {
            item.Delete();
        }

        var copy = (T)Activator.CreateInstance(original.GetType(), serial)!;
        if (copy is IEntity e)
        {
            World.AddEntity(e);
        }

        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    // ---------------------------------------------------------------- 1

    private static PuzzleChestSolution Wrong(PuzzleChest chest)
    {
        var wrong = new PuzzleChestSolution(chest.Solution);
        wrong.Cylinders[0] = wrong.Cylinders[0] == PuzzleChestCylinder.Red ? PuzzleChestCylinder.Blue : PuzzleChestCylinder.Red;
        return wrong;
    }

    [Fact]
    public void TwoWrongGuessesInARowOnTheSameBoardGiveFeedbackBothTimes()
    {
        ShardTestClock.Arm();
        var pm = NewPlayer(new Point3D(1100, 1100, 0), Map.TerMur);
        var key = new MagicKey();
        var board = new MastermindPuzzleItem(key);
        var kit = new CylinderTrapTrainingKit();
        pm.Backpack.DropItem(key);
        pm.Backpack.DropItem(board);
        pm.Backpack.DropItem(kit);

        try
        {
            foreach (PuzzleChest chest in new PuzzleChest[] { board, kit })
            {
                chest.SubmitSolution(pm, Wrong(chest));
                var first = chest.GetLastGuess(pm);
                chest.SubmitSolution(pm, Wrong(chest)); // threw ArgumentException: the player's entry was added again
                var second = chest.GetLastGuess(pm);
                Assert.NotNull(second);
                Assert.NotSame(first, second);
            }
        }
        finally
        {
            board.Delete();
            kit.Delete();
            key.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    /// <summary>A restart: the restart store (if there is one) is saved with the world, the old process ends (the old
    /// controller goes), and the new one reads the store (or finds none, for a save from before it) and loads the
    /// controller. Reflection for the store: the facts also run (and fail) against the code before it.</summary>
    private static MiniChamp Restart(MiniChamp c, bool storeInSave = true)
    {
        var store = typeof(MiniChamp).Assembly.GetType("Server.Engines.MiniChamps.MiniChampRestarts");
        var buffer = new byte[65536];
        if (store != null)
        {
            var writer = new BufferWriter(buffer, true);
            store.GetMethod("Serialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [writer]);
            writer.Flush();
        }

        var cBuf = new byte[65536];
        var cw = new BufferWriter(cBuf, true);
        c.Serialize(cw);
        cw.Flush();
        var serial = c.Serial;
        c.Delete();

        if (store != null)
        {
            store.GetMethod("ClearAll", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
            if (storeInSave)
            {
                store.GetMethod("Deserialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [new BufferReader(buffer)]);
            }
        }

        var copy = new MiniChamp(serial);
        World.AddEntity(copy);
        copy.Deserialize(new BufferReader(cBuf));
        return copy;
    }

    private static MiniChamp NewController()
    {
        var c = new MiniChamp { Type = MiniChampType.CrimsonVeins, RestartDelay = TimeSpan.FromMinutes(5) };
        c.MoveToWorld(new Point3D(990, 600, -50), Map.TerMur);
        return c;
    }

    private static void Complete(MiniChamp c)
    {
        c.Active = true;
        c.Level = MiniChampInfo.GetInfo(c.Type).MaxLevel;
        c.AdvanceLevel(); // past the last level: it stops and its restart waits RestartDelay
        Assert.False(c.Active);
    }

    [Fact]
    public void AControllerSwitchedOffDuringItsCooldownStaysOff()
    {
        ShardTestClock.Arm();
        var c = NewController();
        try
        {
            Complete(c);
            c.Active = false; // staff switch it off while it waits
            ShardTestClock.Advance(TimeSpan.FromMinutes(6));
            Assert.False(c.Active, "the completion's restart was cancelled");
        }
        finally
        {
            c.Delete();
        }
    }

    [Fact]
    public void AControllerSavedSwitchedOffLoadsOff()
    {
        ShardTestClock.Arm();
        var c = NewController();
        MiniChamp copy = null;
        try
        {
            Assert.False(c.Active);
            copy = Restart(c);
            ShardTestClock.Advance(TimeSpan.FromMinutes(6));
            Assert.False(copy.Active, "nothing was pending, so nothing restarts");
        }
        finally
        {
            copy?.Delete();
            c.Delete();
        }
    }

    [Fact]
    public void AControllerSavedInItsCooldownRestartsAtItsSavedTime()
    {
        ShardTestClock.Arm();
        var c = NewController();
        MiniChamp copy = null;
        try
        {
            Complete(c);
            ShardTestClock.Advance(TimeSpan.FromMinutes(2));
            copy = Restart(c);

            ShardTestClock.Advance(TimeSpan.FromMinutes(2));
            Assert.False(copy.Active);
            ShardTestClock.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(5));
            Assert.True(copy.Active, "restarted five minutes after the completion, not five after the load");
        }
        finally
        {
            copy?.Delete();
            c.Delete();
        }
    }

    [Fact]
    public void ASaveFromBeforeTheRestartStoreKeepsServUOsRestart()
    {
        ShardTestClock.Arm();
        var c = NewController();
        MiniChamp copy = null;
        try
        {
            copy = Restart(c, storeInSave: false); // no store in the save
            ShardTestClock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(5));
            Assert.True(copy.Active);
        }
        finally
        {
            copy?.Delete();
            c.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheMagicKeysEndTakesBackItsKeysAndBoardsFromNestedBagsAndNothingElse()
    {
        ShardTestClock.Arm();
        var pm = NewPlayer(new Point3D(1200, 1200, 0), Map.TerMur);
        var key = new MagicKey();
        var otherKey = new MagicKey();
        var bag = new Bag();
        var gold = new GoldPuzzleKey();
        var board = new MastermindPuzzleItem(key);
        var othersBoard = new MastermindPuzzleItem(otherKey);
        var keep = new Dagger();

        try
        {
            pm.Backpack.DropItem(key);
            pm.Backpack.DropItem(bag);
            bag.DropItem(gold);
            bag.DropItem(board);
            bag.DropItem(othersBoard);
            bag.DropItem(keep);

            key.Decay(); // its time ran out (MastermindPuzzleItem's success calls the same)

            Assert.True(gold.Deleted, "a puzzle key in a nested bag is taken back");
            Assert.True(board.Deleted, "this key's board in a nested bag is taken back");
            Assert.False(othersBoard.Deleted, "another session's board stays");
            Assert.False(keep.Deleted);
        }
        finally
        {
            foreach (var i in new Item[] { key, otherKey, bag, gold, board, othersBoard, keep })
            {
                i.Delete();
            }

            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void AGemDeletedBeforeItsStartLeavesNoTimer()
    {
        ShardTestClock.Arm();
        var pm = NewPlayer(new Point3D(980, 1100, 0), Map.TerMur);
        var gem = new ExperimentalGem();
        pm.Backpack.DropItem(gem);

        try
        {
            gem.Activate(pm);
            gem.Delete(); // within the five seconds before BeginRoom_Callback
            ShardTestClock.Advance(TimeSpan.FromSeconds(10));

            var timer = (Timer)typeof(ExperimentalGem).GetField("_timer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(gem);
            Assert.True(timer?.Running != true, "no repeating timer for a deleted gem");
        }
        finally
        {
            gem.Delete();
            pm.Delete();
        }
    }
}
