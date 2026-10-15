// cc-P123: a 2016 bulk order deed, ported from ServUO pub57 Scripts/Services/BulkOrders/LargeBODs/LargeFletchingBOD.cs
// on pinned's LargeBOD. What the six crafts' deeds share, and the changes from ServUO, are in CraftBOD.cs.

using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0, false)]
public partial class LargeFletchingBOD : LargeBOD
{
    [Constructible]
    public LargeFletchingBOD()
    {
        var useMaterials = true;
        var list = Utility.Random(5) switch
        {
            1 => CraftBulkEntries.LargeHumanBows2,
            2 => CraftBulkEntries.LargeAmmunition,
            3 => CraftBulkEntries.LargeElvenBows1,
            4 => CraftBulkEntries.LargeElvenBows2,
            _ => CraftBulkEntries.LargeHumanBows1
        };

        if (list == CraftBulkEntries.LargeAmmunition)
        {
            useMaterials = false;
        }

        var entries = CraftBOD.Convert(this, list, DefBowFletching.CraftSystem);

        CraftBOD.RollLarge(
            this, CraftBOD.Hue(BODType.Fletching), entries, useMaterials, BulkMaterialType.OakWood,
            useMaterials ? CraftBOD.WoodChances : null
        );
    }

    public LargeFletchingBOD(int amountMax, bool reqExceptional, BulkMaterialType mat, LargeBulkEntry[] entries)
        : base(CraftBOD.Hue(BODType.Fletching), amountMax, reqExceptional, mat, entries)
    {
    }

    public override int ComputeFame() => FletchingRewardCalculator.Instance.ComputeFame(this);
    public override int ComputeGold() => FletchingRewardCalculator.Instance.ComputeGold(this);
    public override RewardGroup GetRewardGroup() => FletchingRewardCalculator.Instance.LookupRewards(0);
    public override void EndCombine(Mobile from, Item item) =>
        CraftBOD.EndCombineLarge(this, from, item, CraftBOD.CheckLargeType);
}
