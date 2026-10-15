// cc-P123: a 2016 bulk order deed, ported from ServUO pub57
// Scripts/Services/BulkOrders/SmallBODs/SmallInscriptionBOD.cs on pinned's SmallBOD. What the six crafts' deeds share,
// and the changes from ServUO, are in CraftBOD.cs.

using System;
using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0, false)]
public partial class SmallInscriptionBOD : SmallBOD
{
    public SmallInscriptionBOD(
        int amountCur, int amountMax, Type type, int number, int graphic, bool reqExceptional, BulkMaterialType mat
    ) : base(CraftBOD.Hue(BODType.Inscription), amountCur, amountMax, type, number, graphic, reqExceptional, mat)
    {
    }


    public static SmallInscriptionBOD CreateRandomFor(Mobile m)
    {
        var amountMax = CraftBOD.AmountFor(m.Skills.Inscribe.Base);
        var entry = CraftBOD.PickEntry(m, CraftBulkEntries.InscriptionSmalls, DefInscription.CraftSystem, false);

        return entry == null
            ? null
            : new SmallInscriptionBOD(0, amountMax, entry.Type, entry.Number, entry.Graphic, false, BulkMaterialType.None);
    }

    public override int ComputeFame() => InscriptionRewardCalculator.Instance.ComputeFame(this);
    public override int ComputeGold() => InscriptionRewardCalculator.Instance.ComputeGold(this);
    public override RewardGroup GetRewardGroup() => InscriptionRewardCalculator.Instance.LookupRewards(0);
    public override void EndCombine(Mobile from, Item item) =>
        CraftBOD.EndCombineSmall(this, from, item, i => CraftBOD.CheckSmallType(this, i.GetType()));
}
