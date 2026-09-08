using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/OniCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class OniCostume : BaseCostume
    {
        [Constructible]
        public OniCostume()
        {
            CostumeBody = 241;
        }

        public override string CreatureName => "oni";

        public override int LabelNumber => 1114242; // oni costume
    }
}
