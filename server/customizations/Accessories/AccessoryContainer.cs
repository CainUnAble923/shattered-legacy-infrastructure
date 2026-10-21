// cc-P262 (spec section 1): the accessory container. One per wearer, on the unused layer 0x0F (Layer.Unused_xF, which
// no stock item uses), invisible (Visible false: never sent to a player's client, staff see it), not movable, and it
// cannot be lifted, opened, dropped into, stolen or looted. ClusterFAccessories.Equip and Unequip are the only ways in
// and out.
//
// Death: a jewel inside follows the rule it would follow on a paperdoll layer. Its wearer's GetParentMoveResultFor is
// asked for each (PlayerMobile's insurance and Young checks included, charged once): one bound for the corpse goes to the
// backpack first, so the backpack pass of Mobile.Kill puts it on the corpse; a blessed or insured one stays in its slot.
// The container itself always stays on its wearer (not movable: RemainEquipped).
//
// Resists: Mobile.ComputeResistances adds every worn item's resistances, so the container reports the active jewels'.

#nullable enable

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class AccessoryContainer : Container
{
    public const Layer AccessoryLayer = Layer.Unused_xF;

    [Constructible(AccessLevel.Developer)]
    public AccessoryContainer() : base(0x0E75)
    {
        Layer = AccessoryLayer;
        Movable = false;
        Visible = false;
        LootType = LootType.Blessed;
    }

    public override string DefaultName => "accessory slots";

    public override double DefaultWeight => 0;

    public override int DefaultMaxWeight => 0;

    public override int DefaultMaxItems => ClusterFAccessories.MaxRings + ClusterFAccessories.MaxBracelets + ClusterFAccessories.MaxToeRings;

    private Mobile? Wearer => Parent as Mobile;

    public override bool OnDragLift(Mobile from) => false;

    public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
    {
        reject = LRReason.CannotLift;
        return false;
    }

    public override bool IsAccessibleTo(Mobile check) => false;

    public override bool CheckHold(Mobile m, Item item, bool message, bool checkItems, int plusItems, int plusWeight) => false;

    public override void OnDoubleClick(Mobile from)
    {
    }

    public override void DisplayTo(Mobile to)
    {
    }

    public override int PhysicalResistance => ClusterFAccessories.ResistTotal(Wearer, ResistanceType.Physical);
    public override int FireResistance => ClusterFAccessories.ResistTotal(Wearer, ResistanceType.Fire);
    public override int ColdResistance => ClusterFAccessories.ResistTotal(Wearer, ResistanceType.Cold);
    public override int PoisonResistance => ClusterFAccessories.ResistTotal(Wearer, ResistanceType.Poison);
    public override int EnergyResistance => ClusterFAccessories.ResistTotal(Wearer, ResistanceType.Energy);

    public override void OnItemRemoved(Item item)
    {
        base.OnItemRemoved(item);
        ClusterFAccessories.OnLeftContainer(Wearer, item);
    }

    public override DeathMoveResult OnParentDeath(Mobile parent)
    {
        var pack = parent.Backpack;
        if (pack != null)
        {
            foreach (var item in new List<Item>(Items))
            {
                if (parent.GetParentMoveResultFor(item) == DeathMoveResult.MoveToCorpse)
                {
                    pack.DropItem(item);
                }
            }
        }

        return DeathMoveResult.RemainEquipped;
    }
}
