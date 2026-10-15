// cc-P123: a 2016 bulk order deed, ported from ServUO pub57 Scripts/Services/BulkOrders/SmallBODs/SmallTinkerBOD.cs on
// pinned's SmallBOD. What the six crafts' deeds share, and the changes from ServUO, are in CraftBOD.cs.

using System;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Items;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0, false)]
public partial class SmallTinkerBOD : SmallBOD
{
    // ServUO's GemType: a jewellery deed names its gem, and only that gem's piece fills it.
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private GemType _gemType;

    public SmallTinkerBOD(
        int amountCur, int amountMax, Type type, int number, int graphic, bool reqExceptional, BulkMaterialType mat,
        GemType gemType = GemType.None
    ) : base(CraftBOD.Hue(BODType.Tinkering), amountCur, amountMax, type, number, graphic, reqExceptional, mat)
    {
        _gemType = gemType;
    }


    public static SmallTinkerBOD CreateRandomFor(Mobile m)
    {
        var useMaterials = 0.75 > Utility.RandomDouble();
        var entries = useMaterials ? CraftBulkEntries.TinkeringSmalls : CraftBulkEntries.TinkeringSmallsRegular;
        var skill = m.Skills.Tinkering.Base;
        var amountMax = CraftBOD.AmountFor(skill);
        var material = useMaterials && skill >= 70.1
            ? CraftBOD.RollMaterialFor(skill, BulkMaterialType.DullCopper, CraftBOD.MetalChances)
            : BulkMaterialType.None;
        var reqExceptional = useMaterials && CraftBOD.RollExceptional(skill);

        var entry = CraftBOD.PickEntry(m, entries, DefTinkering.CraftSystem, reqExceptional);

        if (entry == null)
        {
            return null;
        }

        if (CannotAssignMaterial(entry.Type) || !CraftBOD.CanUseMaterial(entry.Type))
        {
            material = BulkMaterialType.None;
        }

        var gem = GemType.None;
        var number = entry.Number;

        if (entry.Type.IsSubclassOf(typeof(BaseJewel)))
        {
            gem = (GemType)Utility.RandomMinMax(1, 9);
            number = GemNumber(entry.Type, gem);
        }

        return new SmallTinkerBOD(
            0, amountMax, entry.Type, number, entry.Graphic, reqExceptional && CraftBOD.CanBeExceptional(entry.Type),
            material, gem
        );
    }

    /// <summary>ServUO SmallTinkerBOD._NonMaterials: tools, the smith's hammer and jewellery take no metal.</summary>
    public static bool CannotAssignMaterial(Type t) =>
        t == typeof(SmithHammer) || t.IsSubclassOf(typeof(BaseTool)) || t.IsSubclassOf(typeof(BaseJewel)) ||
        t == typeof(BaseTool) || t == typeof(BaseJewel);

    /// <summary>ServUO AssignGemNumber: the jewel's name with its gem (a ring of rubies, ...).</summary>
    public static int GemNumber(Type jewelType, GemType gem)
    {
        var offset = (int)gem - 1;

        if (jewelType == typeof(GoldRing) || jewelType == typeof(SilverRing))
        {
            return 1044176 + offset;
        }

        if (jewelType == typeof(GoldBracelet) || jewelType == typeof(SilverBracelet))
        {
            return 1044221 + offset;
        }

        return 1044203 + offset;
    }

    /// <summary>The gem a jewellery deed asked for, from its name (a book keeps the name, not the gem).</summary>
    public static GemType GemFromNumber(Type jewelType, int number)
    {
        if (jewelType == null || !jewelType.IsSubclassOf(typeof(BaseJewel)))
        {
            return GemType.None;
        }

        var gem = number - GemNumber(jewelType, (GemType)1) + 1;
        return gem is >= 1 and <= 9 ? (GemType)gem : GemType.None;
    }

    /// <summary>ServUO's _TinkerTypeTable: left, right and plain spoons, forks, knives, clocks; gold and silver jewellery.</summary>
    private static readonly Type[][] _tinkerTypeTable =
    [
        [typeof(Spoon), typeof(SpoonRight), typeof(SpoonLeft)],
        [typeof(Fork), typeof(ForkRight), typeof(ForkLeft)],
        [typeof(Knife), typeof(KnifeRight), typeof(KnifeLeft)],
        [typeof(Clock), typeof(ClockRight), typeof(ClockLeft)],
        [typeof(GoldRing), typeof(SilverRing)],
        [typeof(GoldBracelet), typeof(SilverBracelet)],
        [typeof(GoldEarrings), typeof(SilverEarrings)]
    ];

    /// <summary>True when the type has a twin in ServUO's table that this craft makes (a left-hand spoon for a spoon).</summary>
    public static bool HasMakeableTwin(Type type, CraftSystem system)
    {
        foreach (var types in _tinkerTypeTable)
        {
            if (Array.IndexOf(types, type) < 0)
            {
                continue;
            }

            foreach (var t in types)
            {
                if (system.CraftItems.SearchFor(t) != null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public static bool CheckTinkerType(Type actual, Type lookingFor)
    {
        foreach (var types in _tinkerTypeTable)
        {
            if (Array.IndexOf(types, lookingFor) >= 0 && Array.IndexOf(types, actual) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    public bool CheckType(Type type) => CraftBOD.CheckSmallType(this, type) || CheckTinkerType(type, Type);

    public bool CheckType(Item item) =>
        (_gemType == GemType.None || item is BaseJewel jewel && jewel.GemType == _gemType) && CheckType(item.GetType());

    public override int ComputeFame() => TinkeringRewardCalculator.Instance.ComputeFame(this);
    public override int ComputeGold() => TinkeringRewardCalculator.Instance.ComputeGold(this);
    public override RewardGroup GetRewardGroup() => TinkeringRewardCalculator.Instance.LookupRewards(0);
    public override void EndCombine(Mobile from, Item item) => CraftBOD.EndCombineSmall(this, from, item, CheckType);
}
