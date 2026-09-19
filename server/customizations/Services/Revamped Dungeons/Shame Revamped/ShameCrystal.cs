// ServUO: Services/Revamped Dungeons/Shame Revamped/ShameCrystal.cs (CC4 Shame).
//
// Crystal of Shame: the dungeon's point currency, dropped by every Shame creature at its own rate and by the
// level guardians. ServUO's double-click awards Amount points through PointsSystem.ShameCrystals and deletes
// the stack. That system is S7 and not on this shard, so while ShameCrystals.Enabled is false the crystal is
// KEPT on double-click rather than consumed for nothing (Despise's D-29 shape). Deviation in notes/cc4-shame.md.

using ModernUO.Serialization;
using Server.Engines.Points;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ShameCrystal : Item
{
    [Constructible]
    public ShameCrystal(int amount = 1) : base(16395)
    {
        Stackable = true;
        Amount = amount;

        Hue = 2611;
    }

    public override int LabelNumber => 1151624; // Crystal of Shame

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            return;
        }

        if (!ShameCrystals.Enabled)
        {
            return; // S7 seam: nothing to award yet, keep the crystal.
        }

        ShameCrystals.AwardPoints(from, Amount);
        Delete();
    }
}
