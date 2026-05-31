using ModernUO.Serialization;

namespace Server.Items;

/// <summary>
/// Shattered Legacy — Arborist's Lumber Satchel (Tier 3).
///
/// Upgrade from Seasoned Lumber Satchel (Tier 2) via the Foresters' Guildmaster.
///
/// Properties:
///   - 60% weight reduction on contents
///   - 800-stone content capacity (320 effective after reduction)
///   - Accepts: same as T1 (all log and board types)
///   - Blessed — not dropped on death
///   - Inherits Tier 2 "Process All Logs" feature from SeasonedLumberSatchel
/// </summary>
[SerializationGenerator(0, false)]
public partial class ArboristLumberSatchel : SeasonedLumberSatchel
{
    private const int TierHue = 0x026C; // teal/forest tint — visual step up from T2

    protected override int TierWeightReductionPct => 85;   // 85% reduction
    protected override int TierMaxContentWeight    => 8000; // ~4,000 boards/logs
    public    override double DefaultWeight        => 4.0;

    [Constructible]
    public ArboristLumberSatchel() : base()
    {
        Hue  = TierHue;
        Name = "Arborist's Lumber Satchel";
    }

    // ── Serialization (v0 — no custom fields) ─────────────────────────────────

    private void Deserialize(IGenericReader reader, int version) { }
}
