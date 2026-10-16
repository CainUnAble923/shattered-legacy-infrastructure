// cc-P128: ServUO pub57 Scripts/Items/Resource/ScouringToxin.cs -> ours, a forced leaf dependency of the basket weaving
// port: scouring toxin, ten uses. Used on two dry reeds of one colour in the pack by someone who has read the
// basket-weaving book it makes softened reeds of that colour; used on anything the Tokuno pigments dye it strips the
// item's hue; and it is the second resource of the softened-reeds craft entries. ServUO's only source is the Underworld
// Maze of Death puzzle (Services/Underworld/Maze of Death/UnderworldPuzzle.cs:96), which is the Underworld row's to
// port, so until then only staff can make one.

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ScouringToxin : Item, IUsesRemaining, ICommodity
{
    [SerializableField(0, fieldChanged: nameof(OnUsesRemainingChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _usesRemaining;

    [Constructible]
    public ScouringToxin(int amount = 10) : base(0x1848) => _usesRemaining = amount;

    public override int LabelNumber => 1112292; // scouring toxin

    private void OnUsesRemainingChanged(int oldValue, int newValue)
    {
        if (newValue <= 0)
        {
            Delete();
        }
        else
        {
            InvalidateProperties();
        }
    }

    public bool ShowUsesRemaining
    {
        get => false;
        set
        {
        }
    }

    int ICommodity.DescriptionNumber => LabelNumber;
    bool ICommodity.IsDeedable => true;

    public override void AddNameProperty(IPropertyList list)
    {
        if (_usesRemaining <= 1)
        {
            list.Add(LabelNumber);
        }
        else
        {
            list.Add(1050039, $"{_usesRemaining}\t#{LabelNumber}"); // ~1_NUMBER~ ~2_ITEMNAME~
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1112348); // Which item do you wish to scour?
            from.BeginTarget(-1, false, TargetFlags.None, OnTarget);
        }
    }

    /// <summary>ServUO's target: the message sent, or 0 when something was scoured.</summary>
    public int Scour(Mobile from, object targeted)
    {
        if (targeted is not Item item)
        {
            return 1112349; // You cannot scour that!
        }

        if (item.Parent is Mobile)
        {
            return 1112350; // You cannot scour items that are being worn!
        }

        if (item.IsLockedDown || item.IsSecure)
        {
            return 1112351; // You may not scour items which are locked down.
        }

        if (item.QuestItem)
        {
            return 1151837; // You may not scour toggled quest items.
        }

        if (item is DryReeds reed1)
        {
            if (!BasketWeaving.Knows(from))
            {
                return 1112253; // You haven't learned basket weaving. Perhaps studying a book would help!
            }

            var cont = from.Backpack;

            if (!reed1.IsChildOf(cont))
            {
                return 1116249; // That must be in your backpack for you to use it.
            }

            var hue = reed1.PlantHue;
            var list = new List<Item>();
            var total = 0;

            foreach (var check in cont.FindItemsByType<DryReeds>())
            {
                if (check.PlantHue == hue)
                {
                    total += check.Amount;
                    list.Add(check);
                }
            }

            if (list.Count == 0 || total <= 1)
            {
                return 1112250; // You don't have enough of this type of dry reeds to make that.
            }

            var toConsume = 2;

            foreach (var it in list)
            {
                if (it.Amount >= toConsume)
                {
                    it.Consume(toConsume);
                    toConsume = 0;
                }
                else
                {
                    toConsume -= it.Amount;
                    it.Delete();
                }

                if (toConsume <= 0)
                {
                    break;
                }
            }

            var sReed = new SoftenedReeds(hue);

            if (!cont.TryDropItem(from, sReed, false))
            {
                sReed.MoveToWorld(from.Location, from.Map);
            }

            UsesRemaining--;
            from.PlaySound(0x23E);
            return 0;
        }

        if (BasePigmentsOfTokuno.IsValidItem(item))
        {
            from.PlaySound(0x23E);
            item.Hue = 0;
            UsesRemaining--;
            return 0;
        }

        return 1112349; // You cannot scour that!
    }

    private void OnTarget(Mobile from, object targeted)
    {
        var message = Scour(from, targeted);

        if (message != 0)
        {
            from.SendLocalizedMessage(message);
        }
    }
}
