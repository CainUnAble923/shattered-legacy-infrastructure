using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/TongueoftheBeast.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [TypeAlias("Server.Items.TongueoftheBeast")]
    [SerializationGenerator(0, false)]
    public partial class TongueOfTheBeast : WoodenKiteShield
    {
        [Constructible]
        public TongueOfTheBeast()
        {
            Hue = 153;
            Attributes.SpellChanneling = 1;
            Attributes.RegenStam = 3;
            Attributes.RegenMana = 3;
        }

        public override int LabelNumber => 1112405;
        public override int BasePhysicalResistance => 10;
        public override int BaseEnergyResistance => 5;
        public override int InitMinHits => 150;
        public override int InitMaxHits => 150;
        public override bool CanFortify => false;
    }
}
