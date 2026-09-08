using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/StoneWarSword.cs (CC9 batch 3). Based off a VikingSword.
    [Flippable(0x900, 0x4071)]
    [SerializationGenerator(0, false)]
    public partial class StoneWarSword : BaseSword
    {
        [Constructible]
        public StoneWarSword() : base(0x900)
        {
        }

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.ArmorIgnore;
        public override WeaponAbility SecondaryAbility => WeaponAbility.ParalyzingBlow;

        public override int AosStrengthReq => 40;
        public override int AosMinDamage => 15;
        public override int AosMaxDamage => 19;
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
    }
}
