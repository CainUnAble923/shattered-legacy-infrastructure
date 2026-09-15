using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Clothing/SwordBelt.cs (CC9 batch 5).
    [SerializationGenerator(0, false)]
    public partial class SwordBelt : BaseWaist, IDyable
    {
        [Constructible]
        public SwordBelt() : base(0xA40D)
        {
            Layer = Layer.Waist;
        }

        public override double DefaultWeight => 3.0;
        public override int LabelNumber => 1126021;
    }
}
