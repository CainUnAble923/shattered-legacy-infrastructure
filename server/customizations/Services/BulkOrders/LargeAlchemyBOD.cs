// cc-P123: a 2016 bulk order deed, ported from ServUO pub57 Scripts/Services/BulkOrders/LargeBODs/LargeAlchemyBOD.cs on
// pinned's LargeBOD. What the six crafts' deeds share, and the changes from ServUO, are in CraftBOD.cs.

using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0, false)]
public partial class LargeAlchemyBOD : LargeBOD
{
    [Constructible]
    public LargeAlchemyBOD()
    {
        var list = Utility.Random(5) switch
        {
            1 => CraftBulkEntries.LargeGreater,
            2 => CraftBulkEntries.LargeLesser,
            3 => CraftBulkEntries.LargeRegular,
            4 => CraftBulkEntries.LargeToxic,
            _ => CraftBulkEntries.LargeExplosive
        };

        var entries = CraftBOD.Convert(this, list, DefAlchemy.CraftSystem);

        CraftBOD.RollLarge(this, CraftBOD.Hue(BODType.Alchemy), entries, false, BulkMaterialType.None, null);
    }

    public LargeAlchemyBOD(int amountMax, bool reqExceptional, BulkMaterialType mat, LargeBulkEntry[] entries)
        : base(CraftBOD.Hue(BODType.Alchemy), amountMax, reqExceptional, mat, entries)
    {
    }

    public override int ComputeFame() => AlchemyRewardCalculator.Instance.ComputeFame(this);
    public override int ComputeGold() => AlchemyRewardCalculator.Instance.ComputeGold(this);
    public override RewardGroup GetRewardGroup() => AlchemyRewardCalculator.Instance.LookupRewards(0);
    public override void EndCombine(Mobile from, Item item) =>
        CraftBOD.EndCombineLarge(this, from, item, CraftBOD.CheckLargeType);
}
