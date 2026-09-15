using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/StormCaller.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1); WeaponAttributes.BattleLust = 1 (D-3).
    [SerializationGenerator(0, false)]
    public partial class StormCaller : Boomerang
    {
        [Constructible]
        public StormCaller()
        {
            Hue = 456;
            Attributes.BonusStr = 5;
            WeaponAttributes.HitLightning = 40;
            WeaponAttributes.HitLowerDefend = 30;
            Attributes.WeaponSpeed = 30;
            Attributes.WeaponDamage = 40;
            AosElementDamages.Physical = 20;
            AosElementDamages.Fire = 20;
            AosElementDamages.Cold = 20;
            AosElementDamages.Poison = 20;
            AosElementDamages.Energy = 20;
        }

        public override int LabelNumber => 1113530;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
