using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

/// <summary>
/// Shattered Legacy — Surveyor's Ore Satchel (Tier 3).
///
/// Upgrade from Reinforced Ore Satchel (Tier 2) via the Miners' Compact Liaison
/// (Upgrade Satchel view). Requires Surveyor rank (15,000 Compact Standing).
///
/// Properties:
///   - 60% weight reduction on contents
///   - 800-stone content capacity (320 effective after reduction)
///   - Accepts: same as T1 (all ore/ingot types, granite)
///   - Blessed — not dropped on death
///   - Smelt Ore, as T2 (cc-P55 Part D: it was lost on upgrading)
///   - Hue: Verite (cc-P55 Part E; was 0x026C)
///
/// Future hooks (not yet implemented):
///   - Survey/logbook integration — show unreported vein count in tooltip
///
/// Restoration key: compact.satchel_t3
/// </summary>
[SerializationGenerator(0, false)]
public partial class SurveyorsSatchel : CompactOreSatchel
{
    protected override int TierWeightReductionPct => 60;
    protected override int TierMaxContentWeight    => 800;
    public    override double DefaultWeight        => 4.0;

    public    override CraftResource TierMetal     => CraftResource.Verite;
    protected override int LegacyTierHue           => 0x026C;
    public    override bool CanSmeltOre            => true;

    [Constructible]
    public SurveyorsSatchel() : base()
    {
        Name = "Surveyor's Ore Satchel";
    }

    private void Deserialize(IGenericReader reader, int version) { }
}
