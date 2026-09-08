using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/WolfSpiderCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class WolfSpiderCostume : BaseCostume
    {
        [Constructible]
        public WolfSpiderCostume()
        {
            CostumeBody = 736;
        }

        public override string CreatureName => "wolf spider";

        public override int LabelNumber => 1114232; // wolf spider costume
    }
}
