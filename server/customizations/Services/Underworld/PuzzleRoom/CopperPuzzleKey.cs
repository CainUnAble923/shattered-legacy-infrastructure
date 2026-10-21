// ServUO: Services/Underworld/PuzzleRoom/CopperPuzzleKey.cs (cc-P197). The copper key (1024110) the first solved
// maze board hands out: a blessed BaseDecayingItem that lives 1800 seconds.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class CopperPuzzleKey : BaseDecayingItem
{
    [Constructible]
    public CopperPuzzleKey() : base(4115)
    {
    }

    public override int Lifespan => 1800;
    public override int LabelNumber => 1024110; // copper key
}
