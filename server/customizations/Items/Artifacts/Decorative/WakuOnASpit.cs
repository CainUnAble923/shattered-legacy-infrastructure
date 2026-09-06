using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class WakuOnASpit : Item
    {
        [Constructible]
        public WakuOnASpit() : base(7832)
        {
        }

        public override string DefaultName => "Waku on a Spit";

        // ServUO: BaseDecorationArtifact with ShowArtifactRarity == false.
        // ModernUO's BaseDecorationArtifact adds the rarity line unconditionally, so this
        // derives from Item and reproduces the three members that base contributes.
        // Rarity retained as data; nothing in ModernUO reads it. See notes/cc3-eodon.md.
        public int ArtifactRarity => 11;

        public override double DefaultWeight => 10.0;
        public override bool ForceShowProperties => true;
        public override bool DisplayWeight => false;
    }
}
