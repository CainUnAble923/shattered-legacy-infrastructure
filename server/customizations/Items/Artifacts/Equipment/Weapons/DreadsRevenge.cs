using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/DreadsRevenge.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class DreadsRevenge : Kryss
    {
        [Constructible]
        public DreadsRevenge()
        {
            Hue = 0x3A;
            SkillBonuses.SetValues(0, SkillName.Fencing, 20.0);
            WeaponAttributes.HitPoisonArea = 30;
            Attributes.AttackChance = 15;
            Attributes.WeaponSpeed = 50;
        }

        public override int LabelNumber => 1072092;
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
