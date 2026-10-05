// RaptorPackFriendsVerification.cs
//
// cc-P51 Part A (F-31, ported). ServUO's Raptor pack friends and its 25% pottery drop, back on upstream's Raptor
// through Raptor-pack-friends.patch (the hooks) and customizations Mobiles/Normal/ClusterFRaptorPack.cs and
// RaptorPackFriend.cs (the behaviour). Route argued in shard-migration/notes/cc-P51-staff-hub-and-raptor-pack.md.
//
// Facts:
//   1. A wild raptor that gains a combatant gets two friends within one tile, each fighting that combatant.
//   2. Friends are not tamable, and call no friends of their own.
//   3. A tamed raptor calls none.
//   4. Friends are deleted when the caller dies, loses its combatant, or leaves the map.
//   5. Friends do not survive a save and load; their caller does.
//   6. A raptor saved before this change loads: upstream's Raptor keeps version 0 and gains no serialized field.
//   7. The drop: AncientPotteryFragments below 0.25 for an uncontrolled raptor, never at 0.25 (see the fact for pets).
//
// The friends' first check runs at once and then every 30 s (ServUO's InternalTimer), so a fact Arms the clock before
// it builds anything and Advances to make the timer fire.

using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RaptorPackFriendsVerification
{
    private readonly ITestOutputHelper _out;

    public RaptorPackFriendsVerification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D Spot = new(1500, 1600, 0);

    private sealed class ScriptedRandom(double nextDouble) : System.Random
    {
        public override double NextDouble() => nextDouble;
        public override int Next() => 0;
        public override int Next(int maxValue) => 0;
        public override int Next(int minValue, int maxValue) => minValue;
        public override long NextInt64(long maxValue) => 0;
        public override long NextInt64(long minValue, long maxValue) => minValue;
    }

    private static Raptor NewRaptor()
    {
        var r = new Raptor();
        r.MoveToWorld(Spot, Map.Trammel);
        return r;
    }

    private static PlayerMobile NewFoe()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        return pm;
    }

    // A raptor in a fight with its friends called: the clock is armed, the combatant set and the first check run.
    private (Raptor Raptor, PlayerMobile Foe) Fight()
    {
        ShardTestClock.Arm();
        var raptor = NewRaptor();
        var foe = NewFoe();
        raptor.Combatant = foe;
        Assert.Same(foe, raptor.Combatant);
        ShardTestClock.Advance(TimeSpan.FromMilliseconds(64));
        return (raptor, foe);
    }

    private static void Cleanup(params Mobile[] mobiles)
    {
        foreach (var m in mobiles)
        {
            if (m is Raptor r)
            {
                foreach (var f in ClusterFRaptorPack.FriendsOf(r).ToList())
                {
                    f.Delete();
                }
            }

            m?.Delete();

            if (m is Raptor gone)
            {
                ClusterFRaptorPack.CheckFriends(gone); // ends a pack whose timer a later Arm would strand
            }
        }
    }

    // ---- 1 ----------------------------------------------------------------------------------------

    [Fact]
    public void AWildRaptorThatGainsACombatantCallsTwoFriendsWithinOneTile()
    {
        var (raptor, foe) = Fight();
        try
        {
            var friends = ClusterFRaptorPack.FriendsOf(raptor);
            foreach (var f in friends)
            {
                _out.WriteLine($"friend {f.GetType().Name} at {f.Location} on {f.Map}, combatant {f.Combatant?.GetType().Name}");
            }

            Assert.Equal(ClusterFRaptorPack.MaxFriends, friends.Count);
            Assert.Equal(2, friends.Count);
            foreach (var f in friends)
            {
                Assert.IsType<RaptorPackFriend>(f);
                Assert.False(f.Deleted);
                Assert.Same(raptor.Map, f.Map);
                Assert.True(Math.Max(Math.Abs(f.X - raptor.X), Math.Abs(f.Y - raptor.Y)) <= 1, $"{f.Location} vs {raptor.Location}");
                Assert.Same(foe, f.Combatant);
            }

            // Thirty seconds on, the pack is topped up, never above two.
            friends[0].Delete();
            ShardTestClock.AdvanceSeconds(30.1);
            Assert.Equal(2, ClusterFRaptorPack.FriendsOf(raptor).Count(f => !f.Deleted));
        }
        finally
        {
            Cleanup(raptor, foe);
        }
    }

    // ---- 2 ----------------------------------------------------------------------------------------

    [Fact]
    public void FriendsAreNotTamableAndCallNoFriendsOfTheirOwn()
    {
        var (raptor, foe) = Fight();
        try
        {
            Assert.True(raptor.Tamable); // upstream's raptor is; only its friends are not

            var friends = ClusterFRaptorPack.FriendsOf(raptor).Cast<Raptor>().ToList();
            Assert.Equal(2, friends.Count);
            foreach (var f in friends)
            {
                Assert.False(f.Tamable);
                Assert.False(ClusterFRaptorPack.IsPackRunning(f));
                Assert.Empty(ClusterFRaptorPack.FriendsOf(f));
            }
        }
        finally
        {
            Cleanup(raptor, foe);
        }
    }

    // ---- 3 ----------------------------------------------------------------------------------------

    [Fact]
    public void ATamedRaptorCallsNone()
    {
        ShardTestClock.Arm();
        var raptor = NewRaptor();
        var owner = NewFoe();
        var foe = NewFoe();
        try
        {
            Assert.True(raptor.SetControlMaster(owner)); // what taming does
            Assert.True(raptor.Controlled);
            raptor.Combatant = foe;
            Assert.Same(foe, raptor.Combatant);
            ShardTestClock.AdvanceSeconds(1);

            Assert.False(ClusterFRaptorPack.IsPackRunning(raptor));
            Assert.Empty(ClusterFRaptorPack.FriendsOf(raptor));
        }
        finally
        {
            Cleanup(raptor, owner, foe);
        }
    }

    // ---- 4 ----------------------------------------------------------------------------------------

    public enum Ending { Dies, LosesItsCombatant, LeavesTheMap }

    [Theory]
    [InlineData(Ending.Dies)]
    [InlineData(Ending.LosesItsCombatant)]
    [InlineData(Ending.LeavesTheMap)]
    public void FriendsAreDeletedWhenTheCallerStopsFighting(Ending ending)
    {
        var (raptor, foe) = Fight();
        try
        {
            var friends = ClusterFRaptorPack.FriendsOf(raptor).ToList();
            Assert.Equal(2, friends.Count);

            switch (ending)
            {
                case Ending.Dies:              raptor.Kill(); break;
                case Ending.LosesItsCombatant: raptor.Combatant = null; break;
                case Ending.LeavesTheMap:      raptor.Internalize(); break;
            }

            ShardTestClock.AdvanceSeconds(30.1);

            Assert.All(friends, f => Assert.True(f.Deleted));
            Assert.False(ClusterFRaptorPack.IsPackRunning(raptor));
        }
        finally
        {
            Cleanup(raptor, foe);
        }
    }

    // ---- 5 ----------------------------------------------------------------------------------------

    private static T RoundTrip<T>(T original, Func<Serial, T> make) where T : Mobile
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = make(original.Serial);
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    [Fact]
    public void FriendsDoNotSurviveASaveAndLoadAndTheCallerDoes()
    {
        var (raptor, foe) = Fight();
        try
        {
            var friend = (RaptorPackFriend)ClusterFRaptorPack.FriendsOf(raptor)[0];
            var friendCopy = RoundTrip(friend, s => new RaptorPackFriend(s));

            // [AfterDeserialization(false)] runs on the next timer tick after the load, as ServUO's delete ran at the
            // end of Deserialize: a friend never reaches a running world.
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(64));
            Assert.True(friendCopy.Deleted);

            var raptorCopy = RoundTrip(raptor, s => new Raptor(s));
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(64));
            Assert.False(raptorCopy.Deleted);
            Assert.IsNotType<RaptorPackFriend>(raptorCopy);
            Assert.Equal(raptor.Body, raptorCopy.Body);
            Assert.Equal(raptor.Tamable, raptorCopy.Tamable);
        }
        finally
        {
            Cleanup(raptor, foe);
        }
    }

    // ---- 6 ----------------------------------------------------------------------------------------

    // The version the source generator was told, read from the attribute's constructor argument.
    private static int GeneratorVersion(Type t)
    {
        var data = t.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType.Name == "SerializationGeneratorAttribute");
        if (data != null)
        {
            return Convert.ToInt32(data.ConstructorArguments[0].Value);
        }

        var field = t.GetField("_version", BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly);
        Assert.True(field != null, $"{t.Name}: neither [SerializationGenerator] nor _version is visible to reflection");
        return Convert.ToInt32(field.GetValue(null));
    }

    [Fact]
    public void ARaptorSavedBeforeThisChangeLoads()
    {
        // Upstream's Raptor.cs is [SerializationGenerator(0)] with no [SerializableField]. Route (b) keeps both, so the
        // bytes a pre-change server wrote for a raptor are exactly the bytes this Raptor reads. If anyone moves Raptor
        // to version 1 or adds a field, a v0 reader and a migration JSON are owed and this fails first.
        Assert.Equal(0, GeneratorVersion(typeof(Raptor)));

        var declared = typeof(Raptor).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
        foreach (var f in declared)
        {
            _out.WriteLine($"Raptor declares field {f.FieldType.Name} {f.Name}");
        }

        Assert.Empty(declared);

        // And one round trip reads back exactly what was written.
        ShardTestClock.Arm();
        var raptor = NewRaptor();
        try
        {
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            raptor.Serialize(writer);
            writer.Flush();
            var written = writer.Position;

            var copy = new Raptor(raptor.Serial);
            var reader = new BufferReader(buffer);
            copy.Deserialize(reader);
            Assert.Equal(written, reader.Position);
            Assert.False(copy.Deleted);
        }
        finally
        {
            Cleanup(raptor);
        }
    }

    // ---- 7 ----------------------------------------------------------------------------------------

    private static Container DieInto(BaseCreature creature, double roll)
    {
        var c = new Backpack();
        var original = BuiltInRng.Generator;
        BuiltInRng.Generator = new ScriptedRandom(roll);
        try
        {
            creature.OnDeath(c);
        }
        finally
        {
            BuiltInRng.Generator = original;
        }

        return c;
    }

    public enum Owner { Wild, Tamed, Bonded }

    // "!Controlled" is read after base.OnDeath, as ServUO and upstream's own claw roll read it. For a pet that is not
    // bonded, base.OnDeath has already deleted the creature (pinned Mobile.cs:4899-4902) and OnDelete has released it
    // (BaseCreature.cs:3687-3688), so it rolls like a wild raptor; only a bonded pet, which returns early from
    // BaseCreature.OnDeath (:3482-3540), is still Controlled and drops nothing. Copied faithfully (D-4: it makes
    // nothing missing); recorded in the cc-P51 notes.
    [Theory]
    [InlineData(0.0, Owner.Wild, true)]
    [InlineData(0.2499, Owner.Wild, true)]
    [InlineData(0.25, Owner.Wild, false)]
    [InlineData(0.9, Owner.Wild, false)]
    [InlineData(0.0, Owner.Tamed, true)]
    [InlineData(0.0, Owner.Bonded, false)]
    public void TheDropIsPotteryFragmentsAtTwentyFivePercentForAnUncontrolledRaptor(double roll, Owner owned, bool drops)
    {
        ShardTestClock.Arm();
        var raptor = NewRaptor();
        var owner = owned == Owner.Wild ? null : NewFoe();
        Container c = null;
        try
        {
            if (owner != null)
            {
                Assert.True(raptor.SetControlMaster(owner)); // what taming does
                raptor.IsBonded = owned == Owner.Bonded;
            }

            Assert.Equal(owned != Owner.Wild, raptor.Controlled);
            c = DieInto(raptor, roll);
            var fragments = c.Items.OfType<AncientPotteryFragments>().Count();
            _out.WriteLine($"roll {roll} {owned}: {fragments} fragments; controlled after death {raptor.Controlled}; container holds {string.Join(", ", c.Items.Select(i => i.GetType().Name))}");
            Assert.Equal(drops ? 1 : 0, fragments);
        }
        finally
        {
            c?.Delete();
            Cleanup(raptor, owner);
        }
    }
}
