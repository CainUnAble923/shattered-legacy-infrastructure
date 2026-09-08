using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/StaffOfResonance.cs (CC9 batch 4).
    // ServUO derives from GlassStaff and gets absorption state from BaseWeapon. Here that state lives on
    // BaseSetWeapon (S10), so the piece derives from that and reproduces stock GlassStaff's own members.
    // GlassStaff is a BaseStaff, and BaseSetWeapon sits on BaseMeleeWeapon, so BaseStaff's members and its
    // OnHit are reproduced here too (pinned ModernUO, statement for statement).
    // [Flippable] is copied from GlassStaff: ModernUO reads it with inherit: false.
    // The random resonance is rolled once in the constructor, exactly as ServUO does.
    // Dropped: IsArtifact (D-1).
    [Flippable(0x905, 0x4070)]
    [SerializationGenerator(0, false)]
    public partial class StaffOfResonance : BaseSetWeapon
    {
        [Constructible]
        public StaffOfResonance() : base(0x905)
        {
            Resource = CraftResource.None;

            switch (Utility.Random(5)) // Random resonance property
            {
                case 0: AbsorptionAttributes.ResonanceFire = 20; break;
                case 1: AbsorptionAttributes.ResonanceCold = 20; break;
                case 2: AbsorptionAttributes.ResonancePoison = 20; break;
                case 3: AbsorptionAttributes.ResonanceEnergy = 20; break;
                case 4: AbsorptionAttributes.ResonanceKinetic = 20; break;
            }

            Attributes.SpellChanneling = 1;
            WeaponAttributes.MageWeapon = 10;
            WeaponAttributes.HitHarm = 50;
            Attributes.DefendChance = 10;
            Attributes.WeaponSpeed = 20;
            Attributes.WeaponDamage = -40;
            Attributes.LowerManaCost = 5;
            AosElementDamages.Poison = 100;
            Hue = 1451; // Hue not exact (ServUO's own comment)
        }

        public override int LabelNumber => 1113527; // Staff of Resonance
        public override int ArtifactRarity => 5;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock GlassStaff members, reproduced because the parent changed.
        public override double DefaultWeight => 4.0;
        public override WeaponAbility PrimaryAbility => WeaponAbility.DoubleStrike;
        public override WeaponAbility SecondaryAbility => WeaponAbility.MortalStrike;
        public override int AosStrengthReq => 20;
        public override int AosMinDamage => 11;
        public override int AosMaxDamage => 14;
        public override float MlSpeed => 2.25f;
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
