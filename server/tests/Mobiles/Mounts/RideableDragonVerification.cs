// RideableDragonVerification.cs
//
// cc-P277 (P139-D6): the rideable red dragon and the colorable dragon (server/customizations/Mobiles/Mounts/
// RideableDragons.cs) on package bodies 2086 and 2087. Notes in shard-migration notes/cc-P277-rideable-red-dragon.md.
//
// Facts:
//   1. Each constructs on its body, the server types the body Animal (ShardBodyTypes), its mount item is the one the
//      package's tiledata names, and it is a staff-only placeholder: not tamable, a horse's numbers.
//   2. Each round-trips the generated serializer with its body and mount item.
//   3. A GameMaster rides it (the mount item goes on the Mount layer, the dragon leaves the map) and dismounts (the
//      dragon is back where the rider stands); a player cannot ride an untamed one.
//   4. The colorable dragon's hue goes onto its mount item, so the ridden dragon shows the same colour.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh.

using System;
using Server;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RideableDragonVerification
{
    private static readonly Point3D Here = new(1010, 1010, 0);

    private readonly ITestOutputHelper _out;

    public RideableDragonVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardBodyTypes.Apply();
    }

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    public static TheoryData<Type, int, int, string> Dragons => new()
    {
        { typeof(RideableRedDragon), 2086, 0x6E87, "a red riding dragon" },
        { typeof(RideableColorableDragon), 2087, 0x6E88, "a riding dragon" }
    };

    [Theory]
    [MemberData(nameof(Dragons))]
    public void EachStandsOnItsBodyTypedAnimalWithThePackagesMountItemAndIsAStaffPlaceholder(Type type, int body, int item, string name)
    {
        Assert.Contains(ShardBodyTypes.Entries, e => e.Body == body && e.Type == BodyType.Animal);
        Assert.Equal(BodyType.Animal, new Body(body).Type);

        var d = (BaseMount)Activator.CreateInstance(type)!;
        _out.WriteLine($"{type.Name}: name='{d.Name}' body={(int)d.Body} type={d.Body.Type} item=0x{d.ItemID:X} tamable={d.Tamable} hits={d.HitsMax} str={d.RawStr}");

        Assert.Equal(body, (int)d.Body);
        Assert.True(d.Body.IsAnimal);
        Assert.Equal(item, d.ItemID);
        Assert.Equal(name, d.Name);
        Assert.False(d.Tamable);
        Assert.InRange(d.HitsMax, 28, 45);   // a stock horse's: a placeholder until Chase decides
        Assert.InRange(d.RawStr, 22, 98);
        Assert.Equal(RideableDragonArt.RedBody, 2086);
        Assert.Equal(RideableDragonArt.RedMountItem, 0x6E87);

        d.Delete();
    }

    [Theory]
    [MemberData(nameof(Dragons))]
    public void EachRoundTripsThroughTheGeneratedSerializer(Type type, int body, int item, string name)
    {
        var d = (BaseMount)Activator.CreateInstance(type)!;
        var copy = RoundTrip(d);

        Assert.Equal(body, (int)copy.Body);
        Assert.Equal(name, copy.Name);
        Assert.Equal(item, copy.ItemID);

        copy.Delete();
        d.Delete();
    }

    [Fact]
    public void AGameMasterRidesAndDismountsTheRedDragonAndAPlayerCannotRideAnUntamedOne()
    {
        var gm = new PlayerMobile { Body = 400, AccessLevel = AccessLevel.GameMaster };
        gm.MoveToWorld(Here, Map.Trammel);
        var d = new RideableRedDragon();
        d.MoveToWorld(Here, Map.Trammel);

        d.OnDoubleClick(gm);

        Assert.Same(gm, d.Rider);
        Assert.Same(d, gm.Mount);
        var worn = gm.FindItemOnLayer(Layer.Mount);
        Assert.NotNull(worn);
        Assert.Equal(0x6E87, worn.ItemID);
        Assert.Equal(Map.Internal, d.Map);
        _out.WriteLine($"mounted: rider={gm.Name} item=0x{worn.ItemID:X} dragon map={d.Map}");

        BaseMount.Dismount(gm);

        Assert.Null(gm.Mount);
        Assert.Null(gm.FindItemOnLayer(Layer.Mount));
        Assert.Null(d.Rider);
        Assert.Same(Map.Trammel, d.Map);
        Assert.Equal(gm.Location, d.Location);

        var player = new PlayerMobile { Body = 400 };
        player.MoveToWorld(Here, Map.Trammel);
        d.OnDoubleClick(player);
        Assert.Null(d.Rider);
        Assert.Null(player.Mount);

        player.Delete();
        d.Delete();
        gm.Delete();
    }

    [Fact]
    public void TheColorableDragonsHueGoesOntoItsMountItem()
    {
        var gm = new PlayerMobile { Body = 400, AccessLevel = AccessLevel.GameMaster };
        gm.MoveToWorld(Here, Map.Trammel);
        var d = new RideableColorableDragon { Hue = 1157 };
        d.MoveToWorld(Here, Map.Trammel);
        d.OnDoubleClick(gm);

        var worn = gm.FindItemOnLayer(Layer.Mount);
        Assert.NotNull(worn);
        Assert.Equal(0x6E88, worn.ItemID);
        Assert.Equal(1157, worn.Hue);

        BaseMount.Dismount(gm);
        d.Delete();
        gm.Delete();
    }
}
