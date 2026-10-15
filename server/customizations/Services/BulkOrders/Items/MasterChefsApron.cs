// cc-P123: a 2016 cooking bulk-order reward (410 points), ported from ServUO
// Scripts/Services/BulkOrders/Items/MasterChefsApron.cs. A full apron with a random exceptional bonus (a talisman's roll);
// worn, it adds that bonus to the chance of an exceptional craft, as ServUO's CraftItem.GetExceptionalChance does
// (:1296-1301) for every craft (CraftItem-chefs-apron.patch calls ExceptionalBonusFor).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MasterChefsApron : FullApron
{
    [SerializableField(0)]
    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _bonus;

    [Constructible]
    public MasterChefsApron() : base(1990)
    {
        while (_bonus == 0)
        {
            _bonus = BaseTalisman.GetRandomExceptional();
        }
    }

    public override int LabelNumber => 1157228; // Master Chef's Apron

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        // ~1_NAME~ Exceptional Bonus: ~2_val~%
        list.Add(1072395, $"{AosSkillBonuses.GetLabel(SkillName.Cooking):#}\t{_bonus}");
    }

    /// <summary>The exceptional chance a worn apron adds (0.0 to 1.0), for CraftItem.GetExceptionalChance.</summary>
    public static double ExceptionalBonusFor(Mobile from) =>
        from?.FindItemOnLayer(Layer.MiddleTorso) is MasterChefsApron apron ? apron.Bonus / 100.0 : 0.0;
}
