using System;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

// ─────────────────────────────────────────────────────────────────────────────
// Hunter's Satchel — three-tier resource bag for the Rangers' League.
//
// Accepts: BaseLeather, BaseHides, Bone, Wool, Feather, raw meats
// (covers all leather/hide subtypes via base-class check)
//
// T1 — Hunter's Satchel        (issued on guild join): 30% weight, 350-stone
//   • Auto-converts hides into cut leather on entry
//   • Auto-grabs accepted items from carved corpses (via Corpse.OnItemAdded hook)
//   • Auto-routes accepted items that land in the backpack (via PlayerMobile.OnSubItemAdded)
//
// T2 — Seasoned Hunter's Satchel                     : 40% weight, 500-stone
//   • All T1 abilities
//   • Auto-skins nearby (2-tile) carvable corpses every 5s
//   • Auto-shears nearby (3-tile) woolly sheep every 5s
//
// T3 — Warden's Field Satchel                        : 50% weight, 700-stone
//   • All T2 abilities
// ─────────────────────────────────────────────────────────────────────────────

// ── T1 ────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class HuntersSatchel : Container
{
    // ── Accepted types ────────────────────────────────────────────────────────

    private static readonly Type[] _acceptedTypes =
    {
        typeof(BaseLeather),
        typeof(BaseHides),
        typeof(Bone),
        typeof(Wool),
        typeof(Feather),
        typeof(RawRibs),
        typeof(RawLambLeg),
        typeof(RawChickenLeg),
        typeof(RawBird),
    };

    /// <summary>Returns true if this item type is accepted by any Hunter's Satchel tier.</summary>
    public static bool Accepts(Item item)
    {
        if (item == null) return false;
        var t = item.GetType();
        foreach (var accepted in _acceptedTypes)
            if (t == accepted || t.IsSubclassOf(accepted)) return true;
        return false;
    }

    // ── Hide → leather conversion ─────────────────────────────────────────────

    /// <summary>
    /// Converts a BaseHides item into its cut-leather equivalent.
    /// Returns the leather item (caller must delete the original hides).
    /// Returns null if the item is not a hide type.
    /// </summary>
    public static Item? ConvertHidesToLeather(BaseHides hides) => hides switch
    {
        SpinedHides h => new SpinedLeather(h.Amount),
        HornedHides h => new HornedLeather(h.Amount),
        BarbedHides h => new BarbedLeather(h.Amount),
        _             => new Leather(hides.Amount),   // Hides or any unrecognised subclass
    };

    // ── Tier-overridable stats ────────────────────────────────────────────────

    protected virtual int TierWeightReductionPct => 30;
    protected virtual int TierMaxContentWeight   => 350;

    // ── Weight reduction ──────────────────────────────────────────────────────

    public override int GetTotal(TotalType type)
    {
        var total = base.GetTotal(type);
        if (type == TotalType.Weight)
            total -= total * TierWeightReductionPct / 100;
        return total;
    }

    // ── Auto-route: called by external loot hooks ─────────────────────────────

    /// <summary>
    /// Attempts to route <paramref name="item"/> into this satchel.
    /// Hides are automatically converted to leather on entry.
    /// Returns true if the item was accepted and placed.
    /// </summary>
    public bool TryRoute(Mobile owner, Item item)
    {
        if (!Accepts(item)) return false;
        if (GetTotal(TotalType.Weight) + item.Weight > TierMaxContentWeight) return false;
        return TryDropItem(owner, item, false);
    }

    // ── Construction ─────────────────────────────────────────────────────────

    [Constructible]
    public HuntersSatchel() : base(0xA272)   // satchel/pouch graphic
    {
        Weight   = 2.0;
        Hue      = 0x5B5;   // dark leather brown
        Name     = "a Hunter's Satchel";
        LootType = LootType.Blessed;
    }

    private void Deserialize(IGenericReader reader, int version) { }

    // ── Properties ────────────────────────────────────────────────────────────

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Accepts: Leather, Hides, Bone, Wool, Feathers, Raw Meat");
        list.Add($"Auto-converts hides to leather");
        list.Add($"Weight reduction: {TierWeightReductionPct}%");
        list.Add($"Capacity: {TierMaxContentWeight} stones");
    }

    // ── Override to restrict and convert on entry ─────────────────────────────

    public override bool TryDropItem(Mobile from, Item dropped, bool sendFullMessage)
    {
        // Auto-convert hides to leather before storing
        if (dropped is BaseHides hides)
        {
            var leather = ConvertHidesToLeather(hides);
            var success = base.TryDropItem(from, leather, sendFullMessage);
            if (success)
                hides.Delete();
            else
                leather.Delete();
            return success;
        }

        if (!Accepts(dropped))
        {
            from?.SendMessage(0x22, "That does not belong in a Hunter's Satchel.");
            return false;
        }
        return base.TryDropItem(from, dropped, sendFullMessage);
    }

    public override bool OnDragDrop(Mobile from, Item dropped)
    {
        // Auto-convert hides to leather when dragged in
        if (dropped is BaseHides hides)
        {
            var leather = ConvertHidesToLeather(hides);
            var success = base.OnDragDrop(from, leather);
            if (success)
                hides.Delete();
            else
                leather.Delete();
            return success;
        }

        if (!Accepts(dropped))
        {
            from.SendMessage(0x22, "That does not belong in a Hunter's Satchel.");
            return false;
        }
        return base.OnDragDrop(from, dropped);
    }
}

// ── T2 ────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class SeasonedHuntersSatchel : HuntersSatchel
{
    protected override int TierWeightReductionPct => 40;
    protected override int TierMaxContentWeight   => 500;

    private TimerExecutionToken _autoHarvestToken;

    [Constructible]
    public SeasonedHuntersSatchel()
    {
        Hue  = 0x5B2;   // slightly richer brown
        Name = "a Seasoned Hunter's Satchel";
    }

    private void Deserialize(IGenericReader reader, int version) { }

    // ── Auto-harvest timer lifecycle ──────────────────────────────────────────

    public override void OnAdded(IEntity parent)
    {
        base.OnAdded(parent);
        if (RootParent is PlayerMobile pm)
            StartAutoHarvest(pm);
    }

    public override void OnRemoved(IEntity parent)
    {
        base.OnRemoved(parent);
        _autoHarvestToken.Cancel();
    }

    /// <summary>Called on WorldLoad to re-register the timer after server restart.</summary>
    public void TryRestartAutoHarvest()
    {
        if (RootParent is PlayerMobile pm)
            StartAutoHarvest(pm);
    }

    private void StartAutoHarvest(PlayerMobile pm)
    {
        _autoHarvestToken.Cancel();
        Timer.StartTimer(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(5),
            () => DoAutoHarvest(pm),
            out _autoHarvestToken
        );
    }

    private void DoAutoHarvest(PlayerMobile pm)
    {
        if (Deleted || pm.Deleted || pm.Backpack == null)
        {
            _autoHarvestToken.Cancel();
            return;
        }

        if (!IsChildOf(pm.Backpack))
        {
            _autoHarvestToken.Cancel();
            return;
        }

        if (pm.Map == null || pm.Map == Map.Internal) return;

        // Auto-skin the nearest uncarved creature corpse within 2 tiles
        foreach (var item in pm.Map.GetItemsInRange(pm.Location, 2))
        {
            if (item is Corpse corpse && !corpse.Deleted && !corpse.Carved && corpse.Owner is BaseCreature)
            {
                ((ICarvable)corpse).Carve(pm, this);
                break;   // one per tick — avoid spam
            }
        }

        // Auto-shear the nearest woolly sheep within 3 tiles
        foreach (var mob in pm.Map.GetMobilesInRange(pm.Location, 3))
        {
            if (mob is Sheep sheep && !sheep.Deleted && Core.Now >= sheep.NextWoolTime)
            {
                ((ICarvable)sheep).Carve(pm, this);
                // Wool lands in backpack → PlayerMobile.OnSubItemAdded routes it here
                break;   // one per tick
            }
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Auto-skins nearby corpses");
        list.Add("Auto-shears nearby sheep");
    }
}

// ── T3 ────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class WardensFieldSatchel : SeasonedHuntersSatchel
{
    protected override int TierWeightReductionPct => 50;
    protected override int TierMaxContentWeight   => 700;

    [Constructible]
    public WardensFieldSatchel()
    {
        Hue  = 0x4B5;   // deep forest brown with slight green tint
        Name = "a Warden's Field Satchel";
    }

    private void Deserialize(IGenericReader reader, int version) { }
}
