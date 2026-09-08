using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/BreastplateOfTheBerserker.cs (CC9). ServUO keeps
    // this armour piece under Weapons; the path is mirrored as-is.
    [SerializationGenerator(0, false)]
    public partial class BreastplateOfTheBerserker : GargishPlateChest
    {
        [Constructible]
        public BreastplateOfTheBerserker()
        {
            Hue = 1172;
            Attributes.WeaponSpeed = 10;
            Attributes.WeaponDamage = 15;
            Attributes.LowerManaCost = 4;
            Attributes.BonusHits = 5;
            Attributes.RegenStam = 3;
        }

        public override int LabelNumber => 1113539; // Breastplate of the Berserker

        public override int BasePhysicalResistance => 18;
        public override int BaseFireResistance => 16;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 11;
        public override int BaseEnergyResistance => 5;

        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
