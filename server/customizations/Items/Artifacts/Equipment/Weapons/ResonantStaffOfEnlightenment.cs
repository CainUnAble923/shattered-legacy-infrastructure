using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/ResonantStaffOfEnlightenment.cs (CC9 batch 4).
    // ServUO derives from QuarterStaff and gets set/absorption state from BaseWeapon. Here that state lives on
    // BaseSetWeapon (S10), so the piece derives from that and reproduces stock QuarterStaff's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from QuarterStaff: ModernUO reads it with inherit: false.
    // QuarterStaff is a BaseStaff, and BaseSetWeapon sits on BaseMeleeWeapon, so BaseStaff's members and its
    // OnDoubleClick/OnHit are reproduced here too (pinned ModernUO, statement for statement).
    // Dropped: IsArtifact (D-1).
    [Flippable(0xE89, 0xE8a)]
    [SerializationGenerator(0, false)]
    public partial class ResonantStaffofEnlightenment : BaseSetWeapon
    {
        [Constructible]
        public ResonantStaffofEnlightenment() : base(0xE89)
        {
            Resource = CraftResource.RegularWood;
            Hue = 2401;
            WeaponAttributes.HitMagicArrow = 40;
            WeaponAttributes.MageWeapon = 20;
            Attributes.SpellChanneling = 1;
            Attributes.DefendChance = 10;
            Attributes.WeaponSpeed = 20;
            Attributes.WeaponDamage = -40;
            Attributes.LowerManaCost = 5;
            AbsorptionAttributes.ResonanceCold = 20;
            AosElementDamages.Cold = 100;
            Attributes.BonusInt = 5;
        }

        public override int LabelNumber => 1113757;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock QuarterStaff members, reproduced because the parent changed.
        public override double DefaultWeight => 4.0;
        public override WeaponAbility PrimaryAbility => WeaponAbility.DoubleStrike;
        public override WeaponAbility SecondaryAbility => WeaponAbility.ConcussionBlow;
        public override int AosStrengthReq => 30;
        public override int AosMinDamage => 11;
        public override int AosMaxDamage => 14;
        public override int AosSpeed => 48;
        public override float MlSpeed => 2.25f;
        public override int OldStrengthReq => 30;
        public override int OldMinDamage => 8;
        public override int OldMaxDamage => 28;
        public override int OldSpeed => 48;

        // Stock BaseStaff members, reproduced because BaseSetWeapon derives from BaseMeleeWeapon directly.
        public override int DefHitSound => 0x233;
        public override int DefMissSound => 0x239;
        public override SkillName DefSkill => SkillName.Macing;
        public override WeaponType DefType => WeaponType.Staff;
        public override WeaponAnimation DefAnimation => WeaponAnimation.Bash2H;

        public override void OnHit(Mobile attacker, Mobile defender, double damageBonus = 1)
        {
            base.OnHit(attacker, defender, damageBonus);

            defender.Stam -= Utility.Random(3, 3); // 3-5 points of stamina loss
        }
    }
}
