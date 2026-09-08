using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/DragonTurtleHideLegs.cs (CC9 batch 3). Leather-class armour.
    // Derives from BaseSetArmor, not BaseArmor: DardensLegs (Sets/Dardens Set/DardensLegs.cs) is a Darden set
    // piece on it, and a set carrier cannot be inserted above a type once an instance exists in a save
    // (AGENTS.md, "Give a base its final parent the first time you port it"). SetID is None, so the
    // carrier is inert here. See shard-migration/notes/cc9-artifacts.md section 13.
    [SerializationGenerator(0, false)]
    public partial class DragonTurtleHideLegs : BaseSetArmor
    {
        [Constructible]
        public DragonTurtleHideLegs() : base(0x782C)
        {
        }

        public override double DefaultWeight => 5.0;

        public override int LabelNumber => 1109636; // Dragon Turtle Hide Leggings

        public override int BasePhysicalResistance => 3;
        public override int BaseFireResistance => 3;
        public override int BaseColdResistance => 4;
        public override int BasePoisonResistance => 3;
        public override int BaseEnergyResistance => 2;

        public override int InitMinHits => 35;
        public override int InitMaxHits => 45;

        public override int AosStrReq => 30;
        public override int OldStrReq => 25;

        public override int ArmorBase => 15;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
