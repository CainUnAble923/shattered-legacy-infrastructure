using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Clothing/MaceBelt.cs (CC9 batch 5).
    [SerializationGenerator(0, false)]
    public partial class MaceBelt : BaseWaist, IDyable
    {
        [Constructible]
        public MaceBelt() : base(0xA40C)
        {
            Layer = Layer.Waist;
        }

        public override double DefaultWeight => 3.0;
        public override int LabelNumber => 1126020;
    }
}
