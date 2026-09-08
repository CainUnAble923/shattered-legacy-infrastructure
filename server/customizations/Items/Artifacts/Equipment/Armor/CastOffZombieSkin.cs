using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/CastOffZombieSkin.cs (CC9). ServUO derives from
    // its GargishLeatherArms (0x302); ModernUO ships that item as GargishLeatherArmsType2.
    [SerializationGenerator(0, false)]
    public partial class CastOffZombieSkin : GargishLeatherArmsType2
    {
        [Constructible]
        public CastOffZombieSkin()
        {
            Hue = 1893;
            SkillBonuses.SetValues(0, SkillName.Necromancy, 5.0);
            SkillBonuses.SetValues(1, SkillName.SpiritSpeak, 5.0);
            Attributes.LowerManaCost = 5;
            Attributes.LowerRegCost = 8;
            Attributes.IncreasedKarmaLoss = 5;
        }

        public override int LabelNumber => 1113538; // Cast-off Zombie Skin

        public override int BasePhysicalResistance => 13;
        public override int BaseFireResistance => -2;
        public override int BaseColdResistance => 17;
        public override int BasePoisonResistance => 18;
        public override int BaseEnergyResistance => 6;

        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
