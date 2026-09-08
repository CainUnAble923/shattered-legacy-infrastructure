using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/Lavaliere.cs (CC9 batch 4).
    // ServUO derives from GoldNecklace and gets set/absorption state from BaseJewel. Here that state lives on
    // BaseSetJewel (S10), so the piece derives from that and reproduces stock GoldNecklace's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class Lavaliere : BaseSetJewel
    {
        [Constructible]
        public Lavaliere() : base(0x1088, Layer.Neck)
        {
            Hue = 1194;
            AbsorptionAttributes.EaterKinetic = 20;
            Attributes.DefendChance = 10;
            Resistances.Physical = 15;
            Attributes.LowerManaCost = 10;
            Attributes.LowerRegCost = 20;
        }

        public override int LabelNumber => 1114843;

        // Stock GoldNecklace members, reproduced because the parent changed.
        public override double DefaultWeight => 0.1;
        public override int BaseGemTypeNumber => 1044241; // star sapphire necklace
    }
}
