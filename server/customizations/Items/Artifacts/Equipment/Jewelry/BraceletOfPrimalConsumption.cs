using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/BraceletOfPrimalConsumption.cs (CC9 batch 4).
    // ServUO derives from GoldBracelet and gets set/absorption state from BaseJewel. Here that state lives on
    // BaseSetJewel (S10), so the piece derives from that and reproduces stock GoldBracelet's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class BraceletOfPrimalConsumption : BaseSetJewel
    {
        [Constructible]
        public BraceletOfPrimalConsumption() : base(0x1086, Layer.Bracelet)
        {
            AbsorptionAttributes.EaterDamage = 6;
            Attributes.Luck = 200;
            Resistances.Physical = 20;
            Resistances.Fire = 20;
            Resistances.Cold = 20;
            Resistances.Poison = 20;
            Resistances.Energy = 20;
        }

        public override int LabelNumber => 1157350;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock GoldBracelet members, reproduced because the parent changed.
        public override double DefaultWeight => 0.1;
        public override int BaseGemTypeNumber => 1044221; // star sapphire bracelet
    }
}
