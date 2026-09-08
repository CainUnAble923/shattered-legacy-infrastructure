using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/GargishAxe.cs (CC9 batch 3). Based off Axe.
    [Flippable(0x48B2, 0x48B3)]
    [SerializationGenerator(0, false)]
    public partial class GargishAxe : BaseAxe
    {
        [Constructible]
        public GargishAxe() : base(0x48B2)
        {
        }

        public override double DefaultWeight => 4.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.CrushingBlow;
        public override WeaponAbility SecondaryAbility => WeaponAbility.Dismount;

        public override int AosStrengthReq => 35;
        public override int AosMinDamage => 14;
        public override int AosMaxDamage => 17;
        public override int AosSpeed => 37;
        public override float MlSpeed => 3.00f;

        public override int OldStrengthReq => 35;
        public override int OldMinDamage => 6;
        public override int OldMaxDamage => 33;
        public override int OldSpeed => 37;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 110;
    }
}
