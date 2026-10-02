// ShatteredRunestoneVerification.cs
//
// cc-P26, F-8: the shattered runestone (our own art, shipped through the player package) and the Reset Stone
// wearing its look. Notes in shard-migration notes/cc-P26-animated-runestone.md.
//
// Facts:
//   1. Each design round-trips through the generated serializer with its design and item ID.
//   2. Designs 1 to 4 are 0x3C10, 0x3C1B, 0x3C26, 0x3C31 (registry.csv); anything else is refused, through [props
//      as staff would set it, and the stone is left as it was. A Player cannot set it at all.
//   3. The server's tiledata for the four base IDs is ours (records.json's values), so the stone blocks like OSI's
//      animated runestone though the server's EA tiledata.mul has UNUSED slots there. The test host loads no
//      tiledata (TileData's static constructor returns under xUnit), so this checks the table Movement reads,
//      not a step: movement through it is a client check (notes, T-rows).
//   4. The Reset Stone wears design 1 at hue 0, and one saved before F-8 (0xED4, hue 0x2B) takes that look at load.

using System;
using System.Collections.Concurrent;
using Server;
using Server.Commands;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ShatteredRunestoneVerification
{
    private readonly ITestOutputHelper _out;

    public ShatteredRunestoneVerification(ITestOutputHelper output) => _out = output;

    private static readonly int[] Expected = { 0x3C10, 0x3C1B, 0x3C26, 0x3C31 };

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

    private static PlayerMobile Staff(AccessLevel level) =>
        new() { Player = true, AccessLevel = level };

    [Fact]
    public void EachDesignRoundTripsWithItsDesignAndItemId()
    {
        for (var design = 1; design <= 4; design++)
        {
            var stone = new ShatteredRunestone(design);
            var copy = RoundTrip(stone);
            _out.WriteLine($"design {design}: 0x{stone.ItemID:X4} -> design {copy.Design}, 0x{copy.ItemID:X4}");

            Assert.Equal(design, copy.Design);
            Assert.Equal(Expected[design - 1], copy.ItemID);
            Assert.False(copy.Movable);
            Assert.False(copy.Decays);
            Assert.Equal(0, copy.Hue);

            stone.Delete();
            copy.Delete();
        }

        // A stone whose ItemID a GM set by hand is put back to its design at load.
        var odd = new ShatteredRunestone(3) { ItemID = 0xED4 };
        Assert.Equal(Expected[2], RoundTrip(odd).ItemID);
        odd.Delete();
    }

    [Fact]
    public void DesignsOneToFourAreTheRegisteredIdsAndAnythingElseIsRefused()
    {
        var gm = Staff(AccessLevel.GameMaster);
        var stone = new ShatteredRunestone();
        var was = CommandLogging.Enabled;
        CommandLogging.Enabled = false;

        try
        {
            Assert.Equal(1, stone.Design);
            Assert.Equal(Expected[0], stone.ItemID);

            for (var design = 1; design <= 4; design++)
            {
                var said = Properties.SetValue(gm, stone, "Design", design.ToString());
                _out.WriteLine($"[props Design {design}: {said}");
                Assert.Equal("Property has been set.", said);
                Assert.Equal(design, stone.Design);
                Assert.Equal(Expected[design - 1], stone.ItemID);
                Assert.Equal(Expected[design - 1], ShatteredRunestone.ItemIdOf(design));
            }

            foreach (var bad in new[] { 0, 5, -1, 15376 })
            {
                var said = Properties.SetValue(gm, stone, "Design", bad.ToString());
                _out.WriteLine($"[props Design {bad}: {said}");
                Assert.NotEqual("Property has been set.", said);
                Assert.Equal(4, stone.Design);
                Assert.Equal(Expected[3], stone.ItemID);
                Assert.Throws<ArgumentOutOfRangeException>(() => ShatteredRunestone.ItemIdOf(bad));
                Assert.Throws<ArgumentOutOfRangeException>(() => new ShatteredRunestone(bad));
            }

            var player = Staff(AccessLevel.Player);
            var refused = Properties.SetValue(player, stone, "Design", "2");
            _out.WriteLine($"a Player's [props Design 2: {refused}");
            Assert.NotEqual("Property has been set.", refused);
            Assert.Equal(4, stone.Design);
            player.Delete();
        }
        finally
        {
            CommandLogging.Enabled = was;
            stone.Delete();
            gm.Delete();
        }
    }

    [Fact]
    public void TheServersTiledataForTheFourBaseIdsIsOurs()
    {
        ShatteredLegacyArt.Apply();

        foreach (var id in Expected)
        {
            var d = TileData.ItemTable[id];
            _out.WriteLine($"0x{id:X4}: '{d.Name}' flags 0x{(ulong)d.Flags:X8} weight {d.Weight} height {d.Height}");

            Assert.Equal("shattered runestone", d.Name);
            Assert.Equal((TileFlag)0x01040040, d.Flags); // records.json "flags"
            Assert.True(d.Impassable);
            Assert.False(d.Surface);
            Assert.Equal(1, d.Weight);
            Assert.Equal(3, d.Height);
            Assert.Equal(3, d.CalcHeight);
        }

        // Only the four base IDs: the frames are drawn by the client and never stand in the world.
        Assert.NotEqual("shattered runestone", TileData.ItemTable[Expected[0] + 1].Name);
    }

    [Fact]
    public void TheResetStoneWearsDesignOneAndAnOldOneTakesItAtLoad()
    {
        var stone = new ResetStone();
        Assert.Equal(Expected[0], stone.ItemID);
        Assert.Equal(0, stone.Hue);

        // As saved before F-8.
        stone.ItemID = 0xED4;
        stone.Hue = 0x2B;
        var loaded = RoundTrip(stone);
        _out.WriteLine($"a pre-F-8 Reset Stone loads as 0x{loaded.ItemID:X4}, hue {loaded.Hue}");

        Assert.Equal(Expected[0], loaded.ItemID);
        Assert.Equal(0, loaded.Hue);
        Assert.False(loaded.Movable);

        stone.Delete();
        loaded.Delete();
    }
}
