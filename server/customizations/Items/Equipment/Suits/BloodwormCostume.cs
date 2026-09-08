using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/BloodwormCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class BloodwormCostume : BaseCostume
    {
        [Constructible]
        public BloodwormCostume()
        {
            CostumeBody = 287;
        }

        public override string CreatureName => "bloodworm";

        public override int LabelNumber => 1114006; // bloodworm halloween costume
    }
}
