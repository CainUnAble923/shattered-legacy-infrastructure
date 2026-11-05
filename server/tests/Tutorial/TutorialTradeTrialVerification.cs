// cc-P291: the tutorial's wing trials, part 2 (customizations/Tutorial/TutorialTradeTrials.cs, the second part of
// TutorialWingTrials). Notes: shard-migration notes/cc-P291-tutorial-wing-trials-trades.md.
//
// Facts, per trial its done check and its cleanup:
//   1. Every one of the 37 stations has a lesson: a handler, and no page that says "coming soon".
//   2. Gathering (Mining, Lumberjacking, Fishing, Camping): the lesson lends its tool and puts its vein, tree or pond
//      beside the trainee; a failed roll or another skill's roll does not count; a successful roll of the skill
//      finishes it; the tool, the skill loan and the props go back, and what was gathered stays.
//   3. The crafting yard (nine crafts): a loaner of the product's type does not count; a product made into the
//      backpack does, and is kept; Blacksmithy, Tinkering and Alchemy also need their covered skill's roll (Arms Lore,
//      Item Identification, Taste Identification); the tool and materials go back; Blacksmithy brings its anvil and forge.
//   4. Animals: the tutorial goat in the farm pen; herding, taming, lore and veterinary finish it; a tamed goat that is
//      not hurt is wounded for the bandage; the goat goes back with the loans.
//   5. Bard: two echoes; peacemaking, provocation and discordance finish it.
//   6. Thieving: the practice chest (locked, trapped, a purse inside) and the mark (with a backpack); the ten rolls
//      finish it; the Hiding base is lifted to 30 for Stealth and put back after, keeping any gain; the stolen purse
//      goes back.
//   7. The four tours: stops count in order only; the last stop finishes it; going far from New Haven stops it.
//   8. Props left by a restart are swept like loaners.

#nullable enable

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
public class TutorialTradeTrialVerification : IDisposable
{
    private readonly Func<Point3D, bool> _fits = TutorialWingTrials.Fits;

    public TutorialTradeTrialVerification()
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
        public readonly Account Account = new($"p291{Guid.NewGuid():N}"[..16], "p291-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(Point2D at)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Trainee", RawStr = 60, RawDex = 60, RawInt = 20 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(at.X, at.Y, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
            Assert.True(ClusterFTutorial.TryEnter(Pm));
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

    private static void AssertAllReturned(Player p, TutorialWingTrials.Trial trial, List<Item> loaners, List<Item> props,
        List<BaseCreature> creatures)
    {
        Assert.All(loaners, i => Assert.True(i.Deleted));
        Assert.All(props, i => Assert.True(i.Deleted));
        Assert.All(creatures, c => Assert.True(c.Deleted));
        Assert.DoesNotContain(p.Pm.SkillMods ?? new List<SkillMod>(), m => m.Name == TutorialWingTrials.LoanName);
        Assert.Null(p.Pm.GetStatMod(TutorialWingTrials.LoanName));
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EveryStationHasALessonAndNoneSaysComingSoon()
    {
        foreach (var station in TutorialStations.All)
        {
            Assert.True(TutorialStations.Handlers.ContainsKey(station.Key), $"{station.Key} has no lesson");
            if (!station.IsCore && !TutorialStations.IsAnytime(station.Key)) // cc-P718: the client tips are not a wing
            {
                Assert.True(TutorialWingTrials.IsWingTrial(station.Key), $"{station.Key} is not a wing trial");
                Assert.DoesNotContain("coming soon", TutorialWingTrials.Hint(station.Key), StringComparison.OrdinalIgnoreCase);
            }

            Assert.DoesNotContain("coming soon", TutorialCoreTrials.Hint(station.Key), StringComparison.OrdinalIgnoreCase);
        }

        Assert.Equal(40, TutorialStations.All.Length); // cc-P663: core.farm joined the core; cc-P718: client.tips; cc-P726: core.healing
        Assert.Equal(20, TutorialWingTrials.Trades.Length); // 28 wing stations less P290's 8
        Assert.Equal(28, TutorialStations.All.Count(s => !s.IsCore && !TutorialStations.IsAnytime(s.Key)));
    }

    // ---------------------------------------------------------------- 2

    [Theory]
    [InlineData("gathering.mining", SkillName.Mining, typeof(Pickaxe), 0)] // cc-P743: the cave's own floor, no vein
    [InlineData("gathering.lumberjacking", SkillName.Lumberjacking, typeof(Hatchet), 2)]
    [InlineData("gathering.fishing", SkillName.Fishing, typeof(FishingPole), 4)]
    [InlineData("gathering.camping", SkillName.Camping, typeof(Kindling), 1)] // cc-P804: the wild garlic
    public void AHarvestFinishesGatheringAndTheToolAndPropsGoBack(string key, SkillName skill, Type tool, int props)
    {
        using var p = new Player(TutorialWingTrials.Trade(key)!.Place);
        var trial = Begin(p, key);
        var loaners = trial.Loaners.ToList();
        var placed = trial.Props.ToList();
        Assert.Contains(loaners, i => i.GetType() == tool);
        Assert.Equal(props, placed.Count);
        Assert.All(placed, i => Assert.StartsWith(TutorialWingTrials.PropPrefix, i.Name));
        Assert.All(placed, i => Assert.Equal(Map.Trammel, i.Map));
        Assert.True(p.Pm.Skills[skill].Value >= 60.0);

        Roll(p, skill, false);                  // nothing came up
        Roll(p, SkillName.Tailoring);            // another skill
        Assert.DoesNotContain(key, p.Record.StationsDone);

        var ore = new IronOre(3);                // what the harvest gave stays the trainee's
        p.Pm.AddToBackpack(ore);
        Roll(p, skill);
        if (key == "gathering.camping")
        {
            Assert.DoesNotContain(key, p.Record.StationsDone); // cc-P804: the garlic is still to pick
            Assert.True(p.Pm.Skills[SkillName.Herbalism].Value >= 30.0);
            Roll(p, SkillName.Herbalism);
        }

        Assert.Contains(key, p.Record.StationsDone);
        AssertAllReturned(p, trial, loaners, placed, trial.Creatures.ToList());
        Assert.False(ore.Deleted);
    }

    // ---------------------------------------------------------------- 3

    [Theory]
    [InlineData("crafting.blacksmithy", typeof(Dagger), SkillName.ArmsLore)]
    [InlineData("crafting.tailoring", typeof(SkullCap), null)]
    [InlineData("crafting.carpentry", typeof(BarrelStaves), null)]
    [InlineData("crafting.tinkering", typeof(Scissors), SkillName.ItemID)]
    [InlineData("crafting.alchemy", typeof(RefreshPotion), SkillName.TasteID)]
    [InlineData("crafting.fletching", typeof(Kindling), null)]
    [InlineData("crafting.cooking", typeof(Dough), null)]
    [InlineData("crafting.inscription", typeof(MagicArrowScroll), null)]
    [InlineData("crafting.cartography", typeof(LocalMap), null)]
    public void AProductInTheBackpackFinishesACraftAndIsKept(string key, Type product, SkillName? covered)
    {
        using var p = new Player(TutorialWingTrials.TradesYard);
        var trial = Begin(p, key);
        var loaners = trial.Loaners.ToList();
        Assert.True(loaners.Count >= 2); // a tool and its materials
        Assert.Contains(loaners, i => i is BaseTool);
        if (key == "crafting.blacksmithy")
        {
            Assert.Single(trial.Props.OfType<Anvil>());
            Assert.Single(trial.Props.OfType<Forge>());
        }

        // A loaner of the product's type is not a product (the thief's trial lends a dagger, for one).
        var fake = product.CreateEntityInstance<Item>();
        fake.Name = TutorialWingTrials.LoanerPrefix + "fake";
        p.Pm.AddToBackpack(fake);
        Tick();
        Assert.DoesNotContain("made", trial.Steps);
        fake.Delete();

        var made = product.CreateEntityInstance<Item>();
        p.Pm.AddToBackpack(made);
        Tick();
        Assert.Contains("made", trial.Steps);
        if (covered is { } skill)
        {
            Assert.DoesNotContain(key, p.Record.StationsDone);
            Roll(p, skill);
        }

        Assert.Contains(key, p.Record.StationsDone);
        AssertAllReturned(p, trial, loaners, trial.Props.ToList(), trial.Creatures.ToList());
        Assert.False(made.Deleted); // the product is the trainee's
    }

    [Fact]
    public void ProductsAlreadyInTheBackpackDoNotCount()
    {
        using var p = new Player(TutorialWingTrials.TradesYard);
        p.Pm.AddToBackpack(new Kindling(4)); // the trainee's own kindling, before the lesson
        var trial = Begin(p, "crafting.fletching");
        Tick();
        Assert.DoesNotContain("made", trial.Steps);
        p.Pm.AddToBackpack(new Kindling()); // made: it may merge into the old pile
        Tick();
        Assert.Contains("crafting.fletching", p.Record.StationsDone);
    }

    // ---------------------------------------------------------------- 4

    // cc-P743 item 4: the goat in the old farm pen became the injured stable horse (NewHavenFixesVerification covers it).
    [Fact]
    public void TheAnimalsLessonIsInTheStablesWithTheInjuredHorse()
    {
        using var p = new Player(new Point2D(HavenSpots.Stables.X, HavenSpots.Stables.Y));
        var trial = Begin(p, "animals.taming");
        Assert.NotNull(trial.Horse);
        Assert.Equal(HavenSpots.SickStall, trial.Horse!.Location);
        Roll(p, SkillName.Veterinary);   // a roll alone is not the horse healed
        Roll(p, SkillName.AnimalTaming); // nor tamed
        Assert.Empty(trial.Steps);
        TutorialWingTrials.Cancel(p.Pm);
        Assert.True(trial.Horse == null || trial.Horse.Deleted);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void PeaceProvocationAndDiscordFinishTheBard()
    {
        using var p = new Player(new Point2D(HavenSpots.BardicGuild.X, HavenSpots.BardicGuild.Y)); // cc-P743 item 7
        var trial = Begin(p, "bard.trial");
        var loaners = trial.Loaners.ToList();
        Assert.Single(loaners.OfType<Lute>());
        var echoes = trial.Creatures.ToList();
        Assert.Equal(2, echoes.Count);
        Assert.True(p.Pm.Skills[SkillName.Musicianship].Value >= 80.0);

        Roll(p, SkillName.Musicianship); // music alone is not a bard skill's use
        Roll(p, SkillName.Peacemaking);
        Roll(p, SkillName.Provocation);
        Assert.DoesNotContain("bard.trial", p.Record.StationsDone);
        Roll(p, SkillName.Discordance);
        // cc-P743 item 5: the provoked echoes fight first; the lesson finishes when the brawl is over.
        Assert.DoesNotContain("bard.trial", p.Record.StationsDone);
        for (var i = 0; i < 40 && !p.Record.StationsDone.Contains("bard.trial"); i++)
        {
            Tick();
        }

        Assert.Contains("bard.trial", p.Record.StationsDone);
        AssertAllReturned(p, trial, loaners, trial.Props.ToList(), echoes);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheTenThiefSkillsFinishThievingAndTheHidingBaseComesBack()
    {
        using var p = new Player(TutorialWingTrials.TradesYard);
        p.Pm.Skills[SkillName.Hiding].Base = 12.0;
        var trial = Begin(p, "thieving.trial");
        var loaners = trial.Loaners.ToList();
        Assert.Single(loaners.OfType<Lockpick>());
        Assert.Single(loaners.OfType<Dagger>());
        Assert.Single(loaners.OfType<LesserPoisonPotion>());
        Assert.Equal(30.0, p.Pm.Skills[SkillName.Hiding].Base); // Stealth's requirement, for the lesson

        var chest = trial.Props.OfType<WoodenChest>().Single();
        Assert.True(chest.Locked);
        Assert.NotEqual(TrapType.None, chest.TrapType);
        var purse = chest.Items.Single();
        Assert.StartsWith(TutorialWingTrials.PropPrefix, purse.Name);
        Assert.NotEqual(LootType.Blessed, purse.LootType); // stealing refuses blessed items
        var mark = trial.Creatures.Single();
        Assert.NotNull(mark.Backpack);
        Assert.True(mark.Body.IsHuman);
        Assert.True(mark.AlwaysAttackable); // snooping a human in town needs it (Snooping.CheckSnoopAllowed)

        p.Pm.AddToBackpack(purse); // stolen
        var skills = new[]
        {
            SkillName.DetectHidden, SkillName.RemoveTrap, SkillName.Lockpicking, SkillName.Stealing, SkillName.Snooping,
            SkillName.Begging, SkillName.Poisoning, SkillName.Tracking, SkillName.Hiding
        };
        foreach (var skill in skills)
        {
            Roll(p, skill);
        }

        p.Pm.Skills[SkillName.Hiding].Base = 31.0; // the lesson's hiding gained 1.0
        Assert.DoesNotContain("thieving.trial", p.Record.StationsDone);
        Roll(p, SkillName.Stealth);
        Assert.Contains("thieving.trial", p.Record.StationsDone);
        Assert.Equal(13.0, p.Pm.Skills[SkillName.Hiding].Base, 3);
        Assert.True(purse.Deleted);
        AssertAllReturned(p, trial, loaners, new List<Item> { chest }, new List<BaseCreature> { mark });
    }

    // ---------------------------------------------------------------- 7

    [Theory]
    [InlineData("housing.basics")]
    [InlineData("travel.wing")]
    [InlineData("reputation.wing")]
    [InlineData("shard.systems")]
    public void ATourCountsItsStopsInOrderAndFinishesAtTheLast(string key)
    {
        using var p = new Player(TutorialWingTrials.TourCentre);
        var trial = Begin(p, key);
        var stops = TutorialWingTrials.Trade(key)!.Stops!;
        var loaners = trial.Loaners.ToList();
        Assert.True(stops.Length >= 2);

        // The last stop first counts for nothing.
        var last = stops[^1].Spot();
        p.Pm.MoveToWorld(last, Map.Trammel);
        Tick();
        Assert.Empty(trial.Steps);

        for (var i = 0; i < stops.Length; i++)
        {
            Assert.DoesNotContain(key, p.Record.StationsDone);
            p.Pm.MoveToWorld(stops[i].Spot(), Map.Trammel);
            Tick();
            Assert.Equal(i + 1, trial.Steps.Count);
        }

        Assert.Contains(key, p.Record.StationsDone);
        Assert.All(loaners, i => Assert.True(i.Deleted));
    }

    [Fact]
    public void GoingFarFromNewHavenStopsATourAndLeavingTheYardStopsATrade()
    {
        using var p = new Player(TutorialWingTrials.TourCentre);
        Begin(p, "travel.wing");
        // 200 tiles south: past the tour's reach (160) and still on the island, which the island gate keeps a trainee on
        // (cc-P678: 200 east of the new centre is off the island)
        p.Pm.MoveToWorld(new Point3D(TutorialWingTrials.TourCentre.X, TutorialWingTrials.TourCentre.Y + 200, 0), Map.Trammel);
        Tick();
        Assert.Null(TutorialWingTrials.Current(p.Pm));

        p.Pm.MoveToWorld(HavenSpots.MiningCave, Map.Trammel); // cc-P743: the mining lesson is at the cave
        var trial = Begin(p, "gathering.mining");
        var loaners = trial.Loaners.ToList();
        var placed = trial.Props.ToList();
        p.Pm.MoveToWorld(new Point3D(HavenSpots.MiningCave.X + 30, HavenSpots.MiningCave.Y, 0), Map.Trammel);
        Tick();
        Assert.Null(TutorialWingTrials.Current(p.Pm));
        AssertAllReturned(p, trial, loaners, placed, new List<BaseCreature>());
        Assert.DoesNotContain("gathering.mining", p.Record.StationsDone);
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void PropsLeftByARestartAreSwept()
    {
        var left = new Static(0x053B) { Name = TutorialWingTrials.PropPrefix + "practice vein" };
        var own = new Static(0x053B);
        left.MoveToWorld(new Point3D(1800, 1802, 0), Map.Trammel);
        own.MoveToWorld(new Point3D(1800, 1803, 0), Map.Trammel);
        try
        {
            Assert.True(TutorialWingTrials.SweepLoaners() >= 1);
            Assert.True(left.Deleted);
            Assert.False(own.Deleted);
        }
        finally
        {
            own.Delete();
        }
    }

    // ---------------------------------------------------------------- cc-P769 items 12 and 13

    // The first outside playtester (11-01): "I could not find the skill to investigate an item", and "Past the first
    // initial quest, I don't have specifics of what to do". The curio's double-click starts Item Identification, the step
    // names the skill, and a finished lesson says to go back to Rook, with the arrow on him.
    [Fact]
    public void P769_TheCurioStartsItemIdentificationAndTheEndSendsYouBackToRook()
    {
        ItemIdentification.Initialize();
        var said = new List<string>();
        var seam = TutorialWingTrials.Said;
        TutorialWingTrials.Said = (_, line) => said.Add(line);
        try
        {
            using var p = new Player(TutorialWingTrials.TradesYard);
            var trial = Begin(p, "crafting.tinkering");
            Assert.Contains("Item Identification skill", TutorialWingTrials.Hint("crafting.tinkering"));
            Assert.Contains("double-click the curio", TutorialWingTrials.Hint("crafting.tinkering"));

            var curio = p.Pm.Backpack!.FindItemByType<TutorialCurio>()!;
            Assert.Null(p.Pm.Target);
            curio.OnDoubleClick(p.Pm);
            Assert.NotNull(p.Pm.Target); // Item Identification's target cursor
            p.Pm.Target = null;

            p.Pm.AddToBackpack(new Scissors());
            Tick();
            Roll(p, SkillName.ItemID);
            Assert.Contains("crafting.tinkering", p.Record.StationsDone);
            Assert.Contains(TutorialWingTrials.NextLessonLine, said);
            Assert.Contains("Rook", TutorialWingTrials.NextLessonLine);
        }
        finally
        {
            TutorialWingTrials.Said = seam;
        }
    }
}
