using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/GargishBardiche.cs (CC9 batch 3). Based Off Bardiche.
    [Flippable(0x48B4, 0x48B5)]
    [SerializationGenerator(0, false)]
    public partial class GargishBardiche : BasePoleArm
    {
        [Constructible]
        public GargishBardiche() : base(0x48B4)
        {
        }

        public override double DefaultWeight => 7.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.ParalyzingBlow;
        public override WeaponAbility SecondaryAbility => WeaponAbility.Dismount;

        public override int AosStrengthReq => 45;
        public override int AosMinDamage => 17;
        public override int AosMaxDamage => 20;
        public override int AosSpeed => 28;
        public override float MlSpeed => 3.75f;

        public override int OldStrengthReq => 40;
        public override int OldMinDamage => 5;
        public override int OldMaxDamage => 43;
        public override int OldSpeed => 26;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 100;
    }
}
