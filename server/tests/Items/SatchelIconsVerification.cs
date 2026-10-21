// SatchelIconsVerification.cs
//
// cc-P236: Chase's satchel and guild bag icons (made in Codex from his own art, approved 2026-10-05) as our own art IDs
// 0x3380-0x3385 (player-package\vendor\shattered-legacy-art\registry.csv), the classes pointed at them, and items
// already in the world moved over on load.
// Notes: shard-migration notes/cc-P236-satchel-icons-ship.md.
//
// Facts:
//   1. Each mapped class constructs with its registry ID and its hue (the Artificer's Satchel takes the decided 0xDC).
//   2. Each keeps its container gump, bounds and drop sound: the satchels the backpack-style 0x3C, the two bags 0xE76's.
//   3. The server knows each new ID as the client does: SatchelArt.Records, PartialHue and Container set.
//   4. A version 0 save still on the stock graphic loads on the new ID; one a staff member changed to anything else keeps
//      it; a version 1 save is never moved (a staff member may set the stock graphic back on purpose).
//   5. A version 0 Artificer's Satchel in the old 0x497 takes 0xDC; a staff hue is kept.

using System;
using Server;
using Server.Items;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SatchelIconsVerification
{
    private readonly ITestOutputHelper _out;

    public SatchelIconsVerification(ITestOutputHelper output) => _out = output;

    private const int StockSatchel = 0xA272;
    private const int StockBag = 0xE76;

    // Each class, the ID it must wear, its hue, and how many class layers below it write their version after the layer
    // that carries the art (the satchel family's base class), so the test can write that layer's save as version 0.
    public static TheoryData<Type, int, int, int, int> Classes => new()
    {
        { typeof(CompactOreSatchel), 0x3380, -1, StockSatchel, 0 },
        { typeof(ReinforcedOreSatchel), 0x3380, -1, StockSatchel, 1 },
        { typeof(SurveyorsSatchel), 0x3380, -1, StockSatchel, 1 },
        { typeof(DeepdelversSatchel), 0x3380, -1, StockSatchel, 1 },
        { typeof(MasterExpeditionSatchel), 0x3380, -1, StockSatchel, 1 },
        { typeof(ForestersLumberSatchel), 0x3381, 0x84C, StockSatchel, 0 },
        { typeof(SeasonedLumberSatchel), 0x3381, 0x60, StockSatchel, 1 },
        { typeof(ArboristLumberSatchel), 0x3381, 0x26C, StockSatchel, 2 },
        { typeof(HuntersSatchel), 0x3382, 0x5B5, StockSatchel, 0 },
        { typeof(SeasonedHuntersSatchel), 0x3382, 0x5B2, StockSatchel, 1 },
        { typeof(WardensFieldSatchel), 0x3382, 0x4B5, StockSatchel, 2 },
        { typeof(ArtificersSatchel), 0x3383, 0xDC, StockSatchel, 0 },
        { typeof(GemologistsSatchel), 0x3384, 1177, StockBag, 0 },
        { typeof(SmithGuildSalvageBag), 0x3385, 1154, StockBag, 0 }
    };

    private static Container Make(Type t) => (Container)Activator.CreateInstance(t);

    private static Container Load(Type t, Serial serial, byte[] buffer)
    {
        var copy = (Container)Activator.CreateInstance(t, serial);
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    // Saves the item, then writes the art layer's version as `version`. [SerializationGenerator(n, false)] writes each
    // layer's version as a plain int (4 bytes, little-endian), and the layers below the art's have no fields, so each
    // writes only those 4 bytes, at the end of the save.
    private static byte[] SaveAs(Container item, int depth, int version, out int length)
    {
        var buffer = new byte[16384];
        var writer = new BufferWriter(buffer, true);
        item.Serialize(writer);
        writer.Flush();
        length = (int)writer.Position;
        var at = length - 4 * (depth + 1);
        Assert.True(buffer[at] == 1 && buffer[at + 1] == 0 && buffer[at + 2] == 0 && buffer[at + 3] == 0, $"{item.GetType().Name}: the art layer saves as version 1 (byte {at} is {buffer[at]}; save {Convert.ToHexString(writer.Buffer, 0, length)}, writer.Buffer is buffer: {ReferenceEquals(writer.Buffer, buffer)})");
        buffer[at] = (byte)version;
        return buffer;
    }

    [Theory]
    [MemberData(nameof(Classes))]
    public void EachClassConstructsWithItsRegistryIdAndHue(Type t, int id, int hue, int stock, int depth)
    {
        var item = Make(t);
        try
        {
            _out.WriteLine($"{t.Name}: 0x{item.ItemID:X4} hue 0x{item.Hue:X} (was 0x{stock:X4}; layers below the art's {depth})");
            Assert.Equal(id, item.ItemID);
            if (hue >= 0)
            {
                Assert.Equal(hue, item.Hue);
            }
            else
            {
                Assert.Equal(((CompactOreSatchel)item).TierHue, item.Hue);
            }
        }
        finally
        {
            item.Delete();
        }
    }

    [Theory]
    [MemberData(nameof(Classes))]
    public void EachKeepsItsContainerGump(Type t, int id, int hue, int stock, int depth)
    {
        var item = Make(t);
        try
        {
            var was = ContainerData.GetData(stock);
            _out.WriteLine($"{t.Name} (0x{id:X4}, hue {hue}, depth {depth}): gump 0x{item.GumpID:X} bounds {item.Bounds} sound 0x{item.DropSound:X}");
            Assert.Equal(stock == StockBag ? 0x3D : 0x3C, item.GumpID);
            Assert.Equal(was.Bounds, item.Bounds);
            Assert.Equal(was.DropSound, item.DropSound);
        }
        finally
        {
            item.Delete();
        }
    }

    [Fact]
    public void TheServerKnowsEachNewIdAsTheClientDoes()
    {
        SatchelArt.Apply();
        Assert.Equal(6, SatchelArt.Records.Length);
        foreach (var r in SatchelArt.Records)
        {
            var d = TileData.ItemTable[r.Id];
            _out.WriteLine($"0x{r.Id:X4} '{d.Name}' {d.Flags} weight {d.Weight} height {d.Height}");
            Assert.InRange(r.Id, 0x3380, 0x3385);
            Assert.True(r.Name.Length <= 19, r.Name);
            Assert.Equal(r.Name, d.Name);
            Assert.True(d.Flags.HasFlag(TileFlag.PartialHue), $"0x{r.Id:X4} PartialHue");
            Assert.True(d.Flags.HasFlag(TileFlag.Container), $"0x{r.Id:X4} Container");
            Assert.Equal(r.Weight, d.Weight);
            Assert.Equal(r.Height, d.Height);
        }
    }

    [Theory]
    [MemberData(nameof(Classes))]
    public void AnOldGraphicMovesOnLoadAndAStaffChoiceStays(Type t, int id, int hue, int stock, int depth)
    {
        // Version 0, still on the stock graphic: moves.
        var item = Make(t);
        item.ItemID = stock;
        var buffer = SaveAs(item, depth, 0, out _);
        var copy = Load(t, item.Serial, buffer);
        _out.WriteLine($"{t.Name} v0 on 0x{stock:X4}: loads on 0x{copy.ItemID:X4} hue 0x{copy.Hue:X} (constructed {hue})");
        Assert.Equal(id, copy.ItemID);

        // Version 0, a staff member's other graphic: kept.
        item.ItemID = 0xE75;
        copy = Load(t, item.Serial, SaveAs(item, depth, 0, out _));
        Assert.Equal(0xE75, copy.ItemID);

        // Version 1 on the stock graphic (set back by staff after the move): kept.
        item.ItemID = stock;
        copy = Load(t, item.Serial, SaveAs(item, depth, 1, out _));
        Assert.Equal(stock, copy.ItemID);

        // And the round trip of a new item keeps its ID.
        item.ItemID = id;
        copy = Load(t, item.Serial, SaveAs(item, depth, 1, out _));
        Assert.Equal(id, copy.ItemID);

        item.Delete();
    }

    [Fact]
    public void AnOldArtificersSatchelTakesTheDecidedHueAndAStaffHueStays()
    {
        var item = new ArtificersSatchel { ItemID = StockSatchel, Hue = 0x497 };
        var copy = Load(typeof(ArtificersSatchel), item.Serial, SaveAs(item, 0, 0, out _));
        Assert.Equal(0xDC, copy.Hue);

        item.Hue = 0x21;
        copy = Load(typeof(ArtificersSatchel), item.Serial, SaveAs(item, 0, 0, out _));
        Assert.Equal(0x21, copy.Hue);

        item.Delete();
    }
}
