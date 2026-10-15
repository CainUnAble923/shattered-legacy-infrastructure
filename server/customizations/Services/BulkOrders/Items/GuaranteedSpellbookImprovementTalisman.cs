// cc-P123: a 2016 inscription bulk-order reward (500 points), ported from ServUO
// Scripts/Services/BulkOrders/Items/GuaranteedSpellbookImprovementTalisman.cs. An inscription talisman with ServUO's random
// success and exceptional bonuses and charges; worn while scribing a spellbook, each charge adds one more property to the
// book, as ServUO's Spellbook.OnCraft does (:1255-1270; Spellbook-improvement-talisman.patch calls ExtraProperty).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class GuaranteedSpellbookImprovementTalisman : BaseTalisman
{
    [Constructible]
    public GuaranteedSpellbookImprovementTalisman(int charges = 10) : base(0x9E28)
    {
        Skill = SkillName.Inscribe;
        SuccessBonus = GetRandomSuccessful();
        ExceptionalBonus = GetRandomExceptional();

        MaxCharges = charges;
        Charges = charges;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1157212); // Guaranteed Spellbook Improvement
    }

    /// <summary>
    /// One more property for a spellbook being scribed, when the scribe wears a charged talisman (ServUO
    /// Spellbook.cs:1255-1270): a charge is spent, and the talisman goes when the last one is.
    /// </summary>
    public static int ExtraProperty(Mobile from)
    {
        if (from?.FindItemOnLayer(Layer.Talisman) is not GuaranteedSpellbookImprovementTalisman talisman ||
            talisman.Charges <= 0)
        {
            return 0;
        }

        talisman.Charges--;
        from.SendLocalizedMessage(1157210); // Your talisman magically improves your spellbook.

        if (talisman.Charges <= 0)
        {
            from.SendLocalizedMessage(1157211); // Your talisman has been destroyed.
            talisman.Delete();
        }

        return 1;
    }
}
