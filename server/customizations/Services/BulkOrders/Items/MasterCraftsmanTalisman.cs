// cc-P122: a 2016 smith and tailor bulk-order reward, ported from ServUO
// Scripts/Services/BulkOrders/Items/MasterCraftsmanTalisman.cs. A crafting talisman for its skill with ServUO's random
// success and exceptional bonuses (pinned's CraftItem reads both from a worn talisman, CraftItem.cs:823-824 and :908),
// and charges.
//
// Not carried: OSI's "Crafting Failure Protection", the charges that save materials on a failed craft. Nothing in pinned or
// ours spends them, so the tooltip line ServUO adds (1157213) is left off rather than promise a protection that does not
// work. ServUO's TalismanSkill is pinned's SkillName.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MasterCraftsmanTalisman : BaseTalisman
{
    [Constructible]
    public MasterCraftsmanTalisman(int charges, int itemID, SkillName skill) : base(itemID)
    {
        Skill = skill;

        SuccessBonus = GetRandomSuccessful();
        ExceptionalBonus = GetRandomExceptional();
        Blessed = GetRandomBlessed();

        MaxCharges = charges;
        Charges = charges;
    }

    public override int LabelNumber => 1157217; // Master Craftsman Talisman
}
