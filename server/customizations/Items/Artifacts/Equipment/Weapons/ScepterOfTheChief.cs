using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/ScepterOfTheChief.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class ScepterOfTheChief : Scepter
    {
        [Constructible]
        public ScepterOfTheChief()
        {
            Hue = 0x481;
            Slayer = SlayerName.Exorcism;
            Attributes.RegenHits = 2;
            Attributes.ReflectPhysical = 15;
            Attributes.WeaponDamage = 45;
            WeaponAttributes.HitDispel = 100;
            WeaponAttributes.HitLeechMana = 100;
        }

        public override int LabelNumber => 1072080;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        public override void GetDamageTypes(
            Mobile wielder, out int phys, out int fire, out int cold, out int pois, out int nrgy, out int chaos,
            out int direct
        )
        {
            phys = fire = cold = nrgy = chaos = direct = 0;
            pois = 100;
        }
    }
}
