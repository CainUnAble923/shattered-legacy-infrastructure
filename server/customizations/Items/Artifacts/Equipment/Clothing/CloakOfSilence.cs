using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/CloakOfSilence.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [Flippable(0x2FB9, 0x3173)]
    [SerializationGenerator(0, false)]
    public partial class CloakOfSilence : BaseOuterTorso
    {
        [Constructible]
        public CloakOfSilence() : base(0x2FB9)
        {
            Hue = 0x2A0;
            SkillBonuses.SetValues(0, SkillName.Stealth, 10);
        }

        public override double DefaultWeight => 2.0;
        public override int LabelNumber => 1112883;
    }
}
