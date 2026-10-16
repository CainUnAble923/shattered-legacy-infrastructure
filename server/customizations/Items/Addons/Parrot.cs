// ServUO: Items/Addons/Parrot.cs, the ParrotItem (cc-P129). The pet parrot the ML peerless bosses drop at 60% (Chief
// Paroxysmus, the Crimson Dragon, Lady Melisande, the Monstrous Interred Grizzle) and the 10% LootPack.Parrot that
// pinned carries inside a comment block (LootPack.cs:514-525, "once added Legacy"), reproduced here as
// ParrotItem.LootPack for the Crystal Hydra (D-72, retired by this port) and Lady Melisande. Placed on a parrot perch
// in a house the player co-owns, it becomes a PetParrot (ported beside it); the perch's Parrot link is
// server/patches/ParrotPerchAddon-pet-parrot.patch. Values verbatim; serialization by the generator.

using ModernUO.Serialization;
using Server.Mobiles;
using Server.Multis;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ParrotItem : Item
{
    private static readonly int[] _hues =
    {
        0x3, 0xD, 0x13, 0x1C, 0x21, 0x30, 0x3F, 0x44, 0x59, 0x62, 0x71
    };

    // Pinned LootPack.cs:514-525, uncommented: a 10% parrot.
    public static readonly LootPack LootPack = new(new[] { new LootPackEntry(false, [new LootPackItem(typeof(ParrotItem), 1)], 10.00, 1) });

    [Constructible]
    public ParrotItem() : base(0x20EE)
    {
        Weight = 1;
        Hue = Utility.RandomList(_hues);
    }

    public override int LabelNumber => 1074281; // pet parrot

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack))
        {
            from.Target = new InternalTarget(this);
            from.SendLocalizedMessage(1072612); // Target the Parrot Perch you wish to place this Parrot upon.
        }
        else
        {
            from.SendLocalizedMessage(1042004); // That must be in your pack for you to use it.
        }
    }

    public class InternalTarget : Target
    {
        private readonly ParrotItem _parrot;

        public InternalTarget(ParrotItem parrot) : base(2, false, TargetFlags.None)
        {
            _parrot = parrot;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is AddonComponent { Addon: ParrotPerchAddon perch })
            {
                var house = BaseHouse.FindHouseAt(perch);

                if (house != null && house.IsCoOwner(from))
                {
                    if (perch.Parrot == null || perch.Parrot.Deleted)
                    {
                        var parrot = new PetParrot { Hue = _parrot.Hue };
                        parrot.MoveToWorld(perch.Location, perch.Map);
                        parrot.Z += 12;

                        perch.Parrot = parrot;
                        _parrot.Delete();
                    }
                    else
                    {
                        from.SendLocalizedMessage(1072616); // That Parrot Perch already has a Parrot.
                    }
                }
                else
                {
                    // Parrots can only be placed on Parrot Perches in houses where you are an owner or co-owner.
                    from.SendLocalizedMessage(1072618);
                }
            }
            else
            {
                from.SendLocalizedMessage(1072614); // You must place the Parrot on a Parrot Perch.
            }
        }

        protected override void OnTargetOutOfRange(Mobile from, object targeted)
        {
            base.OnTargetOutOfRange(from, targeted);

            from.SendLocalizedMessage(1072613); // You must be closer to the Parrot Perch to place the Parrot upon it.
        }
    }
}
