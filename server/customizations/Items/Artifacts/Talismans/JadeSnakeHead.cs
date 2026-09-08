using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Talismans/JadeSnakeHead.cs (CC9 batch 4). Stock BaseTalisman parent.
    // Dropped: SAAbsorptionAttributes.EaterPoison = 5 (D-15). ServUO stores an absorption block on BaseTalisman but
    // never reads it: RunicReforging.GetSAAbsorptionAttributes, the only aggregation path, returns it for
    // BaseArmor, BaseJewel, BaseWeapon and BaseClothing and null for everything else, so a talisman's eater
    // does nothing on ServUO either. Only the tooltip line is lost. Also dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class JadeSnakeHead : BaseTalisman
    {
        [Constructible]
        public JadeSnakeHead() : base(0x2F59)
        {
            Hue = 0x48A;
            Attributes.RegenStam = 2;
            Attributes.LowerManaCost = 5;
        }

        public override int LabelNumber => 1115647; // Jade Snake Head
        public override double DefaultWeight => 1.0;
        public override int PoisonResistance => 3;
    }
}
