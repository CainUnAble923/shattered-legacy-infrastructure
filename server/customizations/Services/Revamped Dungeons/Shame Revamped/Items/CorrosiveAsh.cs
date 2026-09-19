// ServUO: Services/Revamped Dungeons/Shame Revamped/Items/CorrosiveAsh.cs (CC4 Shame).
//
// One of the three Whetstone of Enervation components. Double-clicking any one of them with the other two in
// the pack consumes all three and makes the whetstone. Only the level guardians drop these: Quartz Grit from
// the Quartz Elemental, Corrosive Ash from the Flame Elemental, Cursed Oilstone from the Wind Elemental.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class CorrosiveAsh : Item
{
    [Constructible]
    public CorrosiveAsh(int amount = 1) : base(0x423A)
    {
        Hue = 1360;

        Stackable = true;
        Amount = amount;
    }

    public override int LabelNumber => 1151809; // Corrosive Ash
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
        else if (from.Backpack.GetAmount(typeof(CursedOilstone)) == 0)
        {
            from.SendLocalizedMessage(1151813, "#1151810"); // You do not have a required component: ~1_val~
        }
        else
        {
            from.Backpack.ConsumeTotal(new[] { typeof(CursedOilstone), typeof(QuartzGrit) }, new[] { 1, 1 });

            Consume();

            from.AddToBackpack(new WhetstoneOfEnervation());
            from.SendLocalizedMessage(1151812); // You have managed to form the items into a rancid smelling, crag covered, hardened lump. In a moment of prescience, you realize what it must be named. The Whetstone of Enervation!
        }
    }
}
