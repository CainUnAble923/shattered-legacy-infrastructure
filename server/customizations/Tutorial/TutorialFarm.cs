// cc-P276 (F-35): the New Haven tutorial farm. Notes: shard-migration notes/cc-P276-tutorial-farm-build.md; the design
// and the approved site: docs/feature-queue.md F-35 (the west site, the v3 farmhouse, Chase 2026-10-04 "looks good").
//
// What it is, west of New Haven by the Bardic Guild (field fence x 3381-3399, y 2603-2619, the farmhouse at 3377,2611
// against its west fence, the gate on the east side at 3399,2614-2615 facing town):
//   - TutorialFarmAddon: the farmhouse, fence, pen, crop rows, hay, scarecrows, well and coop, every piece stock art at
//     the approved tile and Z (TutorialFarmLayout, generated from the v3 layout). One addon, server-placed.
//   - A real dark wood door in the farmhouse's east wall (the layout's 0x06AD), so the porch opens into the field.
//   - The Farmhand (Wren Tallow) at the gate, who runs the farm's lesson (below).
//   - Two spawners: the field's pests (tutorial-only: they engage only tutorial participants, TutorialCreature) and the
//     pen's stock goats, sheep, cows and chickens.
// The map patch that clears the two trees inside the fence (player-package/vendor/shattered-legacy-map/trammel.mapops)
// must be on the server's map first: the seeder refuses while the trees are still there, unless "force".
//
// The lesson (F-35's order: tame the tutorial pet, fight the pests with it, heal), the station "animals.taming":
//   1. The Farmhand offers a dog or a cat, the evo pet (EvoPet.GivePet: bonded at once, the player's hue pick).
//      TutorialRecord.PetClaimed (the key farm.pet before cc-P453). An account that took its pet here is not offered
//      the pick again at graduation.
//   2. Clear the field: PestsToClear pests that the player or the pet damaged, killed. Key farm.pests; completes the
//      station animals.taming. The count is kept in memory (a restart mid-step starts the count again; noted).
//   3. Patch up: the Farmhand hands bandages (a stack only when the pack has none, at most BandageIssues stacks per
//      account); a bandage that really healed someone (Bandage-tutorial-real-heal.patch, after Patient.Heal in pinned
//      BandageContext.OnTick) finishes it. Key farm.heal; completes the station combat.healing.
// Keys are TutorialRecord.StationsDone strings (RootdeepVault's pattern); only the two station keys count toward the
// core, and neither is a core station. cc-P453: the pet itself is TutorialRecord.PetClaimed (one per account, kept through
// remedial training), and the bandage stacks handed out are TutorialRecord.BandagesIssued.
//
// The seeder is a staff command, test shard only for now:
//   [ClusterFSeedTutorialFarm [dryrun | force]   places what is missing; moves and deletes nothing; idempotent.
// It refuses on any shard that is not the test shard (ClusterFStaffHub.IsTestShard) unless clusterf.tutorialFarm.allowLive
// is true, which nobody sets until Chase says the farm goes live.
//
// cc-P663 (P481 F3): the farm is on the Wakening Isle now (TutorialIsleFarmLayout: every piece shifted by (+3314, +1364),
// its height above the old ground kept), and its lesson is the core station core.farm, after chat: Wren answers a trainee
// only once the lessons before it are done, and the third step finishes it (TutorialCoreTrials.FarmFinished). An account
// that already has its pet starts at the pests. The isle seeder (TutorialCorePlacement) calls Seed: the isle's farm, its
// door, Wren, the pests and the pen. [ClusterFSeedTutorialFarm now seeds only New Haven's farm props (the farmhouse,
// door and pen) that the wing trials still use there (the pen trial, the porch tour stop) until S7 moves them.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Commands;
using Server.Engines.Spawners;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;

// cc-P718 (Chase's isle walk, 10-28): step 2 begins with the pet commands (items 10): Wren sets the pet to stay and the
// trainee makes it follow ("all follow me") and guard ("all guard me"), by speech or the pet's menu; the pet's own order
// is the evidence, so either way counts. Then the pest part opens: Wren opens the field gate (TutorialPestGate, which only
// a trainee on that part or staff can open), the pests fight only such a trainee or their pet (item 7) and never step
// outside the field fence, and one of the pests must fall to the pet on "all kill". Step 3's line now says how to use a
// bandage and how to make more (item 9).

namespace Server.Tutorial
{
    public static class TutorialFarm
    {
        // The v0 marker for the pet; cc-P453 reads it only when loading an old record (TutorialRecord.PetClaimed now).
        public const string PetKey = TutorialRecord.LegacyFarmPetKey;
        public const string PestsKey = "farm.pests";
        public const string HealKey = "farm.heal";
        public const string TamingStation = "animals.taming";
        public const string CoreStation = "core.farm";
        public const string HealingStation = "combat.healing";

        public static readonly string[] FarmPetLines = ["dog", "cat"];

        /// <summary>The isle farm's spot for New Haven farm spot <paramref name="p"/> (the farm moved whole).</summary>
        public static Point2D OnIsle(Point2D p) => new(p.X + TutorialIsleFarmLayout.Shift.X, p.Y + TutorialIsleFarmLayout.Shift.Y);

        /// <summary>Just outside the gate, beside the sign (New Haven's 3400,2614, moved with the farm).</summary>
        public static readonly Point2D FarmhandSpot = OnIsle(new Point2D(3400, 2614));

        /// <summary>The crop rows the pests live in, and the pen (inside its own fence), on the isle.</summary>
        public static readonly Point2D PestSpot = OnIsle(new Point2D(3388, 2614));
        public const int PestRange = 3;
        public const int PestCount = 3;
        /// <summary>New Haven's pen (x 3393-3398, y 2604-2608), still seeded there for the wing trials' pen.</summary>
        public static readonly Point2D HavenPenSpot = new(3395, 2606);

        public static readonly Point2D PenSpot = OnIsle(HavenPenSpot); // after HavenPenSpot: static fields run in order
        public const int PenRange = 2;
        public const int PenCount = 4;

        public static readonly string[] PestTypes = [nameof(FarmRat), nameof(FieldRat), nameof(GrassSnake)];
        public static readonly string[] PenTypes = ["Goat", "Sheep", "Cow", "Chicken"];

        public static int PestsToClear => ServerConfiguration.GetOrUpdateSetting("clusterf.tutorialFarm.pestsToClear", 3);
        public static int BandagesGiven => ServerConfiguration.GetOrUpdateSetting("clusterf.tutorialFarm.bandages", 10);
        public static int BandageIssues => ServerConfiguration.GetOrUpdateSetting("clusterf.tutorialFarm.bandageIssues", 3);
        public static bool AllowLive => ServerConfiguration.GetOrUpdateSetting("clusterf.tutorialFarm.allowLive", false);

        /// <summary>Whether this is the test shard. Tests replace it.</summary>
        public static Func<bool> IsTestShard { get; set; } = () => ClusterFStaffHub.IsTestShard;

        /// <summary>Whether the server's Trammel has the map patch (the two trees gone). Tests replace it.</summary>
        public static Func<bool> MapPatched { get; set; } = () => TreesLeft(HavenSpots.Map) == 0;

        // The Farmhand's lines (ours, for Chase's eye) and the Echo's (the "System:" voice).
        public const string NotTraineeLine = "These farm lessons are for characters taking the tutorial. Double-click Tamsin on the beach to check your progress.";
        public const string NotYetLine = "Finish {0} first, then come back to me.";
        public const string PetOfferLine = "Choose a dog or cat in the window. This is your one tutorial pet, which you keep after graduation.";
        public const string NoSlotLine = "You cannot control another pet right now. Stable one of your pets with an animal trainer, then come back.";
        public const string PestsLine = "Walk into the cabbage rows, say 'all kill', then click a rat or grass snake for your pet to attack. Defeat {0} pests to finish this step.";
        public const string PestsLeftLine = "Defeat {0} more rats or grass snakes in the cabbage rows.";
        public const string HealLine = "Here are some bandages. To heal, double-click a bandage in your backpack, then click yourself or your hurt pet. A successful heal finishes this step.";
        // cc-P718 item 9: the one line on making more, said after the bandages are handed over.
        public const string MakeBandagesLine = "Later you can make your own bandages: double-click a pair of scissors, then click a pile of cloth to cut it into bandages.";

        // cc-P718 item 10: the pet commands, the first part of step 2.
        public const string FollowKey = "farm.cmd.follow";
        public const string GuardKey = "farm.cmd.guard";
        public const string CommandsLine = "Your pet obeys spoken commands. I told it to stay. Say 'all follow me' and it follows you; then say 'all guard me' and it guards you. You can say your pet's name instead of 'all', or click your pet with the right mouse button and choose a command from its menu.";
        public const string FollowDoneLine = "Your pet follows you. Now say 'all guard me', or choose Guard from your pet's menu.";
        public const string GuardDoneLine = "Your pet guards you. I opened the field gate for you: walk into the cabbage rows, say 'all kill', then click a rat or grass snake for your pet to attack. Defeat {0} pests. If the gate swings shut, double-click it to open it.";
        public const string NoPetForCommandsLine = "You have no pet with you, so the command lesson is skipped. I opened the field gate: defeat {0} rats or grass snakes in the cabbage rows.";
        public const string PetKillLine = "Now let your pet fight one: say 'all kill', then click a rat or grass snake.";
        public const string GateClosedLine = "Wren keeps this gate shut until your pest lesson. Double-click Wren the Farmhand first.";

        /// <summary>
        /// cc-P718 item 7: inside the field fence (P276's x 3381-3399, y 2603-2619, moved to the isle), where the pests stay.
        /// End points exclusive (Rectangle2D): x 6696-6712, y 3968-3982.
        /// </summary>
        public static readonly Rectangle2D PestField = new(OnIsle(new Point2D(3382, 2604)), OnIsle(new Point2D(3399, 2619)));

        /// <summary>
        /// cc-P718: the field's gate, in the two-tile gap of its east fence (6713, 3978-3979; the layout leaves it open).
        /// Every wooden gate's art is impassable open or shut, and pinned swings a gate in a north-south fence east by one
        /// tile (BaseDoor offsets), so two halves would stand on both tiles outside the gap (Wren's and the one below it)
        /// and shut the field: the P718 bot run could not get in. So one gate, on the gap's south tile, swinging north-east
        /// onto Wren's own tile (NorthCCW, offset +1,-1), and a fence piece closing the gap's north tile. A player walks in
        /// from the tile below Wren.
        /// </summary>
        public static readonly Point2D[] FieldGateSpots = [new(6713, 3979)];

        public static readonly Point2D FieldFenceSpot = new(6713, 3978);
        public const int FieldFenceId = 0x0837; // the layout's own wooden fence, north-south
        // cc-P707: the pests die to the pet before they bite, so nobody is hurt and a bandage only says "That being is not
        // damaged!". Wren gives an unhurt trainee a scratch to practise on.
        public const string ScratchLine = "A rat nipped your ankle in the rows. Double-click a bandage in your backpack, then click yourself to treat it. A successful heal finishes this step.";
        // cc-P707 run 4: said when a bandage on the heal step finds nobody hurt (the trainee's hits came back by themselves).
        public const string ScratchAgainLine = "A rat nipped your ankle in the rows. Double-click a bandage again, then click yourself to treat it.";
        public const string NoMoreBandagesLine = "I have no more free bandages for you. You can buy more from a healer in town.";
        public const string NoRoomForBandagesLine = "Your backpack is too full for these bandages. Make room in it, then double-click me again.";
        public const string DoneLine = "You finished the farm lessons. If your bonded pet dies, bring its ghost to Tobin the vet beside me. While you are Young, he brings it back free.";
        public const string EchoPetLine = "This pet is yours to keep. Say 'all follow me' to have it follow you.";
        public const string EchoPestsLine = "You cleared the farm pests. Return to Wren the Farmhand at the gate.";
        public const string EchoHealLine = "Your bandage restored health and finished the farm's healing lesson.";

        // Pests counted per character this run (not saved: a restart starts the step's count again).
        private static readonly Dictionary<Serial, int> _pestsKilled = new();

        private static bool _hooked;

        // The command and the hooks are only needed once the world is up, so Initialize (adds no Configure to pinned's list).
        public static void Initialize()
        {
            CommandSystem.Register("ClusterFSeedTutorialFarm", AccessLevel.Administrator, OnSeedCommand);
            EnsureHooks();
        }

        /// <summary>The station's handler and the heal hook, once. The test host calls it: Initialize never runs there.</summary>
        public static void EnsureHooks()
        {
            if (_hooked)
            {
                return;
            }

            _hooked = true;
            // cc-P291: Rook's animals.taming station is now its own trial at the pen (TutorialTradeTrials.cs); the
            // Farmhand's pet-and-pests lesson still completes it too.
            // cc-P453: the heal step listens to the bandage patch, not SkillEvents.SkillUsed (pinned rolls the skills on
            // a failed treatment too).
            BandageHealed = OnBandageHealed;
            SureHeal = (healer, _) => healer is PlayerMobile pm && StepOf(pm) == 3; // cc-P707
            Unhurt = OnUnhurt; // cc-P707 run 4
        }

        /// <summary>
        /// Raised by Bandage-tutorial-real-heal.patch after a bandage restored hit points (healer, patient). A static
        /// delegate rather than a direct call, so the patch only needs this class to exist.
        /// </summary>
        public static Action<Mobile, Mobile>? BandageHealed { get; set; }

        /// <summary>
        /// cc-P707: asked by Bandage-tutorial-real-heal.patch before the healing roll (healer, patient). True makes the
        /// bandage heal: a trainee on the farm's heal step, whatever their Healing (pinned's chance is (Healing + 10)%, one
        /// in ten for a newcomer without it). The heal is still real, so BandageHealed still finishes the step.
        /// </summary>
        public static Func<Mobile, Mobile, bool>? SureHeal { get; set; }

        /// <summary>
        /// cc-P707 run 4: asked by Bandage-tutorial-real-heal.patch when a bandage finds its patient unhurt, as it begins
        /// ("That being is not damaged!") or as it ends (the hits came back meanwhile). True when the farm handled it
        /// instead: the trainee got a scratch and was told to bandage again, and pinned's message and the Placebo
        /// achievement are skipped.
        /// </summary>
        public static Func<Mobile, Mobile, bool>? Unhurt { get; set; }

        public static TutorialRecord? RecordOf(PlayerMobile pm) => ClusterFTutorial.RecordOf(pm);

        // cc-P480 Part 8: the Farmhand notices a dead bonded pet near her and tells its owner where the vet is, once per
        // owner per cooldown.
        public static readonly TimeSpan GhostPetCooldown = TimeSpan.FromMinutes(2);
        public const string GhostPetLine = "Your pet has died. Wait for its body to disappear, then bring its ghost to Tobin the vet beside me.";
        public const string GhostPetNoVetLine = "Your pet has died. Wait for its body to disappear, then bring its ghost to any town's vet.";
        // cc-P666: a Young owner's pet on the isle comes back at Tobin at once, free and whole (TutorialPetCare.IsleCare).
        public const string GhostPetYoungLine = "Your pet has died. Bring its ghost to Tobin the vet beside me. While you are Young, he brings it back free, with everything it carried.";
        // cc-P666: Sera, the healer at the shrine, points pet ghosts to Tobin too.
        public const string HealerGhostPetLine = "I heal people, not pets. Wait for your pet's body to disappear, then bring its ghost to Tobin the vet at the farm gate.";
        public const string HealerGhostPetYoungLine = "I heal people, not pets. Bring your pet's ghost to Tobin the vet at the farm gate. While you are Young, he brings it back free.";
        private static readonly Dictionary<Serial, DateTime> _ghostPetSaid = new();

        /// <summary>The dead pet line for this speaker (Wren, or Sera the healer) and this pet.</summary>
        public static string GhostPetLineFor(Mobile speaker, BaseCreature pet)
        {
            if (FindVet() == null)
            {
                return GhostPetNoVetLine;
            }

            var young = FoldMaps.IsFold(pet.Map) && pet.ControlMaster is PlayerMobile { Young: true };
            return speaker is TutorialHealer
                ? young ? HealerGhostPetYoungLine : HealerGhostPetLine
                : young ? GhostPetYoungLine : GhostPetLine;
        }

        /// <summary>Says the dead pet line to its owner, unless said to them within the cooldown. True when it spoke.</summary>
        public static bool NoticeGhostPet(Mobile hand, BaseCreature pet)
        {
            if (!pet.IsDeadBondedPet || pet.ControlMaster is not PlayerMobile owner || !hand.InRange(pet, 4) ||
                _ghostPetSaid.TryGetValue(owner.Serial, out var at) && Core.Now - at < GhostPetCooldown)
            {
                return false;
            }

            _ghostPetSaid[owner.Serial] = Core.Now;
            hand.SayTo(owner, GhostPetLineFor(hand, pet));
            return true;
        }

        /// <summary>The farm's vet, beside the Farmhand (placed by the tutorial seeder).</summary>
        public static readonly Point2D FarmVetSpot = OnIsle(new Point2D(3400, 2612));

        public static TutorialVeterinarian? FindVet(Map? map = null)
        {
            foreach (var m in (map ?? TutorialWorld.Isle).GetMobilesInRange(new Point3D(FarmVetSpot, 0), 4))
            {
                if (m is TutorialVeterinarian v && !v.Deleted)
                {
                    return v;
                }
            }

            return null;
        }

        public static bool Has(PlayerMobile pm, string key) => RecordOf(pm)?.StationsDone.Contains(key) == true;

        public static int PestsKilled(PlayerMobile pm) => _pestsKilled.TryGetValue(pm.Serial, out var n) ? n : 0;

        /// <summary>The step a participant is on: 1 pet, 2 pests, 3 heal, 4 done; 0 when not a participant.</summary>
        public static int StepOf(PlayerMobile pm) =>
            !ClusterFTutorial.IsParticipant(pm) ? 0 :
            RecordOf(pm)?.PetClaimed != true ? 1 :
            !Has(pm, PestsKey) ? 2 :
            !Has(pm, HealKey) ? 3 : 4;

        /// <summary>The station's handler: a line and a quest arrow to the Farmhand.</summary>
        public static void PointTo(PlayerMobile pm)
        {
            ClusterFTutorial.SystemLine(pm, "Follow the arrow east along the road to the farm gate. Double-click Wren the Farmhand, who wears a straw hat and carries a pitchfork.");
            if (TutorialFarmhand.Find(TutorialWorld.MapOf(pm)) is { } hand && hand.Map == pm.Map)
            {
                pm.QuestArrow = new GuildDirectionArrow(pm, hand, hand.Location, hand.Map, "Wren the Farmhand");
            }
        }

        /// <summary>The player talks to the Farmhand (double-click): the next step. Returns the step it answered for.</summary>
        public static int Talk(PlayerMobile pm, Mobile hand)
        {
            var step = StepOf(pm);

            // cc-P663: the farm is a core station; a trainee with earlier lessons left is sent back to them.
            if (step is >= 1 and <= 3 && RecordOf(pm) is { } record && !ClusterFTutorial.IsDone(record, CoreStation) &&
                System.Linq.Enumerable.FirstOrDefault(ClusterFTutorial.CoreLeft(record)) is { } next && next.Key != CoreStation)
            {
                hand.SayTo(pm, string.Format(NotYetLine, next.Title));
                return 0;
            }

            switch (step)
            {
                case 0:
                    hand.SayTo(pm, NotTraineeLine);
                    break;
                case 1:
                    hand.SayTo(pm, PetOfferLine);
                    pm.CloseGump<TutorialFarmPetGump>();
                    pm.SendGump(new TutorialFarmPetGump());
                    break;
                case 2 when !CommandsDone(pm):
                    hand.SayTo(pm, BeginCommands(pm) ? CommandsLine : string.Format(NoPetForCommandsLine, PestsToClear));
                    break;
                case 2:
                    OpenFieldGate(hand.Map);
                    var left = Math.Max(0, PestsToClear - PestsKilled(pm));
                    hand.SayTo(pm, PestsKilled(pm) == 0 ? string.Format(PestsLine, PestsToClear) : string.Format(PestsLeftLine, left));
                    break;
                case 3:
                    var gift = GiveBandages(pm);
                    hand.SayTo(pm, gift switch
                    {
                        BandageGift.NoneLeft => NoMoreBandagesLine,
                        BandageGift.NoRoom => NoRoomForBandagesLine,
                        _ => Scratch(pm) ? ScratchLine : HealLine
                    });
                    if (gift is BandageGift.Given or BandageGift.HasSome)
                    {
                        hand.SayTo(pm, MakeBandagesLine); // cc-P718 item 9
                    }

                    break;
                default:
                    hand.SayTo(pm, DoneLine);
                    break;
            }

            return step;
        }

        /// <summary>Step 1: the pet. Null when refused (not on this step, not a farm line, or no follower slot).</summary>
        public static EvoPet? ChoosePet(PlayerMobile pm, string line)
        {
            if (StepOf(pm) != 1 || Array.IndexOf(FarmPetLines, line) < 0)
            {
                return null;
            }

            var pet = EvoPet.GivePet(pm, line);

            if (pet == null)
            {
                pm.SendMessage(0x22, NoSlotLine);
                return null;
            }

            RecordOf(pm)!.PetClaimed = true;
            ClusterFTutorial.SystemLine(pm, EchoPetLine);
            if (!CheckDone(pm) && StepOf(pm) == 2 && !CommandsDone(pm))
            {
                ClusterFTutorial.SystemLine(pm, BeginCommands(pm) ? CommandsLine : string.Format(NoPetForCommandsLine, PestsToClear));
            }

            return pet;
        }

        /// <summary>Step 2: a pest died. Every participant on step 2 who (or whose pet) damaged it counts one.</summary>
        public static void OnPestDeath(FarmPest pest)
        {
            var credited = new HashSet<PlayerMobile>();

            foreach (var entry in pest.DamageEntries.ToArray())
            {
                var who = entry.Damager is BaseCreature { Controlled: true } pet ? pet.ControlMaster : entry.Damager;

                if (who is PlayerMobile pm && PestPartOn(pm) && credited.Add(pm))
                {
                    CountPest(pm, ByPetOnCommand(pest, pm));
                }
            }
        }

        // cc-P718: a pest the trainee's pet damaged while under the kill command.
        private static bool ByPetOnCommand(FarmPest pest, PlayerMobile pm)
        {
            foreach (var entry in pest.DamageEntries.ToArray())
            {
                if (entry.Damager is BaseCreature { Controlled: true } pet && pet.ControlMaster == pm && pet.ControlOrder == OrderType.Attack)
                {
                    return true;
                }
            }

            return false;
        }

        // Trainees whose pet has felled a pest on command this run (in memory, like the count).
        private static readonly HashSet<Serial> _petKilled = new();

        /// <summary>A pest counts for the trainee; staff and tests credit it as the pet's, on command.</summary>
        public static void CountPest(PlayerMobile pm) => CountPest(pm, true);

        public static void CountPest(PlayerMobile pm, bool byPetOnCommand)
        {
            if (byPetOnCommand)
            {
                _petKilled.Add(pm.Serial);
            }

            var n = PestsKilled(pm) + 1;
            _pestsKilled[pm.Serial] = n;

            if (n < PestsToClear)
            {
                pm.SendMessage(0x3B2, $"Farm pests defeated: {n} of {PestsToClear}.");
                return;
            }

            // cc-P718 item 10: at least one pest falls to the pet on "all kill", when the trainee has a pet with them.
            if (!_petKilled.Contains(pm.Serial) && PetsOf(pm).Count > 0)
            {
                ClusterFTutorial.SystemLine(pm, PetKillLine);
                return;
            }

            _pestsKilled.Remove(pm.Serial);
            _petKilled.Remove(pm.Serial);
            RecordOf(pm)!.StationsDone.Add(PestsKey);
            ClusterFTutorial.SystemLine(pm, EchoPestsLine);
            ClusterFTutorial.CompleteStation(pm, TamingStation);
            if (!CheckDone(pm))
            {
                ClusterFTutorial.SystemLine(pm, "Return to Wren the Farmhand at the farm gate and double-click her.");
            }
        }

        /// <summary>cc-P663: all three steps done (StepOf 4) finishes core.farm. True when they are.</summary>
        public static bool CheckDone(PlayerMobile pm)
        {
            if (StepOf(pm) != 4)
            {
                return false;
            }

            TutorialCoreTrials.FarmFinished(pm);
            ClusterFTutorial.SystemLine(pm, TutorialPetCare.FarmPetLessonLine); // cc-P666: the farm's pet lesson line
            return true;
        }

        // -- cc-P718 item 10: the pet commands --

        public static bool CommandsDone(PlayerMobile pm) => Has(pm, FollowKey) && Has(pm, GuardKey);

        /// <summary>cc-P718: the pest part of step 2, after the commands: the pests fight the trainee only now.</summary>
        public static bool PestPartOn(PlayerMobile pm) => StepOf(pm) == 2 && CommandsDone(pm);

        /// <summary>The trainee's living pets on their map.</summary>
        public static List<BaseCreature> PetsOf(PlayerMobile pm)
        {
            var pets = new List<BaseCreature>();
            foreach (var f in pm.AllFollowers ?? [])
            {
                if (f is BaseCreature { Controlled: true, Alive: true, Deleted: false, IsDeadBondedPet: false } pet &&
                    pet.ControlMaster == pm && pet.Map == pm.Map)
                {
                    pets.Add(pet);
                }
            }

            return pets;
        }

        private static readonly HashSet<PlayerMobile> _watching = new();
        private static TimerExecutionToken _watchTimer;

        /// <summary>
        /// Starts the commands: the trainee's pets stay, and their orders are watched until they follow and then guard.
        /// False when the trainee has no pet with them: the commands are skipped and the field gate opens.
        /// </summary>
        public static bool BeginCommands(PlayerMobile pm)
        {
            var pets = PetsOf(pm);
            if (pets.Count == 0)
            {
                var record = RecordOf(pm)!;
                record.StationsDone.Add(FollowKey);
                record.StationsDone.Add(GuardKey);
                OpenFieldGate(pm.Map);
                return false;
            }

            if (!Has(pm, FollowKey))
            {
                foreach (var pet in pets)
                {
                    pet.ControlTarget = null;
                    pet.ControlOrder = OrderType.Stay;
                }
            }

            _watching.Add(pm);
            if (!_watchTimer.Running)
            {
                Timer.StartTimer(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500), WatchAll, out _watchTimer);
            }

            return true;
        }

        private static void WatchAll()
        {
            foreach (var pm in new List<PlayerMobile>(_watching))
            {
                CheckCommands(pm);
            }

            if (_watching.Count == 0)
            {
                _watchTimer.Cancel();
            }
        }

        /// <summary>Reads the pets' orders (speech or menu, both set them): follow first, then guard. True when both are done.</summary>
        public static bool CheckCommands(PlayerMobile pm)
        {
            if (pm.Deleted || StepOf(pm) != 2 || CommandsDone(pm))
            {
                _watching.Remove(pm);
                return CommandsDone(pm);
            }

            var pets = PetsOf(pm);
            var record = RecordOf(pm)!;
            if (!Has(pm, FollowKey) && pets.Exists(p => p.ControlOrder == OrderType.Follow && p.ControlTarget == pm))
            {
                record.StationsDone.Add(FollowKey);
                ClusterFTutorial.SystemLine(pm, FollowDoneLine);
            }
            else if (Has(pm, FollowKey) && pets.Exists(p => p.ControlOrder == OrderType.Guard))
            {
                record.StationsDone.Add(GuardKey);
                _watching.Remove(pm);
                OpenFieldGate(pm.Map);
                ClusterFTutorial.SystemLine(pm, string.Format(GuardDoneLine, PestsToClear));
                return true;
            }

            return false;
        }

        // -- cc-P718 item 7: the field gate --

        /// <summary>The field gate's two halves on <paramref name="map"/>, open or closed.</summary>
        public static List<TutorialPestGate> FieldGates(Map? map)
        {
            var gates = new List<TutorialPestGate>();
            if (map == null)
            {
                return gates;
            }

            foreach (var spot in FieldGateSpots)
            {
                if (FindDoor(map, TutorialWorld.Ground(map, spot)) is TutorialPestGate gate)
                {
                    gates.Add(gate);
                }
            }

            return gates;
        }

        /// <summary>Wren opens the field gate (it closes again by itself, as any gate).</summary>
        public static void OpenFieldGate(Map? map)
        {
            foreach (var gate in FieldGates(map))
            {
                if (!gate.Open)
                {
                    gate.Open = true;
                }
            }
        }

        /// <summary>The field gate (one, see FieldGateSpots).</summary>
        public static TutorialPestGate[] MakeFieldGates() => [new(DoorFacing.NorthCCW)];

        /// <summary>The fence piece closing the gap's north tile on <paramref name="map"/>, or null.</summary>
        public static Item? FindFieldFence(Map map)
        {
            foreach (var item in map.GetItemsInRange<Static>(TutorialWorld.Ground(map, FieldFenceSpot), 0))
            {
                if (!item.Deleted && item.ItemID == FieldFenceId)
                {
                    return item;
                }
            }

            return null;
        }

        public enum BandageGift { Given, HasSome, NoneLeft, NoRoom }

        /// <summary>
        /// cc-P707: step 3 needs a bandage that really heals. When neither the trainee nor a pet of theirs within 12 tiles is
        /// hurt, takes a fifth of the trainee's hits (at least 5, never the last one). True when it did.
        /// </summary>
        public static bool Scratch(PlayerMobile pm)
        {
            if (!pm.Alive || pm.Hits < pm.HitsMax)
            {
                return false;
            }

            foreach (var f in pm.AllFollowers ?? [])
            {
                if (f is BaseCreature { Alive: true, Deleted: false } pet && pet.Map == pm.Map && pet.InRange(pm, 12) &&
                    pet.Hits < pet.HitsMax)
                {
                    return false;
                }
            }

            pm.Hits = Math.Max(1, pm.HitsMax - Math.Max(5, pm.HitsMax / 5));
            return pm.Hits < pm.HitsMax;
        }

        /// <summary>
        /// Step 3's bandages: a stack when the pack has none, counted on the account's record and capped at BandageIssues
        /// (cc-P453: the pack check alone let a banked or traded stack be asked for again, and sold).
        /// cc-P613 (P573 #1): counted only once the stack is in the pack. A pack too full (or none) gets nothing, on the
        /// ground or on the record: NoRoom, and the trainee is asked to make room.
        /// </summary>
        public static BandageGift GiveBandages(PlayerMobile pm)
        {
            if (pm.Backpack?.FindItemByType<Bandage>() != null)
            {
                return BandageGift.HasSome;
            }

            if (RecordOf(pm) is not { } record || record.BandagesIssued >= BandageIssues)
            {
                return BandageGift.NoneLeft;
            }

            var stack = new Bandage(BandagesGiven);
            if (!pm.PlaceInBackpack(stack))
            {
                stack.Delete();
                return BandageGift.NoRoom;
            }

            record.BandagesIssued++;
            return BandageGift.Given;
        }

        // Step 3: a bandage that worked, on anyone (self, the pet, another). The healer's skill rolls do not count: pinned
        // rolls them on a failed treatment too (Bandage.cs, BandageContext.OnTick).
        private static void OnBandageHealed(Mobile healer, Mobile patient)
        {
            if (healer is PlayerMobile pm)
            {
                OnHealed(pm);
            }
        }

        // Step 3, a bandage on an unhurt patient: the trainee or one of their pets. Scratch refuses while anyone is hurt.
        public static bool OnUnhurt(Mobile healer, Mobile patient)
        {
            if (healer is not PlayerMobile pm || StepOf(pm) != 3 ||
                patient != pm && (patient as BaseCreature)?.ControlMaster != pm || !Scratch(pm))
            {
                return false;
            }

            pm.SendMessage(ScratchAgainLine);
            return true;
        }

        public static bool OnHealed(PlayerMobile pm)
        {
            if (StepOf(pm) != 3)
            {
                return false;
            }

            RecordOf(pm)!.StationsDone.Add(HealKey);
            ClusterFTutorial.SystemLine(pm, EchoHealLine);
            ClusterFTutorial.CompleteStation(pm, HealingStation);
            CheckDone(pm);
            return true;
        }

        // -- The seeder --

        public readonly record struct SeedResult(bool Refused, int Addons, int Doors, int Farmhands, int Spawners, int Present, string Message);

        /// <summary>How many of the two trees' four statics the server's map still holds (0 once the patch is on).</summary>
        public static int TreesLeft(Map map)
        {
            var n = 0;
            foreach (var (id, x, y, z) in TutorialFarmLayout.RemovedTrees)
            {
                foreach (var tile in map.Tiles.GetStaticTiles(x, y))
                {
                    if (tile.ID == id && tile.Z == z)
                    {
                        n++;
                    }
                }
            }

            return n;
        }

        [Usage("ClusterFSeedTutorialFarm [dryrun | force]")]
        [Description("Places New Haven's tutorial farm props (the farmhouse and field addon, its door and the pen spawner) that the wing trials use, where missing. The core farm, with Wren and the pests, is on the Wakening Isle ([ClusterFSeedTutorial). Test shard only unless clusterf.tutorialFarm.allowLive; refuses while the map still has the two trees the farm's map patch removes, unless force. Moves and deletes nothing.")]
        [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestOnly, DryRun = "dryrun", Summary = "Places New Haven's tutorial farm props (farmhouse, field, pen) for the wing trials on the test shard. Moves and deletes nothing.")]
        private static void OnSeedCommand(CommandEventArgs e)
        {
            var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";
            if (mode is not ("" or "dryrun" or "force"))
            {
                e.Mobile.SendMessage("Usage: [ClusterFSeedTutorialFarm [dryrun | force]");
                return;
            }

            e.Mobile.SendMessage(SeedHaven(mode == "dryrun", mode == "force").Message);
        }

        private static SeedResult? Refusal(bool island)
        {
            if (!IsTestShard() && !AllowLive)
            {
                return new SeedResult(true, 0, 0, 0, 0, 0,
                    "Tutorial farm: not seeded. This is not the test shard (and clusterf.tutorialFarm.allowLive is off).");
            }

            return island && TutorialWorld.Isle == null
                ? new SeedResult(true, 0, 0, 0, 0, 0, "Tutorial farm: not seeded. The Fold (map 32) is not registered.")
                : null;
        }

        /// <summary>cc-P663: the core farm on the isle: the farm addon, its door, Wren, the pests and the pen.</summary>
        public static SeedResult Seed(bool dryRun) => Seed(TutorialWorld.Isle, dryRun);

        /// <summary>cc-P664: the core farm onto one map: the Fold, or a tutorial copy (TutorialCopies).</summary>
        public static SeedResult Seed(Map map, bool dryRun)
        {
            if (Refusal(true) is { } refused)
            {
                return refused;
            }

            int addons = 0, doors = 0, hands = 0, spawners = 0, present = 0;

            if (FindAddon<TutorialIsleFarmAddon>(map, TutorialIsleFarmLayout.Anchor) is { } placed)
            {
                present++;
                if (!dryRun) { FixGateSign(placed); } // cc-P718
            }
            else
            {
                if (!dryRun) { new TutorialIsleFarmAddon().MoveToWorld(TutorialIsleFarmLayout.Anchor, map); }
                addons++;
            }

            if (FindDoor(map, TutorialIsleFarmLayout.DoorSpot) != null) { present++; }
            else
            {
                if (!dryRun) { MakeDoor().MoveToWorld(TutorialIsleFarmLayout.DoorSpot, map); }
                doors++;
            }

            // cc-P718 item 7: the field gate, closed, in the gap of the east fence, and a fence piece beside it (both
            // counted with the doors).
            if (FindFieldFence(map) != null) { present++; }
            else
            {
                if (!dryRun) { new Static(FieldFenceId) { Name = "wooden fence" }.MoveToWorld(TutorialWorld.Ground(map, FieldFenceSpot), map); }
                doors++;
            }

            var gates = MakeFieldGates();
            for (var i = 0; i < FieldGateSpots.Length; i++)
            {
                var spot = TutorialWorld.Ground(map, FieldGateSpots[i]);
                if (FindDoor(map, spot) != null) { present++; gates[i].Delete(); }
                else if (dryRun) { doors++; gates[i].Delete(); }
                else { gates[i].MoveToWorld(spot, map); doors++; }
            }

            if (TutorialFarmhand.Find(map) != null) { present++; }
            else
            {
                if (!dryRun)
                {
                    var spot = TutorialWorld.Ground(map, FarmhandSpot);
                    new TutorialFarmhand { Direction = Direction.East, Home = spot, RangeHome = 0 }.MoveToWorld(spot, map);
                }

                hands++;
            }

            SeedSpawners(map, SpawnerRows, dryRun, ref spawners, ref present);

            var verb = dryRun ? "would place" : "placed";
            return new SeedResult(false, addons, doors, hands, spawners, present,
                $"Tutorial farm on the isle{(dryRun ? " (dry run)" : "")}: {verb} {addons} farm addon, {doors} door, {hands} Farmhand, {spawners} spawners; already there {present}.");
        }

        /// <summary>New Haven's farm props for the wing trials: the farm addon, its door and the pen (no Wren, no pests).</summary>
        public static SeedResult SeedHaven(bool dryRun, bool force = false)
        {
            if (Refusal(false) is { } refused)
            {
                return refused;
            }

            if (!force && !MapPatched())
            {
                return new SeedResult(true, 0, 0, 0, 0, 0,
                    "Tutorial farm: not seeded. The server's map still has the two trees inside the fence; load the map patch first (or use force).");
            }

            var map = HavenSpots.Map;
            int addons = 0, doors = 0, spawners = 0, present = 0;

            if (FindAddon<TutorialFarmAddon>(map, TutorialFarmLayout.Anchor) != null) { present++; }
            else
            {
                if (!dryRun) { new TutorialFarmAddon().MoveToWorld(TutorialFarmLayout.Anchor, map); }
                addons++;
            }

            if (FindDoor(map, TutorialFarmLayout.DoorSpot) != null) { present++; }
            else
            {
                if (!dryRun) { MakeDoor().MoveToWorld(TutorialFarmLayout.DoorSpot, map); }
                doors++;
            }

            SeedSpawners(map, HavenSpawnerRows, dryRun, ref spawners, ref present);

            var verb = dryRun ? "would place" : "placed";
            return new SeedResult(false, addons, doors, 0, spawners, present,
                $"New Haven farm props{(dryRun ? " (dry run)" : "")}: {verb} {addons} farm addon, {doors} door, {spawners} spawners; already there {present}.");
        }

        private static void SeedSpawners(Map map, (Point2D Spot, int Range, int Count, string[] Types)[] rows, bool dryRun,
            ref int spawners, ref int present)
        {
            foreach (var (spot, range, count, types) in rows)
            {
                if (FindSpawner(map, spot, types[0]) != null) { present++; continue; }

                if (!dryRun)
                {
                    var s = new Spawner(count, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(3), 0, default, types)
                    {
                        Name = $"Tutorial farm: {string.Join(", ", types)}"
                    };
                    s.MoveToWorld(new Point3D(spot.X, spot.Y, map.GetAverageZ(spot.X, spot.Y)), map);
                    s.HomeRange = range; // after the move: the bounds are centred on the spawner (BaseSpawner.cs:189-219)
                    s.Respawn();
                }

                spawners++;
            }
        }

        public static readonly (Point2D Spot, int Range, int Count, string[] Types)[] SpawnerRows =
        [
            (PestSpot, PestRange, PestCount, PestTypes),
            (PenSpot, PenRange, PenCount, PenTypes)
        ];

        public static readonly (Point2D Spot, int Range, int Count, string[] Types)[] HavenSpawnerRows =
        [
            (HavenPenSpot, PenRange, PenCount, PenTypes)
        ];

        /// <summary>The layout's door art 0x06AD: dark wood, facing SouthCW = 4 (closed id 0x06A5 + 2 x facing, Doors.cs DarkWoodDoor).</summary>
        public static BaseDoor MakeDoor() => new DarkWoodDoor(DoorFacing.SouthCW);

        public static T? FindAddon<T>(Map map, Point3D anchor) where T : BaseAddon
        {
            foreach (var item in map.GetItemsInRange<T>(anchor, 2))
            {
                if (!item.Deleted)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>The isle's farm addon.</summary>
        public static TutorialIsleFarmAddon? FindAddon(Map map) => FindAddon<TutorialIsleFarmAddon>(map, TutorialIsleFarmLayout.Anchor);

        // cc-P718 item 6: the farm gate's sign was P276's wall arm (0x0B97) with a board hung on it (0x0BCF), standing on
        // the ground, where it read as debris. On the isle it is a standing post with its board (0xA854) instead. The
        // generated layout keeps P276's pieces; the isle's addon swaps them as it is built, and FixGateSign mends a farm
        // placed before.
        public const int OldSignArm = 0x0B97;
        public const int OldSignBoard = 0x0BCF;
        public const int GateSignpost = 0xA854;
        public const string GateSignpostName = "Farm signpost: Wren the Farmhand at the gate";

        /// <summary>The isle farm's art for a layout piece, or null to leave it out.</summary>
        public static int? IsleArt(int id) => id switch
        {
            OldSignArm => GateSignpost,
            OldSignBoard => null,
            _ => id
        };

        /// <summary>cc-P718: swaps an isle farm's old gate sign for the standing post. Returns the pieces changed.</summary>
        public static int FixGateSign(BaseAddon addon)
        {
            var changed = 0;
            foreach (var c in addon.Components.ToArray())
            {
                if (c.ItemID == OldSignArm)
                {
                    c.ItemID = GateSignpost;
                    c.Name = GateSignpostName;
                    changed++;
                }
                else if (c.ItemID == OldSignBoard)
                {
                    // Detached first: a component deleted while attached deletes its whole addon (AddonComponent.OnAfterDelete).
                    addon.Components.Remove(c);
                    c._addon = null;
                    c.Delete();
                    changed++;
                }
            }

            return changed;
        }

        /// <summary>
        /// The farmhouse door, open or closed. cc-P453: an open door stands at its offset (pinned BaseDoor: +1,+1 for
        /// SouthCW), so it is matched by where it stands closed.
        /// </summary>
        public static BaseDoor? FindDoor(Map map, Point3D doorSpot)
        {
            foreach (var item in map.GetItemsInRange<BaseDoor>(doorSpot, 2))
            {
                var closed = item.Open ? new Point3D(item.X - item.Offset.X, item.Y - item.Offset.Y, item.Z - item.Offset.Z) : item.Location;
                if (!item.Deleted && closed == doorSpot)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>The isle farmhouse's door.</summary>
        public static BaseDoor? FindDoor(Map map) => FindDoor(map, TutorialIsleFarmLayout.DoorSpot);

        public static Spawner? FindSpawner(Map map, Point2D spot, string firstType)
        {
            foreach (var item in map.GetItemsInRange<Spawner>(new Point3D(spot.X, spot.Y, 0), 2))
            {
                if (!item.Deleted && item.Entries.Count > 0 && item.Entries[0].SpawnedName.Equals(firstType, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            return null;
        }
    }

    /// <summary>The Farmhand's offer: a dog or a cat.</summary>
    public class TutorialFarmPetGump : Gump
    {
        public TutorialFarmPetGump() : base(140, 140)
        {
            AddPage(0);
            TutorialStyle.Panel(this, 360, 130);
            AddLabel(24, 18, TutorialStyle.HeadingHue, "Choose your farm pet");
            for (var i = 0; i < TutorialFarm.FarmPetLines.Length; i++)
            {
                AddButton(24, 50 + i * 30, 4005, 4007, 1 + i);
                AddLabel(60, 52 + i * 30, TutorialStyle.ActionHue, $"Choose the {TutorialFarm.FarmPetLines[i]}");
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var i = info.ButtonID - 1;
            if (sender.Mobile is PlayerMobile pm && i >= 0 && i < TutorialFarm.FarmPetLines.Length)
            {
                TutorialFarm.ChoosePet(pm, TutorialFarm.FarmPetLines[i]);
            }
        }
    }
}

namespace Server.Items
{
    using Server.Tutorial;

    /// <summary>The tutorial farm: every piece of the approved v3 layout, anchored on the farmhouse's centre.</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialFarmAddon : BaseAddon
    {
        [Constructible]
        public TutorialFarmAddon()
        {
            var a = TutorialFarmLayout.Anchor;
            foreach (var (id, x, y, z) in TutorialFarmLayout.Pieces)
            {
                AddComponent(new AddonComponent(id), x - a.X, y - a.Y, z - a.Z);
            }
        }

        public override string DefaultName => "the League farm";
    }

    /// <summary>cc-P663: the same farm on the Wakening Isle (TutorialIsleFarmLayout), anchored on the farmhouse's centre.</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialIsleFarmAddon : BaseAddon
    {
        [Constructible]
        public TutorialIsleFarmAddon()
        {
            var a = TutorialIsleFarmLayout.Anchor;
            foreach (var (id, x, y, z) in TutorialIsleFarmLayout.Pieces)
            {
                if (TutorialFarm.IsleArt(id) is not { } art)
                {
                    continue;
                }

                var c = new AddonComponent(art);
                if (art == TutorialFarm.GateSignpost)
                {
                    c.Name = TutorialFarm.GateSignpostName; // cc-P718
                }

                AddComponent(c, x - a.X, y - a.Y, z - a.Z);
            }
        }

        public override string DefaultName => "the League farm";
    }
}

namespace Server.Mobiles
{
    using Server.Items;
    using Server.Tutorial;

    /// <summary>Wren Tallow, at the farm gate: runs the farm's lesson (TutorialFarm.Talk).</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialFarmhand : BaseCreature
    {
        [Constructible]
        public TutorialFarmhand() : base(AIType.AI_Animal, FightMode.None, 10, 1)
        {
            Name = "Wren Tallow";
            Title = "the League farmhand";
            Body = 0x191;
            Female = true;
            Hue = 0x83EA;
            SetStr(80);
            SetDex(80);
            SetInt(80);
            SetHits(150);
            CantWalk = true;

            AddItem(new Shirt { Movable = false, Hue = 0x2C });
            AddItem(new ShortPants { Movable = false, Hue = 0x455 });
            AddItem(new HalfApron { Movable = false, Hue = 0x8A5 });
            AddItem(new Boots { Movable = false });
            AddItem(new StrawHat { Movable = false });
            AddItem(new Pitchfork { Movable = false });
            AddItem(new Item(0x203C) { Movable = false, Hue = 0x2C, Layer = Layer.Hair });
        }

        public override bool IsInvulnerable => true;
        public override bool ClickTitle => true;

        // cc-P480: a dead bonded pet stepping near her (its owner usually close behind) gets the vet line.
        public override void OnMovement(Mobile m, Point3D oldLocation)
        {
            base.OnMovement(m, oldLocation);
            if (m is BaseCreature { IsDeadBondedPet: true } pet && InRange(pet, 4))
            {
                TutorialFarm.NoticeGhostPet(this, pet);
            }
            else if (m is PlayerMobile pm && TutorialGreeting.NeedsWren(pm))
            {
                TutorialGreeting.TryGreet(this, pm, TutorialGreeting.WrenLine); // cc-P718
            }
        }

        public override void OnThink()
        {
            base.OnThink();
            TutorialGreeting.GreetAround(this, TutorialGreeting.NeedsWren, TutorialGreeting.WrenLine); // cc-P718
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from is not PlayerMobile pm)
            {
                return;
            }

            if (!pm.InRange(Location, 4))
            {
                pm.SendMessage("Move closer, then double-click Wren the Farmhand again."); // cc-P609: was cliloc 500446
                return;
            }

            TutorialFarm.Talk(pm, this);
        }

        public static TutorialFarmhand? Find(Map? map = null)
        {
            foreach (var m in (map ?? TutorialWorld.Isle).GetMobilesInRange(new Point3D(TutorialFarm.FarmhandSpot, 0), 10))
            {
                if (m is TutorialFarmhand h && !h.Deleted)
                {
                    return h;
                }
            }

            return null;
        }
    }

    /// <summary>A farm pest: a tutorial-only creature (it engages only participants) that counts for the farm's step 2.</summary>
    [SerializationGenerator(0, false)]
    public abstract partial class FarmPest : TutorialCreature
    {
        protected FarmPest()
        {
        }

        // cc-P718 item 7: a pest fights only a trainee on the pest part of the farm lesson, or that trainee's pet; anyone
        // earlier in the lessons is never attacked, and a blow from them does nothing.
        public static bool Fights(Mobile? m)
        {
            var player = m is BaseCreature { Controlled: true } pet ? pet.ControlMaster : m;
            return player is PlayerMobile pm && TutorialFarm.PestPartOn(pm);
        }

        public override bool IsEnemy(Mobile m) => Fights(m) && base.IsEnemy(m);

        public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness) =>
            Fights(target) && base.CanBeHarmful(target, message, ignoreOurBlessedness);

        public override void Damage(int amount, Mobile? from = null, bool informMount = true, bool ignoreEvilOmen = false)
        {
            if (from != null && !Fights(from))
            {
                return;
            }

            base.Damage(amount, from, informMount, ignoreEvilOmen);
        }

        // ...and never steps outside the field fence, even through an open gate.
        public override bool CheckMovement(Direction d, out int newZ)
        {
            int x = X, y = Y;
            Movement.Movement.Offset(d, ref x, ref y);
            if (!TutorialFarm.PestField.Contains(new Point2D(x, y)) && FoldMaps.IsFold(Map))
            {
                newZ = Z;
                return false;
            }

            return base.CheckMovement(d, out newZ);
        }

        public override void OnDeath(Container c)
        {
            base.OnDeath(c);
            TutorialFarm.OnPestDeath(this);
        }
    }

    [SerializationGenerator(0, false)]
    public partial class FarmRat : FarmPest
    {
        [Constructible]
        public FarmRat()
        {
            Name = "a farm rat";
            Body = 0xEE;
            BaseSoundID = 0xCC;
            SetStr(15);
            SetDex(30);
            SetInt(5);
            SetHits(10);
            SetDamage(1, 2);
            SetSkill(SkillName.Wrestling, 15.0);
            SetSkill(SkillName.Tactics, 15.0);
        }

        public override string CorpseName => "a rat corpse";
    }

    [SerializationGenerator(0, false)]
    public partial class FieldRat : FarmPest
    {
        [Constructible]
        public FieldRat()
        {
            Name = "a field rat";
            Body = 0xD7;
            BaseSoundID = 0x188;
            SetStr(25);
            SetDex(35);
            SetInt(8);
            SetHits(16);
            SetDamage(2, 3);
            SetSkill(SkillName.Wrestling, 20.0);
            SetSkill(SkillName.Tactics, 20.0);
        }

        public override string CorpseName => "a rat corpse";
    }

    [SerializationGenerator(0, false)]
    public partial class GrassSnake : FarmPest
    {
        [Constructible]
        public GrassSnake()
        {
            Name = "a grass snake";
            Body = 0x34;
            BaseSoundID = 0xDB;
            Hue = 0x59F;
            SetStr(20);
            SetDex(30);
            SetInt(5);
            SetHits(14);
            SetDamage(1, 3);
            SetSkill(SkillName.Wrestling, 20.0);
            SetSkill(SkillName.Tactics, 20.0);
        }

        public override string CorpseName => "a snake corpse";
    }

    /// <summary>
    /// cc-P718 item 7: the farm field's gate. A light wood gate that a trainee opens only on the pest part of the farm
    /// lesson (or later), and staff always; Wren opens it when that part begins. It closes by itself, as any gate.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialPestGate : LightWoodGate
    {
        [Constructible]
        public TutorialPestGate(DoorFacing facing) : base(facing) => Name = "farm field gate";

        public override void Use(Mobile from)
        {
            if (from.AccessLevel == AccessLevel.Player && !Open &&
                !(from is PlayerMobile pm && (TutorialFarm.PestPartOn(pm) || TutorialFarm.StepOf(pm) >= 3)))
            {
                from.SendMessage(0x3B2, TutorialFarm.GateClosedLine);
                return;
            }

            base.Use(from);
        }
    }
}
