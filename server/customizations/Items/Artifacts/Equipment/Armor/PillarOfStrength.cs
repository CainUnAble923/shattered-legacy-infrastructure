using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/PillarOfStrength.cs (CC9).
    [SerializationGenerator(0, false)]
    public partial class PillarOfStrength : LargeStoneShield
    {
        [Constructible]
        public PillarOfStrength()
        {
            Attributes.BonusStr = 10;
            Attributes.BonusHits = 10;
            Attributes.WeaponDamage = 20;
        }

        public override int LabelNumber => 1113533; // Pillar of Strength

        public override int BasePhysicalResistance => 10;
        public override int BaseFireResistance => 0;
        public override int BaseColdResistance => 0;
        public override int BasePoisonResistance => 1;
        public override int BaseEnergyResistance => 0;

        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
