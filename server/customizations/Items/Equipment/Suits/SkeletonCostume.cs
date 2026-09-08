using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/SkeletonCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class SkeletonCostume : BaseCostume
    {
        [Constructible]
        public SkeletonCostume()
        {
            CostumeBody = 50;
        }

        public override string CreatureName => "skeleton";

        public override int LabelNumber => 1113996; // skeleton halloween costume
    }
}
