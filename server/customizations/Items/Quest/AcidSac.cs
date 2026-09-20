// ServUO: Items/Quest/SAQuestItems.cs:7-121, one extracted type per file (the LuckyCoin precedent). Values verbatim;
// serialization by the generator. ServUO sets Stackable with no amount constructor, so it stacks but is always made
// one at a time; kept. Weight = 1.0 in the constructor is the DefaultWeight override here.
//
// DEVIATION D-67: the target's AddonComponent branch is not ported. In ServUO, targeting a MagicVinesComponent,
// StoneWallComponent or DungeonWallComponent of a StoneWallAndVineAddon / DungeonWallAndVineAddon swaps the wall for a
// SecretStoneWallNS / SecretDungeonWallNS for fifteen seconds (the Underworld's vine-wall puzzle). Those addons are
// ServUO's Items/Decorative/StoneWallAndVineAddon.cs (123 lines), DungeonWallAndVineAddon.cs (91) and
// SecretSlidingWalls.cs (91), none in pinned ModernUO, and nothing places them here. Any other AddonComponent does
// nothing in ServUO either, and every non-addon target takes the "swiftly burn through it" branch; both are ported.

using ModernUO.Serialization;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class AcidSac : Item
{
    [Constructible]
    public AcidSac() : base(0x0C67)
    {
        Stackable = true;
        Hue = 648;
    }

    public override double DefaultWeight => 1.0;

    public override int LabelNumber => 1111654; // acid sac

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1111656); // What do you wish to use the acid on?
            from.Target = new InternalTarget(this);
        }
        else
        {
            from.SendLocalizedMessage(1080063); // This must be in your backpack to use it.
        }
    }

    private class InternalTarget : Target
    {
        private readonly Item _item;

        public InternalTarget(Item item) : base(2, false, TargetFlags.None) => _item = item;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_item.Deleted)
            {
                return;
            }

            if (targeted is AddonComponent)
            {
                // D-67: the vine-wall branch. ServUO does nothing for any other addon component, so neither does this.
                return;
            }

            from.SendLocalizedMessage(1111657); // The acid swiftly burn through it.
            _item.Consume();
        }
    }
}
