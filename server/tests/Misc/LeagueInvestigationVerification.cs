// LeagueInvestigationVerification.cs
//
// cc-P629 (P565 design, stage 1): the League investigation catalog, the warrant item and the marker seeder. Notes in
// shard-migration notes/cc-P629-build-league-warrants.md.
//
// Facts:
//   1. The catalog is closed and sound: two encounters, each an exact existing mini-boss type at a known site; Chase's
//      numbers (500 AP, 300 Renown, 24 hours); the switch starts off.
//   2. Catalog validation names a duplicate key, a dangling site and a leash that reaches a natural mini-boss spawner.
//   3. A site whose policy area reaches a guarded region or the Void Pool is refused; a dungeon or no region is not.
//   4. The warrant's use checks, in order: another account, not in the backpack, an unknown encounter, reserved,
//      unregistered, wrong facet, too far, then the switch. Each refusal names its cause; none changes the warrant.
//   5. The warrant is bound: another account cannot lift it, take it in trade, or receive it in a pack; it cannot be
//      dropped on the ground or into a pet's pack; the owner's other character may hold it in its bank.
//   6. The warrant survives a save and load with its account, encounter, receipt and state.
//   7. The seeder: a dry run places nothing; a run places a marker at each usable site and none twice; a site under a
//      guarded region gets no marker and is reported.
//   cc-P647 (the P629 and P641 reviews):
//   8. Off the test shard the seeder refuses and places nothing.
//   9. The warrant never goes into a bag, only the top of its account's backpacks and banks; one saved in a bag returns.
//  10. A facet whose map or statics files are not loaded is refused, said so, and gets no marker.
//  11. A town under a higher-priority allowed region, or only below the centre's Z, still refuses the site, and a spawn
//      tile standing in it is not counted.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.LeagueInvestigations;
using Server.Engines.VoidPool;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeagueInvestigationVerification
{
    private const string Siegebreaker = "investigation.siegebreaker";
    private const string HollowOak = "investigation.hollow-oak";

    public LeagueInvestigationVerification() => ShardTestHost.EnsureAccounts();

    // ---------------------------------------------------------------- helpers

    private static Account NewAccount() => new($"p629{Guid.NewGuid():N}"[..16], "p629-test-only");

    private static PlayerMobile NewCharacter(Account account, Point3D at, Map map, int slot = 0)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
        pm.AddItem(new Backpack());
        account[slot] = pm;
        pm.MoveToWorld(at, map);
        return pm;
    }

    private static void Cleanup(params Account[] accounts)
    {
        foreach (var account in accounts)
        {
            for (var i = 0; i < account.Length; i++)
            {
                account[i]?.Delete();
            }

            Accounts.Remove(account);
        }
    }

    private static void Register(Account account) => ClusterFAccountPersistence.GetOrCreate(account).SetFlag(ClusterFLeagueSystem.FlagJoined);

    private static LeagueInvestigationSite SiteOf(string encounterKey) =>
        LeagueInvestigationCatalog.SiteOf(LeagueInvestigationCatalog.FindEncounter(encounterKey)!)!;

    private static LeagueInvestigationWarrant WarrantIn(PlayerMobile pm, string key = HollowOak)
    {
        var w = new LeagueInvestigationWarrant(key, pm.Account.Username);
        pm.Backpack.DropItem(w);
        return w;
    }

    private static GuardedRegion Town(Point3D near, int offset)
    {
        var x = near.X + offset;
        var town = new GuardedRegion($"P629 town {Guid.NewGuid():N}"[..20], Map.Trammel, 50,
            new Rectangle3D(new Point3D(x, near.Y - 2, -128), new Point3D(x + 3, near.Y + 2, 127)));
        town.Register();
        return town;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheCatalogIsClosedAndCarriesChasesNumbers()
    {
        Assert.Empty(LeagueInvestigationCatalog.ValidateData());

        var keys = LeagueInvestigationCatalog.Encounters.Select(e => e.Key).ToArray();
        Assert.Equal(new[] { Siegebreaker, HollowOak }, keys);
        Assert.IsType<TheSiegebreaker>(Make(Siegebreaker));
        Assert.IsType<TheHollowOak>(Make(HollowOak));
        Assert.Equal(typeof(TheSiegebreaker), LeagueInvestigationCatalog.FindEncounter(Siegebreaker)!.BossType);
        Assert.Null(LeagueInvestigationCatalog.FindEncounter("TheSiegebreaker"));

        Assert.Equal(500, LeagueInvestigationCatalog.Settings.ApRequired);
        Assert.Equal(300, LeagueInvestigationCatalog.Settings.RenownPrice);
        Assert.Equal(TimeSpan.FromHours(24), LeagueInvestigationCatalog.Settings.Cooldown);
        Assert.False(LeagueInvestigationCatalog.Settings.Enabled);

        // The design's candidate lake-shore tile is inside the Void Pool's bounds; the catalog's Covetous site is not.
        var covetous = SiteOf(Siegebreaker);
        Assert.DoesNotContain(VoidPoolRegion.Bounds, b => b.Contains(new Point2D(covetous.Center.X, covetous.Center.Y)));
        Assert.Contains(VoidPoolRegion.Bounds, b => b.Contains(new Point2D(5440, 2023)));
    }

    private static BaseCreature Make(string key)
    {
        var boss = LeagueInvestigationCatalog.FindEncounter(key)!.Create();
        boss.Delete();
        return boss;
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void ValidationNamesDuplicatesDanglingSitesAndNaturalSpawnerOverlap()
    {
        var siegeRow = ClusterFLeagueBestiary.Spawners.First(r => r.Types.Contains(nameof(TheSiegebreaker)));
        var good = new LeagueInvestigationSite("good", "good", Map.Trammel, new Point3D(100, 100, 0), true, "");
        var onBoss = new LeagueInvestigationSite("on-boss", "on boss", Map.Trammel,
            new Point3D(siegeRow.X + LeagueInvestigationCatalog.Settings.Leash, siegeRow.Y, 0), true, "");
        var encounters = new[]
        {
            new LeagueInvestigationEncounter("a", "A", typeof(TheHollowOak), () => new TheHollowOak(), "good"),
            new LeagueInvestigationEncounter("a", "A", typeof(TheHollowOak), () => new TheHollowOak(), "nowhere")
        };

        var problems = LeagueInvestigationCatalog.ValidateData([good, onBoss], encounters);

        Assert.Contains(problems, p => p.Contains("encounter key a is used 2 times"));
        Assert.Contains(problems, p => p.Contains("unknown site nowhere"));
        Assert.Contains(problems, p => p.Contains("site on-boss leash overlaps") && p.Contains("Siegebreaker"));
        Assert.DoesNotContain(problems, p => p.Contains("site good"));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ASiteNearATownOrTheVoidPoolIsRefusedAndADungeonIsNot()
    {
        var site = new LeagueInvestigationSite("p629-open", "open ground", Map.Trammel, new Point3D(1700, 700, 0), true, "");
        Assert.DoesNotContain(LeagueInvestigationCatalog.CheckSite(site), p => p.Contains("region"));

        var dungeon = new DungeonRegion("P629 dungeon", Map.Trammel, 50,
            new Rectangle3D(new Point3D(1690, 690, -128), new Point3D(1710, 710, 127)));
        dungeon.Register();
        try
        {
            Assert.True(LeagueInvestigationCatalog.IsAllowedRegion(Region.Find(site.Center, Map.Trammel)));
            Assert.DoesNotContain(LeagueInvestigationCatalog.CheckSite(site), p => p.Contains("region"));
        }
        finally
        {
            dungeon.Unregister();
        }

        // 30 tiles east: outside the leash, inside the 32-tile policy area.
        var town = Town(site.Center, 30);
        try
        {
            Assert.Contains(LeagueInvestigationCatalog.CheckSite(site), p => p.Contains("GuardedRegion"));
        }
        finally
        {
            town.Unregister();
        }

        var pool = new VoidPoolRegion(null!, Map.Trammel);
        Assert.False(LeagueInvestigationCatalog.IsAllowedRegion(pool));

        var closed = site with { Enabled = false };
        Assert.Contains(LeagueInvestigationCatalog.CheckSite(closed), p => p.Contains("disabled"));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheWarrantRefusesInOrderAndNamesTheCause()
    {
        var site = SiteOf(HollowOak);
        var owner = NewAccount();
        var other = NewAccount();
        var pm = NewCharacter(owner, site.Center, site.Map);
        var stranger = NewCharacter(other, site.Center, site.Map);
        try
        {
            var w = WarrantIn(pm);
            var receipt = w.ReceiptId;

            Assert.Equal(WarrantUse.NotOwner, w.CheckUse(stranger));

            Assert.Equal(WarrantUse.NotRegistered, w.CheckUse(pm));
            Assert.Contains("registered League member", w.Message(pm, WarrantUse.NotRegistered));
            Register(owner);

            Assert.Equal(WarrantUse.FeatureOff, w.CheckUse(pm));
            Assert.Contains("not open yet", w.Message(pm, WarrantUse.FeatureOff));

            pm.MoveToWorld(new Point3D(site.Center.X + 5, site.Center.Y, site.Center.Z), site.Map);
            Assert.Equal(WarrantUse.TooFar, w.CheckUse(pm));
            Assert.Contains("5 tiles away", w.Message(pm, WarrantUse.TooFar));
            pm.MoveToWorld(new Point3D(site.Center.X + 4, site.Center.Y, site.Center.Z), site.Map);
            Assert.Equal(WarrantUse.FeatureOff, w.CheckUse(pm));

            pm.MoveToWorld(site.Center, Map.Felucca);
            Assert.Equal(WarrantUse.WrongFacet, w.CheckUse(pm));
            Assert.Contains("You are on Felucca", w.Message(pm, WarrantUse.WrongFacet));
            pm.MoveToWorld(site.Center, site.Map);

            LeagueInvestigationCatalog.Settings.Enabled = true;
            try
            {
                Assert.Equal(WarrantUse.Ok, w.CheckUse(pm));
            }
            finally
            {
                LeagueInvestigationCatalog.Settings.Enabled = false;
            }

            w.State = WarrantState.Reserved;
            Assert.Equal(WarrantUse.Reserved, w.CheckUse(pm));
            w.State = WarrantState.Ready;

            pm.BankBox.DropItem(w);
            Assert.Equal(WarrantUse.NotInBackpack, w.CheckUse(pm));
            pm.Backpack.DropItem(w);

            var stale = WarrantIn(pm, "investigation.withdrawn");
            Assert.Equal(WarrantUse.UnknownEncounter, stale.CheckUse(pm));
            Assert.Contains("no longer offered", stale.Message(pm, WarrantUse.UnknownEncounter));
            Assert.Contains("withdrawn", stale.DefaultName);

            // Checking spent nothing and changed nothing.
            Assert.False(w.Deleted);
            Assert.Equal(receipt, w.ReceiptId);
            Assert.Equal(WarrantState.Ready, w.State);
        }
        finally
        {
            Cleanup(owner, other);
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheWarrantIsBoundToItsAccount()
    {
        var at = new Point3D(1700, 700, 0);
        var owner = NewAccount();
        var other = NewAccount();
        var pm = NewCharacter(owner, at, Map.Trammel);
        var alt = NewCharacter(owner, at, Map.Trammel, 1);
        var stranger = NewCharacter(other, at, Map.Trammel);
        var pet = new Horse { Controlled = true, ControlMaster = pm };
        pet.AddItem(new Backpack());
        pet.MoveToWorld(at, Map.Trammel);
        try
        {
            var w = WarrantIn(pm);
            Assert.Equal(LootType.Blessed, w.LootType);

            var reject = LRReason.Inspecific;
            Assert.False(w.CheckLift(stranger, w, ref reject));
            Assert.Equal(LRReason.CannotLift, reject);
            alt.Backpack.DropItem(w); // the owner's other character, from its own pack
            Assert.True(w.CheckLift(alt, w, ref reject));

            Assert.False(w.AllowSecureTrade(pm, stranger, stranger, true));
            Assert.False(w.OnDroppedToWorld(pm, at));
            Assert.False(w.OnDroppedInto(pm, stranger.Backpack, Point3D.Zero));
            Assert.False(w.OnDroppedInto(pm, pet.Backpack, Point3D.Zero));
            Assert.False(w.OnDroppedToMobile(pm, stranger));
            // The binding lets it rest in the owner's other character's bank (the drop itself also needs the bank
            // open, the engine's rule); it never lets it rest with another account.
            Assert.True(AccountBinding.MayRestIn(alt.BankBox, w.BoundAccount));
            Assert.False(AccountBinding.MayRestIn(stranger.BankBox, w.BoundAccount));
            Assert.Equal(owner.Username, w.BoundAccount);
        }
        finally
        {
            pet.Delete();
            Cleanup(owner, other);
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheWarrantSurvivesASaveAndLoad()
    {
        var original = new LeagueInvestigationWarrant(Siegebreaker, "p629-saved") { State = WarrantState.Reserved };

        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = new LeagueInvestigationWarrant(original.Serial);
        try
        {
            var receipt = original.ReceiptId;
            original.Delete();
            copy.Deserialize(new BufferReader(buffer));

            Assert.Equal("p629-saved", copy.BoundAccount);
            Assert.Equal(Siegebreaker, copy.EncounterKey);
            Assert.Equal(receipt, copy.ReceiptId);
            Assert.Equal(32, copy.ReceiptId.Length);
            Assert.Equal(WarrantState.Reserved, copy.State);
            Assert.Equal("Investigation Warrant: The Siegebreaker", copy.DefaultName);
        }
        finally
        {
            copy.Delete();
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheSeederIsExplicitIdempotentAndRefusesARefusedSite()
    {
        var yew = LeagueInvestigationCatalog.FindSite(LeagueInvestigationCatalog.YewWoods)!;
        var covetous = LeagueInvestigationCatalog.FindSite(LeagueInvestigationCatalog.CovetousHall)!;
        using var shard = new TestShard(true);
        using var terrain = MapData.FlatFixture();
        try
        {
            var dry = LeagueInvestigationSeeder.Seed(true);
            Assert.Equal(2, dry.Placed);
            Assert.Null(LeagueInvestigationSeeder.FindMarker(yew));
            Assert.Null(LeagueInvestigationSeeder.FindMarker(covetous));

            var town = Town(yew.Center, 20);
            try
            {
                var first = LeagueInvestigationSeeder.Seed(false);
                Assert.Equal(1, first.Placed);
                Assert.Contains(first.Refused, p => p.Contains("yew-woods") && p.Contains("GuardedRegion"));
                Assert.Null(LeagueInvestigationSeeder.FindMarker(yew));
            }
            finally
            {
                town.Unregister();
            }

            var second = LeagueInvestigationSeeder.Seed(false);
            Assert.Equal(1, second.Placed);
            Assert.Equal(1, second.Present);

            var third = LeagueInvestigationSeeder.Seed(false);
            Assert.Equal(0, third.Placed);
            Assert.Equal(2, third.Present);

            var marker = LeagueInvestigationSeeder.FindMarker(covetous)!;
            Assert.False(marker.Movable);
            Assert.Equal(covetous.Center, marker.Location);
            Assert.Equal(LeagueInvestigationMarker.Art, marker.ItemID);
        }
        finally
        {
            LeagueInvestigationSeeder.FindMarker(yew)?.Delete();
            LeagueInvestigationSeeder.FindMarker(covetous)?.Delete();
        }
    }

    // ---------------------------------------------------------------- 8 (cc-P647, P629 review)

    [Fact]
    public void TheSeederRefusesOffTheTestShard()
    {
        var yew = LeagueInvestigationCatalog.FindSite(LeagueInvestigationCatalog.YewWoods)!;
        var covetous = LeagueInvestigationCatalog.FindSite(LeagueInvestigationCatalog.CovetousHall)!;
        using var shard = new TestShard(false);
        try
        {
            var live = LeagueInvestigationSeeder.Seed(false);
            Assert.Equal(0, live.Placed);
            Assert.Contains("test", live.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Null(LeagueInvestigationSeeder.FindMarker(yew));
            Assert.Null(LeagueInvestigationSeeder.FindMarker(covetous));
            Assert.Equal(0, LeagueInvestigationSeeder.Seed(true).Placed);
        }
        finally
        {
            LeagueInvestigationSeeder.FindMarker(yew)?.Delete();
            LeagueInvestigationSeeder.FindMarker(covetous)?.Delete();
        }
    }

    // ---------------------------------------------------------------- 9 (cc-P647, P641 finding 1)

    [Fact]
    public void TheWarrantRestsOnlyInItsOwnersBackpackOrBankNeverInABag()
    {
        var at = new Point3D(1700, 700, 0);
        var owner = NewAccount();
        var pm = NewCharacter(owner, at, Map.Trammel);
        var alt = NewCharacter(owner, at, Map.Trammel, 1);
        try
        {
            var w = WarrantIn(pm);
            var receipt = w.ReceiptId;
            var bag = new Bag();
            pm.Backpack.DropItem(bag);
            var bankBag = new Bag();
            pm.BankBox.DropItem(bankBag);

            // A bag can be dropped or traded without asking what is inside it, so the warrant never goes into one.
            Assert.False(w.OnDroppedInto(pm, bag, new Point3D(40, 40, 0)));
            Assert.False(w.OnDroppedOnto(pm, bag));
            Assert.False(w.OnDroppedInto(pm, bankBag, new Point3D(40, 40, 0)));
            Assert.Same(pm.Backpack, w.Parent);

            // The owner's backpack and bank themselves, and the other character's, are fine (a drop into a bank also
            // needs the bank open, the engine's rule).
            Assert.True(w.OnDroppedInto(pm, pm.Backpack, new Point3D(40, 40, 0)));
            Assert.Same(pm.Backpack, w.Parent);
            Assert.True(w.IsRestingPlace(pm.BankBox));
            Assert.True(w.IsRestingPlace(alt.Backpack));
            Assert.True(w.IsRestingPlace(alt.BankBox));
            Assert.False(w.IsRestingPlace(bag));

            // A warrant saved inside a bag before this rule comes back to the top of the pack or bank it was in.
            bag.DropItem(w);
            w.ReturnToRoot();
            Assert.Same(pm.Backpack, w.Parent);
            bankBag.DropItem(w);
            w.ReturnToRoot();
            Assert.Same(pm.BankBox, w.Parent);
            Assert.Equal(receipt, w.ReceiptId);
            Assert.False(w.Deleted);

            // Ordinary bags are untouched by any of this.
            Assert.Same(pm.Backpack, bag.Parent);
            Assert.Same(pm.BankBox, bankBag.Parent);
        }
        finally
        {
            Cleanup(owner);
        }
    }

    // ---------------------------------------------------------------- 10 (cc-P647, P641 finding 2)

    [Fact]
    public void ASiteOnUnloadedMapDataIsRefusedAndGetsNoMarker()
    {
        Assert.Contains("land", LeagueInvestigationCatalog.MissingMapData("Trammel", false, true, true));
        Assert.Contains("statics", LeagueInvestigationCatalog.MissingMapData("Trammel", true, false, true));
        Assert.Contains("statics", LeagueInvestigationCatalog.MissingMapData("Trammel", true, true, false));
        Assert.Null(LeagueInvestigationCatalog.MissingMapData("Trammel", true, true, true));

        var yew = LeagueInvestigationCatalog.FindSite(LeagueInvestigationCatalog.YewWoods)!;
        var covetous = LeagueInvestigationCatalog.FindSite(LeagueInvestigationCatalog.CovetousHall)!;
        using var shard = new TestShard(true);
        try
        {
            // The test host opens no map files: Trammel exists, its terrain is the engine's blank fallback. Refused, and
            // said so, not reported as a crowded site.
            var host = LeagueInvestigationSeeder.Seed(false);
            Assert.Equal(0, host.Placed);
            Assert.Contains(host.Refused, p => p.Contains("yew-woods") && p.Contains("not loaded"));
            Assert.Contains(host.Refused, p => p.Contains("covetous-hall") && p.Contains("not loaded"));
            Assert.DoesNotContain(host.Refused, p => p.Contains("safe spawn tiles"));
            Assert.Empty(LeagueInvestigationCatalog.SpawnTiles(yew));
            Assert.Null(LeagueInvestigationSeeder.FindMarker(yew));
            Assert.Null(LeagueInvestigationSeeder.FindMarker(covetous));

            // Land loaded but the statics missing (walls and rocks would vanish): refused too.
            using (new MapData(_ => LeagueInvestigationCatalog.MissingMapData("Trammel", true, false, false)))
            {
                var noStatics = LeagueInvestigationSeeder.Seed(false);
                Assert.Equal(0, noStatics.Placed);
                Assert.Contains(noStatics.Refused, p => p.Contains("yew-woods") && p.Contains("statics"));
                Assert.Empty(LeagueInvestigationCatalog.SpawnTiles(yew));
                Assert.Null(LeagueInvestigationSeeder.FindMarker(yew));
            }
        }
        finally
        {
            LeagueInvestigationSeeder.FindMarker(yew)?.Delete();
            LeagueInvestigationSeeder.FindMarker(covetous)?.Delete();
        }
    }

    // ---------------------------------------------------------------- 11 (cc-P647, P641 finding 3)

    [Fact]
    public void ARefusedRegionHiddenByPriorityOrHeightStillRefusesTheSite()
    {
        var site = new LeagueInvestigationSite("p647-open", "open ground", Map.Trammel, new Point3D(1760, 760, 20), true, "");
        using var terrain = MapData.FlatFixture();
        Assert.Empty(LeagueInvestigationCatalog.CheckSite(site));

        // An allowed, parentless region at a higher priority laid over a town: every point finds the allowed one first.
        var x = site.Center.X + 20;
        var town = new GuardedRegion("P647 town", Map.Trammel, 50,
            new Rectangle3D(new Point3D(x, site.Center.Y - 2, -128), new Point3D(x + 3, site.Center.Y + 2, 127)));
        var overlay = new BaseRegion("P647 overlay", Map.Trammel, 100,
            new Rectangle3D(new Point3D(x - 2, site.Center.Y - 4, -128), new Point3D(x + 6, site.Center.Y + 4, 127)));
        town.Register();
        overlay.Register();
        try
        {
            Assert.True(LeagueInvestigationCatalog.IsAllowedRegion(Region.Find(new Point3D(x + 1, site.Center.Y, 0), Map.Trammel)));
            Assert.Contains(LeagueInvestigationCatalog.CheckSite(site), p => p.Contains("P647 town") && p.Contains("GuardedRegion"));
        }
        finally
        {
            overlay.Unregister();
            town.Unregister();
        }

        // A town only below the centre's Z, where the ground (Z 0 here) actually is: the spawn tiles stand in it.
        var low = new GuardedRegion("P647 low town", Map.Trammel, 50,
            new Rectangle3D(new Point3D(site.Center.X - 6, site.Center.Y - 6, -128), new Point3D(site.Center.X + 7, site.Center.Y + 7, 10)));
        low.Register();
        try
        {
            Assert.False(low.Contains(site.Center));
            Assert.Contains(LeagueInvestigationCatalog.CheckSite(site), p => p.Contains("P647 low town"));
            Assert.Empty(LeagueInvestigationCatalog.SpawnTiles(site));
        }
        finally
        {
            low.Unregister();
        }

        Assert.Empty(LeagueInvestigationCatalog.CheckSite(site));
    }

    // The explicit terrain fixture for the placement facts: the host has no map files, so these declare its blank
    // fallback ground (land 0 at Z 0 everywhere, no statics) to be the facet's data.
    private sealed class MapData : IDisposable
    {
        private readonly Func<Map, string> _was = LeagueInvestigationCatalog.MapDataProblem;

        public MapData(Func<Map, string> problem) => LeagueInvestigationCatalog.MapDataProblem = problem;

        public static MapData FlatFixture() => new(_ => null);

        public void Dispose() => LeagueInvestigationCatalog.MapDataProblem = _was;
    }

    private sealed class TestShard : IDisposable
    {
        private readonly Func<bool> _was = LeagueInvestigationSeeder.IsTestShard;

        public TestShard(bool on) => LeagueInvestigationSeeder.IsTestShard = () => on;

        public void Dispose() => LeagueInvestigationSeeder.IsTestShard = _was;
    }
}
