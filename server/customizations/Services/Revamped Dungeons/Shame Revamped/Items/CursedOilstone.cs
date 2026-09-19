// ServUO: Services/Revamped Dungeons/Shame Revamped/Items/CursedOilstone.cs (CC4 Shame).
//
// One of the three Whetstone of Enervation components; see CorrosiveAsh.cs. Dropped by the Wind Elemental.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class CursedOilstone : Item
{
    [Constructible]
    public CursedOilstone(int amount = 1) : base(0x0F8B)
    {
        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1151810; // Cursed Oilstone
    public override double DefaultWeight => 1;

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
        }
        else if (from.Backpack.GetAmount(typeof(QuartzGrit)) == 0)
        {
            from.SendLocalizedMessage(1151813, "#1151808"); // You do not have a required component: ~1_val~
        }
        else if (from.Backpack.GetAmount(typeof(CorrosiveAsh)) == 0)
        {
            from.SendLocalizedMessage(1151813, "#1151809"); // You do not have a required component: ~1_val~
        }
        else
        {
            from.Backpack.ConsumeTotal(new[] { typeof(QuartzGrit), typeof(CorrosiveAsh) }, new[] { 1, 1 });

            Consume();

            from.AddToBackpack(new WhetstoneOfEnervation());
            from.SendLocalizedMessage(1151812); // You have managed to form the items into a rancid smelling, crag covered, hardened lump. In a moment of prescience, you realize what it must be named. The Whetstone of Enervation!
        }
    }
}
