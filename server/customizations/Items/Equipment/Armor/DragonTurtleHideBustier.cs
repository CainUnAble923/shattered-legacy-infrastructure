using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/DragonTurtleHideBustier.cs (CC9 batch 3). Leather-class armour.
    // Derives from BaseSetArmor, not BaseArmor: DardensBustier (Sets/Dardens Set/DardensBustier.cs) is a Darden set
    // piece on it, and a set carrier cannot be inserted above a type once an instance exists in a save
    // (AGENTS.md, "Give a base its final parent the first time you port it"). SetID is None, so the
    // carrier is inert here. See shard-migration/notes/cc9-artifacts.md section 13.
    [SerializationGenerator(0, false)]
    public partial class DragonTurtleHideBustier : BaseSetArmor
    {
        [Constructible]
        public DragonTurtleHideBustier() : base(0x782B)
        {
        }

        public override double DefaultWeight => 6.0;

        public override int LabelNumber => 1109635; // Dragon Turtle Hide Bustier

        public override int BasePhysicalResistance => 3;
        public override int BaseFireResistance => 3;
        public override int BaseColdResistance => 4;
        public override int BasePoisonResistance => 3;
        public override int BaseEnergyResistance => 2;

        public override int InitMinHits => 35;
        public override int InitMaxHits => 45;

        public override int AosStrReq => 30;
        public override int OldStrReq => 35;

        public override int ArmorBase => 15;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;

        // ServUO: "We like to cross dress here!" The default is already true on both emulators.
        public override bool AllowMaleWearer => true;
    }
}
