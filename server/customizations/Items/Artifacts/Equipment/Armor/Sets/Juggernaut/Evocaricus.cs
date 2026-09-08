using ModernUO.Serialization;
using Server.Targets;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Juggernaut/Evocaricus.cs (CC9 batch 4).
    // ServUO derives from VikingSword and gets set/absorption state from BaseWeapon. Here that state lives on
    // BaseSetWeapon (S10), so the piece derives from that and reproduces stock VikingSword's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // [Flippable] is copied from VikingSword: ModernUO reads it with inherit: false.
    // VikingSword is a BaseSword, and BaseSetWeapon sits on BaseMeleeWeapon, so BaseSword's members and its
    // OnDoubleClick/OnHit are reproduced here too (pinned ModernUO, statement for statement).
    // Dropped: IsArtifact (D-1).
    [Flippable(0x13B9, 0x13Ba)]
    [SerializationGenerator(0, false)]
    public partial class Evocaricus : BaseSetWeapon
    {
        [Constructible]
        public Evocaricus() : base(0x13B9)
        {
            SetHue = 0x76D;
            Attributes.WeaponDamage = 50;
            SetSelfRepair = 3;
            SetAttributes.DefendChance = 10;
            SetAttributes.BonusStr = 10;
            SetAttributes.WeaponSpeed = 35;
        }

        public override int LabelNumber => 1074309;
        public override SetItem SetID => SetItem.Juggernaut;
        public override int Pieces => 2;

        // Stock VikingSword members, reproduced because the parent changed.
        public override double DefaultWeight => 6.0;
        public override WeaponAbility PrimaryAbility => WeaponAbility.CrushingBlow;
        public override WeaponAbility SecondaryAbility => WeaponAbility.ParalyzingBlow;
        public override int AosStrengthReq => 40;
        public override int AosMinDamage => 15;
        public override int AosMaxDamage => 17;
        public override int AosSpeed => 28;
        public override float MlSpeed => 3.75f;
        public override int OldStrengthReq => 40;
        public override int OldMinDamage => 6;
        public override int OldMaxDamage => 34;
        public override int OldSpeed => 30;
        public override int DefHitSound => 0x237;
        public override int DefMissSound => 0x23A;
        public override int InitMinHits => 31;
        public override int InitMaxHits => 100;

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
