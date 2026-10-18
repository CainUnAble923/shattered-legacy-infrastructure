// BurrowScampBodyRouteVerification.cs
//
// cc-P139, F-46: the creature body route's server half. Body 2048 (0x800) is the first third-party creature body the
// package ships (Vert-I-Go's kobold archer .vd at anim7 slot 1); the shard must type it and the staff test creature
// must wear it. Notes in shard-migration notes/cc-P139-rootdeep-burrows.md.
//
// Facts:
//   1. ShardBodyTypes types body 2048 Monster (pinned's bodyTable.cfg ends at 1973, so an unlisted body would be Empty:
//      no hurt animation, no fidgets, BaseWeapon.PlayHurtAnimation and BaseCreature's idle), and the test creature
//      constructs on it: archer AI, its bow in hand, arrows in its pack, its corpse name.
//   2. It round-trips through the generated serializer with its body and name.
//   3. Spawned into a world, it lives there; killed, it leaves a corpse whose Amount is the body (the protocol's rule for
//      0x2006, Corpse.cs:211), which is what the client draws the death frame from.
//   4. Its bow plays action 4 (the .vd's one attack) for a monster body and leaves any other body to pinned: pinned plays
//      no swing animation for a monster shooting a bow (BaseWeapon.cs:2758-2761).
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh.

using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class BurrowScampBodyRouteVerification
{
    private static readonly Point3D Here = new(1000, 1000, 0);

    private readonly ITestOutputHelper _out;

    public BurrowScampBodyRouteVerification(ITestOutputHelper output) => _out = output;

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

    [Fact]
    public void TheServerTypesBody2048MonsterAndTheTestCreatureWearsItWithABow()
    {
        ShardBodyTypes.Apply();

        Assert.Contains(ShardBodyTypes.Entries, e => e.Body == BurrowScampSlingerBodyTest.BodyId && e.Type == BodyType.Monster);
        Assert.Equal(BodyType.Monster, new Body(BurrowScampSlingerBodyTest.BodyId).Type);

        var bc = new BurrowScampSlingerBodyTest();
        _out.WriteLine($"{bc.GetType().Name}: name='{bc.Name}' body={(int)bc.Body} type={bc.Body.Type} ai={bc.AI} " +
                       $"hits={bc.HitsMax} corpse='{bc.CorpseName}' hand={bc.FindItemOnLayer(Layer.TwoHanded)?.GetType().Name}");

        Assert.Equal(2048, (int)bc.Body);
        Assert.True(bc.Body.IsMonster);
        Assert.Equal(AIType.AI_Archer, bc.AI);
        Assert.Equal("a burrow scamp slinger", bc.Name);
        Assert.Equal("a burrow scamp corpse", bc.CorpseName);
        Assert.IsType<BurrowScampTestBow>(bc.FindItemOnLayer(Layer.TwoHanded));
        Assert.NotNull(bc.Backpack);
        Assert.True(bc.Backpack!.FindItemByType<Arrow>()?.Amount >= 20, "no arrows in its pack");

        bc.Delete();
    }

    [Fact]
    public void ItRoundTripsThroughTheGeneratedSerializer()
    {
        ShardBodyTypes.Apply();

        var bc = new BurrowScampSlingerBodyTest();
        var copy = RoundTrip(bc);

        Assert.Equal(2048, (int)copy.Body);
        Assert.Equal("a burrow scamp slinger", copy.Name);
        Assert.Equal(AIType.AI_Archer, copy.AI);

        copy.Delete();
        bc.Delete();
    }

    [Fact]
    public void SpawnedItLivesAndKilledItLeavesACorpseCarryingTheBody()
    {
        ShardBodyTypes.Apply();

        var bc = new BurrowScampSlingerBodyTest();
        bc.MoveToWorld(Here, Map.TerMur);

        Assert.True(bc.Alive);
        Assert.Same(Map.TerMur, bc.Map);
        Assert.Equal(Here, bc.Location);

        bc.Kill();

        var corpse = Assert.IsAssignableFrom<Corpse>(bc.Corpse);
        _out.WriteLine($"corpse: itemID=0x{corpse.ItemID:X} amount={corpse.Amount} name='{corpse.Name}' items={corpse.Items.Count}");
        Assert.Equal(0x2006, corpse.ItemID);
        Assert.Equal(2048, corpse.Amount);
        Assert.Contains(corpse.Items, i => i is BurrowScampTestBow);

        corpse.Delete();
    }

    [Fact]
    public void TheBowPlaysTheVdOneAttackForAMonsterBodyAndLeavesOthersToPinned()
    {
        ShardBodyTypes.Apply();

        var bc = new BurrowScampSlingerBodyTest();
        Assert.Equal(4, BurrowScampTestBow.SwingActionFor(bc));

        var human = new PlayerMobile { Body = 400 };
        Assert.True(human.Body.IsHuman);
        Assert.Equal(-1, BurrowScampTestBow.SwingActionFor(human));

        var bow = RoundTrip(new BurrowScampTestBow());
        Assert.Equal("a burrow scamp's bow", bow.Name);
        Assert.Equal(Layer.TwoHanded, bow.Layer);

        bow.Delete();
        human.Delete();
        bc.Delete();
    }
}
