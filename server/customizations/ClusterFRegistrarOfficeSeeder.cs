// cc-P19: props for the League of Extraordinary Citizens Field Office in New Haven (Trammel), where the
// League Registrar sits. cc-P678: on SoS the office is the free hall north-east of the square (walls x3658-3674,
// y2547-2562, floor z0, the street door at 3667-3668,2562; the quarters are the back room behind the long counter,
// 3670-3673, 2548-2550); it was the two-room house at x3453-3462, y2596-2603, z18. Adds items only; the map's own
// counters and furniture stay as they are.
//
// The layout is design/registrar-office/layout.json, generated into ClusterFRegistrarOfficeLayout.cs by
// cc-P16's mechanism, and placed by cc-P16's placer (ClusterFMineCampSeeder.SeedAddons). One addon per
// area of the design: outside the door, the office (east room), the quarters (west room). Each addon
// sits at its area's first component and keeps every component at its design x, y, z (pinned
// Items/Addons/BaseAddon.cs:134-146, :241-252). The components are pinned AddonComponents: not movable
// (AddonComponent.cs:102), so they never decay (Projects/Server/Items/Item.cs:331); double-click does
// nothing (BaseAddon.OnComponentUsed is empty, :237); no deed (:37). The bookcases and chest are props,
// not containers.
//
// The sign carries the League's name, which a single click shows (Item.OnSingleClick and
// OnAosSingleClick, Item.cs:4050-4110, use Name when it is set). Its signpost stays a plain prop.
//
// cc-P31: the design went from 18 components to 14 (the north signpost and sign, the bulletin board and
// the globe out; the candelabra to the SE corner). An addon builds its components once, in its
// constructor; a saved one comes back with the components it was saved with (BaseAddon.cs:39-40,
// :278-286), and the plain command skips a type already on Trammel. So a world seeded by cc-P19 keeps
// the 18 until replace swaps them.
//
// [ClusterFSeedRegistrarOffice [status|dryrun]   places any of the three addons not already on Trammel.
//                                                 Places nothing twice; moves and deletes nothing.
// [ClusterFSeedRegistrarOffice replace [dryrun]  deletes the three addon types found on Trammel (their
//                                                 own components go with them, BaseAddon.cs:267-276), then
//                                                 places the current layout. Touches no other item, NPC,
//                                                 door or static. Running it again ends in the same state.
//
// World load places the props only if clusterf.registrarOffice.seedOnWorldLoad is true (default false;
// the server writes the default into modernuo.json on first start), the rule cc-P16 and cc-P17 set: the
// other seeders run on every world load on live, so none of them could carry this.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Items;

namespace Server
{
    public static class ClusterFRegistrarOfficeSeeder
    {
        public const string SignName = "League of Extraordinary Citizens";

        // The sign, by item id (design: 0x0BCF at 3670,2563 since cc-P678). cc-P31: 0x0BD0 left the design.
        public const int SignItemID = 0x0BCF;

        // cc-P51: the Staff Hub's Travel row. The office's addons have no standing point of their own, so this is the
        // League Registrar's seeded tile inside the office (ClusterFNewHavenSeeder.Entries, which places her); nothing
        // here changes what either seeder places.
        public static Map Facet => ClusterFNewHavenSeeder.Facet;

        public static Point3D Anchor => ClusterFNewHavenSeeder.RegistrarAnchor;

        private static bool _seedOnWorldLoad;
        private static bool _upgradeOnWorldLoad;

        public static void Configure()
        {
            _seedOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("clusterf.registrarOffice.seedOnWorldLoad", false);
            // cc-P803: a world that already holds the office decor gets the current layout by itself (guide 7). Only an
            // office standing in this layout's own building is swapped (UpgradeStale), so a world whose office is in
            // another town's house is left alone, and a world with no office decor still gets none from this.
            _upgradeOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("clusterf.registrarOffice.upgradeOnWorldLoad", true);

            EventSink.WorldLoad += OnWorldLoad;
            CommandSystem.Register("ClusterFSeedRegistrarOffice", AccessLevel.Administrator, OnSeedCommand);
        }

        private static void OnWorldLoad()
        {
            if (_upgradeOnWorldLoad)
            {
                // After the load: addons made inside World.Load reach World.Items only once it ends (cc-P726).
                Timer.DelayCall(TimeSpan.Zero, () =>
                {
                    foreach (var line in UpgradeStale())
                    {
                        Console.WriteLine(line);
                    }
                });
            }

            if (!_seedOnWorldLoad)
            {
                return;
            }

            foreach (var line in Seed(false))
            {
                Console.WriteLine(line);
            }
        }

        [Usage("ClusterFSeedRegistrarOffice [status|dryrun|replace [dryrun]]")]
        [Description("Places the League field office props in New Haven (outside, office, quarters) if they are not on the world. status or dryrun lists what it would place and changes nothing. replace deletes the three office addons already on Trammel and places the current layout; replace dryrun lists both and changes nothing.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places the League field office props in New Haven if they are not on Trammel. Moves and deletes nothing. 'replace' (dry run: 'replace dryrun') swaps an older office decor for the current one, deleting only its own three addons.")]
        private static void OnSeedCommand(CommandEventArgs e)
        {
            var args = new string[e.Length];
            for (var i = 0; i < e.Length; i++)
            {
                args[i] = e.GetString(i).ToLowerInvariant();
            }

            List<string> lines = args switch
            {
                [] => Seed(false),
                ["status" or "dryrun"] => Seed(true),
                ["replace"] => Replace(false),
                ["replace", "status" or "dryrun"] => Replace(true),
                _ => null
            };

            if (lines == null)
            {
                e.Mobile.SendMessage("Usage: [ClusterFSeedRegistrarOffice [status|dryrun|replace [dryrun]]");
                return;
            }

            foreach (var line in lines)
            {
                e.Mobile.SendMessage(line);
            }
        }

        internal static readonly (Type Type, Func<BaseAddon> Create, MineCampComponent[] Components)[] Addons =
        [
            (typeof(RegistrarOfficeOutsideAddon), () => new RegistrarOfficeOutsideAddon(), ClusterFRegistrarOfficeLayout.Outside),
            (typeof(RegistrarOfficeAddon), () => new RegistrarOfficeAddon(), ClusterFRegistrarOfficeLayout.Office),
            (typeof(RegistrarQuartersAddon), () => new RegistrarQuartersAddon(), ClusterFRegistrarOfficeLayout.Quarters),
        ];

        internal static List<string> Seed(bool dryRun) =>
            ClusterFMineCampSeeder.SeedAddons("registrar office", "addons", Addons, dryRun);

        // cc-P31. Deletes every addon of the three types on Trammel, whatever layout it was built from, then
        // places the current layout through the same placer as Seed. Selection is by exact type and map only:
        // nothing else is a candidate, whatever its item id or tile. A dry run deletes and places nothing and
        // reports what it would do, the placement as if the deletions had happened.
        internal static List<string> Replace(bool dryRun)
        {
            var found = FindOfficeAddons();
            var lines = new List<string>();
            var components = 0;

            foreach (var addon in found)
            {
                lines.Add(
                    $"{addon.GetType().Name} 0x{addon.Serial.Value:X8}: {(dryRun ? "would delete" : "deleted")} at {addon.Location} " +
                    $"({addon.Map}), {addon.Components.Count} components."
                );
                components += addon.Components.Count;
            }

            if (!dryRun)
            {
                foreach (var addon in found)
                {
                    addon.Delete();
                }
            }

            lines.Add(
                $"ClusterF registrar office replace: {(dryRun ? "would delete" : "deleted")} {found.Count} addons ({components} components)."
            );

            lines.AddRange(ClusterFMineCampSeeder.SeedAddons("registrar office", "addons", Addons, dryRun, found));
            return lines;
        }

        /// <summary>
        /// cc-P803: true when the addon's components are exactly its area's layout (item id and tile, as a multiset).
        /// </summary>
        internal static bool IsCurrent(BaseAddon addon)
        {
            var row = Array.Find(Addons, a => a.Type == addon.GetType());
            if (row.Components == null)
            {
                return false;
            }

            static string Key(int id, int x, int y, int z) => $"{id:X4}@{x},{y},{z}";
            var want = row.Components.Select(c => Key(c.ItemID, c.X, c.Y, c.Z)).OrderBy(k => k, StringComparer.Ordinal);
            var have = addon.Components.Select(c => Key(c.ItemID, c.X, c.Y, c.Z)).OrderBy(k => k, StringComparer.Ordinal);
            return want.SequenceEqual(have);
        }

        /// <summary>cc-P803: the current layout's box, three tiles round every component.</summary>
        internal static bool InThisBuilding(Point3D p)
        {
            var all = ClusterFRegistrarOfficeLayout.Components;
            return p.X >= all.Min(c => c.X) - 3 && p.X <= all.Max(c => c.X) + 3 &&
                   p.Y >= all.Min(c => c.Y) - 3 && p.Y <= all.Max(c => c.Y) + 3;
        }

        /// <summary>
        /// cc-P803: on a world whose office decor (all of it in this layout's building) differs from the current layout,
        /// runs Replace: the three office addons go and the current layout is placed. Nothing when there is no office
        /// decor, when it is current, or when any of it stands elsewhere. Running it again changes nothing.
        /// </summary>
        internal static List<string> UpgradeStale()
        {
            var found = FindOfficeAddons();
            if (found.Count == 0)
            {
                return [];
            }

            if (!found.TrueForAll(a => InThisBuilding(a.Location)))
            {
                return ["ClusterF registrar office upgrade: office decor stands outside this layout's building; left alone."];
            }

            if (found.TrueForAll(IsCurrent) && found.Count == Addons.Length)
            {
                return [];
            }

            var lines = Replace(false);
            lines.Insert(0, "ClusterF registrar office upgrade: the decor was an older layout; replacing it with the current one.");
            return lines;
        }

        internal static List<BaseAddon> FindOfficeAddons()
        {
            var found = new List<BaseAddon>();

            foreach (var item in World.Items.Values)
            {
                if (!item.Deleted && item.Map == Map.Trammel && item is BaseAddon addon &&
                    Array.Exists(Addons, a => a.Type == item.GetType()))
                {
                    found.Add(addon);
                }
            }

            return found;
        }

        public static void NameSigns(BaseAddon addon)
        {
            foreach (var c in addon.Components)
            {
                if (c.ItemID == SignItemID)
                {
                    c.Name = SignName;
                }
            }
        }
    }
}

namespace Server.Items
{
    // Outside: a potted tree by the front door, and the signpost with its sign on the east wall.
    // Components: ClusterFRegistrarOfficeLayout.Outside.
    [SerializationGenerator(0, false)]
    public partial class RegistrarOfficeOutsideAddon : BaseAddon
    {
        [Constructible]
        public RegistrarOfficeOutsideAddon()
        {
            ClusterFMineCampSeeder.AddComponents(this, ClusterFRegistrarOfficeLayout.Outside);
            ClusterFRegistrarOfficeSeeder.NameSigns(this);
        }
    }

    // The office (east room): the desk-top props, bookcases, candelabra, flowerpot.
    // Components: ClusterFRegistrarOfficeLayout.Office.
    [SerializationGenerator(0, false)]
    public partial class RegistrarOfficeAddon : BaseAddon
    {
        [Constructible]
        public RegistrarOfficeAddon() =>
            ClusterFMineCampSeeder.AddComponents(this, ClusterFRegistrarOfficeLayout.Office);
    }

    // The Registrar's quarters (west room): a chest, a bookcase, a scroll on the side table.
    // Components: ClusterFRegistrarOfficeLayout.Quarters.
    [SerializationGenerator(0, false)]
    public partial class RegistrarQuartersAddon : BaseAddon
    {
        [Constructible]
        public RegistrarQuartersAddon() =>
            ClusterFMineCampSeeder.AddComponents(this, ClusterFRegistrarOfficeLayout.Quarters);
    }
}
