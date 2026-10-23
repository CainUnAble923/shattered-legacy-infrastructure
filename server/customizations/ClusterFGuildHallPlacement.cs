// cc-P281 (F-49): the guilds' real halls, placed on the test world first. The sites are cc-P151's survey with Chase's
// picks of 2026-10-21 (ClusterFGuildHallPlacementData, generated from design/guild-halls/halls.json; the map checks in
// shard-migration notes/cc-P281-tools/check.py). Per hall:
//
//   reuse   a free stock building: our guild sign takes the stock sign's place (the stock sign, if [SignGen put one
//           there, is hidden while ours stands and shown again on clear), and the guild's guildmaster stands inside
//   share   a stock building whose stock NPCs stay: our sign hangs beside theirs, our guildmaster stands with them
//   new     OSI's Guild House (multi 0x74, as pinned's GuildHouse) on the survey's plot, its two front doors and two
//           inner doors where pinned's GuildHouse hangs them, unlocked; sign and guildmaster as above
//
// and cc-P265's decor groups where the hall lists them (GuildHallDecorSeeder.Place, one anchor per group). The New
// Haven chapters (GuildLocations, the Registrar's field office) are untouched: these halls are not in the Guild
// Directory or "Show me the way" until Chase takes them live.
//
//   [ClusterFGuildHalls status                    what stands of each hall, and the halls that wait
//   [ClusterFGuildHalls place [hall] [dryrun]     place every hall, or one; skips what already stands
//   [ClusterFGuildHalls clear [hall] [dryrun]     remove exactly what place placed (and show any stock sign it hid)
//   [ClusterFGuildHalls go <hall>                 move yourself to the hall's travel stop
//   [ClusterFGuildHalls runes [hall]              a bag of recall runes marked at the travel stops
//
// TEST SHARD ONLY: place, clear and runes refuse unless testCenter.enable is true (TestCenter.Enabled, as the Staff
// Hub reads test or live). The guildmasters are found by type within a tile of their post (as ClusterFGuildHallSeeder
// finds its own); the sign, building and doors are types placed only here, each knowing its hall.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Commands;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using static Server.ClusterFGuildHallPlacementData;

namespace Server
{
    public static class ClusterFGuildHallPlacement
    {
        public const int GuildHouseMulti = 0x74;

        // pinned Houses.cs GuildHouse: AddSouthDoors(-1, 6, 7) and AddSouthDoor(-3, -1, 7), AddSouthDoor(3, -1, 7);
        // SetSign(4, 8, 16) is where our sign row puts the guild sign.
        public static readonly (int Dx, int Dy, int Dz, DoorFacing Facing)[] GuildHouseDoors =
        [
            (-1, 6, 7, DoorFacing.WestCW),
            (0, 6, 7, DoorFacing.EastCCW),
            (-3, -1, 7, DoorFacing.WestCW),
            (3, -1, 7, DoorFacing.WestCW)
        ];

        public static void Configure()
        {
            CommandSystem.Register("ClusterFGuildHalls", AccessLevel.Administrator, OnCommand);
        }

        /// <summary>Why place, clear or runes may not run here, or null on the test shard.</summary>
        public static string Refusal(bool isTestShard) =>
            isTestShard
                ? null
                : "Refused: the guild hall placement runs on the test shard only (testCenter.enable is false here). " +
                  "Nothing was placed or removed.";

        public static Hall Find(string key) =>
            Halls.FirstOrDefault(h => h.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        public static Map MapOf(Hall h) =>
            h.MapIndex >= 0 && h.MapIndex < Map.Maps.Length ? Map.Maps[h.MapIndex] : null;

        public static Mobile FindNpc(Hall h)
        {
            var map = MapOf(h);
            if (map == null || map == Map.Internal)
            {
                return null;
            }

            foreach (var m in map.GetMobilesInRange(h.Npc, 1))
            {
                if (!m.Deleted && m.GetType() == h.NpcType)
                {
                    return m;
                }
            }

            return null;
        }

        public static List<Item> Parts(Hall h) =>
            World.Items.Values.Where(i => !i.Deleted && i is IGuildHallPart p && p.HallKey == h.Key).ToList();

        public static List<GuildHallDecor> DecorOf(Hall h, Decor d)
        {
            var map = MapOf(h);
            return GuildHallDecorSeeder.Placed(d.Set).Where(i => i.Map == map && i.GroupKey == d.Group && i.Anchor == d.At).ToList();
        }

        private static IEnumerable<Hall> Select(string key) => key == null ? Halls : Halls.Where(h => h.Key == key);

        private static string Unknown(string key) =>
            $"No hall '{key}'. Halls: {string.Join(", ", Halls.Select(h => h.Key))}" +
            $"{(Waits.Length > 0 ? $"; waiting: {string.Join(", ", Waits.Select(w => w.Key))}" : "")}.";

        public static List<string> Place(string key, bool dryRun, bool isTestShard)
        {
            var refusal = Refusal(isTestShard);
            if (refusal != null)
            {
                return [refusal];
            }

            if (key != null && Find(key) == null)
            {
                return [Unknown(key)];
            }

            var lines = new List<string>();
            int placed = 0, standing = 0;
            var verb = dryRun ? "would place" : "placed";
            foreach (var h in Select(key?.ToLowerInvariant()))
            {
                var map = MapOf(h);
                if (map == null || map == Map.Internal)
                {
                    lines.Add($"{h.Key}: facet {h.MapIndex} is not loaded here, skipped.");
                    continue;
                }

                var parts = Parts(h);
                var did = new List<string>();

                if (h.Kind == Kind.New)
                {
                    if (parts.OfType<GuildHallBuilding>().Any())
                    {
                        standing++;
                    }
                    else
                    {
                        if (!dryRun)
                        {
                            new GuildHallBuilding(h.Key, h.MultiId).MoveToWorld(h.MultiAt, map);
                        }

                        did.Add($"the guild house (multi 0x{h.MultiId:X}) at {h.MultiAt}");
                    }

                    var doors = parts.OfType<GuildHallDoor>().ToList();
                    GuildHallDoor west = null, east = null;
                    foreach (var (dx, dy, dz, facing) in GuildHouseDoors)
                    {
                        var at = new Point3D(h.MultiAt.X + dx, h.MultiAt.Y + dy, h.MultiAt.Z + dz);
                        var door = doors.FirstOrDefault(d => d.Location == at);
                        if (door != null)
                        {
                            standing++;
                        }
                        else
                        {
                            did.Add($"a door at {at}");
                            if (!dryRun)
                            {
                                door = new GuildHallDoor(h.Key, facing);
                                door.MoveToWorld(at, map);
                            }
                        }

                        if (dy == 6)
                        {
                            if (dx == -1)
                            {
                                west = door;
                            }
                            else
                            {
                                east = door;
                            }
                        }
                    }

                    if (west != null && east != null)
                    {
                        west.Link = east;
                        east.Link = west;
                    }
                }

                if (parts.OfType<GuildHallSign>().Any())
                {
                    standing++;
                }
                else
                {
                    var stock = h.Kind == Kind.Reuse ? StockSignAt(map, h.Sign) : null;
                    did.Add($"the sign \"{h.SignName}\" at {h.Sign}{(stock != null ? $" (the stock sign 0x{stock.ItemID:X4} there hidden while it stands)" : "")}");
                    if (!dryRun)
                    {
                        var sign = new GuildHallSign(h.Key, 0xB95 + 2 * (int)h.SignType + (int)h.SignFacing, h.SignName);
                        if (stock != null)
                        {
                            sign.HiddenStockSign = stock;
                            stock.Visible = false;
                        }

                        sign.MoveToWorld(h.Sign, map);
                    }
                }

                if (FindNpc(h) != null)
                {
                    standing++;
                }
                else
                {
                    did.Add($"the {h.NpcType.Name} at {h.Npc}");
                    if (!dryRun)
                    {
                        PlaceNpc(h, map);
                    }
                }

                foreach (var d in h.Decor)
                {
                    var set = GuildHallDecorTemplates.Find(d.Set);
                    var group = set?.Groups.FirstOrDefault(g => g.Key == d.Group);
                    if (group == null)
                    {
                        lines.Add($"{h.Key}: no decor group {d.Set}/{d.Group}; skipped.");
                        continue;
                    }

                    var stands = DecorOf(h, d).Count;
                    if (stands >= group.Rows.Length)
                    {
                        standing++;
                        continue;
                    }

                    did.Add($"decor {d.Set}/{d.Group} ({group.Rows.Length - stands} piece(s)) from {d.At}");
                    if (!dryRun)
                    {
                        GuildHallDecorSeeder.Place(set, d.Group, map, d.At, false, isTestShard);
                    }
                }

                placed += did.Count;
                lines.Add(did.Count == 0
                    ? $"{h.Key} ({h.Town}): already standing."
                    : $"{h.Key} ({h.Town}, {h.Building}): {verb} {string.Join("; ", did)}.");
            }

            lines.Add($"Guild halls {(dryRun ? "dry run" : "done")}: {verb} {placed} thing(s), {standing} already standing.");
            return lines;
        }

        public static List<string> Clear(string key, bool dryRun, bool isTestShard)
        {
            var refusal = Refusal(isTestShard);
            if (refusal != null)
            {
                return [refusal];
            }

            if (key != null && Find(key) == null)
            {
                return [Unknown(key)];
            }

            var lines = new List<string>();
            var removed = 0;
            var verb = dryRun ? "would remove" : "removed";
            foreach (var h in Select(key?.ToLowerInvariant()))
            {
                var gone = new List<string>();
                foreach (var part in Parts(h))
                {
                    gone.Add($"{part.GetType().Name} at {part.Location}");
                    if (dryRun)
                    {
                        continue;
                    }

                    if (part is GuildHallSign { HiddenStockSign: { Deleted: false } stock })
                    {
                        stock.Visible = true;
                    }

                    part.Delete();
                }

                var npc = FindNpc(h);
                if (npc != null)
                {
                    gone.Add($"the {h.NpcType.Name} at {npc.Location}");
                    if (!dryRun)
                    {
                        npc.Delete();
                    }
                }

                foreach (var d in h.Decor)
                {
                    var items = DecorOf(h, d);
                    if (items.Count == 0)
                    {
                        continue;
                    }

                    gone.Add($"decor {d.Set}/{d.Group} ({items.Count} piece(s))");
                    if (!dryRun)
                    {
                        foreach (var i in items)
                        {
                            i.Delete();
                        }
                    }
                }

                removed += gone.Count;
                if (gone.Count > 0)
                {
                    lines.Add($"{h.Key}: {verb} {string.Join("; ", gone)}.");
                }
            }

            lines.Add($"Guild halls clear {(dryRun ? "dry run" : "done")}: {verb} {removed} thing(s); nothing else is touched.");
            return lines;
        }

        public static List<string> Status()
        {
            var lines = new List<string> { $"Guild halls (cc-P281, test shard only): {Halls.Length} halls, {Waits.Length} waiting." };
            foreach (var h in Halls)
            {
                var map = MapOf(h);
                var parts = Parts(h);
                var npc = FindNpc(h) != null;
                var decor = h.Decor.Sum(d => DecorOf(h, d).Count);
                var decorAll = h.Decor.Sum(d => GuildHallDecorTemplates.Find(d.Set)?.Groups.FirstOrDefault(g => g.Key == d.Group)?.Rows.Length ?? 0);
                lines.Add(
                    $"  {h.Key} ({h.Kind}, {h.Town}, {map?.Name ?? $"facet {h.MapIndex} not loaded"}): " +
                    $"guildmaster {(npc ? "in" : "missing")}, sign {(parts.OfType<GuildHallSign>().Any() ? "up" : "missing")}" +
                    (h.Kind == Kind.New ? $", guild house {(parts.OfType<GuildHallBuilding>().Any() ? "up" : "missing")}, doors {parts.OfType<GuildHallDoor>().Count()}/{GuildHouseDoors.Length}" : "") +
                    (decorAll > 0 ? $", decor {decor}/{decorAll}" : "") +
                    $"; travel stop {h.Travel} ({h.TravelNote})"
                );
            }

            lines.AddRange(Waits.Select(w => $"  waiting: {w.Key} ({w.Guild}): {w.Why}"));
            return lines;
        }

        /// <summary>A bag of recall runes, one marked at each hall's travel stop (or one hall's), for staff on the test shard.</summary>
        public static Bag Runes(string key, bool isTestShard, out string message)
        {
            message = Refusal(isTestShard);
            if (message != null)
            {
                return null;
            }

            if (key != null && Find(key) == null)
            {
                message = Unknown(key);
                return null;
            }

            var bag = new Bag { Name = "guild hall runes (test shard)", Hue = 0x48D };
            var n = 0;
            foreach (var h in Select(key?.ToLowerInvariant()))
            {
                var map = MapOf(h);
                if (map == null || map == Map.Internal)
                {
                    continue;
                }

                bag.DropItem(new RecallRune { Marked = true, Target = h.Travel, TargetMap = map, Description = $"{h.Guild} hall, {h.Town}" });
                n++;
            }

            message = $"A bag of {n} rune(s) marked at the guild halls' travel stops.";
            return bag;
        }

        private static BaseSign StockSignAt(Map map, Point3D p)
        {
            foreach (var i in map.GetItemsAt(p))
            {
                if (i is BaseSign { Visible: true } s && Math.Abs(s.Z - p.Z) <= 5)
                {
                    return s;
                }
            }

            return null;
        }

        private static void PlaceNpc(Hall h, Map map)
        {
            var npc = h.NpcType.CreateInstance<Mobile>();
            npc.Direction = h.NpcFacing;
            npc.CantWalk = true;
            if (npc is BaseCreature bc)
            {
                bc.Home = h.Npc;
                bc.RangeHome = 0;
            }

            npc.MoveToWorld(h.Npc, map);
        }

        [Usage("ClusterFGuildHalls [status | place [hall] [dryrun] | clear [hall] [dryrun] | go <hall> | runes [hall]]")]
        [Description("Places the guilds' real halls on the test world (cc-P281, F-49): each guild's sign, its guildmaster and its decor in the building P151's survey and Chase picked, and OSI's Guild House where the hall is new. status lists what stands; clear removes exactly what place placed; go moves you to a hall's travel stop; runes gives you runes marked at them. Test shard only.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places the guild halls (signs, guildmasters, decor, two new guild houses) on the test world. Test shard only; skips what stands. Dry run: 'place dryrun'.")]
        private static void OnCommand(CommandEventArgs e)
        {
            var args = e.Arguments.Select(a => a.ToLowerInvariant()).ToList();
            var verb = args.Count > 0 ? args[0] : "status";
            var dryRun = args.Remove("dryrun");
            var key = args.Count > 1 ? args[1] : null;
            var from = e.Mobile;

            switch (verb)
            {
                case "status":
                    Say(from, Status());
                    return;
                case "place":
                    Say(from, Place(key, dryRun, TestCenter.Enabled));
                    return;
                case "clear":
                    Say(from, Clear(key, dryRun, TestCenter.Enabled));
                    return;
                case "go":
                    {
                        var h = key == null ? null : Find(key);
                        var map = h == null ? null : MapOf(h);
                        if (map == null || map == Map.Internal)
                        {
                            from.SendMessage(key == null ? "Usage: [ClusterFGuildHalls go <hall>" : Unknown(key));
                            return;
                        }

                        from.MoveToWorld(h.Travel, map);
                        from.SendMessage($"{h.Guild}: the travel stop, {h.TravelNote}.");
                        return;
                    }
                case "runes":
                    {
                        var bag = Runes(key, TestCenter.Enabled, out var message);
                        if (bag != null)
                        {
                            from.AddToBackpack(bag);
                        }

                        from.SendMessage(message);
                        return;
                    }
                default:
                    from.SendMessage("Usage: [ClusterFGuildHalls [status | place [hall] [dryrun] | clear [hall] [dryrun] | go <hall> | runes [hall]]");
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
    }
}

namespace Server.Items
{
    /// <summary>Something cc-P281's guild hall placement put in the world, and the hall it belongs to.</summary>
    public interface IGuildHallPart
    {
        string HallKey { get; }
    }

    /// <summary>
    /// A guild hall's sign (cc-P281): stock sign art, named for the guild. Not a stock Sign, so pinned's [SignGen never
    /// replaces it; it remembers the stock sign it hid so clear shows that one again. Not movable, does not decay.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class GuildHallSign : Item, IGuildHallPart
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private string _hallKey;

        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private Item _hiddenStockSign;

        public GuildHallSign(string hallKey, int itemId, string name) : base(itemId)
        {
            _hallKey = hallKey;
            Name = name;
            Movable = false;
        }

        public override bool Decays => false;
    }

    /// <summary>A new guild hall's building (cc-P281): OSI's Guild House multi, as pinned's GuildHouse, but no house.</summary>
    [SerializationGenerator(0, false)]
    public partial class GuildHallBuilding : BaseMulti, IGuildHallPart
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private string _hallKey;

        public GuildHallBuilding(string hallKey, int multiId) : base(multiId) => _hallKey = hallKey;

        public override bool Decays => false;
    }

    /// <summary>A door of a new guild hall (cc-P281): pinned's dark wood door, unlocked, knowing its hall.</summary>
    [SerializationGenerator(0, false)]
    public partial class GuildHallDoor : BaseDoor, IGuildHallPart
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private string _hallKey;

        public GuildHallDoor(string hallKey, DoorFacing facing) : base(
            0x6A5 + 2 * (int)facing,
            0x6A6 + 2 * (int)facing,
            0xEA,
            0xF1,
            GetOffset(facing)
        ) => _hallKey = hallKey;
    }
}
