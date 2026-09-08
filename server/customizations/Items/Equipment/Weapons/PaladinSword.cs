using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/PaladinSword.cs (CC9 batch 3).
    [Flippable(0x26CE, 0x26CF)]
    [SerializationGenerator(0, false)]
    public partial class PaladinSword : BaseSword
    {
        [Constructible]
        public PaladinSword() : base(0x26CE)
        {
        }

        public override double DefaultWeight => 6.0;

        public override WeaponAbility PrimaryAbility => WeaponAbility.WhirlwindAttack;
        public override WeaponAbility SecondaryAbility => WeaponAbility.Disarm;

        public override int AosStrengthReq => 85;
        public override int AosMinDamage => 20;
        public override int AosMaxDamage => 24;
        public override float MlSpeed => 5.0f;

        public override int InitMinHits => 36;
        public override int InitMaxHits => 48;
    }
}
