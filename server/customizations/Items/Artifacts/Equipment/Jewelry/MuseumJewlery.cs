using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/MuseumJewlery.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO declares these four constructors without [Constructable]; they carry [Constructible] here so staff can spawn them.
    [SerializationGenerator(0, false)]
    public partial class VesperCollectionRing : GoldRing
    {
        [Constructible]
        public VesperCollectionRing()
        {
        }

        public override int LabelNumber => 1073234;
    }

    // ServUO: Items/Artifacts/Equipment/Jewelry/MuseumJewlery.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO declares these four constructors without [Constructable]; they carry [Constructible] here so staff can spawn them.
    [SerializationGenerator(0, false)]
    public partial class VesperCollectionNecklace : GoldNecklace
    {
        [Constructible]
        public VesperCollectionNecklace()
        {
        }

        public override int LabelNumber => 1073234;
    }

    // ServUO: Items/Artifacts/Equipment/Jewelry/MuseumJewlery.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO declares these four constructors without [Constructable]; they carry [Constructible] here so staff can spawn them.
    [SerializationGenerator(0, false)]
    public partial class VesperCollectionBracelet : GoldBracelet
    {
        [Constructible]
        public VesperCollectionBracelet()
        {
        }

        public override int LabelNumber => 1073234;
    }

    // ServUO: Items/Artifacts/Equipment/Jewelry/MuseumJewlery.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO declares these four constructors without [Constructable]; they carry [Constructible] here so staff can spawn them.
    [SerializationGenerator(0, false)]
    public partial class VesperCollectionEarrings : GoldEarrings
    {
        [Constructible]
        public VesperCollectionEarrings()
        {
        }

        public override int LabelNumber => 1073234;
    }
}
