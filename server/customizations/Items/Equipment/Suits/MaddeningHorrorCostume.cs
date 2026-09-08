using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/MaddeningHorrorCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class MaddeningHorrorCostume : BaseCostume
    {
        [Constructible]
        public MaddeningHorrorCostume()
        {
            CostumeBody = 721;
        }

        public override string CreatureName => "maddening horror";

        public override int LabelNumber => 1114233; // maddening horror costume
    }
}
