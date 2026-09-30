// cc-P16: the New Haven mine camp's two tents (Trammel), and the move of the Miners' Compact Liaison and
// the Survey Archivist to their openings. The first "layer 1" building: existing UO tiles placed by the
// server as addon components, with no client or map-file change.
//
// The layout is design/mine-camp-tents/layout.json, generated into ClusterFMineCampLayout.cs. Each tent
// is one addon whose components sit at the design's world x, y, z exactly: an addon keeps each
// component at its own offset (BaseAddon.AddComponent and OnLocationChange, pinned
// Items/Addons/BaseAddon.cs:134-146, :241-252), and nothing on the MoveToWorld path runs CouldFit
// (:148-204; called only by deeds, BaseAddonDeed.cs:87, and houses), so uneven ground is not refused.
// The components are pinned AddonComponents: not movable (AddonComponent.cs:102), so they never decay
// (Item.Decays, Projects/Server/Items/Item.cs:331); double-click does nothing (OnComponentUsed is empty,
// BaseAddon.cs:237); the addon has no deed (BaseAddon.Deed is null, :37), and chopping works only inside
// a house its chopper owns (:92-132). The crate, chest and barrel are props, not containers.
//
// [ClusterFSeedMineCamp [status|dryrun]   places either tent that is not already on the world. Places
//                                          nothing twice: a tent of that type anywhere on Trammel counts.
// [ClusterFMoveMineCampNpcs [dryrun]      moves the Liaison and the Archivist standing near their old
//                                          spots to the tent openings. Creates nothing; a re-run moves nothing.
//
// World load places the tents only if clusterf.mineCamp.seedOnWorldLoad is true (default false; the
// server writes the default into modernuo.json on first start), the same rule as cc-P17's New Haven
// services seeder: the existing seeders run on every world load on live, so none of them could carry
// this. World load never moves an NPC (ClusterFInstitutionSeeder accepts one near its old spot).

using System;
using System.Collections.Generic;
using System.Text;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server
{
    public static class ClusterFMineCampSeeder
    {
        // How far from its old spot an NPC may stand and still be "the one near its old spot", the same
        // range ClusterFInstitutionSeeder uses for "already there".
        public const int NearRange = 15;

        private static bool _seedOnWorldLoad;

        public static void Configure()
        {
            _seedOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("clusterf.mineCamp.seedOnWorldLoad", false);

            EventSink.WorldLoad += OnWorldLoad;
            CommandSystem.Register("ClusterFSeedMineCamp", AccessLevel.Administrator, OnSeedCommand);
            CommandSystem.Register("ClusterFMoveMineCampNpcs", AccessLevel.Administrator, OnMoveCommand);
        }

        private static void OnWorldLoad()
        {
            if (!_seedOnWorldLoad)
            {
                return;
            }

            foreach (var line in Seed(false))
            {
                Console.WriteLine(line);
            }
        }

        [Usage("ClusterFSeedMineCamp [status|dryrun]")]
        [Description("Places the two New Haven mine camp tents if they are not on the world. status or dryrun lists what it would place and changes nothing.")]
        private static void OnSeedCommand(CommandEventArgs e)
        {
            var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

            if (mode is not ("" or "status" or "dryrun"))
            {
                e.Mobile.SendMessage("Usage: [ClusterFSeedMineCamp [status|dryrun]");
                return;
            }

            foreach (var line in Seed(mode != ""))
            {
                e.Mobile.SendMessage(line);
            }
        }

        [Usage("ClusterFMoveMineCampNpcs [dryrun]")]
        [Description("Moves the Miners' Compact Liaison and the Survey Archivist from near their old spots to the mine camp tent openings. Creates nothing; a re-run moves nothing.")]
        private static void OnMoveCommand(CommandEventArgs e)
        {
            var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

            if (mode is not ("" or "dryrun"))
            {
                e.Mobile.SendMessage("Usage: [ClusterFMoveMineCampNpcs [dryrun]");
                return;
            }

            foreach (var line in MoveNpcs(mode == "dryrun"))
            {
                e.Mobile.SendMessage(line);
            }
        }

        // -- The tents --

        internal static readonly (Type Type, Func<BaseAddon> Create, MineCampComponent[] Components)[] Tents =
        [
            (typeof(MinersCompactTentAddon), () => new MinersCompactTentAddon(), ClusterFMineCampLayout.MinersCompactTent),
            (typeof(SurveyArchivistTentAddon), () => new SurveyArchivistTentAddon(), ClusterFMineCampLayout.SurveyArchivistTent),
        ];

        // An addon sits at its first component; every component is placed at its offset from there.
        public static Point3D OriginOf(MineCampComponent[] components) => components[0].Location;

        public static void AddComponents(BaseAddon addon, MineCampComponent[] components)
        {
            var origin = OriginOf(components);

            foreach (var c in components)
            {
                addon.AddComponent(new AddonComponent(c.ItemID), c.X - origin.X, c.Y - origin.Y, c.Z - origin.Z);
            }
        }

        internal static List<string> Seed(bool dryRun)
        {
            var lines = new List<string>();
            var placed = 0;

            foreach (var (type, create, components) in Tents)
            {
                var origin = OriginOf(components);
                var existing = FindTent(type);

                if (existing != null)
                {
                    lines.Add($"{type.Name}: already on the world at {existing.Location} ({existing.Map}); nothing placed.");
                    continue;
                }

                var others = ForeignItemsOn(components);
                var verb = dryRun ? "would place" : "placed";
                lines.Add($"{type.Name}: {verb} at {origin} (Trammel), {components.Length} components.");

                if (others.Count > 0)
                {
                    lines.Add($"  already on its tiles, left alone: {string.Join("; ", others)}");
                }

                if (!dryRun)
                {
                    create().MoveToWorld(origin, Map.Trammel);
                }

                placed++;
            }

            lines.Add($"ClusterF mine camp {(dryRun ? "dry run" : "seed")}: {(dryRun ? "would place" : "placed")} {placed} of {Tents.Length} tents.");
            return lines;
        }

        internal static BaseAddon FindTent(Type type)
        {
            foreach (var item in World.Items.Values)
            {
                if (!item.Deleted && item.GetType() == type && item.Map == Map.Trammel)
                {
                    return (BaseAddon)item;
                }
            }

            return null;
        }

        // Items not ours standing on a component tile, for the status line. Never moved or deleted.
        private static List<string> ForeignItemsOn(MineCampComponent[] components)
        {
            var tiles = new HashSet<Point2D>();
            foreach (var c in components)
            {
                tiles.Add(new Point2D(c.X, c.Y));
            }

            var found = new List<string>();
            foreach (var item in World.Items.Values)
            {
                if (!item.Deleted && item.Map == Map.Trammel && item.Parent == null &&
                    item is not AddonComponent && tiles.Contains(new Point2D(item.X, item.Y)))
                {
                    found.Add($"{item.GetType().Name} 0x{item.ItemID:X4} at {item.Location}");
                }
            }

            return found;
        }

        // -- The NPCs --

        internal static List<string> MoveNpcs(bool dryRun)
        {
            var lines = new List<string>();
            var moved = 0;

            foreach (var move in ClusterFMineCampLayout.Npcs)
            {
                var type = NpcType(move.TypeName);
                var atNew = Nearest(type, move.To, 0);

                if (atNew != null && IsInPlace(atNew, move))
                {
                    lines.Add($"{move.TypeName}: {Describe(atNew)} is already at {move.To}; not moved.");
                    continue;
                }

                var near = Near(type, move.From);
                if (near.Count == 0)
                {
                    var elsewhere = Nearest(type, move.To, NearRange);
                    lines.Add(elsewhere != null
                        ? $"{move.TypeName}: none near its old spot {move.From}; {Describe(elsewhere)} stands at {elsewhere.Location}, left alone."
                        : $"{move.TypeName}: none near its old spot {move.From}; nothing moved and nothing created.");
                    continue;
                }

                var npc = near[0];
                var from = npc.Location;

                if (!dryRun)
                {
                    Place(npc, move);
                }

                moved++;
                lines.Add($"{move.TypeName}: {(dryRun ? "would move" : "moved")} {Describe(npc)} from {from} to {move.To}, facing {move.Facing}.");

                if (near.Count > 1)
                {
                    lines.Add($"  {near.Count - 1} more {move.TypeName} near the old spot, left where they are.");
                }
            }

            lines.Add($"ClusterF mine camp NPCs {(dryRun ? "dry run" : "moved")}: {moved} of {ClusterFMineCampLayout.Npcs.Length}.");
            return lines;
        }

        internal static Type NpcType(string name) =>
            name switch
            {
                nameof(MinersCompactLiaison) => typeof(MinersCompactLiaison),
                nameof(SurveyArchivist)      => typeof(SurveyArchivist),
                _                            => throw new InvalidOperationException($"layout names an unknown NPC type {name}")
            };

        // What ClusterFInstitutionSeeder does to a new NPC, at the layout's tile and facing.
        private static void Place(Mobile npc, MineCampNpcMove move)
        {
            npc.Direction = move.Facing;
            npc.CantWalk = true;

            if (npc is BaseCreature bc)
            {
                bc.Home = move.To;
                bc.RangeHome = 0;
            }

            npc.MoveToWorld(move.To, Map.Trammel);
        }

        private static bool IsInPlace(Mobile npc, MineCampNpcMove move) =>
            npc.Location == move.To && npc.Direction == move.Facing && npc.CantWalk &&
            (npc is not BaseCreature bc || bc.Home == move.To && bc.RangeHome == 0);

        // Every NPC of the type within NearRange of a point on Trammel, nearest first.
        private static List<Mobile> Near(Type type, Point3D point)
        {
            var list = new List<Mobile>();
            foreach (var m in World.Mobiles.Values)
            {
                if (!m.Deleted && m.GetType() == type && m.Map == Map.Trammel && Distance(m, point) <= NearRange)
                {
                    list.Add(m);
                }
            }

            list.Sort((a, b) => Distance(a, point).CompareTo(Distance(b, point)));
            return list;
        }

        private static Mobile Nearest(Type type, Point3D point, int range)
        {
            Mobile best = null;
            foreach (var m in World.Mobiles.Values)
            {
                if (!m.Deleted && m.GetType() == type && m.Map == Map.Trammel && Distance(m, point) <= range &&
                    (best == null || Distance(m, point) < Distance(best, point)))
                {
                    best = m;
                }
            }

            return best;
        }

        private static int Distance(Mobile m, Point3D p) => Math.Max(Math.Abs(m.X - p.X), Math.Abs(m.Y - p.Y));

        private static string Describe(Mobile m) => $"{m.Name} (serial {m.Serial})";
    }
}

namespace Server.Items
{
    // The Miners' Compact Liaison's tent. Components: ClusterFMineCampLayout.MinersCompactTent.
    [SerializationGenerator(0, false)]
    public partial class MinersCompactTentAddon : BaseAddon
    {
        [Constructible]
        public MinersCompactTentAddon() =>
            ClusterFMineCampSeeder.AddComponents(this, ClusterFMineCampLayout.MinersCompactTent);
    }

    // The Survey Archivist's tent. Components: ClusterFMineCampLayout.SurveyArchivistTent.
    [SerializationGenerator(0, false)]
    public partial class SurveyArchivistTentAddon : BaseAddon
    {
        [Constructible]
        public SurveyArchivistTentAddon() =>
            ClusterFMineCampSeeder.AddComponents(this, ClusterFMineCampLayout.SurveyArchivistTent);
    }
}
