using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/SashOfMight.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO chains base(0x1541) into BodySash(int hue), so the sash is briefly hue 0x1541 and then Hue = 0x481; base() here.
    [Flippable( 0x1541, 0x1542 )]
    [SerializationGenerator(0, false)]
    public partial class SashOfMight : BodySash
    {
        [Constructible]
        public SashOfMight()
        {
            Hue = 0x481;
        }

        public override int LabelNumber => 1075412;
    }
}
