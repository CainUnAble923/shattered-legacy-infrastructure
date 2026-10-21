// cc-P265: the guild hall decor seeders. The art library's per-guild packs (art IDs 0x6D70-0x6E86, GuildHallDecorArt)
// are registered in the player package; nothing places them in the world on its own. A staff member dresses a hall
// from a template (GuildHallDecorTemplates, generated from design/guild-hall-decor/templates.json): one command per
// guild, plus the shared hall structure (towers, marble balustrade).
//
//   [DecorAlembicHall | [DecorSmithsHall | [DecorArtificersHall | [DecorHallStructure
//       status                       the set's groups, and how many of its placements stand in the world
//       place [group] [here] [dryrun]   target the point (or "here": where you stand); the template's offsets are
//                                    tiles from it. A group key places that group alone; none places every group.
//       clear [all] [dryrun]         removes this set's placements anchored within 24 tiles of you (all: everywhere)
//
// TEST SHARD ONLY: place and clear refuse unless testCenter.enable is true (TestCenter.Enabled, as the Staff Hub reads
// test or live). F-49's hall sites still wait on Chase; a later prompt tunes the template per hall and decides live.
//
// What it places and removes: GuildHallDecor items only, a type created only here, each knowing its set, group, row
// and the point it was placed from. So place skips rows already standing at that point (twice places once), and
// clear deletes exactly those items: never a player's item, another seeder's, or a lookalike on the same tile.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Commands;
using Server.Items;
using Server.Misc;
using Server.Targeting;

namespace Server
{
    public static class GuildHallDecorSeeder
    {
        public const int ClearRange = 24;

        public static void Configure()
        {
            CommandSystem.Register("DecorAlembicHall", AccessLevel.Administrator, OnAlembic);
            CommandSystem.Register("DecorSmithsHall", AccessLevel.Administrator, OnSmiths);
            CommandSystem.Register("DecorArtificersHall", AccessLevel.Administrator, OnArtificers);
            CommandSystem.Register("DecorHallStructure", AccessLevel.Administrator, OnStructure);
        }

        /// <summary>Why place or clear may not run here, or null on the test shard.</summary>
        public static string Refusal(bool isTestShard) =>
            isTestShard
                ? null
                : "Refused: the guild hall decor seeders run on the test shard only (testCenter.enable is false here). " +
                  "Nothing was placed or removed.";

        /// <summary>Every item the seeders placed for this set, on any map.</summary>
        public static List<GuildHallDecor> Placed(string set) =>
            World.Items.Values.OfType<GuildHallDecor>().Where(i => !i.Deleted && i.SetKey == set).ToList();

        public static List<string> Place(
            GuildHallDecorTemplates.Set set, string group, Map map, Point3D anchor, bool dryRun, bool isTestShard
        )
        {
            var refusal = Refusal(isTestShard);
            if (refusal != null)
            {
                return [refusal];
            }

            var groups = group == null ? set.Groups : set.Groups.Where(g => g.Key == group).ToArray();
            if (groups.Length == 0)
            {
                return [$"No group '{group}' in {set.Key}. Groups: {string.Join(", ", set.Groups.Select(g => g.Key))}."];
            }

            if (map == null || map == Map.Internal)
            {
                return ["Refused: no map to place on."];
            }

            var lines = new List<string>();
            var standing = new HashSet<(string, int)>(
                Placed(set.Key).Where(i => i.Map == map && i.Anchor == anchor).Select(i => (i.GroupKey, i.Row))
            );
            int placed = 0, skipped = 0;
            foreach (var g in groups)
            {
                for (var r = 0; r < g.Rows.Length; r++)
                {
                    if (standing.Contains((g.Key, r)))
                    {
                        skipped++;
                        continue;
                    }

                    var row = g.Rows[r];
                    var at = new Point3D(anchor.X + row.Dx, anchor.Y + row.Dy, anchor.Z + row.Dz);
                    placed++;
                    if (dryRun)
                    {
                        lines.Add($"  would place {g.Key} {row.Piece}: {row.Name} (0x{row.ItemId:X4}) at {at.X},{at.Y},{at.Z}");
                        continue;
                    }

                    new GuildHallDecor(set.Key, g.Key, r, anchor, row).MoveToWorld(at, map);
                }
            }

            lines.Add(
                $"[{set.Command}] {(dryRun ? "dry run: would place" : "placed")} {placed} item(s) of " +
                $"{string.Join(", ", groups.Select(g => g.Key))} from {anchor.X},{anchor.Y},{anchor.Z} ({map}); " +
                $"{skipped} already there."
            );
            return lines;
        }

        public static List<string> Clear(
            GuildHallDecorTemplates.Set set, Map map, Point3D near, bool all, bool dryRun, bool isTestShard
        )
        {
            var refusal = Refusal(isTestShard);
            if (refusal != null)
            {
                return [refusal];
            }

            var items = Placed(set.Key)
                .Where(i => all || i.Map == map && Math.Max(Math.Abs(i.Anchor.X - near.X), Math.Abs(i.Anchor.Y - near.Y)) <= ClearRange)
                .ToList();
            var anchors = items.Select(i => (i.Map, i.Anchor)).Distinct().Count();
            if (!dryRun)
            {
                foreach (var i in items)
                {
                    i.Delete();
                }
            }

            return
            [
                $"[{set.Command}] {(dryRun ? "dry run: would remove" : "removed")} {items.Count} item(s) from {anchors} " +
                $"placement(s){(all ? " (all)" : $" anchored within {ClearRange} tiles")}; nothing else is touched."
            ];
        }

        public static List<string> Status(GuildHallDecorTemplates.Set set)
        {
            var placed = Placed(set.Key);
            var lines = new List<string>
            {
                $"[{set.Command}] {set.Guild}: {set.Groups.Sum(g => g.Rows.Length)} items in {set.Groups.Length} group(s). " +
                $"In the world: {placed.Count} item(s) from {placed.Select(i => (i.Map, i.Anchor)).Distinct().Count()} placement(s)."
            };
            lines.AddRange(set.Groups.Select(g => $"  {g.Key} ({g.Rows.Length}): {g.About}"));
            return lines;
        }

        private static void Run(CommandEventArgs e, string setKey)
        {
            var set = GuildHallDecorTemplates.Find(setKey);
            var args = e.Arguments.Select(a => a.ToLowerInvariant()).ToList();
            var verb = args.Count > 0 ? args[0] : "status";
            var dryRun = args.Remove("dryrun");
            var here = args.Remove("here");
            var all = args.Remove("all");
            var group = args.Count > 1 ? args[1] : null;
            var from = e.Mobile;

            switch (verb)
            {
                case "status":
                    Say(from, Status(set));
                    return;
                case "clear":
                    Say(from, Clear(set, from.Map, from.Location, all, dryRun, TestCenter.Enabled));
                    return;
                case "place" when here:
                    Say(from, Place(set, group, from.Map, from.Location, dryRun, TestCenter.Enabled));
                    return;
                case "place":
                    {
                        var refusal = Refusal(TestCenter.Enabled);
                        if (refusal != null)
                        {
                            from.SendMessage(refusal);
                            return;
                        }

                        from.SendMessage($"Target the point to dress from ({set.Guild}{(group != null ? $", {group}" : "")}).");
                        from.BeginTarget(
                            -1, true, TargetFlags.None,
                            (m, targeted, _) =>
                            {
                                if (targeted is IPoint3D p)
                                {
                                    var at = p is Item item ? item.GetWorldLocation() : new Point3D(p);
                                    Say(m, Place(set, group, m.Map, at, dryRun, TestCenter.Enabled));
                                }
                            },
                            0
                        );
                        return;
                    }
                default:
                    from.SendMessage($"Usage: [{set.Command} [status | place [group] [here] [dryrun] | clear [all] [dryrun]]");
                    return;
            }
        }

        private static void Say(Mobile m, IEnumerable<string> lines)
        {
            foreach (var l in lines)
            {
                m.SendMessage(l);
            }
        }

        [Usage("DecorAlembicHall [status|place|clear] [group] [here|all] [dryrun]")]
        [Description("Dresses an Order of the Alembic hall from its template (cc-P265): the bubbling vat, specimen tanks, the flask bench, the crystal ball. place targets the point (here: where you stand); a group key places that group alone; clear removes this set's placements near you (all: everywhere). Test shard only.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places the Order of the Alembic decor template at a targeted point. Test shard only; places nothing twice at one point. Dry run: 'place here dryrun'.")]
        private static void OnAlembic(CommandEventArgs e) => Run(e, "alembic");

        [Usage("DecorSmithsHall [status|place|clear] [group] [here|all] [dryrun]")]
        [Description("Dresses a Society of Smiths hall from its template (cc-P265): the fire pit, forge, anvil and a blacksmith at work. place targets the point (here: where you stand); clear removes this set's placements near you (all: everywhere). Test shard only.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places the Society of Smiths decor template at a targeted point. Test shard only; places nothing twice at one point. Dry run: 'place here dryrun'.")]
        private static void OnSmiths(CommandEventArgs e) => Run(e, "smiths");

        [Usage("DecorArtificersHall [status|place|clear] [group] [here|all] [dryrun]")]
        [Description("Dresses an Artificers' Order hall from its template (cc-P265): the arc orb gate, floating crystals and orb pillars. place targets the point (here: where you stand); clear removes this set's placements near you (all: everywhere). Test shard only.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places the Artificers' Order decor template at a targeted point. Test shard only; places nothing twice at one point. Dry run: 'place here dryrun'.")]
        private static void OnArtificers(CommandEventArgs e) => Run(e, "artificers");

        [Usage("DecorHallStructure [status|place|clear] [group] [here|all] [dryrun]")]
        [Description("Places the shared hall structure (cc-P265): Hestia's five towers and a marble balustrade with a ladder. place targets the point (here: where you stand); a group key (tower-large, tower-medium, tower-small, tower-white-brick, tower-white-column, balustrade) places that group alone; clear removes these placements near you (all: everywhere). Test shard only.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places the shared hall structure template (towers, balustrade) at a targeted point. Test shard only; places nothing twice at one point. Dry run: 'place here dryrun'.")]
        private static void OnStructure(CommandEventArgs e) => Run(e, "structure");
    }
}

namespace Server.Items
{
    /// <summary>
    /// One piece of guild hall decor (cc-P265): a row of a GuildHallDecorTemplates set, placed from Anchor. Placed and
    /// removed only by GuildHallDecorSeeder; not constructible, so no [add makes one. Not movable, does not decay.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class GuildHallDecor : Item
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private string _setKey;

        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private string _groupKey;

        [SerializableField(2)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _row;

        [SerializableField(3)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private Point3D _anchor;

        public GuildHallDecor(string setKey, string groupKey, int row, Point3D anchor, GuildHallDecorTemplates.Row data)
            : base(data.ItemId)
        {
            _setKey = setKey;
            _groupKey = groupKey;
            _row = row;
            _anchor = anchor;
            Name = data.Name;
            Hue = data.Hue;
            Movable = false;
        }

        public override bool Decays => false;
    }
}
