using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/AnimatedLegsoftheInsaneTinker.cs (CC9).
    [SerializationGenerator(0, false)]
    public partial class AnimatedLegsoftheInsaneTinker : PlateLegs
    {
        [Constructible]
        public AnimatedLegsoftheInsaneTinker()
        {
            Hue = 2310;
            Attributes.BonusDex = 5;
            Attributes.RegenStam = 2;
            Attributes.WeaponDamage = 10;
            Attributes.WeaponSpeed = 10;
            ArmorAttributes.LowerStatReq = 50;
        }

        public override int LabelNumber => 1113760; // Animated Legs of the Insane Tinker

        public override int BasePhysicalResistance => 17;
        public override int BaseFireResistance => 15;
        public override int BaseColdResistance => 7;
        public override int BasePoisonResistance => 15;
        public override int BaseEnergyResistance => 2;

        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
