// cc-P80: the League recruiter who runs the New Haven tutorial. Notes: shard-migration notes/cc-P80-new-player-tutorial.md.
//
// Stands on the New Haven plaza at 3500,2572,14: open floor two tiles from the arrival point (3503,2574,14; Sir Helper
// and the quest board are there), checked standable offline with cc-P15's port of pinned Map.CanFit
// (notes/cc-P80-tools). Placing an NPC changes a world, so it is never done at load by default:
//   [ClusterFSeedTutorial [dryrun]   places missing core props/NPCs and the P276 farm, on the test world only.
// tutorial.seedOnWorldLoad (default false) uses the same test-only seeder at world load.
// cc-P663: the core lessons moved to the Wakening Isle, with Tamsin (TutorialGuide). Rook stays on the plaza (HavenSpots)
// for graduation, the wings, skip and remedial training, and sends a trainee with core lessons left to the isle.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.Commands;
using Server.Gumps;
using Server.ContextMenus;
using Server.Items;
using Server.Tutorial;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class TutorialRecruiter : BaseCreature
    {
        public static Point3D Spot => HavenSpots.Rook;

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
                pm.SendMessage("Move closer, then double-click Rook again."); // cc-P609: was cliloc 500446
                return;
            }

            Server.Tutorial.ClusterFTutorial.AdoptOrphanedRecord(pm); // cc-P613: a record whose character is gone
            SayTo(pm, "Welcome to New Haven. My window shows your tutorial progress."); // cc-P663: to this player only
            pm.CloseGump<Server.Tutorial.TutorialRecruiterGump>();
            pm.SendGump(new Server.Tutorial.TutorialRecruiterGump(pm));
        }

        public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list) =>
            base.GetContextMenuEntries(from, ref list);

        /// <summary>
        /// A quest arrow from the player to the recruiter, if one stands near its spot. cc-P663: a trainee on the isle is
        /// pointed at Tamsin instead (the moongate is the way to Rook).
        /// </summary>
        public static void PointTo(PlayerMobile pm)
        {
            if (TutorialWorld.IsOnIsle(pm))
            {
                Server.Tutorial.TutorialGuide.PointTo(pm);
                return;
            }

            var recruiter = Find();
            if (recruiter != null && recruiter.Map == pm.Map)
            {
                pm.QuestArrow = new GuildDirectionArrow(pm, recruiter, recruiter.Location, recruiter.Map, "Rook Hallowell, the tutorial guide");
            }
        }

        public static TutorialRecruiter? Find()
        {
            foreach (var m in HavenSpots.Map.GetMobilesInRange(Spot, 15))
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
            // cc-P728 item 7 (Chase 10-28 15:28, "pip is missing"): the isle's pieces come out at every world load, so an
            // update never needs a staff command. tutorial.seedMissingOnLoad (true) runs the idempotent seed while the
            // tutorial is on (the test shard; live once the isle opens there): only what is missing is placed, on the Fold's
            // isle and on every seeded copy, and what it placed is logged. tutorial.seedOnWorldLoad (false, before cc-P728)
            // still forces it, as it did.
            var missingOnLoad = ServerConfiguration.GetOrUpdateSetting("tutorial.seedMissingOnLoad", true);
            var forced = ServerConfiguration.GetOrUpdateSetting("tutorial.seedOnWorldLoad", false);
            if (missingOnLoad || forced)
            {
                // cc-P726: on the first tick after the load, not inside it: an entity made during World.Load reaches
                // World.Items only once the load ends, so the copies' top-up counted nothing ("nothing missing") and left
                // what it placed off the copy's reset list (seen in the P726 bot run's shard log).
                EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () =>
                {
                    foreach (var line in SeedMissing(forced))
                    {
                        Console.WriteLine($"[Tutorial] {line}");
                    }
                });
            }

            CommandSystem.Register("ClusterFSeedTutorial", AccessLevel.Administrator, OnSeed);
        }

        /// <summary>
        /// cc-P728 item 7: places what is missing on the Fold's isle (with Rook and New Haven's vet) and on every seeded copy.
        /// Nothing while the tutorial is off (unless <paramref name="forced"/>) or the isle's land is not on this server's map.
        /// Returns the lines to log.
        /// </summary>
        public static List<string> SeedMissing(bool forced = false)
        {
            var lines = new List<string>();
            if (!ClusterFTutorial.Enabled && !forced)
            {
                lines.Add("Seed on load: skipped, the tutorial is off on this shard.");
                return lines;
            }

            if (!TutorialWorld.IsleReady())
            {
                lines.Add("Seed on load: skipped, the Wakening Isle's land is not on this server's map.");
                return lines;
            }

            lines.Add($"Seed on load, the Fold: {Seed(false)}");
            lines.AddRange(TutorialCopies.TopUpSeeded());
            return lines;
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
