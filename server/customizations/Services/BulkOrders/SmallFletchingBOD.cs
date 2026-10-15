// cc-P123: a 2016 bulk order deed, ported from ServUO pub57 Scripts/Services/BulkOrders/SmallBODs/SmallFletchingBOD.cs
// on pinned's SmallBOD. What the six crafts' deeds share, and the changes from ServUO, are in CraftBOD.cs.

using System;
using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0, false)]
public partial class SmallFletchingBOD : SmallBOD
{
    public SmallFletchingBOD(
        int amountCur, int amountMax, Type type, int number, int graphic, bool reqExceptional, BulkMaterialType mat
    ) : base(CraftBOD.Hue(BODType.Fletching), amountCur, amountMax, type, number, graphic, reqExceptional, mat)
    {
    }


    public static SmallFletchingBOD CreateRandomFor(Mobile m)
    {
        var skill = m.Skills.Fletching.Base;
        var useMaterials = !(skill < 30.0 || 0.20 > Utility.RandomDouble());
        var entries = useMaterials ? CraftBulkEntries.FletchingSmalls : CraftBulkEntries.FletchingSmallsRegular;
        var amountMax = CraftBOD.AmountFor(skill);
        var material = useMaterials && skill >= 70.1
            ? CraftBOD.RollMaterialFor(skill, BulkMaterialType.OakWood, CraftBOD.WoodChances)
            : BulkMaterialType.None;
        var reqExceptional = useMaterials && CraftBOD.RollExceptional(skill);

        var entry = CraftBOD.PickEntry(m, entries, DefBowFletching.CraftSystem, reqExceptional);

        return entry == null
            ? null
            : new SmallFletchingBOD(
                0, amountMax, entry.Type, entry.Number, entry.Graphic,
                reqExceptional && CraftBOD.CanBeExceptional(entry.Type),
                CraftBOD.CanUseMaterial(entry.Type) ? material : BulkMaterialType.None
            );
    }

    public override int ComputeFame() => FletchingRewardCalculator.Instance.ComputeFame(this);
    public override int ComputeGold() => FletchingRewardCalculator.Instance.ComputeGold(this);
    public override RewardGroup GetRewardGroup() => FletchingRewardCalculator.Instance.LookupRewards(0);
    public override void EndCombine(Mobile from, Item item) =>
        CraftBOD.EndCombineSmall(this, from, item, i => CraftBOD.CheckSmallType(this, i.GetType()));
}
