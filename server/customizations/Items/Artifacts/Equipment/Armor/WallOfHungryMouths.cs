using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/WallOfHungryMouths.cs (CC9 batch 4).
    // ServUO derives from HeaterShield and gets set/absorption state from BaseArmor (through BaseShield). Here that state lives on
    // BaseSetShield (S10), so the piece derives from that and reproduces stock HeaterShield's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1).
    [TypeAlias("Server.Items.WallofHungryMouths")]
    [SerializationGenerator(0, false)]
    public partial class WallOfHungryMouths : BaseSetShield
    {
        [Constructible]
        public WallOfHungryMouths() : base(0x1B76)
        {
            Hue = 1034;
            AbsorptionAttributes.EaterEnergy = 20;
            AbsorptionAttributes.EaterPoison = 20;
            AbsorptionAttributes.EaterCold = 20;
            AbsorptionAttributes.EaterFire = 20;
        }

        public override int LabelNumber => 1113722;
        public override int BasePhysicalResistance => 5;
        public override int BaseFireResistance => 1;
        public override int BaseColdResistance => 0;
        public override int BasePoisonResistance => 0;
        public override int BaseEnergyResistance => 0;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock HeaterShield members, reproduced because the parent changed.
        public override double DefaultWeight => 8.0;
        public override int AosStrReq => 90;
        public override int ArmorBase => 23;
    }
}
