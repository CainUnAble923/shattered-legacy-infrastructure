using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/SatyrCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class SatyrCostume : BaseCostume
    {
        [Constructible]
        public SatyrCostume()
        {
            CostumeBody = 271;
        }

        public override string CreatureName => "satyr";

        public override int LabelNumber => 1114287; // satyr costume
    }
}
