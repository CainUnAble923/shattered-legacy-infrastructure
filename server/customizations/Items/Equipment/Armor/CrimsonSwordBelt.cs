using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/CrimsonSwordBelt.cs (CC9 batch 5).
    [SerializationGenerator(0, false)]
    public partial class CrimsonSwordBelt : SwordBelt
    {
        [Constructible]
        public CrimsonSwordBelt()
        {
            Attributes.BonusDex = 5;
            Attributes.BonusHits = 10;
            Attributes.RegenHits = 2;
        }

        public override int LabelNumber => 1159212;
    }
}
