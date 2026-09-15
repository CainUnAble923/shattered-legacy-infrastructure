using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/FNPitchfork.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [Flippable(0xE87, 0xE88)]
    [SerializationGenerator(0, false)]
    public partial class FNPitchfork : BaseSpear
    {
        [Constructible]
        public FNPitchfork() : base(0xE87)
        {
        }

        public override double DefaultWeight => 11.0;
        public override int LabelNumber => 1113498;
        public override WeaponAbility PrimaryAbility => WeaponAbility.BleedAttack;
        public override WeaponAbility SecondaryAbility => WeaponAbility.Dismount;
        public override int AosStrengthReq => 50;
        public override int AosMinDamage => 13;
        public override int AosMaxDamage => 14;
        public override int AosSpeed => 43;
        public override float MlSpeed => 2.50f;
        public override int OldStrengthReq => 15;
        public override int OldMinDamage => 4;
        public override int OldMaxDamage => 16;
        public override int OldSpeed => 45;
        public override int InitMinHits => 31;
        public override int InitMaxHits => 60;
    }
}
