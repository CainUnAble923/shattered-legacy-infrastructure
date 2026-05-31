using System;
using System.Collections.Generic;
using Server;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Items;
using Server.Mobiles;

namespace Server.Mobiles;

// ─────────────────────────────────────────────────────────────────────────────
// ClusterF Smith BOD System — Phase 4C-i
//
// Merges the vanilla BOD system into the Society of Smiths guild so there
// is exactly one kind of Smith BOD:
//
//   • Regular Blacksmith NPCs no longer issue or accept BODs.
//     Players who try to get a BOD from a random smith are redirected to the
//     Guildmaster.
//
//   • BlacksmithGuildmaster is the sole source and turn-in point.
//     Generation is cap-based (3 small / 1 large concurrent), no cooldown.
//     Large BODs require Journeyman rank (5 000 standing) + 70.1 Blacksmithy.
//
//   • Turn-in gives Smithing Seals + a skill check (no vanilla item rewards).
//     Skill check range is scaled to material tier + exceptional requirement
//     so BODs always push skill gain in the right bracket.
//
// Ore gating hook:
//   TODO Phase 4C-ore: replace skill-only material check with ore knowledge
//   gate (MiningGuildOreKnowledge.KnowsOre) once Mining Guild data exists.
//   Skill-threshold gating is already correct for Phase 4C-i.
//
// Replaces ClusterFSmithBODRewards.cs — that file's reward logic now lives
// in BlacksmithGuildmaster.OnDragDrop / ComputeGuildReward below.
// ─────────────────────────────────────────────────────────────────────────────

// ── Note: Blacksmith.cs (server file, not customization) is patched separately
// to disable BOD generation on regular Blacksmith NPCs.  The four overrides
// (SupportsBulkOrders, CreateBulkOrder, IsValidBulkOrder, GetNextBulkOrder)
// are replaced with no-op returns so only the Guildmaster issues BODs.
// See patches/Blacksmith_DisableBODs.patch in this repo for the diff.

// ── BlacksmithGuildmaster — guild BOD generation and turn-in ─────────────────

public partial class BlacksmithGuildmaster
{
    private const int SmallBODCap = 3; // max concurrent small BODs in pack
    private const int LargeBODCap = 1; // max concurrent large BODs in pack

    // ── Generation ───────────────────────────────────────────────────────────

    public override bool SupportsBulkOrders(Mobile from)
    {
        if (from is not PlayerMobile pm) return false;
        if (pm.Account is not IAccount acct) return false;
        return ClusterFGuildSystem.IsJoined(acct, "smithing");
    }

    // No cooldown — generation is throttled by the active-BOD cap instead.
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

    // ── Book-facing static BOD creation ───────────────────────────────────────
    // Callable from SmithGuildBook without a Guildmaster NPC reference.
    // Contains the same generation logic as the old CreateBulkOrder body.

    public static Item? TryCreateBOD(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return null;
        if (!ClusterFGuildSystem.IsJoined(acct, "smithing"))
        {
            pm.SendMessage(0x22, "You must join the Society of Smiths first.");
            return null;
        }

        var bypass   = DevTestingCrystal.IsActive(pm);
        var skill    = pm.Skills.Blacksmith.Base;
        var data     = ClusterFAccountPersistence.GetOrCreate(acct);
        var standing = data.GetReputation("smithing");

        var (smallCount, largeCount) = CountBODs(pm);

        // Large BOD: Journeyman rank + 70.1 skill, random chance same as vanilla
        var qualifiesLarge = bypass || (standing >= 5_000 && skill >= 70.1);
        var rollsLarge     = skill >= 70.1 && (skill - 40.0) / 300.0 > Utility.RandomDouble();

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

        // Small BOD
        if (smallCount >= SmallBODCap)
        {
            pm.SendMessage(0x22,
                $"You have {SmallBODCap} active orders. Complete some before requesting more.");
            return null;
        }

        var bod = SmallSmithBOD.CreateRandomFor(pm);
        if (bod == null)
            pm.SendMessage(0x22,
                "There are no suitable orders for your skill level right now. " +
                "Practice your craft and return.");
        return bod;
    }

    // ── Forced large BOD creation (guild book "Request Large" button) ─────────

    /// <summary>
    /// Creates a large BOD directly, skipping the small/large random roll.
    /// Requires Journeyman rank (5 000 standing) + 70.1 Blacksmithy.
    /// Respects the large-BOD cap (max 1 active).
    /// </summary>
    public static Item? TryCreateLargeBOD(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return null;
        if (!ClusterFGuildSystem.IsJoined(acct, "smithing"))
        {
            pm.SendMessage(0x22, "You must join the Society of Smiths first.");
            return null;
        }

        var bypass   = DevTestingCrystal.IsActive(pm);
        var skill    = pm.Skills.Blacksmith.Base;
        var data     = ClusterFAccountPersistence.GetOrCreate(acct);
        var standing = data.GetReputation("smithing");

        if (!bypass && standing < 5_000)
        {
            pm.SendMessage(0x22, "You must achieve Journeyman rank (5,000 standing) to request large orders.");
            return null;
        }

        if (!bypass && skill < 70.1)
        {
            pm.SendMessage(0x22, "You need at least 70.1 Blacksmithy to request large orders.");
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

    // ── Turn-in ───────────────────────────────────────────────────────────────

    public override bool IsValidBulkOrder(Item item) => item is SmallSmithBOD or LargeSmithBOD;

    /// <summary>
    /// Intercepts BOD turn-ins before BaseVendor fires vanilla item/gold rewards.
    /// Smith BODs are handled entirely here; non-BOD drops fall through to base.
    /// </summary>
    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        // ── Commission turn-in — check before the BOD type filter ─────────────
        // Commission items are weapons/armor, so they would otherwise fall through
        // to base.OnDragDrop.  Intercept them here for guild members.
        if (from is PlayerMobile pmComm && pmComm.Account is IAccount acctComm
            && ClusterFGuildSystem.IsJoined(acctComm, "smithing"))
        {
            var commData       = ClusterFAccountPersistence.GetOrCreate(acctComm);
            var matchedComm    = SmithCommissionSystem.FindMatch(commData, dropped);
            if (matchedComm != null)
            {
                SmithCommissionSystem.Complete(pmComm, matchedComm, dropped);
                SayTo(from, "Excellent work. The commissioner will be well pleased.");
                return true;
            }
        }

        // Not a BOD at all — let base handle it (vendor sale, etc.)
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
            || !ClusterFGuildSystem.IsJoined(acct, "smithing"))
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

        // ── Reward
        var (seals, standing, skillChecks) = ComputeGuildReward(dropped);

        var playerData = ClusterFAccountPersistence.GetOrCreate(acct);
        playerData.AddReputation("smithing", standing);
        playerData.AddCurrency("smithing", seals);

        pm.SendMessage(0x44,
            $"Society of Smiths: +{standing} standing, +{seals} Smithing Seal{(seals == 1 ? "" : "s")}.");

        // Skill checks — each is a real gain-eligible roll scaled to BOD difficulty
        var (skillMin, skillMax) = GetSkillRange(dropped);
        for (var i = 0; i < skillChecks; i++)
            pm.CheckSkill(SkillName.Blacksmith, skillMin, skillMax);

        from.SendSound(0x3D);
        SayTo(from, "Well done. The Society thanks you for your craft.");
        dropped.Delete();
        return true;
    }

    // ── Reward calculation ────────────────────────────────────────────────────
    // Seals are derived from the vanilla gold value (SmithRewardCalculator.ComputeGold)
    // divided by SealDivisor.  This naturally captures quantity (10/15/20), material
    // tier, exceptional flag, AND item type (ringmail vs platemail vs weapons) without
    // a hand-coded table.  The ±10 % randomisation in ComputeGold also gives slight
    // turn-in variation so the same BOD doesn't always yield the exact same seals.
    //
    // Post-Valorite materials are not in the vanilla gold table so we compute a
    // Valorite-equivalent gold value and scale it by PostValoriteMultiplier.
    //
    // Representative values at divisor 400:
    //   Iron small regular  qty10           →    1 seal  (floor)
    //   DullCopper exc      qty20           →    4 seals
    //   Valorite small reg  qty20           →   10 seals
    //   Valorite small exc  qty20           →  ~30 seals
    //   Platinum small exc  qty20           →  ~45 seals
    //   Celestial small exc qty20           → ~150 seals
    //   Large Valorite exc  qty20           → ~500 seals
    //   Large Platinum exc  qty20           → ~750 seals
    //   Large Celestial exc qty20           → ~2 500 seals

    private const int SealDivisor = 400;

    // Returns the post-Valorite multiplier over a Valorite-equivalent gold value.
    // Multipliers produce a smooth curve: Valorite large exc ≈ 500 seals,
    // Celestial large exc ≈ 2 500 seals.
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
    // gold table only covers None–Valorite (indices 0–8).  We instead compute the
    // Valorite-equivalent gold and scale it by the tier multiplier.
    private static int GoldEquivalentForSeals(SmallSmithBOD small)
    {
        if (!IsPostValorite(small.Material))
            return small.ComputeGold();

        var valGold = SmithRewardCalculator.Instance.ComputeGold(
            small.AmountMax, small.RequireExceptional, BulkMaterialType.Valorite, 1, small.Type);
        return (int)(valGold * PostValoriteMultiplier(small.Material));
    }

    private static int GoldEquivalentForSeals(LargeSmithBOD large)
    {
        if (!IsPostValorite(large.Material))
            return large.ComputeGold();

        var valGold = SmithRewardCalculator.Instance.ComputeGold(
            large.AmountMax, large.RequireExceptional, BulkMaterialType.Valorite,
            large.Entries.Length, large.Entries[0].Details.Type);
        return (int)(valGold * PostValoriteMultiplier(large.Material));
    }

    private static (int seals, int standing, int skillChecks) ComputeGuildReward(Item deed)
    {
        if (deed is SmallSmithBOD small)
        {
            var tier     = MaterialTier(small.Material);
            var standing = small.RequireExceptional
                ? 60  + tier * 15
                : tier == 0 ? 25 : 40 + tier * 10;

            var gold  = GoldEquivalentForSeals(small);
            var seals = Math.Max(1, (int)Math.Round(gold / (double)SealDivisor));

            return (seals, standing, 1);
        }

        if (deed is LargeSmithBOD large)
        {
            var tier     = MaterialTier(large.Material);
            var standing = tier == 0 ? 100 : 150 + tier * 20;
            var checks   = tier >= 5 ? 3 : 2;   // Gold+ large earns an extra skill check

            var gold  = GoldEquivalentForSeals(large);
            var seals = Math.Max(1, (int)Math.Round(gold / (double)SealDivisor));

            return (seals, standing, checks);
        }

        return (0, 0, 0);
    }

    // ── Skill check range ─────────────────────────────────────────────────────

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
            // Post-Valorite — extended skill ranges (no 120 cap; extended skill can exceed 300)
            BulkMaterialType.Platinum   => ( 85.0,  115.0),
            BulkMaterialType.Toxic      => ( 95.0,  130.0),
            BulkMaterialType.Blaze      => (110.0,  150.0),
            BulkMaterialType.Frost      => (130.0,  175.0),
            BulkMaterialType.Obsidian   => (155.0,  205.0),
            BulkMaterialType.Mythril    => (180.0,  235.0),
            BulkMaterialType.Adamantium => (230.0,  285.0),
            BulkMaterialType.Celestial  => (280.0,  340.0),
            _                           => (  0.0,   55.0),
        };

        if (exceptional)
        {
            min += 10.0;
            // No 120 cap for post-Valorite — extended skill goes well above 120
            max = IsPostValorite(mat) ? max + 10.0 : Math.Min(120.0, max + 10.0);
        }

        return (min, max);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Completes a BOD turn-in programmatically (e.g. from the SmithGuildBook gump).
    /// Returns true if the turn-in succeeded; false if incomplete or invalid.
    /// </summary>
    public static bool TurnInBOD(PlayerMobile pm, Item bod)
    {
        if (bod is not (SmallSmithBOD or LargeSmithBOD)) return false;
        if (pm.Account is not IAccount acct) return false;
        if (!ClusterFGuildSystem.IsJoined(acct, "smithing")) return false;

        var complete = bod switch
        {
            SmallBOD s => s.Complete,
            LargeBOD l => l.Complete,
            _          => false,
        };
        if (!complete) return false;

        var (seals, standing, skillChecks) = ComputeGuildReward(bod);
        var playerData = ClusterFAccountPersistence.GetOrCreate(acct);
        playerData.AddReputation("smithing", standing);
        playerData.AddCurrency("smithing", seals);

        pm.SendMessage(0x44,
            $"Society of Smiths: +{standing} standing, +{seals} Smithing Seal{(seals == 1 ? "" : "s")}.");

        var (skillMin, skillMax) = GetSkillRange(bod);
        for (var i = 0; i < skillChecks; i++)
            pm.CheckSkill(SkillName.Blacksmith, skillMin, skillMax);

        pm.PlaySound(0x3D);
        bod.Delete();
        return true;
    }

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
        // Post-Valorite tiers 9–16
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
