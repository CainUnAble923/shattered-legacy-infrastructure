using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/GoreFiendCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class GoreFiendCostume : BaseCostume
    {
        [Constructible]
        public GoreFiendCostume()
        {
            CostumeBody = 305;
        }

        public override string CreatureName => "gore fiend";

        public override int LabelNumber => 1114227; // gore fiend costume
    }
}
