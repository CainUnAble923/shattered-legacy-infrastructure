using System;
using ModernUO.Serialization;
using Server.Engines.ConPVP;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/JadeWarAxe.cs (CC9 batch 4).
    // ServUO derives from WarAxe and gets set/absorption state from BaseWeapon. Here that state lives on
    // BaseSetWeapon (S10), so the piece derives from that and reproduces stock WarAxe's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from WarAxe: ModernUO reads it with inherit: false.
    // WarAxe is a BaseAxe, and BaseSetWeapon sits on BaseMeleeWeapon, so BaseAxe's members and its
    // OnDoubleClick/OnHit are reproduced here too (pinned ModernUO, statement for statement).
    // Dropped: IsArtifact (D-1).
    // BaseAxe's UsesRemaining/ShowUsesRemaining fields are not reproduced: WarAxe has HarvestSystem => null, so they could never be consumed.
    [Flippable(0x13B0, 0x13AF)]
    [SerializationGenerator(0, false)]
    public partial class JadeWarAxe : BaseSetWeapon
    {
        [Constructible]
        public JadeWarAxe() : base(0x13B0)
        {
            Hue = 1162;
            AbsorptionAttributes.EaterFire = 10;
            Slayer = SlayerName.ReptilianDeath;
            WeaponAttributes.HitFireball = 30;
            WeaponAttributes.HitLowerDefend = 60;
            Attributes.WeaponSpeed = 20;
            Attributes.WeaponDamage = 50;
        }

        public override int LabelNumber => 1115445;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // Stock WarAxe members, reproduced because the parent changed.
        public override double DefaultWeight => 8.0;
        public override WeaponAbility PrimaryAbility => WeaponAbility.ArmorIgnore;
        public override WeaponAbility SecondaryAbility => WeaponAbility.BleedAttack;
        public override int AosStrengthReq => 35;
        public override int AosMinDamage => 14;
        public override int AosMaxDamage => 15;
        public override int AosSpeed => 33;
        public override float MlSpeed => 3.25f;
        public override int OldStrengthReq => 35;
        public override int OldMinDamage => 9;
        public override int OldMaxDamage => 27;
        public override int OldSpeed => 40;
        public override int DefHitSound => 0x233;
        public override int DefMissSound => 0x239;
        public override SkillName DefSkill => SkillName.Macing;
        public override WeaponType DefType => WeaponType.Bashing;
        public override WeaponAnimation DefAnimation => WeaponAnimation.Bash1H;

        // Stock BaseAxe members, reproduced because BaseSetWeapon derives from BaseMeleeWeapon directly.

        public override void OnHit(Mobile attacker, Mobile defender, double damageBonus = 1)
        {
            base.OnHit(attacker, defender, damageBonus);

            if (!Core.AOS && Core.UOR && (attacker.Player || attacker.Body.IsHuman) && Layer == Layer.TwoHanded &&
                attacker.Skills.Anatomy.Value >= 80 &&
                attacker.Skills.Anatomy.Value / 400.0 >= Utility.RandomDouble() &&
                DuelContext.AllowSpecialAbility(attacker, "Concussion Blow", false))
            {
                var mod = defender.GetStatMod("Concussion");

                if (mod == null)
                {
                    defender.SendMessage("You receive a concussion blow!");
                    defender.AddStatMod(
                        new StatMod(StatType.Int, "Concussion", -(defender.RawInt / 2), TimeSpan.FromSeconds(30.0))
                    );

                    attacker.SendMessage("You deliver a concussion blow!");
                    attacker.PlaySound(0x308);
                }
            }
        }
    }
}
