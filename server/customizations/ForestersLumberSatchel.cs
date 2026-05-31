using System;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

/// <summary>
/// Shattered Legacy — Foresters' Lumber Satchel (Tier 1).
///
/// A Foresters' Union logistics item issued to members on joining.
///
/// Properties:
///   - 50% weight reduction on contents
///   - 400-stone content capacity (200 effective after reduction)
///   - Accepts: all log types (regular, vanilla colored, extended) and all board types
///   - Auto-routes newly chopped logs via ClusterFLumberjackingExtension (Lumberjacking.Give override)
///   - Blessed — not dropped on death
///
/// Acquisition:
///   Issued by ClusterFGuildSystem when a player first joins the Foresters' Union.
///   Replaceable via ForestersGuildmasterGump (5 Timber Tokens).
///
/// Tier Progression (all inherit from this class):
///   T1  Foresters' Lumber Satchel   — issued on join, 50% reduction, 400 max
///   T2  Seasoned Lumber Satchel     — 55% reduction, 600 max  (SeasonedLumberSatchel.cs)
///   T3  Arborist's Lumber Satchel   — 60% reduction, 800 max  (ArboristLumberSatchel.cs)
/// </summary>
[SerializationGenerator(0, false)]
public partial class ForestersLumberSatchel : Container
{
    // ── Item type sets accepted by this satchel ───────────────────────────────
    //
    // We accept Log and Board (the base classes) plus all subclasses.
    // The Accepts() check uses t.IsSubclassOf(), so listing the base types is
    // sufficient to cover OakLog, IronwoodLog, OakBoard, IronwoodBoard, etc.

    private static readonly Type[] _acceptedTypes =
    {
        typeof(Log),   // covers all log types: OakLog, AshLog, IronwoodLog, …
        typeof(Board), // covers all board types: OakBoard, AshBoard, IronwoodBoard, …
    };

    // ── Container configuration ───────────────────────────────────────────────

    private const int SatchelGraphic = 0xA272; // satchel art (same silhouette as ore satchel)
    private const int SatchelHue     = 0x84C;  // dark forest green

    // Override in subclasses to change tier stats without duplicating logic.
    protected virtual int TierWeightReductionPct => 75;   // 75% reduction so heavy stacks stay portable
    protected virtual int TierMaxContentWeight    => 2000; // ~1,000 boards/logs at 2 stone each

    public override int    DefaultGumpID    => 0x3C;
    public override double DefaultWeight    => 3.0;
    public override int    DefaultMaxWeight => TierMaxContentWeight;

    // ── Constructor ───────────────────────────────────────────────────────────

    [Constructible]
    public ForestersLumberSatchel() : base(SatchelGraphic)
    {
        Hue      = SatchelHue;
        Name     = "Foresters' Lumber Satchel";
        LootType = LootType.Blessed;
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

    // ── Item acceptance filter ────────────────────────────────────────────────

    /// <summary>Returns true if the item is a log or board (any grade/tier).</summary>
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
                m.SendMessage("The Foresters' Lumber Satchel only holds logs and boards.");
            return false;
        }
        return base.CheckHold(m, item, message, checkItems, plusItems, plusWeight);
    }

    // ── Properties display ────────────────────────────────────────────────────

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1072210, TierWeightReductionPct);             // Weight Reduction: ~1_PERCENTAGE~%
        list.Add(1060742, $"{TotalWeight}\t{TierMaxContentWeight}"); // ~1~ / ~2~ stones
    }

    // ── Serialization (v0 — no custom fields) ─────────────────────────────────

    private void Deserialize(IGenericReader reader, int version) { }
}
