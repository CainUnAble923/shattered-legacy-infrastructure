// cc-P237 (F-21): New Haven's community art. Seven packs Chase approved for the town (farm carts by Moonrose, an oil
// lantern by Ems, marble railings by OUTLANDSOwyn, bridge support beams by gollin, an orc encampment by Vrark, paintings
// by Odin), shipped in the player package as our art IDs 0x3C88-0x3CA9. Notes in shard-migration
// notes/cc-P237-new-haven-community-art.md.
//
// What it places, and what it removes
//   One CommunityArt per row of ClusterFCommunityArtLayout (generated from the checked placement table). CommunityArt
//   is a type of its own, created only here, so clear and replace delete exactly those and nothing else: never a
//   player's item, never another seeder's, never a same-art lookalike on the same tile. Each knows its row's key, so
//   place skips a row already placed: place twice places once, clear twice removes once, place after clear restores
//   the same set. replace is clear then place, for a world that has an older layout.
//   Nothing runs on its own: no world load, no timer. The placement is a draft Chase walks on the test shard first.
//
// [ClusterFCommunityArt status | place [dryrun] | replace [dryrun] | clear [dryrun]]

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Commands;
using Server.Items;

namespace Server
{
    /// <summary>
    /// Our tiledata records for the community art that blocks, animates or gives light (cc-P237). The values are
    /// records.json's: change the two together (player-package\tests\Build-UoOverrides.Tests.ps1 compares them).
    /// ShatteredLegacyArt.Apply writes them into the server's in-memory TileData.ItemTable, as it does the runestone's.
    /// Quality is the tiledata's layer byte, which for a light source is its light shape.
    /// </summary>
    public static class ClusterFCommunityArtRecords
    {
        public sealed record Record(int Id, string Name, TileFlag Flags, int Weight, int Quality, int Height);

        // The 34 IDs registered for this set (player-package\vendor\shattered-legacy-art\registry.csv).
        public const int FirstId = 0x3C88;
        public const int LastId = 0x3CA9;

        public static readonly Record[] Records =
        {
            // OSI's ore cart 0x1A82 (Impassable, height 3), ArticleA.
            new(0x3C88, "fruit cart", TileFlag.Impassable | TileFlag.ArticleA, 255, 0, 3),
            new(0x3C89, "fruit cart", TileFlag.Impassable | TileFlag.ArticleA, 255, 0, 3),
            new(0x3C8A, "vegetable cart", TileFlag.Impassable | TileFlag.ArticleA, 255, 0, 3),
            new(0x3C8B, "vegetable cart", TileFlag.Impassable | TileFlag.ArticleA, 255, 0, 3),
            // OSI's lantern 0x0A15 (light 2, height 2) without Wearable.
            new(0x3C8C, "oil lantern", TileFlag.Animation | TileFlag.LightSource | TileFlag.ArticleAn, 255, 2, 2),
            // OSI's wooden banister 0x08AE.
            new(0x3C8F, "marble railing", TileFlag.Impassable | TileFlag.Transparent | TileFlag.ArticleA, 255, 0, 5),
            new(0x3C90, "marble railing", TileFlag.Impassable | TileFlag.Transparent | TileFlag.ArticleA, 255, 0, 5),
            // OSI's wood supports 0x019C. Unknown3 is OSI's bit 0x04000000, kept as OSI sets it.
            new(0x3C91, "support beam", TileFlag.Impassable | TileFlag.Wall | TileFlag.Unknown3, 255, 0, 16),
            new(0x3C92, "support beam", TileFlag.Impassable | TileFlag.Wall | TileFlag.Unknown3, 255, 0, 16),
            new(0x3C93, "support beam", TileFlag.Impassable | TileFlag.Wall | TileFlag.Unknown3, 255, 0, 16),
            new(0x3C94, "support beam", TileFlag.Impassable | TileFlag.Wall | TileFlag.Unknown3, 255, 0, 16),
            // Ours: Impassable, the height of a tent wall.
            new(0x3C95, "orcish cage", TileFlag.Impassable | TileFlag.ArticleAn, 255, 0, 20),
            // OSI's campfire 0x0DE3 (light 29, height 1) without Animation (one frame) and Damaging.
            new(0x3C96, "orcish campfire", TileFlag.LightSource | TileFlag.ArticleAn, 255, 29, 1),
            // Ours: Impassable, as OSI's chicken on a spit 0x1E94, taller.
            new(0x3C97, "orcish meat rack", TileFlag.Impassable | TileFlag.ArticleAn, 255, 0, 15),
            // OSI's palisade 0x0221 without Damaging, ArticleAn.
            new(0x3C98, "orcish palisade", TileFlag.Impassable | TileFlag.Wall | TileFlag.NoShoot | TileFlag.ArticleAn | TileFlag.Unknown3, 255, 0, 20),
            new(0x3C99, "orcish palisade", TileFlag.Impassable | TileFlag.Wall | TileFlag.NoShoot | TileFlag.ArticleAn | TileFlag.Unknown3, 255, 0, 20),
            // OSI's tent wall 0x01F0, ArticleAn.
            new(0x3C9B, "orcish tent", TileFlag.Impassable | TileFlag.Wall | TileFlag.NoShoot | TileFlag.ArticleAn, 255, 0, 20),
            new(0x3C9C, "orcish tent", TileFlag.Impassable | TileFlag.Wall | TileFlag.NoShoot | TileFlag.ArticleAn, 255, 0, 20),
            new(0x3C9D, "orcish hut", TileFlag.Impassable | TileFlag.Wall | TileFlag.NoShoot | TileFlag.ArticleAn, 255, 0, 20)
        };

        public static void Apply()
        {
            foreach (var r in Records)
            {
                TileData.ItemTable[r.Id] = new ItemData(r.Name, r.Flags, r.Weight, r.Quality, 0, 0, 0, r.Height);
            }
        }
    }

    public static class ClusterFCommunityArt
    {
        public static Map Facet => Map.Trammel;

        public static void Configure()
        {
            CommandSystem.Register("ClusterFCommunityArt", AccessLevel.Administrator, OnCommand);
        }

        /// <summary>Every item this seeder placed, on any map.</summary>
        public static List<CommunityArt> Placed() =>
            World.Items.Values.OfType<CommunityArt>().Where(i => !i.Deleted).ToList();

        public static List<string> Place(bool dryRun)
        {
            var lines = new List<string>();
            var have = new HashSet<string>(Placed().Select(i => i.Key), StringComparer.Ordinal);
            var placed = 0;
            foreach (var row in ClusterFCommunityArtLayout.Rows)
            {
                if (have.Contains(row.Key))
                {
                    continue;
                }

                placed++;
                if (dryRun)
                {
                    lines.Add($"  would place {row.Key}: {row.Name} (0x{row.ItemId:X4}) at {row.X},{row.Y},{row.Z} ({row.Area})");
                    continue;
                }

                new CommunityArt(row).MoveToWorld(new Point3D(row.X, row.Y, row.Z), Facet);
            }

            lines.Add($"[CommunityArt] {(dryRun ? "dry run: would place" : "placed")} {placed} of {ClusterFCommunityArtLayout.Rows.Length}; {have.Count} already placed.");
            return lines;
        }

        public static List<string> Clear(bool dryRun)
        {
            var lines = new List<string>();
            var items = Placed();
            foreach (var item in items)
            {
                if (dryRun)
                {
                    lines.Add($"  would remove {item.Key}: {item.Name} at {item.Location} ({item.Map})");
                }
                else
                {
                    item.Delete();
                }
            }

            lines.Add($"[CommunityArt] {(dryRun ? "dry run: would remove" : "removed")} {items.Count} community art item(s); nothing else is touched.");
            return lines;
        }

        /// <summary>Clear, then place: swaps an older layout for the current one. A dry run lists both and changes nothing.</summary>
        public static List<string> Replace(bool dryRun)
        {
            var lines = Clear(dryRun);
            if (dryRun)
            {
                lines.Add($"[CommunityArt] dry run: would then place all {ClusterFCommunityArtLayout.Rows.Length} rows.");
                return lines;
            }

            lines.AddRange(Place(false));
            return lines;
        }

        public static List<string> Status()
        {
            var placed = Placed();
            var keys = new HashSet<string>(placed.Select(i => i.Key), StringComparer.Ordinal);
            var stale = placed.Count(i => ClusterFCommunityArtLayout.Rows.All(r => r.Key != i.Key));
            return new List<string>
            {
                $"[CommunityArt] {ClusterFCommunityArtLayout.Rows.Count(r => keys.Contains(r.Key))} of {ClusterFCommunityArtLayout.Rows.Length} rows placed{(stale > 0 ? $"; {stale} placed item(s) belong to no current row ('replace' swaps them)" : "")}."
            };
        }

        [Usage("ClusterFCommunityArt [status|place|replace|clear] [dryrun]")]
        [Description("New Haven's community art (cc-P237). status says how many rows are placed. place places the rows not already placed. replace removes every community art item and places the current layout. clear removes every community art item and nothing else. dryrun lists and changes nothing.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places New Haven's community art (carts, lanterns, railings, beams, the orc camp, paintings). Places nothing twice; 'clear' and 'replace' remove only this art. Dry run: 'place dryrun', 'replace dryrun' or 'clear dryrun'.")]
        private static void OnCommand(CommandEventArgs e)
        {
            var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
            var dryRun = e.Arguments.Any(a => a.Equals("dryrun", StringComparison.OrdinalIgnoreCase));
            var lines = verb switch
            {
                "place" => Place(dryRun),
                "replace" => Replace(dryRun),
                "clear" => Clear(dryRun),
                "status" or "dryrun" => Status(),
                _ => null
            };

            if (lines == null)
            {
                e.Mobile.SendMessage("Usage: [ClusterFCommunityArt [status|place|replace|clear] [dryrun]");
                return;
            }

            foreach (var line in lines)
            {
                e.Mobile.SendMessage(line);
            }
        }
    }
}

namespace Server.Items
{
    /// <summary>
    /// One piece of New Haven's community art (cc-P237): a row of ClusterFCommunityArtLayout, by its key. Placed and
    /// removed only by ClusterFCommunityArt; not constructible, so no [add makes one. Not movable, does not decay.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class CommunityArt : Item
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private string _key;

        public CommunityArt(ClusterFCommunityArtLayout.Row row) : base(row.ItemId)
        {
            _key = row.Key;
            Name = row.Name;
            Movable = false;
        }

        public override bool Decays => false;
    }
}
