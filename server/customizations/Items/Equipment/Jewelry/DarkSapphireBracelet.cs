using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Jewelry/DarkSapphireBracelet.cs (CC9 batch 3). Randomised on construction only.
    [SerializationGenerator(0, false)]
    public partial class DarkSapphireBracelet : GoldBracelet
    {
        [Constructible]
        public DarkSapphireBracelet()
        {
            BaseRunicTool.ApplyAttributesTo(this, true, 0, Utility.RandomMinMax(1, 4), 0, 100);

            if (Utility.Random(100) < 10)
            {
                Attributes.RegenMana += 2;
            }
            else
            {
                Resistances.Cold += 10;
            }
        }

        public override double DefaultWeight => 1.0;

        public override int LabelNumber => 1073455; // dark sapphire bracelet
    }
}
