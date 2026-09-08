using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Jewelry/TurqouiseRing.cs (CC9 batch 3). Randomised on construction only.
    // The misspelling is ServUO's type name and is kept so the type matches every reference to it.
    [SerializationGenerator(0, false)]
    public partial class TurqouiseRing : GoldRing
    {
        [Constructible]
        public TurqouiseRing()
        {
            BaseRunicTool.ApplyAttributesTo(this, true, 0, Utility.RandomMinMax(1, 3), 0, 100);

            if (Utility.Random(100) < 10)
            {
                Attributes.WeaponSpeed += 5;
            }
            else
            {
                Attributes.WeaponDamage += 15;
            }
        }

        public override double DefaultWeight => 1.0;

        public override int LabelNumber => 1073460; // turquoise ring
    }
}
