using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/ConjurersGarbHarbringer.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1); CanBeWornByGargoyles (D-2).
    [Flippable(0x1F03, 0x1F04)]
    [SerializationGenerator(0, false)]
    public partial class ConjureresGarbHarbringer : BaseOuterTorso
    {
        [Constructible]
        public ConjureresGarbHarbringer() : base(0x1F03, 0x486)
        {
            Hue = 0x4AA;
            Attributes.DefendChance = 5;
            Attributes.RegenMana = 2;
        }

        public override double DefaultWeight => 3.0;
        public override int LabelNumber => 1114052;
    }
}
