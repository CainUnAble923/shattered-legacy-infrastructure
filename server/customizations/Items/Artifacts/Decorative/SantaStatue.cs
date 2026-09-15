using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/SantaStatue.cs (CC9 batch 5).
    // ServUO's ForceShowProperties => ObjectPropertyList.Enabled is Core.AOS on ModernUO.
    [Flippable(0x4A9A, 0x4A9B)]
    [SerializationGenerator(0, false)]
    public partial class SantaStatue : MonsterStatuette
    {
        [Constructible]
        public SantaStatue() : base(MonsterStatuetteType.Santa)
        {
        }

        public override double DefaultWeight => 10.0;
        public override int LabelNumber => 1097968;
        public override bool ForceShowProperties => Core.AOS;
    }
}
