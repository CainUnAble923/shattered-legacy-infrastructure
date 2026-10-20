// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/SOS Artifacts/YellowPolkaDotBikini.cs -> ours, the
// High Seas fishing port. A shipwreck find: leather bustier arms hued 1169, "recovered from a shipwreck" on the tooltip.
// ServUO adds it to the SOS chest's artifact roll in its Fishing.cs; pinned's SOS chest fill reads pinned's own list, so
// here it is a staff and loot item until that roll is patched (left for the next prompt).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class YellowPolkaDotBikini : LeatherBustierArms
{
    [Constructible]
    public YellowPolkaDotBikini() => Hue = 1169;

    public override int LabelNumber => 1149962; // Yellow Polka Dot Bikini

    public override void AddNameProperties(IPropertyList list)
    {
        base.AddNameProperties(list);
        list.Add(1041645); // recovered from a shipwreck
    }
}
