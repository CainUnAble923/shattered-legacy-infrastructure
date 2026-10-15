// cc-P123: a 2016 bulk order deed, ported from ServUO pub57 Scripts/Services/BulkOrders/LargeBODs/LargeCookingBOD.cs on
// pinned's LargeBOD. What the six crafts' deeds share, and the changes from ServUO, are in CraftBOD.cs.

using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0, false)]
public partial class LargeCookingBOD : LargeBOD
{
    [Constructible]
    public LargeCookingBOD()
    {
        var nonExceptional = false;
        SmallBulkEntry[] list;

        switch (Utility.Random(7))
        {
            default:
                list = CraftBulkEntries.LargeBarbeque;
                nonExceptional = true;
                break;
            case 1:
                list = CraftBulkEntries.LargeDough;
                break;
            case 2:
                list = CraftBulkEntries.LargeFruits;
                nonExceptional = true;
                break;
            case 3:
                list = CraftBulkEntries.LargeMiso;
                break;
            case 4:
                list = CraftBulkEntries.LargeSushi;
                break;
            case 5:
                list = CraftBulkEntries.LargeSweets;
                break;
            case 6:
                list = CraftBulkEntries.LargeUnbakedPies;
                break;
        }

        var entries = CraftBOD.Convert(this, list, DefCooking.CraftSystem);

        CraftBOD.RollLarge(this, CraftBOD.Hue(BODType.Cooking), entries, !nonExceptional, BulkMaterialType.None, null);
    }

    public LargeCookingBOD(int amountMax, bool reqExceptional, BulkMaterialType mat, LargeBulkEntry[] entries)
        : base(CraftBOD.Hue(BODType.Cooking), amountMax, reqExceptional, mat, entries)
    {
    }

    public override int ComputeFame() => CookingRewardCalculator.Instance.ComputeFame(this);
    public override int ComputeGold() => CookingRewardCalculator.Instance.ComputeGold(this);
    public override RewardGroup GetRewardGroup() => CookingRewardCalculator.Instance.LookupRewards(0);
    public override void EndCombine(Mobile from, Item item) =>
        CraftBOD.EndCombineLarge(this, from, item, CraftBOD.CheckLargeType);
}
