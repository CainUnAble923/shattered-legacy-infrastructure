// ServUO: Services/PointsSystems/ShameCrystals.cs (CC4 Shame) - NOT the ServUO class. That one is a
// PointsSystem subclass (51 lines over a 463-line base in Services/PointsSystems/PointsSystem.cs, which is
// gap register B7 / task S7 and is not on this shard). This file is the SEAM the Shame port calls through
// so that S7 can land behind it without touching ShameAltar or ShameCrystal:
//
//   ShameAltar.CheckSummon   ServUO: PointsSystem.ShameCrystals.GetPoints(from) < SummonCost ... DeductPoints
//   ShameCrystal.OnDoubleClick  ServUO: PointsSystem.ShameCrystals.AwardPoints(from, Amount); Delete()
//
// Until S7 lands Enabled is false: GetPoints returns 0, so every altar answers 1151623 "You are not yet
// worthy of challenging the champion", and a Crystal of Shame is kept rather than consumed for nothing
// (the D-29 shape from Despise's PutridHeart). A GM can still run a guardian fight by setting an altar's
// SummonCost to 0. Deviation recorded in notes/cc4-shame.md.
//
// When S7 lands: derive the real ShameCrystals from PointsSystem, point these three members at it, and
// flip Enabled. The ServUO messages this system sends are kept here so they are not lost:
//   SendMessage: 1151634 "You gain ~1_AMT~ dungeon points for ~2_NAME~. Your total is now ~3_TOTAL~."
//               with String.Format("{0}\t{1}\t{2}", (int)points, "Shame", (int)(old + points))
//   Name: TextDefinition(1151673). GetTitle: TextDefinition(1123444). AutoAdd true, MaxPoints double.MaxValue.

namespace Server.Engines.Points;

public static class ShameCrystals
{
    /// <summary>
    ///     False until Services/PointsSystems (S7) exists on this shard.
    /// </summary>
    public static bool Enabled => false;

    public static double GetPoints(Mobile from) => 0;

    public static void AwardPoints(Mobile from, double points)
    {
    }

    public static bool DeductPoints(Mobile from, double points) => false;
}
