using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Suits/ZombieCostume.cs (CC9). Template costume on BaseCostume.
    [SerializationGenerator(0, false)]
    public partial class ZombieCostume : BaseCostume
    {
        [Constructible]
        public ZombieCostume()
        {
            CostumeBody = 3;
        }

        public override string CreatureName => "zombie";

        public override int LabelNumber => 1114222; // zombie costume
    }
}
