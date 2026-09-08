using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/GargishPlateKilt.cs (CC9 batch 3).
    // Derives from BaseSetArmor, not BaseArmor: VoidInfusedKilt (Items/Artifacts/Equipment/Armor/VoidInfusedKilt.cs)
    // assigns AbsorptionAttributes on it, and a carrier cannot be inserted above a type once an instance exists
    // in a save (AGENTS.md, "Give a base its final parent the first time you port it"). SetID is None, so the
    // carrier is inert here. See shard-migration/notes/cc9-artifacts.md section 13.
    [TypeAlias("Server.Items.MaleGargishPlateKilt")]
    [SerializationGenerator(0, false)]
    public partial class GargishPlateKilt : BaseSetArmor
    {
        [Constructible]
        public GargishPlateKilt(int hue = 0) : base(0x30C) => Hue = hue;

        public override double DefaultWeight => 5.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override int BasePhysicalResistance => 8;
        public override int BaseFireResistance => 6;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 6;
        public override int BaseEnergyResistance => 5;

        public override int InitMinHits => 50;
        public override int InitMaxHits => 65;

        public override int AosStrReq => 80;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Plate;
    }
}
