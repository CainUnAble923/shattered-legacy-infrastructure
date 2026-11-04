// PeacockVerification.cs
//
// cc-P793: the Peacock (server/customizations/Mobiles/Mounts/Peacock.cs, PeacockTable.cs, PeacockSystem.cs). Notes in
// shard-migration notes/cc-P793-peacock-mount.md.
//
// Facts:
//   1. It stands on body 1440 typed Animal (so it idles and cannot open doors) with mount item 0x3ECF and a desert ostard's
//      taming numbers and food; the table's defaults are the ostard's numbers.
//   2. A real tame (Animal Taming's own target and timer) makes it the tamer's.
//   3. Its owner rides it and dismounts; a stranger cannot ride it.
//   4. It round-trips the generated serializer, tamed and while ridden.
//   5. The leash shrinks it to its own figurine, 0xA0C0, and pinned's rows stay as they were.
//   6. The seed gives each meadow one spawner, a second seed adds nothing, and an existing peacock spawner is kept.
//   7. No meadow lies under an SoS fence.
//   8. The table: new peacocks take its numbers, the shipped defaults round-trip, a bad file is refused and the table in
//      force stays, and "enabled": false seeds nothing.
//   9. Nothing is in its pack or loot: it grants nothing.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Server.Regions;
using Server.SkillHandlers;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PeacockVerification
{
    // Its own spot: (1000, 1000) is shared, and another fact's leftover chest mimic there killed the tamer mid-tame.
    private static readonly Point3D Here = new(1793, 1179, 0);

    private readonly ITestOutputHelper _out;

    public PeacockVerification(ITestOutputHelper output) => _out = output;

    private static PlayerMobile Player(string name)
    {
        var pm = new PlayerMobile { Player = true, Name = name, Body = 400, FollowersMax = 5 };
        pm.MoveToWorld(Here, Map.TerMur);
        pm.AddItem(new Backpack());
        return pm;
    }

    private static Peacock Tamed(PlayerMobile pm)
    {
        var bird = new Peacock();
        bird.MoveToWorld(Here, Map.TerMur);
        bird.SetControlMaster(pm);
        return bird;
    }

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(typeof(T), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    private static List<Spawner> MeadowSpawners()
    {
        var found = new List<Spawner>();
        foreach (var spot in PeacockTable.Current.Spawns)
        {
            foreach (var item in Map.Trammel.GetItemsInRange(spot.Location, PeacockSystem.SearchRange))
            {
                if (item is Spawner s && !found.Contains(s))
                {
                    found.Add(s);
                }
            }
        }

        return found;
    }

    private static void ClearMeadows()
    {
        foreach (var s in MeadowSpawners())
        {
            s.Delete();
        }
    }

    [Fact]
    public void ItIsAnAnimalOnItsOwnBodyWithAnOstardsTamingNumbersAndFood()
    {
        ShardBodyTypes.Apply();

        var bird = new Peacock();
        var ostard = new DesertOstard();
        _out.WriteLine($"name='{bird.Name}' body={(int)bird.Body} type={bird.Body.Type} item=0x{bird.ItemID:X} " +
                       $"tame={bird.MinTameSkill} slots={bird.ControlSlots} hits={bird.HitsMax} doors={bird.CanOpenDoors}");

        Assert.Contains(ShardBodyTypes.Entries, e => e.Body == 1440 && e.Type == BodyType.Animal);
        Assert.Equal(1440, (int)bird.Body);
        Assert.Equal(BodyType.Animal, new Body(1440).Type);
        Assert.False(bird.CanOpenDoors);
        Assert.Equal(0x3ECF, bird.ItemID);
        Assert.Equal("a peacock", bird.Name);
        Assert.Equal("a peacock corpse", bird.CorpseName);

        Assert.True(bird.Tamable);
        Assert.Equal(ostard.MinTameSkill, bird.MinTameSkill);
        Assert.Equal(29.1, bird.MinTameSkill);
        Assert.Equal(ostard.ControlSlots, bird.ControlSlots);
        Assert.Equal(1, bird.ControlSlots);
        Assert.Equal(ostard.FavoriteFood, bird.FavoriteFood);
        Assert.True(bird.CheckFoodPreference(new Apple()));
        Assert.True(bird.CheckFoodPreference(new SheafOfHay()));
        Assert.Equal(ostard.Meat, bird.Meat);

        // No row in ServUO's training table (it stays ServUO's): untrainable, and its slots stay the table's.
        Assert.Equal(Class.Untrainable, PetTrainingHelper.GetTrainingDefinition(bird).Class);

        ostard.Delete();
        bird.Delete();
    }

    [Fact]
    public void ARealTameMakesItTheTamers()
    {
        ShardTestClock.Arm();

        var pm = Player("Tamer");
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        pm.Skills.AnimalTaming.Cap = 120.0;
        pm.Skills.AnimalTaming.Base = 120.0;
        pm.Skills.AnimalLore.Cap = 120.0;
        pm.Skills.AnimalLore.Base = 120.0;

        // Rooted, as the Pet Training tame fact's bear: the taming time must not walk it out of range.
        var bird = new Peacock { CantWalk = true };
        bird.MoveToWorld(new Point3D(Here.X + 1, Here.Y, Here.Z), Map.TerMur);

        try
        {
            AnimalTaming.OnUse(pm);
            pm.Target.Invoke(pm, bird);
            ShardTestClock.AdvanceSeconds(20);

            Assert.True(bird.Controlled, $"a 120 tamer tames a 29.1 peacock (tamer alive: {pm.Alive}, killer: {pm.LastKiller})");
            Assert.Same(pm, bird.ControlMaster);
            Assert.Equal(1, pm.Followers);
        }
        finally
        {
            pm.NetState = null;
            bird.Delete();
            pm.Delete();
        }
    }

    [Fact]
    public void ItsOwnerRidesAndDismountsAndAStrangerCannotRideIt()
    {
        var pm = Player("Rider");
        var stranger = Player("Stranger");
        var bird = Tamed(pm);

        stranger.Location = pm.Location;
        bird.OnDoubleClick(stranger);
        Assert.Null(bird.Rider);
        Assert.Null(stranger.Mount);

        bird.OnDoubleClick(pm);

        Assert.Same(pm, bird.Rider);
        Assert.Same(bird, pm.Mount);
        var worn = pm.FindItemOnLayer(Layer.Mount);
        Assert.NotNull(worn);
        Assert.Equal(0x3ECF, worn.ItemID);
        Assert.Equal(Map.Internal, bird.Map);
        _out.WriteLine($"mounted: item=0x{worn.ItemID:X} bird map={bird.Map}");

        BaseMount.Dismount(pm);

        Assert.Null(pm.Mount);
        Assert.Null(pm.FindItemOnLayer(Layer.Mount));
        Assert.Null(bird.Rider);
        Assert.Same(Map.TerMur, bird.Map);
        Assert.Equal(pm.Location, bird.Location);

        bird.Delete();
        stranger.Delete();
        pm.Delete();
    }

    [Fact]
    public void ItRoundTripsTheSerializerTamedAndRidden()
    {
        var pm = Player("Saver");
        var bird = Tamed(pm);
        bird.Name = "Argus";

        var copy = RoundTrip(bird);

        Assert.Equal(1440, (int)copy.Body);
        Assert.Equal(0x3ECF, copy.ItemID);
        Assert.Equal("Argus", copy.Name);
        Assert.Equal(29.1, copy.MinTameSkill);
        Assert.Equal(1, copy.ControlSlots);
        Assert.Same(pm, copy.ControlMaster);
        Assert.True(copy.Controlled);

        bird.OnDoubleClick(pm);
        Assert.Same(pm, bird.Rider);

        var ridden = RoundTrip(bird);
        Assert.Same(pm, ridden.Rider);
        Assert.Equal(0x3ECF, ridden.ItemID);

        // copy and ridden share bird's serial; only the real one is deleted, which is what a server stop leaves behind.
        BaseMount.Dismount(pm);
        bird.Delete();
        pm.Delete();
    }

    [Fact]
    public void TheLeashShrinksItToItsOwnFigurineAndPinnedsRowsStay()
    {
        Assert.Equal(Peacock.ShrinkItemId, ShrinkTable.Lookup(1440));
        Assert.Equal(0xA0C0, ShrinkTable.Lookup(1440));
        Assert.Equal(0x2588, ShrinkTable.Lookup(0xD9)); // the dog: pinned's row, as the leash's facts use it
        Assert.Equal(0x20DF, ShrinkTable.Lookup(1)); // pinned's first row

        var pm = Player("Leasher");
        var bird = Tamed(pm);
        bird.Location = pm.Location;

        var leash = new PetLeash();
        pm.Backpack!.DropItem(leash);

        var fig = leash.Use(pm, bird);

        Assert.NotNull(fig);
        _out.WriteLine($"figurine: item=0x{fig!.ItemID:X} name={fig.Name}");
        Assert.Equal(0xA0C0, fig.ItemID);
        Assert.Equal(Map.Internal, bird.Map);

        Assert.True(fig.Restore(pm));
        Assert.Same(Map.TerMur, bird.Map);

        bird.Delete();
        pm.Delete();
    }

    [Fact]
    public void TheSeedGivesEachMeadowOneSpawnerAndASecondSeedAddsNothing()
    {
        ClearMeadows();

        try
        {
            var first = PeacockSystem.Seed();
            first.ForEach(_out.WriteLine);

            var spawners = MeadowSpawners();
            Assert.Equal(PeacockTable.Current.Spawns.Count, spawners.Count);
            Assert.True(spawners.Count >= 1);

            foreach (var s in spawners)
            {
                var spot = PeacockTable.Current.Spawns.Single(p => p.Location == s.Location);
                Assert.Equal(spot.Count, s.Count);
                Assert.Equal(spot.HomeRange, s.HomeRange);
                Assert.Equal(spot.HomeRange, s.WalkingRange);
                Assert.Equal(TimeSpan.FromMinutes(spot.MinDelayMinutes), s.MinDelay);
                Assert.Equal(TimeSpan.FromMinutes(spot.MaxDelayMinutes), s.MaxDelay);
                Assert.Equal("Peacock", Assert.Single(s.Entries).SpawnedName);
            }

            var dry = PeacockSystem.Seed(true);
            Assert.Contains(dry, l => l.Contains(" 0 to place"));

            var second = PeacockSystem.Seed();
            Assert.Contains(second, l => l.Contains(" 0 placed"));
            Assert.Equal(spawners.OrderBy(s => s.Serial.Value), MeadowSpawners().OrderBy(s => s.Serial.Value));
        }
        finally
        {
            ClearMeadows();
        }
    }

    [Fact]
    public void ADryRunPlacesNothing()
    {
        ClearMeadows();

        var dry = PeacockSystem.Seed(true);

        Assert.Contains(dry, l => l.Contains($" {PeacockTable.Current.Spawns.Count} to place"));
        Assert.Empty(MeadowSpawners());
    }

    [Fact]
    public void NoMeadowLiesUnderAFence()
    {
        foreach (var s in PeacockTable.Defaults.Spawns)
        {
            foreach (var fence in SosFenceRegion.Fences)
            {
                foreach (var (x1, y1, x2, y2) in fence.Rects)
                {
                    Assert.False(
                        s.X + s.HomeRange >= x1 && s.X - s.HomeRange <= x2 && s.Y + s.HomeRange >= y1 && s.Y - s.HomeRange <= y2,
                        $"{s.Name} touches {fence.Name}"
                    );
                }
            }
        }
    }

    [Fact]
    public void NewPeacocksTakeTheTablesNumbersAndSavedOnesKeepTheirs()
    {
        var old = PeacockTable.Current;
        var saved = new Peacock();

        try
        {
            var t = PeacockTable.Defaults;
            t.MinTameSkill = 45.5;
            t.ControlSlots = 2;
            t.HitsMin = t.HitsMax = 100;
            t.Meat = 7;
            PeacockTable.Current = t;

            var bird = new Peacock();

            Assert.Equal(45.5, bird.MinTameSkill);
            Assert.Equal(2, bird.ControlSlots);
            Assert.Equal(100, bird.HitsMax);
            Assert.Equal(7, bird.Meat);
            Assert.Equal(29.1, saved.MinTameSkill);

            bird.Delete();
        }
        finally
        {
            PeacockTable.Current = old;
            saved.Delete();
        }
    }

    [Fact]
    public void TheShippedDefaultsRoundTripAndABadFileIsRefused()
    {
        var errors = new List<string>();
        var table = PeacockTable.Parse(PeacockTable.ToJson(PeacockTable.Defaults), "defaults", errors);
        Assert.Empty(errors);
        Assert.NotNull(table);
        Assert.Equal(PeacockTable.Defaults.Spawns.Count, table!.Spawns.Count);
        Assert.Equal(29.1, table.MinTameSkill);

        var old = PeacockTable.Current;
        var path = Path.Combine(Path.GetTempPath(), $"peacock-{Guid.NewGuid():N}.json");

        try
        {
            var bad = PeacockTable.Defaults;
            bad.ControlSlots = 9;
            bad.Spawns[0].Count = 0;
            File.WriteAllText(path, PeacockTable.ToJson(bad));

            var lines = PeacockTable.Reload(path);
            lines.ForEach(_out.WriteLine);

            Assert.Contains(lines, l => l.Contains("refused"));
            Assert.Contains(lines, l => l.Contains("controlSlots"));
            Assert.Contains(lines, l => l.Contains("count"));
            Assert.Same(old, PeacockTable.Current);

            File.WriteAllText(path, "{ \"enabled\": false, \"notAField\": 1 }");
            Assert.Contains(PeacockTable.Reload(path), l => l.Contains("refused"));
            Assert.Same(old, PeacockTable.Current);
        }
        finally
        {
            PeacockTable.Current = old;
            File.Delete(path);
        }
    }

    [Fact]
    public void WithTheTableOffNothingIsSeeded()
    {
        var old = PeacockTable.Current;
        ClearMeadows();

        try
        {
            var t = PeacockTable.Defaults;
            t.Enabled = false;
            PeacockTable.Current = t;

            Assert.Contains(PeacockSystem.Seed(), l => l.Contains("off"));
            Assert.Empty(MeadowSpawners());
        }
        finally
        {
            PeacockTable.Current = old;
        }
    }

    [Fact]
    public void ItCarriesNoLoot()
    {
        var bird = new Peacock();

        Assert.Null(bird.Backpack);
        Assert.Equal(-1, bird.TreasureMapLevel); // pinned's "no map"

        bird.Delete();
    }
}
