using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/MummifiedCorpse.cs (CC9 batch 5).
    [SerializationGenerator(0, false)]
    public partial class MummifiedCorpse : Item
    {
        [Constructible]
        public MummifiedCorpse() : base(0x1C20)
        {
        }

        public override int LabelNumber => 1112400;
    }
}
