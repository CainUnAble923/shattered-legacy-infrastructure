// ServUO: Services/Revamped Dungeons/BlackthornDungeon/Items/BlackthornPainting.cs (cc-P196). Two Agent of the Crown
// rewards (10 points each): the paintings 0x4C62/0x4C63 and 0x4C64/0x4C65, "painting" (1023744). The port recipe's
// transformation; Flipable is pinned's Flippable; IsArtifact dropped (no pinned reader).

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(0x4C62, 0x4C63)]
[SerializationGenerator(0, false)]
public partial class BlackthornPainting1 : Item
{
    [Constructible]
    public BlackthornPainting1() : base(0x4C62)
    {
    }

    public override int LabelNumber => 1023744; // painting
}

[Flippable(0x4C64, 0x4C65)]
[SerializationGenerator(0, false)]
public partial class BlackthornPainting2 : Item
{
    [Constructible]
    public BlackthornPainting2() : base(0x4C64)
    {
    }

    public override int LabelNumber => 1023744; // painting
}
