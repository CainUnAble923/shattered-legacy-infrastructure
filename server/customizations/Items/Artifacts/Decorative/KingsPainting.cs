using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/KingsPainting.cs (CC9 batch 5). Same shape as KingsGildedStatue: ServUO's
    // ShowArtifactRarity => false has no switch to flip in ModernUO's BaseDecorationArtifact, so both paintings
    // derive from Item and reproduce the base's DefaultWeight and ForceShowProperties without the rarity line.
    [SerializationGenerator(0, false)]
    public partial class KingsPainting1 : Item
    {
        [Constructible]
        public KingsPainting1() : base(19552)
        {
        }

        public int ArtifactRarity => 8;
        public override double DefaultWeight => 10.0;
        public override bool ForceShowProperties => true;
        public override string DefaultName => "A Painting From The Personal Collection Of The King";
    }

    [SerializationGenerator(0, false)]
    public partial class KingsPainting2 : Item
    {
        [Constructible]
        public KingsPainting2() : base(19558)
        {
        }

        public int ArtifactRarity => 8;
        public override double DefaultWeight => 10.0;
        public override bool ForceShowProperties => true;
        public override string DefaultName => "A Painting From The Personal Collection Of The King";
    }
}
