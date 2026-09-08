using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/ObsidianEarrings.cs (CC9 batch 4).
    // Dropped: IsArtifact (D-1); CanBeWornByGargoyles (D-2).
    // GargishEarrings is already on BaseSetArmor (S10), so this is a plain derivation.
    [SerializationGenerator(0, false)]
    public partial class ObsidianEarrings : GargishEarrings
    {
        [Constructible]
        public ObsidianEarrings() : base()
        {
            Attributes.BonusMana = 8;
            Attributes.RegenMana = 2;
            Attributes.RegenStam = 2;
            Attributes.SpellDamage = 8;
            AbsorptionAttributes.CastingFocus = 4;
        }

        public override int LabelNumber => 1113820;
        public override int BasePhysicalResistance => 4;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 10;
        public override int BasePoisonResistance => 3;
        public override int BaseEnergyResistance => 13;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
