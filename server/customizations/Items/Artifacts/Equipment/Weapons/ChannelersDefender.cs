using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Weapons/ChannelersDefender.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class ChannelersDefender : GlassSword
    {
        [Constructible]
        public ChannelersDefender()
        {
            Hue = 95;
            Attributes.DefendChance = 10;
            Attributes.AttackChance = 5;
            Attributes.LowerManaCost = 5;
            Attributes.WeaponSpeed = 20;
            Attributes.CastRecovery = 1;
            Attributes.SpellChanneling = 1;
            WeaponAttributes.HitLowerAttack = 60;
            AosElementDamages.Energy = 100;
        }

        public override int LabelNumber => 1113518;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;
    }
}
