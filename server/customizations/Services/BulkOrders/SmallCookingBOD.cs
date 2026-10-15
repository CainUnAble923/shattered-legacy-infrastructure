// cc-P123: a 2016 bulk order deed, ported from ServUO pub57 Scripts/Services/BulkOrders/SmallBODs/SmallCookingBOD.cs on
// pinned's SmallBOD. What the six crafts' deeds share, and the changes from ServUO, are in CraftBOD.cs.

using System;
using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Engines.BulkOrders;

[SerializationGenerator(0, false)]
public partial class SmallCookingBOD : SmallBOD
{
    public SmallCookingBOD(
        int amountCur, int amountMax, Type type, int number, int graphic, bool reqExceptional, BulkMaterialType mat
    ) : base(CraftBOD.Hue(BODType.Cooking), amountCur, amountMax, type, number, graphic, reqExceptional, mat)
    {
    }


    public static SmallCookingBOD CreateRandomFor(Mobile m)
    {
        var skill = m.Skills.Cooking.Base;
        var nonExceptional = 0.20 > Utility.RandomDouble();
        var entries = nonExceptional ? CraftBulkEntries.CookingSmallsRegular : CraftBulkEntries.CookingSmalls;
        var amountMax = CraftBOD.AmountFor(skill);
        var reqExceptional = !nonExceptional && CraftBOD.RollExceptional(skill);

        // ServUO picks among what the player can make at all here, exceptional or not (SmallCookingBOD.cs:120-135).
        var entry = CraftBOD.PickEntry(m, entries, DefCooking.CraftSystem, false);

        return entry == null
            ? null
            : new SmallCookingBOD(
                0, amountMax, entry.Type, entry.Number, entry.Graphic,
                reqExceptional && CraftBOD.CanBeExceptional(entry.Type), BulkMaterialType.None
            );
    }

    public override int ComputeFame() => CookingRewardCalculator.Instance.ComputeFame(this);
    public override int ComputeGold() => CookingRewardCalculator.Instance.ComputeGold(this);
    public override RewardGroup GetRewardGroup() => CookingRewardCalculator.Instance.LookupRewards(0);
    public override void EndCombine(Mobile from, Item item) =>
        CraftBOD.EndCombineSmall(this, from, item, i => CraftBOD.CheckSmallType(this, i.GetType()));
}
