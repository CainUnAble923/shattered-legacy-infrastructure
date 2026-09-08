using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/Shortblade.cs (CC9 batch 3). Based off a Warfork.
    [Flippable(0x907, 0x4076)]
    [SerializationGenerator(0, false)]
    public partial class Shortblade : BaseSword
    {
        [Constructible]
        public Shortblade() : base(0x907)
        {
        }

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.ArmorIgnore;
        public override WeaponAbility SecondaryAbility => WeaponAbility.MortalStrike;

        public override int AosStrengthReq => 45;
        public override int AosMinDamage => 10;
        public override int AosMaxDamage => 13;
        public override int AosSpeed => 43;
        public override float MlSpeed => 2.25f;

        public override int OldStrengthReq => 35;
        public override int OldMinDamage => 4;
        public override int OldMaxDamage => 32;
        public override int OldSpeed => 45;

        public override int DefHitSound => 0x236;
        public override int DefMissSound => 0x238;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 110;

        public override SkillName DefSkill => SkillName.Fencing;
        public override WeaponType DefType => WeaponType.Piercing;
        public override WeaponAnimation DefAnimation => WeaponAnimation.Pierce1H;
    }
}
