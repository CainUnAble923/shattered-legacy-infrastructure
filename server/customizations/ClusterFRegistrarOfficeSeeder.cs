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
// nothing (BaseAddon.OnComponentUsed is empty, :237); no deed (:37). The bookcases, chest and bulletin
// board are props, not containers or boards.
//
// The two signs carry the League's name, which a single click shows (Item.OnSingleClick and
// OnAosSingleClick, Item.cs:4050-4110, use Name when it is set). Their signposts stay plain props.
//
// [ClusterFSeedRegistrarOffice [status|dryrun]   places any of the three addons not already on Trammel.
//                                                 Places nothing twice; moves and deletes nothing.
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

        // The sign faces, by item id (design: 0x0BCF at 3463,2597 and 0x0BD0 at 3460,2595).
        public static readonly int[] SignItemIDs = [0x0BCF, 0x0BD0];

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

        [Usage("ClusterFSeedRegistrarOffice [status|dryrun]")]
        [Description("Places the League field office props in New Haven (outside, office, quarters) if they are not on the world. status or dryrun lists what it would place and changes nothing.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places the League field office props in New Haven if they are not on Trammel. Moves and deletes nothing.")]
        private static void OnSeedCommand(CommandEventArgs e)
        {
            var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

            if (mode is not ("" or "status" or "dryrun"))
            {
                e.Mobile.SendMessage("Usage: [ClusterFSeedRegistrarOffice [status|dryrun]");
                return;
            }

            foreach (var line in Seed(mode != ""))
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

        public static void NameSigns(BaseAddon addon)
        {
            foreach (var c in addon.Components)
            {
                if (Array.IndexOf(SignItemIDs, c.ItemID) >= 0)
                {
                    c.Name = SignName;
                }
            }
        }
    }
}

namespace Server.Items
{
    // Outside the front door: a potted tree and two signposts with their signs.
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

    // The office (east room): the desk-top props, bookcases, bulletin board, candelabra, flowerpot, globe.
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
