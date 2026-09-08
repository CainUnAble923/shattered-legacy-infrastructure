using ModernUO.Serialization;
using Server.Targets;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/GargishDagger.cs (CC9). Based off Dagger; ServUO leaves
    // the weight commented out, so the base default stands here too.
    //
    // CC9 batch 4 (2026-09-08, Q-040): derives from BaseSetWeapon, not BaseKnife. StoneDragonsTooth
    // (Items/Artifacts/Equipment/Weapons/StoneDragonsTooth.cs:21) assigns AbsorptionAttributes.EaterPoison,
    // and a carrier cannot be inserted above a type once an instance exists in a save. Re-verified before
    // the change that no save on this machine holds a GargishDagger (notes/cc9-artifacts.md section 14).
    // SetID is None, so the carrier is inert here. BaseKnife's own members are reproduced below because
    // BaseSetWeapon sits on BaseMeleeWeapon and skips BaseKnife.
    [Flippable(0x902, 0x406A)]
    [SerializationGenerator(0, false)]
    public partial class GargishDagger : BaseSetWeapon
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

        // Stock BaseKnife members, reproduced because BaseSetWeapon derives from BaseMeleeWeapon directly
        // (pinned ModernUO Items/Weapons/Knives/BaseKnife.cs, statement for statement).
        public override int DefHitSound => 0x23B;
        public override int DefMissSound => 0x238;

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
