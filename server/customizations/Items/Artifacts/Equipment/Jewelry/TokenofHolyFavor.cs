using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/TokenofHolyFavor.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class TokenOfHolyFavor : GoldBracelet
    {
        [Constructible]
        public TokenOfHolyFavor()
        {
            Hue = 96;
            Attributes.BonusHits = 5;
            Attributes.CastRecovery = 2;
            Attributes.CastSpeed = 1;
            Attributes.DefendChance = 10;
            Attributes.AttackChance = 10;
            Attributes.SpellDamage = 4;
            Resistances.Cold = 5;
            Resistances.Poison = 5;
        }

        public override int LabelNumber => 1113652;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
