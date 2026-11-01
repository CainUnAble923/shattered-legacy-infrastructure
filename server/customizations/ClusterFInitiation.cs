// cc-P714: the two initiation quests that open ClusterFSchoolGate's schools (P709's design, Chase's decisions of
// 2026-10-27; shard-migration notes/cc-P714-gated-skills.md). Places are Zigholtul's, on his Siege of Sosaria map; the
// people and their stories are ours. Every spot was checked standable, under open sky and joined on foot to its way in
// (notes/cc-P714-tools/sites.py and sites.txt).
//
// Chivalry, "A Place on the Watch" (Sister Aveline, Fort Whitestone; tuned for about 20-40):
//   1  Aveline hands over three relief bundles; the watch quartermaster says where they go.
//   2  Sergeant Tobin's patrol on the road south of the fort takes the bundles; scavengers block the road.
//   3  Drive off the three scavengers (each must be one the player fought: personal credit).
//   4  Aveline explains tithing. 5  Tithe 100 gold at the ankh in the courtyard: the balance must rise by 100 after
//      this step began. A balance too full to take 100 more gives the 100 gold to the relief fund instead (Codex P709).
//   6  Confirm: the permission is saved, then the Book of Chivalry (its stock ten abilities) is delivered once.
// Necromancy, "Names for the Last Door" (Oren Pike, the Keepers' clerk at the Dhampire Ruins; about 25-45):
//   1  Oren lends a copy of the burial register; three disturbed graves round the ruins each hide a nameplate, guarded
//      by a grave robber. 2  Back to Oren with the three plates. 3  Break the collector's binding east of the ruins and
//      defeat the guardian it raised; the bound shade is laid to rest. 4  Oren reads Warden Morrow's terms (the karma
//      warning); confirm, or come back later. The permission is saved, then a Necromancer spellbook with Curse Weapon
//      and Evil Omen and ten each of pig iron, bat wing, grave dust, daemon blood and nox crystal are delivered once.
//
// Rules from the design: no step needs a gated spell; friends may help fight, but a foe credits only the player it was
// raised for, and only if that player fought it; quest items are the player's own and replaced if lost; the supplies
// are delivered once, in one bag, or held by the teacher until the pack has room; nothing here raises a skill or a cap.
// Foes live in memory only: any left by a restart are deleted when the world loads, and the player raises them again.
//
// Staff: [Initiation place|clear|status (test shard only, idempotent), [Initiation reset <school> on a player.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Initiation
{
    public static class ClusterFInitiation
    {
        private static readonly ILogger logger = LogFactory.GetLogger(typeof(ClusterFInitiation));

        public const string Chivalry = "chivalry";
        public const string Necromancy = "necromancy";

        // Chivalry steps
        public const int CBundles = 1, CPatrol = 2, CScavengers = 3, CTitheTalk = 4, CTithe = 5, CConfirm = 6, CDone = 7;
        // Necromancy steps
        public const int NPlates = 1, NReturn = 2, NBinding = 3, NConfirm = 4, NDone = 5;

        public const int TitheAmount = 100;
        public const int TitheCap = 100000; // pinned TithingGump.MaxTitheAmount
        public const int Scavengers = 3;
        public const int Graves = 3;
        public const int BindingBit = 1 << 8;
        public const ulong NecroBookContent = (1UL << 3) | (1UL << 4); // Curse Weapon (103), Evil Omen (104)
        public const int ReagentAmount = 10;

        public static readonly Map Map = Map.Trammel;

        public static readonly string[] Scouts = ["Wren Hale", "Bram Cutter", "Isolde Venn"];
        public static readonly string[] Names = ["Edda Marrow", "Tomas Reed", "Hollis Crane"];

        // The spots (notes/cc-P714-tools/spots.json, checked by sites.py).
        public static readonly Point3D Aveline = new(893, 1036, 32);
        public static readonly Point3D Quartermaster = new(888, 1038, 32);
        public static readonly Point3D Ankh = new(890, 1034, 32);
        public static readonly Point3D Sergeant = new(889, 1078, 11);
        public static readonly Point3D[] ScavengerSpots = [new(890, 1082, 13), new(887, 1085, 13), new(893, 1088, 14)];
        public static readonly Point3D Oren = new(1458, 2512, 15);
        public static readonly Point3D[] GraveSpots = [new(1455, 2500, 11), new(1486, 2499, 10), new(1484, 2527, 19)];
        public static readonly Point3D BindingSpot = new(1492, 2521, 16);

        /// <summary>Every line said to a player passes through here as well, so the tests can read them.</summary>
        public static Action<Mobile, string> Said { get; set; }

        public static void Configure() =>
            CommandSystem.Register("Initiation", AccessLevel.Administrator, Initiation_OnCommand);

        public static void Initialize() => SweepFoes();

        /// <summary>Foes left by a restart are deleted once the world is up.</summary>
        public static int SweepFoes()
        {
            var gone = new List<Mobile>();
            foreach (var m in World.Mobiles.Values)
            {
                if (m is InitiationFoe)
                {
                    gone.Add(m);
                }
            }

            foreach (var m in gone)
            {
                m.Delete();
            }

            return gone.Count;
        }

        public static InitiationProgress Progress(Mobile m, string school) => ClusterFSchoolGate.ProgressOf(m, school);

        public static void Tell(Mobile m, string line)
        {
            m.SendMessage(0x59, line);
            Said?.Invoke(m, line);
        }

        private static void Speak(Mobile npc, PlayerMobile pm, string line)
        {
            npc.SayTo(pm, line);
            Said?.Invoke(pm, line);
        }

        // ------------------------------------------------------------------ talking to the teachers

        public static void Talk(PlayerMobile pm, InitiationNpc npc)
        {
            if (!pm.Alive)
            {
                Tell(pm, "Return to life before taking up this work.");
                return;
            }

            if (!pm.InRange(npc.Location, 4))
            {
                Tell(pm, $"Move closer, then double-click {npc.Name} again.");
                return;
            }

            switch (npc.Role)
            {
                case InitiationNpc.AvelineRole:
                    AvelineTalk(pm, npc);
                    break;
                case InitiationNpc.QuartermasterRole:
                    QuartermasterTalk(pm, npc);
                    break;
                case InitiationNpc.SergeantRole:
                    SergeantTalk(pm, npc);
                    break;
                case InitiationNpc.OrenRole:
                    OrenTalk(pm, npc);
                    break;
            }
        }

        public const int BtnAccept = 1, BtnTithe = 2, BtnFund = 3, BtnLearn = 4, BtnLater = 5;

        public static void Show(PlayerMobile pm, string school, string title, string text, params (int Id, string Label)[] buttons)
        {
            pm.CloseGump<InitiationGump>();
            pm.SendGump(new InitiationGump(school, title, text, buttons));
            Said?.Invoke(pm, text);
        }

        private static void AvelineTalk(PlayerMobile pm, InitiationNpc npc)
        {
            var p = Progress(pm, Chivalry);
            var title = "A Place on the Watch";
            switch (p.Step)
            {
                case 0 when ClusterFSchoolGate.HasPermission(pm, ClusterFSchoolGate.Find(Chivalry)):
                    Speak(npc, pm, "You already know the words. Keep the watch.");
                    return;
                case 0:
                    Show(pm, Chivalry, title,
                        "I teach Chivalry to those who serve first. A book gives you the words. Bringing these people home is why I will teach you to use them.<br><br>" +
                        "Three scouts of the relief watch are waiting for medicine. Take these three bundles, and ask the quartermaster in this courtyard where the scouts are.<br><br>" +
                        "You need no Chivalry for this work: any weapon, spells or a pet will do. Friends may help you, but each of you must do the work yourself.",
                        (BtnAccept, "I will take the bundles"));
                    return;
                case CBundles or CPatrol:
                    ReplaceBundles(pm);
                    Speak(npc, pm, p.Step == CBundles
                        ? "Ask the quartermaster in this courtyard where the bundles go."
                        : "Take the three bundles to Sergeant Tobin's patrol on the road south of the fort.");
                    return;
                case CScavengers:
                    Speak(npc, pm, "The road south is still blocked. Help Sergeant Tobin drive off the scavengers.");
                    return;
                case CTitheTalk:
                    Show(pm, Chivalry, title,
                        "The scouts are home. Thank you.<br><br>" +
                        "Now the cost. Chivalry's abilities spend tithing points, and you get tithing points by giving gold. " +
                        "Tithing turns your gold into points these abilities spend. Karma affects their strength. You can learn other arts, but their consequences follow you here too.<br><br>" +
                        "Go to the ankh here in the courtyard. Single-click it (or press and hold), choose Tithe Gold, and give 100 gold. Then come back to me.",
                        (BtnTithe, "I will tithe 100 gold"));
                    return;
                case CTithe:
                    if (pm.TithingPoints - p.Mark >= TitheAmount)
                    {
                        p.Step = CConfirm;
                        ShowConfirm(pm);
                        return;
                    }

                    if (pm.TithingPoints > TitheCap - TitheAmount)
                    {
                        Show(pm, Chivalry, title,
                            "Your tithing balance is too full to take another 100 points. Give 100 gold from your backpack to the watch's relief fund instead.",
                            (BtnFund, "Give 100 gold to the relief fund"));
                        return;
                    }

                    Speak(npc, pm, "Tithe 100 gold at the ankh in the courtyard: single-click it and choose Tithe Gold. Then come back.");
                    return;
                case CConfirm:
                    ShowConfirm(pm);
                    return;
                default:
                    DeliverKit(pm, Chivalry);
                    Speak(npc, pm, "You can now train Chivalry. Your skill starts at zero; practice, or pay a trainer. " +
                        "A lost Book of Chivalry is sold by mages and provisioners; tithe again at any ankh when your points run low.");
                    return;
            }
        }

        private static void ShowConfirm(PlayerMobile pm) =>
            Show(pm, Chivalry, "A Place on the Watch",
                "Service earned the teaching. Tithing supplies the abilities. Karma affects their strength.<br><br>" +
                "If you learn Chivalry now, your skill starts at zero and its cap is your normal cap: this does not raise either. " +
                "You will receive a Book of Chivalry. Double-click the book, then double-click an ability; a failed attempt does not mean you lost anything.",
                (BtnLearn, "Learn Chivalry"), (BtnLater, "Not yet"));

        private static void QuartermasterTalk(PlayerMobile pm, InitiationNpc npc)
        {
            var p = Progress(pm, Chivalry);
            if (p.Step == CBundles)
            {
                p.Step = CPatrol;
                Speak(npc, pm, $"Those are for {Scouts[0]}, {Scouts[1]} and {Scouts[2]}. They are with Sergeant Tobin's patrol, " +
                    "out the south gate and down the road. Watch for scavengers on the way.");
                return;
            }

            Speak(npc, pm, "Supplies in, supplies out. Sister Aveline has work for anyone who will serve.");
        }

        private static void SergeantTalk(PlayerMobile pm, InitiationNpc npc)
        {
            var p = Progress(pm, Chivalry);
            switch (p.Step)
            {
                case CPatrol:
                    {
                        var bundles = Tokens(pm, InitiationToken.BundleRole);
                        if (bundles.Count < Scouts.Length)
                        {
                            Speak(npc, pm, "Sister Aveline's bundles? I need all three. Ask her for any you lost.");
                            return;
                        }

                        foreach (var b in bundles)
                        {
                            b.Delete();
                        }

                        p.Step = CScavengers;
                        p.Count = 0;
                        Speak(npc, pm, $"Medicine! {Scouts[0]} is hurt; the orderly will carry her in. But scavengers are on the road behind us. Drive them off!");
                        RaiseScavengers(pm);
                        return;
                    }
                case CScavengers:
                    RaiseScavengers(pm);
                    Speak(npc, pm, $"Scavengers driven off: {p.Count} of {Scavengers}.");
                    return;
                case < CPatrol:
                    Speak(npc, pm, "We wait on supplies from the fort. Sister Aveline is sending someone.");
                    return;
                default:
                    Speak(npc, pm, "The road is open. Thank you.");
                    return;
            }
        }

        private static void OrenTalk(PlayerMobile pm, InitiationNpc npc)
        {
            var p = Progress(pm, Necromancy);
            var title = "Names for the Last Door";
            switch (p.Step)
            {
                case 0 when ClusterFSchoolGate.HasPermission(pm, ClusterFSchoolGate.Find(Necromancy)):
                    Speak(npc, pm, "You know the art. Use it with respect.");
                    return;
                case 0:
                    Show(pm, Necromancy, title,
                        "I keep the burial register for the Keepers of the Last Door. Warden Morrow says no one learns Necromancy from us before they have done this work: " +
                        "find their names before you ask anything of their dead.<br><br>" +
                        "Grave robbers have dug up three graves round these ruins and taken the nameplates. Here is a copy of the register. " +
                        "Search the three disturbed graves (dark dirt round the ruins) and bring the plates back.<br><br>" +
                        "You need no Necromancy for this: any weapon, spells or a pet will do. Friends may help you fight, but each of you must recover the plates yourself.",
                        (BtnAccept, "I will find the names"));
                    return;
                case NPlates:
                    ReplaceRegister(pm);
                    Speak(npc, pm, $"Plates recovered: {p.Count} of {Graves}. The disturbed graves are west, north-east and south-east of the ruins.");
                    return;
                case NReturn:
                    foreach (var plate in Tokens(pm, InitiationToken.PlateRole))
                    {
                        plate.Delete();
                    }

                    p.Step = NBinding;
                    Speak(npc, pm, $"{Names[0]}, {Names[1]}, {Names[2]}. They match the register. " +
                        "One more thing: a relic collector east of the ruins holds a nameless shade to guard what he stole. " +
                        "Snuff his skull candle to break the binding, then deal with whatever he raises.");
                    return;
                case NBinding:
                    Speak(npc, pm, "Break the collector's binding: his skull candle is east of the ruins.");
                    return;
                case NConfirm:
                    ShowWarning(pm);
                    return;
                default:
                    DeliverKit(pm, Necromancy);
                    Speak(npc, pm, "You can now train Necromancy. Your skill starts at zero; practice, or pay a trainer. " +
                        "Spirit Speak strengthens these spells. Reagent sellers and mages sell more reagents and scrolls; a lost book is bought, not given again.");
                    return;
            }
        }

        private static void ShowWarning(PlayerMobile pm) =>
            Show(pm, Necromancy, "Names for the Last Door",
                "The shade has its name back and has gone on. You did this work well. These are Warden Morrow's terms.<br><br>" +
                "Necromancy's spells lower your karma when you cast them. If you also practice Chivalry, lower karma weakens its effects. " +
                "You may learn both, but I will not hide the cost from you. Learning does not change your karma now.<br><br>" +
                "Your skill starts at zero and its cap is your normal cap: this does not raise either. You will receive a spellbook with Curse Weapon and Evil Omen, and ten of each of the five Necromancy reagents, once. " +
                "Curse Weapon can be tried from the start; Evil Omen needs more practice.",
                (BtnLearn, "Accept the terms and learn Necromancy"), (BtnLater, "Not yet"));

        public static void OnButton(PlayerMobile pm, string school, int button)
        {
            var p = Progress(pm, school);
            switch (school, button)
            {
                case (Chivalry, BtnAccept) when p.Step == 0 && !ClusterFSchoolGate.HasPermission(pm, ClusterFSchoolGate.Find(Chivalry)):
                    p.Step = CBundles;
                    ReplaceBundles(pm);
                    Tell(pm, "Quest: A Place on the Watch. Ask the quartermaster in Fort Whitestone's courtyard where the three relief bundles go.");
                    break;
                case (Chivalry, BtnTithe) when p.Step == CTitheTalk:
                    p.Step = CTithe;
                    p.Mark = pm.TithingPoints;
                    Tell(pm, "Tithe 100 gold at the ankh in Fort Whitestone's courtyard, then speak to Sister Aveline.");
                    break;
                case (Chivalry, BtnFund) when p.Step == CTithe && pm.TithingPoints > TitheCap - TitheAmount:
                    if (pm.Backpack?.ConsumeTotal(typeof(Gold), TitheAmount) == true)
                    {
                        p.Step = CConfirm;
                        Tell(pm, "You give 100 gold to the relief fund.");
                        ShowConfirm(pm);
                    }
                    else
                    {
                        Tell(pm, "You need 100 gold in your backpack.");
                    }

                    break;
                case (Chivalry, BtnLearn) when p.Step == CConfirm:
                    Complete(pm, Chivalry, CDone);
                    break;
                case (Necromancy, BtnAccept) when p.Step == 0 && !ClusterFSchoolGate.HasPermission(pm, ClusterFSchoolGate.Find(Necromancy)):
                    p.Step = NPlates;
                    p.Count = 0;
                    p.Sites = 0;
                    ReplaceRegister(pm);
                    Tell(pm, "Quest: Names for the Last Door. Search the three disturbed graves round the Dhampire Ruins for their nameplates.");
                    break;
                case (Necromancy, BtnLearn) when p.Step == NConfirm:
                    Complete(pm, Necromancy, NDone);
                    break;
                case (_, BtnLater):
                    Tell(pm, "Come back when you are ready. Your work is not lost.");
                    break;
            }
        }

        /// <summary>The permission first (saved on its own), then the one-time supplies.</summary>
        public static void Complete(PlayerMobile pm, string school, int doneStep)
        {
            var p = Progress(pm, school);
            ClusterFSchoolGate.Grant(pm, school, "initiation quest");
            p.Step = doneStep;
            var s = ClusterFSchoolGate.Find(school);
            Tell(pm, $"You can now train {s.Name}. Your skill starts at zero. Practice or pay a trainer to improve it. The quest did not raise your skill cap." +
                (school == Chivalry ? " Your book's abilities use mana and tithing points." : " Its spells use reagents and lower your karma; Spirit Speak strengthens them."));
            DeliverKit(pm, school);
        }

        public static List<Item> KitItems(string school)
        {
            if (school == Chivalry)
            {
                return [new BookOfChivalry()];
            }

            return
            [
                new NecromancerSpellbook(NecroBookContent),
                new PigIron(ReagentAmount), new BatWing(ReagentAmount), new GraveDust(ReagentAmount),
                new DaemonBlood(ReagentAmount), new NoxCrystal(ReagentAmount)
            ];
        }

        /// <summary>
        /// The supplies, once, in one bag dropped into the pack in one step. A pack without room keeps them with the teacher;
        /// the flag is set only after the bag is in the pack.
        /// </summary>
        public static bool DeliverKit(PlayerMobile pm, string school)
        {
            var p = Progress(pm, school);
            if (p.KitGiven || !ClusterFSchoolGate.HasPermission(pm, ClusterFSchoolGate.Find(school)))
            {
                return false;
            }

            var bag = new Bag { Name = school == Chivalry ? "Sister Aveline's supplies" : "the Keepers' supplies" };
            foreach (var item in KitItems(school))
            {
                bag.DropItem(item);
            }

            if (pm.Backpack == null || !pm.Backpack.TryDropItem(pm, bag, false))
            {
                bag.Delete();
                Tell(pm, "Your backpack is full. Make room, then speak to your teacher again for your supplies.");
                return false;
            }

            p.KitGiven = true;
            logger.Information("Initiation: {Character} ({Serial}) received the {School} supplies", pm.Name, pm.Serial, school);
            Tell(pm, school == Chivalry
                ? "A bag with a Book of Chivalry is in your backpack."
                : "A bag with a Necromancer spellbook (Curse Weapon, Evil Omen) and ten of each Necromancy reagent is in your backpack.");
            return true;
        }

        // ------------------------------------------------------------------ quest items

        public static List<InitiationToken> Tokens(PlayerMobile pm, string role)
        {
            var list = new List<InitiationToken>();
            if (pm.Backpack == null)
            {
                return list;
            }

            foreach (var t in pm.Backpack.FindItemsByType<InitiationToken>())
            {
                if (t.Role == role && t.Owner == pm)
                {
                    list.Add(t);
                }
            }

            return list;
        }

        private static void Give(PlayerMobile pm, InitiationToken token)
        {
            if (pm.Backpack == null || !pm.Backpack.TryDropItem(pm, token, false))
            {
                token.MoveToWorld(pm.Location, pm.Map);
            }
        }

        public static void ReplaceBundles(PlayerMobile pm)
        {
            var have = new HashSet<string>();
            foreach (var t in Tokens(pm, InitiationToken.BundleRole))
            {
                have.Add(t.Detail);
            }

            foreach (var scout in Scouts)
            {
                if (!have.Contains(scout))
                {
                    Give(pm, new InitiationToken(InitiationToken.BundleRole, pm, scout));
                }
            }
        }

        private static void ReplaceRegister(PlayerMobile pm)
        {
            if (Tokens(pm, InitiationToken.RegisterRole).Count == 0)
            {
                Give(pm, new InitiationToken(InitiationToken.RegisterRole, pm, ""));
            }
        }

        // ------------------------------------------------------------------ the field

        public static List<InitiationFoe> FoesOf(PlayerMobile pm, string kind)
        {
            var list = new List<InitiationFoe>();
            foreach (var m in World.Mobiles.Values)
            {
                if (m is InitiationFoe { Deleted: false } f && f.Owner == pm && f.Kind == kind)
                {
                    list.Add(f);
                }
            }

            return list;
        }

        /// <summary>Raises the scavengers still owed on the road: never more than the count left.</summary>
        public static void RaiseScavengers(PlayerMobile pm)
        {
            var p = Progress(pm, Chivalry);
            var alive = FoesOf(pm, InitiationFoe.ScavengerKind).Count;
            for (var i = alive; i < Scavengers - p.Count; i++)
            {
                var foe = new InitiationFoe(InitiationFoe.ScavengerKind, pm, i);
                foe.MoveToWorld(ScavengerSpots[i % ScavengerSpots.Length], Map);
                foe.Combatant = pm;
            }
        }

        public static void UseSite(PlayerMobile pm, InitiationSite site)
        {
            if (!pm.InRange(site.GetWorldLocation(), 3))
            {
                Tell(pm, "Move closer, then double-click it again.");
                return;
            }

            var p = Progress(pm, Necromancy);
            if (site.Kind == InitiationSite.GraveKind)
            {
                var bit = 1 << site.Index;
                var robbed = 1 << (site.Index + 4);
                if (p.Step != NPlates)
                {
                    Tell(pm, "Dark dirt over a disturbed grave.");
                }
                else if ((p.Sites & bit) != 0)
                {
                    Tell(pm, "You already recovered this grave's nameplate.");
                }
                else if ((p.Sites & robbed) != 0)
                {
                    p.Sites |= bit;
                    p.Count++;
                    Give(pm, new InitiationToken(InitiationToken.PlateRole, pm, Names[site.Index]));
                    Tell(pm, $"Under the dirt you find a nameplate: {Names[site.Index]}. It matches a line in the register.");
                    if (p.Count >= Graves)
                    {
                        p.Step = NReturn;
                        Tell(pm, "You have all three nameplates. Take them back to Oren Pike at the ruins.");
                    }
                }
                else if (FoesAt(pm, InitiationFoe.RobberKind, site.Index).Count > 0)
                {
                    Tell(pm, "The grave robber still guards this grave.");
                }
                else
                {
                    var foe = new InitiationFoe(InitiationFoe.RobberKind, pm, site.Index);
                    foe.MoveToWorld(site.GetWorldLocation(), site.Map);
                    foe.Combatant = pm;
                    Tell(pm, "A grave robber jumps up from behind the stones!");
                }

                return;
            }

            if (site.Kind == InitiationSite.BindingKind)
            {
                if (p.Step != NBinding)
                {
                    Tell(pm, "A skull with a guttering candle.");
                }
                else if (FoesOf(pm, InitiationFoe.GuardianKind).Count > 0)
                {
                    Tell(pm, "The collector's guardian is still standing.");
                }
                else
                {
                    p.Sites |= BindingBit;
                    var foe = new InitiationFoe(InitiationFoe.GuardianKind, pm, 0);
                    foe.MoveToWorld(site.GetWorldLocation(), site.Map);
                    foe.Combatant = pm;
                    Tell(pm, "You snuff the candle and the binding breaks. The collector runs, and the guardian he raised turns on you!");
                }
            }
        }

        private static List<InitiationFoe> FoesAt(PlayerMobile pm, string kind, int index)
        {
            var list = FoesOf(pm, kind);
            list.RemoveAll(f => f.Index != index);
            return list;
        }

        /// <summary>A foe died: its owner gets credit only if the owner fought it.</summary>
        public static void OnFoeDeath(InitiationFoe foe)
        {
            if (foe.Owner is not PlayerMobile pm || pm.Deleted)
            {
                return;
            }

            if (foe.LastKiller != pm && foe.FindDamageEntryFor(pm) == null)
            {
                Tell(pm, "You did not fight that one, so it does not count for your quest.");
                return;
            }

            switch (foe.Kind)
            {
                case InitiationFoe.ScavengerKind:
                    {
                        var p = Progress(pm, Chivalry);
                        if (p.Step != CScavengers)
                        {
                            return;
                        }

                        p.Count++;
                        Tell(pm, $"Scavengers driven off: {p.Count} of {Scavengers}.");
                        if (p.Count >= Scavengers)
                        {
                            p.Step = CTitheTalk;
                            Tell(pm, "The road is open and the scouts reach the relief post. Return to Sister Aveline in Fort Whitestone.");
                        }

                        return;
                    }
                case InitiationFoe.RobberKind:
                    {
                        var p = Progress(pm, Necromancy);
                        if (p.Step == NPlates)
                        {
                            p.Sites |= 1 << (foe.Index + 4);
                            Tell(pm, "The grave robber is beaten. Search the disturbed grave again.");
                        }

                        return;
                    }
                case InitiationFoe.GuardianKind:
                    {
                        var p = Progress(pm, Necromancy);
                        if (p.Step == NBinding)
                        {
                            p.Step = NConfirm;
                            Effects.SendLocationEffect(foe.Location, foe.Map, 0x376A, 20, 10, 1153, 0);
                            Tell(pm, "A pale shade whispers its name, finds it on the register, and is gone. Return to Oren Pike.");
                        }

                        return;
                    }
            }
        }

        // ------------------------------------------------------------------ placement

        public sealed record Piece(string Label, Func<IEntity> Make, Point3D Location, Func<IEntity, bool> Is);

        public static List<Piece> Pieces()
        {
            var list = new List<Piece>
            {
                Npc(InitiationNpc.AvelineRole, Aveline),
                Npc(InitiationNpc.QuartermasterRole, Quartermaster),
                Npc(InitiationNpc.SergeantRole, Sergeant),
                Npc(InitiationNpc.OrenRole, Oren),
                new("the courtyard ankh", () => new AnkhWest(), Ankh, e => e is AnkhWest),
                new("the binding", () => new InitiationSite(InitiationSite.BindingKind, 0), BindingSpot,
                    e => e is InitiationSite { Kind: InitiationSite.BindingKind })
            };
            for (var i = 0; i < GraveSpots.Length; i++)
            {
                var index = i;
                list.Add(new($"grave {i + 1}", () => new InitiationSite(InitiationSite.GraveKind, index), GraveSpots[i],
                    e => e is InitiationSite { Kind: InitiationSite.GraveKind } s && s.Index == index));
            }

            return list;
        }

        private static Piece Npc(string role, Point3D at) =>
            new(role, () => new InitiationNpc(role), at, e => e is InitiationNpc n && n.Role == role);

        private static IEntity Existing(Piece piece)
        {
            foreach (var m in Map.GetMobilesInRange(piece.Location, 0))
            {
                if (piece.Is(m))
                {
                    return m;
                }
            }

            foreach (var i in Map.GetItemsInRange(piece.Location, 0))
            {
                if (piece.Is(i))
                {
                    return i;
                }
            }

            return null;
        }

        /// <summary>Places what is missing; a second call places nothing. Returns the lines of the reply.</summary>
        public static List<string> Place(bool dryRun = false)
        {
            var lines = new List<string>();
            var placed = 0;
            foreach (var piece in Pieces())
            {
                if (Existing(piece) != null)
                {
                    lines.Add($"{piece.Label}: already standing at {piece.Location}.");
                    continue;
                }

                if (!dryRun)
                {
                    var e = piece.Make();
                    if (e is Mobile m)
                    {
                        m.MoveToWorld(piece.Location, Map);
                        if (m is BaseCreature bc)
                        {
                            bc.Home = piece.Location;
                            bc.RangeHome = 0;
                        }
                    }
                    else if (e is Item item)
                    {
                        item.MoveToWorld(piece.Location, Map);
                    }
                }

                placed++;
                lines.Add($"{piece.Label}: {(dryRun ? "would place" : "placed")} at {piece.Location}.");
            }

            lines.Add($"Initiation: {(dryRun ? "would place" : "placed")} {placed} thing(s).");
            return lines;
        }

        public static int Clear()
        {
            var count = 0;
            foreach (var piece in Pieces())
            {
                while (Existing(piece) is { } e)
                {
                    e.Delete();
                    count++;
                }
            }

            count += SweepFoes();
            return count;
        }

        [Usage("Initiation [place|dry|clear|status|reset <school>]")]
        [Description("cc-P714: the Chivalry (Fort Whitestone) and Necromancy (Dhampire Ruins) initiation teachers and field pieces. Test shard only for place and clear. reset targets a player and restarts that quest (the permission and the supplies flag are kept).")]
        [ShardCommand(CommandCategory.DevTool)]
        private static void Initiation_OnCommand(CommandEventArgs e)
        {
            var from = e.Mobile;
            var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
            switch (verb)
            {
                case "place" or "clear" when !ClusterFSchoolGate.IsTestShard():
                    from.SendMessage("Initiation places and clears only on the test shard.");
                    return;
                case "place":
                    foreach (var line in Place())
                    {
                        from.SendMessage(line);
                    }

                    return;
                case "dry" or "status":
                    foreach (var line in Place(true))
                    {
                        from.SendMessage(line);
                    }

                    from.SendMessage($"School gates: {ClusterFSchoolGate.Mode} (in force: {(ClusterFSchoolGate.Enabled ? "yes" : "no")}).");
                    return;
                case "clear":
                    from.SendMessage($"Initiation: took away {Clear()} thing(s).");
                    return;
                case "reset":
                    {
                        var school = e.Length > 1 ? e.GetString(1).ToLowerInvariant() : "";
                        if (school is not (Chivalry or Necromancy))
                        {
                            from.SendMessage("Name a school: chivalry or necromancy.");
                            return;
                        }

                        from.SendMessage("Target a player.");
                        from.Target = new ResetTarget(school);
                        return;
                    }
                default:
                    from.SendMessage("Usage: [Initiation [place|dry|clear|status|reset <school>]");
                    return;
            }
        }

        private sealed class ResetTarget : Target
        {
            private readonly string _school;

            public ResetTarget(string school) : base(-1, false, TargetFlags.None) => _school = school;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (targeted is not PlayerMobile pm)
                {
                    from.SendMessage("That is not a player.");
                    return;
                }

                var p = Progress(pm, _school);
                p.Step = ClusterFSchoolGate.HasPermission(pm, ClusterFSchoolGate.Find(_school)) ? (_school == Chivalry ? CDone : NDone) : 0;
                p.Count = 0;
                p.Sites = 0;
                p.Mark = 0;
                from.SendMessage($"{pm.Name}: {_school} quest restarted at step {p.Step}. {ClusterFSchoolGate.Describe(pm)}");
            }
        }
    }

    public class InitiationGump : Gump
    {
        private readonly string _school;

        public InitiationGump(string school, string title, string text, (int Id, string Label)[] buttons) : base(80, 80)
        {
            _school = school;
            var h = 300 + buttons.Length * 30;
            Server.Tutorial.TutorialStyle.Panel(this, 520, h);
            AddLabel(24, 18, Server.Tutorial.TutorialStyle.HeadingHue, title);
            AddHtml(24, 46, 472, 220, Server.Tutorial.TutorialStyle.Html(text), false, true);
            var y = 276;
            foreach (var (id, label) in buttons)
            {
                AddButton(24, y, 4005, 4007, id);
                AddLabel(60, y + 2, Server.Tutorial.TutorialStyle.ActionHue, label);
                y += 30;
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile is PlayerMobile pm && info.ButtonID > 0)
            {
                ClusterFInitiation.OnButton(pm, _school, info.ButtonID);
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class InitiationNpc : BaseCreature
    {
        public const string AvelineRole = "aveline";
        public const string QuartermasterRole = "quartermaster";
        public const string SergeantRole = "sergeant";
        public const string OrenRole = "oren";

        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private string _role;

        [Constructible]
        public InitiationNpc(string role = AvelineRole) : base(AIType.AI_Animal, FightMode.None, 10, 1)
        {
            _role = role;
            SetStr(80);
            SetDex(80);
            SetInt(80);
            SetHits(150);
            CantWalk = true;
            Dress(role);
        }

        public override bool IsInvulnerable => true;
        public override bool ClickTitle => true;

        private void Dress(string role)
        {
            switch (role)
            {
                case AvelineRole:
                    Name = "Sister Aveline Hart";
                    Title = "teacher of Chivalry";
                    Body = 0x191;
                    Female = true;
                    Hue = 0x83EA;
                    AddItem(new PlateChest { Movable = false, Hue = 0x47E });
                    AddItem(new Cloak { Movable = false, Hue = 0x47E });
                    AddItem(new Skirt { Movable = false, Hue = 0x455 });
                    AddItem(new Boots { Movable = false });
                    AddItem(new Item(0x203C) { Movable = false, Hue = 0x45E, Layer = Layer.Hair });
                    break;
                case QuartermasterRole:
                    Name = "Hendric Ash";
                    Title = "the watch quartermaster";
                    Body = 0x190;
                    Hue = 0x83F0;
                    AddItem(new FancyShirt { Movable = false, Hue = 0x47E });
                    AddItem(new LongPants { Movable = false, Hue = 0x455 });
                    AddItem(new HalfApron { Movable = false, Hue = 0x47E });
                    AddItem(new Shoes { Movable = false });
                    AddItem(new Item(0x203B) { Movable = false, Hue = 0x44E, Layer = Layer.Hair });
                    break;
                case SergeantRole:
                    Name = "Sergeant Tobin";
                    Title = "of the relief watch";
                    Body = 0x190;
                    Hue = 0x83F8;
                    AddItem(new ChainChest { Movable = false });
                    AddItem(new ChainLegs { Movable = false });
                    AddItem(new Cloak { Movable = false, Hue = 0x47E });
                    AddItem(new Boots { Movable = false });
                    AddItem(new Item(0x203B) { Movable = false, Hue = 0x3B2, Layer = Layer.Hair });
                    break;
                default:
                    Name = "Oren Pike";
                    Title = "clerk of the Keepers of the Last Door";
                    Body = 0x190;
                    Hue = 0x83F8;
                    AddItem(new Robe { Movable = false, Hue = 0x455 });
                    AddItem(new Sandals { Movable = false, Hue = 0x455 });
                    AddItem(new Item(0x203B) { Movable = false, Hue = 0x3B2, Layer = Layer.Hair });
                    break;
            }
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from is PlayerMobile pm)
            {
                ClusterFInitiation.Talk(pm, this);
            }
        }
    }

    /// <summary>A quest item: the relief bundles, the register copy, the nameplates. Only its owner can use it.</summary>
    [SerializationGenerator(0, false)]
    public partial class InitiationToken : Item
    {
        public const string BundleRole = "bundle";
        public const string RegisterRole = "register";
        public const string PlateRole = "plate";

        [SerializableField(0)]
        private string _role;

        [SerializableField(1)]
        private Mobile _owner;

        [SerializableField(2)]
        private string _detail;

        public InitiationToken(string role, Mobile owner, string detail) : base(role switch
        {
            BundleRole => 0x0E76,
            RegisterRole => 0x0FEF,
            _ => 0x1F14
        })
        {
            _role = role;
            _owner = owner;
            _detail = detail;
            Hue = role switch { BundleRole => 0x47E, RegisterRole => 0x455, _ => 0x3B2 };
            Weight = 1.0;
            LootType = LootType.Blessed;
            QuestItem = true;
            Name = role switch
            {
                BundleRole => $"relief bundle for scout {detail}",
                RegisterRole => "burial register (copy)",
                _ => $"nameplate: {detail}"
            };
        }

        public override bool DropToWorld(Mobile from, Point3D p)
        {
            from.SendMessage("Keep it for the quest; if you lose it, your teacher has another.");
            return false;
        }
    }

    /// <summary>A disturbed grave or the collector's skull candle.</summary>
    [SerializationGenerator(0, false)]
    public partial class InitiationSite : Item
    {
        public const string GraveKind = "grave";
        public const string BindingKind = "binding";

        [SerializableField(0)]
        private string _kind;

        [SerializableField(1)]
        private int _index;

        [Constructible]
        public InitiationSite(string kind = GraveKind, int index = 0) : base(kind == BindingKind ? 0x1853 : 0x0913)
        {
            _kind = kind;
            _index = index;
            Movable = false;
            Hue = kind == BindingKind ? 0 : 0x455;
            Name = kind == BindingKind ? "a skull candle" : "a disturbed grave";
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from is PlayerMobile pm)
            {
                ClusterFInitiation.UseSite(pm, this);
            }
        }
    }

    /// <summary>A field foe raised for one player. Anyone may fight it; only its owner gets credit, and only if they fought.</summary>
    [SerializationGenerator(0, false)]
    public partial class InitiationFoe : BaseCreature
    {
        public const string ScavengerKind = "scavenger";
        public const string RobberKind = "robber";
        public const string GuardianKind = "guardian";
        public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        public string Kind { get; private set; } = ScavengerKind;
        public Mobile Owner { get; private set; }
        public int Index { get; private set; }
        public DateTime Expires { get; private set; }

        public InitiationFoe(string kind, Mobile owner, int index) : base(AIType.AI_Melee, FightMode.Closest, 8, 1)
        {
            Kind = kind;
            Owner = owner;
            Index = index;
            Expires = Core.Now + Lifetime;
            Fame = 300;
            Karma = -300;

            switch (kind)
            {
                case GuardianKind:
                    Name = "the collector's guardian";
                    Body = 50; // skeleton
                    BaseSoundID = 0x48D;
                    SetStr(90);
                    SetDex(60);
                    SetInt(20);
                    SetHits(85, 95);
                    SetDamage(6, 10);
                    SetSkill(SkillName.Wrestling, 55.0, 60.0);
                    SetSkill(SkillName.Tactics, 50.0, 55.0);
                    SetSkill(SkillName.MagicResist, 40.0);
                    VirtualArmor = 24;
                    break;
                default:
                    Name = kind == RobberKind ? "a grave robber" : "a road scavenger";
                    Body = 0x190;
                    Hue = Race.Human.RandomSkinHue();
                    SetStr(65);
                    SetDex(55);
                    SetInt(20);
                    SetHits(kind == RobberKind ? 60 : 50, kind == RobberKind ? 70 : 60);
                    SetDamage(kind == RobberKind ? 5 : 4, kind == RobberKind ? 9 : 8);
                    SetSkill(SkillName.Macing, 40.0, 50.0);
                    SetSkill(SkillName.Tactics, 40.0, 50.0);
                    SetSkill(SkillName.MagicResist, 25.0);
                    VirtualArmor = 14;
                    AddItem(new Club { Movable = false });
                    AddItem(new Shirt { Movable = false, Hue = Utility.RandomNeutralHue() });
                    AddItem(new ShortPants { Movable = false, Hue = Utility.RandomNeutralHue() });
                    AddItem(new Boots { Movable = false });
                    break;
            }
        }

        public override bool AlwaysMurderer => true;
        public override bool CanRummageCorpses => false;

        // No loot or gold: the field gives no income.
        public override void GenerateLoot()
        {
        }

        public override void OnThink()
        {
            base.OnThink();
            if (Core.Now > Expires || Owner is not { Deleted: false } || Owner.Map != Map || !Owner.InRange(Location, 40))
            {
                Delete();
            }
        }

        public override void OnDeath(Container c)
        {
            base.OnDeath(c);
            ClusterFInitiation.OnFoeDeath(this);
        }
    }
}
