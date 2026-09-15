using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/DemonBridleRing.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class DemonBridleRing : GoldRing
    {
        [Constructible]
        public DemonBridleRing()
        {
            Hue = 39;
            Attributes.CastRecovery = 2;
            Attributes.CastSpeed = 1;
            Attributes.RegenHits = 1;
            Attributes.RegenMana = 1;
            Attributes.DefendChance = 10;
            Attributes.LowerManaCost = 4;
            Resistances.Fire = 5;
        }

        public override int LabelNumber => 1113651;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
