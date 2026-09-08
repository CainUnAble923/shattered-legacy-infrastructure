using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/GiantPixieCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class GiantPixieCostume : BaseCostume
    {
        [Constructible]
        public GiantPixieCostume()
        {
            CostumeBody = 176;
        }

        public override string CreatureName => "giant pixie";

        public override int LabelNumber => 1114244; // giant pixie costume
    }
}
