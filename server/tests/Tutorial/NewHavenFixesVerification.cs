// NewHavenFixesVerification.cs
//
// cc-P743: Chase's New Haven notes of 2026-10-30 (shard-migration notes/cc-P743-new-haven-tutorial-fixes.md).
//
// Facts:
//   1. The sparring lesson arms in the Proving Grounds, its echo inside the palisade, and not in the old training yard.
//   2. New Haven's old tutorial farm (addon, door, pen spawner and its animals) is taken away, and nothing places it again.
//   3. The mining lesson arms at the cave on the mountain path, with no practice vein; a signpost marks the path's start.
//   4. The stable horse: seen only by its trainee; not tamable until that trainee bandages it; then tamed it is kept,
//      visible to all, one per character (a replay's horse goes back to its stall); full follower slots are said; the
//      isle's pet never marks Animals done.
//   5. Bard: a successful Provocation makes the echoes hurt each other for about 12 s; the lesson finishes after.
//   6. Rook: an older world's Rook moves to the bench by the Albatross tavern, facing north; the bench is placed.
//   7. Magery arms in the Haven Magery School with a mage echo that shows Magic Arrow until the trainee casts; it fades.
//   8. Thieving: a trap that went off is set again while Remove Trap is to do, in every order of the steps; after Remove
//      Trap it is left alone.
//   9. Housing: three stops, each line one goal ending with the next stop, a signpost beside the first two.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Items;
using Server.LootBoxes;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class NewHavenFixesVerification : IDisposable
{
    private readonly Func<Point3D, bool> _fits = TutorialWingTrials.Fits;

    public NewHavenFixesVerification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        ClusterFTutorial.Enabled = true;
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
        TutorialWingTrials.EnsureHooks();
        TutorialWingTrials.Fits = _ => true;
    }

    public void Dispose() => TutorialWingTrials.Fits = _fits;

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p743{Guid.NewGuid():N}"[..16], "p743-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(Point3D at, bool tutorial = true)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Trainee", RawStr = 60, RawDex = 60, RawInt = 40 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(at, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
            if (tutorial)
            {
                Assert.True(ClusterFTutorial.TryEnter(Pm));
            }
        }

        public TutorialRecord Record => ClusterFTutorial.RecordOf(Pm)!;
        public TutorialWingTrials.Trial Trial => TutorialWingTrials.Current(Pm)!;

        public void Dispose()
        {
            TutorialWingTrials.Cancel(Pm);
            TutorialCoreTrials.Cancel(Pm);
            ClusterFFanfare.FlushAll();
            Pm.NetState = null;
            Ns.Mobile = null;
            Ns.Dispose();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static TutorialWingTrials.Trial Begin(Player p, string key)
    {
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find(key)!);
        var trial = p.Trial;
        Assert.Equal(key, trial.Key);
        Assert.True(trial.Armed);
        return trial;
    }

    private static void Roll(Player p, SkillName skill, bool success = true) =>
        SkillEvents.InvokeSkillUsed(p.Pm, p.Pm.Skills[skill], success);

    private static void Tick() => ShardTestClock.Advance(TimeSpan.FromSeconds(0.5));

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheSparringLessonIsInTheProvingGrounds()
    {
        using var away = new Player(TutorialWingTrials.YardSpot);
        ClusterFTutorial.RunStation(away.Pm, TutorialStations.Find("combat.weapons")!);
        Assert.False(away.Trial.Armed); // the old yard is not its place any more
        TutorialWingTrials.Cancel(away.Pm);

        using var p = new Player(HavenSpots.ProvingGrounds);
        var trial = Begin(p, "combat.weapons");
        TutorialWingTrials.PickWeapon(p.Pm, 0);
        Assert.NotNull(trial.Target);
        Assert.Equal(TutorialWingTrials.TargetOf("combat.weapons"), trial.Target!.Location);
        Assert.Contains("Proving Grounds", TutorialWingTrials.FollowLine("combat.weapons"));
        Assert.Contains("Proving Grounds", TutorialStations.Find("combat.weapons")!.Where);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheOldNewHavenFarmIsTakenAway()
    {
        var map = HavenSpots.Map;
        var testShard = TutorialFarm.IsTestShard;
        TutorialFarm.IsTestShard = () => true;
        TutorialFarm.ClearHaven(false);
        bool refused;
        try
        {
            refused = TutorialFarm.SeedHaven(false, true).Refused; // what an older world may still have
        }
        finally
        {
            TutorialFarm.IsTestShard = testShard;
        }

        Assert.False(refused);
        Assert.NotNull(TutorialFarm.FindAddon<TutorialFarmAddon>(map, TutorialFarmLayout.Anchor));
        Assert.Contains("took away", TutorialFarm.ClearHaven(false));
        Assert.Null(TutorialFarm.FindAddon<TutorialFarmAddon>(map, TutorialFarmLayout.Anchor));
        Assert.Null(TutorialFarm.FindDoor(map, TutorialFarmLayout.DoorSpot));
        Assert.Null(TutorialFarm.FindSpawner(map, TutorialFarm.HavenSpawnerRows[0].Spot, TutorialFarm.HavenSpawnerRows[0].Types[0]));
        Assert.Equal("old New Haven farm: took away 0 thing(s).", TutorialFarm.ClearHaven(false));
        // No lesson points at the old farm any more.
        Assert.All(TutorialWingTrials.Trades, t => Assert.False(t.Place == TutorialWingTrials.FarmField));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void MiningIsAtTheMountainCaveWithASignAtThePathsStart()
    {
        using var p = new Player(HavenSpots.MiningCave);
        var trial = Begin(p, "gathering.mining");
        Assert.Empty(trial.Props);
        Assert.Single(trial.Loaners.OfType<Pickaxe>());
        Assert.Contains("cave", TutorialWingTrials.FollowLine("gathering.mining"));
        Assert.Equal(HavenSpots.MiningCave, TutorialWingTrials.PointAt(trial).Spot);
        var sign = Assert.Single(TutorialWingTrials.HavenPieces, x => x.Name.StartsWith("The mountain path", StringComparison.Ordinal));
        Assert.Equal(0xA854, sign.Id);
        Roll(p, SkillName.Mining);
        Assert.Contains("gathering.mining", p.Record.StationsDone);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheStableHorseIsHealedTamedAndKeptOncePerCharacter()
    {
        TutorialStableHorseStore.Kept.Clear();
        using var p = new Player(HavenSpots.Stables);
        using var other = new Player(new Point3D(HavenSpots.Stables.X + 1, HavenSpots.Stables.Y, 0), false);
        var trial = Begin(p, "animals.taming");
        var horse = trial.Horse!;
        Assert.Equal(HavenSpots.SickStall, horse.Location);
        Assert.True(horse.Hits <= horse.HitsMax / 10);
        Assert.True(p.Pm.CanSee(horse));
        Assert.False(other.Pm.CanSee(horse)); // private until tamed (PlayerMobile-private-mobile.patch)
        Assert.False(horse.Tamable);

        horse.Heal(20, other.Pm); // someone else's bandage does not count
        Assert.False(horse.Tamable);
        horse.Heal(20, p.Pm);
        Assert.True(horse.Tamable);
        Tick();
        Assert.Contains(SkillName.Veterinary.ToString(), trial.Steps);

        horse.SetControlMaster(p.Pm); // what a successful taming does (AnimalTaming.cs)
        Tick();
        Assert.Contains("animals.taming", p.Record.StationsDone);
        Assert.False(horse.Deleted);
        Assert.True(horse.Kept);
        Assert.True(other.Pm.CanSee(horse));
        Assert.True(TutorialStableHorseStore.Has(p.Pm));

        // A replay: the second horse goes back to its stall when the lesson ends.
        TutorialWingTrials.Cancel(p.Pm);
        var again = Begin(p, "animals.taming");
        var second = again.Horse!;
        second.Heal(20, p.Pm);
        second.SetControlMaster(p.Pm);
        Tick();
        Assert.True(second.Deleted);
        Assert.False(horse.Deleted);
        horse.Delete();
    }

    [Fact]
    public void FullFollowerSlotsAreSaidAndTheIslePetNeverFinishesAnimals()
    {
        using var p = new Player(HavenSpots.Stables);
        p.Pm.FollowersMax = 1;
        p.Pm.Followers = 1;
        var lines = new List<string>();
        TutorialWingTrials.Said = (_, line) => lines.Add(line);
        try
        {
            Begin(p, "animals.taming");
        }
        finally
        {
            TutorialWingTrials.Said = null;
        }

        Assert.Contains(TutorialWingTrials.HorseNoSlotLine, lines);
        // The isle's farm lesson (pet, pests and commands) completes core.farm only.
        Assert.DoesNotContain("animals.taming", p.Record.StationsDone);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void ProvokedEchoesFightEachOtherForAWhile()
    {
        using var p = new Player(HavenSpots.BardicGuild);
        var trial = Begin(p, "bard.trial");
        var a = (TutorialTrainingEcho)trial.Creatures[0];
        var b = (TutorialTrainingEcho)trial.Creatures[1];
        Assert.Equal(TutorialWingTrials.BardEchoA, a.Location);
        Roll(p, SkillName.Provocation);
        Assert.True(trial.Brawling);
        for (var i = 0; i < 6; i++)
        {
            Tick();
        }

        // Hits refill at once in the test host, so the blows are counted where they land.
        Assert.True(a.BrawlBlowsTaken > 0 && b.BrawlBlowsTaken > 0, $"blows taken: {a.BrawlBlowsTaken}, {b.BrawlBlowsTaken}");
        for (var i = 0; i < 30 && trial.Brawling; i++)
        {
            Tick();
        }

        Assert.False(trial.Brawling);
        Assert.False(a.Deleted || b.Deleted); // nobody died
        Assert.Equal(a.HitsMax, a.Hits);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void AnOlderWorldsRookMovesToHisBenchByTheTavern()
    {
        var old = new TutorialRecruiter();
        try
        {
            old.MoveToWorld(HavenSpots.OldRook, HavenSpots.Map);
            Assert.True(TutorialWingTrials.MoveRook(false));
            Assert.Equal(HavenSpots.Rook, old.Location);
            Assert.Equal(Direction.North, old.Direction & Direction.Mask);
            Assert.False(TutorialWingTrials.MoveRook(false));
            Assert.Same(old, TutorialRecruiter.Find());
            Assert.Equal(2, TutorialWingTrials.HavenPieces.Count(x => x.Id == 0x0B2C));
            Assert.Equal(HavenSpots.Rook, TutorialWingTrials.HavenPieces.First(x => x.Id == 0x0B2C).At);
        }
        finally
        {
            old.Delete();
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheMageryLessonShowsMagicArrowInTheMagerySchoolUntilTheTraineeCasts()
    {
        using var p = new Player(HavenSpots.MageryHall);
        var trial = Begin(p, "magic.magery");
        Assert.NotNull(trial.Demo);
        var demo = trial.Demo!;
        Assert.Equal(TutorialWingTrials.MageDemoSpot, demo.Location);
        for (var i = 0; i < 30; i++)
        {
            Tick();
        }

        Assert.False(demo.Deleted); // it stays until the trainee has cast
        TutorialWingTrials.OnSkillUsed(p.Pm, p.Pm.Skills[SkillName.Magery], true);
        TutorialWingTrials.OnTargetDamaged(trial.Target!, p.Pm);
        Assert.Contains("magic.magery", p.Record.StationsDone);
        Assert.True(demo.Deleted);
    }

    // ---------------------------------------------------------------- 8

    public static IEnumerable<object[]> ThiefOrders()
    {
        var steps = new[]
        {
            SkillName.DetectHidden, SkillName.RemoveTrap, SkillName.Lockpicking, SkillName.Stealing, SkillName.Snooping,
            SkillName.Begging, SkillName.Poisoning, SkillName.Tracking, SkillName.Hiding, SkillName.Stealth
        };
        yield return [steps];
        yield return [steps.Reverse().ToArray()];
        yield return [new[] { SkillName.Lockpicking, SkillName.Stealing }.Concat(steps.Where(s => s is not (SkillName.Lockpicking or SkillName.Stealing))).ToArray()];
        var rng = new System.Random(743);
        for (var i = 0; i < 4; i++)
        {
            yield return [steps.OrderBy(_ => rng.Next()).ToArray()];
        }
    }

    [Theory]
    [MemberData(nameof(ThiefOrders))]
    public void TheThiefChestAlwaysHasATrapUntilRemoveTrapIsDone(SkillName[] order)
    {
        using var p = new Player(new Point3D(TutorialWingTrials.TradesYard.X, TutorialWingTrials.TradesYard.Y, 0));
        var trial = Begin(p, "thieving.trial");
        var chest = trial.Props.OfType<WoodenChest>().Single();
        foreach (var skill in order)
        {
            chest.TrapType = TrapType.None; // the trap went off (opening the chest fires it, TrappableContainer.ExecuteTrap)
            Tick();
            if (!trial.Steps.Contains(SkillName.RemoveTrap.ToString()))
            {
                Assert.Equal(TrapType.DartTrap, chest.TrapType);
            }

            Roll(p, skill);
        }

        Assert.Contains("thieving.trial", p.Record.StationsDone);
        Assert.True(trial.ChestResets >= 1);
    }

    // ---------------------------------------------------------------- 6b

    [Fact]
    public void ASeededPieceAtAnOldSpotIsMovedAndBothBenchHalvesStay()
    {
        var map = HavenSpots.Map;
        var sign = TutorialWingTrials.HavenPieces.First(x => x.Name.StartsWith("Housing lesson, stop 2", StringComparison.Ordinal));
        var old = new Static(sign.Id) { Name = sign.Name };
        old.MoveToWorld(new Point3D(3703, 2599, 0), map); // where an earlier run of this prompt put it
        try
        {
            TutorialWingTrials.SeedHaven(false);
            Assert.True(old.Deleted);
            var here = new List<Item>();
            foreach (var item in map.GetItemsAt(sign.At.X, sign.At.Y)) { here.Add(item); }
            Assert.Contains(here, i => i.ItemID == sign.Id && i.Name == sign.Name);
            foreach (var bench in TutorialWingTrials.HavenPieces.Where(x => x.Id == 0x0B2C))
            {
                var at = new List<Item>();
                foreach (var item in map.GetItemsAt(bench.At.X, bench.At.Y)) { at.Add(item); }
                Assert.Single(at, i => i.ItemID == 0x0B2C);
            }

            Assert.Contains("placed 0 pieces (took away 0", TutorialWingTrials.SeedHaven(false));
        }
        finally
        {
            foreach (var piece in TutorialWingTrials.HavenPieces)
            {
                var gone = new List<Item>();
                foreach (var item in map.GetItemsAt(piece.At.X, piece.At.Y))
                {
                    if (item.Name == piece.Name) { gone.Add(item); }
                }

                foreach (var item in gone) { item.Delete(); }
            }

            old.Delete();
        }
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void TheHousingLessonHasThreeClearStopsWithSigns()
    {
        var t = TutorialWingTrials.Trade("housing.basics")!;
        Assert.Equal(3, t.Stops!.Length);
        Assert.Contains("Stop 1 of 3", t.Stops[0].Text);
        Assert.Contains("Next: follow the arrow to stop 2", t.Stops[0].Text);
        Assert.Contains("Next: follow the arrow to stop 3", t.Stops[1].Text);
        Assert.Contains("end of the housing lesson", t.Stops[2].Text);
        for (var i = 0; i < 2; i++)
        {
            var stop = t.Stops[i].Spot();
            Assert.Contains(TutorialWingTrials.HavenPieces, x => x.Id == 0xA854 && x.Name.StartsWith($"Housing lesson, stop {i + 1}", StringComparison.Ordinal) &&
                Math.Abs(x.At.X - stop.X) <= 4 && Math.Abs(x.At.Y - stop.Y) <= 4);
        }
    }
}
