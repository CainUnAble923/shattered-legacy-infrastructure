// ServUO: Services/Revamped Dungeons/Shame Revamped/Items/QuartzGrit.cs (CC4 Shame).
//
// One of the three Whetstone of Enervation components; see CorrosiveAsh.cs. Dropped by the Quartz Elemental,
// which only an altar summons, so until Services/PointsSystems (S7) lands this is the component a player
// cannot get without a GM zeroing an altar's SummonCost (notes/cc4-shame.md).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class QuartzGrit : Item
{
    [Constructible]
    public QuartzGrit(int amount = 1) : base(0x423A)
    {
        Hue = 1151;

        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1151808; // Quartz Grit
    public override double DefaultWeight => 1;

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
        }
        else if (from.Backpack.GetAmount(typeof(CursedOilstone)) == 0)
        {
            from.SendLocalizedMessage(1151813, "#1151810"); // You do not have a required component: ~1_val~
        }
        else if (from.Backpack.GetAmount(typeof(CorrosiveAsh)) == 0)
        {
            from.SendLocalizedMessage(1151813, "#1151809"); // You do not have a required component: ~1_val~
        }
        else
        {
            from.Backpack.ConsumeTotal(new[] { typeof(CursedOilstone), typeof(CorrosiveAsh) }, new[] { 1, 1 });

            Consume();

            from.AddToBackpack(new WhetstoneOfEnervation());
            from.SendLocalizedMessage(1151812); // You have managed to form the items into a rancid smelling, crag covered, hardened lump. In a moment of prescience, you realize what it must be named. The Whetstone of Enervation!
        }
    }
}
