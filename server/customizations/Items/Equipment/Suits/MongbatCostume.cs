using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/MongbatCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class MongbatCostume : BaseCostume
    {
        [Constructible]
        public MongbatCostume()
        {
            CostumeBody = 39;
        }

        public override string CreatureName => "mongbat";

        public override int LabelNumber => 1114223; // mongbat costume
    }
}
