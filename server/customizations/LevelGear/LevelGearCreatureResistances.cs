// cc-P261: Chase's rule 3, companions may wear levelable gear. Pinned's Mobile.ComputeResistances adds every equipped
// item's resistance for any mobile, through the GetItemResistance virtual (Mobile-set-item-resistance-hook.patch), so a
// creature wearing a levelable shield or armour piece gets its bought resist the way a player does
// (PlayerMobileSetResistances.cs). Its AosAttribute bonuses need nothing here: AosAttributes.GetValue walks any mobile's
// items. No companion type exists yet (F-12/P41 research only); this covers any BaseCreature, pets included.

using Server.Items;

namespace Server.Mobiles;

public partial class BaseCreature
{
    public override int GetItemResistance(Item item, ResistanceType type) =>
        base.GetItemResistance(item, type) + ClusterFLevelGear.ResistBonus(item, type);
}
