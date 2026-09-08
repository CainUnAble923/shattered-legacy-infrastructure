using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/DrakeCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class DrakeCostume : BaseCostume
    {
        [Constructible]
        public DrakeCostume()
        {
            CostumeBody = 60;
        }

        public override string CreatureName => "drake";

        public override int LabelNumber => 1114245; // drake costume
    }
}
