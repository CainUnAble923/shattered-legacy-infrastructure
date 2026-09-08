using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/DragonJadeEarrings.cs (CC9 batch 4).
    // Dropped: IsArtifact (D-1); CanBeWornByGargoyles (D-2).
    // GargishEarrings is already on BaseSetArmor (S10), so this is a plain derivation.
    [SerializationGenerator(0, false)]
    public partial class DragonJadeEarrings : GargishEarrings
    {
        [Constructible]
        public DragonJadeEarrings() : base()
        {
            Hue = 2129;
            Attributes.BonusDex = 5;
            Attributes.BonusStr = 5;
            Attributes.RegenHits = 2;
            Attributes.RegenStam = 3;
            Attributes.LowerManaCost = 5;
            AbsorptionAttributes.EaterFire = 10;
        }

        public override int LabelNumber => 1113720;
        public override int BasePhysicalResistance => 9;
        public override int BaseFireResistance => 16;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 13;
        public override int BaseEnergyResistance => 3;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
