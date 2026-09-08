using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Glasses/GargishGlasses.cs (CC9).
    // ServUO's IRepairable/RepairSystem is dropped: ModernUO's Repair dispatches on BaseArmor
    // directly (Repair.cs), so tinkers can already repair this. The version-0 deserialize
    // helper is a save-upgrade path new content never runs.
    [Flippable(0x4644, 0x4645)]
    [SerializationGenerator(0, false)]
    public partial class GargishGlasses : BaseArmor
    {
        [Constructible]
        public GargishGlasses() : base(0x4644) => Layer = Layer.Earrings;

        public override double DefaultWeight => 2.0;

        public override int LabelNumber => 1096713; // Gargish Glasses

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override int BasePhysicalResistance => 2;
        public override int BaseFireResistance => 4;
        public override int BaseColdResistance => 4;
        public override int BasePoisonResistance => 3;
        public override int BaseEnergyResistance => 2;

        public override int InitMinHits => 36;
        public override int InitMaxHits => 48;

        public override int AosStrReq => 45;
        public override int OldStrReq => 40;

        public override int ArmorBase => 30;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
