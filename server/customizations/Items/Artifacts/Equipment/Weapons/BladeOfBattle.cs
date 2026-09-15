using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/BladeOfBattle.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1); WeaponAttributes.BattleLust = 1 (D-3).
    [SerializationGenerator(0, false)]
    public partial class BladeOfBattle : Shortblade
    {
        [Constructible]
        public BladeOfBattle()
        {
            Hue = 2045;
            WeaponAttributes.HitLowerDefend = 40;
            Attributes.AttackChance = 15;
            Attributes.DefendChance = 10;
            Attributes.WeaponSpeed = 25;
            Attributes.WeaponDamage = 50;
        }

        public override int LabelNumber => 1113525;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
