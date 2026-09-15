using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/MedusaStatue.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class MedusaStatue : Item
    {
        [Constructible]
        public MedusaStatue() : base(0x40BC)
        {
        }

        public override string DefaultName => "Medusa";
        public override double DefaultWeight => 10.0;
        public override int LabelNumber => 1113626;
    }
}
