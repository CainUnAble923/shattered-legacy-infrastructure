using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Clothing/DaggerBelt.cs (CC9 batch 5).
    [SerializationGenerator(0, false)]
    public partial class DaggerBelt : BaseWaist, IDyable
    {
        [Constructible]
        public DaggerBelt() : base(0xA40E)
        {
            Layer = Layer.Waist;
        }

        public override double DefaultWeight => 3.0;
        public override int LabelNumber => 1159210;
    }
}
