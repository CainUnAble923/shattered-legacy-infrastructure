using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/CloakOfDeath.cs (CC9).
    [Flippable(0x2FB9, 0x3173)]
    [SerializationGenerator(0, false)]
    public partial class CloakOfDeath : BaseOuterTorso
    {
        [Constructible]
        public CloakOfDeath() : base(0x2FB9)
        {
            Hue = 0x966;
            Attributes.DefendChance = 3;
            Attributes.AttackChance = 3;
            Attributes.SpellDamage = 3;
        }

        public override double DefaultWeight => 2.0;

        public override int LabelNumber => 1112881; // Cloak of Death
    }
}
