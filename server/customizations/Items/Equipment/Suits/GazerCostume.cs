using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/GazerCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class GazerCostume : BaseCostume
    {
        [Constructible]
        public GazerCostume()
        {
            CostumeBody = 22;
        }

        public override string CreatureName => "gazer";

        public override int LabelNumber => 1114004; // gazer halloween costume
    }
}
