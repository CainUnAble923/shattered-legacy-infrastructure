using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

/// <summary>
/// Shattered Legacy — Deepdelver's Ore Satchel (Tier 4).
///
/// Upgrade from Surveyor's Ore Satchel (Tier 3) via the Miners' Compact Liaison
/// (Upgrade Satchel view). Requires Master Delver rank (40,000 Compact Standing).
///
/// Properties:
///   - 65% weight reduction on contents
///   - 1,000-stone content capacity (350 effective after reduction)
///   - Accepts: same as T1 (all ore/ingot types, granite)
///   - Blessed — not dropped on death
///   - Smelt Ore, as T2 (cc-P55 Part D)
///   - Hue: Valorite (cc-P55 Part E; was 0x0455)
///
/// Future hooks (not yet implemented):
///   - Ore fragment recovery for partial ore piles
///
/// Restoration key: compact.satchel_t4
/// </summary>
[SerializationGenerator(0, false)]
public partial class DeepdelversSatchel : CompactOreSatchel
{
    protected override int TierWeightReductionPct => 65;
    protected override int TierMaxContentWeight    => 1000;
    public    override double DefaultWeight        => 4.5;

    public    override CraftResource TierMetal     => CraftResource.Valorite;
    protected override int LegacyTierHue           => 0x0455;
    public    override bool CanSmeltOre            => true;

    [Constructible]
    public DeepdelversSatchel() : base()
    {
        Name = "Deepdelver's Ore Satchel";
    }

    private void Deserialize(IGenericReader reader, int version) { }
}
