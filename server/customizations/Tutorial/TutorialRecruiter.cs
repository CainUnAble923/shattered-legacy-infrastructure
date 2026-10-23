// cc-P80: the League recruiter who runs the New Haven tutorial. Notes: shard-migration notes/cc-P80-new-player-tutorial.md.
//
// Stands on the New Haven plaza at 3500,2572,14: open floor two tiles from the arrival point (3503,2574,14; Sir Helper
// and the quest board are there), checked standable offline with cc-P15's port of pinned Map.CanFit
// (notes/cc-P80-tools). Placing an NPC changes a world, so it is never done at load by default:
//   [ClusterFSeedTutorial [dryrun]   places missing core props/NPCs and the P276 farm, on the test world only.
// tutorial.seedOnWorldLoad (default false) uses the same test-only seeder at world load.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.Commands;
using Server.Gumps;
using Server.ContextMenus;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class TutorialRecruiter : BaseCreature
    {
        public static readonly Point3D Spot = new(3500, 2572, 14);

        [Constructible]
        public TutorialRecruiter() : base(AIType.AI_Animal, FightMode.None, 10, 1)
        {
            Name = "Rook Hallowell";
            Title = "the League recruiter";
            Body = 0x190;
            Hue = 0x83EA;
            SetStr(80);
            SetDex(80);
            SetInt(80);
            SetHits(150);
            CantWalk = true;

            AddItem(new FancyShirt { Movable = false, Hue = 0x8AF });
            AddItem(new LongPants { Movable = false, Hue = 0x455 });
            AddItem(new Boots { Movable = false });
            AddItem(new BodySash { Movable = false, Hue = 0x8AF });
            AddItem(new Item(0x203B) { Movable = false, Hue = 0x47E, Layer = Layer.Hair });
        }

        public override bool IsInvulnerable => true;
        public override bool ClickTitle => true;

        public override void OnDoubleClick(Mobile from)
        {
            if (from is not PlayerMobile pm)
            {
                return;
            }

            if (!pm.InRange(Location, 4))
            {
                pm.SendLocalizedMessage(500446); // That is too far away.
                return;
            }

            Say("Ah, a new face. Or an old one. It is hard to tell anymore.");
            pm.CloseGump<Server.Tutorial.TutorialRecruiterGump>();
            pm.SendGump(new Server.Tutorial.TutorialRecruiterGump(pm));
        }

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list) =>
            base.GetContextMenuEntries(from, ref list);

        /// <summary>A quest arrow from the player to the recruiter, if one stands near its spot.</summary>
        public static void PointTo(PlayerMobile pm)
        {
            var recruiter = Find();
            if (recruiter != null && recruiter.Map == pm.Map)
            {
                pm.QuestArrow = new GuildDirectionArrow(pm, recruiter, recruiter.Location, recruiter.Map, "the League recruiter");
            }
        }

        public static TutorialRecruiter? Find()
        {
            foreach (var m in Map.Trammel.GetMobilesInRange(Spot, 15))
            {
                if (m is TutorialRecruiter r && !r.Deleted)
                {
                    return r;
                }
            }

            return null;
        }
    }
}

namespace Server.Tutorial
{
    using Server.Mobiles;

    public static class TutorialSeeder
    {
        public static void Configure()
        {
            var seedOnLoad = ServerConfiguration.GetOrUpdateSetting("tutorial.seedOnWorldLoad", false);
            if (seedOnLoad)
            {
                EventSink.WorldLoad += () => Console.WriteLine($"[Tutorial] {Seed(false)}");
            }

            CommandSystem.Register("ClusterFSeedTutorial", AccessLevel.Administrator, OnSeed);
        }

        [Usage("ClusterFSeedTutorial [dryrun]")]
        [Description("Places the tutorial core and P276 farm on the test world. dryrun places nothing.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places the tutorial core and farm once on the test world.")]
        private static void OnSeed(CommandEventArgs e)
        {
            var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";
            if (mode is not ("" or "dryrun"))
            {
                e.Mobile.SendMessage("Usage: [ClusterFSeedTutorial [dryrun]");
                return;
            }

            e.Mobile.SendMessage(Seed(mode == "dryrun"));
        }

        public static string Seed(bool dryRun) => TutorialCorePlacement.Seed(dryRun);
    }
}
