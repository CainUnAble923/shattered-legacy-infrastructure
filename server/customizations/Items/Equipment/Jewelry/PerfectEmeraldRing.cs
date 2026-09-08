using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Jewelry/PerfectEmeraldRing.cs (CC9 batch 3). Randomised on construction only.
    [SerializationGenerator(0, false)]
    public partial class PerfectEmeraldRing : GoldRing
    {
        [Constructible]
        public PerfectEmeraldRing()
        {
            BaseRunicTool.ApplyAttributesTo(this, true, 0, Utility.RandomMinMax(2, 4), 0, 100);

            if (Utility.RandomBool())
            {
                Resistances.Poison += 10;
            }
            else
            {
                Attributes.SpellDamage += 5;
            }
        }

        public override double DefaultWeight => 1.0;

        public override int LabelNumber => 1073459; // perfect emerald ring
    }
}
