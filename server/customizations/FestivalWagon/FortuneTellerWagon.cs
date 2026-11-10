// FortuneTellerWagon.cs (cc-P858): the fortune teller's wagon by Olorin (servuo.dev, 2022), rebuilt as our own addon
// from its parts list (FortuneTellerWagonDesign.cs, generated), with a four-step stair up to the south porch in place of
// the author's ladder so a player can walk in. It stands beside the Festival Peddler's stall while the stall is up (a
// season is on) and goes away with it: FestivalMarket places it, tracks it by serial with the stall's pieces, and puts it
// up on a world whose stall is already up (TopUpWagon, at every world load and hourly). Where: the setting
// clusterf.festival.wagonAt (x,y,z, or "off"), default the spot chosen for main's New Haven in cc-P858's review.
// Notes: shard-migration notes/cc-P858-lamps-and-peddler-wagon.md.

#nullable enable

using ModernUO.Serialization;

namespace Server.Items;

/// <summary>One part of the wagon: art, offset, hue, light (null for none) and name (null for the art's own).</summary>
public sealed record WagonPart(int ItemID, int X, int Y, int Z, int Hue, LightType? Light, string? Name);

[SerializationGenerator(0, false)]
public partial class FortuneTellerWagonComponent : AddonComponent
{
    public FortuneTellerWagonComponent(int itemID) : base(itemID)
    {
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"The fortune teller's wagon, after {FortuneTellerWagonDesign.Credit}");
    }
}

[SerializationGenerator(0, false)]
public partial class FortuneTellerWagon : BaseAddon
{
    [Constructible]
    public FortuneTellerWagon()
    {
        foreach (var p in FortuneTellerWagonDesign.Parts)
        {
            var c = new FortuneTellerWagonComponent(p.ItemID) { Hue = p.Hue };
            if (p.Light is { } light)
            {
                c.Light = light;
            }

            if (p.Name != null)
            {
                c.Name = p.Name;
            }

            AddComponent(c, p.X, p.Y, p.Z);
        }
    }

    // Each part keeps its own hue (the wood, the curtains, the wheels).
    public override bool ShareHue => false;
}
