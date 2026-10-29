// cc-P480: the tutorial fixes from Chase's first play (10-23). Notes: shard-migration
// notes/cc-P480-tutorial-first-play-fixes.md.
//
// Facts, each seen red before the change:
//   1. Rook's menu shows the nine core lessons and "Skip" only until the core is done; then the wings appear. A wing
//      button pressed before that runs nothing; staff still run any lesson.
//   2. The backpack lesson comes before the paperdoll lesson, and a record saved partway through the old order carries
//      on without redoing a lesson.
//   3. The seeder places a bright blue and a bright gold floor tile for the walking lesson, once.
//   4. Every tutorial gump uses the shared light text colors, and none says "System:".
//   5. The practice supplies are bright and recognisable, and "Hint and arrow" replaces one that went missing.
//   6. The graduation companion pick is never lost: Rook offers it until it is chosen.
//   7. A dead pet: the Farmhand notices it once per cooldown, the farm vet offers to bring it back, the graduation box
//      holds an elixir of rebirth, and animal trainers and the Rangers sell the elixir.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialFirstPlayVerification : IDisposable
{
    private readonly Func<PlayerMobile, int> _loopProvider = ClusterFLootBoxes.LoopProvider;

    public TutorialFirstPlayVerification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }

        ClusterFTutorial.Enabled = true;
        FoldMaps.Configure(); // cc-P663: the lessons are on the isle (map 32)
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
        ClusterFLootBoxes.LoopProvider = ClusterFLoops.LoopOf;
    }

    public void Dispose() => ClusterFLootBoxes.LoopProvider = _loopProvider;

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p480{Guid.NewGuid():N}"[..16], "p480-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<IEntity> Litter = new();

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Newbie", RawStr = 60, RawDex = 60, RawInt = 60 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1720, 1720, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public TutorialRecord Record => ClusterFTutorial.RecordOf(Pm)!;

        public void Dispose()
        {
            TutorialCoreTrials.Cancel(Pm);
            ClusterFFanfare.FlushAll();
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
            Ns.Dispose();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static TutorialAction[] Evidence(string key) => key switch
    {
        "core.movement" => [TutorialAction.FirstMark, TutorialAction.SecondMark],
        "core.paperdoll" => [TutorialAction.Equipped],
        "core.backpack" => [TutorialAction.OpenedPaperdoll, TutorialAction.OpenedBag], // cc-P718: the character window first
        "core.chat" => [TutorialAction.SaidPhrase],
            "core.farm" => [TutorialAction.FarmDone],
            "core.healing" => [TutorialAction.Healed],
        "core.death" => [TutorialAction.EchoDeath, TutorialAction.HealerOffer, TutorialAction.Resurrected],
        "core.banking" => [TutorialAction.Deposited],
        "core.travel" => [TutorialAction.UsedGate],
        "core.league" => [TutorialAction.TalkedToLeague],
        "core.loop" => [TutorialAction.VisionStarted, TutorialAction.VisionFinished],
        _ => throw new InvalidOperationException(key)
    };

    private static void Play(PlayerMobile pm, string key)
    {
        ClusterFTutorial.RunStation(pm, TutorialStations.Find(key)!);
        foreach (var action in Evidence(key))
        {
            TutorialCoreTrials.Observe(pm, action);
        }

        Assert.Contains(key, ClusterFTutorial.RecordOf(pm)!.StationsDone);
    }

    /// <summary>Plays the core in its order until (not including) the given lesson; all of it when null.</summary>
    private static void PlayCoreUntil(PlayerMobile pm, string? stop)
    {
        foreach (var station in TutorialStations.Core.ToList())
        {
            if (station.Key == stop)
            {
                return;
            }

            Play(pm, station.Key);
        }
    }

    private static int[] Buttons(Gump g) => g.Entries.OfType<GumpButton>().Select(b => b.ButtonID).ToArray();

    private static string[] Texts(Gump g) =>
        g.Entries.OfType<GumpLabel>().Select(l => l.Text)
            .Concat(g.Entries.OfType<GumpHtml>().Select(h => h.Text)).ToArray();

    private static int StationButton(string key) =>
        TutorialRecruiterGump.BtnStationBase + Array.FindIndex(TutorialStations.All, s => s.Key == key);

    private static RelayInfo Press(int button) => new(button, default, default, default, default);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void RooksMenuShowsOnlyTheCoreAndSkipUntilTheCoreIsDone()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);

        var before = new TutorialRecruiterGump(p.Pm);
        var buttons = Buttons(before);
        foreach (var s in TutorialStations.All)
        {
            if (s.IsCore || TutorialStations.IsAnytime(s.Key)) // cc-P718: the client tips are offered at any time
            {
                Assert.Contains(StationButton(s.Key), buttons);
            }
            else
            {
                Assert.DoesNotContain(StationButton(s.Key), buttons);
            }
        }

        Assert.Contains(TutorialRecruiterGump.BtnSkip, buttons);
        Assert.Contains(Texts(before), t => t.Contains("Finish the main lessons to open", StringComparison.OrdinalIgnoreCase));

        // A wing's button sent before the core is done (a crafted reply) runs nothing.
        before.OnResponse(p.Ns, Press(StationButton("crafting.alchemy")));
        Assert.False(p.Pm.HasGump<TutorialStationGump>());

        // Staff still run any lesson (cc-P291: every wing station has its own lesson page now).
        TutorialWingTrials.EnsureHooks();
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("crafting.alchemy")!);
        Assert.True(p.Pm.HasGump<TutorialWingTrialGump>());
        TutorialWingTrials.Cancel(p.Pm);

        PlayCoreUntil(p.Pm, null);
        Assert.Equal(TutorialState.InWings, p.Record.State);
        var after = Buttons(new TutorialRecruiterGump(p.Pm));
        Assert.All(TutorialStations.All, s => Assert.Contains(StationButton(s.Key), after));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheBackpackLessonComesBeforeThePaperdollAndOldProgressCarriesOn()
    {
        var order = TutorialStations.Core.Select(s => s.Key).ToList();
        Assert.True(order.IndexOf("core.backpack") < order.IndexOf("core.paperdoll"));

        // Partway through the old order (movement, then paperdoll): the backpack is next, the paperdoll stays done.
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        p.Record.StationsDone.Add("core.movement");
        p.Record.StationsDone.Add("core.paperdoll");

        var w = new BufferWriter(true);
        ClusterFAccountPersistence.GetOrCreate(p.Account).Serialize(w);
        var copy = new ClusterFAccountData(new BufferReader(w.Buffer)).Tutorial;
        Assert.Equal("core.backpack", ClusterFTutorial.CoreLeft(copy).First().Key);

        Play(p.Pm, "core.backpack");
        Assert.Equal("core.chat", ClusterFTutorial.CoreLeft(p.Record).First().Key);

        // Only the first lesson done (either order): the backpack lesson is next, and the paperdoll waits for it.
        using var q = new Player();
        TutorialTestStart.Begin(q.Pm);
        Play(q.Pm, "core.movement");
        Assert.Equal("core.backpack", ClusterFTutorial.CoreLeft(q.Record).First().Key);
        ClusterFTutorial.RunStation(q.Pm, TutorialStations.Find("core.paperdoll")!);
        Assert.Null(TutorialCoreTrials.Current(q.Pm));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheSeederPlacesABrightBlueAndABrightGoldFloorTileOnce()
    {
        var testWorld = TutorialCorePlacement.IsTestShard;
        var fits = TutorialCorePlacement.FitsOn;
        var farmWorld = TutorialFarm.IsTestShard;
        var patched = TutorialFarm.MapPatched;
        var items = new HashSet<Item>(World.Items.Values);
        var mobiles = new HashSet<Mobile>(World.Mobiles.Values);
        try
        {
            TutorialCorePlacement.IsTestShard = () => true;
            TutorialCorePlacement.FitsOn = (_, _) => true;
            TutorialFarm.IsTestShard = () => true;
            TutorialFarm.MapPatched = () => true;

            TutorialSeeder.Seed(false);
            List<Item> Tiles(Point3D at, int hue)
            {
                var found = new List<Item>();
                foreach (var i in TutorialWorld.Isle.GetItemsAt(at))
                {
                    if (i.ItemID == TutorialStyle.FloorTileId && i.Hue == hue && !items.Contains(i))
                    {
                        found.Add(i);
                    }
                }

                return found;
            }

            Assert.Single(Tiles(TutorialCorePlacement.FirstMark, TutorialStyle.BlueTileHue));
            Assert.Single(Tiles(TutorialCorePlacement.SecondMark, TutorialStyle.GoldTileHue));

            var count = World.Items.Count;
            TutorialSeeder.Seed(false);
            Assert.Equal(count, World.Items.Count);
        }
        finally
        {
            TutorialCorePlacement.IsTestShard = testWorld; TutorialCorePlacement.FitsOn = fits;
            TutorialFarm.IsTestShard = farmWorld; TutorialFarm.MapPatched = patched;
            foreach (var item in World.Items.Values.ToArray()) { if (!items.Contains(item)) { item.Delete(); } }
            foreach (var mobile in World.Mobiles.Values.ToArray()) { if (!mobiles.Contains(mobile)) { mobile.Delete(); } }
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void EveryTutorialGumpUsesTheSharedLightTextAndNoneSaysSystem()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        var echo = new TutorialDeathEcho();
        var healer = new TutorialHealer();
        p.Litter.Add(echo);
        p.Litter.Add(healer);

        var gumps = new List<Gump>
        {
            new TutorialRecruiterGump(p.Pm), new TutorialSkipGump(1), new TutorialSkipGump(2), new TutorialDeathGump(echo),
            new TutorialResurrectionGump(healer), new TutorialPetPickGump(), new TutorialFarmPetGump(),
            new TutorialStationGump(TutorialStations.Find("crafting.alchemy")!)
        };
        foreach (var s in TutorialStations.Core)
        {
            gumps.Add(new TutorialTrialGump(s, false));
            gumps.Add(new TutorialTrialGump(s, true));
        }

        PlayCoreUntil(p.Pm, null);
        gumps.Add(new TutorialRecruiterGump(p.Pm));

        int[] style = [TutorialStyle.HeadingHue, TutorialStyle.TextHue, TutorialStyle.ActionHue, TutorialStyle.DoneHue, TutorialStyle.WarnHue];
        foreach (var g in gumps)
        {
            var name = g.GetType().Name;
            Assert.All(Texts(g), t => Assert.DoesNotContain("System:", t));
            Assert.All(g.Entries.OfType<GumpLabel>(), l => Assert.True(style.Contains(l.Hue), $"{name}: label '{l.Text}' hue {l.Hue}"));
            Assert.All(g.Entries.OfType<GumpHtml>(), h => Assert.True(h.Text.StartsWith("<BASEFONT COLOR=", StringComparison.Ordinal), $"{name}: html '{h.Text}'"));
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheSuppliesAreBrightAndAMissingOneIsReplacedByHintAndArrow()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        Assert.Contains("tile", TutorialCoreTrials.Hint("core.movement"));
        Assert.Contains("backpack", TutorialCoreTrials.Hint("core.banking"));

        foreach (var key in new[] { "core.backpack", "core.banking" }) // cc-P718: the cloak waits on the table instead
        {
            PlayCoreUntil(p.Pm, key);
            var station = TutorialStations.Find(key)!;
            ClusterFTutorial.RunStation(p.Pm, station);
            var supply = TutorialCoreTrials.Current(p.Pm)!.Supply!;
            Assert.Equal(TutorialStyle.SupplyHue, supply.Hue);
            Assert.NotEqual(0x1F14, supply.ItemID); // not a recall rune
            Assert.Contains("practice", supply.Name);
            Assert.Contains("blue", TutorialCoreTrials.Hint(key));

            // Still in the backpack: "Hint and arrow" gives no second one.
            new TutorialTrialGump(station, false).OnResponse(p.Ns, Press(1));
            Assert.Same(supply, TutorialCoreTrials.Current(p.Pm)!.Supply);

            // Lost: "Hint and arrow" gives a new one, in the backpack.
            supply.Delete();
            new TutorialTrialGump(station, false).OnResponse(p.Ns, Press(1));
            var fresh = TutorialCoreTrials.Current(p.Pm)!.Supply!;
            Assert.NotSame(supply, fresh);
            Assert.False(fresh.Deleted);
            Assert.True(fresh.IsChildOf(p.Pm.Backpack));

            foreach (var action in Evidence(key))
            {
                TutorialCoreTrials.Observe(p.Pm, action);
            }

            Assert.Contains(key, p.Record.StationsDone);
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheCompanionPickIsNeverLostAndRookOffersItUntilChosen()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        PlayCoreUntil(p.Pm, null);
        Assert.Null(ClusterFTutorial.Graduate(p.Pm));

        var pick = p.Pm.FindGump<TutorialPetPickGump>();
        Assert.NotNull(pick);
        Assert.Equal(ClusterFTutorial.StarterPetLines.Length, pick!.Entries.OfType<GumpItem>().Count()); // a picture each
        p.Pm.CloseGump<TutorialPetPickGump>(); // closed without choosing

        var rook = new TutorialRecruiterGump(p.Pm);
        Assert.Contains(TutorialRecruiterGump.BtnCompanion, Buttons(rook));
        Assert.Contains(Texts(rook), t => t.Contains("Choose your pet"));
        rook.OnResponse(p.Ns, Press(TutorialRecruiterGump.BtnCompanion));
        pick = p.Pm.FindGump<TutorialPetPickGump>();
        Assert.NotNull(pick);

        pick!.OnResponse(p.Ns, Press(1)); // the first line, the dog
        Assert.Single(p.Pm.AllFollowers);
        p.Litter.AddRange(p.Pm.AllFollowers);
        var later = new TutorialRecruiterGump(p.Pm);
        Assert.DoesNotContain(TutorialRecruiterGump.BtnCompanion, Buttons(later));
        Assert.DoesNotContain(Texts(later), t => t.Contains("Choose your pet"));
    }

    // ---------------------------------------------------------------- 7

    private static EvoPet DeadPet(Player p)
    {
        var pet = ClusterFTutorial.GivePet(p.Pm, "dog")!;
        p.Litter.Add(pet);
        pet.MoveToWorld(new Point3D(TutorialFarm.FarmhandSpot.X + 2, TutorialFarm.FarmhandSpot.Y, 0), TutorialWorld.Isle);
        pet.Kill();
        pet.Corpse?.Delete(); // its body has faded
        Assert.True(pet.IsDeadBondedPet);
        return pet;
    }

    [Fact]
    public void TheFarmhandNoticesADeadPetOncePerCooldownAndTheFarmVetBringsItBack()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        p.Pm.Young = true;
        p.Pm.MoveToWorld(new Point3D(TutorialFarm.FarmhandSpot, 0), TutorialWorld.Isle);
        var hand = new TutorialFarmhand();
        hand.MoveToWorld(new Point3D(TutorialFarm.FarmhandSpot.X, TutorialFarm.FarmhandSpot.Y - 1, 0), TutorialWorld.Isle);
        p.Litter.Add(hand);
        var pet = DeadPet(p);

        hand.OnMovement(pet, new Point3D(pet.X + 10, pet.Y, pet.Z));
        Assert.False(TutorialFarm.NoticeGhostPet(hand, pet)); // said once already, by the step in
        ShardTestClock.Advance(TimeSpan.FromMinutes(3));
        Assert.True(TutorialFarm.NoticeGhostPet(hand, pet));

        var vet = new TutorialVeterinarian();
        vet.MoveToWorld(new Point3D(p.Pm.X + 1, p.Pm.Y, p.Pm.Z), p.Pm.Map);
        p.Litter.Add(vet);
        vet.OnMovement(p.Pm, new Point3D(p.Pm.X + 10, p.Pm.Y, p.Pm.Z));
        // cc-P666: on the isle, a Young owner's pet within 3 tiles of the vet comes back at once, with no window.
        Assert.Null(p.Pm.FindGump<TutorialVetResurrectGump>());
        Assert.False(pet.IsDeadBondedPet);
    }

    private static int Elixirs(PlayerMobile pm)
    {
        var n = 0;
        foreach (var _ in pm.Backpack.FindItemsByType<ElixirOfRebirth>())
        {
            n++;
        }

        return n;
    }

    [Fact]
    public void TheGraduationBoxHoldsAnElixirAndTrainersAndRangersSellIt()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        PlayCoreUntil(p.Pm, null);
        Assert.Null(ClusterFTutorial.Graduate(p.Pm));
        LootBox? box = null;
        foreach (var b in p.Pm.Backpack.FindItemsByType<LootBox>())
        {
            if (b.EarnedFor == "finishing the New Haven tutorial")
            {
                box = b;
            }
        }

        Assert.NotNull(box);
        var elixir = box!.Contents.OfType<ElixirOfRebirth>().Single();
        Assert.Equal(LootType.Blessed, elixir.LootType);

        var trainer = new AnimalTrainer();
        p.Litter.Add(trainer);
        Assert.Contains(trainer.GetBuyInfo(), i => i is GenericBuyInfo { Type: var t } && t == typeof(ElixirOfRebirth));

        // A Rangers' League member buys one at the guildmaster for gold.
        var guild = ClusterFAccountPersistence.GetOrCreate(p.Account).GetOrCreateGuildData(p.Pm.Serial);
        guild.JoinedGuilds.Add("rangers");
        p.Pm.Backpack.DropItem(new Gold(1000));
        var gm = new OutridersGuildmaster(); // cc-P610: sold in person (TutorialServicesVerification)
        gm.MoveToWorld(new Point3D(p.Pm.X + 1, p.Pm.Y, p.Pm.Z), p.Pm.Map);
        p.Litter.Add(gm);
        var dashboard = new OutridersGuildmasterGump(p.Pm, OutridersGuildmasterGump.View.MemberDashboard);
        Assert.Contains(Texts(dashboard), t => t.Contains("elixir of rebirth", StringComparison.OrdinalIgnoreCase));
        var before = Elixirs(p.Pm);
        var gold = p.Pm.Backpack.GetAmount(typeof(Gold));
        dashboard.OnResponse(p.Ns, Press(34));
        Assert.Equal(before + 1, Elixirs(p.Pm));
        Assert.Equal(gold - OutridersGuildmasterGump.ElixirMemberPrice, p.Pm.Backpack.GetAmount(typeof(Gold)));
    }
}
