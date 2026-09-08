using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/FireElementalCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class FireElementalCostume : BaseCostume
    {
        [Constructible]
        public FireElementalCostume()
        {
            CostumeBody = 15;
        }

        public override string CreatureName => "fire elemental";

        public override int LabelNumber => 1114224; // fire elemental costume
    }
}
