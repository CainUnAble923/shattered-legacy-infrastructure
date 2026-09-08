using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/TheScholarsHalo.cs (CC9).
    [SerializationGenerator(0, false)]
    public partial class TheScholarsHalo : Bandana
    {
        [Constructible]
        public TheScholarsHalo()
        {
            Attributes.BonusMana = 15;
            Attributes.RegenMana = 2;
            Attributes.SpellDamage = 15;
            Attributes.CastSpeed = 1;
            Attributes.LowerManaCost = 10;
        }

        public override int LabelNumber => 1157354; // the scholar's halo

        public override int BasePhysicalResistance => 15;
        public override int BaseFireResistance => 15;
        public override int BaseColdResistance => 15;
        public override int BasePoisonResistance => 15;
        public override int BaseEnergyResistance => 15;

        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
