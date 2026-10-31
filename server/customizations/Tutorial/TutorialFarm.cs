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
using System.Linq;
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

// cc-P721 (Chase 10-28 09:16 to 09:31): the heal step has a patient who stays hurt, Pip the farm boy (his hits never
// come back by themselves, and he is cut again after each heal), Wren says that a bandage takes a few seconds, and on the
// isle a trainee's bandage says how long to wait, shows a cue over their head, says plainly when it fails, and takes 4 s
// on themselves (Bandage-tutorial-wait-lines.patch). Before the pests, Wren hands a pitchfork (kept, like the cloak)
// and waits until it is held, then until war mode is on (Tab, or the Peace/War button); the pest part also wants one
// pest struck by the trainee themselves.

// cc-P728 (Chase 10-28 15:25 to 15:27): the farm's combat part starts with the weapon. Once the pet is chosen, Wren hands
// the pitchfork (hold it: drag it to the hand on the character window, or double-click it), then war mode, then the pet's
// commands, then the gate opens on the pests (BeginCombat, CheckCommands). P726 moves the pet and its commands after the
// pests; this order lets it do that by moving them alone. Wren never says "Defeat 0 more": with the count reached she says
// what is still missing (the pet's kill, the trainee's own strike). When the rows are clear she says so, and the next step
// ("Go back to Wren...") is in her overhead line, over the trainee's head, in the system line and at the top of the lesson
// window (reopened), with the arrow back on her (TutorialCoreTrials.NowStep).

// cc-P726 (Chase 10-28 12:22 to 12:25): the pet is the reward. The farm's order is now: the pitchfork in hand, war mode,
// the pests (step 1); then Wren thanks the trainee with the pet, a dog or a cat (step 2); then its commands (step 3); done
// (step 4). Bandaging left the farm: it is its own core lesson after it, core.healing, with Pip on a bench inside the
// farmhouse (BeginHealing, OnHealed). The pests no longer wait for a pet's kill (there is no pet yet), only for one
// struck by the trainee. An old record keeps its progress: pests done and the pet claimed with its commands is step 4,
// and the old heal key counts for core.healing (ClusterFTutorial.IsDone).

// cc-P740 (Chase 10-30: "might need a hint of speech from Wren to talk again to get bandages.... possibly have bandages on
// table in farm to pick up and use on the healing npc"): both. A small stack of bandages lies on the farmhouse table beside
// Pip (TutorialTableBandages): double-click it, or try to pick it up, and a trainee on the healing lesson with none in
// their backpack gets TableBandagesGiven of their own (it stays on the table for the next; TableTakes a character). Wren,
// double-clicked during the healing lesson, hands bandages again (the farm's GiveBandages) or says where they are, and
// while the trainee has none she says so over her head (HintBandages, every 30 s, HintTimes in a row). Either way a bandage
// on Pip finishes the lesson. Notes: shard-migration notes/cc-P740-isle-wake-visible.md.

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
        public const string PetOfferLine = "Thank you! The rows are safe again. As thanks, take a pet of your own: choose a dog or a cat in the window. It grows as it fights beside you, and it is yours to keep after the isle.";
        public const string NoSlotLine = "You cannot control another pet right now. Stable one of your pets with an animal trainer, then come back.";
        public const string PestsLine = "Walk into the cabbage rows, double-click a rat or grass snake to attack it yourself, then say 'all kill' and click one for your pet. Defeat {0} pests to finish this step.";
        public const string PestsNoPetLine = "Walk into the cabbage rows and double-click a rat or grass snake to attack it. Defeat {0} pests to finish this step.";
        public const string PestsLeftLine = "Defeat {0} more rats or grass snakes in the cabbage rows.";
        public const string HealLine = "Here are some bandages. To heal, double-click a bandage in your backpack, then click yourself or your hurt pet. A successful heal finishes this step.";
        // cc-P721: the heal step's patient, and how long a bandage takes.
        public const string PipLine = "Here are some bandages. Pip, the farm boy, cut his arm on the fence and sits hurt on the bench inside the farmhouse. Double-click a bandage in your backpack, then click Pip.";
        public const string WaitLine = "A bandage takes a few seconds. You will see 'You begin applying the bandages': stand still and wait until 'You finish applying the bandages'. A bandage that heals finishes this step.";
        public const string PipNotYouLine = "You are not hurt. Pip is the one who needs it: double-click a bandage, then click Pip on the bench in the farmhouse.";
        public const string FumbleLine = "You fumble the bandage. Try again: double-click a bandage, then click your patient.";
        public const double IsleSelfBandageSeconds = 4.0;
        // cc-P718 item 9: the one line on making more, said after the bandages are handed over.
        public const string MakeBandagesLine = "Later you can make your own bandages: double-click a pair of scissors, then click a pile of cloth to cut it into bandages.";

        // cc-P740 item 4: more bandages for the healing lesson, from Wren or from the farmhouse table.
        public const int TableBandagesGiven = 5;
        public const int TableTakes = 5;
        public const int HintTimes = 3;
        public const string WrenBandageHintLine = "No bandages? Double-click me for more, or take some from the table beside Pip in the farmhouse.";
        public const string WrenMoreBandagesLine = "Here are more bandages. Pip is on the bench in the farmhouse: double-click a bandage, then click him.";
        public const string WrenHasBandagesLine = "You have bandages in your backpack. Double-click one, then click Pip on the bench in the farmhouse.";
        public const string WrenTableLine = "I have none left to spare, but there are bandages on the table beside Pip in the farmhouse. Double-click them.";
        public const string TableTakenLine = "You take some bandages from the table. Double-click one, then click Pip.";
        public const string TableHasSomeLine = "You already have bandages in your backpack. Double-click one, then click Pip.";
        public const string TableNotYoursLine = "These bandages are for the healing lesson. They stay on the table.";
        public const string TableNoMoreLine = "You have taken all the bandages you need from this table.";

        // cc-P718 item 10: the pet commands, the first part of step 2.
        public const string FollowKey = "farm.cmd.follow";
        public const string GuardKey = "farm.cmd.guard";
        public const string CommandsLine = "Your pet obeys spoken commands. I told it to stay. Say 'all follow me' and it follows you; then say 'all guard me' and it guards you. You can say your pet's name instead of 'all', or click your pet with the right mouse button and choose a command from its menu.";
        public const string FollowDoneLine = "Your pet follows you. Now say 'all guard me', or choose Guard from your pet's menu.";
        public const string GuardDoneLine = "Your pet guards you. Those are its commands: say 'all kill' and click a foe to send it to fight, and 'all follow me' to call it back.";
        public const string NoPetForCommandsLine = "You have no pet with you, so its commands wait for another time.";
        // cc-P726 item 2: how to see the pet's progress, said when it is given.
        public const string PetInfoLine = "To see how your pet is doing, click it with the right mouse button and choose Pet Info, or type [PetInfo and click your pet.";

        // cc-P728 item 4: the weapon comes first in the combat part.
        public const string PitchforkLine = "Before the pests, a weapon: I put a pitchfork in your backpack. Drag it onto your character's hand in your character window, or double-click it, to hold it. If you already hold a weapon, drag that into your backpack first.";
        public const string PitchforkNoRoomLine = "Your backpack is too full for the pitchfork. Make room, then double-click me again.";
        // cc-P721: the weapon and war mode, before the first fight.
        public const string WeaponKey = "farm.weapon";
        public const string WarKey = "farm.war";
        public const string WeaponDoneLine = "You hold the pitchfork. Now press Tab, or click the Peace/War button on your character window, to switch to war mode: that is how you fight. Press Tab again for peace mode when you are done.";
        public const string WarDoneLine = "You are in war mode: that is how you fight. Press Tab again for peace mode when you are done. I opened the field gate for you: walk into the cabbage rows and double-click a rat or grass snake to attack it. Defeat {0} pests. If the gate swings shut, double-click it to open it.";
        public const string StrikeLine = "Now strike one yourself: in war mode, double-click a rat or grass snake.";
        public const string GateClosedLine = "Wren keeps this gate shut until your pest lesson. Double-click Wren the Farmhand first.";

        /// <summary>
        /// cc-P718 item 7: inside the field fence (P276's x 3381-3399, y 2603-2619, moved to the isle), where the pests stay.
        /// End points exclusive (Rectangle2D): x 6678-6694, y 3968-3982 (cc-P741: 18 west of P720's).
        /// </summary>
        public static readonly Rectangle2D PestField = new(OnIsle(new Point2D(3382, 2604)), OnIsle(new Point2D(3399, 2619)));

        /// <summary>
        /// cc-P718: the field's gate, in the two-tile gap of its east fence (6695, 3978-3979 since cc-P741; the layout leaves
        /// it open).
        /// Every wooden gate's art is impassable open or shut, and pinned swings a gate in a north-south fence east by one
        /// tile (BaseDoor offsets), so two halves would stand on both tiles outside the gap (Wren's and the one below it)
        /// and shut the field: the P718 bot run could not get in. So one gate, on the gap's south tile, swinging north-east
        /// onto Wren's own tile (NorthCCW, offset +1,-1), and a fence piece closing the gap's north tile. A player walks in
        /// from the tile below Wren.
        /// </summary>
        public static readonly Point2D[] FieldGateSpots = [new(6695, 3979)];

        public static readonly Point2D FieldFenceSpot = new(6695, 3978);

        /// <summary>
        /// cc-P741 (Chase 10-30: "farm house should be east of farm and farm shifts west so no doubling back"): the field
        /// moved 18 tiles west and the farmhouse 12 east. These are where their pieces stood before (P720/P726), which the
        /// seeder clears from a world laid then: the gate and the fence piece by it, Pip's bench and blood, the pest and
        /// pen spawners (with what they spawned).
        /// </summary>
        public const int FieldMovedX = -18;
        public const int HouseMovedX = 12;
        public static readonly Point2D OldFieldGateSpot = new(6713, 3979);
        public static readonly Point2D OldFieldFenceSpot = new(6713, 3978);
        public static readonly Point2D OldPestSpot = new(PestSpot.X - FieldMovedX, PestSpot.Y);
        public static readonly Point2D OldPenSpot = new(PenSpot.X - FieldMovedX, PenSpot.Y);
        public const int FieldFenceId = 0x0837; // the layout's own wooden fence, north-south
        // cc-P707: the pests die to the pet before they bite, so nobody is hurt and a bandage only says "That being is not
        // damaged!". Wren gives an unhurt trainee a scratch to practise on.
        public const string ScratchLine = "A rat nipped your ankle in the rows. Double-click a bandage in your backpack, then click yourself to treat it. A successful heal finishes this step.";
        // cc-P707 run 4: said when a bandage on the heal step finds nobody hurt (the trainee's hits came back by themselves).
        public const string ScratchAgainLine = "A rat nipped your ankle in the rows. Double-click a bandage again, then click yourself to treat it.";
        public const string NoMoreBandagesLine = "I have no more free bandages for you. You can buy more from a healer in town.";
        public const string NoRoomForBandagesLine = "Your backpack is too full for these bandages. Make room in it, then double-click me again.";
        public const string DoneLine = "You finished the farm lesson. If your bonded pet dies, bring its ghost to Tobin the vet beside me. While you are Young, he brings it back free. Pip, in the farmhouse, could use your help next.";
        public const string EchoPetLine = "This pet is yours to keep.";
        public const string EchoPestsLine = "The cabbage rows are clear! Go back to Wren at the farm gate and double-click her.";
        public const string EchoHealLine = "Your bandage restored health and finished the healing lesson.";

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
            SureHeal = (healer, _) => healer is PlayerMobile pm && HealingOn(pm); // cc-P707; cc-P726: the healing lesson
            Unhurt = OnUnhurt; // cc-P707 run 4
            BandageSeconds = IsleSeconds; // cc-P721
            // cc-P721: pinned's Trammel rules refuse a player's beneficial act on an uncontrolled creature ("Players
            // cannot heal uncontrolled mobiles", Notoriety.Mobile_AllowBeneficial), the isle uses Trammel's rules, and
            // Pip is one: the bandage was refused (the P721 bot run). Pip alone is let through; everything else is pinned's.
            var inner = Mobile.AllowBeneficialHandler;
            Mobile.AllowBeneficialHandler = (from, target) => AllowBeneficial(from, target, inner);
            BandageBegun = OnBandageBegun;
            BandageFumbled = OnBandageFumbled;
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

        /// <summary>
        /// cc-P726: the step a participant is on: 1 the fight (pitchfork, war mode, pests), 2 the pet as the reward, 3 its
        /// commands, 4 done; 0 when not a participant. An account that already has its tutorial pet goes from 1 to 3.
        /// </summary>
        public static int StepOf(PlayerMobile pm) =>
            !ClusterFTutorial.IsParticipant(pm) ? 0 :
            !Has(pm, PestsKey) ? 1 :
            RecordOf(pm)?.PetClaimed != true ? 2 :
            !CommandsDone(pm) ? 3 : 4;

        /// <summary>cc-P726: Wren has started the fight (the pitchfork handed or held): her greeting stops.</summary>
        public static bool CombatStarted(PlayerMobile pm) =>
            StepOf(pm) != 1 || Has(pm, WeaponKey) || HoldsPitchfork(pm) || pm.Backpack?.FindItemByType<TutorialPitchfork>() != null;

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
            TutorialCoreTrials.ClearNow(pm); // cc-P728: Wren gives the next step herself

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
                case 1 when !ArmedDone(pm):
                    hand.SayTo(pm, BeginCombat(pm));
                    break;
                case 1:
                    OpenFieldGate(hand.Map);
                    hand.SayTo(pm, PestsNow(pm));
                    break;
                case 2:
                    hand.SayTo(pm, PetOfferLine); // cc-P726: the pet, as thanks for the cleared rows
                    pm.CloseGump<TutorialFarmPetGump>();
                    pm.SendGump(new TutorialFarmPetGump());
                    break;
                case 3:
                    hand.SayTo(pm, BeginCommands(pm));
                    break;
                default:
                    CheckDone(pm);
                    hand.SayTo(pm, HealingOn(pm) ? WrenBandages(pm) : DoneLine); // cc-P740 item 4
                    break;
            }

            return step;
        }

        // -- cc-P740 item 4: bandages again, from Wren or the farmhouse table --

        // How many times each character took from the table, and how many hints Wren gave in a row (not saved).
        private static readonly Dictionary<Serial, int> _tableTaken = new();
        private static readonly Dictionary<Serial, int> _hinted = new();

        public static bool HasBandages(PlayerMobile pm) => pm.Backpack?.FindItemByType<Bandage>() != null;

        /// <summary>Wren, double-clicked during the healing lesson: bandages again (or where to find them), and Pip.</summary>
        public static string WrenBandages(PlayerMobile pm)
        {
            var gift = GiveBandages(pm);
            if (gift is BandageGift.Given or BandageGift.HasSome)
            {
                FindPip(pm.Map)?.PointTo(pm);
            }

            return gift switch
            {
                BandageGift.Given => WrenMoreBandagesLine,
                BandageGift.HasSome => WrenHasBandagesLine,
                BandageGift.NoRoom => NoRoomForBandagesLine,
                _ => WrenTableLine
            };
        }

        /// <summary>
        /// The healing lesson's tick (every 30 s): while the trainee has no bandages, Wren says where to get them, over her
        /// head (HintTimes in a row; again once they had some and ran out). True when she said it.
        /// </summary>
        public static bool HintBandages(PlayerMobile pm)
        {
            if (!HealingOn(pm) || HasBandages(pm))
            {
                _hinted.Remove(pm.Serial);
                return false;
            }

            var said = _hinted.GetValueOrDefault(pm.Serial);
            if (said >= HintTimes || TutorialFarmhand.Find(TutorialWorld.MapOf(pm)) is not { } wren)
            {
                return false;
            }

            _hinted[pm.Serial] = said + 1;
            wren.SayTo(pm, WrenBandageHintLine);
            return true;
        }

        /// <summary>
        /// The farmhouse table's bandages, double-clicked or lifted: a stack of the trainee's own when the healing lesson is
        /// theirs and their backpack has none (TableTakes a character). The table's stack stays. True when given.
        /// </summary>
        public static bool TakeTableBandages(PlayerMobile pm)
        {
            if (!HealingOn(pm))
            {
                pm.SendMessage(0x3B2, TableNotYoursLine);
                return false;
            }

            if (HasBandages(pm))
            {
                pm.SendMessage(0x3B2, TableHasSomeLine);
                return false;
            }

            var taken = _tableTaken.GetValueOrDefault(pm.Serial);
            if (taken >= TableTakes)
            {
                pm.SendMessage(0x3B2, TableNoMoreLine);
                return false;
            }

            var stack = new Bandage(TableBandagesGiven);
            if (!pm.PlaceInBackpack(stack))
            {
                stack.Delete();
                pm.SendMessage(0x3B2, NoRoomForBandagesLine);
                return false;
            }

            _tableTaken[pm.Serial] = taken + 1;
            _hinted.Remove(pm.Serial);
            pm.SendMessage(0x3B2, TableTakenLine);
            FindPip(pm.Map)?.PointTo(pm);
            return true;
        }

        /// <summary>
        /// cc-P740: the farmhouse table (TutorialIsleFarmLayout's 0x0B90 at 6702,3975, Z 7, 6 high), beside Pip. cc-P746:
        /// 12 east of P740's spot, with the farmhouse (cc-P741); MoveFromOldLayout carries a table laid there across.
        /// </summary>
        public static readonly Point3D TableBandagesSpot = new(6702, 3975, 13);

        /// <summary>cc-P746: where cc-P740 laid the table, on the farmhouse table before the farmhouse moved (cc-P741).</summary>
        public static readonly Point3D OldTableBandagesSpot = new(6690, 3975, 13);

        public static TutorialTableBandages? FindTableBandages(Map? map)
        {
            if (map == null)
            {
                return null;
            }

            foreach (var item in map.GetItemsInRange<TutorialTableBandages>(TableBandagesSpot, 2))
            {
                if (!item.Deleted)
                {
                    return item;
                }
            }

            return null;
        }

        /// <summary>Step 1: the pet. Null when refused (not on this step, not a farm line, or no follower slot).</summary>
        public static EvoPet? ChoosePet(PlayerMobile pm, string line)
        {
            if (StepOf(pm) != 2 || Array.IndexOf(FarmPetLines, line) < 0)
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
            ClusterFTutorial.SystemLine(pm, PetInfoLine); // cc-P726 item 2
            if (!CheckDone(pm) && StepOf(pm) == 3)
            {
                ClusterFTutorial.SystemLine(pm, BeginCommands(pm)); // cc-P726: the commands follow the pet
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
        public static void CountPest(PlayerMobile pm)
        {
            NoteStruck(pm); // staff and tests credit the trainee's own strike too (cc-P721)
            CountPest(pm, true);
        }

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

            // cc-P721: and the trainee has attacked one themselves.
            if (!_struck.Contains(pm.Serial))
            {
                ClusterFTutorial.SystemLine(pm, StrikeLine);
                return;
            }

            _pestsKilled.Remove(pm.Serial);
            _petKilled.Remove(pm.Serial);
            _struck.Remove(pm.Serial);
            RecordOf(pm)!.StationsDone.Add(PestsKey);
            if (!CheckDone(pm))
            {
                Cleared(pm);
            }
        }

        /// <summary>
        /// cc-P728 item 6: the rows are clear. Said plainly with the next step, by Wren over her head, over the trainee's, in
        /// the system line and at the top of the lesson window (reopened if closed), and the arrow turns back to her.
        /// </summary>
        public static void Cleared(PlayerMobile pm)
        {
            var map = TutorialWorld.MapOf(pm);
            var hand = TutorialFarmhand.Find(map);
            hand?.SayTo(pm, EchoPestsLine);
            TutorialCoreTrials.NowStep(pm, EchoPestsLine, hand?.Location ?? TutorialWorld.Ground(map ?? TutorialWorld.Isle, FarmhandSpot), "Wren the Farmhand");
        }

        /// <summary>
        /// cc-P728 item 6: what Wren says on the pest part: the pests to go, or with the count reached, what is still missing
        /// (never "Defeat 0 more").
        /// </summary>
        public static string PestsNow(PlayerMobile pm)
        {
            var left = PestsToClear - PestsKilled(pm);
            if (PestsKilled(pm) == 0)
            {
                return string.Format(PestsNoPetLine, PestsToClear); // cc-P726: no pet yet
            }

            if (left > 0)
            {
                return string.Format(PestsLeftLine, left);
            }

            return StrikeLine;
        }

        /// <summary>cc-P663: all three steps done (StepOf 4) finishes core.farm. True when they are.</summary>
        public static bool CheckDone(PlayerMobile pm)
        {
            if (StepOf(pm) != 4)
            {
                return false;
            }

            ClusterFTutorial.CompleteStation(pm, TamingStation); // cc-P726: the pet and its commands

            TutorialCoreTrials.FarmFinished(pm);
            ClusterFTutorial.SystemLine(pm, TutorialPetCare.FarmPetLessonLine); // cc-P666: the farm's pet lesson line
            return true;
        }

        // -- cc-P718 item 10: the pet commands --

        /// <summary>cc-P726: the pet's commands, follow and guard (step 3).</summary>
        public static bool CommandsDone(PlayerMobile pm) => Has(pm, FollowKey) && Has(pm, GuardKey);

        /// <summary>cc-P721: the weapon in hand and war mode: what the pest part waits for.</summary>
        public static bool ArmedDone(PlayerMobile pm) => Has(pm, WeaponKey) && Has(pm, WarKey);

        /// <summary>cc-P718: the pest part of step 1, once armed: the pests fight the trainee only now.</summary>
        public static bool PestPartOn(PlayerMobile pm) => StepOf(pm) == 1 && ArmedDone(pm);

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
        /// cc-P728 item 4: starts (or picks up) the combat part, in order: the pitchfork in hand, war mode, then the pet's
        /// commands. Hands the pitchfork when it is the step, watches the trainee, and returns Wren's line for where they are.
        /// </summary>
        public static string BeginCombat(PlayerMobile pm)
        {
            Watch(pm);
            if (!Has(pm, WeaponKey))
            {
                return GivePitchfork(pm) != null || HoldsPitchfork(pm) || pm.Backpack?.FindItemByType<TutorialPitchfork>() != null
                    ? PitchforkLine
                    : PitchforkNoRoomLine;
            }

            return !Has(pm, WarKey) ? WeaponDoneLine : PestsNow(pm);
        }

        /// <summary>
        /// cc-P726: step 3, the commands of the pet just given: the pets stay, and their orders are watched until they
        /// follow and then guard. With no pet along they wait for another time, and the farm is done. Wren's line.
        /// </summary>
        public static string BeginCommands(PlayerMobile pm)
        {
            var pets = PetsOf(pm);
            if (pets.Count == 0)
            {
                var record = RecordOf(pm)!;
                record.StationsDone.Add(FollowKey);
                record.StationsDone.Add(GuardKey);
                CheckDone(pm);
                return NoPetForCommandsLine;
            }

            Watch(pm);
            if (Has(pm, FollowKey))
            {
                return FollowDoneLine;
            }

            StayPets(pets);
            return CommandsLine;
        }

        private static void StayPets(List<BaseCreature> pets)
        {
            foreach (var pet in pets)
            {
                pet.ControlTarget = null;
                pet.ControlOrder = OrderType.Stay;
            }
        }

        private static void Watch(PlayerMobile pm)
        {
            _watching.Add(pm);
            if (!_watchTimer.Running)
            {
                Timer.StartTimer(TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500), WatchAll, out _watchTimer);
            }
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

        /// <summary>
        /// cc-P728 item 4: the combat part, one step per call, in order: the pitchfork in hand, war mode, the pet following,
        /// the pet guarding (its orders come from speech or its menu; both set them). With no pet along at war mode the
        /// commands are skipped. Then the field gate opens. True when all are done.
        /// </summary>
        public static bool CheckCommands(PlayerMobile pm)
        {
            var step = StepOf(pm);
            if (pm.Deleted || step is not (1 or 3) || step == 1 && ArmedDone(pm))
            {
                _watching.Remove(pm);
                return step == 1 ? ArmedDone(pm) : step == 4;
            }

            var record = RecordOf(pm)!;
            if (step == 1)
            {
                if (!Has(pm, WeaponKey) && HoldsPitchfork(pm))
                {
                    record.StationsDone.Add(WeaponKey);
                    ClusterFTutorial.SystemLine(pm, WeaponDoneLine);
                }
                else if (Has(pm, WeaponKey) && pm.Warmode)
                {
                    record.StationsDone.Add(WarKey);
                    return OpenPests(pm, WarDoneLine);
                }

                return false;
            }

            var pets = PetsOf(pm);
            if (!Has(pm, FollowKey))
            {
                if (pets.Exists(p => p.ControlOrder == OrderType.Follow && p.ControlTarget == pm))
                {
                    record.StationsDone.Add(FollowKey);
                    ClusterFTutorial.SystemLine(pm, FollowDoneLine);
                }

                return false;
            }

            if (!pets.Exists(p => p.ControlOrder == OrderType.Guard))
            {
                return false;
            }

            record.StationsDone.Add(GuardKey);
            _watching.Remove(pm);
            ClusterFTutorial.SystemLine(pm, GuardDoneLine);
            CheckDone(pm);
            return true;
        }

        private static bool OpenPests(PlayerMobile pm, string line)
        {
            _watching.Remove(pm);
            OpenFieldGate(pm.Map);
            ClusterFTutorial.SystemLine(pm, string.Format(line, PestsToClear));
            return true;
        }

        // -- cc-P721: the pitchfork --

        /// <summary>The trainee's own pitchfork in hand (either hand layer).</summary>
        public static bool HoldsPitchfork(PlayerMobile pm) =>
            pm.FindItemOnLayer(Layer.TwoHanded) is TutorialPitchfork || pm.FindItemOnLayer(Layer.OneHanded) is TutorialPitchfork;

        /// <summary>Once per character: a pitchfork in the backpack, unless they have theirs. Null when none was given.</summary>
        public static TutorialPitchfork? GivePitchfork(PlayerMobile pm)
        {
            if (HoldsPitchfork(pm) || pm.Backpack?.FindItemByType<TutorialPitchfork>() != null)
            {
                return null;
            }

            var fork = new TutorialPitchfork(pm);
            if (!pm.PlaceInBackpack(fork))
            {
                fork.Delete();
                return null; // BeginCombat says so (PitchforkNoRoomLine)
            }

            return fork;
        }

        // Trainees who have struck a pest themselves this run (in memory, like the count).
        private static readonly HashSet<Serial> _struck = new();

        public static void NoteStruck(PlayerMobile pm) => _struck.Add(pm.Serial);

        public static bool HasStruck(PlayerMobile pm) => _struck.Contains(pm.Serial);

        // -- cc-P721: the heal step's patient and the bandage on the isle --

        /// <summary>Pip, the hurt farm boy beside Wren, on <paramref name="map"/>, or null.</summary>
        public static TutorialWoundedHelper? FindPip(Map? map)
        {
            if (map == null)
            {
                return null;
            }

            // cc-P726: on his bench in the farmhouse, or (a world seeded before) by the barrel at the gate.
            foreach (var at in new[] { PipBench, OldPipBench, TutorialWorld.Ground(map, OldPipSpot) })
            {
                foreach (var m in map.GetMobilesInRange(at, 3))
                {
                    if (m is TutorialWoundedHelper pip && !pip.Deleted)
                    {
                        return pip;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// cc-P726 item 4 (Chase 10-28 12:25, 15:28): Pip sits hurt on a bench inside the farmhouse, against its east wall
        /// between the bed and the box, on the floor boards (Z 7, TutorialIsleFarmLayout), blood on the floor around him.
        /// The client draws a person on a bench tile seated.
        /// </summary>
        public static readonly Point2D PipSpot = new(6704, 3975);
        public static readonly Point3D PipBench = new(6704, 3975, 7);
        public static readonly Point2D OldPipSpot = new(6715, 3981); // cc-P721's, by the barrel south of the gate
        public static readonly Point3D OldPipBench = new(6692, 3975, 7); // cc-P726's, before the farmhouse moved (cc-P741)

        public const int BenchId = 0x0B2D; // "wooden bench", the north-south piece along the east wall

        /// <summary>The bench's two tiles and the blood decals (stock "blood" art 0x122A-0x122E) on the floor beside it.</summary>
        public static readonly (int Id, Point3D At, string Name)[] PipProps =
        [
            (BenchId, new Point3D(6704, 3975, 7), "wooden bench"),
            (BenchId, new Point3D(6704, 3976, 7), "wooden bench"),
            (0x122A, new Point3D(6703, 3975, 7), "blood"),
            (0x122D, new Point3D(6703, 3976, 7), "blood"),
            (0x122C, new Point3D(6703, 3974, 7), "blood")
        ];

        /// <summary>cc-P741: Pip's props where they stood before the farmhouse moved (cc-P726's), removed by the seeder.</summary>
        public static readonly (int Id, Point3D At, string Name)[] OldPipProps =
            PipProps.Select(p => (p.Id, new Point3D(p.At.X - HouseMovedX, p.At.Y, p.At.Z), p.Name)).ToArray();

        /// <summary>cc-P721: Pip may be bandaged; every other beneficial act is <paramref name="inner"/>'s (pinned's) to judge.</summary>
        public static bool AllowBeneficial(Mobile from, Mobile target, AllowBeneficialHandler? inner) =>
            target is TutorialWoundedHelper || (inner?.Invoke(from, target) ?? true);

        /// <summary>A trainee on the Wakening Isle (or a copy of it).</summary>
        public static bool IsleTrainee(Mobile m) => m is PlayerMobile pm && ClusterFTutorial.IsParticipant(pm) && TutorialWorld.IsOnIsle(pm);

        /// <summary>Asked by Bandage-tutorial-wait-lines.patch for the delay (healer, patient, pinned's seconds).</summary>
        public static Func<Mobile, Mobile, double, double>? BandageSeconds { get; set; }

        /// <summary>Raised by the patch as a bandage begins (healer, patient, seconds).</summary>
        public static Action<Mobile, Mobile, double>? BandageBegun { get; set; }

        /// <summary>Raised by the patch when the healing roll fails (healer, patient).</summary>
        public static Action<Mobile, Mobile>? BandageFumbled { get; set; }

        public static double IsleSeconds(Mobile healer, Mobile patient, double seconds) =>
            healer == patient && IsleTrainee(healer) ? Math.Min(seconds, IsleSelfBandageSeconds) : seconds;

        public static void OnBandageBegun(Mobile healer, Mobile patient, double seconds)
        {
            if (healer is not PlayerMobile pm || !IsleTrainee(pm))
            {
                return;
            }

            pm.PrivateOverheadMessage(MessageType.Emote, 0x3B2, false, "*bandaging*", pm.NetState);
            ClusterFTutorial.SystemLine(pm, $"This bandage takes about {Math.Ceiling(seconds)} seconds. Stand still until you see 'You finish applying the bandages'.");
        }

        public static void OnBandageFumbled(Mobile healer, Mobile patient)
        {
            if (healer is PlayerMobile pm && IsleTrainee(pm))
            {
                ClusterFTutorial.SystemLine(pm, FumbleLine);
            }
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

            (patient as TutorialWoundedHelper)?.WoundAgainSoon(); // cc-P721
        }

        // Step 3, a bandage on an unhurt patient: the trainee or one of their pets. Scratch refuses while anyone is hurt.
        public static bool OnUnhurt(Mobile healer, Mobile patient)
        {
            if (healer is not PlayerMobile pm || !HealingOn(pm) ||
                patient != pm && (patient as BaseCreature)?.ControlMaster != pm)
            {
                return false;
            }

            // cc-P721: with Pip there, the trainee is sent to him; a scratch of their own would heal by itself.
            if (FindPip(pm.Map) is { } pip)
            {
                pm.SendMessage(PipNotYouLine);
                pip.PointTo(pm);
                return true;
            }

            if (!Scratch(pm))
            {
                return false;
            }

            pm.SendMessage(ScratchAgainLine);
            return true;
        }

        public static bool OnHealed(PlayerMobile pm)
        {
            if (!HealingOn(pm))
            {
                return false;
            }

            RecordOf(pm)!.StationsDone.Add(HealKey);
            ClusterFTutorial.SystemLine(pm, EchoHealLine);
            ClusterFTutorial.CompleteStation(pm, HealingStation);
            TutorialCoreTrials.HealingFinished(pm);
            return true;
        }

        // -- cc-P726 item 4: the healing lesson, core.healing, after the farm --

        public const string HealingStationKey = "core.healing";

        /// <summary>The healing lesson is the trainee's to do: the farm done, the heal not yet.</summary>
        public static bool HealingOn(PlayerMobile pm) =>
            ClusterFTutorial.IsParticipant(pm) && !Has(pm, HealKey) &&
            (TutorialCoreTrials.IsDoing(pm, HealingStationKey) || RecordOf(pm) is { } r && ClusterFTutorial.IsDone(r, CoreStation));

        /// <summary>
        /// The healing lesson starts: bandages (a stack when the pack has none), how to use them and how long they take,
        /// and the arrow to Pip on his bench. With no Pip on the map, the trainee's own scratch is the patient.
        /// </summary>
        public static BandageGift BeginHealing(PlayerMobile pm)
        {
            var gift = GiveBandages(pm);
            var pip = FindPip(TutorialWorld.MapOf(pm));
            ClusterFTutorial.SystemLine(pm, gift switch
            {
                BandageGift.NoneLeft => NoMoreBandagesLine,
                BandageGift.NoRoom => NoRoomForBandagesLine,
                _ when pip != null => PipLine,
                _ => Scratch(pm) ? ScratchLine : HealLine
            });
            if (gift is BandageGift.Given or BandageGift.HasSome)
            {
                ClusterFTutorial.SystemLine(pm, WaitLine); // cc-P721
                ClusterFTutorial.SystemLine(pm, MakeBandagesLine); // cc-P718 item 9
                pip?.PointTo(pm);
            }

            return gift;
        }

        // -- The seeder --

        public readonly record struct SeedResult(bool Refused, int Addons, int Doors, int Farmhands, int Spawners, int Present, string Message);

        private static bool HasProp(Map map, int id, Point3D at, string name)
        {
            foreach (var i in map.GetItemsInRange<Static>(at, 0))
            {
                if (!i.Deleted && i.ItemID == id && i.Z == at.Z && i.Name == name)
                {
                    return true;
                }
            }

            return false;
        }

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

            // cc-P741: a farm laid before the field and the farmhouse swapped sides: its gate, fence piece, Pip's props and
            // spawners go, and Wren, Tobin and Pip walk to their new spots (the farm addon itself is relaid just below)
            if (!dryRun) { MoveFromOldLayout(map); }

            // cc-P720: a farm laid with an older layout (P663's hillside farmhouse) is taken up, with its door, and laid again.
            if (FindAddon<TutorialIsleFarmAddon>(map, TutorialIsleFarmLayout.Anchor) is { } old && !IsCurrent(old))
            {
                if (!dryRun)
                {
                    old.Delete();
                    foreach (var spot in TutorialIsleFarmLayout.OldDoorSpots) { FindDoor(map, spot)?.Delete(); }
                }

                addons++;
                if (!dryRun) { new TutorialIsleFarmAddon().MoveToWorld(TutorialIsleFarmLayout.Anchor, map); }
            }
            else if (FindAddon<TutorialIsleFarmAddon>(map, TutorialIsleFarmLayout.Anchor) is { } placed)
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
                if (!dryRun) { MakeIsleDoor().MoveToWorld(TutorialIsleFarmLayout.DoorSpot, map); }
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

            // cc-P721: Pip, the healing lesson's patient (he is optional: the lesson falls back to a scratch). cc-P726: on his
            // bench in the farmhouse, with blood around him; one placed by the gate before is moved there.
            foreach (var (id, at, name) in PipProps)
            {
                if (!HasProp(map, id, at, name) && !dryRun)
                {
                    new Static(id) { Name = name }.MoveToWorld(at, map);
                }
            }

            // cc-P740 item 4: the bandages on the farmhouse table beside Pip (a prop, like Pip's bench: not counted).
            if (FindTableBandages(map) == null && !dryRun)
            {
                new TutorialTableBandages().MoveToWorld(TableBandagesSpot, map);
            }

            if (FindPip(map) is { } pip)
            {
                present++;
                if (!dryRun && pip.Location != PipBench)
                {
                    pip.Home = PipBench;
                    pip.Direction = Direction.West;
                    pip.MoveToWorld(PipBench, map);
                }
            }
            else
            {
                if (!dryRun)
                {
                    new TutorialWoundedHelper { Direction = Direction.West, Home = PipBench, RangeHome = 0 }.MoveToWorld(PipBench, map);
                }

                hands++;
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

        /// <summary>
        /// cc-P741: clears what a farm laid before the move left where the field and the farmhouse used to be, and walks the
        /// farm's people to their new spots. Idempotent: on a world laid since, it finds nothing. Returns how many things it
        /// removed or moved.
        /// </summary>
        public static int MoveFromOldLayout(Map map)
        {
            var gone = new List<Item>();
            if (FindDoor(map, TutorialWorld.Ground(map, OldFieldGateSpot)) is TutorialPestGate oldGate) { gone.Add(oldGate); }
            foreach (var item in map.GetItemsInRange<Static>(TutorialWorld.Ground(map, OldFieldFenceSpot), 0))
            {
                if (!item.Deleted && item.ItemID == FieldFenceId) { gone.Add(item); }
            }

            foreach (var (id, at, name) in OldPipProps)
            {
                foreach (var i in map.GetItemsInRange<Static>(at, 0))
                {
                    if (!i.Deleted && i.ItemID == id && i.Z == at.Z && i.Name == name) { gone.Add(i); }
                }
            }

            foreach (var (spot, now, types) in new[] { (OldPestSpot, PestSpot, PestTypes), (OldPenSpot, PenSpot, PenTypes) })
            {
                if (FindSpawner(map, spot, types[0]) is { } s && FindSpawner(map, now, types[0]) != s)
                {
                    gone.Add(s); // BaseSpawner.OnDelete removes what it spawned
                }
            }

            foreach (var i in gone) { i.Delete(); }

            var moved = 0;

            // cc-P746: P740's bandages on the old farmhouse table go to the new one (or go, when one is there already).
            var oldTables = new List<TutorialTableBandages>();
            foreach (var t in map.GetItemsInRange<TutorialTableBandages>(OldTableBandagesSpot, 0))
            {
                if (!t.Deleted && t.Location == OldTableBandagesSpot) { oldTables.Add(t); }
            }

            foreach (var t in oldTables)
            {
                if (FindTableBandages(map) == null) { t.MoveToWorld(TableBandagesSpot, map); moved++; }
                else { t.Delete(); moved++; }
            }
            foreach (var (m, spot) in new (Mobile? M, Point2D Spot)[]
                     {
                         (FindNear<TutorialFarmhand>(map, FarmhandSpot), FarmhandSpot),
                         (FindNear<TutorialVeterinarian>(map, FarmVetSpot), FarmVetSpot)
                     })
            {
                if (m != null && (m.X != spot.X || m.Y != spot.Y))
                {
                    var at = TutorialWorld.Ground(map, spot);
                    if (m is BaseCreature bc) { bc.Home = at; }
                    m.MoveToWorld(at, map);
                    moved++;
                }
            }

            if (FindPip(map) is { } pip && pip.Location != PipBench)
            {
                pip.Home = PipBench;
                pip.Direction = Direction.West;
                pip.MoveToWorld(PipBench, map);
                moved++;
            }

            return gone.Count + moved;
        }

        /// <summary>The first <typeparamref name="T"/> within 30 tiles of <paramref name="spot"/> (the whole farm), or null.</summary>
        private static T? FindNear<T>(Map map, Point2D spot) where T : Mobile
        {
            foreach (var m in map.GetMobilesInRange(new Point3D(spot.X, spot.Y, 0), 30))
            {
                if (m is T t && !t.Deleted) { return t; }
            }

            return null;
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

        /// <summary>cc-P720: the isle farmhouse's door, in the cottage's south wall (OSI's 0x06A5 there).</summary>
        public static BaseDoor MakeIsleDoor() => new DarkWoodDoor(DoorFacing.WestCW);

        /// <summary>
        /// cc-P720: true when <paramref name="addon"/> holds exactly today's layout (art and offsets; the gate sign as the
        /// addon lays it). A farm laid before (P663's hillside farmhouse) is not.
        /// </summary>
        public static bool IsCurrent(BaseAddon addon)
        {
            var a = TutorialIsleFarmLayout.Anchor;
            var wanted = TutorialIsleFarmLayout.Pieces.Where(p => IsleArt(p.Id) != null)
                .Select(p => (IsleArt(p.Id)!.Value, p.X - a.X, p.Y - a.Y, p.Z - a.Z)).OrderBy(t => t).ToList();
            var placed = addon.Components.Select(c => (c.ItemID, c.Offset.X, c.Offset.Y, c.Offset.Z)).OrderBy(t => t).ToList();
            return wanted.SequenceEqual(placed);
        }

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

        // cc-P720 item 6: the farmhouse's own sign (the cottage multi's brass sign, on its hanger by the steps)
        public const int HouseSignId = 0x0BD2;
        public const string HouseSignName = "Wren's farmhouse";

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
                else if (art == TutorialFarm.HouseSignId)
                {
                    c.Name = TutorialFarm.HouseSignName; // cc-P720
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

            if (from is PlayerMobile striker)
            {
                TutorialFarm.NoteStruck(striker); // cc-P721: the trainee's own blow
            }

            base.Damage(amount, from, informMount, ignoreEvilOmen);
        }

        // cc-P721: the trainee's attack counts as they swing (pinned DoHarmful raises this on every swing, hit or miss).
        public override void AggressiveAction(Mobile aggressor, bool criminal)
        {
            base.AggressiveAction(aggressor, criminal);
            if (aggressor is PlayerMobile striker && Fights(striker))
            {
                TutorialFarm.NoteStruck(striker);
            }
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
                !(from is PlayerMobile pm && (TutorialFarm.PestPartOn(pm) || TutorialFarm.StepOf(pm) >= 2)))
            {
                from.SendMessage(0x3B2, TutorialFarm.GateClosedLine);
                return;
            }

            base.Use(from);
        }
    }

    /// <summary>
    /// cc-P721: the farm's pitchfork, handed out by Wren for the first fight. Kept like the isle's cloak: blessed, and
    /// Nontransferable (no sale, trade, bank or drop: pinned Item and GenericSellInfo).
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialPitchfork : Pitchfork
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private Mobile? _owner;

        [Constructible]
        public TutorialPitchfork() : this(null) { }

        public TutorialPitchfork(Mobile? owner)
        {
            _owner = owner;
            Name = "farmhand's pitchfork";
            LootType = LootType.Blessed;
        }

        public override bool Nontransferable => true;

        // A pitchfork wants 55 Strength (pinned Pitchfork.cs); a new character may have 45 or less, so this one any can hold.
        public override int AosStrengthReq => 10;
        public override int OldStrengthReq => 10;

        public override void HandleInvalidTransfer(Mobile from) =>
            from.SendMessage(0x3B2, "Your farmhand's pitchfork is yours alone. Keep it in your backpack or hold it.");
    }

    /// <summary>
    /// cc-P740 item 4: the bandages on the farmhouse table beside Pip. They never leave the table: double-clicking them, or
    /// trying to pick them up, gives a trainee on the healing lesson a stack of their own (TutorialFarm.TakeTableBandages).
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialTableBandages : Item
    {
        [Constructible]
        public TutorialTableBandages() : base(0x0E21)
        {
            Name = "bandages";
            Movable = false;
        }

        public override bool Decays => false;

        public override void OnDoubleClick(Mobile from)
        {
            if (from is not PlayerMobile pm)
            {
                return;
            }

            if (!pm.InRange(GetWorldLocation(), 2))
            {
                pm.SendMessage(0x3B2, "Move closer to the table, then double-click the bandages again.");
                return;
            }

            TutorialFarm.TakeTableBandages(pm);
        }

        // A drag reaches CheckLift (pinned Mobile.Lift asks VerifyMove first), which hands the trainee their own stack and
        // keeps this one on the table. Staff move it as usual.
        public override bool VerifyMove(Mobile from) => from is PlayerMobile || base.VerifyMove(from);

        public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
        {
            if (from.AccessLevel >= AccessLevel.GameMaster && Movable)
            {
                return base.CheckLift(from, item, ref reject);
            }

            if (from is PlayerMobile pm && from.AccessLevel < AccessLevel.GameMaster)
            {
                TutorialFarm.TakeTableBandages(pm);
            }

            reject = LRReason.Inspecific;
            return false;
        }
    }

    /// <summary>
    /// cc-P721: Pip, the farm boy beside Wren, cut on the fence: the heal step's patient. His hits never come back by
    /// themselves (CanRegenHits), so he is still hurt when the bandage lands, and he is cut again a little after each
    /// heal for the next trainee. Nothing can harm him.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialWoundedHelper : BaseCreature
    {
        public override bool CanBeDamaged() => false;

        public static readonly TimeSpan WoundAgainAfter = TimeSpan.FromSeconds(20);
        private TimerExecutionToken _wound;

        [Constructible]
        public TutorialWoundedHelper() : base(AIType.AI_Animal, FightMode.None, 10, 1)
        {
            Name = "Pip";
            Title = "the farm boy, cut on the fence";
            Body = 0x190;
            Hue = 0x83EA;
            SetStr(60);
            SetDex(40);
            SetInt(40);
            SetHits(60);
            CantWalk = true;
            AddItem(new Shirt { Movable = false, Hue = 0x2C });
            AddItem(new ShortPants { Movable = false, Hue = 0x455 });
            AddItem(new Sandals { Movable = false });
            AddItem(new Item(0x203B) { Movable = false, Hue = 0x2C, Layer = Layer.Hair });
            Wound();
        }

        // Not invulnerable: pinned refuses a beneficial act (a bandage) on an invulnerable target ("You cannot perform
        // beneficial acts on your target", the P721 bot run). Harm is refused instead: no damage, no death.
        public override bool ClickTitle => true;
        public override bool CanRegenHits => false;

        public override void Damage(int amount, Mobile? from = null, bool informMount = true, bool ignoreEvilOmen = false)
        {
        }

        /// <summary>Down to a third of his hits.</summary>
        public void Wound() => Hits = Math.Max(1, HitsMax / 3);

        public void WoundAgainSoon()
        {
            _wound.Cancel();
            Timer.StartTimer(WoundAgainAfter, Wound, out _wound);
        }

        public override void OnAfterDelete()
        {
            base.OnAfterDelete();
            _wound.Cancel();
        }

        [AfterDeserialization(false)]
        private void AfterLoad() => Wound();

        /// <summary>An arrow from the trainee to Pip.</summary>
        public void PointTo(PlayerMobile pm)
        {
            if (pm.Map == Map)
            {
                pm.QuestArrow?.Stop();
                pm.QuestArrow = new GuildDirectionArrow(pm, this, Location, Map, "Pip, the hurt farm boy");
            }
        }
    }
}
