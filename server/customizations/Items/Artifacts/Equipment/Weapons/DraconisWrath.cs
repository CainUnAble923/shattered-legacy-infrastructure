using ModernUO.Serialization;
using Server.Targets;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/DraconisWrath.cs (CC9 batch 4).
    // ServUO derives from Katana and gets set/absorption state from BaseWeapon. Here that state lives on
    // BaseSetWeapon (S10), so the piece derives from that and reproduces stock Katana's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from Katana: ModernUO reads it with inherit: false.
    // Katana is a BaseSword, and BaseSetWeapon sits on BaseMeleeWeapon, so BaseSword's members and its
    // OnDoubleClick/OnHit are reproduced here too (pinned ModernUO, statement for statement).
    // Dropped: IsArtifact (D-1).
    [Flippable(0x13FF, 0x13FE)]
    [SerializationGenerator(0, false)]
    public partial class DraconisWrath : BaseSetWeapon
    {
        [Constructible]
        public DraconisWrath() : base(0x13FF)
        {
            Hue = 1177;
            AbsorptionAttributes.EaterFire = 20;
            WeaponAttributes.HitFireball = 60;
            Attributes.AttackChance = 15;
            Attributes.WeaponDamage = 50;
            WeaponAttributes.UseBestSkill = 1;
        }

        public override int LabelNumber => 1114789;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock Katana members, reproduced because the parent changed.
        public override double DefaultWeight => 6.0;
        public override WeaponAbility PrimaryAbility => WeaponAbility.DoubleStrike;
        public override WeaponAbility SecondaryAbility => WeaponAbility.ArmorIgnore;
        public override int AosStrengthReq => 25;
        public override int AosMinDamage => 11;
        public override int AosMaxDamage => 13;
        public override int AosSpeed => 46;
        public override float MlSpeed => 2.50f;
        public override int OldStrengthReq => 10;
        public override int OldMinDamage => 5;
        public override int OldMaxDamage => 26;
        public override int OldSpeed => 58;
        public override int DefHitSound => 0x23B;
        public override int DefMissSound => 0x23A;

        // Stock BaseSword members, reproduced because BaseSetWeapon derives from BaseMeleeWeapon directly.
        public override SkillName DefSkill => SkillName.Swords;
        public override WeaponType DefType => WeaponType.Slashing;
        public override WeaponAnimation DefAnimation => WeaponAnimation.Slash1H;

        public override void OnDoubleClick(Mobile from)
        {
            from.SendLocalizedMessage(1010018); // What do you want to use this item on?
            from.Target = new BladedItemTarget(this);
        }

        public override void OnHit(Mobile attacker, Mobile defender, double damageBonus = 1)
        {
            base.OnHit(attacker, defender, damageBonus);

            if (!Core.AOS && Poison != null && PoisonCharges > 0)
            {
                --PoisonCharges;

                if (Utility.RandomBool()) // 50% chance to poison
                {
                    defender.ApplyPoison(attacker, Poison);
                }
            }
        }
    }
}
