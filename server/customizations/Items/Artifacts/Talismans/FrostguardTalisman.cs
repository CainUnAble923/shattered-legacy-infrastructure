using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Talismans/FrostguardTalisman.cs (CC9 batch 4). Stock BaseTalisman parent.
    // Dropped: SAAbsorptionAttributes.EaterCold = 5 (D-15). ServUO stores an absorption block on BaseTalisman but
    // never reads it: RunicReforging.GetSAAbsorptionAttributes, the only aggregation path, returns it for
    // BaseArmor, BaseJewel, BaseWeapon and BaseClothing and null for everything else, so a talisman's eater
    // does nothing on ServUO either. Only the tooltip line is lost. No talisman carrier was built (S10) and
    // this is the reason not to build one. Also dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class FrostguardTalisman : BaseTalisman
    {
        [Constructible]
        public FrostguardTalisman() : base(0x2F5B)
        {
            Hue = 0x556;
            Attributes.RegenMana = 1;
            Attributes.LowerManaCost = 5;
        }

        public override int LabelNumber => 1115516; // Frostguard Talisman
        public override double DefaultWeight => 1.0;
        public override int ColdResistance => 3;
    }
}
