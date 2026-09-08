using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/PixieCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class PixieCostume : BaseCostume
    {
        [Constructible]
        public PixieCostume()
        {
            CostumeBody = 128;
        }

        public override string CreatureName => "pixie";

        public override int LabelNumber => 1114236; // pixie costume
    }
}
