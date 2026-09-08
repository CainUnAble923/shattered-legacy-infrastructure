using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/GargishKryss.cs (CC9 batch 3).
    [Flippable(0x48BC, 0x48BD)]
    [SerializationGenerator(0, false)]
    public partial class GargishKryss : BaseSword
    {
        [Constructible]
        public GargishKryss() : base(0x48BC)
        {
        }

        public override double DefaultWeight => 2.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.ArmorIgnore;
        public override WeaponAbility SecondaryAbility => WeaponAbility.InfectiousStrike;

        public override int AosStrengthReq => 10;
        public override int AosMinDamage => 10;
        public override int AosMaxDamage => 12;
        public override int AosSpeed => 53;
        public override float MlSpeed => 2.00f;

        public override int OldStrengthReq => 10;
        public override int OldMinDamage => 3;
        public override int OldMaxDamage => 28;
        public override int OldSpeed => 53;

        public override int DefHitSound => 0x23C;
        public override int DefMissSound => 0x238;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 90;

        public override SkillName DefSkill => SkillName.Fencing;
        public override WeaponType DefType => WeaponType.Piercing;
        public override WeaponAnimation DefAnimation => WeaponAnimation.Pierce1H;
    }
}
