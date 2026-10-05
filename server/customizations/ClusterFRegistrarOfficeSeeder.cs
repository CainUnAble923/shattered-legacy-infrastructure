// cc-P19: props for the League of Extraordinary Citizens Field Office in New Haven (Trammel), the
// two-room house where the League Registrar sits (walls x3453-3462, y2596-2603, floor z18). Adds items
// only; the map's own bed, dresser, table and chair stay as they are.
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
using ModernUO.Serialization;
using Server.Items;

namespace Server
{
    public static class ClusterFRegistrarOfficeSeeder
    {
        public const string SignName = "League of Extraordinary Citizens";

        // The sign, by item id (design: 0x0BCF at 3463,2597). cc-P31: 0x0BD0 at 3460,2595 left the design.
        public const int SignItemID = 0x0BCF;

        // cc-P51: the Staff Hub's Travel row. The office's addons have no standing point of their own, so this is the
        // League Registrar's seeded tile inside the office (ClusterFNewHavenSeeder.Entries, which places her); nothing
        // here changes what either seeder places.
        public static Map Facet => ClusterFNewHavenSeeder.Facet;

        public static Point3D Anchor => ClusterFNewHavenSeeder.RegistrarAnchor;

        private static bool _seedOnWorldLoad;

        public static void Configure()
        {
            _seedOnWorldLoad = ServerConfiguration.GetOrUpdateSetting("clusterf.registrarOffice.seedOnWorldLoad", false);

            EventSink.WorldLoad += OnWorldLoad;
            CommandSystem.Register("ClusterFSeedRegistrarOffice", AccessLevel.Administrator, OnSeedCommand);
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
