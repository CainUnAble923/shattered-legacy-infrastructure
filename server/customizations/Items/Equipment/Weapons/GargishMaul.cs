using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/GargishMaul.cs (CC9 batch 3).
    [Flippable(0x48C2, 0x48C3)]
    [SerializationGenerator(0, false)]
    public partial class GargishMaul : BaseBashing
    {
        [Constructible]
        public GargishMaul() : base(0x48C2)
        {
        }

        public override double DefaultWeight => 10.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.DoubleStrike;
        public override WeaponAbility SecondaryAbility => WeaponAbility.ConcussionBlow;

        public override int AosStrengthReq => 45;
        public override int AosMinDamage => 14;
        public override int AosMaxDamage => 18;
        public override int AosSpeed => 32;
        public override float MlSpeed => 3.50f;

        public override int OldStrengthReq => 20;
        public override int OldMinDamage => 10;
        public override int OldMaxDamage => 30;
        public override int OldSpeed => 30;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 70;
    }
}
