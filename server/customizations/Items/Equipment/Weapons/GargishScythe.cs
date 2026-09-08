using ModernUO.Serialization;
using Server.Engines.Harvest;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/GargishScythe.cs (CC9 batch 3).
    [Flippable(0x48C4, 0x48C5)]
    [SerializationGenerator(0, false)]
    public partial class GargishScythe : BasePoleArm
    {
        [Constructible]
        public GargishScythe() : base(0x48C4)
        {
        }

        public override double DefaultWeight => 5.0;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override WeaponAbility PrimaryAbility => WeaponAbility.BleedAttack;
        public override WeaponAbility SecondaryAbility => WeaponAbility.ParalyzingBlow;

        public override int AosStrengthReq => 45;
        public override int AosMinDamage => 16;
        public override int AosMaxDamage => 19;
        public override int AosSpeed => 32;
        public override float MlSpeed => 3.50f;

        public override int OldStrengthReq => 45;
        public override int OldMinDamage => 15;
        public override int OldMaxDamage => 18;
        public override int OldSpeed => 32;

        public override int InitMinHits => 31;
        public override int InitMaxHits => 100;

        public override HarvestSystem HarvestSystem => null;
    }
}
