using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/DefenderOfTheMagus.cs (CC9 batch 4).
    // ServUO derives from MetalShield and gets absorption state from BaseArmor (through BaseShield). Here that
    // state lives on BaseSetShield (S10), so the piece derives from that and reproduces stock MetalShield's own
    // members. Both random rolls happen once in the constructor, exactly as ServUO does.
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class DefenderOfTheMagus : BaseSetShield
    {
        [Constructible]
        public DefenderOfTheMagus() : base(0x1B7B)
        {
            Hue = 590;
            Attributes.SpellChanneling = 1;
            Attributes.DefendChance = 10;
            Attributes.CastRecovery = 1;

            // Random resonance
            switch (Utility.Random(5))
            {
                case 0: AbsorptionAttributes.ResonanceCold = 10; break;
                case 1: AbsorptionAttributes.ResonanceFire = 10; break;
                case 2: AbsorptionAttributes.ResonanceKinetic = 10; break;
                case 3: AbsorptionAttributes.ResonancePoison = 10; break;
                case 4: AbsorptionAttributes.ResonanceEnergy = 10; break;
            }

            // Random resist
            switch (Utility.Random(5))
            {
                case 0: ColdBonus = 10; break;
                case 1: FireBonus = 10; break;
                case 2: PhysicalBonus = 10; break;
                case 3: PoisonBonus = 10; break;
                case 4: EnergyBonus = 10; break;
            }
        }

        public override int LabelNumber => 1113851; // Defender of the Magus

        public override int BasePhysicalResistance => 0;
        public override int BaseFireResistance => 1;
        public override int BaseColdResistance => 0;
        public override int BasePoisonResistance => 0;
        public override int BaseEnergyResistance => 0;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock MetalShield members, reproduced because the parent changed.
        public override double DefaultWeight => 6.0;
        public override int AosStrReq => 45;
        public override int ArmorBase => 11;
    }
}
