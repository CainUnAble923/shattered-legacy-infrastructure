using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/HumanSignOfOrder.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class HumanSignOfOrder : OrderShield
    {
        [Constructible]
        public HumanSignOfOrder()
        {
            SkillBonuses.SetValues(0, SkillName.Chivalry, 10.0);
            Attributes.AttackChance = 5;
            Attributes.DefendChance = 10;
            Attributes.CastSpeed = 1;
            Attributes.CastRecovery = 1;
        }

        public override int LabelNumber => 1113534;
        public override int BasePhysicalResistance => 1;
        public override int BaseFireResistance => 0;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 5;
        public override int BaseEnergyResistance => 0;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
