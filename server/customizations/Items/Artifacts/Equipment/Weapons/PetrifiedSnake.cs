using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/PetrifiedSnake.cs (CC9 batch 4).
    // ServUO derives from SerpentstoneStaff and gets set/absorption state from BaseWeapon. Here that state lives on
    // BaseSetWeapon (S10), so the piece derives from that and reproduces stock SerpentstoneStaff's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from SerpentstoneStaff: ModernUO reads it with inherit: false.
    // SerpentstoneStaff is a BaseStaff, and BaseSetWeapon sits on BaseMeleeWeapon, so BaseStaff's members and its
    // OnDoubleClick/OnHit are reproduced here too (pinned ModernUO, statement for statement).
    // Dropped: IsArtifact (D-1).
    // ServUO spells the base SerpentStoneStaff; ModernUO's is SerpentstoneStaff (already in the tree, not ported).
    [Flippable(0x906, 0x406F)]
    [SerializationGenerator(0, false)]
    public partial class PetrifiedSnake : BaseSetWeapon
    {
        [Constructible]
        public PetrifiedSnake() : base(0x906)
        {
            Resource = CraftResource.RegularWood;
            Hue = 460;
            AbsorptionAttributes.EaterPoison = 20;
            Slayer = SlayerName.ReptilianDeath;
            WeaponAttributes.HitMagicArrow = 30;
            WeaponAttributes.HitLowerDefend = 30;
            Attributes.WeaponSpeed = 30;
            Attributes.WeaponDamage = 50;
            AosElementDamages.Poison = 100;
            WeaponAttributes.ResistPoisonBonus = 10;
        }

        public override int LabelNumber => 1113528;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock SerpentstoneStaff members, reproduced because the parent changed.
        public override WeaponAbility PrimaryAbility => WeaponAbility.CrushingBlow;
        public override WeaponAbility SecondaryAbility => WeaponAbility.Dismount;
        public override int AosStrengthReq => 35;
        public override int AosMinDamage => 16;
        public override int AosMaxDamage => 19;
        public override float MlSpeed => 3.50f;
        public override int RequiredRaces => Race.AllowGargoylesOnly;

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
