// cc-P123: a 2016 bulk order deed, ported from ServUO pub57 Scripts/Services/BulkOrders/LargeBODs/LargeCarpentryBOD.cs
// on pinned's LargeBOD. What the six crafts' deeds share, and the changes from ServUO, are in CraftBOD.cs.

using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0, false)]
public partial class LargeCarpentryBOD : LargeBOD
{
    [Constructible]
    public LargeCarpentryBOD()
    {
        var list = Utility.Random(7) switch
        {
            1 => CraftBulkEntries.LargeCabinets,
            2 => CraftBulkEntries.LargeChests,
            3 => CraftBulkEntries.LargeElvenWeapons,
            4 => CraftBulkEntries.LargeInstruments,
            5 => CraftBulkEntries.LargeWeapons,
            6 => CraftBulkEntries.LargeWoodFurniture,
            _ => CraftBulkEntries.LargeArmoire
        };

        var entries = CraftBOD.Convert(this, list, DefCarpentry.CraftSystem);

        CraftBOD.RollLarge(
            this, CraftBOD.Hue(BODType.Carpentry), entries, true, BulkMaterialType.OakWood, CraftBOD.WoodChances
        );
    }

    public LargeCarpentryBOD(int amountMax, bool reqExceptional, BulkMaterialType mat, LargeBulkEntry[] entries)
        : base(CraftBOD.Hue(BODType.Carpentry), amountMax, reqExceptional, mat, entries)
    {
    }

    public override int ComputeFame() => CarpentryRewardCalculator.Instance.ComputeFame(this);
    public override int ComputeGold() => CarpentryRewardCalculator.Instance.ComputeGold(this);
    public override RewardGroup GetRewardGroup() => CarpentryRewardCalculator.Instance.LookupRewards(0);
    public override void EndCombine(Mobile from, Item item) =>
        CraftBOD.EndCombineLarge(this, from, item, CraftBOD.CheckLargeType);
}
