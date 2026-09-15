using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/BurningAmber.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class BurningAmber : GoldRing
    {
        [Constructible]
        public BurningAmber()
        {
            Hue = 1174;
            Attributes.CastRecovery = 3;
            Attributes.RegenMana = 2;
            Attributes.BonusDex = 5;
            Resistances.Fire = 20;
        }

        public override int LabelNumber => 1114790;
    }
}
