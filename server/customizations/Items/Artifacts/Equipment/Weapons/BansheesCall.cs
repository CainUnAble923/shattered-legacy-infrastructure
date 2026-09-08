using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/BansheesCall.cs (CC9). Thrown; Velocity lives on
    // ModernUO's BaseRanged, which BaseThrown derives from, so it ports unchanged.
    [SerializationGenerator(0, false)]
    public partial class BansheesCall : Cyclone
    {
        [Constructible]
        public BansheesCall()
        {
            Hue = 1266;
            WeaponAttributes.HitHarm = 40;
            Attributes.BonusStr = 5;
            WeaponAttributes.HitLeechHits = 45;
            Attributes.WeaponSpeed = 30;
            Attributes.WeaponDamage = 50;
            Velocity = 35;
            AosElementDamages.Cold = 100;
        }

        public override int LabelNumber => 1113529; // Banshee's Call

        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
