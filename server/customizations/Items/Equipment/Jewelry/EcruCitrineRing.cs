using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Jewelry/EcruCitrineRing.cs (CC9 batch 3). Randomised on construction only.
    [SerializationGenerator(0, false)]
    public partial class EcruCitrineRing : GoldRing
    {
        [Constructible]
        public EcruCitrineRing()
        {
            BaseRunicTool.ApplyAttributesTo(this, true, 0, Utility.RandomMinMax(2, 3), 0, 100);

            if (Utility.RandomBool())
            {
                Attributes.EnhancePotions = 50;
            }
            else
            {
                Attributes.BonusStr += 5;
            }
        }

        public override double DefaultWeight => 1.0;

        public override int LabelNumber => 1073457; // ecru citrine ring
    }
}
