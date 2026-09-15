using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/GildedStatue.cs (CC9 batch 5).
    // ServUO derives from BaseDecorationArtifact and overrides ShowArtifactRarity => false, which its base
    // consults before emitting the rarity line. Pinned ModernUO's BaseDecorationArtifact has no such switch
    // (Items/Decoration Artifacts/BaseDecorationArtifact.cs always adds 1061078), so this derives from Item and
    // reproduces the base's other two members (DefaultWeight 10, ForceShowProperties) without the line, the
    // batch 4 "stock base cannot be re-parented, inline it" shape. ArtifactRarity is kept as a plain property
    // for parity; nothing reads it.
    [SerializationGenerator(0, false)]
    public partial class KingsGildedStatue : Item
    {
        [Constructible]
        public KingsGildedStatue() : base(0x139D) => Hue = 2721;

        public int ArtifactRarity => 8;
        public override double DefaultWeight => 10.0;
        public override bool ForceShowProperties => true;
        public override string DefaultName => "A Gilded Statue from the Personal Collection of the King";
    }
}
