// cc-P123: a 2016 bulk order deed, ported from ServUO pub57
// Scripts/Services/BulkOrders/LargeBODs/LargeInscriptionBOD.cs on pinned's LargeBOD. What the six crafts' deeds share,
// and the changes from ServUO, are in CraftBOD.cs.

using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0, false)]
public partial class LargeInscriptionBOD : LargeBOD
{
    [Constructible]
    public LargeInscriptionBOD()
    {
        var list = Utility.Random(10) switch
        {
            1 => CraftBulkEntries.LargeCircle1,
            2 => CraftBulkEntries.LargeCircle1and2,
            3 => CraftBulkEntries.LargeCircle4,
            4 => CraftBulkEntries.LargeCircle5,
            5 => CraftBulkEntries.LargeCircle7,
            6 => CraftBulkEntries.LargeCircle8,
            7 => CraftBulkEntries.LargeNecromancy1,
            8 => CraftBulkEntries.LargeNecromancy2,
            9 => CraftBulkEntries.LargeNecromancy3,
            _ => CraftBulkEntries.LargeBooks
        };

        var entries = CraftBOD.Convert(this, list, DefInscription.CraftSystem);

        CraftBOD.RollLarge(this, CraftBOD.Hue(BODType.Inscription), entries, false, BulkMaterialType.None, null);
    }

    public LargeInscriptionBOD(int amountMax, bool reqExceptional, BulkMaterialType mat, LargeBulkEntry[] entries)
        : base(CraftBOD.Hue(BODType.Inscription), amountMax, reqExceptional, mat, entries)
    {
    }

    public override int ComputeFame() => InscriptionRewardCalculator.Instance.ComputeFame(this);
    public override int ComputeGold() => InscriptionRewardCalculator.Instance.ComputeGold(this);
    public override RewardGroup GetRewardGroup() => InscriptionRewardCalculator.Instance.LookupRewards(0);
    public override void EndCombine(Mobile from, Item item) =>
        CraftBOD.EndCombineLarge(this, from, item, CraftBOD.CheckLargeType);
}
