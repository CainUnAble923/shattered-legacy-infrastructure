// cc-P199: ServUO pub57 Scripts/Items/Functional/Grinder.cs. A blessed coffee grinder (a house fixture with a secure
// level): locked down in a house, double-clicking it within two tiles gives a target, and a stack of coffee pods in
// the user's pack becomes the same number of coffee grounds (CoffeePod and CoffeeGrounds are ours, in
// Services/Plants/PottedCoffeePlant.cs).
//
// Shape changes forced by ModernUO, none visible to a player: the secure level is a generated [SerializableField]
// (ServUO writes it as an int, Grinder.cs:105); house.IsLockedDown(item) (ServUO BaseHouse.cs:4282) is pinned's
// BaseHouse.HasLockedDownItem (BaseHouse.cs:3668), which reads the same two lists. ServUO leaves its CheckAccessible
// commented out (Grinder.cs:26-46 and :67), so anyone who can reach a locked-down grinder may use it; that is kept.

using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Multis;
using Server.Targeting;

namespace Server.Items;

[Flippable(0x9A97, 0x9A98)]
[SerializationGenerator(0, false)]
public partial class Grinder : Item, ISecurable
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private SecureLevel _level;

    [Constructible]
    public Grinder() : base(0x9A97) => LootType = LootType.Blessed;

    public override int LabelNumber => 1123599; // Grinder

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);
        SetSecureLevelEntry.AddTo(from, this, ref list);
    }

    public override void OnDoubleClick(Mobile from)
    {
        var house = BaseHouse.FindHouseAt(this);

        if (house?.HasLockedDownItem(this) != true)
        {
            from.SendLocalizedMessage(1114298); // This must be locked down in order to use it.
        }
        else if (from.InRange(GetWorldLocation(), 2))
        {
            from.Target = new InternalTarget();
        }
    }

    private class InternalTarget : Target
    {
        public InternalTarget() : base(-1, true, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is CoffeePod pod)
            {
                if (!pod.IsChildOf(from.Backpack))
                {
                    from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
                }
                else
                {
                    from.AddToBackpack(new CoffeeGrounds(pod.Amount));
                    pod.Delete();
                }
            }
            else
            {
                from.SendLocalizedMessage(1155729); // That is not something that can be ground.
            }
        }
    }
}
