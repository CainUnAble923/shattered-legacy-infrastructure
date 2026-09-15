using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/PetrifiedMatriarchsTongue.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class PetrifiedMatriarchsTongue : GoldRing
    {
        [Constructible]
        public PetrifiedMatriarchsTongue()
        {
            Hue = 2006;
            Attributes.RegenMana = 2;
            Attributes.AttackChance = 10;
            Attributes.CastSpeed = 1;
            Attributes.CastRecovery = 2;
            Attributes.LowerManaCost = 4;
            Resistances.Poison = 5;
        }

        public override int LabelNumber => 1115776;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
