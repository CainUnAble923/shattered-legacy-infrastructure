using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/VoidWandererCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class VoidWandererCostume : BaseCostume
    {
        [Constructible]
        public VoidWandererCostume()
        {
            CostumeBody = 316;
        }

        public override string CreatureName => "wanderer of the void";

        public override int LabelNumber => 1114286; // void wanderer costume
    }
}
