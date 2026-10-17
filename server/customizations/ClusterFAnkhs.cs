// cc-P137: every ankh a player sees either works like OSI's or is known to be decoration.
//
// Chase, 2026-10-16: the ankh by the League Registrar's office in New Haven "needs to be usable". It is a map
// static (0x3/0x2, facing west, Trammel 3464,2600,27), not pinned's working ankh (Items/Construction/Ankhs.cs:
// AnkhWest/AnkhNorth, which offer resurrection to a ghost that comes within a tile and give a living player
// Lock/Unlock Karma on the context menu). A map static has no behavior at all.
//
// OSI's working ankhs are items placed over the art, which is how pinned and ServUO reproduce them: their
// Data/Decoration sets name each one (AnkhWest/AnkhNorth, shrines, Yew, Magincia, Destard, Haven, Ilshenar, Malas).
// A survey of the test world (2026-10-17, notes/cc-P137-usable-ankhs-and-playtest-fixes.md) found every entry of
// pinned's set in place ([Decorate), New Haven's from ClusterFNewHavenServicesSeeder, and Trammel Despise's as the
// revamp's DespiseAnkh. Missing were the entries only ServUO's data has: two by the Felucca champion teleporters and
// six in the Stygian Abyss (Ter Mur). Those are below, verbatim, built by pinned's own DecorationList.Read and
// Generate like the New Haven seeder's. The one entry that is ours is Chase's New Haven ankh (fidelity D-123): OSI's
// data has no working ankh on that static, and he asked for one. Every other ankh static (86 on the test world) has
// no working ankh in OSI's data either and stays decoration; the survey lists them.
//
// [ClusterFAnkhSurvey lists every ankh graphic on every facet, map static or item, and says which work.
// [ClusterFSeedAnkhs [dryrun] places the entries below where no working ankh stands within 2 tiles; a re-run places
// nothing twice. It runs at world load only if clusterf.ankhs.seedOnWorldLoad is true (default false), so a fresh
// world gets them from the world-setup commands like the other seeders. It moves and deletes nothing.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server.Commands;
using Server.Engines.Despise;
using Server.Items;

namespace Server;

public static class ClusterFAnkhs
{
    // The tile each working ankh's main item stands on (pinned Ankhs.cs: AnkhWest 0x3, AnkhNorth 0x4, bloodied
    // 0x1D98 and 0x1E5D); its second half is the tile beside it.
    public static bool IsAnkhHead(int itemId) => itemId is 0x3 or 0x4 or 0x1D98 or 0x1E5D;

    public static bool IsAnkhGraphic(int itemId) => itemId is 0x2 or 0x3 or 0x4 or 0x5 or 0x1D97 or 0x1D98 or 0x1E5C or 0x1E5D;

    /// <summary>Resurrects a ghost: pinned's ankhs (either half), the rejuvenation ankhs and revamped Despise's.</summary>
    public static bool IsWorkingAnkh(Item item) =>
        item is AnkhWest or AnkhNorth or BaseRejuvinationAnkh or RejuvinationAddonComponent or DespiseAnkh ||
        item.GetType().DeclaringType is { } t && (t == typeof(AnkhWest) || t == typeof(AnkhNorth));

    public readonly record struct Placement(string Header, int MapIndex, int X, int Y, int Z, string Source)
    {
        public Map Map => Map.Maps[MapIndex];
        public Point3D Location => new(X, Y, Z);
    }

    public static readonly Placement[] Placements =
    {
        // Ours (fidelity D-123): Chase's New Haven ankh, over the static 0x3/0x2 at 3464,2600-2601,27, same art and facing.
        new("AnkhWest 0x0003", 1, 3464, 2600, 27, "ours, Chase 2026-10-16"),
        new("AnkhWest 0x0003 (Hue=0x47E)", 0, 6937, 719, 27, "ServUO Felucca/_champion teleporters.cfg"),
        new("AnkhWest 0x0003 (Hue=0x47E)", 0, 6947, 987, -5, "ServUO Felucca/_champion teleporters.cfg"),
        new("AnkhNorth 0x0004", 5, 79, 345, 1, "ServUO Stygian Abyss/Ter Mur/Stygian Abyss.cfg"),
        new("AnkhNorth 0x0004", 5, 79, 543, 1, "ServUO Stygian Abyss/Ter Mur/Stygian Abyss.cfg"),
        new("AnkhNorth 0x0004", 5, 79, 751, 1, "ServUO Stygian Abyss/Ter Mur/Stygian Abyss.cfg"),
        new("AnkhWest 0x0000", 5, 841, 1264, -5, "ServUO Stygian Abyss/Ter Mur/Stygian Abyss.cfg"),
        new("AnkhWest 0x0000", 5, 841, 1094, -5, "ServUO Stygian Abyss/Ter Mur/Stygian Abyss.cfg"),
        new("AnkhWest 0x0000", 5, 841, 927, -5, "ServUO Stygian Abyss/Ter Mur/Stygian Abyss.cfg"),
    };

    public const int PresentRange = 2;

    private static bool _seedOnWorldLoad;

    public static void Configure()
    {
        _seedOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("clusterf.ankhs.seedOnWorldLoad", false);
        EventSink.WorldLoad += OnWorldLoad;
        CommandSystem.Register("ClusterFSeedAnkhs", AccessLevel.Administrator, OnSeedCommand);
        CommandSystem.Register("ClusterFAnkhSurvey", AccessLevel.GameMaster, OnSurveyCommand);
    }

    private static void OnWorldLoad()
    {
        if (_seedOnWorldLoad)
        {
            Console.WriteLine(Seed(false).Message);
        }
    }

    [Usage("ClusterFSeedAnkhs [dryrun]")]
    [Description("Places OSI's working ankhs that pinned's [Decorate lacks (ServUO's data) and Chase's New Haven ankh, where no working ankh stands within 2 tiles. Moves and deletes nothing; a re-run places nothing twice.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places the working ankhs pinned's [Decorate lacks and Chase's New Haven ankh where missing.")]
    private static void OnSeedCommand(CommandEventArgs e)
    {
        var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";
        if (mode != "" && mode != "dryrun")
        {
            e.Mobile.SendMessage("Usage: [ClusterFSeedAnkhs [dryrun]");
            return;
        }

        e.Mobile.SendMessage(Seed(mode == "dryrun").Message);
    }

    public readonly record struct SeedResult(int Placed, int Present, string Message);

    public static SeedResult Seed(bool dryRun)
    {
        int placed = 0, present = 0;

        foreach (var p in Placements)
        {
            if (WorkingAnkhNear(p.Map, p.Location))
            {
                present++;
                continue;
            }

            if (dryRun)
            {
                placed++;
                continue;
            }

            var generated = new ClusterFNewHavenServicesSeeder.DecorEntry(p.Header, p.X, p.Y, p.Z).Read().Generate([p.Map]);
            placed += generated > 0 ? 1 : 0;
            present += generated > 0 ? 0 : 1;
        }

        var verb = dryRun ? "would place" : "placed";
        return new SeedResult(placed, present, $"Ankhs{(dryRun ? " (dry run)" : "")}: {verb} {placed}, already working {present}.");
    }

    public static bool WorkingAnkhNear(Map map, Point3D p)
    {
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        foreach (var item in map.GetItemsInRange(p, PresentRange))
        {
            if (!item.Deleted && IsWorkingAnkh(item))
            {
                return true;
            }
        }

        return false;
    }

    // -- The survey -------------------------------------------------------------------------------------------------

    public enum AnkhState { Working, Placed, Decoration }

    public readonly record struct Sighting(Map Map, Point3D Location, int ItemId, string Source);

    public readonly record struct SurveyRow(Sighting Sighting, AnkhState State, string Why);

    /// <summary>
    /// Every ankh head graphic (one row per ankh, not per half): working when a working ankh item stands on it or
    /// beside it, "Placed" when [ClusterFSeedAnkhs has an entry for it, otherwise decoration (OSI's data has no
    /// working ankh there).
    /// </summary>
    public static List<SurveyRow> Classify(IEnumerable<Sighting> sightings)
    {
        var rows = new List<SurveyRow>();
        foreach (var s in sightings)
        {
            if (!IsAnkhHead(s.ItemId))
            {
                continue;
            }

            if (WorkingAnkhNear1(s.Map, s.Location))
            {
                rows.Add(new SurveyRow(s, AnkhState.Working, "a working ankh stands on it"));
            }
            else if (Placements.Any(p => p.Map == s.Map && Math.Abs(p.X - s.Location.X) <= 1 && Math.Abs(p.Y - s.Location.Y) <= 1))
            {
                rows.Add(new SurveyRow(s, AnkhState.Placed, "[ClusterFSeedAnkhs places one here"));
            }
            else
            {
                rows.Add(new SurveyRow(s, AnkhState.Decoration, "decoration: OSI's data has no working ankh here"));
            }
        }

        return rows;
    }

    private static bool WorkingAnkhNear1(Map map, Point3D p)
    {
        if (map == null || map == Map.Internal)
        {
            return false;
        }

        foreach (var item in map.GetItemsInRange(p, 1))
        {
            if (!item.Deleted && IsWorkingAnkh(item) && Math.Abs(item.Z - p.Z) <= 5)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Every ankh graphic in the world: map statics on every facet, then items that are not working ankhs.</summary>
    public static IEnumerable<Sighting> Sightings()
    {
        foreach (var map in Map.AllMaps)
        {
            if (map == null || map == Map.Internal)
            {
                continue;
            }

            // The default facets' files are numbered by MapID (TileMatrix opens staidx{fileIndex}.mul).
            var idx = Core.FindDataFile($"staidx{map.MapID}.mul", false);
            var sta = Core.FindDataFile($"statics{map.MapID}.mul", false);
            if (idx == null || sta == null)
            {
                continue;
            }

            foreach (var s in StaticSightings(map, map.Height >> 3, idx, sta))
            {
                yield return s;
            }
        }

        foreach (var item in World.Items.Values)
        {
            if (!item.Deleted && item.Parent == null && item.Map != null && item.Map != Map.Internal &&
                IsAnkhGraphic(item.ItemID) && !IsWorkingAnkh(item))
            {
                yield return new Sighting(item.Map, item.Location, item.ItemID, item.GetType().Name);
            }
        }
    }

    /// <summary>
    /// Ankh graphics in one facet's staidx/statics .mul pair, read straight from the files (TileMatrix.GetStaticBlock
    /// would cache every block of every facet). staidx: 12 bytes per 8x8 block (offset, length, extra), block
    /// number bx * blockHeight + by; statics: 7 bytes per static (id, x, y, z, hue).
    /// </summary>
    public static List<Sighting> StaticSightings(Map map, int blockHeight, string idxPath, string staPath)
    {
        var found = new List<Sighting>();
        using var idx = new BinaryReader(File.OpenRead(idxPath));
        using var sta = new BinaryReader(File.OpenRead(staPath));
        var blocks = idx.BaseStream.Length / 12;

        for (var block = 0L; block < blocks; block++)
        {
            var offset = idx.ReadInt32();
            var length = idx.ReadInt32();
            idx.ReadInt32();
            if (offset < 0 || length <= 0)
            {
                continue;
            }

            var bx = (int)(block / blockHeight);
            var by = (int)(block % blockHeight);
            sta.BaseStream.Seek(offset, SeekOrigin.Begin);
            for (var i = 0; i < length / 7; i++)
            {
                int id = sta.ReadUInt16();
                int x = sta.ReadByte();
                int y = sta.ReadByte();
                int z = sta.ReadSByte();
                sta.ReadUInt16();
                if (IsAnkhGraphic(id))
                {
                    found.Add(new Sighting(map, new Point3D(bx * 8 + x, by * 8 + y, z), id, "map static"));
                }
            }
        }

        return found;
    }

    [Usage("ClusterFAnkhSurvey")]
    [Description("Lists every ankh graphic on every facet (map statics and items) and whether it works; writes the full list to Logs/ankh-survey.txt.")]
    [ShardCommand(CommandCategory.Diagnostic)]
    private static void OnSurveyCommand(CommandEventArgs e)
    {
        var rows = Classify(Sightings());
        var lines = rows.Select(r => $"{r.State,-10} {r.Sighting.Map.Name,-8} {r.Sighting.Location} 0x{r.Sighting.ItemId:X4} {r.Sighting.Source}: {r.Why}").ToList();
        var path = Path.Combine(Core.BaseDirectory, "Logs", "ankh-survey.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, lines);
        e.Mobile.SendMessage(
            $"Ankh survey: {rows.Count(r => r.State == AnkhState.Working)} working, " +
            $"{rows.Count(r => r.State == AnkhState.Placed)} waiting for [ClusterFSeedAnkhs, " +
            $"{rows.Count(r => r.State == AnkhState.Decoration)} decoration. Full list: {path}"
        );

        foreach (var r in rows.Where(r => r.State == AnkhState.Placed))
        {
            e.Mobile.SendMessage($"  not yet working: {r.Sighting.Map.Name} {r.Sighting.Location}");
        }
    }
}
