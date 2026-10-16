// cc-P127: ServUO pub57 Scripts/Services/TreasureMaps/BagOfGems.cs -> ours. The bag a revamped treasure chest keeps its
// gems and gold in.

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0xA333, 0xA334)]
[SerializationGenerator(0, false)]
public partial class BagOfGems : Bag
{
    [Constructible]
    public BagOfGems()
    {
        ItemID = 0xA333;
    }

    public override int LabelNumber => 1048032; // a bag
}
