using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/DragonTurtleHideChest.cs (CC9 batch 3). Leather-class armour.
    // Derives from BaseSetArmor, not BaseArmor: DardensTunic (Sets/Dardens Set/DardensTunic.cs) is a Darden set
    // piece on it, and a set carrier cannot be inserted above a type once an instance exists in a save
    // (AGENTS.md, "Give a base its final parent the first time you port it"). SetID is None, so the
    // carrier is inert here. See shard-migration/notes/cc9-artifacts.md section 13.
    [SerializationGenerator(0, false)]
    public partial class DragonTurtleHideChest : BaseSetArmor
    {
        [Constructible]
        public DragonTurtleHideChest() : base(0x782A)
        {
        }

        public override double DefaultWeight => 8.0;

        public override int LabelNumber => 1109634; // Dragon Turtle Hide Chest

        public override int BasePhysicalResistance => 2;
        public override int BaseFireResistance => 4;
        public override int BaseColdResistance => 3;
        public override int BasePoisonResistance => 3;
        public override int BaseEnergyResistance => 4;

        public override int InitMinHits => 35;
        public override int InitMaxHits => 45;

        public override int AosStrReq => 30;
        public override int OldStrReq => 35;

        public override int ArmorBase => 16;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
