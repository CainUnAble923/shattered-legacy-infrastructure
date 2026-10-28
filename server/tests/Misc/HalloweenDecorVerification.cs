// HalloweenDecorVerification.cs
//
// cc-P267: the Halloween decor at the main banks and the shared seasonal switch. Notes in shard-migration
// notes/cc-P267-halloween-bank-decor.md.
//
// What the facts pin:
//   1. The placement table loads: keys unique, no item twice on a facet tile, every row's bank is a listed bank on the
//      same facet, every facet is a named map, every item ID is one of the stock pieces the table uses, no row sits
//      on or beside a banker's tile or on a teleporter tile listed for its box. New Haven's bank is the Second Bank of
//      Haven on SoS (cc-P678), its rows in the town at that bank.
//   2. The season window: "MM-DD" parsing, the default October 24 to November 2 inclusive, a window that wraps the
//      year (December 1 to January 6), a bad setting falls back to the default and says so.
//   3. The automatic check acts only when the season changes: it puts the decor up when the window opens, leaves a
//      staff clear inside the window alone, takes it down after the window closes; a world first seen out of season
//      is left as it is, a world first seen in season is brought up.
//   4. place twice places once; clear removes exactly the decor, by serial, and leaves an unrelated item and a
//      same-art lookalike on the same tile; place after clear restores the same set; preview and clear dryrun change
//      nothing; a bank or facet filter places and clears only that part.
//   5. Survives a restart: the item round-trips with its key and bank; the saved state round-trips the last automatic
//      state and the serials; after the state is reloaded the placed items are still the tracked ones, an in-season
//      check adds nothing, and clear removes exactly them; a serial whose item is gone is dropped without fuss; a stray
//      HalloweenDecor the list does not know is swept and reported.
//   6. The setting keys are exactly clusterf.seasonal.halloween.start, .end and .automatic.
// The test host has no map files and no tiledata (D27), so nothing here asks the map about z or art: the rows were
// checked offline against the server's map files (notes/cc-P267-tools/validate.py, its table in the notes); New
// Haven's rows against the SoS map files (notes/cc-P678-tools/art/hw_validate.py, hw-validate.out.txt).

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HalloweenDecorVerification
{
    // The stock pieces the generator draws from (notes/cc-P267-tools/gen.py). A row with any other ID is a mistake.
    private static readonly HashSet<int> StockIds = new()
    {
        0x0C6A, 0x0C6B, 0x0C6C, 0x0C64, 0x4691, 0x4695,
        0x0ECA, 0x0ECB, 0x0ECD, 0x0ECE, 0x0ED0, 0x1ADA, 0x1ADD, 0x1AE0, 0x1AE1, 0x1AE2, 0x1AE3,
        0x0F34, 0x0F35, 0x1A92, 0x1A94,
        0x0974, 0x1E34, 0x1E35, 0x0ED5, 0x0EDB,
        0x0EE3, 0x0EE4, 0x0EE5, 0x0EE6, 0x10D2, 0x10D4,
        0x1854, 0x1858, 0x0A0F, 0x0A22
    };

    private static void Clean()
    {
        foreach (var i in World.Items.Values.OfType<HalloweenDecor>().ToList())
        {
            i.Delete();
        }

        ClusterFSeasonalState.Clear();
        ClusterFHalloweenDecor.Season.Window = SeasonWindow.Parse(ClusterFHalloweenDecor.DefaultStart, ClusterFHalloweenDecor.DefaultEnd);
    }

    private static DateTime At(int y, int m, int d, int h = 12) => new(y, m, d, h, 30, 0);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ThePlacementTableLoadsAndStaysOffBankersAndTeleporters()
    {
        var rows = ClusterFHalloweenDecorLayout.Rows;
        var banks = ClusterFHalloweenDecorLayout.Banks;
        Assert.True(rows.Length >= 200, $"{rows.Length} rows");
        Assert.True(banks.Length >= 13, $"{banks.Length} banks");
        Assert.Equal(rows.Length, rows.Select(r => r.Key).Distinct().Count());
        Assert.Equal(rows.Length, rows.Select(r => (r.Facet, r.X, r.Y, r.ItemId)).Distinct().Count());
        Assert.Equal(banks.Length, banks.Select(b => b.Key).Distinct().Count());

        var byKey = banks.ToDictionary(b => b.Key);
        var tele = new HashSet<(string, int, int)>(ClusterFHalloweenDecorLayout.TeleporterTiles);
        foreach (var r in rows)
        {
            Assert.True(byKey.TryGetValue(r.Bank, out var bank), $"{r.Key}: bank {r.Bank} is not listed");
            Assert.Equal(bank.Facet, r.Facet);
            Assert.Equal(bank.Name, r.BankName);
            Assert.NotNull(ClusterFHalloweenDecor.MapOf(r.Facet));
            Assert.True(StockIds.Contains(r.ItemId), $"{r.Key}: 0x{r.ItemId:X4} is not one of the stock pieces");
            Assert.False(string.IsNullOrWhiteSpace(r.Name), r.Key);
            Assert.Contains(r.Mode, new[] { "outside", "corner", "indoor", "table" });
            foreach (var (bx, by) in bank.Bankers)
            {
                Assert.True(Math.Max(Math.Abs(r.X - bx), Math.Abs(r.Y - by)) >= 2, $"{r.Key} at {r.X},{r.Y} is on or beside the banker at {bx},{by}");
            }

            Assert.False(tele.Contains((r.Facet, r.X, r.Y)), $"{r.Key} is on a teleporter tile");
        }

        // Every bank the prompt named is there, on its facet, and has rows.
        foreach (var (key, facet) in new[] { ("new-haven", "Trammel"), ("britain", "Trammel"), ("luna", "Malas"), ("moonglow", "Trammel"), ("trinsic", "Trammel"), ("vesper", "Trammel"), ("minoc", "Trammel"), ("yew", "Trammel"), ("skara-brae", "Trammel"), ("jhelom", "Trammel") })
        {
            Assert.True(byKey.TryGetValue(key, out var b) && b.Facet == facet, key);
            Assert.True(rows.Count(r => r.Bank == key) >= 5, $"{key}: {rows.Count(r => r.Bank == key)} rows");
        }

        // cc-P678: New Haven's bank is the Second Bank of Haven on SoS: pinned's banker spawner and our Sarsmea Smythe,
        // and every New Haven row is in the town, inside the bank's box (3615-3631, 2609-2623) or at its outer wall.
        Assert.Equal(new[] { (3620, 2617), (3624, 2616) }, byKey["new-haven"].Bankers);
        Assert.Equal(new Point3D(3620, 2617, 0), HavenSpots.Bank);
        foreach (var r in rows.Where(r => r.Bank == "new-haven"))
        {
            Assert.True(HavenSpots.InTown(new Point3D(r.X, r.Y, r.Z)), r.Key);
            Assert.InRange(r.X, 3614, 3632);
            Assert.InRange(r.Y, 2608, 2624);
        }
    }

    // ---------------------------------------------------------------- 2

    [Theory]
    [InlineData("10-24", "11-02", 2026, 10, 23, false)]
    [InlineData("10-24", "11-02", 2026, 10, 24, true)]
    [InlineData("10-24", "11-02", 2026, 10, 31, true)]
    [InlineData("10-24", "11-02", 2026, 11, 2, true)]
    [InlineData("10-24", "11-02", 2026, 11, 3, false)]
    [InlineData("10-24", "11-02", 2026, 7, 4, false)]
    [InlineData("12-01", "01-06", 2026, 11, 30, false)]
    [InlineData("12-01", "01-06", 2026, 12, 1, true)]
    [InlineData("12-01", "01-06", 2026, 12, 31, true)]
    [InlineData("12-01", "01-06", 2027, 1, 6, true)]
    [InlineData("12-01", "01-06", 2027, 1, 7, false)]
    [InlineData("12-01", "01-06", 2027, 6, 15, false)]
    public void TheSeasonWindowIsInclusiveAndMayWrapTheYear(string start, string end, int y, int m, int d, bool inSeason)
    {
        var w = SeasonWindow.Parse(start, end);
        Assert.Equal(inSeason, w.Contains(At(y, m, d, 0)));
        Assert.Equal(inSeason, w.Contains(At(y, m, d, 23)));
    }

    [Fact]
    public void ABadDateSettingFallsBackToTheDefaultAndSaysSo()
    {
        var s = ClusterFHalloweenDecor.Season;
        try
        {
            Assert.Empty(s.ReadSettings("10-20", "11-05"));
            Assert.Equal("10-20 to 11-05", s.Window.ToString());

            var lines = s.ReadSettings("Oct 24", "11-05");
            Assert.Single(lines);
            Assert.Contains(s.StartKey, lines[0]);
            Assert.Equal("10-24 to 11-05", s.Window.ToString());

            lines = s.ReadSettings("10-20", "13-40");
            Assert.Single(lines);
            Assert.Contains(s.EndKey, lines[0]);
            Assert.Equal("10-20 to 11-02", s.Window.ToString());

            Assert.False(SeasonWindow.TryParseDay("02-30", out _, out _));
            Assert.True(SeasonWindow.TryParseDay("02-29", out _, out _));
            Assert.False(SeasonWindow.TryParseDay("", out _, out _));
            Assert.False(SeasonWindow.TryParseDay(null, out _, out _));
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheAutomaticCheckActsOnlyWhenTheSeasonChanges()
    {
        var s = ClusterFHalloweenDecor.Season;
        var rows = ClusterFHalloweenDecorLayout.Rows.Length;
        try
        {
            Clean();

            // A world first seen on October 1: nothing placed, the state remembered as down.
            Assert.Empty(s.Sync(At(2026, 10, 1)));
            Assert.Empty(ClusterFHalloweenDecor.Placed());
            Assert.False(s.LastAutomatic);

            // Staff put it up early for the test shard: later October checks leave it up.
            ClusterFHalloweenDecor.Up(false, null);
            Assert.Empty(s.Sync(At(2026, 10, 10)));
            Assert.Equal(rows, ClusterFHalloweenDecor.Placed().Count);

            // October 24: up (already up: nothing doubled).
            Assert.NotEmpty(s.Sync(At(2026, 10, 24, 0)));
            Assert.Equal(rows, ClusterFHalloweenDecor.Placed().Count);
            Assert.True(s.LastAutomatic);

            // Staff clear it on October 28: the checks inside the window leave it down.
            ClusterFHalloweenDecor.Down(false, null);
            Assert.Empty(s.Sync(At(2026, 10, 29)));
            Assert.Empty(ClusterFHalloweenDecor.Placed());

            // Up again by hand; November 2 is still in; November 3 takes it down.
            ClusterFHalloweenDecor.Up(false, null);
            Assert.Empty(s.Sync(At(2026, 11, 2, 23)));
            Assert.Equal(rows, ClusterFHalloweenDecor.Placed().Count);
            Assert.NotEmpty(s.Sync(At(2026, 11, 3, 0)));
            Assert.Empty(ClusterFHalloweenDecor.Placed());
            Assert.False(s.LastAutomatic);

            // A world first seen in season (a fresh test world on October 30): brought up.
            Clean();
            Assert.NotEmpty(s.Sync(At(2026, 10, 30)));
            Assert.Equal(rows, ClusterFHalloweenDecor.Placed().Count);

            // The dates come from the settings: with the window moved to 10-01 to 10-05 the same world is out of season.
            Clean();
            s.ReadSettings("10-01", "10-05");
            Assert.Empty(s.Sync(At(2026, 10, 30)));
            Assert.Empty(ClusterFHalloweenDecor.Placed());
            Assert.NotEmpty(s.Sync(At(2027, 10, 3)));
            Assert.Equal(rows, ClusterFHalloweenDecor.Placed().Count);
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void PlaceTwicePlacesOnceAndClearRemovesExactlyTheDecorBySerial()
    {
        var rows = ClusterFHalloweenDecorLayout.Rows;
        var first = rows[0];
        var map = ClusterFHalloweenDecor.MapOf(first.Facet);
        var stranger = new Item(0x0EED) { Movable = false };   // a gold coin pile, nothing to do with the decor
        var lookalike = new Item(first.ItemId) { Movable = false, Name = first.Name };   // same art, not ours
        try
        {
            Clean();
            stranger.MoveToWorld(new Point3D(first.X, first.Y, first.Z), map);
            lookalike.MoveToWorld(new Point3D(first.X, first.Y, first.Z), map);

            var before = World.Items.Count;
            ClusterFHalloweenDecor.Up(true, null);
            Assert.Equal(before, World.Items.Count);
            Assert.Empty(ClusterFSeasonalState.Placed(ClusterFHalloweenDecor.SeasonName));

            ClusterFHalloweenDecor.Up(false, null);
            ClusterFHalloweenDecor.Up(false, null);
            var placed = ClusterFHalloweenDecor.Placed();
            Assert.Equal(rows.Length, placed.Count);
            Assert.Equal(rows.Length, ClusterFSeasonalState.Placed(ClusterFHalloweenDecor.SeasonName).Count);
            Assert.Equal(rows.Select(r => r.Key).OrderBy(k => k), placed.Select(p => p.Key).OrderBy(k => k));
            Assert.Empty(ClusterFHalloweenDecor.Strays());
            foreach (var p in placed)
            {
                var r = rows.Single(x => x.Key == p.Key);
                Assert.Equal(new Point3D(r.X, r.Y, r.Z), p.Location);
                Assert.Equal(ClusterFHalloweenDecor.MapOf(r.Facet), p.Map);
                Assert.Equal(r.ItemId, p.ItemID);
                Assert.Equal(r.Bank, p.Bank);
                Assert.False(p.Movable);
                Assert.False(p.Decays);
                Assert.Contains(p.Serial, ClusterFSeasonalState.Placed(ClusterFHalloweenDecor.SeasonName));
            }

            before = World.Items.Count;
            ClusterFHalloweenDecor.Down(true, null);
            Assert.Equal(before, World.Items.Count);

            ClusterFHalloweenDecor.Down(false, null);
            ClusterFHalloweenDecor.Down(false, null);
            Assert.Empty(ClusterFHalloweenDecor.Placed());
            Assert.Empty(ClusterFSeasonalState.Placed(ClusterFHalloweenDecor.SeasonName));
            Assert.Equal(0, World.Items.Values.OfType<HalloweenDecor>().Count(i => !i.Deleted));
            Assert.False(stranger.Deleted);
            Assert.False(lookalike.Deleted);
            Assert.Equal(new Point3D(first.X, first.Y, first.Z), stranger.Location);

            ClusterFHalloweenDecor.Up(false, null);
            Assert.Equal(rows.Select(r => r.Key).OrderBy(k => k), ClusterFHalloweenDecor.Placed().Select(p => p.Key).OrderBy(k => k));
        }
        finally
        {
            Clean();
            stranger.Delete();
            lookalike.Delete();
        }
    }

    [Fact]
    public void ABankOrFacetFilterPlacesAndClearsOnlyThatPart()
    {
        var rows = ClusterFHalloweenDecorLayout.Rows;
        try
        {
            Clean();
            Assert.True(ClusterFHalloweenDecor.KnownFilter("luna"));
            Assert.True(ClusterFHalloweenDecor.KnownFilter("Malas"));
            Assert.False(ClusterFHalloweenDecor.KnownFilter("atlantis"));

            ClusterFHalloweenDecor.Up(false, "luna");
            var placed = ClusterFHalloweenDecor.Placed();
            Assert.Equal(rows.Count(r => r.Bank == "luna"), placed.Count);
            Assert.All(placed, p => Assert.Equal("luna", p.Bank));

            ClusterFHalloweenDecor.Up(false, "Trammel");
            placed = ClusterFHalloweenDecor.Placed();
            Assert.Equal(rows.Count(r => r.Bank == "luna" || r.Facet == "Trammel"), placed.Count);

            ClusterFHalloweenDecor.Down(false, "Trammel");
            placed = ClusterFHalloweenDecor.Placed();
            Assert.Equal(rows.Count(r => r.Bank == "luna"), placed.Count);
            Assert.All(placed, p => Assert.Equal("luna", p.Bank));

            ClusterFHalloweenDecor.Down(false, "luna");
            Assert.Empty(ClusterFHalloweenDecor.Placed());
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheDecorAndTheStateSurviveARestart()
    {
        var rows = ClusterFHalloweenDecorLayout.Rows;
        var s = ClusterFHalloweenDecor.Season;
        var stray = (HalloweenDecor)null;
        try
        {
            Clean();

            // The item round-trips with its key and bank.
            var row = rows.First(r => r.Mode == "table");
            var item = new HalloweenDecor(row);
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            item.Serialize(writer);
            writer.Flush();
            var copy = (HalloweenDecor)Activator.CreateInstance(typeof(HalloweenDecor), item.Serial)!;
            copy.Deserialize(new BufferReader(buffer));
            Assert.Equal(row.Key, copy.Key);
            Assert.Equal(row.Bank, copy.Bank);
            Assert.Equal(row.ItemId, copy.ItemID);
            Assert.Equal(row.Name, copy.Name);
            Assert.False(copy.Movable);
            item.Delete();

            // The season comes in, the decor goes up, the world "saves" (the state is written) ...
            Assert.NotEmpty(s.Sync(At(2026, 10, 25)));
            var serials = ClusterFSeasonalState.Placed(ClusterFHalloweenDecor.SeasonName).ToList();
            Assert.Equal(rows.Length, serials.Count);
            var saved = new byte[65536];
            var w = new BufferWriter(saved, true);
            ClusterFSeasonalState.WriteTo(w);
            w.Flush();

            // ... and the server restarts: the state is wiped and read back, the items are still in the world.
            ClusterFSeasonalState.Clear();
            Assert.Null(s.LastAutomatic);
            Assert.Empty(ClusterFSeasonalState.Placed(ClusterFHalloweenDecor.SeasonName));
            ClusterFSeasonalState.ReadFrom(new BufferReader(saved));
            Assert.True(s.LastAutomatic);
            Assert.Equal(serials.OrderBy(x => x.Value), ClusterFSeasonalState.Placed(ClusterFHalloweenDecor.SeasonName).OrderBy(x => x.Value));
            Assert.Equal(rows.Length, ClusterFHalloweenDecor.Placed().Count);

            // The in-season check after the restart adds nothing; the next place adds nothing either.
            Assert.Empty(s.Sync(At(2026, 10, 26)));
            Assert.Equal(rows.Length, World.Items.Values.OfType<HalloweenDecor>().Count(i => !i.Deleted));
            ClusterFHalloweenDecor.Up(false, null);
            Assert.Equal(rows.Length, World.Items.Values.OfType<HalloweenDecor>().Count(i => !i.Deleted));

            // A tracked item that vanished (deleted by hand) is dropped from the list without fuss.
            var gone = ClusterFHalloweenDecor.Placed()[0];
            var goneSerial = gone.Serial;
            gone.Delete();
            Assert.Equal(rows.Length - 1, ClusterFHalloweenDecor.Placed().Count);
            Assert.DoesNotContain(goneSerial, ClusterFSeasonalState.Placed(ClusterFHalloweenDecor.SeasonName));

            // A stray HalloweenDecor the list does not know (a serial the save lost) is swept by clear, and reported.
            stray = new HalloweenDecor(rows[1]);
            stray.MoveToWorld(new Point3D(rows[1].X, rows[1].Y, rows[1].Z), ClusterFHalloweenDecor.MapOf(rows[1].Facet));
            ClusterFSeasonalState.Placed(ClusterFHalloweenDecor.SeasonName).Remove(stray.Serial);
            Assert.Single(ClusterFHalloweenDecor.Strays());

            // The season ends after the restart: everything placed before it comes down, by the reloaded serials.
            var lines = s.Sync(At(2026, 11, 3));
            Assert.NotEmpty(lines);
            Assert.Contains(lines, l => l.Contains("stray"));
            Assert.Equal(0, World.Items.Values.OfType<HalloweenDecor>().Count(i => !i.Deleted));
            Assert.Empty(ClusterFSeasonalState.Placed(ClusterFHalloweenDecor.SeasonName));
            Assert.False(s.LastAutomatic);

            // The state round-trips each last-automatic value.
            foreach (var state in new bool?[] { null, true, false })
            {
                ClusterFSeasonalState.Clear();
                ClusterFSeasonalState.SetLast(ClusterFHalloweenDecor.SeasonName, state);
                ClusterFSeasonalState.Placed(ClusterFHalloweenDecor.SeasonName).Add((Serial)0x40001234u);
                var b = new byte[256];
                var bw = new BufferWriter(b, true);
                ClusterFSeasonalState.WriteTo(bw);
                bw.Flush();
                ClusterFSeasonalState.Clear();
                ClusterFSeasonalState.ReadFrom(new BufferReader(b));
                Assert.Equal(state, ClusterFSeasonalState.GetLast(ClusterFHalloweenDecor.SeasonName));
                Assert.Contains((Serial)0x40001234u, ClusterFSeasonalState.Placed(ClusterFHalloweenDecor.SeasonName));
            }
        }
        finally
        {
            Clean();
            stray?.Delete();
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheSettingKeysAreTheOnesThePromptNames()
    {
        var s = ClusterFHalloweenDecor.Season;
        Assert.Equal("clusterf.seasonal.halloween.start", s.StartKey);
        Assert.Equal("clusterf.seasonal.halloween.end", s.EndKey);
        Assert.Equal("clusterf.seasonal.halloween.automatic", s.AutomaticKey);
        Assert.Equal("10-24", s.DefaultStart);
        Assert.Equal("11-02", s.DefaultEnd);
        Assert.Same(s, ClusterFSeasonalSwitch.All["halloween"]);
    }
}
