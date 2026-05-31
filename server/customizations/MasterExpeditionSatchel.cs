using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

/// <summary>
/// Shattered Legacy — Master Expedition Satchel (Tier 5).
///
/// Upgrade from Deepdelver's Ore Satchel (Tier 4) via the Miners' Compact Liaison
/// (Upgrade Satchel view). Requires Deepwarden rank (80,000 Compact Standing).
///
/// Properties:
///   - 70% weight reduction on contents
///   - 1,200-stone content capacity (360 effective after reduction)
///   - Accepts: same as T1 (all ore/ingot types, granite)
///   - Blessed — not dropped on death
///
/// Future hooks (not yet implemented):
///   - "Smelt All" context menu — smelt all ore in satchel to ingots at a forge
///   - Overflow routing to Compact Mule cargo
///   - Shared logistics UI with Compact Dispatch Ledger
///   - Depository deposit integration
///
/// Restoration key: compact.satchel_t5
/// </summary>
[SerializationGenerator(0, false)]
public partial class MasterExpeditionSatchel : CompactOreSatchel
{
    private const int TierHue = 0x0B2A; // bright gold — matches T5 pickaxe

    protected override int TierWeightReductionPct => 70;
    protected override int TierMaxContentWeight    => 1200;
    public    override double DefaultWeight        => 5.0;

    [Constructible]
    public MasterExpeditionSatchel() : base()
    {
        Hue  = TierHue;
        Name = "Master Expedition Satchel";
    }

    private void Deserialize(IGenericReader reader, int version) { }
}
