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
//     patch does not exist (cc-P32 Part B).
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

        // -- Reward
        var (seals, standing, skillChecks) = ComputeGuildReward(dropped);

        var guild = ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(pm.Serial);
        guild.AddReputation("smithing", standing);
        guild.AddCurrency("smithing", seals);

        pm.SendMessage(0x44,
            $"Society of Smiths: +{standing} standing, +{seals} Smithing Seal{(seals == 1 ? "" : "s")}.");

        // Skill checks - each is a real gain-eligible roll scaled to BOD difficulty
        var (skillMin, skillMax) = GetSkillRange(dropped);
        for (var i = 0; i < skillChecks; i++)
            pm.CheckSkill(SkillName.Blacksmith, skillMin, skillMax);

        from.SendSound(0x3D);
        SayTo(from, "Well done. The Society thanks you for your craft.");
        dropped.Delete();
        return true;
    }

    // -- Reward calculation ----------------------------------------------------
    // Seals are derived from the vanilla gold value (SmithRewardCalculator.ComputeGold)
    // divided by SealDivisor.  This naturally captures quantity (10/15/20), material
    // tier, exceptional flag, AND item type (ringmail vs platemail vs weapons) without
    // a hand-coded table.  The +/-10 % randomisation in ComputeGold also gives slight
    // turn-in variation so the same BOD doesn't always yield the exact same seals.
    //
    // Post-Valorite materials are not in the vanilla gold table so we compute a
    // Valorite-equivalent gold value and scale it by PostValoriteMultiplier.
    //
    // cc-P42 Part G2 (Chase, 2026-10-03): about 3x Seals and 2x standing across the board, still no gold. Seals are
    // the divisor-400 count (floor 1) times SealMultiplier, so every order pays exactly three times what it did and
    // exceptional and large stay worth more; standing is the old table times StandingMultiplier.
    //
    // Representative values, computed from the formulas below and pinned's gold table (Rewards.cs m_GoldTable,
    // ComputeGold's gold * 9 / 10 to gold * 10 / 9), shown as the range that randomisation gives (Seals, standing):
    //   Iron small regular   qty10            ->    3 Seals,      50 standing   (was 1, 25)
    //   Iron small exc       qty20            ->    3 Seals,     120 standing   (was 1, 60)
    //   DullCopper small exc qty20            ->    6 Seals,     150 standing   (was 2, 75)
    //   Valorite small reg   qty20            ->   27-33 Seals,  240 standing   (was 9-11, 120)
    //   Valorite small exc   qty20            ->   81-99 Seals,  360 standing   (was 27-33, 180)
    //   Platinum small exc   qty20            ->  120-150 Seals, 390 standing   (was 40-50, 195)
    //   Celestial small exc  qty20            ->  405-501 Seals, 600 standing   (was 135-167, 300)
    //   Large Valorite exc   qty20 (plate)    -> 1350-1668 Seals, 620 standing  (was 450-556, 310)
    //   Large Platinum exc   qty20 (plate)    -> 2025-2499 Seals, 660 standing  (was 675-833, 330)
    //   Large Celestial exc  qty20 (plate)    -> 6750-8334 Seals, 940 standing  (was 2250-2778, 470)
    // The table and how it was computed: shard-migration notes/cc-P42-defect-batch-3.md, Part G2.

    private const int SealDivisor = 400;
    internal const int SealMultiplier = 3;
    internal const int StandingMultiplier = 2;

    /// <summary>Seals for an order worth <paramref name="gold"/> (its gold equivalent): floor 1, times SealMultiplier.</summary>
    internal static int SealsForGold(int gold) =>
        Math.Max(1, (int)Math.Round(gold / (double)SealDivisor)) * SealMultiplier;

    // Returns the post-Valorite multiplier over a Valorite-equivalent gold value.
    // Multipliers produce a smooth curve: Valorite large exc ~ 500 seals,
    // Celestial large exc ~ 2 500 seals.
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

    // For post-Valorite BODs, ComputeGold() returns iron-level gold because the vanilla
    // gold table only covers None-Valorite (indices 0-8).  We instead compute the
    // Valorite-equivalent gold and scale it by the tier multiplier.
    internal static int GoldEquivalentForSeals(SmallSmithBOD small)
    {
        if (!IsPostValorite(small.Material))
            return small.ComputeGold();

        var valGold = SmithRewardCalculator.Instance.ComputeGold(
            small.AmountMax, small.RequireExceptional, BulkMaterialType.Valorite, 1, small.Type);
        return (int)(valGold * PostValoriteMultiplier(small.Material));
    }

    internal static int GoldEquivalentForSeals(LargeSmithBOD large)
    {
        if (!IsPostValorite(large.Material))
            return large.ComputeGold();

        var valGold = SmithRewardCalculator.Instance.ComputeGold(
            large.AmountMax, large.RequireExceptional, BulkMaterialType.Valorite,
            large.Entries.Length, large.Entries[0].Details.Type);
        return (int)(valGold * PostValoriteMultiplier(large.Material));
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
            var seals = SealsForGold(gold);

            return (seals, standing, 1);
        }

        if (deed is LargeSmithBOD large)
        {
            var tier     = MaterialTier(large.Material);
            var standing = (tier == 0 ? 100 : 150 + tier * 20) * StandingMultiplier;
            var checks   = tier >= 5 ? 3 : 2;   // Gold+ large earns an extra skill check

            var gold  = GoldEquivalentForSeals(large);
            var seals = SealsForGold(gold);

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

        var (seals, standing, skillChecks) = ComputeGuildReward(bod);
        var guild = ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(pm.Serial);
        guild.AddReputation("smithing", standing);
        guild.AddCurrency("smithing", seals);

        pm.SendMessage(0x44,
            $"Society of Smiths: +{standing} standing, +{seals} Smithing Seal{(seals == 1 ? "" : "s")}.");

        var (skillMin, skillMax) = GetSkillRange(bod);
        for (var i = 0; i < skillChecks; i++)
            pm.CheckSkill(SkillName.Blacksmith, skillMin, skillMax);

        pm.PlaySound(0x3D);
        bod.Delete();
        return true;
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
