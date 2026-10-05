using ModernUO.Serialization;

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
///   - Smelt Ore (context menu "Smelt", near a forge): every pile smelts exactly as at the forge, ingots back into the
///     satchel. Shared with T3 to T5 in CompactOreSatchel (cc-P55 Parts B, C, D).
///   - Hue: Gold (cc-P55 Part E). It was 0x8A5C, which carries the 0x8000 flag bit and showed uncolored.
///
/// Restoration key: compact.satchel_t2
/// </summary>
[SerializationGenerator(0, false)]
public partial class ReinforcedOreSatchel : CompactOreSatchel
{
    protected override int TierWeightReductionPct => 55;
    protected override int TierMaxContentWeight    => 600;
    public    override double DefaultWeight        => 3.5;

    public    override CraftResource TierMetal     => CraftResource.Gold;
    protected override int LegacyTierHue           => 0x8A5C;
    public    override bool CanSmeltOre            => true;

    [Constructible]
    public ReinforcedOreSatchel() : base()
    {
        Name = "Reinforced Ore Satchel";
    }

    // ── Serialization (v0 — no custom fields) ─────────────────────────────────

    private void Deserialize(IGenericReader reader, int version) { }
}
