// ServUO: Services/Underworld/ExperimentalRoom/ExperimentalRoomRewards.cs (cc-P197). The eight decorations the
// Experimental Room's reward chest hands out (ExperimentalRoomChest.GetRandomDrop: stalagmites 7 in 17, flowstone
// 4 in 17, the rest 1 in 17 each), kept in one file as ServUO has them. Plain items; the recipe's transformation.
// One ServUO value is not copied: CanvaslessEasel's LabelNumber 123467 is not a cliloc number (the client's strings
// start at 500000) and shows an empty name, so the override is dropped and the easel takes its tile name (the D-4
// rule's broken kind; notes).

using ModernUO.Serialization;

namespace Server.Items;

[Flippable(12287, 12288)]
[SerializationGenerator(0, false)]
public partial class TwoStoryBanner : Item
{
    [Constructible]
    public TwoStoryBanner() : base(12287)
    {
    }
}

[SerializationGenerator(0, false)]
public partial class Stalagmite : Item
{
    [Constructible]
    public Stalagmite() : base(Utility.RandomList(2272, 2273, 2276, 2277, 2279, 2281, 2282))
    {
    }
}

[SerializationGenerator(0, false)]
public partial class Flowstone : Item
{
    [Constructible]
    public Flowstone() : base(Utility.RandomList(2274, 2275, 2278, 2280))
    {
    }
}

[SerializationGenerator(0, false)]
public partial class HangingChainmailLegs : Item
{
    [Constructible]
    public HangingChainmailLegs() : base(5052)
    {
    }
}

[SerializationGenerator(0, false)]
public partial class HangingRingmailTunic : Item
{
    [Constructible]
    public HangingRingmailTunic() : base(5095)
    {
    }
}

[SerializationGenerator(0, false)]
public partial class PluckedChicken : Item
{
    [Constructible]
    public PluckedChicken() : base(7819)
    {
    }
}

[SerializationGenerator(0, false)]
public partial class ColorfulTapestry : Item
{
    [Constructible]
    public ColorfulTapestry() : base(17092)
    {
    }
}

[SerializationGenerator(0, false)]
public partial class CanvaslessEasel : Item
{
    // ServUO: public override int LabelNumber => 123467; not a cliloc (header).

    [Constructible]
    public CanvaslessEasel() : base(Utility.RandomBool() ? 3943 : 3945)
    {
    }
}
