using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/MuseumArmor.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class MinaxsArmor : FemaleStuddedChest
    {
        [Constructible]
        public MinaxsArmor()
        {
            Hue = 0x453;
            Attributes.RegenMana = 2;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1073257;
        public override int BasePhysicalResistance => 15;
        public override int BaseFireResistance => 5;
        public override int BaseColdResistance => 10;
        public override int BasePoisonResistance => 15;
        public override int BaseEnergyResistance => 25;
        public override int InitMinHits => 150;
        public override int InitMaxHits => 150;
    }

    // ServUO: Items/Artifacts/Equipment/Armor/MuseumArmor.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class KeeoneansChainMail : ChainChest
    {
        [Constructible]
        public KeeoneansChainMail()
        {
            Hue = 0x84E;
            Attributes.RegenHits = 3;
            Attributes.NightSight = 1;
            ArmorAttributes.MageArmor = 1;
        }

        public override int LabelNumber => 1073264;
        public override int BasePhysicalResistance => 15;
        public override int BaseFireResistance => 20;
        public override int BaseColdResistance => 15;
        public override int BasePoisonResistance => 10;
        public override int BaseEnergyResistance => 15;
        public override int InitMinHits => 150;
        public override int InitMaxHits => 150;
    }

    // ServUO: Items/Artifacts/Equipment/Armor/MuseumArmor.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class VesperOrderShield : OrderShield
    {
        [Constructible]
        public VesperOrderShield()
        {
            Hue = 0x835;
            Attributes.SpellChanneling = 1;
            Attributes.Luck = 80;
            Attributes.CastSpeed = -1;
            Attributes.AttackChance = 15;
            Attributes.DefendChance = 15;
        }

        public override int LabelNumber => 1073258;
        public override int BasePhysicalResistance => 1;
        public override int InitMinHits => 80;
        public override int InitMaxHits => 80;
    }

    // ServUO: Items/Artifacts/Equipment/Armor/MuseumArmor.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class VesperChaosShield : ChaosShield
    {
        [Constructible]
        public VesperChaosShield()
        {
            Hue = 0xFA;
            Attributes.SpellChanneling = 1;
            Attributes.CastRecovery = 2;
            Attributes.CastSpeed = 1;
            ArmorAttributes.SelfRepair = 1;
        }

        public override int LabelNumber => 1073259;
        public override int BasePhysicalResistance => 1;
        public override int InitMinHits => 80;
        public override int InitMaxHits => 80;
    }
}
