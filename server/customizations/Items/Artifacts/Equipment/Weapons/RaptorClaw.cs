using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class RaptorClaw : Boomerang
    {
        [Constructible]
        public RaptorClaw()
        {
            Hue = 53;
            Slayer = SlayerName.Silver;

            Attributes.AttackChance = 12;
            Attributes.WeaponSpeed = 30;
            Attributes.WeaponDamage = 35;

            WeaponAttributes.HitLeechStam = 40;
        }

        public override int LabelNumber => 1112394; // Raptor Claw

        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
