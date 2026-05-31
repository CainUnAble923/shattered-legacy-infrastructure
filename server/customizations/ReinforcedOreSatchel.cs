using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Craft;
using Server.Mobiles;

namespace Server.Items;

/// <summary>
/// Shattered Legacy — Reinforced Ore Satchel (Tier 2).
///
/// Upgrade from Compact Ore Satchel (Tier 1) via the Miners' Compact Liaison
/// (Upgrade Satchel view).
///
/// Properties:
///   - 55% weight reduction on contents
///   - 600-stone content capacity (270 effective after reduction)
///   - Accepts: same as T1 (all ore/ingot types, granite)
///   - Blessed — not dropped on death
///
/// Tier 2 Feature — Smelt All:
///   Right-click context menu "Smelt Metal" option.
///   Requires the player to be within 2 tiles of a forge.
///   Smelts all BaseOre in the satchel using the player's Mining skill.
///   Ore whose required skill exceeds the player's Mining is left untouched.
///   Ingots are deposited back into the satchel; any overflow goes to the backpack.
///   Conversion rate: 2 ore → 1 ingot (Amount / 2, minimum 1), guaranteed on success.
///
/// Restoration key: compact.satchel_t2
/// </summary>
[SerializationGenerator(0, false)]
public partial class ReinforcedOreSatchel : CompactOreSatchel
{
    private const int TierHue = 0x8A5C; // bronze/copper tint — matches T2 pickaxe

    protected override int TierWeightReductionPct => 55;
    protected override int TierMaxContentWeight    => 600;
    public    override double DefaultWeight        => 3.5;

    [Constructible]
    public ReinforcedOreSatchel() : base()
    {
        Hue  = TierHue;
        Name = "Reinforced Ore Satchel";
    }

    // ── Ore smelt difficulty ──────────────────────────────────────────────────
    // Mirrors the skill requirements from Mining.cs so the same rules apply.
    // Extended ores (Platinum → Celestial) all require GM Mining (100.0).

    private static double SmeltDifficulty(CraftResource r) => r switch
    {
        CraftResource.Iron        =>   0.0,
        CraftResource.DullCopper  =>  65.0,
        CraftResource.ShadowIron  =>  70.0,
        CraftResource.Copper      =>  75.0,
        CraftResource.Bronze      =>  80.0,
        CraftResource.Gold        =>  85.0,
        CraftResource.Agapite     =>  90.0,
        CraftResource.Verite      =>  95.0,
        CraftResource.Valorite    =>  99.0,
        CraftResource.Platinum    => 100.0,
        CraftResource.Toxic       => 100.0,
        CraftResource.Blaze       => 100.0,
        CraftResource.Frost       => 100.0,
        CraftResource.Obsidian    => 100.0,
        CraftResource.Mythril     => 100.0,
        CraftResource.Adamantium  => 100.0,
        CraftResource.Celestial   => 100.0,
        _                         =>   0.0,
    };

    // ── Context menu ──────────────────────────────────────────────────────────

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from.Alive)
        {
            var canSmelt = IsChildOf(from.Backpack) && HasSmeltableOre();
            list.Add(new SmeltAllOreEntry(canSmelt));
        }
    }

    private bool HasSmeltableOre()
    {
        foreach (var item in Items)
            if (item is BaseOre) return true;
        return false;
    }

    // ── Smelt All ─────────────────────────────────────────────────────────────

    public void SmeltAllOre(Mobile from)
    {
        if (!from.CheckAlive()) return;

        if (!IsChildOf(from.Backpack))
        {
            from.SendMessage("The satchel must be in your backpack to smelt.");
            return;
        }

        // Forge proximity check (same as SmithGuildSalvageBag — range 2)
        DefBlacksmithy.CheckAnvilAndForge(from, 2, out _, out var hasForge);
        if (!hasForge)
        {
            from.SendLocalizedMessage(1044265); // You must be near a forge.
            return;
        }

        // Gather all ore currently in the satchel
        var ores = new List<BaseOre>();
        foreach (var item in Items)
            if (item is BaseOre ore) ores.Add(ore);

        if (ores.Count == 0)
        {
            from.SendMessage("There is no ore in the satchel to smelt.");
            return;
        }

        var mining      = from.Skills.Mining.Value;
        var smelted     = 0;
        var skipped     = 0;
        var totalIngots = 0;

        foreach (var ore in ores)
        {
            var difficulty = SmeltDifficulty(ore.Resource);

            if (mining < difficulty)
            {
                // Skill too low for this ore type — leave it in the satchel.
                skipped++;
                continue;
            }

            // Skill requirement met → guaranteed smelt (no random failure).
            // Standard conversion rate: 2 ore = 1 ingot, minimum 1.
            var ingot   = ore.GetIngot();
            ingot.Amount = Math.Max(1, ore.Amount / 2);
            ore.Delete();

            // Return ingot to the satchel; fall back to backpack if satchel is full.
            if (!TryDropItem(from, ingot, false))
                from.AddToBackpack(ingot);

            totalIngots += ingot.Amount;
            smelted++;
        }

        // Sound effect — forge/anvil strike
        from.PlaySound(0x2A);
        from.PlaySound(0x240);

        if (smelted == 0 && skipped > 0)
        {
            from.SendMessage(
                "Your Mining skill is too low to smelt any of the ore in the satchel.");
        }
        else if (skipped > 0)
        {
            from.SendMessage(
                $"You smelt {smelted} ore pile(s) into {totalIngots} ingot(s). " +
                $"{skipped} pile(s) remain — your Mining skill is too low for those ore types.");
        }
        else
        {
            from.SendMessage(
                $"You smelt {smelted} ore pile(s) into {totalIngots} ingot(s).");
        }
    }

    // ── Context menu entry ────────────────────────────────────────────────────

    private sealed class SmeltAllOreEntry : ContextMenuEntry
    {
        // Cliloc 6277 = "Smelt Metal" in the UO client context menu.
        public SmeltAllOreEntry(bool enabled) : base(6277) => Enabled = enabled;

        public override void OnClick(Mobile from, IEntity target)
        {
            if (from.CheckAlive() && target is ReinforcedOreSatchel { Deleted: false } satchel)
                satchel.SmeltAllOre(from);
        }
    }

    // ── Serialization (v0 — no custom fields) ─────────────────────────────────

    private void Deserialize(IGenericReader reader, int version) { }
}
