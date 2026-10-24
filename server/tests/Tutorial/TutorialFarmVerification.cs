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

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Items;
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
        TutorialFarm.EnsureHooks(); // Initialize never runs in the test host
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

            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Ns.Dispose();
            Accounts.Remove(Account);
        }
    }

    private static void CleanFarm()
    {
        var map = Map.Trammel;
        TutorialFarm.FindAddon(map)?.Delete();
        TutorialFarm.FindDoor(map)?.Delete();
        TutorialFarmhand.Find()?.Delete();
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
        CleanFarm();

        try
        {
            TutorialFarm.IsTestShard = () => false;
            TutorialFarm.MapPatched = () => true;
            var live = TutorialFarm.Seed(false);
            Assert.True(live.Refused);
            Assert.Contains("not the test shard", live.Message);
            Assert.Null(TutorialFarm.FindAddon(Map.Trammel));

            TutorialFarm.IsTestShard = () => true;
            TutorialFarm.MapPatched = () => false;
            var unpatched = TutorialFarm.Seed(false);
            Assert.True(unpatched.Refused);
            Assert.Contains("two trees", unpatched.Message);
            Assert.Null(TutorialFarm.FindAddon(Map.Trammel));

            TutorialFarm.MapPatched = () => true;
            var dry = TutorialFarm.Seed(true);
            Assert.Equal((false, 1, 1, 1, 2, 0), (dry.Refused, dry.Addons, dry.Doors, dry.Farmhands, dry.Spawners, dry.Present));
            Assert.Null(TutorialFarm.FindAddon(Map.Trammel));

            var first = TutorialFarm.Seed(false);
            Assert.Equal((false, 1, 1, 1, 2, 0), (first.Refused, first.Addons, first.Doors, first.Farmhands, first.Spawners, first.Present));

            var addon = TutorialFarm.FindAddon(Map.Trammel);
            Assert.NotNull(addon);
            Assert.Equal(TutorialFarmLayout.Anchor, addon!.Location);
            Assert.Equal(TutorialFarmLayout.Pieces.Length, addon.Components.Count);
            var placed = addon.Components.Select(c => (c.ItemID, c.X, c.Y, c.Z)).OrderBy(t => t).ToArray();
            var wanted = TutorialFarmLayout.Pieces.Select(p => (p.Id, p.X, p.Y, p.Z)).OrderBy(t => t).ToArray();
            Assert.Equal(wanted, placed);

            var door = TutorialFarm.FindDoor(Map.Trammel);
            Assert.IsType<DarkWoodDoor>(door);
            Assert.Equal(0x06AD, door!.ItemID);
            Assert.Equal(TutorialFarmLayout.DoorSpot, door.Location);

            var hand = TutorialFarmhand.Find();
            Assert.NotNull(hand);
            Assert.Equal((TutorialFarm.FarmhandSpot.X, TutorialFarm.FarmhandSpot.Y), (hand!.X, hand.Y));

            foreach (var (spot, range, count, types) in TutorialFarm.SpawnerRows)
            {
                var s = TutorialFarm.FindSpawner(Map.Trammel, spot, types[0]);
                Assert.NotNull(s);
                Assert.Equal((count, range), (s!.Count, s.HomeRange));
                Assert.Equal(types, s.Entries.Select(e => e.SpawnedName).ToArray());
            }

            var again = TutorialFarm.Seed(false);
            Assert.Equal((0, 0, 0, 0, 5), (again.Addons, again.Doors, again.Farmhands, again.Spawners, again.Present));
        }
        finally
        {
            TutorialFarm.IsTestShard = testShard;
            TutorialFarm.MapPatched = patched;
            CleanFarm();
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
            Assert.Contains(TutorialFarm.PetKey, p.Record.StationsDone);
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

            // 3. Bandages, then a heal that worked.
            Assert.Equal(3, TutorialFarm.Talk(p.Pm, hand));
            Assert.NotNull(p.Pm.Backpack.FindItemByType<Bandage>());
            SkillEvents.InvokeSkillUsed(p.Pm, p.Pm.Skills.Healing, false);
            Assert.Equal(3, TutorialFarm.StepOf(p.Pm));
            SkillEvents.InvokeSkillUsed(p.Pm, p.Pm.Skills.Veterinary, true);
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
        farmed.Record.StationsDone.Add(TutorialFarm.PetKey);
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
            new[] { new[] { 217 }, new[] { 739 }, new[] { 1547, 1548, 1549, 1546, 1552, 1551 }, new[] { 277 }, new[] { 1410 } },
            Bodies(EvoPetData.Dog));
        Assert.Equal(
            new[] { new[] { 201 }, new[] { 1309 }, new[] { 1254, 1426 }, new[] { 1416 }, new[] { 1573 } },
            Bodies(EvoPetData.Cat));
        Assert.All(EvoPetData.Dog.Stages.Concat(EvoPetData.Cat.Stages), s => Assert.False(s.ArtPending));
        Assert.Equal(new[] { 0, 0, 0, 1, 5 }, EvoPetData.Dog.Stages.Select(s => s.MinOwnerLoops));
        Assert.Equal(new[] { 0, 0, 0, 1, 5 }, EvoPetData.Cat.Stages.Select(s => s.MinOwnerLoops));
    }
}
