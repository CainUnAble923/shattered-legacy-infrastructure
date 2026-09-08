using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/DragonTurtleHideHelm.cs (CC9 batch 3). Leather-class armour.
    // Derives from BaseSetArmor, not BaseArmor: DardensHelm (Sets/Dardens Set/DardensHelm.cs) is a Darden set
    // piece on it, and a set carrier cannot be inserted above a type once an instance exists in a save
    // (AGENTS.md, "Give a base its final parent the first time you port it"). SetID is None, so the
    // carrier is inert here. See shard-migration/notes/cc9-artifacts.md section 13.
    [SerializationGenerator(0, false)]
    public partial class DragonTurtleHideHelm : BaseSetArmor
    {
        [Constructible]
        public DragonTurtleHideHelm() : base(0x782D)
        {
        }

        public override double DefaultWeight => 2.0;

        public override int LabelNumber => 1109637; // Dragon Turtle Hide Helm

        public override int BasePhysicalResistance => 1;
        public override int BaseFireResistance => 5;
        public override int BaseColdResistance => 2;
        public override int BasePoisonResistance => 2;
        public override int BaseEnergyResistance => 5;

        public override int InitMinHits => 20;
        public override int InitMaxHits => 35;

        public override int AosStrReq => 30;
        public override int OldStrReq => 10;

        public override int ArmorBase => 30;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;
    }
}
