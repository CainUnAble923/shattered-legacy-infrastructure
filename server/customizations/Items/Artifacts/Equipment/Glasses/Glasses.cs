using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Glasses/Glasses.cs (CC9). The plain glasses artifacts
    // derive from this. ServUO's [Alterable(DefTinkering -> GargishGlasses)] is dropped: ModernUO
    // has no AlterItem system (S9 §4, deletable). IRepairable dropped as for GargishGlasses.
    [SerializationGenerator(0, false)]
    public partial class Glasses : BaseArmor
    {
        [Constructible]
        public Glasses() : base(0x2FB8)
        {
        }

        public override double DefaultWeight => 2.0;

        public override int AosStrReq => 45;
        public override int OldStrReq => 40;

        public override int ArmorBase => 30;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Leather;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;

        public override bool CanEquip(Mobile m)
        {
            if (m.NetState?.SupportsExpansion(Expansion.ML) == false)
            {
                m.SendLocalizedMessage(1072791); // You must upgrade to Mondain's Legacy in order to use that item.
                return false;
            }

            return true;
        }
    }
}
