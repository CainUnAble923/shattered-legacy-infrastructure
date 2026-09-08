using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/GargishKatana.cs (CC9). Based off Katana.
    [Flippable(0x48BA, 0x48BB)]
    [SerializationGenerator(0, false)]
    public partial class GargishKatana : BaseSword
    {
        [Constructible]
        public GargishKatana() : base(0x48BA)
        {
        }

        public override double DefaultWeight => 6.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.DoubleStrike;
        public override WeaponAbility SecondaryAbility => WeaponAbility.ArmorIgnore;

        public override int AosStrengthReq => 25;
        public override int AosMinDamage => 10;
        public override int AosMaxDamage => 14;
        public override int AosSpeed => 46;
        public override float MlSpeed => 2.50f;

        public override int OldStrengthReq => 10;
        public override int OldMinDamage => 5;
        public override int OldMaxDamage => 26;
        public override int OldSpeed => 58;

        public override int DefHitSound => 0x23B;
        public override int DefMissSound => 0x23A;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 90;
    }
}
