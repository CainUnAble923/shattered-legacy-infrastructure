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
    // S10 (2026-09-08): derives from BaseSetArmor, not BaseArmor. ServUO carries set and absorption
    // state on every BaseArmor, so for a ported armour-class base the carrier is the faithful parent;
    // with SetID None it is inert. Decided before any instance reached a live world, because the
    // parent is part of the save layout and cannot change afterwards without a wipe of the type.
    // See shard-migration/notes/s10-carriers.md section 4.
    [SerializationGenerator(0, false)]
    public partial class GargishEarrings : BaseSetArmor
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
