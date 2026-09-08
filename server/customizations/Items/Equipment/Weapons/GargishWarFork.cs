using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/GargishWarFork.cs (CC9 batch 3). Based Off War Fork.
    [Flippable(0x48BE, 0x48BF)]
    [SerializationGenerator(0, false)]
    public partial class GargishWarFork : BaseSpear
    {
        [Constructible]
        public GargishWarFork() : base(0x48BE)
        {
        }

        public override double DefaultWeight => 9.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.BleedAttack;
        public override WeaponAbility SecondaryAbility => WeaponAbility.Disarm;

        public override int AosStrengthReq => 45;
        public override int AosMinDamage => 10;
        public override int AosMaxDamage => 14;
        public override int AosSpeed => 43;
        public override float MlSpeed => 2.50f;

        public override int OldStrengthReq => 35;
        public override int OldMinDamage => 4;
        public override int OldMaxDamage => 32;
        public override int OldSpeed => 45;

        public override int DefHitSound => 0x236;
        public override int DefMissSound => 0x238;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 110;

        public override WeaponAnimation DefAnimation => WeaponAnimation.Pierce1H;
    }
}
