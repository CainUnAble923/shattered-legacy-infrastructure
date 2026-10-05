using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Craft;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

/// <summary>
/// Shattered Legacy — Compact Ore Satchel (Tier 1).
///
/// A Miners' Compact logistics item issued to members.
///
/// Properties:
///   - 50% weight reduction on contents
///   - 400-stone content capacity (200 effective after reduction)
///   - Accepts: ore, ingots, granite, saltpeter
///   - Auto-routes newly mined ore via CompactOreSatchelRoutingHook (Mining.Give override)
///   - Blessed — not dropped on death
///
/// Acquisition:
///   Issued by the Compact Liaison when a member first joins the Compact.
///   Restorable via MinersCompactLiaisonGump (ReplaceKit view, 5 vouchers).
///
/// Tier Progression (all inherit from this class):
///   T1  Compact Ore Satchel        — issued on join, 50% reduction, 400 max
///   T2  Reinforced Ore Satchel     — 55% reduction, 600 max  (ReinforcedOreSatchel.cs)
///   T3  Surveyor's Ore Satchel     — 60% reduction, 800 max  (SurveyorsSatchel.cs)
///   T4  Deepdelver's Ore Satchel   — 65% reduction, 1000 max (DeepdelversSatchel.cs)
///   T5  Master Expedition Satchel  — 70% reduction, 1200 max (MasterExpeditionSatchel.cs)
///
/// Each higher tier is obtained via upgrade at the Miners' Compact Liaison (Upgrade Satchel view).
/// Upgrades consume the current tier satchel and pay vouchers + ingots + gold.
/// Higher tiers are restorable through the Liaison.
///
/// cc-P55 (Chase 2026-10-05):
///   Part D (D71): Smelt Ore lives here for every tier that has it (T2 to T5, CanSmeltOre); T1 has none. It used to be
///     on the Reinforced satchel only, so upgrading to T3 lost it. No field or base class changed, so saved satchels
///     of every tier load as they are.
///   Part B (D70): each pile smelts exactly as at a forge, through the forge's own step (pinned Ore.cs, made reachable by
///     Ore-smelt-at-forge.patch): the same ratio by pile size, the same Mining check and loss on failure, the same
///     messages, and still only within 2 tiles of a forge. Before, every pile gave Amount / 2 ingots with no roll.
///   Part C (D74): the entry reads "Smelt" (cliloc 3006143). It used to send 6277, which the client shows as
///     "Salvage Ingots".
///   Part E (D72): each tier wears the hue of its rung on the ore ladder, read from CraftResources (TierMetal).
/// </summary>
[SerializationGenerator(0, false)]
public partial class CompactOreSatchel : Container
{
    // ── Item type sets accepted by this satchel ───────────────────────────────

    private static readonly Type[] _acceptedTypes =
    {
        // Raw ore (vanilla)
        typeof(IronOre), typeof(DullCopperOre), typeof(ShadowIronOre),
        typeof(CopperOre), typeof(BronzeOre), typeof(GoldOre),
        typeof(AgapiteOre), typeof(VeriteOre), typeof(ValoriteOre),

        // Extended ore (Shattered Legacy custom)
        typeof(PlatinumOre), typeof(ToxicOre), typeof(BlazeOre),
        typeof(FrostOre), typeof(ObsidianOre), typeof(MythrilOre),
        typeof(AdamantiumOre), typeof(CelestialOre),

        // Smelted ingots (vanilla)
        typeof(IronIngot), typeof(DullCopperIngot), typeof(ShadowIronIngot),
        typeof(CopperIngot), typeof(BronzeIngot), typeof(GoldIngot),
        typeof(AgapiteIngot), typeof(VeriteIngot), typeof(ValoriteIngot),

        // Extended ingots
        typeof(PlatinumIngot), typeof(ToxicIngot), typeof(BlazeIngot),
        typeof(FrostIngot), typeof(ObsidianIngot), typeof(MythrilIngot),
        typeof(AdamantiumIngot), typeof(CelestialIngot),

        // Mining bonus resources
        typeof(Granite),
    };

    // ── Container configuration ───────────────────────────────────────────────

    private const int SatchelGraphic = 0xA272; // ore satchel art

    // Override in subclasses to change tier stats without duplicating logic.
    protected virtual int TierWeightReductionPct => 50;
    protected virtual int TierMaxContentWeight    => 400; // before reduction → 200 stones effective

    // -- Tier hue (cc-P55 Part E) ----------------------------------------------

    /// <summary>The metal whose hue this tier wears: T1 Iron (no hue), T2 Gold, T3 Verite, T4 Valorite, T5 Platinum.</summary>
    public virtual CraftResource TierMetal => CraftResource.Iron;

    /// <summary>This tier's hue, read from the metal's own entry so the two stay in step.</summary>
    public int TierHue => CraftResources.GetHue(TierMetal);

    /// <summary>The hue this tier was given before cc-P55; a saved satchel still wearing it takes TierHue on load.</summary>
    protected virtual int LegacyTierHue => 0x0482;

    /// <summary>cc-P55 Part D: tiers 2 to 5 have Smelt Ore.</summary>
    public virtual bool CanSmeltOre => false;

    public override int DefaultGumpID  => 0x3C;    // generic bag gump
    public override double DefaultWeight => 3.0;
    public override int DefaultMaxWeight => TierMaxContentWeight;

    // ── Constructor ───────────────────────────────────────────────────────────

    [Constructible]
    public CompactOreSatchel() : base(SatchelGraphic)
    {
        Hue       = TierHue;
        Name      = "Compact Ore Satchel";
        LootType  = LootType.Blessed;
    }

    // ── Weight reduction ──────────────────────────────────────────────────────

    public override int GetTotal(TotalType type)
    {
        var total = base.GetTotal(type);
        if (type == TotalType.Weight)
            total -= total * TierWeightReductionPct / 100;
        return total;
    }

    public override void UpdateTotal(Item sender, TotalType type, int delta)
    {
        InvalidateProperties();
        base.UpdateTotal(sender, type, delta);
    }

    // Propagate weight change to the carrying mobile so their burden bar updates.
    private void InvalidateCarrierWeight()
    {
        if (RootParent is Mobile m)
            m.UpdateTotals();
    }

    public override void OnItemAdded(Item item)
    {
        base.OnItemAdded(item);
        InvalidateCarrierWeight();
    }

    public override void OnItemRemoved(Item item)
    {
        base.OnItemRemoved(item);
        InvalidateCarrierWeight();
    }

    // ── Item acceptance filter ─────────────────────────────────────────────────

    /// <summary>Returns true if the item type is accepted by any Compact Ore Satchel.</summary>
    public static bool Accepts(Item item)
    {
        if (item == null) return false;
        var t = item.GetType();
        foreach (var accepted in _acceptedTypes)
            if (t == accepted || t.IsSubclassOf(accepted))
                return true;
        return false;
    }

    public override bool CheckHold(Mobile m, Item item, bool message, bool checkItems, int plusItems, int plusWeight)
    {
        if (!Accepts(item))
        {
            if (message)
                m.SendMessage("The Compact Ore Satchel only holds ore, ingots, granite, and saltpeter.");
            return false;
        }
        return base.CheckHold(m, item, message, checkItems, plusItems, plusWeight);
    }

    // ── Properties display ────────────────────────────────────────────────────

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1072210, TierWeightReductionPct); // Weight Reduction: ~1_PERCENTAGE~%
        list.Add(1060742, $"{TotalWeight}\t{TierMaxContentWeight}"); // contents: ~1_COUNT~/~2_MAXCOUNT~ stones
    }

    // -- Smelt Ore (cc-P55 Parts B, C, D) --------------------------------------

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (CanSmeltOre && from.Alive)
            list.Add(new SmeltOreEntry(IsChildOf(from.Backpack) && HasOre()));
    }

    private bool HasOre()
    {
        foreach (var item in Items)
            if (item is BaseOre) return true;
        return false;
    }

    /// <summary>
    /// Smelts every pile in the satchel at the nearby forge, one pile at a time through the forge's own step
    /// (BaseOre.InternalTarget.SmeltAt), with the ingots going back into the satchel. Ends with one summary line.
    /// </summary>
    public void SmeltAllOre(Mobile from)
    {
        if (!CanSmeltOre || !from.CheckAlive()) return;

        if (!IsChildOf(from.Backpack))
        {
            from.SendMessage("The satchel must be in your backpack to smelt.");
            return;
        }

        var forge = FindForge(from, 2);
        if (forge == null)
        {
            from.SendLocalizedMessage(1044265); // You must be near a forge.
            return;
        }

        var ores = new List<BaseOre>();
        foreach (var item in Items)
            if (item is BaseOre ore) ores.Add(ore);

        if (ores.Count == 0)
        {
            from.SendMessage("There is no ore in the satchel to smelt.");
            return;
        }

        var smelted = 0;
        var failed  = 0;
        var refused = 0;
        var ingots  = 0;

        foreach (var ore in ores)
        {
            var amountBefore = ore.Amount;
            var itemIdBefore = ore.ItemID;
            var ingotsBefore = CountIngots(from);

            new BaseOre.InternalTarget(ore) { IngotDestination = this }.SmeltAt(from, forge);

            var made = CountIngots(from) - ingotsBefore;
            if (made > 0)
            {
                smelted++;
                ingots += made;
            }
            else if (ore.Deleted || ore.Amount != amountBefore || ore.ItemID != itemIdBefore)
            {
                failed++; // the forge burned part of the pile away
            }
            else
            {
                refused++; // the forge would not take it (Mining too low, or one small ore)
            }
        }

        from.PlaySound(0x2A);

        from.SendMessage(0x59,
            $"Smelt Ore: {ores.Count} pile{(ores.Count == 1 ? "" : "s")}: {smelted} smelted into {ingots} " +
            $"ingot{(ingots == 1 ? "" : "s")}, {failed} failed, {refused} left unsmelted.");
    }

    private static int CountIngots(Mobile from) => from.Backpack?.GetAmount(typeof(BaseIngot)) ?? 0;

    /// <summary>
    /// A forge within range the way DefBlacksmithy.CheckAnvilAndForge finds one (pinned DefBlacksmithy.cs:30-90): a forge
    /// item, or a forge tile as a StaticTarget. The forge's own step still decides whether it is a forge.
    /// </summary>
    public static object? FindForge(Mobile from, int range)
    {
        var map = from.Map;
        if (map == null || map == Map.Internal) return null;

        foreach (var item in map.GetItemsInRange(from.Location, range))
        {
            var isForge = item.GetType().IsDefined(typeof(ForgeAttribute), false) ||
                          item.ItemID is 4017 or >= 6522 and <= 6569 or 11736;

            if (isForge && from.Z + 16 >= item.Z && item.Z + 16 >= from.Z && from.InLOS(item))
                return item;
        }

        for (var x = -range; x <= range; ++x)
        {
            for (var y = -range; y <= range; ++y)
            {
                foreach (var tile in map.Tiles.GetStaticAndMultiTiles(from.X + x, from.Y + y))
                {
                    if (tile.ID is not (4017 or >= 6522 and <= 6569 or 11736))
                        continue;

                    if (from.Z + 16 < tile.Z || tile.Z + 16 < from.Z ||
                        !from.InLOS(new Point3D(from.X + x, from.Y + y, tile.Z + tile.Height / 2 + 1)))
                        continue;

                    return new StaticTarget(new Point3D(from.X + x, from.Y + y, tile.Z), tile.ID);
                }
            }
        }

        return null;
    }

    public sealed class SmeltOreEntry : ContextMenuEntry
    {
        /// <summary>
        /// "Smelt Ore", our own cliloc, which the player package adds to the client (cc-P57 Part D, ShardClilocs). It was
        /// 3006143 "Smelt": no stock cliloc reads "Smelt Ore" (cc-P55 notes, Part C).
        /// </summary>
        public const int Cliloc = ShardClilocs.SmeltOre;

        public SmeltOreEntry(bool enabled) : base(Cliloc) => Enabled = enabled;

        public override void OnClick(Mobile from, IEntity target)
        {
            if (from.CheckAlive() && target is CompactOreSatchel { Deleted: false } satchel)
                satchel.SmeltAllOre(from);
        }
    }

    // ── Serialization (v0 — no custom fields) ─────────────────────────────────

    private void Deserialize(IGenericReader reader, int version) { }

    // cc-P55 Part E: a satchel saved in its tier's old hue takes the new one; any other hue (one staff set) is kept.
    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (Hue == LegacyTierHue)
            Hue = TierHue;
    }
}
