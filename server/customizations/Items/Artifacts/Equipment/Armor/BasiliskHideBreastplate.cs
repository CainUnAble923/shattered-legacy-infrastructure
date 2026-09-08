using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/BasiliskHideBreastplate.cs (CC9 batch 4).
    // ServUO derives from DragonChest and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock DragonChest's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from DragonChest: ModernUO reads it with inherit: false.
    // Dropped: IsArtifact (D-1).
    // ServUO serializes this at version 1 to force Resource=None/Hue on old instances; no old instances here, so version 0.
    [Flippable(0x2641, 0x2642)]
    [SerializationGenerator(0, false)]
    public partial class BasiliskHideBreastplate : BaseSetArmor
    {
        [Constructible]
        public BasiliskHideBreastplate() : base(0x2641)
        {
            Resource = CraftResource.None;
            Hue = 1366;
            AbsorptionAttributes.EaterDamage = 10;
            Attributes.BonusDex = 5;
            Attributes.RegenHits = 2;
            Attributes.RegenStam = 2;
            Attributes.RegenMana = 1;
            Attributes.DefendChance = 5;
            Attributes.LowerManaCost = 5;
        }

        public override int LabelNumber => 1115444;
        public override int BasePhysicalResistance => 12;
        public override int BaseFireResistance => 14;
        public override int BaseColdResistance => 6;
        public override int BasePoisonResistance => 11;
        public override int BaseEnergyResistance => 5;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock DragonChest members, reproduced because the parent changed.
        public override double DefaultWeight => 10.0;
        public override int AosStrReq => 75;
        public override int OldStrReq => 60;
        public override int OldDexBonus => -8;
        public override int ArmorBase => 40;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Dragon;
        public override CraftResource DefaultResource => CraftResource.RedScales;
    }
}
