using System;
using ModernUO.Serialization;
using Server.Mobiles;

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
    private const int SatchelHue     = 0x0482; // muted earthy brown

    // Override in subclasses to change tier stats without duplicating logic.
    protected virtual int TierWeightReductionPct => 50;
    protected virtual int TierMaxContentWeight    => 400; // before reduction → 200 stones effective

    public override int DefaultGumpID  => 0x3C;    // generic bag gump
    public override double DefaultWeight => 3.0;
    public override int DefaultMaxWeight => TierMaxContentWeight;

    // ── Constructor ───────────────────────────────────────────────────────────

    [Constructible]
    public CompactOreSatchel() : base(SatchelGraphic)
    {
        Hue       = SatchelHue;
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

    // ── Serialization (v0 — no custom fields) ─────────────────────────────────

    private void Deserialize(IGenericReader reader, int version) { }
}
