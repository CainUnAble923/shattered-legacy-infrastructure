using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/SabertoothedTigerCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class SabertoothedTigerCostume : BaseCostume
    {
        [Constructible]
        public SabertoothedTigerCostume()
        {
            CostumeBody = 0x588;
        }

        public override string CreatureName => "saber-toothed tiger";

        public override string DefaultName => "a saber-toothed tiger costume";
    }
}
