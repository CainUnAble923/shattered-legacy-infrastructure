using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Glasses/CollectionsBritLibraryGlasses.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1); [Alterable] (D-20: pinned ModernUO has no alter-item system, zero consumers of the name); WeaponAttributes.HitLowerDefend = 30 (D-22: no WeaponAttributes on ModernUO armour or clothing).
    [SerializationGenerator(0, false)]
    public partial class MaceAndShieldGlasses : Glasses
    {
        [Constructible]
        public MaceAndShieldGlasses()
        {
            Hue = 0x1DD;
            Attributes.BonusStr = 10;
            Attributes.BonusDex = 5;
        }

        public override int LabelNumber => 1073381;
        public override int BasePhysicalResistance => 25;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 10;
        public override int BasePoisonResistance => 10;
        public override int BaseEnergyResistance => 10;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }

    // ServUO: Items/Artifacts/Equipment/Glasses/CollectionsBritLibraryGlasses.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1); WeaponAttributes.HitLowerDefend = 30 (D-22: no WeaponAttributes on ModernUO armour or clothing).
    [SerializationGenerator(0, false)]
    public partial class GargishMaceAndShieldGlasses : GargishGlasses
    {
        [Constructible]
        public GargishMaceAndShieldGlasses()
        {
            Hue = 0x1DD;
            Attributes.BonusStr = 10;
            Attributes.BonusDex = 5;
        }

        public override int LabelNumber => 1073381;
        public override int BasePhysicalResistance => 25;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 10;
        public override int BasePoisonResistance => 10;
        public override int BaseEnergyResistance => 10;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }

    // ServUO: Items/Artifacts/Equipment/Glasses/CollectionsBritLibraryGlasses.cs (CC9 batch 5).
    // Dropped: [Alterable] (D-20: pinned ModernUO has no alter-item system, zero consumers of the name).
    [SerializationGenerator(0, false)]
    public partial class GlassesOfTheArts : Glasses
    {
        [Constructible]
        public GlassesOfTheArts()
        {
            Hue = 0x73;
            Attributes.BonusInt = 5;
            Attributes.BonusStr = 5;
            Attributes.BonusHits = 15;
        }

        public override int LabelNumber => 1073363;
        public override int BasePhysicalResistance => 10;
        public override int BaseFireResistance => 8;
        public override int BaseColdResistance => 8;
        public override int BasePoisonResistance => 4;
        public override int BaseEnergyResistance => 10;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }

    // ServUO: Items/Artifacts/Equipment/Glasses/CollectionsBritLibraryGlasses.cs (CC9 batch 5).
    [SerializationGenerator(0, false)]
    public partial class GargishGlassesOfTheArts : GargishGlasses
    {
        [Constructible]
        public GargishGlassesOfTheArts()
        {
            Hue = 0x73;
            Attributes.BonusInt = 5;
            Attributes.BonusStr = 5;
            Attributes.BonusHits = 15;
        }

        public override int LabelNumber => 1073363;
        public override int BasePhysicalResistance => 10;
        public override int BaseFireResistance => 8;
        public override int BaseColdResistance => 8;
        public override int BasePoisonResistance => 4;
        public override int BaseEnergyResistance => 10;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }

    // ServUO: Items/Artifacts/Equipment/Glasses/CollectionsBritLibraryGlasses.cs (CC9 batch 5).
    // Dropped: [Alterable] (D-20: pinned ModernUO has no alter-item system, zero consumers of the name).
    [SerializationGenerator(0, false)]
    public partial class TradesGlasses : Glasses
    {
        [Constructible]
        public TradesGlasses()
        {
            Attributes.BonusStr = 10;
            Attributes.BonusInt = 10;
        }

        public override int LabelNumber => 1073362;
        public override int BasePhysicalResistance => 10;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 10;
        public override int BasePoisonResistance => 10;
        public override int BaseEnergyResistance => 10;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }

    // ServUO: Items/Artifacts/Equipment/Glasses/CollectionsBritLibraryGlasses.cs (CC9 batch 5).
    [SerializationGenerator(0, false)]
    public partial class GargishTradesGlasses : GargishGlasses
    {
        [Constructible]
        public GargishTradesGlasses()
        {
            Attributes.BonusStr = 10;
            Attributes.BonusInt = 10;
        }

        public override int LabelNumber => 1073362;
        public override int BasePhysicalResistance => 10;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 10;
        public override int BasePoisonResistance => 10;
        public override int BaseEnergyResistance => 10;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }

    // ServUO: Items/Artifacts/Equipment/Glasses/CollectionsBritLibraryGlasses.cs (CC9 batch 5).
    // Dropped: [Alterable] (D-20: pinned ModernUO has no alter-item system, zero consumers of the name).
    [SerializationGenerator(0, false)]
    public partial class WizardsCrystalGlasses : Glasses
    {
        [Constructible]
        public WizardsCrystalGlasses()
        {
            Hue = 0x2B0;
            Attributes.BonusMana = 10;
            Attributes.RegenMana = 3;
            Attributes.SpellDamage = 15;
        }

        public override int LabelNumber => 1073374;
        public override int BasePhysicalResistance => 5;
        public override int BaseFireResistance => 5;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 5;
        public override int BaseEnergyResistance => 5;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }

    // ServUO: Items/Artifacts/Equipment/Glasses/CollectionsBritLibraryGlasses.cs (CC9 batch 5).
    [SerializationGenerator(0, false)]
    public partial class GargishWizardsCrystalGlasses : GargishGlasses
    {
        [Constructible]
        public GargishWizardsCrystalGlasses()
        {
            Hue = 0x2B0;
            Attributes.BonusMana = 10;
            Attributes.RegenMana = 3;
            Attributes.SpellDamage = 15;
        }

        public override int LabelNumber => 1073374;
        public override int BasePhysicalResistance => 5;
        public override int BaseFireResistance => 5;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 5;
        public override int BaseEnergyResistance => 5;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }

    // ServUO: Items/Artifacts/Equipment/Glasses/CollectionsBritLibraryGlasses.cs (CC9 batch 5).
    // Dropped: [Alterable] (D-20: pinned ModernUO has no alter-item system, zero consumers of the name).
    [SerializationGenerator(0, false)]
    public partial class TreasuresAndTrinketsGlasses : Glasses
    {
        [Constructible]
        public TreasuresAndTrinketsGlasses()
        {
            Hue = 0x5A6;
            Attributes.BonusInt = 10;
            Attributes.BonusHits = 5;
            Attributes.SpellDamage = 10;
        }

        public override int LabelNumber => 1073373;
        public override int BasePhysicalResistance => 10;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 10;
        public override int BasePoisonResistance => 10;
        public override int BaseEnergyResistance => 10;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }

    // ServUO: Items/Artifacts/Equipment/Glasses/CollectionsBritLibraryGlasses.cs (CC9 batch 5).
    [SerializationGenerator(0, false)]
    public partial class GargishTreasuresAndTrinketsGlasses : GargishGlasses
    {
        [Constructible]
        public GargishTreasuresAndTrinketsGlasses()
        {
            Hue = 0x5A6;
            Attributes.BonusInt = 10;
            Attributes.BonusHits = 5;
            Attributes.SpellDamage = 10;
        }

        public override int LabelNumber => 1073373;
        public override int BasePhysicalResistance => 10;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 10;
        public override int BasePoisonResistance => 10;
        public override int BaseEnergyResistance => 10;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
