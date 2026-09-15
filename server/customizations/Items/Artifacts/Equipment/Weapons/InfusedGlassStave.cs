using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/InfusedGlassStave.cs (CC9 batch 5).
    // Dropped: CanBeWornByGargoyles (D-2).
    [Flippable(0x905, 0x4070)]
    [SerializationGenerator(0, false)]
    public partial class InfusedGlassStave : BaseStaff
    {
        [Constructible]
        public InfusedGlassStave() : base(0x905)
        {
            Hue = 23;
        }

        public override double DefaultWeight => 4.0;
        public override int LabelNumber => 1112909;
        public override WeaponAbility PrimaryAbility => WeaponAbility.DoubleStrike;
        public override WeaponAbility SecondaryAbility => WeaponAbility.MortalStrike;
        public override int AosStrengthReq => 20;
        public override int AosMinDamage => 11;
        public override int AosMaxDamage => 14;
        public override int AosSpeed => 39;
        public override float MlSpeed => 2.25f;
        public override int OldStrengthReq => 35;
        public override int OldMinDamage => 8;
        public override int OldMaxDamage => 33;
        public override int OldSpeed => 35;
        public override int InitMinHits => 31;
        public override int InitMaxHits => 70;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
    }
}
