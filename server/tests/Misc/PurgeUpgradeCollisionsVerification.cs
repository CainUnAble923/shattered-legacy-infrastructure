// PurgeUpgradeCollisionsVerification.cs
//
// cc-P37: the staff command that deletes the 11 types upstream d4531cd9 ships under our names
// (server/customizations/ClusterFPurgeUpgradeCollisions.cs). Notes in shard-migration
// notes/cc-P37-upgrade-prep.md. P38 deletes this file with the command and our 11 collision files.
//
// Facts:
//   1. Every one of the 11 still carries our serialization shape, [SerializationGenerator(_, false)], which is
//      what breaks the load on the new engine (cc-P30 2c) and what the guard looks for.
//   2. The guard refuses when any guarded type is encoded, as upstream's are: nothing is deleted.
//   3. A dry run reports and deletes nothing. "dryrun" is a dry run; no argument and "purge" purge, as every world
//      command's plain form acts; any other word does neither.
//   4. A purge deletes a colliding creature, a tamed one, a colliding item inside a container and the creature's
//      own pack item; it keeps an unrelated creature and item, keeps the spawner and its entry, and holds a
//      spawner that names a colliding type for SpawnerHoldMinutes. A dry run afterwards finds nothing of ours.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PurgeUpgradeCollisionsVerification
{
    private readonly ITestOutputHelper _out;

    public PurgeUpgradeCollisionsVerification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D Here = new(1500, 1600, 0);

    // ---- 1 ----------------------------------------------------------------------------------------

    [Fact]
    public void AllElevenStillHaveOurSerializationShape()
    {
        Assert.Equal(11, ClusterFPurgeUpgradeCollisions.CollidingTypes.Length);
        Assert.Equal(11, ClusterFPurgeUpgradeCollisions.CollidingTypes.Distinct().Count());

        foreach (var t in ClusterFPurgeUpgradeCollisions.CollidingTypes)
        {
            _out.WriteLine($"{t.FullName}: ours {ClusterFPurgeUpgradeCollisions.IsOurPort(t)}");
        }

        Assert.Empty(ClusterFPurgeUpgradeCollisions.TypesThatAreNotOurPorts(ClusterFPurgeUpgradeCollisions.CollidingTypes));
    }

    // ---- 2 ----------------------------------------------------------------------------------------

    [Fact]
    public void TheGuardRefusesWhenATypeIsEncodedAndDeletesNothing()
    {
        // ShrunkPet is ours too but [SerializationGenerator(0)], the encoded form upstream's 11 use.
        Assert.False(ClusterFPurgeUpgradeCollisions.IsOurPort(typeof(ShrunkPet)));

        var raptor = new Raptor();
        raptor.MoveToWorld(Here, Map.Felucca);

        try
        {
            var lines = new List<string>();
            var guarded = ClusterFPurgeUpgradeCollisions.CollidingTypes.Append(typeof(ShrunkPet)).ToArray();
            var report = ClusterFPurgeUpgradeCollisions.Run(false, lines.Add, guarded);

            lines.ForEach(_out.WriteLine);
            Assert.True(report.Refused);
            Assert.Equal(0, report.Deleted);
            Assert.False(raptor.Deleted);
            Assert.StartsWith("REFUSED", Assert.Single(lines));
            Assert.Contains("ShrunkPet", lines[0]);
        }
        finally
        {
            raptor.Delete();
        }
    }

    // ---- 3 ----------------------------------------------------------------------------------------

    [Fact]
    public void DryrunReportsThePlainFormAndPurgeDeleteAnythingElseDoesNothing()
    {
        Assert.True(ClusterFPurgeUpgradeCollisions.ParseMode(null));
        Assert.True(ClusterFPurgeUpgradeCollisions.ParseMode(""));
        Assert.False(ClusterFPurgeUpgradeCollisions.ParseMode("dryrun"));
        Assert.False(ClusterFPurgeUpgradeCollisions.ParseMode(" DryRun "));
        Assert.True(ClusterFPurgeUpgradeCollisions.ParseMode("purge"));
        Assert.True(ClusterFPurgeUpgradeCollisions.ParseMode("PURGE"));
        Assert.Null(ClusterFPurgeUpgradeCollisions.ParseMode("yes"));
        Assert.Null(ClusterFPurgeUpgradeCollisions.ParseMode("purge now"));
    }

    [Fact]
    public void ADryRunReportsAndDeletesNothing()
    {
        var slith = new Slith();
        slith.MoveToWorld(Here, Map.Felucca);
        var glaive = new ValkyriesGlaive();
        glaive.MoveToWorld(Here, Map.Felucca);

        try
        {
            var lines = new List<string>();
            var report = ClusterFPurgeUpgradeCollisions.Run(true, lines.Add);

            lines.ForEach(_out.WriteLine);
            Assert.False(report.Refused);
            Assert.Contains(slith, report.Mobiles);
            Assert.Contains(glaive, report.Items);
            Assert.Equal(0, report.Deleted);
            Assert.False(slith.Deleted);
            Assert.False(glaive.Deleted);
            Assert.StartsWith("DRY RUN", lines[0]);
            Assert.StartsWith("TOTAL (dry run)", lines[^1]);
        }
        finally
        {
            slith.Delete();
            glaive.Delete();
        }
    }

    // ---- 4 ----------------------------------------------------------------------------------------

    [Fact]
    public void APurgeDeletesTheElevenKeepsTheRestAndHoldsTheirSpawners()
    {
        var owner = new PlayerMobile { Name = "PurgeTestOwner" };
        owner.MoveToWorld(Here, Map.Felucca);

        var wild = new StoneSlith();
        wild.MoveToWorld(Here, Map.Felucca);
        var packItem = new Gold(7);
        wild.AddToBackpack(packItem);

        var tame = new Raptor();
        tame.MoveToWorld(Here, Map.Felucca);
        tame.SetControlMaster(owner);

        var bag = new Bag();
        bag.MoveToWorld(Here, Map.Felucca);
        var held = new StormCaller();
        bag.DropItem(held);

        var unrelatedMobile = new Mongbat();
        unrelatedMobile.MoveToWorld(Here, Map.Felucca);
        var unrelatedItem = new Katana();
        unrelatedItem.MoveToWorld(Here, Map.Felucca);

        var spawner = new Spawner(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), 0, default, "ToxicSlith");
        spawner.MoveToWorld(Here, Map.Felucca);
        var otherSpawner = new Spawner(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(10), 0, default, "Rat");
        otherSpawner.MoveToWorld(Here, Map.Felucca);
        var otherNextBefore = otherSpawner.NextSpawn;

        try
        {
            var lines = new List<string>();
            var report = ClusterFPurgeUpgradeCollisions.Run(false, lines.Add);
            lines.ForEach(_out.WriteLine);

            Assert.False(report.Refused);
            Assert.True(wild.Deleted);
            Assert.True(packItem.Deleted); // with its creature
            Assert.True(tame.Deleted);
            Assert.True(held.Deleted);
            Assert.False(bag.Deleted);
            Assert.False(owner.Deleted);
            Assert.False(unrelatedMobile.Deleted);
            Assert.False(unrelatedItem.Deleted);

            Assert.Contains(lines, l => l.Contains("owned: Raptor") && l.Contains("PurgeTestOwner"));
            Assert.Contains(lines, l => l.Contains("item: StormCaller") && l.Contains("inside Bag"));

            // The spawner stays, with its entry, and waits SpawnerHoldMinutes; the unrelated one is untouched.
            Assert.False(spawner.Deleted);
            Assert.Equal("ToxicSlith", Assert.Single(spawner.Entries).SpawnedName);
            Assert.Contains(spawner, report.Spawners);
            Assert.DoesNotContain(otherSpawner, report.Spawners);
            var hold = spawner.NextSpawn;
            _out.WriteLine($"held spawner next spawn in {hold}; other spawner {otherNextBefore} -> {otherSpawner.NextSpawn}");
            Assert.InRange(
                hold,
                TimeSpan.FromMinutes(ClusterFPurgeUpgradeCollisions.SpawnerHoldMinutes - 1),
                TimeSpan.FromMinutes(ClusterFPurgeUpgradeCollisions.SpawnerHoldMinutes)
            );
            Assert.True(otherSpawner.NextSpawn <= TimeSpan.FromMinutes(10));

            // Nothing of ours is left for the new engine to trip on.
            var again = ClusterFPurgeUpgradeCollisions.Run(true, _ => { });
            Assert.Empty(again.Mobiles);
            Assert.Empty(again.Items);
        }
        finally
        {
            foreach (var e in new IEntity[] { owner, wild, tame, bag, unrelatedMobile, unrelatedItem, spawner, otherSpawner })
            {
                e.Delete();
            }
        }
    }
}
