using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/DreamWraithCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class DreamWraithCostume : BaseCostume
    {
        [Constructible]
        public DreamWraithCostume()
        {
            CostumeBody = 740;
        }

        public override string CreatureName => "dream wraith";

        public override int LabelNumber => 1114008; // dream wraith halloween costume
    }
}
