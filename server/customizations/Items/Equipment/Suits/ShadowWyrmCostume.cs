using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/ShadowWyrmCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class ShadowWyrmCostume : BaseCostume
    {
        [Constructible]
        public ShadowWyrmCostume()
        {
            CostumeBody = 106;
            CostumeHue = 0;
        }

        public override string CreatureName => "shadow wyrm";

        public override int LabelNumber => 1114009; // shadow wyrm halloween costume
    }
}
