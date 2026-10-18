// KitsuneEarsTailVerification.cs
//
// cc-P62, F-35: the fox ears and tail, a staff-only test item for the kitsune's woman form. Notes in shard-migration
// notes/cc-P62-kitsune-walk-test.md.
//
// Facts:
//   1. It round-trips through the generated serializer as item 0x3C3C on the Earrings layer, named "fox ears and tail".
//   2. A woman and a man put it on (it sits on Earrings); a gargoyle does not, and it stays where it was
//      (cc-P138: men were refused until the male paperdoll art existed).
//   3. The server's tiledata for 0x3C3C is ours (records.json's values): flags 0x00400002 (Wearable | Weapon, as OSI's
//      earrings), weight 1, layer 0x12, Animation 4783, height 1. The test host loads no tiledata, so this reads the
//      table, as the runestone's fact does.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh.

using System;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class KitsuneEarsTailVerification
{
    private readonly ITestOutputHelper _out;

    public KitsuneEarsTailVerification(ITestOutputHelper output) => _out = output;

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

    private static PlayerMobile Person(bool female, Race race = null)
    {
        race ??= Race.Human;
        var pm = new PlayerMobile { Player = true, Race = race, Female = female, Body = race.AliveBody(female) };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(new Point3D(1240, 1240, 0), Map.Trammel);
        return pm;
    }

    [Fact]
    public void ItRoundTripsAsItsArtIdOnTheEarringsLayer()
    {
        var item = new KitsuneEarsTail();
        try
        {
            var copy = RoundTrip(item);
            _out.WriteLine($"0x{copy.ItemID:X4} {copy.Layer} '{copy.Name ?? item.DefaultName}' hue {copy.Hue}");
            Assert.Equal(0x3C3C, copy.ItemID);
            Assert.Equal(Layer.Earrings, copy.Layer);
            Assert.Equal(0, copy.Hue);
            Assert.Equal("fox ears and tail", item.DefaultName);
        }
        finally
        {
            item.Delete();
        }
    }

    [Fact]
    public void AWomanAndAManPutItOnAndAGargoyleDoesNot()
    {
        var woman = Person(true);
        var man = Person(false);
        var gargoyle = Person(true, Race.Gargoyle);
        var hers = new KitsuneEarsTail();
        var his = new KitsuneEarsTail();
        var theirs = new KitsuneEarsTail();
        try
        {
            Assert.True(woman.EquipItem(hers));
            Assert.Same(hers, woman.FindItemOnLayer(Layer.Earrings));

            Assert.True(man.EquipItem(his));
            Assert.Same(his, man.FindItemOnLayer(Layer.Earrings));

            gargoyle.Backpack.DropItem(theirs);
            var worn = gargoyle.EquipItem(theirs);
            _out.WriteLine($"woman: {woman.FindItemOnLayer(Layer.Earrings)?.GetType().Name}; man: {man.FindItemOnLayer(Layer.Earrings)?.GetType().Name}; gargoyle: EquipItem {worn}, parent {theirs.Parent?.GetType().Name}");
            Assert.False(worn);
            Assert.Null(gargoyle.FindItemOnLayer(Layer.Earrings));
            Assert.Same(gargoyle.Backpack, theirs.Parent);
            Assert.Equal("Only humans and elves can wear this.", KitsuneEarsTail.RefusalForGargoyles);
        }
        finally
        {
            hers.Delete();
            his.Delete();
            theirs.Delete();
            woman.Delete();
            man.Delete();
            gargoyle.Delete();
        }
    }

    [Fact]
    public void TheServersTiledataForItsArtIdIsOurs()
    {
        ShatteredLegacyArt.Apply();
        var d = TileData.ItemTable[0x3C3C];
        _out.WriteLine($"0x3C3C: '{d.Name}' flags 0x{(ulong)d.Flags:X8} weight {d.Weight} layer {d.Quality} anim {d.Animation} height {d.Height}");

        Assert.Equal("fox ears and tail", d.Name);
        Assert.Equal((TileFlag)0x00400002, d.Flags); // records.json "flags"
        Assert.True((d.Flags & TileFlag.Wearable) != 0);
        Assert.Equal(1, d.Weight);
        Assert.Equal(0x12, d.Quality);
        Assert.Equal(4783, d.Animation);
        Assert.Equal(1, d.Height);
    }
}
