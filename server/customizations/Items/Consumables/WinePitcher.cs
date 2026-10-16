// cc-P128: ServUO pub57 Scripts/Items/Consumables/WinePitcher.cs -> ours. A begging reward (ServUO Skills/Begging.cs:200-300
// hands these out from Begging's SA reward table): the stock Pitcher with "Acquired by begging" on its properties.
// Pinned's Begging has no reward table yet, so until that skill file is ported only staff can make one ([add BegWinePitcher).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BegWinePitcher : Pitcher
{
    [Constructible]
    public BegWinePitcher() : base(BeverageType.Wine)
    {
        ItemID = Utility.RandomDouble() > .5 ? 8091 : 8092;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1075129); // Acquired by begging
    }
}
