using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/CrimsonDaggerBelt.cs (CC9 batch 5).
    [SerializationGenerator(0, false)]
    public partial class CrimsonDaggerBelt : DaggerBelt
    {
        [Constructible]
        public CrimsonDaggerBelt()
        {
            Attributes.BonusDex = 5;
            Attributes.BonusHits = 10;
            Attributes.RegenHits = 2;
        }

        public override int LabelNumber => 1159213;
    }
}
