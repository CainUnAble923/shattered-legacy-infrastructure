using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/MysticsGarb.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1); CanBeWornByGargoyles (D-2).
    // ServUO carries a Deserialize version fix-up for old instances; none exist here, so version 0 with no fix-up.
    [SerializationGenerator(0, false)]
    public partial class MysticsGarb : Robe
    {
        [Constructible]
        public MysticsGarb()
        {
            ItemID = 0x4000;
            Hue = 1420;
            Attributes.BonusMana = 5;
            Attributes.LowerManaCost = 1;
        }

        public override int LabelNumber => 1113649;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
