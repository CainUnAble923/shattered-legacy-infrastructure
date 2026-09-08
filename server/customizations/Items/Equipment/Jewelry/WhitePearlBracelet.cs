using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Jewelry/WhitePearlBracelet.cs (CC9 batch 3). Randomised on construction only.
    [SerializationGenerator(0, false)]
    public partial class WhitePearlBracelet : GoldBracelet
    {
        [Constructible]
        public WhitePearlBracelet()
        {
            Attributes.NightSight = 1;

            BaseRunicTool.ApplyAttributesTo(this, true, 0, Utility.RandomMinMax(3, 5), 0, 100);

            if (Utility.Random(100) < 50)
            {
                switch (Utility.Random(3))
                {
                    case 0:
                        Attributes.CastSpeed += 1;
                        break;
                    case 1:
                        Attributes.CastRecovery += 2;
                        break;
                    case 2:
                        Attributes.LowerRegCost += 10;
                        break;
                }
            }
        }

        public override double DefaultWeight => 1.0;

        public override int LabelNumber => 1073456; // white pearl bracelet
    }
}
