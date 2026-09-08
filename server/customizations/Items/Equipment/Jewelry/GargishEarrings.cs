using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Jewelry/GargishEarrings.cs (CC9). Armour-class jewellery.
    //
    // ServUO overrides GetDurabilityBonus (exceptional +20 plus ArmorAttributes.DurabilityBonus,
    // no resource term) and ApplyResourceResistances (empty) to keep the crafting resource from
    // contributing to the earrings. Neither is virtual in ModernUO. Both are inert here anyway:
    // Chainmail defaults the resource to Iron, and Iron registers CraftAttributeInfo.Blank
    // (ResourceInfo.cs), so the resource terms ModernUO adds are zero.
    [SerializationGenerator(0, false)]
    public partial class GargishEarrings : BaseArmor
    {
        [Constructible]
        public GargishEarrings() : base(0x4213) => Layer = Layer.Earrings;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Chainmail;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;

        public override int BasePhysicalResistance => 1;
        public override int BaseFireResistance => 2;
        public override int BaseColdResistance => 2;
        public override int BasePoisonResistance => 2;
        public override int BaseEnergyResistance => 3;

        public override int InitMinHits => 30;
        public override int InitMaxHits => 40;
    }
}
