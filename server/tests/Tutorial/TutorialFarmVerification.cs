// cc-P276 (F-35): the New Haven tutorial farm (customizations/Tutorial/TutorialFarm.cs, TutorialFarmLayout.cs). Notes:
// shard-migration notes/cc-P276-tutorial-farm-build.md.
//
// Facts:
//   1. The seeder refuses on a shard that is not the test shard, and while the map still has the two trees; a dry run
//      places nothing; the first run places the farm addon (every piece of the approved layout at its tile and Z), the
//      dark wood door (0x06AD) in the east wall, the Farmhand at the gate and the two spawners; a second run places
//      nothing.
//   2. The layout: 330 pieces, all inside the farm's footprint, none on the two cleared tree tiles but the crops the
//      approved mockup put there, the porch steps the Light Wood set's east stair (stairs.txt), and the four removed
//      statics exactly the map patch's.
//   3. The Farmhand's steps: a stranger is turned away; a participant picks a dog (bonded, following, the farm.pet key);
//      a second pick is refused; three pests the pet damaged clear the field (farm.pests, the animals.taming station);
//      the Farmhand hands bandages and a successful Healing use finishes it (farm.heal, the combat.healing station).
//   4. Graduation offers no second pet to an account that took its pet at the farm, and still offers one otherwise.
//   5. The evo dog and cat stages are exactly Chase's picks (F-35, 2026-10-05).
// cc-P453 (the P420/P421 fix-up, notes/cc-P453-tutorial-fixups.md):
//   6. A farm pet, then skip and remedial: no second pet from the Farmhand or at graduation, and the claim survives a
//      save and load.
//   7. A closed companion pick waits on the record through a save and load; Rook offers it again; one claim only.
//   8. Bandages moved out of the pack and asked for again: a new stack only up to the cap.
//   9. A failed treatment whose skill roll passed leaves step 3; a bandage that really heals completes it.
//  10. An outsider cannot make a pest their combatant, so never swings or rolls a skill at it.
//  11. Reseeding with the farmhouse door open places no second door.
//  12. The pen and the pests are replaced after a death and a taming.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.Spawners;
using Server.Gumps;
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
public class TutorialFarmVerification
{
    public TutorialFarmVerification()
    {
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ClusterFTutorial.Enabled = true;
        FoldMaps.Configure(); // cc-P663: the farm is on the isle (map 32)
        TutorialFarm.EnsureHooks(); // Initialize never runs in the test host
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p276{Guid.NewGuid():N}"[..16], "p276-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<IEntity> Litter = new();

        public Player(bool participant)
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Trainee", RawStr = 60, RawDex = 60, RawInt = 60 };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(3395, 2614, 0), Map.Trammel);
            Pm.Hits = Pm.HitsMax;
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;

            if (participant)
            {
                Assert.True(ClusterFTutorial.TryEnter(Pm));
                TutorialTestStart.ToTheFarm(Pm); // cc-P663: the farm is core.farm, after chat
            }
        }

        public TutorialRecord Record => ClusterFTutorial.RecordOf(Pm)!;

        public void Dispose()
        {
            foreach (var e in Litter)
            {
                e.Delete();
            }

            foreach (var m in Pm.AllFollowers?.ToList() ?? new List<Mobile>())
            {
                m.Delete();
            }

            ClusterFFanfare.FlushAll();
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Ns.Dispose();
            Accounts.Remove(Account);
        }
    }

    // A bandage's end (pinned BandageContext.OnTick), run now. Many slips make the treatment fail while the skill rolls
    // still run; none, with the skill at 120, always heals.
    private sealed class Bandaging : BandageContext
    {
        public Bandaging(Mobile healer, Mobile patient, int slips) : base(healer, patient, TimeSpan.Zero) => Slips = slips;

        public void Finish() => OnTick();
    }

    private static TutorialRecord RoundTrip(Account account)
    {
        var w = new BufferWriter(true);
        ClusterFAccountPersistence.GetOrCreate(account).Serialize(w);
        return new ClusterFAccountData(new BufferReader(w.Buffer)).Tutorial;
    }

    private static List<BaseDoor> DoorsNearTheFarmhouse()
    {
        var doors = new List<BaseDoor>();
        foreach (var d in TutorialWorld.Isle.GetItemsInRange<BaseDoor>(TutorialIsleFarmLayout.DoorSpot, 3))
        {
            doors.Add(d);
        }

        return doors;
    }

    private static void SeedFarm()
    {
        TutorialFarm.IsTestShard = () => true;
        TutorialFarm.MapPatched = () => true;
        Assert.False(TutorialFarm.Seed(false).Refused);
    }

    private static void CleanFarm()
    {
        var map = TutorialWorld.Isle;
        TutorialFarm.FindAddon(map)?.Delete();
        TutorialFarm.FindDoor(map)?.Delete();
        TutorialFarmhand.Find(map)?.Delete();
        foreach (var (spot, _, _, types) in TutorialFarm.SpawnerRows)
        {
            var s = TutorialFarm.FindSpawner(map, spot, types[0]);
            s?.RemoveSpawns();
            s?.Delete();
        }
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheSeederRefusesOnLiveAndUnpatchedAndPlacesTheFarmOnce()
    {
        var testShard = TutorialFarm.IsTestShard;
        var patched = TutorialFarm.MapPatched;
        var isle = TutorialWorld.Isle;
        CleanFarm();

        try
        {
            TutorialFarm.IsTestShard = () => false;
            TutorialFarm.MapPatched = () => true;
            var live = TutorialFarm.Seed(false);
            Assert.True(live.Refused);
            Assert.Contains("not the test shard", live.Message);
            Assert.Null(TutorialFarm.FindAddon(isle));

            // cc-P663: the two trees are New Haven's; only its prop seeding (SeedHaven) waits for that patch.
            TutorialFarm.IsTestShard = () => true;
            TutorialFarm.MapPatched = () => false;
            var unpatched = TutorialFarm.SeedHaven(false);
            Assert.True(unpatched.Refused);
            Assert.Contains("two trees", unpatched.Message);
            Assert.Null(TutorialFarm.FindAddon<TutorialFarmAddon>(Map.Trammel, TutorialFarmLayout.Anchor));

            TutorialFarm.MapPatched = () => true;
            var dry = TutorialFarm.Seed(true);
            Assert.Equal((false, 1, 1, 1, 2, 0), (dry.Refused, dry.Addons, dry.Doors, dry.Farmhands, dry.Spawners, dry.Present));
            Assert.Null(TutorialFarm.FindAddon(isle));

            var first = TutorialFarm.Seed(false);
            Assert.Equal((false, 1, 1, 1, 2, 0), (first.Refused, first.Addons, first.Doors, first.Farmhands, first.Spawners, first.Present));

            // cc-P663: the isle's farm: the same 330 pieces moved by (+3314, +1364), each at its height above the old ground.
            var addon = TutorialFarm.FindAddon(isle);
            Assert.NotNull(addon);
            Assert.Same(isle, addon!.Map);
            Assert.Equal(TutorialIsleFarmLayout.Anchor, addon.Location);
            Assert.Equal(TutorialFarmLayout.Pieces.Length, addon.Components.Count);
            var placed = addon.Components.Select(c => (c.ItemID, c.X, c.Y, c.Z)).OrderBy(t => t).ToArray();
            var wanted = TutorialIsleFarmLayout.Pieces.Select(p => (p.Id, p.X, p.Y, p.Z)).OrderBy(t => t).ToArray();
            Assert.Equal(wanted, placed);
            var moved = TutorialFarmLayout.Pieces.Select(p => (p.Id, p.X + 3314, p.Y + 1364)).OrderBy(t => t).ToArray();
            Assert.Equal(moved, TutorialIsleFarmLayout.Pieces.Select(p => (p.Id, p.X, p.Y)).OrderBy(t => t).ToArray());

            var door = TutorialFarm.FindDoor(isle);
            Assert.IsType<DarkWoodDoor>(door);
            Assert.Equal(0x06AD, door!.ItemID);
            Assert.Equal(TutorialIsleFarmLayout.DoorSpot, door.Location);

            var hand = TutorialFarmhand.Find(isle);
            Assert.NotNull(hand);
            Assert.Equal((TutorialFarm.FarmhandSpot.X, TutorialFarm.FarmhandSpot.Y), (hand!.X, hand.Y));

            foreach (var (spot, range, count, types) in TutorialFarm.SpawnerRows)
            {
                var s = TutorialFarm.FindSpawner(isle, spot, types[0]);
                Assert.NotNull(s);
                Assert.Equal((count, range), (s!.Count, s.HomeRange));
                Assert.Equal(types, s.Entries.Select(e => e.SpawnedName).ToArray());
            }

            var again = TutorialFarm.Seed(false);
            Assert.Equal((0, 0, 0, 0, 5), (again.Addons, again.Doors, again.Farmhands, again.Spawners, again.Present));

            // New Haven keeps the farmhouse, door and pen for the wing trials, with no Wren and no pests.
            var haven = TutorialFarm.SeedHaven(false);
            Assert.Equal((false, 1, 1, 0, 1), (haven.Refused, haven.Addons, haven.Doors, haven.Farmhands, haven.Spawners));
            Assert.Null(TutorialFarm.FindSpawner(Map.Trammel, new Point2D(3388, 2614), TutorialFarm.PestTypes[0]));
            Assert.Null(TutorialFarmhand.Find(Map.Trammel));
        }
        finally
        {
            TutorialFarm.IsTestShard = testShard;
            TutorialFarm.MapPatched = patched;
            CleanFarm();
            TutorialFarm.FindAddon<TutorialFarmAddon>(Map.Trammel, TutorialFarmLayout.Anchor)?.Delete();
            TutorialFarm.FindDoor(Map.Trammel, TutorialFarmLayout.DoorSpot)?.Delete();
            var pen = TutorialFarm.FindSpawner(Map.Trammel, TutorialFarm.HavenPenSpot, TutorialFarm.PenTypes[0]);
            pen?.RemoveSpawns();
            pen?.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheLayoutIsTheApprovedFarmAndTheMapEditIsThePatchs()
    {
        var pieces = TutorialFarmLayout.Pieces;
        Assert.Equal(330, pieces.Length);
        Assert.All(pieces, p => Assert.True(p.X is >= 3370 and <= 3401 && p.Y is >= 2603 and <= 2619, $"{p} outside the farm"));

        // The farmhouse: walls x 3374-3380, y 2608-2614 round the anchor, the east porch's steps the Light Wood east stair.
        Assert.Equal(new Point3D(3377, 2611, 15), TutorialFarmLayout.Anchor);
        var steps = pieces.Where(p => p.Id is >= 0x0722 and <= 0x0735).ToArray();
        Assert.Equal(3, steps.Length);
        Assert.All(steps, p => Assert.Equal(0x0725, p.Id)); // stairs.txt: Light Wood, East 1829
        Assert.DoesNotContain(pieces, p => p.Id == 0x06AD);   // the door is a real door

        // The two cleared trees: the patch's four statics exactly, and no fence or building on their tiles.
        Assert.Equal(
            new[] { (0x0CE7, 3388, 2607, 19), (0x0CE6, 3388, 2607, 19), (0x0CE1, 3396, 2616, 38), (0x0CE0, 3396, 2616, 38) },
            TutorialFarmLayout.RemovedTrees);
        Assert.DoesNotContain(pieces, p => (p.X, p.Y) is (3388, 2607) or (3396, 2616) && p.Id is >= 0x0835 and <= 0x0837);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheFarmhandRunsPetPestsAndHealing()
    {
        var hand = new TutorialFarmhand();
        hand.MoveToWorld(new Point3D(3396, 2614, 0), Map.Trammel);
        using var stranger = new Player(false);
        using var p = new Player(true);
        p.Litter.Add(hand);

        try
        {
            Assert.Equal(0, TutorialFarm.Talk(stranger.Pm, hand));
            Assert.False(stranger.Pm.HasGump<TutorialFarmPetGump>());

            // 1. The pet.
            Assert.Equal(1, TutorialFarm.Talk(p.Pm, hand));
            Assert.True(p.Pm.HasGump<TutorialFarmPetGump>());
            Assert.Null(TutorialFarm.ChoosePet(p.Pm, "fox")); // the farm offers a dog or a cat
            var pet = TutorialFarm.ChoosePet(p.Pm, "dog");
            Assert.NotNull(pet);
            Assert.Equal("dog", pet!.Line.Key);
            Assert.True(pet.IsBonded && pet.Controlled && pet.ControlMaster == p.Pm);
            Assert.True(p.Record.PetClaimed);
            Assert.Null(TutorialFarm.ChoosePet(p.Pm, "cat")); // one pet
            Assert.Equal(2, TutorialFarm.StepOf(p.Pm));

            // 2. Three pests the pet damaged; a pest nobody of theirs touched does not count.
            var untouched = new FarmRat();
            untouched.MoveToWorld(new Point3D(3388, 2614, 0), Map.Trammel);
            untouched.Kill();
            p.Litter.Add(untouched);
            Assert.Equal(0, TutorialFarm.PestsKilled(p.Pm));

            foreach (var pest in new FarmPest[] { new FarmRat(), new FieldRat(), new GrassSnake() })
            {
                pest.MoveToWorld(new Point3D(3388, 2614, 0), Map.Trammel);
                p.Litter.Add(pest);
                pest.Damage(1, pet);
                Assert.Contains(pest.DamageEntries.ToArray(), e => e.Damager == pet);
                pest.Kill();
            }

            Assert.Contains(TutorialFarm.PestsKey, p.Record.StationsDone);
            Assert.Contains(TutorialFarm.TamingStation, p.Record.StationsDone);
            Assert.Equal(3, TutorialFarm.StepOf(p.Pm));

            // 3. Bandages, then a heal that worked (on the pet: Veterinary).
            Assert.Equal(3, TutorialFarm.Talk(p.Pm, hand));
            Assert.NotNull(p.Pm.Backpack.FindItemByType<Bandage>());
            pet.MoveToWorld(p.Pm.Location, p.Pm.Map);
            pet.Hits = pet.HitsMax / 2;
            p.Pm.Skills.Veterinary.Cap = 120.0;
            p.Pm.Skills.Veterinary.Base = 120.0;
            new Bandaging(p.Pm, pet, 0).Finish();
            Assert.Contains(TutorialFarm.HealKey, p.Record.StationsDone);
            Assert.Contains(TutorialFarm.HealingStation, p.Record.StationsDone);
            Assert.Equal(4, TutorialFarm.Talk(p.Pm, hand));

            // The station has a handler (cc-P291: the animals trial; the Farmhand's lesson still completes it too).
            TutorialWingTrials.EnsureHooks(); // cc-P291: the station is the animals trial at the pen now
            Assert.True(TutorialStations.Handlers.ContainsKey(TutorialFarm.TamingStation));
        }
        finally
        {
            hand.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void GraduationOffersNoSecondPetAfterTheFarm()
    {
        using var farmed = new Player(true);
        farmed.Record.PetClaimed = true;
        ClusterFTutorial.GraduationRewards(farmed.Pm);
        Assert.False(farmed.Pm.HasGump<TutorialPetPickGump>());

        using var other = new Player(true);
        ClusterFTutorial.GraduationRewards(other.Pm);
        Assert.True(other.Pm.HasGump<TutorialPetPickGump>());

        foreach (var pm in new[] { farmed.Pm, other.Pm })
        {
            foreach (var box in pm.Backpack.Items.ToList())
            {
                box.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheEvoDogAndCatStagesAreChasesPicks()
    {
        int[][] Bodies(EvoLine line) => line.Stages.Select(s => s.Bodies).ToArray();

        Assert.Equal(
            new[] { new[] { 217 }, new[] { 739 }, new[] { 1547, 1548, 1549, 1546, 1552, 1551 }, new[] { 277 }, new[] { 2133, 1410 } },
            Bodies(EvoPetData.Dog)); // cc-P365: the KR Wolf joins stage 5, the windrunner kept as the pick
        Assert.Equal(
            new[] { new[] { 201 }, new[] { 1309 }, new[] { 1254, 1426 }, new[] { 1416 }, new[] { 1573 } },
            Bodies(EvoPetData.Cat));
        Assert.All(EvoPetData.Dog.Stages.Concat(EvoPetData.Cat.Stages), s => Assert.False(s.ArtPending));
        Assert.Equal(new[] { 0, 0, 0, 1, 5 }, EvoPetData.Dog.Stages.Select(s => s.MinOwnerLoops));
        Assert.Equal(new[] { 0, 0, 0, 1, 5 }, EvoPetData.Cat.Stages.Select(s => s.MinOwnerLoops));
    }

    // ---------------------------------------------------------------- 6 (cc-P453, P420 #3 = P421 #2)

    [Fact]
    public void AFarmPetThenSkipAndRemedialGivesNoSecondPetAcrossASaveAndLoad()
    {
        var counts = ClusterFTutorial.SkipCountsAsGraduation;
        using var p = new Player(true);
        try
        {
            ClusterFTutorial.SkipCountsAsGraduation = true;
            Assert.NotNull(TutorialFarm.ChoosePet(p.Pm, "dog"));
            Assert.True(RoundTrip(p.Account).PetClaimed); // the claim is saved

            Assert.True(ClusterFTutorial.Skip(p.Pm));
            Assert.True(ClusterFTutorial.StartRemedial(p.Pm));
            Assert.True(RoundTrip(p.Account).PetClaimed); // remedial training clears the stations, not the claim

            Assert.Equal(2, TutorialFarm.StepOf(p.Pm)); // straight to the pests, with the dog they have
            Assert.Null(TutorialFarm.ChoosePet(p.Pm, "cat"));

            foreach (var station in TutorialStations.Core)
            {
                ClusterFTutorial.CompleteStation(p.Pm, station.Key);
            }

            Assert.Null(ClusterFTutorial.Graduate(p.Pm));
            Assert.False(p.Pm.HasGump<TutorialPetPickGump>());
            Assert.False(ClusterFTutorial.CompanionPending(p.Record));
            Assert.Null(ClusterFTutorial.ChooseCompanion(p.Pm, "fox"));
            Assert.Single(p.Pm.AllFollowers);
        }
        finally
        {
            ClusterFTutorial.SkipCountsAsGraduation = counts;
            foreach (var item in p.Pm.Backpack.Items.ToList())
            {
                item.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- 7 (P420 #2)

    [Fact]
    public void AClosedCompanionPickWaitsOnTheRecordAndIsClaimedOnce()
    {
        using var p = new Player(true);
        try
        {
            ClusterFTutorial.GraduationRewards(p.Pm);
            var pick = p.Pm.FindGump<TutorialPetPickGump>();
            Assert.NotNull(pick);
            pick!.OnResponse(p.Ns, new RelayInfo(0, default, default, default, default)); // closed without choosing
            p.Pm.CloseGump<TutorialPetPickGump>();

            var saved = RoundTrip(p.Account); // a restart keeps it
            Assert.True(saved.PetClaimPending);
            Assert.False(saved.PetClaimed);

            Assert.True(ClusterFTutorial.OfferCompanion(p.Pm)); // Rook offers it again
            Assert.True(p.Pm.HasGump<TutorialPetPickGump>());
            Assert.NotNull(ClusterFTutorial.ChooseCompanion(p.Pm, "fox"));
            Assert.Null(ClusterFTutorial.ChooseCompanion(p.Pm, "bird")); // one claim
            Assert.False(ClusterFTutorial.OfferCompanion(p.Pm));
            Assert.True(p.Record.PetClaimed);
            Assert.False(p.Record.PetClaimPending);
            Assert.Single(p.Pm.AllFollowers);

            ClusterFTutorial.GraduationRewards(p.Pm); // another graduation box never reopens it
            Assert.False(p.Record.PetClaimPending);
            Assert.False(RoundTrip(p.Account).PetClaimPending);
        }
        finally
        {
            foreach (var item in p.Pm.Backpack.Items.ToList())
            {
                item.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- 8 (P420 #4 = P421 #1)

    [Fact]
    public void BandagesMovedOutOfThePackAreReissuedOnlyUpToTheCap()
    {
        var hand = new TutorialFarmhand();
        hand.MoveToWorld(new Point3D(3396, 2614, 0), Map.Trammel);
        using var p = new Player(true);
        p.Litter.Add(hand);
        p.Record.PetClaimed = true;
        p.Record.StationsDone.Add(TutorialFarm.PestsKey);
        Assert.Equal(3, TutorialFarm.StepOf(p.Pm));

        var banked = 0;
        for (var i = 0; i < TutorialFarm.BandageIssues + 2; i++)
        {
            Assert.Equal(3, TutorialFarm.Talk(p.Pm, hand));
            Assert.Equal(3, TutorialFarm.Talk(p.Pm, hand)); // the pack has some: no second stack
            if (p.Pm.Backpack.FindItemByType<Bandage>() is { } stack)
            {
                p.Pm.BankBox.DropItem(stack); // banked (or traded, or sold) and asked again
                banked++;
            }
        }

        Assert.Equal(TutorialFarm.BandageIssues, banked);
        Assert.Null(p.Pm.Backpack.FindItemByType<Bandage>());
        Assert.Equal(TutorialFarm.BandageIssues, p.Record.BandagesIssued);
        Assert.Equal(TutorialFarm.BandageIssues, RoundTrip(p.Account).BandagesIssued);
        p.Pm.BankBox.Delete();
    }

    // ---------------------------------------------------------------- 9 (P421 #4)

    [Fact]
    public void AFailedTreatmentLeavesTheHealStepAndARealHealCompletesIt()
    {
        using var p = new Player(true);
        p.Record.PetClaimed = true;
        p.Record.StationsDone.Add(TutorialFarm.PestsKey);
        p.Pm.Skills.Healing.Cap = 120.0;
        p.Pm.Skills.Healing.Base = 120.0; // the skill roll always passes

        var rolled = new List<bool>();
        void Record(Mobile from, Skill skill, bool success)
        {
            if (from == p.Pm && skill.SkillName == SkillName.Healing)
            {
                rolled.Add(success);
            }
        }

        SkillEvents.SkillUsed += Record;
        try
        {
            p.Pm.Hits = p.Pm.HitsMax - 20;
            var hits = p.Pm.Hits;
            new Bandaging(p.Pm, p.Pm, 100).Finish(); // so many slips the treatment fails
            Assert.Equal(hits, p.Pm.Hits);
            Assert.Equal(new[] { true }, rolled);    // and still the Healing roll passed
            Assert.Equal(3, TutorialFarm.StepOf(p.Pm));
            Assert.DoesNotContain(TutorialFarm.HealKey, p.Record.StationsDone);

            p.Pm.Hits = p.Pm.HitsMax - 20;
            new Bandaging(p.Pm, p.Pm, 0).Finish();
            Assert.True(p.Pm.Hits > p.Pm.HitsMax - 20);
            Assert.Contains(TutorialFarm.HealKey, p.Record.StationsDone);
            Assert.Equal(4, TutorialFarm.StepOf(p.Pm));
        }
        finally
        {
            SkillEvents.SkillUsed -= Record;
        }
    }

    // ---------------------------------------------------------------- 10 (P421 #6)

    [Fact]
    public void AnOutsidersSwingAtAPestRunsNoSkillCheck()
    {
        using var outsider = new Player(false);
        using var trainee = new Player(true);
        var pest = new FarmRat();
        pest.MoveToWorld(new Point3D(outsider.Pm.X + 1, outsider.Pm.Y, outsider.Pm.Z), Map.Trammel);
        outsider.Litter.Add(pest);

        var rolls = new List<SkillName>();
        void Record(Mobile from, Skill skill, bool success)
        {
            if (from == outsider.Pm)
            {
                rolls.Add(skill.SkillName);
            }
        }

        // The test host leaves the harmful handler unset (every region then allows); the outsider patch sits in the real
        // one, so this fact installs it and puts the host's back after.
        var handler = Mobile.AllowHarmfulHandler;
        Mobile.AllowHarmfulHandler = NotorietyHandlers.Mobile_AllowHarmful;
        SkillEvents.SkillUsed += Record;
        try
        {
            Assert.False(outsider.Pm.CanBeHarmful(pest, false));
            Assert.True(trainee.Pm.CanBeHarmful(pest, false));

            outsider.Pm.Warmode = true;
            outsider.Pm.Combatant = pest;
            ShardTestClock.Advance(TimeSpan.FromSeconds(6));

            Assert.Null(outsider.Pm.Combatant);
            Assert.Empty(rolls);
            Assert.Equal(pest.HitsMax, pest.Hits);
        }
        finally
        {
            SkillEvents.SkillUsed -= Record;
            outsider.Pm.Combatant = null;
            Mobile.AllowHarmfulHandler = handler;
        }
    }

    // ---------------------------------------------------------------- 11 (P421 #3)

    [Fact]
    public void ReseedingWithTheDoorOpenPlacesNoSecondDoor()
    {
        var testShard = TutorialFarm.IsTestShard;
        var patched = TutorialFarm.MapPatched;
        CleanFarm();

        try
        {
            SeedFarm();
            var door = TutorialFarm.FindDoor(TutorialWorld.Isle)!;
            door.Open = true;
            Assert.NotEqual(TutorialIsleFarmLayout.DoorSpot, door.Location); // an open door stands aside

            var again = TutorialFarm.Seed(false);
            Assert.Equal((0, 5), (again.Doors, again.Present));
            Assert.Single(DoorsNearTheFarmhouse());
            Assert.Same(door, TutorialFarm.FindDoor(TutorialWorld.Isle));
        }
        finally
        {
            foreach (var d in DoorsNearTheFarmhouse())
            {
                d.Delete();
            }

            CleanFarm();
            TutorialFarm.IsTestShard = testShard;
            TutorialFarm.MapPatched = patched;
        }
    }

    // ---------------------------------------------------------------- 12 (P421 #8)

    [Fact]
    public void ThePenAndThePestsAreReplacedAfterADeathAndATaming()
    {
        var testShard = TutorialFarm.IsTestShard;
        var patched = TutorialFarm.MapPatched;
        CleanFarm();
        using var p = new Player(false);

        try
        {
            SeedFarm();
            var pests = TutorialFarm.FindSpawner(TutorialWorld.Isle, TutorialFarm.PestSpot, TutorialFarm.PestTypes[0])!;
            var pen = TutorialFarm.FindSpawner(TutorialWorld.Isle, TutorialFarm.PenSpot, TutorialFarm.PenTypes[0])!;
            Assert.True(pests.Running && pen.Running);
            Assert.Equal(TutorialFarm.PestCount, pests.Spawned.Count);
            Assert.Equal(TutorialFarm.PenCount, pen.Spawned.Count);

            var dead = (BaseCreature)pests.Spawned.Keys.First();
            dead.Kill();
            dead.Corpse?.Delete();
            var tamed = (BaseCreature)pen.Spawned.Keys.First();
            Assert.True(tamed.SetControlMaster(p.Pm));
            p.Litter.Add(tamed);

            ShardTestClock.Advance(TimeSpan.FromMinutes(4)); // past the spawners' longest delay (3 minutes)
            pests.Defrag();
            pen.Defrag();

            Assert.Equal(TutorialFarm.PestCount, pests.Spawned.Count);
            Assert.Equal(TutorialFarm.PenCount, pen.Spawned.Count);
            Assert.DoesNotContain(dead, pests.Spawned.Keys);
            Assert.DoesNotContain(tamed, pen.Spawned.Keys);
        }
        finally
        {
            CleanFarm();
            TutorialFarm.IsTestShard = testShard;
            TutorialFarm.MapPatched = patched;
        }
    }
}
