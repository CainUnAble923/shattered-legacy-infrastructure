using System;
using System.Collections.Generic;
using Server;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;

namespace Server.Mobiles;

// -----------------------------------------------------------------------------
// ClusterF Smith BOD System - Phase 4C-i
//
// The Society of Smiths path for Smith BODs. One kind of deed, two paths, on purpose
// (Chase, 2026-10-02, PT-11: the guildmaster is not everywhere smiths are):
//
//   * Regular Blacksmith NPCs give and take Smith BODs and pay OSI's rewards, as pinned
//     (Mobiles/Vendors/NPC/Blacksmith.cs:76-127, BaseVendor.cs:1078-1150). Nothing patches
//     them; an earlier version of this note said Blacksmith_DisableBODs.patch did, and that
//     patch does not exist (cc-P32 Part B). Since cc-P55 Part H their request is a Small/Large
//     choice and a Society member's turn-in there pays the guild's way (ClusterFSmithBODPayout.cs).
//
//   * BlacksmithGuildmaster and the Smithing Guild Book give and take them for members.
//     Generation is cap-based (3 small / 1 large concurrent), no cooldown.
//     Large BODs need 70.1 Blacksmithy and nothing else (cc-P42 Part F, Chase 2026-10-03: the
//     Journeyman rank gate is gone), to request and to turn in. The Bulk Order button (guild page,
//     with or without the NPC) and the guild book let the player pick small or large
//     (SmithBulkOrderChoiceGump); talking to the guildmaster keeps the stock random roll.
//
//   * Turn-in gives Smithing Seals + a skill check (no vanilla item rewards).
//     Skill check range is scaled to material tier + exceptional requirement
//     so BODs always push skill gain in the right bracket.
//
//   * cc-P55 Part H (Chase 2026-10-05): a member's turn-in, here, from the book, or at any regular smith, is banked
//     (Seals, the default) or cashed out (gold and a chance at OSI's item) by a per-character setting, or asks each time
//     (ClusterFSmithBODPayout.cs). Regular smiths open a Small/Large choice by the stock rules and pay non-members
//     OSI's rewards as before.
//
// Ore gating hook:
//   TODO Phase 4C-ore: replace skill-only material check with ore knowledge
//   gate (MiningGuildOreKnowledge.KnowsOre) once Mining Guild data exists.
//   Skill-threshold gating is already correct for Phase 4C-i.
//
// Replaces ClusterFSmithBODRewards.cs - that file's reward logic now lives
// in BlacksmithGuildmaster.OnDragDrop / ComputeGuildReward below.
// -----------------------------------------------------------------------------

// -- BlacksmithGuildmaster - guild BOD generation and turn-in -----------------

public partial class BlacksmithGuildmaster
{
    private const int SmallBODCap = 3; // max concurrent small BODs in pack
    private const int LargeBODCap = 1; // max concurrent large BODs in pack

    /// <summary>Blacksmithy needed for a large order, to request and to turn in (cc-P42 Part F: skill only).</summary>
    public const double LargeOrderSkill = 70.1;

    public const string LargeSkillMessage = "You need at least 70.1 Blacksmithy to request large orders.";

    /// <summary>70.1 Blacksmithy, or an active Dev Testing Crystal.</summary>
    public static bool MeetsLargeSkill(PlayerMobile pm) =>
        DevTestingCrystal.IsActive(pm) || pm.Skills.Blacksmith.Base >= LargeOrderSkill;

    // -- Generation -----------------------------------------------------------

    public override bool SupportsBulkOrders(Mobile from)
    {
        if (from is not PlayerMobile pm) return false;
        if (pm.Account is not IAccount acct) return false;
        return ClusterFGuildSystem.IsJoined(pm, "smithing");
    }

    // No cooldown - generation is throttled by the active-BOD cap instead.
    public override TimeSpan GetNextBulkOrder(Mobile from) => TimeSpan.Zero;

    /// <summary>
    /// Generates a SmallSmithBOD or LargeSmithBOD for the player, subject to
    /// the active cap (3 small / 1 large).  Delegates to TryCreateBOD.
    /// </summary>
    public override Item CreateBulkOrder(Mobile from, bool fromContextMenu)
    {
        if (from is not PlayerMobile pm) return null;
        return TryCreateBOD(pm);
    }

    // -- Book-facing static BOD creation ---------------------------------------
    // Callable from SmithGuildBook without a Guildmaster NPC reference.
    // Contains the same generation logic as the old CreateBulkOrder body.

    public static Item? TryCreateBOD(PlayerMobile pm)
    {
        if (pm.Account is not IAccount) return null;
        if (!ClusterFGuildSystem.IsJoined(pm, "smithing"))
        {
            pm.SendMessage(0x22, "You must join the Society of Smiths first.");
            return null;
        }

        var skill = pm.Skills.Blacksmith.Base;
        var (_, largeCount) = CountBODs(pm);

        // Large BOD: 70.1 skill, random chance same as vanilla (the guildmaster's talk and context menu path)
        var qualifiesLarge = MeetsLargeSkill(pm);
        var rollsLarge     = skill >= LargeOrderSkill && (skill - 40.0) / 300.0 > Utility.RandomDouble();

        if (qualifiesLarge && rollsLarge)
        {
            if (largeCount >= LargeBODCap)
            {
                pm.SendMessage(0x22,
                    "You already have a large order in progress. Finish it before requesting another.");
                return null;
            }
            return LargeSmithBOD.CreateRandomFor(pm);
        }

        return TryCreateSmallBOD(pm);
    }

    // -- Small BOD on demand (cc-P42 Part F: the choice gump's Small, never a large roll) --------

    public static Item? TryCreateSmallBOD(PlayerMobile pm)
    {
        if (pm.Account is not IAccount) return null;
        if (!ClusterFGuildSystem.IsJoined(pm, "smithing"))
        {
            pm.SendMessage(0x22, "You must join the Society of Smiths first.");
            return null;
        }

        var (smallCount, _) = CountBODs(pm);
        if (smallCount >= SmallBODCap)
        {
            pm.SendMessage(0x22,
                $"You have {SmallBODCap} active orders. Complete some before requesting more.");
            return null;
        }

        // cc-P42 Part G1; cc-P46 Part B: off, the stock generator's own pick.
        var bod = SmallSmithBOD.CreateRandomFor(pm, teachingOnly: ClusterFSmithTeaching.WantsTeaching(pm));
        if (bod == null)
            pm.SendMessage(0x22,
                "There are no suitable orders for your skill level right now. " +
                "Practice your craft and return.");
        return bod;
    }

    // -- Large BOD on demand (the choice gump's and the guild book's Large) ------

    /// <summary>
    /// Creates a large BOD directly, skipping the small/large random roll.
    /// Requires 70.1 Blacksmithy (no rank since cc-P42 Part F).
    /// Respects the large-BOD cap (max 1 active).
    /// </summary>
    public static Item? TryCreateLargeBOD(PlayerMobile pm)
    {
        if (pm.Account is not IAccount) return null;
        if (!ClusterFGuildSystem.IsJoined(pm, "smithing"))
        {
            pm.SendMessage(0x22, "You must join the Society of Smiths first.");
            return null;
        }

        if (!MeetsLargeSkill(pm))
        {
            pm.SendMessage(0x22, LargeSkillMessage);
            return null;
        }

        var (_, largeCount) = CountBODs(pm);
        if (largeCount >= LargeBODCap)
        {
            pm.SendMessage(0x22,
                "You already have a large order in progress. Finish it before requesting another.");
            return null;
        }

        return LargeSmithBOD.CreateRandomFor(pm);
    }

    /// <summary>
    /// cc-P42 Part F. The one entry point for a chosen order (choice gump, guild book): creates the small or large
    /// deed with the rules above, needing no NPC, and shows pinned's own accept gump, as asking the guildmaster does
    /// (BaseVendor.cs:1393-1397). Accepting puts the deed in the backpack; cancelling deletes it.
    /// </summary>
    public static Item? OfferBOD(PlayerMobile pm, bool large)
    {
        var bod = large ? TryCreateLargeBOD(pm) : TryCreateSmallBOD(pm);

        if (bod is LargeSmithBOD largeBod)
            pm.SendGump(new LargeBODAcceptGump(largeBod));
        else if (bod is SmallSmithBOD smallBod)
            pm.SendGump(new SmallBODAcceptGump(smallBod));

        return bod;
    }

    // -- Turn-in ---------------------------------------------------------------

    public override bool IsValidBulkOrder(Item item) => item is SmallSmithBOD or LargeSmithBOD;

    /// <summary>
    /// Intercepts BOD turn-ins before BaseVendor fires vanilla item/gold rewards.
    /// Smith BODs are handled entirely here; non-BOD drops fall through to base.
    /// </summary>
    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        // -- Commission turn-in - check before the BOD type filter -------------
        // Commission items are weapons/armor, so they would otherwise fall through
        // to base.OnDragDrop.  Intercept them here for guild members.
        if (from is PlayerMobile pmComm && pmComm.Account is IAccount acctComm
            && ClusterFGuildSystem.IsJoined(pmComm, "smithing"))
        {
            var commData       = ClusterFAccountPersistence.GetOrCreate(acctComm).GetOrCreateGuildData(pmComm.Serial);
            var matchedComm    = SmithCommissionSystem.FindMatch(commData, dropped);
            if (matchedComm != null)
            {
                SmithCommissionSystem.Complete(pmComm, matchedComm, dropped);
                SayTo(from, "Excellent work. The commissioner will be well pleased.");
                return true;
            }
        }

        // Not a BOD at all - let base handle it (vendor sale, etc.)
        if (dropped is not SmallBOD and not LargeBOD)
            return base.OnDragDrop(from, dropped);

        // Wrong type of BOD (e.g. tailor)
        if (!IsValidBulkOrder(dropped))
        {
            SayTo(from, 1045130); // That order is for some other shopkeeper.
            return false;
        }

        // Must be a guild member
        if (from is not PlayerMobile pm || pm.Account is not IAccount acct
            || !ClusterFGuildSystem.IsJoined(pm, "smithing"))
        {
            SayTo(from, "Only members of the Society of Smiths may turn in orders here.");
            return false;
        }

        // Must be complete
        var complete = dropped switch
        {
            SmallBOD s => s.Complete,
            LargeBOD l => l.Complete,
            _          => false,
        };

        if (!complete)
        {
            SayTo(from, 1045131); // You have not completed the order yet.
            return false;
        }

        // cc-P32 (PT-11), cc-P42 Part F: the gate on requesting a large order (70.1 Blacksmithy) holds for
        // turning one in, so a deed the player could request can be turned in and one they could not cannot.
        if (dropped is LargeSmithBOD && !CanTurnInLarge(pm))
        {
            SayTo(from, LargeSkillRefusal);
            return false;
        }

        // -- Reward: bank or cash out by the member's setting (cc-P55 Part H, ClusterFSmithBODPayout). With "ask each
        // time" the deed goes back to the pack and the choice gump pays it.
        var result = ClusterFSmithBODPayout.Begin(pm, dropped, this, inHand: true);
        if (result != SmithTurnInResult.Paid)
            return false;

        SayTo(from, "Well done. The Society thanks you for your craft.");
        return true;
    }

    /// <summary>The turn-in's Blacksmithy checks, each a real gain-eligible roll scaled to the deed (GetSkillRange).</summary>
    internal static void RollTurnInSkillChecks(PlayerMobile pm, Item deed, int skillChecks)
    {
        var (skillMin, skillMax) = GetSkillRange(deed);
        for (var i = 0; i < skillChecks; i++)
            pm.CheckSkill(SkillName.Blacksmith, skillMin, skillMax);
    }

    // -- Reward calculation ----------------------------------------------------
    // cc-P56 Part A (bug-list D77, Chase 2026-10-05): banking a deed is worth at least what OSI pays a non-member for
    // the same deed at a regular smith (pinned's BaseBOD.GetRewards: gold and one item from the reward ladder), valued in
    // Seals: the gold at the guild's rate (SealMultiplier Seals per SealDivisor gold) and the item at its Seal catalog
    // price. So the bank pays two parts:
    //
    //   1. Gold part: the deed's gold value (pinned's SmithRewardCalculator.ComputeGold, which rolls 90% to 111% of its
    //      table) times BankGoldShare, at the guild's rate, rounded up, floor 1. BankGoldShare 1.25 covers OSI's whole
    //      roll: the lowest bank roll (90% x 1.25 = 112.5%) is above OSI's highest (111.1%), so no roll on either side
    //      lets a non-member out-earn a member.
    //   2. Item part: OsiItemSeals, the best item OSI's ladder gives for the deed's points, at its catalog price.
    //
    // Post-Valorite deeds: pinned's tables stop at Valorite, so OSI pays a non-member iron's gold and an item with no
    // material points. The gold part is the larger of that and the Valorite-equivalent gold times PostValoriteMultiplier
    // (the multipliers and what they multiply are unchanged since cc-P42); the item part is the larger of the deed's own
    // rung and the same deed's rung in Valorite. The item part is not multiplied: OSI's ladder has nothing above a
    // Valorite runic, and multiplying it would make one large Celestial exceptional deed worth five Valorite runics
    // (cc-P46 Part E raised the runic prices against exactly that).
    //
    // Representative values at the middle of the gold roll (Seals; before cc-P56 in brackets; OSI = what a non-member
    // gets, valued the same way):
    //   Iron small regular   qty10          ->     52 (3)       OSI 51
    //   Iron small exc       qty20          ->    305 (3)       OSI 304
    //   Valorite small exc   qty20          ->    263 (90)      OSI 240
    //   Large Valorite exc   qty20 (plate)  -> 16,875 (1,500)   OSI 16,500
    //   Platinum small exc   qty20          ->    469 (135)     OSI 304
    //   Large Platinum exc   qty20 (plate)  -> 17,813 (2,250)   OSI 700
    //   Large Celestial exc  qty20 (plate)  -> 24,375 (7,500)   OSI 700
    // Every smith deed shape (918) and the table: shard-migration notes/cc-P56-smith-economy-and-labels.md, Part A.

    private const int SealDivisor = 400;
    internal const int SealMultiplier = 3;
    internal const int StandingMultiplier = 2;

    /// <summary>cc-P56 Part A: the bank's gold part pays this share of the deed's gold value (covers OSI's 90%-111% roll).</summary>
    internal const double BankGoldShare = 1.25;

    /// <summary>The bank's gold part for an order worth <paramref name="gold"/>: BankGoldShare of it at the guild's rate, rounded up, floor 1.</summary>
    internal static int SealsForGold(int gold) =>
        Math.Max(1, (int)Math.Ceiling(gold * BankGoldShare * SealMultiplier / SealDivisor));

    // OSI's smith reward ladder (pinned Rewards.cs, SmithRewardCalculator's Groups: the points each rung needs), each rung
    // valued at the best item it can give, at the Seal catalog's price (SmithSealCatalogGump). An item the catalog does not
    // sell (mining gloves +1, the colored anvil, the 105 to 120 power scrolls) is valued at the midpoint of the best priced
    // item on the nearest priced rung below and above its own, as cc-P55 Part H valued the 115 scroll. Checked against the
    // live ladder and catalog by SmithBankValueVerification, so a price change not carried here fails the build.
    private static readonly (int Points, int Seals)[] OsiRungSeals =
    {
        (0, 50),       // Sturdy Shovel
        (25, 50),      // Sturdy Pickaxe
        (50, 175),     // shovel, pickaxe, mining gloves +1 (unpriced: 50 and 300)
        (200, 300),    // Gargoyle's Pickaxe, Prospector's Tool, mining gloves +3
        (400, 200),    // Gargoyle's Pickaxe, Prospector's Tool, Powder of Temperament
        (450, 600),    // Powder of Temperament, mining gloves +5
        (500, 200),    // Dull Copper runic
        (550, 350),    // Dull Copper or Shadow Iron runic
        (600, 350),    // Shadow Iron runic
        (625, 450),    // Shadow Iron runic, 105 scroll, colored anvil (unpriced: 350 and 550)
        (650, 550),    // Copper runic
        (675, 675),    // colored anvil, 110 scroll (unpriced: 550 and 800), Copper runic
        (700, 800),    // Bronze runic
        (750, 100),    // Ancient Smithy Hammer +10
        (800, 150),    // 115 scroll (unpriced: 100 and 200)
        (850, 200),    // Ancient Smithy Hammer +15
        (900, 1_900),  // 120 scroll (unpriced: 200 and 3,600)
        (950, 3_600),  // Gold runic
        (1000, 500),   // Ancient Smithy Hammer +30
        (1050, 5_400), // Agapite runic
        (1100, 1_000), // Ancient Smithy Hammer +60
        (1150, 9_000), // Verite runic
        (1200, 15_000) // Valorite runic
    };

    /// <summary>The ladder rungs the bank's item part reads, lowest first. Read only: for the facts.</summary>
    internal static (int Points, int Seals)[] OsiRungs => OsiRungSeals;

    /// <summary>The best item OSI's ladder gives for <paramref name="points"/>, in Seals (pinned's LookupRewards: the highest rung reached).</summary>
    internal static int OsiItemSeals(int points)
    {
        var seals = OsiRungSeals[0].Seals;
        foreach (var (rung, value) in OsiRungSeals)
        {
            if (points >= rung)
            {
                seals = value;
            }
        }

        return seals;
    }

    // Returns the post-Valorite multiplier over a Valorite-equivalent gold value (cc-P42; Chase 2026-10-04 kept them).
    private static double PostValoriteMultiplier(BulkMaterialType mat) => mat switch
    {
        BulkMaterialType.Platinum   => 1.5,
        BulkMaterialType.Toxic      => 2.0,
        BulkMaterialType.Blaze      => 2.5,
        BulkMaterialType.Frost      => 3.0,
        BulkMaterialType.Obsidian   => 3.5,
        BulkMaterialType.Mythril    => 4.0,
        BulkMaterialType.Adamantium => 4.5,
        BulkMaterialType.Celestial  => 5.0,
        _ => 1.0,
    };

    private static bool IsPostValorite(BulkMaterialType mat) => (int)mat >= 12;

    // For post-Valorite BODs, ComputeGold() returns iron-level gold because the vanilla gold table only covers
    // None-Valorite (indices 0-8). The gold part is the larger of that (what OSI pays) and the Valorite-equivalent gold
    // scaled by the tier multiplier. cc-P56: the larger, because pinned's table pays no gold for a large weapon order in
    // any colored metal (Rewards.cs, the 2-, 5- and 6-part rows) but iron's gold for a metal it does not know.
    internal static int GoldEquivalentForSeals(SmallSmithBOD small)
    {
        if (!IsPostValorite(small.Material))
            return small.ComputeGold();

        var valGold = SmithRewardCalculator.Instance.ComputeGold(
            small.AmountMax, small.RequireExceptional, BulkMaterialType.Valorite, 1, small.Type);
        return Math.Max(small.ComputeGold(), (int)(valGold * PostValoriteMultiplier(small.Material)));
    }

    internal static int GoldEquivalentForSeals(LargeSmithBOD large)
    {
        if (!IsPostValorite(large.Material))
            return large.ComputeGold();

        var valGold = SmithRewardCalculator.Instance.ComputeGold(
            large.AmountMax, large.RequireExceptional, BulkMaterialType.Valorite,
            large.Entries.Length, large.Entries[0].Details.Type);
        return Math.Max(large.ComputeGold(), (int)(valGold * PostValoriteMultiplier(large.Material)));
    }

    /// <summary>The bank's item part: the best item OSI gives for this deed (post-Valorite: or for it in Valorite), in Seals.</summary>
    internal static int ItemSealsForDeed(BulkMaterialType material, int amountMax, bool exceptional, int itemCount, Type type)
    {
        var calc = SmithRewardCalculator.Instance;
        var own  = OsiItemSeals(calc.ComputePoints(amountMax, exceptional, material, itemCount, type));

        return IsPostValorite(material)
            ? Math.Max(own, OsiItemSeals(calc.ComputePoints(amountMax, exceptional, BulkMaterialType.Valorite, itemCount, type)))
            : own;
    }

    internal static (int seals, int standing, int skillChecks) ComputeGuildReward(Item deed)
    {
        if (deed is SmallSmithBOD small)
        {
            var tier     = MaterialTier(small.Material);
            var standing = (small.RequireExceptional
                ? 60  + tier * 15
                : tier == 0 ? 25 : 40 + tier * 10) * StandingMultiplier;

            var gold  = GoldEquivalentForSeals(small);
            var seals = SealsForGold(gold) +
                        ItemSealsForDeed(small.Material, small.AmountMax, small.RequireExceptional, 1, small.Type);

            return (seals, standing, 1);
        }

        if (deed is LargeSmithBOD large)
        {
            var tier     = MaterialTier(large.Material);
            var standing = (tier == 0 ? 100 : 150 + tier * 20) * StandingMultiplier;
            var checks   = tier >= 5 ? 3 : 2;   // Gold+ large earns an extra skill check

            var gold  = GoldEquivalentForSeals(large);
            var seals = SealsForGold(gold) +
                        ItemSealsForDeed(large.Material, large.AmountMax, large.RequireExceptional,
                            large.Entries.Length, large.Entries[0].Details.Type);

            return (seals, standing, checks);
        }

        return (0, 0, 0);
    }

    // -- Skill check range -----------------------------------------------------

    /// <summary>
    /// Returns the (minSkill, maxSkill) window for CheckSkill on BOD completion.
    /// Below min = too easy (no gain); above max = too hard (no gain).
    /// Scaled to material tier + exceptional so BODs always reward skill practice
    /// in the appropriate bracket.
    /// </summary>
    private static (double min, double max) GetSkillRange(Item deed)
    {
        BulkMaterialType mat;
        bool exceptional;

        switch (deed)
        {
            case SmallSmithBOD s: mat = s.Material; exceptional = s.RequireExceptional; break;
            case LargeSmithBOD l: mat = l.Material; exceptional = l.RequireExceptional; break;
            default: return (0.0, 50.0);
        }

        var (min, max) = mat switch
        {
            BulkMaterialType.None       => (  0.0,   55.0),
            BulkMaterialType.DullCopper => ( 40.0,   72.0),
            BulkMaterialType.ShadowIron => ( 50.0,   80.0),
            BulkMaterialType.Copper     => ( 55.0,   85.0),
            BulkMaterialType.Bronze     => ( 60.0,   88.0),
            BulkMaterialType.Gold       => ( 65.0,   92.0),
            BulkMaterialType.Agapite    => ( 70.0,   96.0),
            BulkMaterialType.Verite     => ( 75.0,  100.0),
            BulkMaterialType.Valorite   => ( 80.0,  105.0),
            // Post-Valorite - requirement-20 to requirement+20 (ClusterFMetalTiers, cc-P53; no 120 cap). The 300-era rows
            // ran from Platinum 85-115 to Celestial 280-340 and put Adamantium and Celestial wholly above the 200 cap.
            BulkMaterialType.Platinum or BulkMaterialType.Toxic or BulkMaterialType.Blaze or BulkMaterialType.Frost
                or BulkMaterialType.Obsidian or BulkMaterialType.Mythril or BulkMaterialType.Adamantium
                or BulkMaterialType.Celestial => (ClusterFMetalTiers.PostValoriteRequiredSkill(mat) - 20.0,
                    ClusterFMetalTiers.PostValoriteRequiredSkill(mat) + 20.0),
            _                           => (  0.0,   55.0),
        };

        if (exceptional)
        {
            min += 10.0;
            // No 120 cap for post-Valorite - extended skill goes well above 120
            max = IsPostValorite(mat) ? max + 10.0 : Math.Min(120.0, max + 10.0);
        }

        return (min, max);
    }

    // -- Helpers ---------------------------------------------------------------

    /// <summary>
    /// Completes a BOD turn-in programmatically (e.g. from the SmithGuildBook gump).
    /// Returns true if the turn-in succeeded; false if incomplete or invalid.
    /// </summary>
    public static bool TurnInBOD(PlayerMobile pm, Item bod)
    {
        if (bod is not (SmallSmithBOD or LargeSmithBOD)) return false;
        if (pm.Account is not IAccount acct) return false;
        if (!ClusterFGuildSystem.IsJoined(pm, "smithing")) return false;

        var complete = bod switch
        {
            SmallBOD s => s.Complete,
            LargeBOD l => l.Complete,
            _          => false,
        };
        if (!complete) return false;
        if (bod is LargeSmithBOD && !CanTurnInLarge(pm)) return false;

        // cc-P55 Part H: bank or cash out by the member's setting; "ask each time" shows the choice and counts as handled.
        return ClusterFSmithBODPayout.Begin(pm, bod, null) != SmithTurnInResult.Refused;
    }

    public const string LargeSkillRefusal =
        "Large orders need 70.1 Blacksmithy. Any blacksmith will take this one.";

    /// <summary>70.1 Blacksmithy, or an active Dev Testing Crystal: the same gate as requesting a large order.</summary>
    public static bool CanTurnInLarge(PlayerMobile pm) => MeetsLargeSkill(pm);

    /// <summary>
    /// Counts SmallSmithBOD and LargeSmithBOD items anywhere in the player's pack,
    /// including inside containers such as the SmithGuildBook.
    /// </summary>
    public static (int small, int large) CountBODs(PlayerMobile pm)
    {
        if (pm.Backpack == null) return (0, 0);
        var small = 0; var large = 0;
        CountBODsIn(pm.Backpack.Items, ref small, ref large);
        return (small, large);
    }

    private static void CountBODsIn(List<Item> items, ref int small, ref int large)
    {
        foreach (var item in items)
        {
            if      (item is SmallSmithBOD) small++;
            else if (item is LargeSmithBOD) large++;
            else if (item is Container c)
                CountBODsIn(c.Items, ref small, ref large);
        }
    }

    private static int MaterialTier(BulkMaterialType mat) => mat switch
    {
        BulkMaterialType.DullCopper => 1,
        BulkMaterialType.ShadowIron => 2,
        BulkMaterialType.Copper     => 3,
        BulkMaterialType.Bronze     => 4,
        BulkMaterialType.Gold       => 5,
        BulkMaterialType.Agapite    => 6,
        BulkMaterialType.Verite     => 7,
        BulkMaterialType.Valorite   => 8,
        // Post-Valorite tiers 9-16
        BulkMaterialType.Platinum   => 9,
        BulkMaterialType.Toxic      => 10,
        BulkMaterialType.Blaze      => 11,
        BulkMaterialType.Frost      => 12,
        BulkMaterialType.Obsidian   => 13,
        BulkMaterialType.Mythril    => 14,
        BulkMaterialType.Adamantium => 15,
        BulkMaterialType.Celestial  => 16,
        _                           => 0,
    };
}
