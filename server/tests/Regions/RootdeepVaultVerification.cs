// RootdeepVaultVerification.cs
//
// cc-P141 (F-45, Part D): the tutorial Vault (server/customizations/Regions/RootdeepVault.cs) and the Burrows' seeder
// (server/customizations/ClusterFRootdeepSeeder.cs). Notes: shard-migration notes/cc-P141-rootdeep-burrows.md.
//
// Facts:
//   V1. The Vault is a child region of the Burrows over x 5142-5178, y 1132-1159 exactly; inside it the Burrows' rules
//       still hold (the Young death teleport finds the Burrows' Entrance, recall in is refused); the Burrows' player
//       name is the Rootdeep Burrows.
//   V2. The doorway refuses a non-participant and a graduate, each with its Echo line, and a non-participant's pet;
//       it lets a participant and their pet in; staff are exempt; teleport in from the landing is refused.
//   V3. The crawlway opens on the Overdraft's death and only then: shut before, shut after another Vault creature dies;
//       on the Overdraft's death every participant who damaged it (or whose pet did) is credited and no one else; a
//       credited player goes through to the Warren with their pet, an uncredited one does not; it shuts again.
//   V4. The Vault seals behind a credited player: the doorway refuses them; a graduate or credited player who logs in
//       inside is put on the landing, a participant is not.
//   V5. The seeder refuses on void land; then places every spawner row (types resolve, counts, Group, each inside its
//       zone, none over 3 where a Young player walks), the landing's working ankh and the crawlway; a re-run places none.
//   V6. The crawlway round-trips the serializer and comes back shut.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;
using Server.Tutorial;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RootdeepVaultVerification
{
    private static bool _startupHooksRun;
    private readonly ITestOutputHelper _out;

    private static readonly Point3D InVault = new(5160, 1150, 0);
    private static readonly Point3D OnLanding = new(5190, 1138, 0);
    private static readonly Point3D Doorway = RootdeepVault.Doorway;
    private static readonly Point3D JustInside = new(5178, 1137, 0);

    public RootdeepVaultVerification(ITestOutputHelper output)
    {
        _out = output;
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }

        ClusterFTutorial.Enabled = true;
        RootdeepVaultRegion.Initialize();
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account;
        public readonly PlayerMobile Pm;

        public Player(Point3D at, AccessLevel level = AccessLevel.Player)
        {
            Account = new Account($"p141{Guid.NewGuid():N}"[..16], "p141-test-only");
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Trainee", AccessLevel = level };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(at, Map.Trammel);
            Pm.Hits = Pm.HitsMax;
        }

        public TutorialRecord Record => ClusterFTutorial.RecordOf(Pm)!;

        public Player Participant()
        {
            Assert.True(ClusterFTutorial.TryEnter(Pm));
            return this;
        }

        public Player Graduate()
        {
            Record.State = TutorialState.Graduated;
            return this;
        }

        public void Dispose()
        {
            for (var i = 0; i < Account.Length; i++)
            {
                Account[i]?.Delete();
            }

            Accounts.Remove(Account);
        }
    }

    private static BaseCreature PetOf(PlayerMobile pm)
    {
        var dog = new Dog();
        dog.MoveToWorld(pm.Location, pm.Map);
        dog.SetControlMaster(pm);
        dog.ControlOrder = OrderType.Follow;
        return dog;
    }

    private static bool WalkIn(Mobile m) =>
        RootdeepVaultRegion.Instance!.OnMoveInto(m, Direction.West, JustInside, Doorway);

    private static (bool Ok, string? Line) WalkInWithLine(Mobile m)
    {
        var ok = RootdeepVault.MayEnter(m, out var line);
        Assert.Equal(ok, WalkIn(m));
        return (ok, line);
    }

    // ---------------------------------------------------------------- V1

    [Fact]
    public void TheVaultIsAChildOfTheBurrowsOverItsDraftExactly()
    {
        var vault = Region.Find(InVault, Map.Trammel);
        Assert.Same(RootdeepVaultRegion.Instance, vault);
        Assert.Same(OldHavenBurrowsRegion.Instance, vault.Parent);
        Assert.Equal("the Rootdeep Burrows", OldHavenBurrowsRegion.RegionName);
        Assert.Equal(OldHavenBurrowsRegion.HoleEntrance, vault.GetRegion<DungeonRegion>()!.Entrance);

        foreach (var corner in new[] { new Point3D(5142, 1132, 0), new Point3D(5178, 1132, 0), new Point3D(5142, 1159, 0), new Point3D(5178, 1159, 0) })
        {
            Assert.IsType<RootdeepVaultRegion>(Region.Find(corner, Map.Trammel));
        }

        // The doorway's wall (x 5179), the Warren's wall (y 1160) and the landing are the Burrows, not the Vault.
        foreach (var past in new[] { Doorway, new Point3D(5141, 1140, 0), new Point3D(5160, 1160, 0), new Point3D(5160, 1131, 0), OnLanding })
        {
            Assert.IsType<OldHavenBurrowsRegion>(Region.Find(past, Map.Trammel));
        }

        // The Burrows' travel rules still apply inside: recall in is refused with the Burrows' own line.
        using var p = new Player(new Point3D(1220, 1220, 0));
        Assert.False(SpellHelper.CheckTravel(p.Pm, Map.Trammel, InVault, TravelCheckType.RecallTo, out var msg));
        Assert.Equal(OldHavenBurrowsRegion.RecallInMessage, msg?.String);
    }

    // ---------------------------------------------------------------- V2

    [Fact]
    public void TheDoorwayLetsInOnlyParticipantsAndTheirPets()
    {
        using var never = new Player(OnLanding);
        never.Record.State = TutorialState.Skipped;
        using var graduate = new Player(OnLanding);
        graduate.Participant().Graduate();
        using var trainee = new Player(OnLanding);
        trainee.Participant();
        using var staff = new Player(OnLanding, AccessLevel.GameMaster);

        var refused = WalkInWithLine(never.Pm);
        Assert.False(refused.Ok);
        Assert.Equal(RootdeepVault.NotParticipantLine, refused.Line);

        refused = WalkInWithLine(graduate.Pm);
        Assert.False(refused.Ok);
        Assert.Equal(RootdeepVault.GraduateLine, refused.Line);

        var strayPet = PetOf(never.Pm);
        Assert.False(WalkIn(strayPet));

        Assert.True(WalkIn(trainee.Pm));
        var pet = PetOf(trainee.Pm);
        Assert.True(WalkIn(pet));
        Assert.True(WalkIn(staff.Pm));

        // A wild creature keeps the region's ordinary walk rule (Region.OnMoveInto).
        var rat = new PennyRat();
        rat.MoveToWorld(Doorway, Map.Trammel);
        Assert.True(WalkIn(rat));

        // Teleport in from the landing: refused for a participant too (the doorway is the way in); staff may.
        trainee.Pm.MoveToWorld(OnLanding, Map.Trammel);
        Assert.False(SpellHelper.CheckTravel(trainee.Pm, Map.Trammel, JustInside, TravelCheckType.TeleportTo, out var msg));
        Assert.Equal(RootdeepVault.TeleportInMessage, msg?.String);
        Assert.True(SpellHelper.CheckTravel(staff.Pm, Map.Trammel, JustInside, TravelCheckType.TeleportTo, out _));

        foreach (var m in new Mobile[] { strayPet, pet, rat })
        {
            m.Delete();
        }
    }

    // ---------------------------------------------------------------- V3

    [Fact]
    public void TheCrawlwayOpensOnTheOverdraftsDeathAndOnlyThen()
    {
        using var fighter = new Player(InVault);
        fighter.Participant();
        using var petFighter = new Player(InVault);
        petFighter.Participant();
        using var bystander = new Player(InVault);
        bystander.Participant();

        var crawlway = new RootdeepCrawlway();
        crawlway.MoveToWorld(RootdeepVault.CrawlwayMouth, Map.Trammel);
        var made = new List<IEntity> { crawlway };
        try
        {
            Assert.False(crawlway.IsOpen);
            Assert.Equal(RootdeepCrawlway.ClosedId, crawlway.ItemID);

            // Another Vault creature's death opens nothing.
            var rat = new HoardingRat();
            made.Add(rat);
            rat.MoveToWorld(InVault, Map.Trammel);
            rat.Damage(rat.Hits + 50, fighter.Pm);
            Assert.False(crawlway.IsOpen);
            Assert.False(RootdeepVault.HasCleared(fighter.Pm));

            // Shut: the crawlway refuses even a would-be graduate.
            fighter.Pm.MoveToWorld(new Point3D(5160, 1158, 0), Map.Trammel);
            Assert.False(RootdeepVault.TryCrawl(fighter.Pm, crawlway));

            var overdraft = new TheOverdraft();
            made.Add(overdraft);
            overdraft.MoveToWorld(InVault, Map.Trammel);
            var dog = PetOf(petFighter.Pm);
            made.Add(dog);
            overdraft.Damage(5, dog);
            Assert.False(crawlway.IsOpen); // hurt is not dead
            overdraft.Damage(overdraft.Hits + 50, fighter.Pm);
            Assert.False(overdraft.Alive);

            Assert.True(crawlway.IsOpen);
            Assert.Equal(RootdeepCrawlway.OpenId, crawlway.ItemID);
            Assert.True(RootdeepVault.HasCleared(fighter.Pm));
            Assert.True(RootdeepVault.HasCleared(petFighter.Pm)); // the pet's blow credits its master
            Assert.False(RootdeepVault.HasCleared(bystander.Pm));

            // Through: the credited fighter (and their pet), to the Warren; not the bystander.
            Assert.False(RootdeepVault.TryCrawl(bystander.Pm, crawlway));
            Assert.True(RootdeepVault.IsInside(bystander.Pm.Map, bystander.Pm.Location));

            petFighter.Pm.MoveToWorld(new Point3D(5160, 1158, 0), Map.Trammel);
            dog.MoveToWorld(new Point3D(5161, 1158, 0), Map.Trammel);
            Assert.True(RootdeepVault.TryCrawl(petFighter.Pm, crawlway));
            Assert.Equal(RootdeepVault.CrawlwayExit, petFighter.Pm.Location);
            Assert.Equal(RootdeepVault.CrawlwayExit, dog.Location);
            Assert.IsType<OldHavenBurrowsRegion>(Region.Find(petFighter.Pm.Location, Map.Trammel)); // the Warren

            crawlway.Close();
            Assert.False(crawlway.IsOpen);
            Assert.Equal(RootdeepCrawlway.ClosedId, crawlway.ItemID);
            Assert.False(RootdeepVault.TryCrawl(fighter.Pm, crawlway));
        }
        finally
        {
            foreach (var e in made)
            {
                e.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- V4

    [Fact]
    public void TheVaultSealsBehindItsGraduatesAndTurnsThemOutAtLogin()
    {
        using var cleared = new Player(OnLanding);
        cleared.Participant().Record.StationsDone.Add(RootdeepVault.VaultClearedKey);
        Assert.Equal((false, RootdeepVault.SealedLine), WalkInWithLine(cleared.Pm));
        Assert.True(ClusterFTutorial.IsParticipant(cleared.Pm)); // still in the tutorial, still sealed out

        using var trainee = new Player(InVault);
        trainee.Participant();
        Assert.False(RootdeepVault.TurnOut(trainee.Pm));
        Assert.Equal(InVault, trainee.Pm.Location);

        cleared.Pm.MoveToWorld(InVault, Map.Trammel);
        RootdeepVault.OnLogin(cleared.Pm);
        Assert.Equal(RootdeepVault.LandingSpot, cleared.Pm.Location);

        using var graduate = new Player(InVault);
        graduate.Participant().Graduate();
        Assert.True(RootdeepVault.TurnOut(graduate.Pm));
        Assert.Equal(RootdeepVault.LandingSpot, graduate.Pm.Location);
        Assert.IsType<OldHavenBurrowsRegion>(Region.Find(RootdeepVault.LandingSpot, Map.Trammel));
    }

    // ---------------------------------------------------------------- V5

    private static readonly Dictionary<string, Rectangle2D> Zones = new()
    {
        ["Vault"] = new Rectangle2D(new Point2D(5142, 1132), new Point2D(5179, 1160)),
        ["Warren"] = new Rectangle2D(new Point2D(5142, 1162), new Point2D(5243, 1193)),
        ["Brood Pit"] = new Rectangle2D(new Point2D(5212, 1168), new Point2D(5241, 1189)),
        ["Tangle"] = new Rectangle2D(new Point2D(5142, 1198), new Point2D(5189, 1229)),
        ["Dawnseam"] = new Rectangle2D(new Point2D(5194, 1198), new Point2D(5243, 1229)),
        ["Gloom"] = new Rectangle2D(new Point2D(5150, 1234), new Point2D(5237, 1261)),
        ["Gloom lair"] = new Rectangle2D(new Point2D(5184, 1238), new Point2D(5227, 1257)),
    };

    [Fact]
    public void TheSeederPlacesEverySpawnerTheAnkhAndTheCrawlwayOnceTheMapIsBuilt()
    {
        var mapBuilt = ClusterFRootdeepSeeder.MapBuilt;
        var placed = new List<Item>();
        try
        {
            // The data: every type resolves, every row sits in its zone, packs respect the Young limit.
            foreach (var name in ClusterFRootdeepSeeder.AllTypeNames())
            {
                Assert.True(AssemblyHandler.FindTypeByName(name) is { } t && typeof(BaseCreature).IsAssignableFrom(t), name);
            }

            foreach (var row in ClusterFRootdeepSeeder.Spawners)
            {
                Assert.True(Zones[row.Zone].Contains(row.Location), $"{row.Label} at {row.Location} is outside {row.Zone}");
                Assert.True(row.Count <= OldHavenBurrowsRegion.MaxHostilesOnYoung, $"{row.Label}: {row.Count}");
                Assert.True(row.MinMinutes <= row.MaxMinutes, row.Label);
                Assert.Equal(row.Zone == "Vault", RootdeepVault.IsInside(Map.Trammel, new Point3D(row.X, row.Y, 0)));
            }

            var overdraftRow = ClusterFRootdeepSeeder.Spawners.Single(r => r.Types[0] == nameof(TheOverdraft));
            Assert.True(TimeSpan.FromMinutes(overdraftRow.MinMinutes) >= RootdeepVault.CrawlwayOpenFor);

            // Void land: refused, nothing placed.
            ClusterFRootdeepSeeder.MapBuilt = () => false;
            var refused = ClusterFRootdeepSeeder.Seed(false);
            Assert.True(refused.Refused);
            Assert.Equal((0, 0, 0), (refused.Spawners, refused.Ankhs, refused.Crawlways));
            Assert.Null(ClusterFRootdeepSeeder.FindCrawlway(Map.Trammel));

            ClusterFRootdeepSeeder.MapBuilt = () => true;
            var dry = ClusterFRootdeepSeeder.Seed(true);
            Assert.Equal((ClusterFRootdeepSeeder.Spawners.Length, 1, 1, 2), (dry.Spawners, dry.Ankhs, dry.Crawlways, dry.Teleporters));
            Assert.Null(ClusterFRootdeepSeeder.FindCrawlway(Map.Trammel));

            var first = ClusterFRootdeepSeeder.Seed(false);
            _out.WriteLine(first.Message);
            Assert.Equal((ClusterFRootdeepSeeder.Spawners.Length, 1, 1, 2, 0), (first.Spawners, first.Ankhs, first.Crawlways, first.Teleporters, first.Present));

            // cc-P188: the way in, both ends (RootdeepWayInVerification has the walk through them).
            foreach (var row in ClusterFRootdeepSeeder.Teleporters)
            {
                var t = ClusterFRootdeepSeeder.FindTeleporter(Map.Trammel, row);
                Assert.NotNull(t);
                placed.Add(t);
            }

            foreach (var row in ClusterFRootdeepSeeder.Spawners)
            {
                var s = ClusterFRootdeepSeeder.FindSpawner(Map.Trammel, row);
                Assert.NotNull(s);
                placed.Add(s);
                Assert.Equal(row.Count, s.Count);
                Assert.Equal(row.Group, s.Group);
                Assert.Equal(row.HomeRange, s.HomeRange);
                Assert.Equal(row.Types, s.Entries.Select(e => e.SpawnedName).ToArray());
                Assert.Equal(TimeSpan.FromMinutes(row.MinMinutes), s.MinDelay);
            }

            var ankhAt = new Point3D(ClusterFRootdeepSeeder.AnkhAt.X, ClusterFRootdeepSeeder.AnkhAt.Y, 0);
            Assert.True(ClusterFAnkhs.WorkingAnkhNear(Map.Trammel, ankhAt));
            foreach (var item in Map.Trammel.GetItemsInRange(ankhAt, 2))
            {
                if (ClusterFAnkhs.IsWorkingAnkh(item))
                {
                    placed.Add(item);
                }
            }

            Assert.Contains(placed, i => i is AnkhWest);
            var crawlway = ClusterFRootdeepSeeder.FindCrawlway(Map.Trammel);
            Assert.NotNull(crawlway);
            placed.Add(crawlway);
            Assert.False(crawlway.IsOpen);

            var again = ClusterFRootdeepSeeder.Seed(false);
            Assert.Equal((0, 0, 0, 0, ClusterFRootdeepSeeder.Spawners.Length + 4), (again.Spawners, again.Ankhs, again.Crawlways, again.Teleporters, again.Present));
        }
        finally
        {
            ClusterFRootdeepSeeder.MapBuilt = mapBuilt;
            foreach (var item in placed)
            {
                if (item is Spawner s)
                {
                    s.RemoveSpawns();
                }

                item.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- V6

    [Fact]
    public void TheCrawlwayRoundTripsAndComesBackShut()
    {
        var crawlway = new RootdeepCrawlway();
        crawlway.Open(TimeSpan.FromMinutes(2));
        Assert.True(crawlway.IsOpen);

        var buffer = new byte[4096];
        var writer = new BufferWriter(buffer, true);
        crawlway.Serialize(writer);
        writer.Flush();

        var copy = (RootdeepCrawlway)Activator.CreateInstance(typeof(RootdeepCrawlway), crawlway.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        Assert.False(copy.IsOpen); // the open state is never saved
        Assert.False(copy.Movable);

        crawlway.Delete();
    }
}
