// cc-P123: a 2016 tinkering bulk-order reward (700 points), ported from ServUO
// Scripts/Services/BulkOrders/Items/PowderOfFortKeg.cs. A keg that holds up to 250 uses of powder of fortification
// (pinned's PowderOfTemperament): drop powder on it to fill it, double-click it to draw one use into the powder in your
// pack (or a new powder of one use).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class PowderOfFortKeg : Item
{
    public const int MaxCharges = 250;

    [SerializableField(0)]
    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _charges;

    [Constructible]
    public PowderOfFortKeg(int uses = 0) : base(0x1940)
    {
        _charges = uses;
        Hue = 2419;
    }

    public override int LabelNumber => 1157221; // A specially lined keg for powder of fortification.

    public override double DefaultWeight => 15.0;

    public override bool OnDragDrop(Mobile m, Item dropped)
    {
        if (dropped is PowderOfTemperament powder && _charges < MaxCharges)
        {
            if (powder.UsesRemaining + _charges > MaxCharges)
            {
                powder.UsesRemaining -= MaxCharges - _charges;
                Charges = MaxCharges;
            }
            else
            {
                Charges += powder.UsesRemaining;
                powder.Delete();
            }

            m.PlaySound(0x247);
        }

        return false;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from.Backpack == null || !IsChildOf(from.Backpack) || _charges <= 0)
        {
            return;
        }

        if (from.Backpack.FindItemByType<PowderOfTemperament>() is { } powder)
        {
            powder.UsesRemaining++;
            Charges--;
        }
        else
        {
            powder = new PowderOfTemperament(1);

            if (!from.Backpack.TryDropItem(from, powder, false))
            {
                from.SendLocalizedMessage(1080016); // That container cannot hold more weight.
                powder.Delete();
                return;
            }

            Charges--;
        }

        from.PlaySound(0x247);
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1060584, _charges); // uses remaining: ~1_val~

        var perc = (int)((double)_charges / MaxCharges * 100);

        list.Add(
            perc switch
            {
                <= 0  => 502246, // The keg is empty.
                < 5   => 502248, // The keg is nearly empty.
                < 20  => 502249, // The keg is not very full.
                < 30  => 502250, // The keg is about one quarter full.
                < 40  => 502251, // The keg is about one third full.
                < 47  => 502252, // The keg is almost half full.
                < 54  => 502254, // The keg is approximately half full.
                < 70  => 502253, // The keg is more than half full.
                < 80  => 502255, // The keg is about three quarters full.
                < 90  => 502256, // The keg is very full.
                < 100 => 502257, // The liquid is almost to the top of the keg.
                _     => 502258  // The keg is completely full.
            }
        );
    }
}
