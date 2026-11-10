// RidablePolarBearVerification.cs
//
// cc-P860: the ridable polar bear (server/customizations/Mobiles/Mounts/RidablePolarBear.cs, RidablePolarBearDens.cs).
// Notes in shard-migration notes/cc-P860-ridable-polar-bear.md.
//
// Facts:
//   1. It stands on the polar bear's body 213 (an animal: idles, opens no doors) with the ethereal polar bear's mount
//      item 0x3EC5, the polar bear's fighting numbers, a tame of 59.1, one slot and the polar bear's food; no row in
//      ServUO's training table (untrainable, as the Peacock); it names Milva, whose bear it follows.
//   2. A real tame (Animal Taming's own target and timer) makes it the tamer's.
//   3. Its owner rides it (the worn mount item is 0x3EC5) and dismounts; a stranger cannot ride it.
//   4. It round-trips the generated serializer, tamed and while ridden.
//   5. The leash shrinks it to the polar bear's figurine 0x20E1, and it comes back.
//   6. The seed gives each den one spawner of ridable polar bears on Trammel, a second seed adds nothing, a dry run
//      places nothing.
//   7. Every den is on Trammel's Dagger Isle snow, and no den's home range touches an SoS fence.
//   8. Nothing is in its pack and it drops no loot: it grants nothing.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh.

using System;
using System.Collections.Generic;
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
public class RidablePolarBearVerification
{
    // The Peacock facts' quiet spot (TerMur); every fact here cleans up after itself.
    private static readonly Point3D Here = new(1793, 1179, 0);

    private readonly ITestOutputHelper _out;

    public RidablePolarBearVerification(ITestOutputHelper output) => _out = output;

    private static PlayerMobile Player(string name)
    {
        var pm = new PlayerMobile { Player = true, Name = name, Body = 400, FollowersMax = 5 };
        pm.MoveToWorld(Here, Map.TerMur);
        pm.AddItem(new Backpack());
        return pm;
    }

    private static RidablePolarBear Tamed(PlayerMobile pm)
    {
        var bear = new RidablePolarBear();
        bear.MoveToWorld(Here, Map.TerMur);
        bear.SetControlMaster(pm);
        return bear;
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

    private static List<Spawner> DenSpawners()
    {
        var found = new List<Spawner>();
        foreach (var den in RidablePolarBearDens.Dens)
        {
            foreach (var item in Map.Trammel.GetItemsInRange(den.Location, RidablePolarBearDens.SearchRange))
            {
                if (item is Spawner s && !found.Contains(s))
                {
                    found.Add(s);
                }
            }
        }

        return found;
    }

    private static void ClearDens()
    {
        foreach (var s in DenSpawners())
        {
            s.Delete();
        }
    }

    // ---- 1 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ItIsAPolarBearYouCanRideWithItsOwnTame()
    {
        var bear = new RidablePolarBear();
        var wild = new PolarBear();
        _out.WriteLine($"name='{bear.Name}' body={(int)bear.Body} type={bear.Body.Type} item=0x{bear.ItemID:X} " +
                       $"tame={bear.MinTameSkill} slots={bear.ControlSlots} hits={bear.HitsMax} str={bear.RawStr}");

        try
        {
            Assert.IsAssignableFrom<BaseMount>(bear);
            Assert.Equal(213, (int)bear.Body);
            Assert.Equal(BodyType.Animal, new Body(213).Type);
            Assert.False(bear.CanOpenDoors);
            Assert.Equal(0x3EC5, bear.ItemID);
            Assert.Equal("a ridable polar bear", bear.Name);
            Assert.Equal("a polar bear corpse", bear.CorpseName);
            Assert.Equal(AIType.AI_Animal, bear.AI);
            Assert.Equal(FightMode.Aggressor, bear.FightMode);

            // The polar bear's fighting numbers (pinned PolarBear.cs and Milva's download agree).
            Assert.InRange(bear.RawStr, 116, 140);
            Assert.InRange(bear.RawDex, 81, 105);
            Assert.InRange(bear.HitsMax, 70, 84);
            Assert.Equal(wild.DamageMin, bear.DamageMin);
            Assert.Equal(wild.DamageMax, bear.DamageMax);
            Assert.InRange(bear.ColdResistance, 60, 80);
            Assert.Equal(wild.Fame, bear.Fame);
            Assert.Equal(wild.VirtualArmor, bear.VirtualArmor);

            Assert.True(bear.Tamable);
            Assert.Equal(59.1, bear.MinTameSkill);
            Assert.True(bear.MinTameSkill > wild.MinTameSkill);
            Assert.Equal(1, bear.ControlSlots);
            Assert.Equal(wild.FavoriteFood, bear.FavoriteFood);
            Assert.True(bear.CheckFoodPreference(new FishSteak()));
            Assert.Equal(PackInstinct.Bear, bear.PackInstinct);
            Assert.Equal(wild.Hides, bear.Hides);

            // No row in ServUO's training table (PetTrainingVerification keeps it ServUO's, as for the Peacock):
            // untrainable, its slots its own.
            Assert.Equal(Class.Untrainable, PetTrainingHelper.GetTrainingDefinition(bear).Class);

            Assert.Equal("Milva", RidablePolarBear.Credit);
        }
        finally
        {
            wild.Delete();
            bear.Delete();
        }
    }

    // ---- 2 ------------------------------------------------------------------------------------------------------

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

        // Rooted, as the Peacock's tame fact: the taming time must not walk it out of range.
        var bear = new RidablePolarBear { CantWalk = true };
        bear.MoveToWorld(new Point3D(Here.X + 1, Here.Y, Here.Z), Map.TerMur);

        try
        {
            AnimalTaming.OnUse(pm);
            pm.Target.Invoke(pm, bear);
            ShardTestClock.AdvanceSeconds(20);

            Assert.True(bear.Controlled, $"a 120 tamer tames a 59.1 bear (tamer alive: {pm.Alive}, killer: {pm.LastKiller})");
            Assert.Same(pm, bear.ControlMaster);
            Assert.Equal(1, pm.Followers);
        }
        finally
        {
            pm.NetState = null;
            bear.Delete();
            pm.Delete();
        }
    }

    // ---- 3 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ItsOwnerRidesAndDismountsAndAStrangerCannotRideIt()
    {
        var pm = Player("Rider");
        var stranger = Player("Stranger");
        var bear = Tamed(pm);

        try
        {
            stranger.Location = pm.Location;
            bear.OnDoubleClick(stranger);
            Assert.Null(bear.Rider);
            Assert.Null(stranger.Mount);

            bear.OnDoubleClick(pm);

            Assert.Same(pm, bear.Rider);
            Assert.Same(bear, pm.Mount);
            var worn = pm.FindItemOnLayer(Layer.Mount);
            Assert.NotNull(worn);
            Assert.Equal(0x3EC5, worn.ItemID);
            Assert.Equal(Map.Internal, bear.Map);
            _out.WriteLine($"mounted: item=0x{worn.ItemID:X} bear map={bear.Map}");

            BaseMount.Dismount(pm);

            Assert.Null(pm.Mount);
            Assert.Null(pm.FindItemOnLayer(Layer.Mount));
            Assert.Null(bear.Rider);
            Assert.Same(Map.TerMur, bear.Map);
            Assert.Equal(pm.Location, bear.Location);
        }
        finally
        {
            bear.Delete();
            stranger.Delete();
            pm.Delete();
        }
    }

    // ---- 4 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ItRoundTripsTheSerializerTamedAndRidden()
    {
        var pm = Player("Saver");
        var bear = Tamed(pm);
        bear.Name = "Frost";

        try
        {
            var copy = RoundTrip(bear);

            Assert.Equal(213, (int)copy.Body);
            Assert.Equal(0x3EC5, copy.ItemID);
            Assert.Equal("Frost", copy.Name);
            Assert.Equal(59.1, copy.MinTameSkill);
            Assert.Equal(1, copy.ControlSlots);
            Assert.Same(pm, copy.ControlMaster);
            Assert.True(copy.Controlled);

            bear.OnDoubleClick(pm);
            Assert.Same(pm, bear.Rider);

            var ridden = RoundTrip(bear);
            Assert.Same(pm, ridden.Rider);
            Assert.Equal(0x3EC5, ridden.ItemID);
        }
        finally
        {
            // copy and ridden share bear's serial; only the real one is deleted, which is what a server stop leaves.
            BaseMount.Dismount(pm);
            bear.Delete();
            pm.Delete();
        }
    }

    // ---- 5 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheLeashShrinksItToThePolarBearsFigurine()
    {
        Assert.Equal(0x20E1, ShrinkTable.Lookup(213));

        var pm = Player("Leasher");
        var bear = Tamed(pm);
        bear.Location = pm.Location;

        try
        {
            var leash = new PetLeash();
            pm.Backpack!.DropItem(leash);

            var fig = leash.Use(pm, bear);

            Assert.NotNull(fig);
            _out.WriteLine($"figurine: item=0x{fig!.ItemID:X} name={fig.Name}");
            Assert.Equal(0x20E1, fig.ItemID);
            Assert.Equal(Map.Internal, bear.Map);

            Assert.True(fig.Restore(pm));
            Assert.Same(Map.TerMur, bear.Map);
        }
        finally
        {
            bear.Delete();
            pm.Delete();
        }
    }

    // ---- 6 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheSeedGivesEachDenOneSpawnerAndASecondSeedAddsNothing()
    {
        ClearDens();

        try
        {
            var dry = RidablePolarBearDens.Seed(true);
            dry.ForEach(_out.WriteLine);
            Assert.Contains(dry, l => l.Contains($" {RidablePolarBearDens.Dens.Length} to place"));
            Assert.Empty(DenSpawners());

            var first = RidablePolarBearDens.Seed();
            first.ForEach(_out.WriteLine);

            var spawners = DenSpawners();
            Assert.Equal(RidablePolarBearDens.Dens.Length, spawners.Count);
            Assert.True(spawners.Count >= 2);

            foreach (var s in spawners)
            {
                var den = RidablePolarBearDens.Dens.Single(d => d.Location == s.Location);
                Assert.Same(Map.Trammel, s.Map);
                Assert.Equal(den.Count, s.Count);
                Assert.Equal(den.HomeRange, s.HomeRange);
                Assert.Equal(den.HomeRange, s.WalkingRange);
                Assert.Equal(TimeSpan.FromMinutes(den.MinDelayMinutes), s.MinDelay);
                Assert.Equal(TimeSpan.FromMinutes(den.MaxDelayMinutes), s.MaxDelay);
                Assert.Equal(nameof(RidablePolarBear), Assert.Single(s.Entries).SpawnedName);
            }

            var second = RidablePolarBearDens.Seed();
            Assert.Contains(second, l => l.Contains(" 0 placed"));
            Assert.Equal(spawners.OrderBy(s => s.Serial.Value), DenSpawners().OrderBy(s => s.Serial.Value));
        }
        finally
        {
            ClearDens();
        }
    }

    // ---- 7 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void EveryDenIsOnDaggerIsleAndClearOfTheSosFences()
    {
        foreach (var d in RidablePolarBearDens.Dens)
        {
            // Dagger Isle, north-east of Minoc (Britannia's polar bear country, kept on the Siege of Sosaria land).
            Assert.InRange(d.Location.X, 3900, 4223);
            Assert.InRange(d.Location.Y, 200, 700);

            foreach (var fence in SosFenceRegion.Fences)
            {
                foreach (var (x1, y1, x2, y2) in fence.Rects)
                {
                    Assert.False(
                        d.Location.X + d.HomeRange >= x1 && d.Location.X - d.HomeRange <= x2 &&
                        d.Location.Y + d.HomeRange >= y1 && d.Location.Y - d.HomeRange <= y2,
                        $"{d.Name} touches {fence.Name}"
                    );
                }
            }
        }
    }

    // ---- 8 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ItCarriesNothingAndGrantsNothing()
    {
        var bear = new RidablePolarBear();
        bear.MoveToWorld(Here, Map.TerMur);

        try
        {
            Assert.True(bear.Backpack == null || bear.Backpack.Items.Count == 0);
            Assert.True(bear.TreasureMapLevel < 1, $"treasure map level {bear.TreasureMapLevel}");
            Assert.Equal(0, bear.Karma);
        }
        finally
        {
            bear.Delete();
        }
    }
}
