using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/DragonWolfCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class DragonWolfCostume : BaseCostume
    {
        [Constructible]
        public DragonWolfCostume()
        {
            CostumeBody = 719;
        }

        public override string CreatureName => "dragon wolf";

        public override string DefaultName => "a dragon wolf costume";
    }
}
