// cc-P123: a 2016 bulk order deed, ported from ServUO pub57 Scripts/Services/BulkOrders/LargeBODs/LargeTinkerBOD.cs on
// pinned's LargeBOD. What the six crafts' deeds share, and the changes from ServUO, are in CraftBOD.cs.

using System;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Items;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0, false)]
public partial class LargeTinkerBOD : LargeBOD
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private GemType _gemType;

    [Constructible]
    public LargeTinkerBOD()
    {
        var useMaterials = true;
        var jewelry = false;
        SmallBulkEntry[] list;

        switch (Utility.Random(4))
        {
            default:
                list = CraftBulkEntries.LargeDining;
                break;
            case 1:
                list = CraftBulkEntries.LargeJewelry;
                useMaterials = false;
                jewelry = true;
                break;
            case 2:
                list = CraftBulkEntries.LargeKeyGlobe;
                break;
            case 3:
                list = CraftBulkEntries.LargeTools;
                useMaterials = false;
                break;
        }

        if (jewelry)
        {
            _gemType = (GemType)Utility.RandomMinMax(1, 9);
            list = GemEntries(list, _gemType);
        }

        var entries = CraftBOD.Convert(this, list, DefTinkering.CraftSystem);

        CraftBOD.RollLarge(
            this, CraftBOD.Hue(BODType.Tinkering), entries, true, BulkMaterialType.DullCopper,
            useMaterials ? CraftBOD.MetalChances : null
        );
    }

    public LargeTinkerBOD(
        int amountMax, bool reqExceptional, BulkMaterialType mat, LargeBulkEntry[] entries, GemType gemType = GemType.None
    ) : base(CraftBOD.Hue(BODType.Tinkering), amountMax, reqExceptional, mat, entries) => _gemType = gemType;

    /// <summary>ServUO AssignGemNumbers: each piece named with the deed's gem.</summary>
    private static SmallBulkEntry[] GemEntries(SmallBulkEntry[] list, GemType gem)
    {
        var result = new SmallBulkEntry[list.Length];

        for (var i = 0; i < list.Length; i++)
        {
            result[i] = new SmallBulkEntry(list[i].Type, SmallTinkerBOD.GemNumber(list[i].Type, gem), list[i].Graphic);
        }

        return result;
    }

    /// <summary>ServUO LargeTinkerBOD.CheckType: a jewellery deed takes only small deeds of its own gem.</summary>
    public bool CheckType(SmallBOD small, Type type) =>
        (_gemType == GemType.None || small is SmallTinkerBOD tinker && tinker.GemType == _gemType) &&
        (CraftBOD.CheckLargeType(small, type) || small is SmallTinkerBOD && SmallTinkerBOD.CheckTinkerType(type, small.Type));

    public override int ComputeFame() => TinkeringRewardCalculator.Instance.ComputeFame(this);
    public override int ComputeGold() => TinkeringRewardCalculator.Instance.ComputeGold(this);
    public override RewardGroup GetRewardGroup() => TinkeringRewardCalculator.Instance.LookupRewards(0);
    public override void EndCombine(Mobile from, Item item) => CraftBOD.EndCombineLarge(this, from, item, CheckType);
}
