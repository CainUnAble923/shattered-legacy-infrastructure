// cc-P123: a 2016 bulk order deed, ported from ServUO pub57 Scripts/Services/BulkOrders/SmallBODs/SmallCarpentryBOD.cs
// on pinned's SmallBOD. What the six crafts' deeds share, and the changes from ServUO, are in CraftBOD.cs.

using System;
using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0, false)]
public partial class SmallCarpentryBOD : SmallBOD
{
    public SmallCarpentryBOD(
        int amountCur, int amountMax, Type type, int number, int graphic, bool reqExceptional, BulkMaterialType mat
    ) : base(CraftBOD.Hue(BODType.Carpentry), amountCur, amountMax, type, number, graphic, reqExceptional, mat)
    {
    }


    public static SmallCarpentryBOD CreateRandomFor(Mobile m)
    {
        var skill = m.Skills.Carpentry.Base;
        var useMaterials = Utility.RandomBool();
        var amountMax = CraftBOD.AmountFor(skill);
        var material = useMaterials && skill >= 70.1
            ? CraftBOD.RollMaterialFor(skill, BulkMaterialType.OakWood, CraftBOD.WoodChances)
            : BulkMaterialType.None;
        var reqExceptional = CraftBOD.RollExceptional(skill);

        var entry = CraftBOD.PickEntry(m, CraftBulkEntries.CarpentrySmalls, DefCarpentry.CraftSystem, reqExceptional);

        return entry == null ? null : New(entry, amountMax, reqExceptional, material);
    }

    private static SmallCarpentryBOD New(SmallBulkEntry e, int amountMax, bool exc, BulkMaterialType mat) =>
        new(
            0, amountMax, e.Type, e.Number, e.Graphic, exc && CraftBOD.CanBeExceptional(e.Type),
            CraftBOD.CanUseMaterial(e.Type) ? mat : BulkMaterialType.None
        );

    public override int ComputeFame() => CarpentryRewardCalculator.Instance.ComputeFame(this);
    public override int ComputeGold() => CarpentryRewardCalculator.Instance.ComputeGold(this);
    public override RewardGroup GetRewardGroup() => CarpentryRewardCalculator.Instance.LookupRewards(0);
    public override void EndCombine(Mobile from, Item item) =>
        CraftBOD.EndCombineSmall(this, from, item, i => CraftBOD.CheckSmallType(this, i.GetType()));
}
