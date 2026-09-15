using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/ShroudOfTheCondemned.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1); CanBeWornByGargoyles (D-2).
    [Flippable(0x1F03, 0x1F04)]
    [SerializationGenerator(0, false)]
    public partial class ShroudOfTheCondemned : BaseOuterTorso
    {
        [Constructible]
        public ShroudOfTheCondemned() : base(0x1F04, 0xD6)
        {
            Hue = 2075;
            Attributes.BonusHits = 3;
            Attributes.BonusInt = 5;
        }

        public override int LabelNumber => 1113703;
    }
}
