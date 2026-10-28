// WinterDecorVerification.cs
//
// cc-P34 and cc-P266 (F-22): the dated winter decor for New Haven on the shared seasonal switch. Notes in
// shard-migration notes/cc-P34-winter-decor.md and notes/cc-P266-new-haven-winter-decor.md. cc-P678 moved every row into
// SoS Haven (notes/cc-P678-tools/winter): the rows sit in HavenSpots.Town, and the fixed tiles are HavenSpots' own.
//
// What the facts pin:
//   1. The placement table loads: keys unique, no item twice on a tile, every item ID is a registered winter ID (the
//      layout's RegisteredIds, from registry.csv: P34's 0x3C40-0x3C6E and P266's 0x3388-0x33B6) or one of the OSI
//      holiday IDs pinned's own classes use; the bank, the square and the main roads have trees, lights, garlands and
//      snow; exactly one row is the advent calendar.
//   2. No row sits on an NPC post, a spawner tile, the quest board, the arrival tile, Rook, the moongate's way out,
//      the Fountain of Youth or the Reset Stone, on a League field office component, or on
//      a P237 community-art tile (the two seeders never share a spot). The posts, office, community rows and
//      HavenSpots are the seeders' own tables, not copies. (cc-P678: doors and their swings are the SoS validator's.)
//   3. The switch: the setting keys are clusterf.seasonal.winter.start, .end and .automatic with defaults 12-01 and
//      01-06; the window is inclusive and wraps the year; a bad setting falls back and says so.
//   4. The automatic check acts only when the season changes: the decor is placed in range (December 1), a staff
//      clear inside the window stands, it is removed after January 6; a world first seen out of season is left as it
//      is, one first seen in season is brought up; the dates come from the settings.
//   5. place twice places once; clear removes exactly the decor, by serial, and leaves an unrelated item and a
//      same-art lookalike on the same tile (nothing else touched); place after clear restores the same set; preview
//      and clear dryrun change nothing; an area filter places and clears only that area.
//   6. Survives a restart: the items round-trip with their keys; after the state is reloaded the placed items are
//      still the tracked ones, an in-season check adds nothing, a vanished item is dropped from the list, a stray is
//      swept and reported.
//   7. The server's in-memory records for the winter art are in place after ShatteredLegacyArt.Apply.
//   8. The advent calendar: door N opens from December N, once per character per year, not before December and not
//      for a day not yet come; the gift lands in the backpack; a new year starts fresh; the calendar is placed by the
//      seeder as its own type and comes down with the rest.
// The test host has no map files, so nothing here asks the map about z: the rows were checked offline against the
// Siege of Sosaria map files (notes/cc-P678-tools/winter/validate.py; P266's were the old map's).

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class WinterDecorVerification
{
    // The OSI IDs the rows may use, each from pinned's own class: Snowman.cs:5, Wreath.cs:254/258, Mistletoe.cs:266/270,
    // Stockings.cs:6/20, HolidayBell.cs:29, SnowPiles.cs:8, SnowyTree.cs:9.
    private static readonly HashSet<int> OsiIds = new()
    {
        0x2328, 0x2329, 0x232C, 0x232D, 0x2374, 0x2375, 0x2BD9, 0x2BDA, 0x2BDB, 0x2BDC, 0x1C12,
        0x08E2, 0x08E0, 0x08E6, 0x08E5, 0x08E3, 0x2377
    };

    // cc-P678: the board and the arrival tile are HavenSpots' (the square in SoS Haven).
    private static readonly (int X, int Y) QuestBoard = (HavenSpots.QuestBoard.X, HavenSpots.QuestBoard.Y);
    private static readonly (int X, int Y) ArrivalTile = (HavenSpots.Start.X, HavenSpots.Start.Y);

    private static void Clean()
    {
        foreach (var i in World.Items.Values.Where(i => i is IWinterDecorItem).ToList())
        {
            i.Delete();
        }

        ClusterFSeasonalState.Clear();
        ClusterFWinterDecor.Season.Window = SeasonWindow.Parse(ClusterFWinterDecor.DefaultStart, ClusterFWinterDecor.DefaultEnd);
    }

    private static DateTime At(int y, int m, int d, int h = 12) => new(y, m, d, h, 30, 0);

    private static List<Item> Placed() => ClusterFWinterDecor.Placed();

    private static IEnumerable<string> Keys(IEnumerable<Item> items) => items.Select(i => ((IWinterDecorItem)i).Key).OrderBy(k => k);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ThePlacementTableLoadsAndEveryItemIsRegisteredOrStock()
    {
        var rows = ClusterFWinterDecorLayout.Rows;
        var registered = new HashSet<int>(ClusterFWinterDecorLayout.RegisteredIds);
        Assert.True(rows.Length >= 100, $"{rows.Length} rows");
        Assert.Equal(rows.Length, rows.Select(r => r.Key).Distinct().Count());
        Assert.Equal(rows.Length, rows.Select(r => (r.X, r.Y, r.Z, r.ItemId)).Distinct().Count());
        Assert.True(registered.Count >= 94, $"{registered.Count} registered IDs");
        foreach (var id in registered)
        {
            Assert.True(ClusterFWinterDecorArt.IsOurs(id), $"0x{id:X4} is registered as winter art but outside both blocks");
        }

        foreach (var r in rows)
        {
            Assert.True(registered.Contains(r.ItemId) || OsiIds.Contains(r.ItemId), $"{r.Key}: 0x{r.ItemId:X4} is neither a registered winter ID nor an OSI holiday ID");
            Assert.False(string.IsNullOrWhiteSpace(r.Name), r.Key);
            // the entrance arches stand on the roads into town, so they may sit up to 3 tiles outside the town box
            var slack = r.Area == "entrance" ? 3 : 0;
            var box = new Rectangle2D(HavenSpots.Town.X - slack, HavenSpots.Town.Y - slack, HavenSpots.Town.Width + 2 * slack,
                HavenSpots.Town.Height + 2 * slack);
            Assert.True(box.Contains(new Point2D(r.X, r.Y)), $"{r.Key} at {r.X},{r.Y} is outside Haven's town box");
            Assert.Contains(r.Layer, new[] { "osi", "community", ClusterFWinterDecor.CalendarLayer });
        }

        // Every record's ID is registered.
        foreach (var rec in ClusterFWinterDecorArt.Records)
        {
            Assert.Contains(rec.Id, registered);
        }

        // P34's layers: the square's tree, statue and firepit, the inn's fireplace and a stove, the sleigh.
        foreach (var id in new[] { 0x3C52, 0x3C40, 0x3C45, 0x3C60, 0x3C4A, 0x3C4E, 0x3C6E })
        {
            Assert.Contains(rows, r => r.ItemId == id);
        }

        // P266's: the bank has the large lit tree, garlands, candles and snow outside; the square and the main roads
        // have the small lit trees and snow; exactly one advent calendar, in the bank.
        var bank = rows.Where(r => r.Area == "bank").ToList();
        Assert.True(bank.Count >= 25, $"{bank.Count} bank rows");
        Assert.Contains(bank, r => r.ItemId == 0x3388);
        Assert.Contains(bank, r => r.ItemId is 0x339E or 0x339F);
        Assert.Contains(bank, r => r.ItemId is 0x33A0 or 0x33A4 or 0x33A8);
        Assert.Contains(bank, r => r.ItemId is 0x08E2 or 0x08E0 or 0x08E6 or 0x08E5 or 0x08E3 or 0x3C6B or 0x3C6C or 0x3C6D);
        Assert.True(bank.Count(r => r.ItemId == 0x3393) >= 4, "small trees outside the bank");
        Assert.True(rows.Count(r => r.Area == "square" && r.ItemId == 0x3393) >= 2, "small trees on the square");
        Assert.True(rows.Count(r => r.Area == "main road" && r.ItemId == 0x3393) >= 6, "small trees beside the main roads");
        var calendars = rows.Where(r => r.Layer == ClusterFWinterDecor.CalendarLayer).ToList();
        Assert.Single(calendars);
        Assert.Equal("bank", calendars[0].Area);
        Assert.True(calendars[0].ItemId is 0x33B5 or 0x33B6, "the calendar uses its own art");
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void NoRowSitsOnADoorItsSwingAnNpcASpawnerAFixedTileOrACommunityArtTile()
    {
        var bad = new Dictionary<(int, int), string>();
        void Add(int x, int y, string why) => bad.TryAdd((x, y), why);

        // cc-P678: the doors were ClusterFNewHavenServicesSeeder's (ServUO NewHaven.cfg, the ML plaza's), retired with
        // the plaza. SoS Haven's doors are pinned's decoration and DoorGenerator's, which the layout's SoS validator
        // keeps clear with their swing tiles (shard-migration notes/cc-P678-tools/winter/validate.py).

        foreach (var e in ClusterFNewHavenSeeder.Entries)
        {
            var p = e.GetLocation();
            Add(p.X, p.Y, $"NPC {e.Label}");
        }

        foreach (var (x, y) in ClusterFWinterDecorLayout.SpawnerTiles)
        {
            Add(x, y, "spawner");
        }

        Add(QuestBoard.X, QuestBoard.Y, "quest board");
        Add(ArrivalTile.X, ArrivalTile.Y, "arrival tile");
        Add(ClusterFResetStoneSeeder.Location.X, ClusterFResetStoneSeeder.Location.Y, "Reset Stone");
        Add(HavenSpots.Rook.X, HavenSpots.Rook.Y, "Rook");
        Add(HavenSpots.MoongateExit.X, HavenSpots.MoongateExit.Y, "the moongate's way out");

        // cc-P678: the Fountain of Youth's addon, pinned StoneFountainAddon's offsets -2..+1 from its spot.
        for (var dx = -2; dx <= 1; dx++)
        {
            for (var dy = -2; dy <= 1; dy++)
            {
                Add(HavenSpots.FountainOfYouth.X + dx, HavenSpots.FountainOfYouth.Y + dy, "the Fountain of Youth");
            }
        }

        foreach (var c in ClusterFRegistrarOfficeLayout.Components)
        {
            Add(c.X, c.Y, $"office {c.Name}");
        }

        // cc-P266: P237's community art keeps its spots.
        Assert.True(ClusterFCommunityArtLayout.Rows.Length >= 36, "the community art table"); // cc-P678: 36 on SoS
        foreach (var c in ClusterFCommunityArtLayout.Rows)
        {
            Add(c.X, c.Y, $"community art {c.Key}");
        }

        Assert.Equal(ArrivalTile, (ClusterFNewHavenSeeder.SpotOf(typeof(Server.Engines.MLQuests.Mobiles.SirHelper)).X, ClusterFNewHavenSeeder.SpotOf(typeof(Server.Engines.MLQuests.Mobiles.SirHelper)).Y));

        foreach (var r in ClusterFWinterDecorLayout.Rows)
        {
            Assert.False(bad.TryGetValue((r.X, r.Y), out var why), $"{r.Key} at {r.X},{r.Y} is on a protected tile: {why}");
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheSettingKeysAndDefaultsAreThePromptsAndTheSwitchIsTheSharedOne()
    {
        var s = ClusterFWinterDecor.Season;
        Assert.Equal("clusterf.seasonal.winter.start", s.StartKey);
        Assert.Equal("clusterf.seasonal.winter.end", s.EndKey);
        Assert.Equal("clusterf.seasonal.winter.automatic", s.AutomaticKey);
        Assert.Equal("12-01", s.DefaultStart);
        Assert.Equal("01-06", s.DefaultEnd);
        Assert.Same(s, ClusterFSeasonalSwitch.All["winter"]);
        Assert.NotSame(s, ClusterFHalloweenDecor.Season);   // touching the class registers its switch in the test host
    }

    [Theory]
    [InlineData(2026, 11, 30, false)]
    [InlineData(2026, 12, 1, true)]
    [InlineData(2026, 12, 25, true)]
    [InlineData(2026, 12, 31, true)]
    [InlineData(2027, 1, 1, true)]
    [InlineData(2027, 1, 6, true)]
    [InlineData(2027, 1, 7, false)]
    [InlineData(2027, 7, 4, false)]
    public void TheSeasonIsDecemberFirstToJanuarySixthInclusive(int y, int m, int d, bool inSeason)
    {
        try
        {
            Clean();
            Assert.Equal(inSeason, ClusterFWinterDecor.Season.IsInSeason(At(y, m, d, 0)));
            Assert.Equal(inSeason, ClusterFWinterDecor.Season.IsInSeason(At(y, m, d, 23)));
        }
        finally
        {
            Clean();
        }
    }

    [Fact]
    public void ABadDateSettingFallsBackToTheDefaultAndSaysSo()
    {
        var s = ClusterFWinterDecor.Season;
        try
        {
            Clean();
            Assert.Empty(s.ReadSettings("12-15", "01-02"));
            Assert.Equal("12-15 to 01-02", s.Window.ToString());
            Assert.True(s.IsInSeason(At(2026, 12, 20)));
            Assert.False(s.IsInSeason(At(2026, 12, 10)));

            var lines = s.ReadSettings("Dec 1", "01-06");
            Assert.Single(lines);
            Assert.Contains(s.StartKey, lines[0]);
            Assert.Equal("12-01 to 01-06", s.Window.ToString());
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheAutomaticCheckPlacesInRangeRemovesAfterAndActsOnlyWhenTheSeasonChanges()
    {
        var s = ClusterFWinterDecor.Season;
        var rows = ClusterFWinterDecorLayout.Rows.Length;
        try
        {
            Clean();

            // A world first seen in November: nothing placed, the state remembered as down.
            Assert.Empty(s.Sync(At(2026, 11, 20)));
            Assert.Empty(Placed());
            Assert.False(s.LastAutomatic);

            // Staff put it up early for the test shard: November checks leave it up.
            ClusterFWinterDecor.Up(false, null);
            Assert.Empty(s.Sync(At(2026, 11, 25)));
            Assert.Equal(rows, Placed().Count);

            // December 1: up (already up: nothing doubled).
            Assert.NotEmpty(s.Sync(At(2026, 12, 1, 0)));
            Assert.Equal(rows, Placed().Count);
            Assert.True(s.LastAutomatic);

            // Staff clear it on December 10: the December checks leave it down.
            ClusterFWinterDecor.Down(false, null);
            Assert.Empty(s.Sync(At(2026, 12, 11)));
            Assert.Empty(Placed());

            // Up again by hand; January 6 is still in; January 7 takes it down.
            ClusterFWinterDecor.Up(false, null);
            Assert.Empty(s.Sync(At(2027, 1, 6, 23)));
            Assert.Equal(rows, Placed().Count);
            Assert.NotEmpty(s.Sync(At(2027, 1, 7, 0)));
            Assert.Empty(Placed());
            Assert.Equal(0, World.Items.Values.Count(i => i is IWinterDecorItem && !i.Deleted));
            Assert.False(s.LastAutomatic);

            // A world first seen in season (a fresh test world on December 3): brought up.
            Clean();
            Assert.NotEmpty(s.Sync(At(2026, 12, 3)));
            Assert.Equal(rows, Placed().Count);

            // The dates come from the settings: moved to 12-20 to 12-26, December 3 is out and December 24 is in.
            Clean();
            s.ReadSettings("12-20", "12-26");
            Assert.Empty(s.Sync(At(2026, 12, 3)));
            Assert.Empty(Placed());
            Assert.NotEmpty(s.Sync(At(2026, 12, 24)));
            Assert.Equal(rows, Placed().Count);
            Assert.NotEmpty(s.Sync(At(2026, 12, 27)));
            Assert.Empty(Placed());
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void PlaceTwicePlacesOnceAndClearRemovesExactlyTheDecorBySerialAndNothingElse()
    {
        var rows = ClusterFWinterDecorLayout.Rows;
        var first = rows[0];
        var stranger = new Item(0x0EED) { Movable = false };   // a gold coin pile, nothing to do with the decor
        var lookalike = new Item(first.ItemId) { Movable = false, Name = first.Name };   // same art, not ours
        try
        {
            Clean();
            stranger.MoveToWorld(new Point3D(first.X, first.Y, first.Z), Map.Trammel);
            lookalike.MoveToWorld(new Point3D(first.X, first.Y, first.Z), Map.Trammel);

            var before = World.Items.Count;
            ClusterFWinterDecor.Up(true, null);
            Assert.Equal(before, World.Items.Count);
            Assert.Empty(ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName));

            ClusterFWinterDecor.Up(false, null);
            ClusterFWinterDecor.Up(false, null);
            var placed = Placed();
            Assert.Equal(rows.Length, placed.Count);
            Assert.Equal(rows.Length, ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName).Count);
            Assert.Equal(rows.Select(r => r.Key).OrderBy(k => k), Keys(placed));
            Assert.Empty(ClusterFWinterDecor.Strays());
            foreach (var p in placed)
            {
                var r = rows.Single(x => x.Key == ((IWinterDecorItem)p).Key);
                Assert.Equal(new Point3D(r.X, r.Y, r.Z), p.Location);
                Assert.Equal(Map.Trammel, p.Map);
                Assert.Equal(r.ItemId, p.ItemID);
                Assert.Equal(r.Hue, p.Hue);
                Assert.False(p.Movable);
                Assert.False(p.Decays);
                Assert.Contains(p.Serial, ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName));
                Assert.Equal(r.Layer == ClusterFWinterDecor.CalendarLayer, p is WinterAdventCalendar);
            }

            before = World.Items.Count;
            ClusterFWinterDecor.Down(true, null);
            Assert.Equal(before, World.Items.Count);

            ClusterFWinterDecor.Down(false, null);
            ClusterFWinterDecor.Down(false, null);
            Assert.Empty(Placed());
            Assert.Empty(ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName));
            Assert.Equal(0, World.Items.Values.Count(i => i is IWinterDecorItem && !i.Deleted));
            Assert.False(stranger.Deleted);
            Assert.False(lookalike.Deleted);
            Assert.Equal(new Point3D(first.X, first.Y, first.Z), stranger.Location);
            Assert.Equal(before - rows.Length, World.Items.Count);

            ClusterFWinterDecor.Up(false, null);
            Assert.Equal(rows.Select(r => r.Key).OrderBy(k => k), Keys(Placed()));
        }
        finally
        {
            Clean();
            stranger.Delete();
            lookalike.Delete();
        }
    }

    [Fact]
    public void AnAreaFilterPlacesAndClearsOnlyThatArea()
    {
        var rows = ClusterFWinterDecorLayout.Rows;
        try
        {
            Clean();
            Assert.True(ClusterFWinterDecor.KnownFilter("bank"));
            Assert.True(ClusterFWinterDecor.KnownFilter("main road"));
            Assert.False(ClusterFWinterDecor.KnownFilter("atlantis"));
            Assert.Contains("No area called 'atlantis'", ClusterFWinterDecor.Run(new[] { "place", "atlantis" })[0]);

            ClusterFWinterDecor.Up(false, "bank");
            var placed = Placed();
            Assert.Equal(rows.Count(r => r.Area == "bank"), placed.Count);

            ClusterFWinterDecor.Run(new[] { "place", "main", "road" });
            placed = Placed();
            Assert.Equal(rows.Count(r => r.Area is "bank" or "main road"), placed.Count);

            ClusterFWinterDecor.Down(false, "main road");
            placed = Placed();
            Assert.Equal(rows.Count(r => r.Area == "bank"), placed.Count);

            ClusterFWinterDecor.Down(false, "bank");
            Assert.Empty(Placed());
        }
        finally
        {
            Clean();
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheDecorAndTheStateSurviveARestart()
    {
        var rows = ClusterFWinterDecorLayout.Rows;
        var s = ClusterFWinterDecor.Season;
        var stray = (WinterDecor)null;
        try
        {
            Clean();

            // The items round-trip with their keys.
            var row = rows.First(r => r.Hue != 0);
            var item = new WinterDecor(row);
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            item.Serialize(writer);
            writer.Flush();
            var copy = (WinterDecor)Activator.CreateInstance(typeof(WinterDecor), item.Serial)!;
            copy.Deserialize(new BufferReader(buffer));
            Assert.Equal(row.Key, copy.Key);
            Assert.Equal(row.ItemId, copy.ItemID);
            Assert.Equal(row.Hue, copy.Hue);
            Assert.Equal(row.Name, copy.Name);
            Assert.False(copy.Movable);
            item.Delete();

            // The season comes in, the decor goes up, the world "saves" (the state is written) ...
            Assert.NotEmpty(s.Sync(At(2026, 12, 2)));
            var serials = ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName).ToList();
            Assert.Equal(rows.Length, serials.Count);
            var saved = new byte[65536];
            var w = new BufferWriter(saved, true);
            ClusterFSeasonalState.WriteTo(w);
            w.Flush();

            // ... and the server restarts: the state is wiped and read back, the items are still in the world.
            ClusterFSeasonalState.Clear();
            Assert.Null(s.LastAutomatic);
            Assert.Empty(ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName));
            ClusterFSeasonalState.ReadFrom(new BufferReader(saved));
            Assert.True(s.LastAutomatic);
            Assert.Equal(serials.OrderBy(x => x.Value), ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName).OrderBy(x => x.Value));
            Assert.Equal(rows.Length, Placed().Count);

            // The in-season check after the restart adds nothing; the next place adds nothing either.
            Assert.Empty(s.Sync(At(2026, 12, 3)));
            Assert.Equal(rows.Length, World.Items.Values.Count(i => i is IWinterDecorItem && !i.Deleted));
            ClusterFWinterDecor.Up(false, null);
            Assert.Equal(rows.Length, World.Items.Values.Count(i => i is IWinterDecorItem && !i.Deleted));

            // A tracked item that vanished (deleted by hand) is dropped from the list without fuss.
            var gone = Placed()[0];
            var goneSerial = gone.Serial;
            gone.Delete();
            Assert.Equal(rows.Length - 1, Placed().Count);
            Assert.DoesNotContain(goneSerial, ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName));

            // A stray WinterDecor the list does not know (a serial the save lost) is swept by the take-down, and reported.
            stray = new WinterDecor(rows[1]);
            stray.MoveToWorld(new Point3D(rows[1].X, rows[1].Y, rows[1].Z), Map.Trammel);
            ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName).Remove(stray.Serial);
            Assert.Single(ClusterFWinterDecor.Strays());

            // The season ends after the restart: everything placed before it comes down, by the reloaded serials.
            var lines = s.Sync(At(2027, 1, 7));
            Assert.NotEmpty(lines);
            Assert.Contains(lines, l => l.Contains("stray"));
            Assert.Equal(0, World.Items.Values.Count(i => i is IWinterDecorItem && !i.Deleted));
            Assert.Empty(ClusterFSeasonalState.Placed(ClusterFWinterDecor.SeasonName));
            Assert.False(s.LastAutomatic);
        }
        finally
        {
            Clean();
            stray?.Delete();
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheServerHasTheWinterArtRecords()
    {
        ShatteredLegacyArt.Apply();
        Assert.True(ClusterFWinterDecorArt.Records.Length >= 14, "P34's seven and P266's seven");
        foreach (var r in ClusterFWinterDecorArt.Records)
        {
            var data = TileData.ItemTable[r.Id];
            Assert.Equal(r.Name, data.Name);
            Assert.Equal(r.Flags, data.Flags);
            Assert.Equal(r.Height, data.Height);
            Assert.Equal(r.Light, data.Quality);
            Assert.True(ClusterFWinterDecorArt.IsOurs(r.Id), $"0x{r.Id:X4}");
            Assert.True(r.Name.Length <= 19, r.Name);
        }

        Assert.True(TileData.ItemTable[0x3C52].Impassable, "the static tree blocks");
        Assert.True(TileData.ItemTable[0x3388].Impassable, "the large lit tree blocks");
        Assert.True((TileData.ItemTable[0x3388].Flags & TileFlag.Animation) != 0, "the large lit tree animates");
        Assert.True(TileData.ItemTable[0x33A0].LightSource, "the candle stand gives light");
        Assert.Equal(29, TileData.ItemTable[0x33A0].Quality);
        Assert.False(TileData.ItemTable[0x33A4].Impassable, "the floor candle is walk-over");
        Assert.True((TileData.ItemTable[0x3C40].Flags & TileFlag.Animation) != 0, "the statue animates");
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void TheAdventCalendarOpensOneDoorADayOncePerCharacterAndStartsFreshEachYear()
    {
        var row = ClusterFWinterDecorLayout.Rows.Single(r => r.Layer == ClusterFWinterDecor.CalendarLayer);
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        var other = new PlayerMobile { Player = true };
        other.AddItem(new Backpack());
        var calendar = (WinterAdventCalendar)null;
        try
        {
            Clean();
            pm.MoveToWorld(new Point3D(row.X, row.Y, row.Z), Map.Trammel);
            other.MoveToWorld(new Point3D(row.X, row.Y, row.Z), Map.Trammel);
            calendar = (WinterAdventCalendar)ClusterFWinterDecor.Make(row);
            calendar.MoveToWorld(new Point3D(row.X, row.Y, row.Z), Map.Trammel);
            Assert.Equal(row.Key, calendar.Key);

            // Not before December; not a door whose day has not come; not a number that is not a door.
            Assert.Equal(AdventDoorResult.NotDecember, calendar.TryOpen(pm, 1, At(2026, 11, 30)));
            Assert.Equal(AdventDoorResult.NotYet, calendar.TryOpen(pm, 4, At(2026, 12, 3)));
            Assert.Equal(AdventDoorResult.BadDay, calendar.TryOpen(pm, 0, At(2026, 12, 3)));
            Assert.Equal(AdventDoorResult.BadDay, calendar.TryOpen(pm, 26, At(2026, 12, 3)));
            Assert.Empty(pm.Backpack.Items);

            // December 3: doors 1 to 3 open, each once; the gift is a placeholder box in the backpack.
            Assert.Equal(AdventDoorResult.Opened, calendar.TryOpen(pm, 3, At(2026, 12, 3)));
            Assert.Equal(AdventDoorResult.AlreadyOpened, calendar.TryOpen(pm, 3, At(2026, 12, 3)));
            Assert.Equal(AdventDoorResult.Opened, calendar.TryOpen(pm, 1, At(2026, 12, 3)));
            Assert.Equal(2, pm.Backpack.Items.Count);
            Assert.All(pm.Backpack.Items, i => Assert.IsType<GiftBox>(i));
            Assert.Contains(pm.Backpack.Items, i => i.Name == "an advent calendar gift (December 3)");
            Assert.True(calendar.IsOpened(pm, 3, At(2026, 12, 3)));
            Assert.False(calendar.IsOpened(pm, 2, At(2026, 12, 3)));

            // Another character has its own doors.
            Assert.False(calendar.IsOpened(other, 3, At(2026, 12, 3)));
            Assert.Equal(AdventDoorResult.Opened, calendar.TryOpen(other, 3, At(2026, 12, 3)));
            Assert.Single(other.Backpack.Items);

            // Door 25 on Christmas day; January is over.
            Assert.Equal(AdventDoorResult.Opened, calendar.TryOpen(pm, 25, At(2026, 12, 25)));
            Assert.Equal(AdventDoorResult.NotDecember, calendar.TryOpen(pm, 2, At(2027, 1, 2)));

            // Next December starts fresh: door 3 opens again for the same character.
            Assert.Equal(AdventDoorResult.Opened, calendar.TryOpen(pm, 3, At(2027, 12, 3)));
            Assert.False(calendar.IsOpened(pm, 25, At(2027, 12, 3)));

            // The openings round-trip with the item.
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            calendar.Serialize(writer);
            writer.Flush();
            var copy = (WinterAdventCalendar)Activator.CreateInstance(typeof(WinterAdventCalendar), calendar.Serial)!;
            copy.Deserialize(new BufferReader(buffer));
            Assert.Equal(row.Key, copy.Key);
            Assert.Equal(2027, copy.Year);
            Assert.True(copy.IsOpened(pm, 3, At(2027, 12, 3)));
            Assert.Equal(AdventDoorResult.AlreadyOpened, copy.TryOpen(pm, 3, At(2027, 12, 3)));
            copy.Delete();

            // The seeder places it as its own type and takes it down with the rest.
            calendar.Delete();
            ClusterFWinterDecor.Up(false, "bank");
            Assert.Single(Placed().OfType<WinterAdventCalendar>());
            ClusterFWinterDecor.Down(false, null);
            Assert.Equal(0, World.Items.Values.Count(i => i is WinterAdventCalendar && !i.Deleted));
        }
        finally
        {
            Clean();
            calendar?.Delete();
            pm.Delete();
            other.Delete();
        }
    }
}
