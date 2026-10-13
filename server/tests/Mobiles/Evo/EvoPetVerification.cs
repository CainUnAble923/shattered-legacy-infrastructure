// EvoPetVerification.cs
//
// cc-P65, F-35: evo pets, the data-driven evolution framework. Notes in shard-migration notes/cc-P65-evo-pets-framework.md.
//
// Facts:
//   1. The data: four lines, live today Dog 5, Cat 5, Fox 2, Bird 3; art-pending rows flagged; every stage a body of
//      its own (no recolors); gates on the second-to-last and last stages; one stat curve, stage 1 a town dog.
//   2. Every line constructs at stage 1 on its body with the curve's stats and 1 follower slot, and round-trips with
//      its line, stage, XP, hue choice, picks, Bond Trial flag and test loops.
//   3. XP comes only from kills it dealt damage in, by its share of the damage.
//   4. A dog walks its line: XP-only step-ups, the owner's pick (once), the loop gate, then loop 5 plus the Bond Trial.
//   5. Art-pending stages are skipped (gates of the stage entered still apply); with nothing enabled above, it is at its
//      top for now, and says so.
//   6. The owner picks a hue once from the list; it carries through every stage.
//   7. The owner's reset (ClusterFDevTools.ExecuteReset, the stand-in for F-12's loop reset) leaves the pet's stage, XP,
//      pick, hue, owner and bond as they were.
//   8. Every unused body we use is typed Animal, so a pet on one cannot open doors.
//   9. The UOP-era bodies get their actions remapped; the classic bodies are left alone.
//  10. GivePet hands a player a stage-1 pet, tamed and bonded, at their feet; with no free slot it gives nothing.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class EvoPetVerification
{
    private readonly ITestOutputHelper _out;

    public EvoPetVerification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D Spot = new(3460, 2604, 18);

    private static T RoundTrip<T>(T original) where T : Mobile
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(typeof(T), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    private static PlayerMobile Player()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private static int[] Live(EvoLine line) => line.Stages.Where(s => !s.ArtPending).Select(s => s.Number).ToArray();

    [Fact]
    public void TheDataHasFourLinesLiveDog5Cat5Fox2Bird3()
    {
        Assert.Equal(new[] { "dog", "cat", "fox", "bird" }, EvoPetData.Lines.Select(l => l.Key));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Live(EvoPetData.Dog));
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, Live(EvoPetData.Cat));
        Assert.Equal(new[] { 1, 3 }, Live(EvoPetData.Fox));
        Assert.Equal(new[] { 1, 2, 5 }, Live(EvoPetData.Bird));

        // The decided picks (F-35 "Stage picks, decided"; cc-P65 brief).
        Assert.Equal(new[] { 217 }, EvoPetData.Dog.Stage(1).Bodies);
        Assert.Equal(new[] { 1547, 1548, 1549, 1546, 1552, 1551 }, EvoPetData.Dog.Stage(3).Bodies);
        Assert.Equal(new[] { 277 }, EvoPetData.Dog.Stage(4).Bodies);
        Assert.Equal(new[] { 1410 }, EvoPetData.Dog.Stage(5).Bodies);
        Assert.Equal(new[] { 1254, 1426 }, EvoPetData.Cat.Stage(3).Bodies);
        Assert.Equal(new[] { 1573 }, EvoPetData.Cat.Stage(5).Bodies);
        Assert.Equal(new[] { 1423 }, EvoPetData.Fox.Stage(1).Bodies);
        Assert.Equal(new[] { 246 }, EvoPetData.Fox.Stage(3).Bodies);
        Assert.True(EvoPetData.Fox.Stage(4).ArtPending);
        Assert.Equal(new[] { 832 }, EvoPetData.Bird.Stage(2).Bodies);
        Assert.Equal(new[] { 243 }, EvoPetData.Bird.Stage(5).Bodies);

        foreach (var line in EvoPetData.Lines)
        {
            var live = line.Stages.Where(s => !s.ArtPending).SelectMany(s => s.Bodies).ToArray();
            Assert.Equal(live.Length, live.Distinct().Count()); // every stage a different body
            Assert.All(line.Stages, s => Assert.Equal(s.Bodies.Length, s.BodyNames.Length));
            Assert.All(line.Stages, s => Assert.Null(s.Abilities)); // the role slot, empty until the abilities batch

            // Gates: second-to-last needs a loop, the last needs loop 5 and the Bond Trial; the first two none.
            var n = line.Stages.Length;
            Assert.Equal(1, line.Stage(n - 1).MinOwnerLoops);
            Assert.Equal(5, line.Stage(n).MinOwnerLoops);
            Assert.True(line.Stage(n).BondTrial);
            Assert.Equal(0, line.Stage(2).MinOwnerLoops);
            Assert.False(line.Stage(2).BondTrial);
        }

        // Stage 1 is about a town dog (pinned Dog.cs: Str 27-37, hits 17-22, damage 4-7), stage 5 about a Cu Sidhe.
        Assert.InRange(EvoPetData.StatsFor(1).Str, 27, 37);
        Assert.InRange(EvoPetData.StatsFor(1).Hits, 17, 22);
        Assert.InRange(EvoPetData.StatsFor(5).Str, 1200, 1225);
        Assert.InRange(EvoPetData.StatsFor(5).Hits, 1010, 1275);
        Assert.Equal(0, EvoPetData.Hues[0].Hue); // "natural" first
    }

    [Fact]
    public void EachLineStartsAtStageOneAndRoundTrips()
    {
        foreach (var line in EvoPetData.Lines)
        {
            var pet = new EvoPet(line.Key);
            Assert.Equal(1, pet.Stage);
            Assert.Equal(line.Stage(1).Bodies[0], (int)pet.Body);
            Assert.Equal(EvoPetData.StatsFor(1).Str, pet.RawStr);
            Assert.Equal(EvoPetData.StatsFor(1).Hits, pet.HitsMax);
            Assert.Equal(1, pet.ControlSlots);
            Assert.Equal(line.Stage(1).Sounds.Idle, pet.GetIdleSound());
            pet.Delete();
        }

        var dog = new EvoPet("dog");
        dog.StaffSetStage(3, 1549);
        dog.BondTrialDone = true;
        dog.TestOwnerLoops = 2;
        dog.Hue = 0x455;
        var copy = RoundTrip(dog);
        _out.WriteLine($"copy: {copy.LineKey} stage {copy.Stage} xp {copy.Xp} pick {copy.PickAt(3)} bond {copy.BondTrialDone} loops {copy.TestOwnerLoops}");
        Assert.Equal("dog", copy.LineKey);
        Assert.Equal(3, copy.Stage);
        Assert.Equal(dog.Xp, copy.Xp);
        Assert.Equal(1549, copy.PickAt(3));
        Assert.True(copy.BondTrialDone);
        Assert.Equal(2, copy.TestOwnerLoops);
        Assert.Equal(1549, (int)copy.Body);
        Assert.Equal(0x455, copy.Hue);
        dog.Delete();

        Assert.Throws<ArgumentException>(() => new EvoPet("dragon"));
    }

    [Fact]
    public void XpComesOnlyFromKillsItDamaged()
    {
        var pm = Player();
        var pet = EvoPet.GivePet(pm, "dog");
        var idle = new EvoPet("cat"); // not controlled: earns nothing
        idle.MoveToWorld(Spot, Map.Trammel);

        var rabbit = new Rabbit();
        rabbit.MoveToWorld(Spot, Map.Trammel);
        var half = rabbit.HitsMax / 2;
        rabbit.Damage(half, pet);
        rabbit.Damage(1, idle);
        var expected = EvoPet.XpForKill(rabbit, (double)half / rabbit.HitsMax);
        rabbit.Kill();

        _out.WriteLine($"rabbit fame {rabbit.Fame} hits {rabbit.HitsMax}: pet xp {pet.Xp} (expected {expected}), idle {idle.Xp}");
        Assert.True(expected > 0);
        Assert.Equal(expected, pet.Xp);
        Assert.Equal(0, idle.Xp);

        // A kill it took no part in: nothing.
        var other = new Rabbit();
        other.MoveToWorld(Spot, Map.Trammel);
        other.Kill();
        Assert.Equal(expected, pet.Xp);

        // A controlled victim (someone's pet) gives nothing.
        var tame = new Rabbit();
        tame.MoveToWorld(Spot, Map.Trammel);
        tame.SetControlMaster(pm);
        tame.Damage(1, pet);
        tame.Kill();
        Assert.Equal(expected, pet.Xp);

        Assert.Equal(0, EvoPet.XpForKill(rabbit, 0));
        Assert.True(EvoPet.XpForKill(new Lich(), 1.0) > EvoPet.XpForKill(new Rabbit(), 1.0));

        pet.Delete();
        idle.Delete();
        pm.Delete();
    }

    [Fact]
    public void ADogWalksItsLineThroughXpThePickTheLoopGateAndTheBondTrial()
    {
        var pm = Player();
        var pet = EvoPet.GivePet(pm, "dog");
        Assert.NotNull(pet);

        // 1 -> 2: XP only.
        pet.AwardXp(EvoPetSettings.XpToEnter(2) - 1);
        Assert.Equal(1, pet.Stage);
        pet.AwardXp(1);
        Assert.Equal(2, pet.Stage);
        Assert.Equal(739, (int)pet.Body);
        Assert.Equal(EvoPetData.StatsFor(2).Hits, pet.HitsMax);
        Assert.Equal(1, pet.ControlSlots);

        // 2 -> 3: XP, then it waits for the owner's pick, which is made once.
        pet.AwardXp(EvoPetSettings.XpToEnter(3));
        Assert.Equal(2, pet.Stage);
        Assert.Contains("the owner's pick", pet.EvoStatus);
        Assert.False(pet.ChoosePick(pm, 3, 1410)); // not a body on offer
        Assert.True(pet.ChoosePick(pm, 3, 1548));
        Assert.Equal(3, pet.Stage);
        Assert.Equal(1548, (int)pet.Body);
        Assert.False(pet.ChoosePick(pm, 3, 1549)); // never changed after

        // 3 -> 4: XP is not enough; the owner must have looped once.
        pet.AwardXp(EvoPetSettings.XpToEnter(4));
        Assert.Equal(3, pet.Stage);
        Assert.Contains("owner at loop 1", pet.EvoStatus);
        pet.TestOwnerLoops = 1;
        Assert.Equal(1, pet.TryEvolve());
        Assert.Equal(4, pet.Stage);
        Assert.Equal(277, (int)pet.Body);

        // 4 -> 5: loop 5 and the Bond Trial.
        pet.AwardXp(EvoPetSettings.XpToEnter(5));
        pet.TestOwnerLoops = 5;
        Assert.Equal(0, pet.TryEvolve());
        Assert.Contains("the Bond Trial", pet.EvoStatus);
        pet.BondTrialDone = true;
        Assert.Equal(1, pet.TryEvolve());
        Assert.Equal(5, pet.Stage);
        Assert.Equal(1410, (int)pet.Body);
        Assert.Equal(EvoPetData.StatsFor(5).Str, pet.RawStr);
        Assert.Contains(EvoPetData.TopForNowLine, pet.EvoStatus);

        // F-12's loop count, when it lands, comes through OwnerLoopsSource.
        var saved = EvoPet.OwnerLoopsSource;
        try
        {
            EvoPet.OwnerLoopsSource = m => m == pm ? 3 : 0;
            pet.TestOwnerLoops = -1;
            Assert.Equal(3, pet.OwnerLoops);
        }
        finally
        {
            EvoPet.OwnerLoopsSource = saved;
        }

        pet.Delete();
        pm.Delete();
    }

    [Fact]
    public void ArtPendingStagesAreSkippedAndTheTopForNowIsSaid()
    {
        var pm = Player();

        // Fox: 1423, then L_Fox (pending) is skipped to 246, whose loop gate still applies; then the kitsune is pending.
        var fox = EvoPet.GivePet(pm, "fox");
        Assert.Equal(3, EvoPetData.Fox.NextEnabled(1)!.Number);
        fox.AwardXp(EvoPetSettings.XpToEnter(3));
        Assert.Equal(1, fox.Stage);
        fox.TestOwnerLoops = 1;
        fox.TryEvolve();
        Assert.Equal(3, fox.Stage);
        Assert.Equal(246, (int)fox.Body);
        Assert.Null(EvoPetData.Fox.NextEnabled(3));
        Assert.Contains(EvoPetData.TopForNowLine, fox.EvoStatus);
        Assert.Equal(0, fox.StaffAddXp(1_000_000));
        Assert.Equal(3, fox.Stage);
        Assert.False(fox.StaffSetStage(4)); // the kitsune row is pending
        fox.Delete();

        // Bird: 254, 832, then the two pending birds are skipped to 243, gated by loop 5 and the Bond Trial.
        var bird = EvoPet.GivePet(pm, "bird");
        bird.AwardXp(EvoPetSettings.XpToEnter(2));
        Assert.Equal(832, (int)bird.Body);
        Assert.Equal(5, EvoPetData.Bird.NextEnabled(2)!.Number);
        bird.AwardXp(EvoPetSettings.XpToEnter(5));
        bird.TestOwnerLoops = 5;
        bird.TryEvolve();
        Assert.Equal(2, bird.Stage);
        bird.BondTrialDone = true;
        bird.TryEvolve();
        Assert.Equal(5, bird.Stage);
        Assert.Equal(243, (int)bird.Body);
        bird.Delete();
        pm.Delete();
    }

    [Fact]
    public void TheOwnerPicksAHueOnceAndItCarriesThroughEveryStage()
    {
        var pm = Player();
        var stranger = Player();
        var pet = EvoPet.GivePet(pm, "cat");

        Assert.False(pet.ChooseHue(stranger, 0x455));
        Assert.False(pet.ChooseHue(pm, 0x1234)); // not on the list
        Assert.True(pet.ChooseHue(pm, 0x455));
        Assert.False(pet.ChooseHue(pm, 0x47E)); // once
        Assert.True(pet.HueChosen);

        pet.StaffSetStage(2);
        pet.StaffSetStage(3, 1426);
        Assert.Equal(1426, (int)pet.Body);
        Assert.Equal(0x455, pet.Hue);

        pet.Delete();
        pm.Delete();
        stranger.Delete();
    }

    [Fact]
    public void TheOwnersResetLeavesTheStagePickXpHueAndBond()
    {
        var account = new Account($"p65e{Guid.NewGuid():N}"[..16], "p65-test-only");
        var pm = Player();
        account[0] = pm;

        try
        {
            var pet = EvoPet.GivePet(pm, "dog");
            pet.ChooseHue(pm, 0x47E);
            pet.AwardXp(EvoPetSettings.XpToEnter(3));
            pet.ChoosePick(pm, 3, 1552);
            Assert.Equal(3, pet.Stage);
            var xp = pet.Xp;

            // The stand-in for F-12's loop reset: the Reset Stone's reset, everything on.
            ClusterFDevTools.ExecuteReset(pm, pm, account, new ResetOptions(true));

            _out.WriteLine($"after the reset: stage {pet.Stage}, xp {pet.Xp}, pick {pet.PickAt(3)}, hue 0x{pet.Hue:X}, master {pet.ControlMaster?.Serial}, bonded {pet.IsBonded}");
            Assert.False(pet.Deleted);
            Assert.Equal(3, pet.Stage);
            Assert.Equal(xp, pet.Xp);
            Assert.Equal(1552, pet.PickAt(3));
            Assert.Equal(1552, (int)pet.Body);
            Assert.Equal(0x47E, pet.Hue);
            Assert.Same(pm, pet.ControlMaster);
            Assert.True(pet.IsBonded);
            pet.Delete();
        }
        finally
        {
            pm.Delete();
            Accounts.Remove(account);
        }
    }

    [Fact]
    public void EveryUnusedBodyWeUseIsAnAnimalAndCannotOpenDoors()
    {
        ShardBodyTypes.Apply();

        foreach (var (body, type, what) in ShardBodyTypes.Entries)
        {
            Assert.Equal(type, new Body(body).Type);
        }

        // Every live stage body is an animal now (the classic ones already were).
        foreach (var line in EvoPetData.Lines)
        {
            foreach (var stage in line.Stages.Where(s => !s.ArtPending))
            {
                foreach (var body in stage.Bodies)
                {
                    Assert.True(new Body(body).IsAnimal, $"{line.Key} stage {stage.Number} body {body} is {new Body(body).Type}");
                }
            }
        }

        var pet = new EvoPet("dog");
        pet.StaffSetStage(3, 1547);
        Assert.False(pet.CanOpenDoors);
        pet.StaffSetStage(5);
        Assert.False(pet.CanOpenDoors);
        pet.Delete();
    }

    [Fact]
    public void TheUopEraBodiesGetTheirActionsRemapped()
    {
        Assert.Equal(10, EvoAnimations.Remap(7));
        Assert.Equal(4, EvoAnimations.Remap(12));
        Assert.Equal(-1, EvoAnimations.Remap(3));
        Assert.Equal(-1, EvoAnimations.Remap(9));
        Assert.Equal(-1, EvoAnimations.Remap(10));
        for (var i = 0; i < 20; i++)
        {
            Assert.InRange(EvoAnimations.Remap(5), 4, 5);
            Assert.InRange(EvoAnimations.Remap(6), 4, 5);
        }

        Assert.Equal(0, EvoAnimations.Remap(0));

        // Every live stage body is either a classic animal body (left alone) or a UOP-era body (remapped).
        var classic = new[] { 217, 201, 277, 254, 246, 243 };
        foreach (var body in EvoPetData.Lines.SelectMany(l => l.Stages).Where(s => !s.ArtPending).SelectMany(s => s.Bodies))
        {
            Assert.True(classic.Contains(body) ^ EvoAnimations.IsUopBody(body), $"body {body}");
        }
    }

    [Fact]
    public void GivePetHandsAStageOnePetTamedAndBondedAtTheirFeet()
    {
        var pm = Player();
        var pet = EvoPet.GivePet(pm, "cat");

        Assert.NotNull(pet);
        Assert.Same(pm, pet.ControlMaster);
        Assert.True(pet.Controlled);
        Assert.True(pet.IsBonded);
        Assert.Contains(pm, pet.Owners);
        Assert.Equal(OrderType.Follow, pet.ControlOrder);
        Assert.Equal(pm.Location, pet.Location);
        Assert.Equal(1, pm.Followers);
        Assert.Null(EvoPet.GivePet(pm, "griffin"));

        // No free follower slot: nothing given.
        pm.Followers = pm.FollowersMax;
        var count = World.Mobiles.Values.Count(m => m is EvoPet);
        Assert.Null(EvoPet.GivePet(pm, "dog"));
        Assert.Equal(count, World.Mobiles.Values.Count(m => m is EvoPet));

        pet.Delete();
        pm.Delete();
    }
}
