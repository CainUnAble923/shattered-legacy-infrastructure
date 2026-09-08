using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/SkitteringHopperCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class SkitteringHopperCostume : BaseCostume
    {
        [Constructible]
        public SkitteringHopperCostume()
        {
            CostumeBody = 302;
        }

        public override string CreatureName => "skittering hopper";

        public override int LabelNumber => 1114240; // skittering hopper costume
    }
}
