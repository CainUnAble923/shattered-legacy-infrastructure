using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/ExodusMinionCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class ExodusMinionCostume : BaseCostume
    {
        [Constructible]
        public ExodusMinionCostume()
        {
            CostumeBody = 757;
        }

        public override string CreatureName => "exodus minion";

        public override int LabelNumber => 1114239; // exodus minion costume
    }
}
