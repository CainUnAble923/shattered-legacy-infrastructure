// ServUO: Services/Underworld/Maze of Death/Rewards.cs (cc-P197). The seven decorations the Maze of Death's
// puzzle board can pay (UnderworldPuzzleItem.Rewards, with the venoms, essence, toxin and lucky coins the shard
// already has), kept in one file as ServUO has them. Plain items; the recipe's transformation. ServUO's
// commented-out FlouredBreadBoard stays out.

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(5353, 5354)]
[SerializationGenerator(0, false)]
public partial class MouldingBoard : Item
{
    [Constructible]
    public MouldingBoard() : base(5353)
    {
    }
}

[SerializationGenerator(0, false)]
public partial class DoughBowl : Item
{
    [Constructible]
    public DoughBowl() : base(4323)
    {
    }
}

[SerializationGenerator(0, false)]
public partial class HornedTotemPole : Item
{
    [Constructible]
    public HornedTotemPole() : base(12289)
    {
    }
}

[SerializationGenerator(0, false)]
public partial class LargeSquarePillow : Item
{
    [Constructible]
    public LargeSquarePillow() : base(5691)
    {
    }
}

[SerializationGenerator(0, false)]
public partial class LargeDiamondPillow : Item
{
    [Constructible]
    public LargeDiamondPillow() : base(5690)
    {
    }
}

[SerializationGenerator(0, false)]
public partial class DustyPillow : Item
{
    [Constructible]
    public DustyPillow() : base(Utility.RandomList(5690, 5691))
    {
    }

    public override int LabelNumber => 1113638; // dusty pillow
}

[SerializationGenerator(0, false)]
public partial class StatuePedestal : Item
{
    [Constructible]
    public StatuePedestal() : base(13042) => Weight = 5;
}
