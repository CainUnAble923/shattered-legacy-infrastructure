using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Talismans/Slither.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // RandomTalisman.GetRandomBlessed() is BaseTalisman.GetRandomBlessed() on ModernUO.
    [SerializationGenerator(0, false)]
    public partial class Slither : BaseTalisman
    {
        [Constructible]
        public Slither() : base(0x2F5B)
        {
            Hue = 0x587;
            Blessed = GetRandomBlessed();
            Attributes.BonusHits = 10;
            Attributes.RegenHits = 2;
            Attributes.DefendChance = 10;
        }

        public override int LabelNumber => 1114782;
    }
}
