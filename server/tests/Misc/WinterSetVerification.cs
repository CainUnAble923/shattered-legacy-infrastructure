// WinterSetVerification.cs
//
// cc-P856: New Haven's winter set (WinterSet/WinterSetAddon.cs, WinterSetDesigns.cs, ClusterFWinterSetLayout.cs), on
// the winter decor's switch (ClusterFWinterDecorSeeder.cs). Notes in shard-migration notes/cc-P856-new-haven-winter-set.md.
//
// Facts:
//   1. The five designs load with their parts as the downloads list them (counted by convert856.py: train 241, snow
//      globe 111, Santa bench 14, snowman bench 26, holiday fireplace 35), each with its author, and only stock art (no
//      ID in our art package's ranges).
//   2. A house copy from its deed: every part at its offset with its hue, light and name, its own hues kept, the author
//      on each part and on the deed, the deed back on a redeed, and the design kept through a save.
//   3. The winter set's rows go up with the decor as WinterSeasonAddons at their row's tile, tracked by serial; place
//      twice places once; clear takes each down with all its parts and nothing else; the seasonal copy has no deed.
//   4. A world whose decor is already up gets the winter set's missing rows at the next start (TopUp), once; a world
//      whose decor is down (out of season, never put up, or cleared whole by staff) gets nothing.
//   5. The rows: four on main (the park west of the hall and the garden south of the square), keys unique, each a
//      design, inside the town box; no holiday fireplace row on main.
//   6. The Festival's winter stock sells the Santa bench, the snowman bench and the holiday fireplace as deeds.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server;
using Server.Engines.RewardStores;
using Server.Items;
using Server.Mobiles;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class WinterSetVerification
{
    private static void Clean()
    {
        foreach (var i in World.Items.Values.Where(i => i is IWinterDecorItem).ToList())
        {
            i.Delete();
        }

        ClusterFSeasonalState.Clear();
        ClusterFWinterDecor.Season.Window = SeasonWindow.Parse(ClusterFWinterDecor.DefaultStart, ClusterFWinterDecor.DefaultEnd);
        ClusterFWinterDecor.Season.Automatic = true;
    }

    private static DateTime At(int y, int m, int d) => new(y, m, d, 12, 30, 0);

    private static List<WinterSeasonAddon> SetPieces() => ClusterFWinterDecor.Placed().OfType<WinterSeasonAddon>().ToList();

    // Our art package's ID ranges (registry.csv, as RewardStoreVerification): stock art lies outside every one.
    private static bool IsInOurArtRanges(int id) => id is >= 0x3C10 and <= 0x3D0D or >= 0x6D70 and <= 0x6E86 or >= 0x3384 and <= 0x33B6;

    // The property list's text arguments, as the client is sent them (ItemKnowledgeVerification.Sent).
    private static string Props(Item item)
    {
        var buffer = item.PropertyList.Buffer;
        var entries = new List<string>();
        var pos = 15;
        while (pos + 6 <= buffer.Length)
        {
            var number = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(pos));
            if (number == 0)
            {
                break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(pos + 4));
            entries.Add(Encoding.Unicode.GetString(buffer, pos + 6, length));
            pos += 6 + length;
        }

        return string.Join("\n", entries);
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

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheFiveDesignsLoadWithTheirAuthorsAndOnlyStockArt()
    {
        var expected = new (string Key, int Parts, string Credit)[]
        {
            ("christmas-train", 241, "Celestial"),
            ("large-snow-globe", 111, "Milva"),
            ("santa-bench", 14, "Candy Apple"),
            ("snowman-bench", 26, "Candy Apple"),
            ("holiday-fireplace", 35, "Lagatha, after Gwenivere (Knights of the Round Table shard)")
        };

        Assert.Equal(expected.Select(e => e.Key).OrderBy(k => k), WinterSetDesigns.All.Keys.OrderBy(k => k));
        foreach (var (key, parts, credit) in expected)
        {
            var d = WinterSetDesigns.All[key];
            Assert.Equal((key, parts, credit), (d.Key, d.Parts.Count, d.Credit));
            Assert.False(string.IsNullOrWhiteSpace(d.Name));
            Assert.All(d.Parts, p => Assert.InRange(p.ItemID, 1, 0xFFFF));
            Assert.All(d.Parts, p => Assert.False(IsInOurArtRanges(p.ItemID), $"{key}: 0x{p.ItemID:X4} is not stock art"));
            Assert.All(d.Parts, p => Assert.InRange(Math.Max(Math.Abs(p.X), Math.Abs(p.Y)), 0, 10));
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AHouseCopyFromItsDeedBuildsEveryPartAndCreditsItsAuthor()
    {
        var deed = new WinterSetDeed("santa-bench");
        WinterSetAddon addon = null;
        try
        {
            Assert.Equal("a deed for a Santa bench", deed.Name);
            Assert.Contains("Design by Candy Apple", Props(deed));
            Assert.Contains("House decor", Props(deed));

            addon = Assert.IsType<WinterSetAddon>(deed.Addon);
            var at = new Point3D(5720, 1210, 0);
            addon.MoveToWorld(at, Map.Felucca);
            var design = WinterSetDesigns.All["santa-bench"];
            Assert.Equal(design.Parts.Count, addon.Components.Count);
            for (var i = 0; i < design.Parts.Count; i++)
            {
                var part = design.Parts[i];
                var c = Assert.IsType<WinterSetComponent>(addon.Components[i]);
                Assert.Equal((part.ItemID, new Point3D(at.X + part.X, at.Y + part.Y, at.Z + part.Z), part.Hue),
                    (c.ItemID, c.Location, c.Hue));
                if (part.Light is { } light)
                {
                    Assert.Equal(light, c.Light);
                }

                if (part.Name != null)
                {
                    Assert.Equal(part.Name, c.Name);
                }
            }

            Assert.False(addon.ShareHue);
            Assert.Contains("Design by Candy Apple", Props(addon.Components[0]));

            var back = Assert.IsType<WinterSetDeed>(addon.Deed);
            Assert.Equal("santa-bench", back.Design);
            back.Delete();

            Assert.Equal("santa-bench", RoundTrip(addon).Design);
            Assert.Equal("santa-bench", RoundTrip(deed).Design);
        }
        finally
        {
            addon?.Delete();
            deed.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheSetGoesUpWithTheDecorBySerialAndComesDownWithAllItsParts()
    {
        Clean();
        var unrelated = new Static(0x0B2C);
        try
        {
            var row = ClusterFWinterSetLayout.Rows[0];
            unrelated.MoveToWorld(new Point3D(row.X, row.Y, row.Z), Map.Trammel);

            ClusterFWinterDecor.Up(false, null);
            var pieces = SetPieces();
            Assert.Equal(ClusterFWinterSetLayout.Rows.Select(r => r.Key).OrderBy(k => k), pieces.Select(p => p.Key).OrderBy(k => k));
            foreach (var p in pieces)
            {
                var r = ClusterFWinterSetLayout.Rows.Single(x => x.Key == p.Key);
                Assert.Equal((new Point3D(r.X, r.Y, r.Z), Map.Trammel), (p.Location, p.Map));
                Assert.Equal(ClusterFWinterSetLayout.DesignOf(r.Key), p.Design);
                Assert.Equal(WinterSetDesigns.All[p.Design].Parts.Count, p.Components.Count);
                Assert.Contains(p.Serial, ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName));
                Assert.Null(p.Deed);
                Assert.False(p.Decays);
            }

            var before = World.Items.Count;
            ClusterFWinterDecor.Up(false, null);
            Assert.Equal(before, World.Items.Count);

            var parts = pieces.SelectMany(p => p.Components).ToList();
            ClusterFWinterDecor.Down(false, null);
            Assert.All(pieces, p => Assert.True(p.Deleted));
            Assert.All(parts, c => Assert.True(c.Deleted));
            Assert.False(unrelated.Deleted);
            Assert.Empty(ClusterFWinterDecor.Placed());
        }
        finally
        {
            unrelated.Delete();
            ClusterFWinterDecor.Down(false, null);
            Clean();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void AWorldWhoseDecorIsUpGetsTheMissingSetPiecesAtTheNextStartAndOnlyThen()
    {
        Clean();
        var s = ClusterFWinterDecor.Season;
        var clock = s.Clock;
        try
        {
            // The decor went up on December 1 before this build: every row but the winter set's.
            s.Clock = () => At(2026, 12, 5);
            s.LastAutomatic = true;
            foreach (var area in ClusterFWinterDecorLayout.Rows.Select(r => r.Area).Distinct())
            {
                ClusterFWinterDecor.Up(false, area);
            }

            foreach (var p in SetPieces())
            {
                ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName).Remove(p.Serial);
                p.Delete();
            }

            Assert.Empty(SetPieces());
            var older = ClusterFWinterDecor.Placed().Count;
            Assert.Equal(ClusterFWinterDecorLayout.Rows.Length, older);

            // The next start: the winter set arrives, once.
            Assert.NotEmpty(ClusterFWinterDecor.TopUp());
            Assert.Equal(ClusterFWinterSetLayout.Rows.Length, SetPieces().Count);
            Assert.Empty(ClusterFWinterDecor.TopUp());
            Assert.Equal(older + ClusterFWinterSetLayout.Rows.Length, ClusterFWinterDecor.Placed().Count);

            // Staff cleared the whole decor: it stays down.
            ClusterFWinterDecor.Down(false, null);
            Assert.Empty(ClusterFWinterDecor.TopUp());
            Assert.Empty(ClusterFWinterDecor.Placed());

            // Out of season, or never put up by the check: nothing.
            ClusterFWinterDecor.Up(false, "square");
            foreach (var p in SetPieces())
            {
                ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName).Remove(p.Serial);
                p.Delete();
            }

            s.Clock = () => At(2027, 2, 1);
            Assert.Empty(ClusterFWinterDecor.TopUp());
            s.Clock = () => At(2026, 12, 5);
            s.LastAutomatic = false;
            Assert.Empty(ClusterFWinterDecor.TopUp());
            s.LastAutomatic = null;
            Assert.Empty(ClusterFWinterDecor.TopUp());
            Assert.Empty(SetPieces());
        }
        finally
        {
            s.Clock = clock;
            ClusterFWinterDecor.Down(false, null);
            Clean();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void FourRowsOnMainEachADesignInsideTheTown()
    {
        var rows = ClusterFWinterSetLayout.Rows;
        Assert.Equal(4, rows.Length);
        Assert.Equal(rows.Length, rows.Select(r => r.Key).Distinct().Count());
        Assert.Equal(
            new[] { "christmas-train", "large-snow-globe", "santa-bench", "snowman-bench" },
            rows.Select(r => ClusterFWinterSetLayout.DesignOf(r.Key)).OrderBy(d => d));
        foreach (var r in rows)
        {
            Assert.Equal(ClusterFWinterSetLayout.AddonLayer, r.Layer);
            Assert.True(WinterSetDesigns.All.ContainsKey(ClusterFWinterSetLayout.DesignOf(r.Key)), r.Key);
            Assert.True(HavenSpots.Town.Contains(new Point2D(r.X, r.Y)), $"{r.Key} at {r.X},{r.Y} is outside Haven's town box");
            Assert.Equal(WinterSetDesigns.All[ClusterFWinterSetLayout.DesignOf(r.Key)].Name, r.Name);
        }

        Assert.Contains(ClusterFWinterDecor.Areas(), a => a == "park");
        Assert.Equal(ClusterFWinterDecorLayout.Rows.Length + rows.Length, ClusterFWinterDecor.AllRows.Length);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheFestivalWinterStockSellsTheBenchesAndTheFireplaceAsDeeds()
    {
        var rows = FestivalStock.For(FestivalSeasons.Winter);
        foreach (var (key, design) in new[]
                 {
                     ("festival.winter.santabench", "santa-bench"),
                     ("festival.winter.snowmanbench", "snowman-bench"),
                     ("festival.winter.holidayfireplace", "holiday-fireplace")
                 })
        {
            var row = rows.Single(r => r.Key == key);
            var items = row.Create!(null!);
            var deed = Assert.IsType<WinterSetDeed>(Assert.Single(items));
            Assert.Equal(design, deed.Design);
            deed.Delete();
        }
    }
}
