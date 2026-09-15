using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/GlovesOfFeudalGrip.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO carries a Deserialize version fix-up for old instances; none exist here, so version 0 with no fix-up.
    [SerializationGenerator(0, false)]
    public partial class GlovesOfFeudalGrip : DragonGloves
    {
        [Constructible]
        public GlovesOfFeudalGrip()
        {
            Resource = CraftResource.None;
            Attributes.BonusStr = 8;
            Attributes.BonusStam = 8;
            Attributes.RegenHits = 3;
            Attributes.RegenMana = 3;
            Attributes.WeaponDamage = 30;
        }

        public override int LabelNumber => 1157349;
        public override int BasePhysicalResistance => 15;
        public override int BaseFireResistance => 15;
        public override int BaseColdResistance => 15;
        public override int BasePoisonResistance => 15;
        public override int BaseEnergyResistance => 15;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
        public override CraftResource DefaultResource => CraftResource.None;
    }

    // ServUO: Items/Artifacts/Equipment/Armor/GlovesOfFeudalGrip.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO carries a Deserialize version fix-up for old instances; none exist here, so version 0 with no fix-up.
    [SerializationGenerator(0, false)]
    public partial class GargishKiltOfFeudalVise : GargishPlateKilt
    {
        [Constructible]
        public GargishKiltOfFeudalVise()
        {
            Resource = CraftResource.None;
            Attributes.BonusStr = 8;
            Attributes.BonusStam = 8;
            Attributes.RegenHits = 3;
            Attributes.RegenMana = 3;
            Attributes.WeaponDamage = 30;
        }

        public override int LabelNumber => 1157367;
        public override int BasePhysicalResistance => 15;
        public override int BaseFireResistance => 15;
        public override int BaseColdResistance => 15;
        public override int BasePoisonResistance => 15;
        public override int BaseEnergyResistance => 15;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
        public override CraftResource DefaultResource => CraftResource.None;
    }
}
