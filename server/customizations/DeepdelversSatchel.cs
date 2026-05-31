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
///
/// Future hooks (not yet implemented):
///   - "Smelt All" context menu — smelt all ore in satchel to ingots at a forge
///   - Ore fragment recovery for partial ore piles
///
/// Restoration key: compact.satchel_t4
/// </summary>
[SerializationGenerator(0, false)]
public partial class DeepdelversSatchel : CompactOreSatchel
{
    private const int TierHue = 0x0455; // deep slate blue — matches T4 pickaxe

    protected override int TierWeightReductionPct => 65;
    protected override int TierMaxContentWeight    => 1000;
    public    override double DefaultWeight        => 4.5;

    [Constructible]
    public DeepdelversSatchel() : base()
    {
        Hue  = TierHue;
        Name = "Deepdelver's Ore Satchel";
    }

    private void Deserialize(IGenericReader reader, int version) { }
}
