// cc-P760: the reward stores' one item of their own, RewardDecor: house decor drawn from registered art (registry.csv) or
// stock art. It is plain decor: no item properties, nothing on use, movable so it can be locked down in a house. The art's
// tiledata on the server comes from the set that registered it (GuildHallDecorArt, ClusterFCommunityArtRecords), which
// gives these pieces a weight of 255 (immovable); RewardDecor's own weight makes them carryable.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class RewardDecor : Item
{
    [Constructible]
    public RewardDecor() : this(0x14F0, "a piece of decor")
    {
    }

    public RewardDecor(int itemID, string name, int hue = 0) : base(itemID)
    {
        Name = name;
        Hue = hue;
    }

    public override double DefaultWeight => 5.0;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("House decor");
    }
}
