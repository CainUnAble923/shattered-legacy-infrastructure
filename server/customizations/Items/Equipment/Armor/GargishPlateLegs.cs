using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/GargishPlateLegs.cs (CC9).
    [TypeAlias("Server.Items.MaleGargishPlateLegs")]
    // S10 (2026-09-08): derives from BaseSetArmor, not BaseArmor. ServUO carries set and absorption
    // state on every BaseArmor, so for a ported armour-class base the carrier is the faithful parent;
    // with SetID None it is inert. Decided before any instance reached a live world, because the
    // parent is part of the save layout and cannot change afterwards without a wipe of the type.
    // See shard-migration/notes/s10-carriers.md section 4.
    [SerializationGenerator(0, false)]
    public partial class GargishPlateLegs : BaseSetArmor
    {
        [Constructible]
        public GargishPlateLegs(int hue = 0) : base(0x30E) => Hue = hue;

        public override double DefaultWeight => 7.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override int BasePhysicalResistance => 8;
        public override int BaseFireResistance => 6;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 6;
        public override int BaseEnergyResistance => 5;

        public override int InitMinHits => 50;
        public override int InitMaxHits => 65;

        public override int AosStrReq => 90;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }
}
