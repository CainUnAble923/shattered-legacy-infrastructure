using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/GargishDagger.cs (CC9). Based off Dagger; ServUO leaves
    // the weight commented out, so the base default stands here too.
    [Flippable(0x902, 0x406A)]
    [SerializationGenerator(0, false)]
    public partial class GargishDagger : BaseKnife
    {
        [Constructible]
        public GargishDagger() : base(0x902)
        {
        }

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.ShadowStrike;
        public override WeaponAbility SecondaryAbility => WeaponAbility.InfectiousStrike;

        public override int AosStrengthReq => 10;
        public override int AosMinDamage => 10;
        public override int AosMaxDamage => 12;
        public override int AosSpeed => 56;
        public override float MlSpeed => 2.00f;

        public override int OldStrengthReq => 1;
        public override int OldMinDamage => 3;
        public override int OldMaxDamage => 15;
        public override int OldSpeed => 55;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 40;

        public override SkillName DefSkill => SkillName.Fencing;
        public override WeaponType DefType => WeaponType.Piercing;
        public override WeaponAnimation DefAnimation => WeaponAnimation.Pierce1H;
    }
}
