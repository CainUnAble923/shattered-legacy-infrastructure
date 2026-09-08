using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Jewelry/FireRubyBracelet.cs (CC9 batch 3). Randomised on construction only.
    [SerializationGenerator(0, false)]
    public partial class FireRubyBracelet : GoldBracelet
    {
        [Constructible]
        public FireRubyBracelet()
        {
            BaseRunicTool.ApplyAttributesTo(this, true, 0, Utility.RandomMinMax(1, 4), 0, 100);

            if (Utility.Random(100) < 10)
            {
                Attributes.RegenHits += 2;
            }
            else
            {
                Resistances.Fire += 10;
            }
        }

        public override double DefaultWeight => 1.0;

        public override int LabelNumber => 1073454; // fire ruby bracelet
    }
}
