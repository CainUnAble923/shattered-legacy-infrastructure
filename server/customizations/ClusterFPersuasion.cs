// cc-P626: Persuasion, stage 1 of shard-migration notes/cc-P606-begging-skill-design.md (Chase 2026-10-25, option B:
// haggling with audited price floors, modest sale bonuses, diplomacy for standing, earned skill gain; begging stays as the
// lowest use). Stage 1 is the design's prompt (1): identity, charity, and novice work.
//
// The Begging skill keeps its slot (SkillName.Begging, 6), its saved Base, Cap, lock and modifiers; the server calls it
// Persuasion. Using it targets an NPC and opens a short menu of the social choices this stage supports:
//
//   Ask for help   charity, the bottom of the skill. A willing townsperson (a spawned, uncontrolled, human civilian) gives
//                  a few coins from their own pack, or one plain meal. One result per account, contact and UTC day; at
//                  most 50 gold and 2 meals per account per UTC day, across characters. It never lowers karma. Training
//                  from it is introductory: once per distinct contact, and only below 20 Base.
//   Ask about work at a banker or the Merchants' Exchange guildmaster, a courier job: a sealed letter to deliver by hand to
//                  a named kind of NPC in a named town. Delivering it is the training route, through every band to 200
//                  (the design's job difficulties 10, 35, ... 195; each gains in a window from difficulty - 25 to + 25).
//
// The stock Begging handler (pinned Skills/Begging.cs) is left in place but no longer reached from the skill button:
// Initialize below replaces the callback after pinned's (CallPriority). Its endless (0, 100) target roll was the old
// training route, which the design retires once work trains every band. Later stages add haggling, rumors, terms, calm and
// the tutorial pass (drafted in shard-migration notes/cc-P626-build-social-skill.md).
//
// cc-P647 (review fixes): the menu rechecks the conversation when it is answered (both alive, same facet, in range and
// sight, still a person to persuade); a courier holds one job wherever its letter is (a banked letter still counts);
// a town is the nearest NAMED town region around an NPC, so unnamed town subregions (Luna's bank) belong to their town;
// and a letter for a banker is taken by any kind of banker the board can pick (a gypsy banker, a minter).

using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Targeting;

namespace Server;

public static class ClusterFPersuasion
{
    public const string NewName = "Persuasion";

    public const int ContactRange = 3;
    public static readonly TimeSpan ActionDelay = TimeSpan.FromSeconds(10.0);

    // Charity.
    public const int DailyGold = 50;
    public const int DailyMeals = 2;
    public const int CoinCeiling = 14;
    public const double CharityTeachesBelow = 20.0;

    // Work.
    public static readonly int[] JobDifficulties = [10, 35, 60, 85, 110, 135, 160, 185, 195];
    public const int GainHalfWindow = 25;
    public const int FarTownTiles = 1000;
    public static readonly TimeSpan LetterLifetime = TimeSpan.FromHours(3.0);

    // Account tags (Fountain of Youth keeps its clock the same way): charity is per account, so alts share it.
    public const string DayTag = "Persuasion.CharityDay";
    public const string GoldTag = "Persuasion.CharityGold";
    public const string MealsTag = "Persuasion.CharityMeals";
    public const string ContactsTag = "Persuasion.CharityContacts";
    public const string TaughtTag = "Persuasion.CharityTaught";
    private const int MaxTaughtKept = 200;

    // Every line Persuasion says to a player passes through here as well, so the shard's tests can read them.
    public static Action<Mobile, string> Heard { get; set; }

    private static readonly Dictionary<Serial, string> _lastDestination = new();

    [CallPriority(100)] // after pinned's Begging.Initialize (default 50), which sets the same callback
    public static void Initialize()
    {
        SkillInfo.Table[(int)SkillName.Begging].Name = NewName;
        SkillInfo.Table[(int)SkillName.Begging].Callback = OnUse;
    }

    private static void Tell(Mobile m, int hue, string text)
    {
        m.SendMessage(hue, text);
        Heard?.Invoke(m, text);
    }

    private static void Say(Mobile npc, Mobile to, string text)
    {
        npc.SayTo(to, text);
        Heard?.Invoke(to, $"{npc.Name}: {text}");
    }

    // -- The skill button ---------------------------------------------------------------------------------------------

    public static TimeSpan OnUse(Mobile m)
    {
        m.RevealingAction();
        m.Target = new PersuadeTarget();
        Tell(m, 0x3B2, "Whom do you wish to persuade?");
        return TimeSpan.FromSeconds(30.0); // released or set to 10 s by the target, as pinned's Begging does
    }

    private sealed class PersuadeTarget : Target
    {
        public PersuadeTarget() : base(12, false, TargetFlags.None)
        {
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            from.NextSkillTime = Core.TickCount;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (from is PlayerMobile pm && Open(pm, targeted as Mobile))
            {
                from.NextSkillTime = Core.TickCount + (long)ActionDelay.TotalMilliseconds;
            }
            else
            {
                from.NextSkillTime = Core.TickCount;
            }
        }
    }

    /// <summary>
    /// Why pm cannot talk to npc right now, or null when they can. Checked when the menu opens and again when it is
    /// answered (cc-P647): InRange alone ignores the facet, and a menu can sit open while either side changes.
    /// </summary>
    public static string CannotConverse(PlayerMobile pm, Mobile npc)
    {
        if (npc == null || npc.Deleted)
        {
            return "There is nobody there to persuade.";
        }

        if (npc.Player)
        {
            return "Perhaps just asking would work better.";
        }

        if (!npc.Alive || !npc.Body.IsHuman || npc is BaseCreature { Controlled: true } or BaseCreature { Summoned: true })
        {
            return "There is little chance of persuading that.";
        }

        if (!pm.Alive)
        {
            return "You cannot persuade anyone while dead.";
        }

        if (pm.Map != npc.Map || !pm.InRange(npc, ContactRange) || !pm.InLOS(npc))
        {
            return "You are too far away to talk to them.";
        }

        return null;
    }

    /// <summary>Checks the person and opens the choices. False (and nothing spent) for anyone who cannot be persuaded.</summary>
    public static bool Open(PlayerMobile pm, Mobile npc)
    {
        var why = CannotConverse(pm, npc);
        if (why != null)
        {
            Tell(pm, 0x3B2, why);
            return false;
        }

        pm.RevealingAction();
        pm.CloseGump<PersuasionGump>();
        pm.SendGump(new PersuasionGump(npc));
        return true;
    }

    public sealed class PersuasionGump : DynamicGump
    {
        public const int AskForHelpButton = 1;
        public const int AskAboutWorkButton = 2;

        private readonly Mobile _npc;

        public PersuasionGump(Mobile npc) : base(60, 60) => _npc = npc;

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 320, 120, 5054);
            builder.AddBackground(10, 10, 300, 100, 3000);
            builder.AddHtml(20, 15, 280, 20, $"Persuasion: {_npc.Name}");
            builder.AddButton(20, 45, 4005, 4007, AskForHelpButton);
            builder.AddHtml(55, 46, 240, 20, "Ask for help");
            builder.AddButton(20, 75, 4005, 4007, AskAboutWorkButton);
            builder.AddHtml(55, 76, 240, 20, "Ask about work");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile is not PlayerMobile pm || info.ButtonID is not (AskForHelpButton or AskAboutWorkButton))
            {
                return;
            }

            var why = CannotConverse(pm, _npc);
            if (why != null)
            {
                Tell(pm, 0x3B2, why);
                return;
            }

            switch (info.ButtonID)
            {
                case AskForHelpButton:
                    AskForHelp(pm, _npc);
                    break;
                case AskAboutWorkButton:
                    AskAboutWork(pm, _npc);
                    break;
            }
        }
    }

    // -- Charity ------------------------------------------------------------------------------------------------------

    /// <summary>The outcome value S: effective skill, clamped to 0..200 (gear helps an action, never past 200).</summary>
    public static double S(Mobile m) => Math.Clamp(m.Skills[SkillName.Begging].Value, 0.0, 200.0);

    /// <summary>The design's lawful attitude factor: 1 at neutral or good karma, down to 0.8 at -10000.</summary>
    public static double KarmaFactor(Mobile m) => 1.0 - 0.2 * Math.Clamp(-m.Karma / 10000.0, 0.0, 1.0);

    /// <summary>The coin ceiling of one ask, before the giver's purse and the daily allowance: 2 to 14, no richer after 40.</summary>
    public static int CoinsOffered(Mobile m) =>
        Math.Min(CoinCeiling, (int)Math.Floor((2.0 + 0.3 * Math.Min(S(m), 40.0)) * KarmaFactor(m)));

    /// <summary>
    /// A stable identity for a charity contact: the spawner that keeps them and their kind, so a respawned or replaced
    /// townsperson is the same contact. Null for anyone who cannot give charity: no spawner (staff-made or fresh NPCs),
    /// hostile, controlled or summoned.
    /// </summary>
    public static string ContactKey(Mobile npc)
    {
        if (npc is not BaseCreature bc || bc.Controlled || bc.Summoned || !bc.Body.IsHuman || bc.Spawner is not Item spawner)
        {
            return null;
        }

        if (bc is not BaseVendor && bc.FightMode is not (FightMode.None or FightMode.Aggressor))
        {
            return null;
        }

        return $"{spawner.Serial.Value:X}:{bc.GetType().Name}";
    }

    private sealed class CharityDay
    {
        public int Gold;
        public int Meals;
        public HashSet<string> Contacts = new();
        public HashSet<string> Taught = new();
    }

    private static string Today => Core.Now.ToUniversalTime().ToString("yyyyMMdd");

    private static CharityDay Load(Account acct)
    {
        var day = new CharityDay();
        Split(acct.GetTag(TaughtTag), day.Taught);

        if (acct.GetTag(DayTag) == Today)
        {
            int.TryParse(acct.GetTag(GoldTag), out day.Gold);
            int.TryParse(acct.GetTag(MealsTag), out day.Meals);
            Split(acct.GetTag(ContactsTag), day.Contacts);
        }

        return day;
    }

    private static void Save(Account acct, CharityDay day)
    {
        acct.SetTag(DayTag, Today);
        acct.SetTag(GoldTag, day.Gold.ToString());
        acct.SetTag(MealsTag, day.Meals.ToString());
        acct.SetTag(ContactsTag, string.Join("|", day.Contacts));

        var taught = new List<string>(day.Taught);
        if (taught.Count > MaxTaughtKept)
        {
            taught.RemoveRange(0, taught.Count - MaxTaughtKept);
        }

        acct.SetTag(TaughtTag, string.Join("|", taught));
    }

    private static void Split(string value, HashSet<string> into)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        foreach (var part in value.Split('|', StringSplitOptions.RemoveEmptyEntries))
        {
            into.Add(part);
        }
    }

    /// <summary>Ask for help. True when the contact's attempt for today was spent (a gift or a valid refusal).</summary>
    public static bool AskForHelp(PlayerMobile pm, Mobile npc)
    {
        // The tutorial's practice person: a guaranteed demonstration, no coins, no meal, no skill.
        if (npc is TutorialTrainingEcho echo && echo.TrialOwner == pm.Serial)
        {
            Say(npc, pm, "A real townsperson might spare a coin or a meal. Ask the Exchange at any bank for paid work.");
            SkillEvents.InvokeSkillUsed(pm, pm.Skills[SkillName.Begging], true); // the thief trial's step
            return false;
        }

        var key = ContactKey(npc);
        if (key == null || pm.Account is not Account acct)
        {
            Tell(pm, 0x3B2, "They are not someone who can spare you anything.");
            return false;
        }

        var day = Load(acct);
        if (day.Contacts.Contains(key))
        {
            Tell(pm, 0x3B2, "You have already asked them today.");
            return false;
        }

        pm.Direction = pm.GetDirectionTo(npc);
        pm.Animate(32, 5, 1, true, false, 0); // bow, as pinned's Begging
        day.Contacts.Add(key);

        var gave = false;
        var refusedCoins = false;

        // Pinned's untrustworthy refusal (Begging.cs:100-106), kept for its hidden achievement: it refuses coins only.
        if (pm.Karma < 0 && 0.5 - pm.Karma / 8570.0 > Utility.RandomDouble())
        {
            refusedCoins = true;
            Say(npc, pm, "Thou dost not look trustworthy... no gold for thee today!");
            ClusterFJokeAchievements.BeggedUntrustworthy(pm);
        }
        else if (day.Gold < DailyGold && npc.Backpack is { } theirPack)
        {
            var coins = Math.Min(Math.Min(CoinsOffered(pm), theirPack.GetAmount(typeof(Gold)) / 10), DailyGold - day.Gold);
            var consumed = coins > 0 ? theirPack.ConsumeUpTo(typeof(Gold), coins) : 0;

            if (consumed > 0)
            {
                pm.AddToBackpack(new Gold(consumed));
                day.Gold += consumed;
                gave = true;
                Say(npc, pm, "I feel sorry for thee...");
                Tell(pm, 0x59, $"They give you {consumed} gold.");
            }
        }

        // No coins (an empty purse, the daily coin allowance spent, or a refusal): a plain meal, which needs no purse.
        if (!gave && day.Meals < DailyMeals)
        {
            pm.AddToBackpack(new BegFrenchBread());
            day.Meals++;
            gave = true;
            Say(npc, pm, refusedCoins ? "But nobody should go hungry. Here, take this bread." : "Here, take some bread.");
        }

        if (!gave && !refusedCoins)
        {
            Say(npc, pm, "I have nothing to spare for thee today.");
        }

        // Introductory training: once per distinct contact that actually gave, and only below 20 Base.
        if (gave && pm.Skills[SkillName.Begging].Base < CharityTeachesBelow && day.Taught.Add(key))
        {
            Practice(pm, -CharityTeachesBelow, CharityTeachesBelow);
        }

        Save(acct, day);
        return true;
    }

    /// <summary>
    /// One gain event on the window (min, max), through the shard's normal gain pipeline (its extra attempts, locks, cap,
    /// one SkillUsed). A positive worn bonus is added to both edges for this check only, so bonus gear cannot end the
    /// window before Base does; WornBonusIncluded stops the gain hook adding it a second time when the top is the cap.
    /// </summary>
    public static bool Practice(Mobile m, double min, double max)
    {
        var bonus = ClusterFSkillGain.WornBonus(m, m.Skills[SkillName.Begging]);
        return ClusterFSkillGain.CheckSkillWornBonusIncluded(m, SkillName.Begging, min + bonus, max + bonus);
    }

    // -- Work: courier letters ----------------------------------------------------------------------------------------

    public static bool IsExchangeContact(Mobile npc) => npc is Banker or MerchantGuildmaster;

    /// <summary>The hardest job tier this Base may take: difficulties unlock at max(0, d - 25).</summary>
    public static int JobDifficultyFor(double baseSkill)
    {
        var best = JobDifficulties[0];
        foreach (var d in JobDifficulties)
        {
            if (baseSkill >= Math.Max(0, d - GainHalfWindow))
            {
                best = d;
            }
        }

        return best;
    }

    /// <summary>
    /// The courier town an NPC stands in: the nearest NAMED town region in its ancestry. Unnamed town subregions (Luna's
    /// bank) belong to the town around them. Issue, destination choice, rotation and delivery all use this (cc-P647).
    /// </summary>
    public static string TownOf(Mobile m)
    {
        for (var r = m.Region; r != null; r = r.Parent)
        {
            if (r is TownRegion && !string.IsNullOrEmpty(r.Name))
            {
                return r.Name;
            }
        }

        return null;
    }

    /// <summary>The courier's one outstanding letter wherever it is (a pack, a bank box, the floor), or null (cc-P647).</summary>
    public static ExchangeLetter OutstandingLetter(Mobile m) => ExchangeLetter.OutstandingFor(m);

    /// <summary>Ask about work. At an Exchange contact, a courier letter; anywhere else, free directions.</summary>
    public static ExchangeLetter AskAboutWork(PlayerMobile pm, Mobile npc)
    {
        if (!IsExchangeContact(npc))
        {
            Say(npc, pm, "The Merchants' Exchange hires couriers. Ask any banker.");
            return null;
        }

        var outstanding = OutstandingLetter(pm);
        if (outstanding != null && !outstanding.Expired)
        {
            Say(npc, pm, $"Deliver the letter you were given first: {outstanding.Instructions}.");
            return null;
        }

        outstanding?.Delete(); // stale, wherever it was left

        var town = TownOf(npc);
        var difficulty = JobDifficultyFor(pm.Skills[SkillName.Begging].Base);
        var target = PickDestination(pm, npc, town, difficulty);

        if (target == null)
        {
            Say(npc, pm, "We have no letters going out from here today. Try another bank.");
            return null;
        }

        var letter = new ExchangeLetter(pm, npc, target.Value.Type, target.Value.Town, npc.Map, difficulty, Core.Now + LetterLifetime);
        pm.AddToBackpack(letter);
        _lastDestination[pm.Serial] = $"{target.Value.Town}:{target.Value.Type.Name}";
        Say(npc, pm, $"Take this sealed letter to {letter.Instructions}. Hand it over yourself.");
        return letter;
    }

    public readonly record struct Destination(Type Type, string Town);

    /// <summary>
    /// Where a letter of this difficulty goes. The first tier stays in the issuing town (another kind of shopkeeper there);
    /// every other tier goes to a banker in another town on the same facet, the top tiers to a far one where there is one,
    /// and never straight back to the last destination. Null when nowhere fits.
    /// </summary>
    public static Destination? PickDestination(PlayerMobile pm, Mobile issuer, string town, int difficulty)
    {
        var map = issuer.Map;
        if (map == null || map == Map.Internal)
        {
            return null;
        }

        _lastDestination.TryGetValue(pm.Serial, out var last);
        var options = new List<Destination>();
        var far = new List<Destination>();
        var seen = new HashSet<string>();

        foreach (var m in World.Mobiles.Values)
        {
            if (m is not BaseVendor v || v.Deleted || v.Map != map || v == issuer)
            {
                continue;
            }

            var vTown = TownOf(v);
            if (vTown == null)
            {
                continue;
            }

            Destination d;
            if (difficulty <= JobDifficulties[0])
            {
                if (town == null || vTown != town || v.GetType() == issuer.GetType() || IsExchangeContact(v))
                {
                    continue;
                }

                d = new Destination(v.GetType(), vTown);
            }
            else
            {
                // Any kind of banker (ExchangeLetter.IsRecipient takes the subclasses a Banker letter names).
                if (v is not Banker || vTown == town)
                {
                    continue;
                }

                d = new Destination(typeof(Banker), vTown);
            }

            var id = $"{d.Town}:{d.Type.Name}";
            if (id == last || !seen.Add(id))
            {
                continue;
            }

            options.Add(d);
            if (!issuer.InRange(v, FarTownTiles))
            {
                far.Add(d);
            }
        }

        var pool = difficulty >= 110 && far.Count > 0 ? far : options;
        return pool.Count == 0 ? null : pool[Utility.Random(pool.Count)];
    }

    /// <summary>The letter handed to its recipient: the job is consumed, then its one gain event.</summary>
    public static bool Deliver(PlayerMobile pm, ExchangeLetter letter, Mobile recipient)
    {
        if (letter.Deleted || letter.Courier != pm)
        {
            Tell(pm, 0x3B2, "This letter is not yours to deliver.");
            return false;
        }

        if (letter.Expired)
        {
            Tell(pm, 0x3B2, "The letter's business has gone stale. The Exchange will not pay for it now.");
            letter.Delete();
            return false;
        }

        // A save from before cc-P647 can hold several letters for one courier; only the newest is the job.
        if (!letter.IsOutstanding)
        {
            Tell(pm, 0x3B2, "The Exchange has replaced this letter with a newer one. It will not pay for it now.");
            letter.Delete();
            return false;
        }

        if (!pm.InRange(recipient, ContactRange))
        {
            Tell(pm, 0x3B2, "Hand the letter over in person.");
            return false;
        }

        if (!letter.IsRecipient(recipient))
        {
            Tell(pm, 0x3B2, $"This letter is for {letter.Instructions}.");
            return false;
        }

        var difficulty = letter.Difficulty;
        letter.Delete(); // consumed before the gain, so no replay can teach twice

        Say(recipient, pm, "Ah, the Exchange's letter. My thanks, courier.");
        Practice(pm, difficulty - GainHalfWindow, difficulty + GainHalfWindow);
        return true;
    }

    /// <summary>Tests only: forget the last destinations.</summary>
    public static void ResetForTests() => _lastDestination.Clear();
}
