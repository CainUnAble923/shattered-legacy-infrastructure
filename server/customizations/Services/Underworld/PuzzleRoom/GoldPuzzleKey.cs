// ServUO: Services/Underworld/PuzzleRoom/GoldPuzzleKey.cs (cc-P197). The gold key (1024111, hue 1174) the second
// solved maze board hands out, which the north puzzle box asks for: a blessed BaseDecayingItem that lives 1800
// seconds.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class GoldPuzzleKey : BaseDecayingItem
{
    [Constructible]
    public GoldPuzzleKey() : base(4114) => Hue = 1174;

    public override int Lifespan => 1800;
    public override int LabelNumber => 1024111; // gold key
}
