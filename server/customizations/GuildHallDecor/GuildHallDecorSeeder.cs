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
// What it places and removes: GuildHallDecor items only, a type created only here, each knowing its set, group, row,
// the point it was placed from and its group's template hash. So place skips rows already standing at that point
// (twice places once), and clear deletes exactly those items: never a player's item, another seeder's, or a lookalike
// on the same tile.
//
// cc-P459 (Codex's P419 #1, #2, #4): place refuses, and places nothing, when
//   - a group already standing at that point was placed from another version of its template (its rows moved, or one
//     was added): "clear first", since skipping by row number would leave the old pieces and add shifted copies;
//   - the placement's tiles meet the same set's placement from another point (one tile off doubles a set);
//   - a row would stand in an impassable static or item, or on a door's tile or swing (the preflight). A dry run lists
//     the conflicts. The rows of the placement itself are not conflicts (a tank on its stand).
// A template row marked door is a GuildHallDecorDoor (the towers' doors, Chase 10-23); one marked hidden is placed
// unseen (the towers' invisible side walls).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
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

        /// <summary>
        /// A group's template version (cc-P459): the first 16 hex digits of the SHA-256 of every row's art, offset, hue,
        /// piece and switches, in order. Any edit to the group's rows changes it.
        /// </summary>
        public static string TemplateHash(GuildHallDecorTemplates.Group group)
        {
            var sb = new StringBuilder();
            foreach (var r in group.Rows)
            {
                sb.Append($"{r.ItemId:X4},{r.Dx},{r.Dy},{r.Dz},{r.Hue},{r.Piece},{(r.Door ? 1 : 0)},{(r.Hidden ? 1 : 0)};");
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16].ToLowerInvariant();
        }

        public static string TemplateHash(string setKey, string groupKey) =>
            GuildHallDecorTemplates.Find(setKey)?.Groups.FirstOrDefault(g => g.Key == groupKey) is { } group
                ? TemplateHash(group)
                : null;

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
            var onMap = Placed(set.Key).Where(i => i.Map == map).ToList();
            var here = onMap.Where(i => i.Anchor == anchor).ToList();

            // An older template at this point: its row numbers no longer mean the same pieces.
            foreach (var g in groups)
            {
                var hash = TemplateHash(g);
                var old = here.Count(i => i.GroupKey == g.Key && i.TemplateHash != hash);
                if (old > 0)
                {
                    lines.Add($"  {g.Key}: {old} item(s) here were placed from another version of its template.");
                }
            }

            if (lines.Count > 0)
            {
                lines.Add(
                    $"[{set.Command}] Refused: the template changed since it was placed at {anchor.X},{anchor.Y},{anchor.Z}. " +
                    $"Clear first ([{set.Command} clear), then place again. Nothing was placed."
                );
                return lines;
            }

            // The same set from another point, on any tile this placement would use.
            var footprint = groups.SelectMany(g => g.Rows).Select(r => (anchor.X + r.Dx, anchor.Y + r.Dy)).ToHashSet();
            foreach (var meet in onMap.Where(i => i.Anchor != anchor && footprint.Contains((i.X, i.Y))).GroupBy(i => (i.Anchor, i.GroupKey)))
            {
                var a = meet.Key.Anchor;
                lines.Add($"  it meets {meet.Key.GroupKey} placed from {a.X},{a.Y},{a.Z} ({meet.Count()} of its item(s) on these tiles).");
            }

            if (lines.Count > 0)
            {
                lines.Add(
                    $"[{set.Command}] Refused: this would overlap the same set placed from another point. Clear that one " +
                    "first, or pick another point. Nothing was placed."
                );
                return lines;
            }

            var standing = new HashSet<(string, int)>(here.Select(i => (i.GroupKey, i.Row)));
            var own = new HashSet<Item>(here);
            var rows = new List<(GuildHallDecorTemplates.Group Group, int Index, Point3D At)>();
            var conflicts = new List<string>();
            int skipped = 0;
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
                    var why = Conflict(map, at, row, own);
                    if (why != null)
                    {
                        conflicts.Add($"  {g.Key} {row.Piece} at {at.X},{at.Y},{at.Z}: {why}.");
                    }

                    rows.Add((g, r, at));
                }
            }

            if (conflicts.Count > 0)
            {
                lines.AddRange(conflicts);
                lines.Add(
                    $"[{set.Command}] {(dryRun ? "dry run: would be refused" : "Refused")}: {conflicts.Count} row(s) of " +
                    $"{string.Join(", ", groups.Select(g => g.Key))} from {anchor.X},{anchor.Y},{anchor.Z} meet something " +
                    "already there. Nothing was placed."
                );
                return lines;
            }

            foreach (var (g, r, at) in rows)
            {
                var row = g.Rows[r];
                if (dryRun)
                {
                    lines.Add($"  would place {g.Key} {row.Piece}: {row.Name} (0x{row.ItemId:X4}) at {at.X},{at.Y},{at.Z}");
                    continue;
                }

                var item = row.Door
                    ? new GuildHallDecorDoor(set.Key, g.Key, r, anchor, row)
                    : new GuildHallDecor(set.Key, g.Key, r, anchor, row);
                if (row.Hidden)
                {
                    item.Visible = false;
                }

                item.MoveToWorld(at, map);
            }

            lines.Add(
                $"[{set.Command}] {(dryRun ? "dry run: would place" : "placed")} {rows.Count} item(s) of " +
                $"{string.Join(", ", groups.Select(g => g.Key))} from {anchor.X},{anchor.Y},{anchor.Z} ({map}); " +
                $"{skipped} already there."
            );
            return lines;
        }

        /// <summary>
        /// What a row at this point would stand in, or null (cc-P459, P419 #2): an impassable static or multi tile, an
        /// impassable item, or a door (on the tile, or swinging onto it), overlapping the row's own height. Items of
        /// this placement already standing (own) do not count.
        /// </summary>
        public static string Conflict(Map map, Point3D at, GuildHallDecorTemplates.Row row, ICollection<Item> own)
        {
            var low = at.Z;
            var high = at.Z + Math.Max(1, TileData.ItemTable[row.ItemId & TileData.MaxItemValue].CalcHeight);

            foreach (var tile in map.Tiles.GetStaticAndMultiTiles(at.X, at.Y))
            {
                var data = TileData.ItemTable[tile.ID & TileData.MaxItemValue];
                if (data.Impassable && tile.Z < high && tile.Z + Math.Max(1, data.CalcHeight) > low)
                {
                    return $"an impassable {data.Name} (0x{tile.ID & TileData.MaxItemValue:X4}) at z {tile.Z}";
                }
            }

            foreach (var item in map.GetItemsAt(at.X, at.Y))
            {
                if (item.Deleted || own.Contains(item))
                {
                    continue;
                }

                if (item is BaseDoor)
                {
                    return $"a door ({item.GetType().Name}) on this tile";
                }

                var data = item.ItemData;
                if (data.Impassable && item.Z < high && item.Z + Math.Max(1, data.CalcHeight) > low)
                {
                    return $"an impassable {item.GetType().Name} (0x{item.ItemID:X4}) at z {item.Z}";
                }
            }

            foreach (var door in map.GetItemsInRange<BaseDoor>(at, 1))
            {
                var sign = door.Open ? -1 : 1;
                if (door.X + sign * door.Offset.X == at.X && door.Y + sign * door.Offset.Y == at.Y)
                {
                    return $"the swing of a door ({door.GetType().Name}) at {door.X},{door.Y},{door.Z}";
                }
            }

            return null;
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
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places the Order of the Alembic decor template at a targeted point. Test shard only; places nothing twice at one point, and refuses a changed template, an overlap with the same set or anything in the way. Dry run: 'place here dryrun'.")]
        private static void OnAlembic(CommandEventArgs e) => Run(e, "alembic");

        [Usage("DecorSmithsHall [status|place|clear] [group] [here|all] [dryrun]")]
        [Description("Dresses a Society of Smiths hall from its template (cc-P265): the fire pit, forge, anvil and a blacksmith at work. place targets the point (here: where you stand); clear removes this set's placements near you (all: everywhere). Test shard only.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places the Society of Smiths decor template at a targeted point. Test shard only; places nothing twice at one point, and refuses a changed template, an overlap with the same set or anything in the way. Dry run: 'place here dryrun'.")]
        private static void OnSmiths(CommandEventArgs e) => Run(e, "smiths");

        [Usage("DecorArtificersHall [status|place|clear] [group] [here|all] [dryrun]")]
        [Description("Dresses an Artificers' Order hall from its template (cc-P265): the arc orb gate, floating crystals and orb pillars. place targets the point (here: where you stand); clear removes this set's placements near you (all: everywhere). Test shard only.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places the Artificers' Order decor template at a targeted point. Test shard only; places nothing twice at one point, and refuses a changed template, an overlap with the same set or anything in the way. Dry run: 'place here dryrun'.")]
        private static void OnArtificers(CommandEventArgs e) => Run(e, "artificers");

        [Usage("DecorHallStructure [status|place|clear] [group] [here|all] [dryrun]")]
        [Description("Places the shared hall structure (cc-P265): Hestia's five towers, each with a door that opens and a floor inside (cc-P459), and a marble balustrade with a ladder. place targets the point (here: where you stand); a group key (tower-large, tower-medium, tower-small, tower-white-brick, tower-white-column, balustrade) places that group alone; clear removes these placements near you (all: everywhere). Test shard only.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places the shared hall structure template (towers you can walk into, balustrade) at a targeted point. Test shard only; places nothing twice at one point, and refuses a changed template, an overlap with the same set or anything in the way. Dry run: 'place here dryrun'.")]
        private static void OnStructure(CommandEventArgs e) => Run(e, "structure");
    }
}

namespace Server.Items
{
    /// <summary>
    /// One piece of guild hall decor (cc-P265): a row of a GuildHallDecorTemplates set, placed from Anchor. Placed and
    /// removed only by GuildHallDecorSeeder; not constructible, so no [add makes one. Not movable, does not decay.
    /// Version 1 (cc-P459) adds the group's template hash; a version 0 item has none, so placing at its point again asks
    /// for a clear first.
    /// </summary>
    [SerializationGenerator(1, false)]
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

        [SerializableField(4)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private string _templateHash;

        public GuildHallDecor(string setKey, string groupKey, int row, Point3D anchor, GuildHallDecorTemplates.Row data)
            : base(data.ItemId)
        {
            _setKey = setKey;
            _groupKey = groupKey;
            _row = row;
            _anchor = anchor;
            _templateHash = GuildHallDecorSeeder.TemplateHash(setKey, groupKey);
            Name = data.Name;
            Hue = data.Hue;
            Movable = false;
        }

        public override bool Decays => false;

        private void MigrateFrom(V0Content content)
        {
            _setKey = content.SetKey;
            _groupKey = content.GroupKey;
            _row = content.Row;
            _anchor = content.Anchor;
        }
    }

    /// <summary>
    /// A tower's door (cc-P459, Chase 10-23: the towers are meant to be walked into). The tower's door piece, closed;
    /// double-clicked within two tiles, it opens: it becomes the stock "nodraw" tile (0x0001, not impassable) and unseen,
    /// so the doorway is open to walk through, and it closes itself 20 seconds later, or as soon after as nobody stands
    /// in it (as a stock door's timer does). A door saved open loads closed.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class GuildHallDecorDoor : GuildHallDecor
    {
        public const int OpenedId = 0x0001;

        public static readonly TimeSpan CloseDelay = TimeSpan.FromSeconds(20.0);

        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _closedId;

        private TimerExecutionToken _closeTimer;

        public GuildHallDecorDoor(string setKey, string groupKey, int row, Point3D anchor, GuildHallDecorTemplates.Row data)
            : base(setKey, groupKey, row, anchor, data) => _closedId = data.ItemId;

        public bool Open => ItemID == OpenedId;

        public override void OnDoubleClick(Mobile from)
        {
            if (from.AccessLevel == AccessLevel.Player && !from.InRange(GetWorldLocation(), 2))
            {
                from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
                return;
            }

            if (Open)
            {
                TryClose();
            }
            else
            {
                SetOpen(true);
            }
        }

        public bool IsFreeToClose()
        {
            if (Map == null || Map == Map.Internal)
            {
                return true;
            }

            foreach (var _ in Map.GetMobilesAt(Location))
            {
                return false;
            }

            return true;
        }

        private void SetOpen(bool open)
        {
            ItemID = open ? OpenedId : _closedId;
            Visible = !open;
            Effects.PlaySound(Location, Map, open ? 0xEA : 0xF1); // a wooden door's open and close
            _closeTimer.Cancel();
            if (open)
            {
                Timer.StartTimer(CloseDelay, TimeSpan.FromSeconds(10.0), TryClose, out _closeTimer);
            }
        }

        private void TryClose()
        {
            if (Deleted || !Open)
            {
                _closeTimer.Cancel();
                return;
            }

            if (IsFreeToClose())
            {
                SetOpen(false);
            }
        }

        public override void OnAfterDelete()
        {
            base.OnAfterDelete();
            _closeTimer.Cancel();
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            if (Open)
            {
                ItemID = _closedId;
                Visible = true;
            }
        }
    }
}
