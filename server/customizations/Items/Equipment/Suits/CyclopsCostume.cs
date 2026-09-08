using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/CyclopsCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class CyclopsCostume : BaseCostume
    {
        [Constructible]
        public CyclopsCostume()
        {
            CostumeBody = 75;
        }

        public override string CreatureName => "cyclops";

        public override int LabelNumber => 1114234; // cyclops costume
    }
}
