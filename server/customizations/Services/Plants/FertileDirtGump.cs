// cc-P128: ServUO pub57 Scripts/Services/Plants/FertileDirtGump.cs -> ours. When a seed is used on an empty raised
// garden plot and the planter has fertile dirt in the top of the pack, this asks whether to spend 20 of it on the mound
// (fertile dirt grows the plant faster, as in a bowl). ServUO's other branch, a New Magincia plot (MaginciaPlantItem),
// is not here: the Magincia system is in neither tree.

using Server.Gumps;
using Server.Items;
using Server.Network;

namespace Server.Engines.Plants;

public class FertileDirtGump : Gump
{
    private const int LabelColor = 0x7FFF;
    private const int FontColor = 0xFFFFFF;

    private readonly Seed _seed;
    private readonly GardenAddonComponent _attachTo;

    public FertileDirtGump(Seed seed, int amount, GardenAddonComponent attachTo) : base(50, 50)
    {
        _seed = seed;
        _attachTo = attachTo;

        AddBackground(0, 0, 300, 210, 9200);
        AddImageTiled(5, 5, 290, 30, 2624);
        AddImageTiled(5, 40, 290, 100, 2624);
        AddImageTiled(5, 145, 150, 60, 2624);
        AddImageTiled(160, 145, 135, 60, 2624);

        AddHtmlLocalized(90, 10, 150, 16, 1150359, LabelColor); // Raised Garden Bed
        AddHtmlLocalized(10, 45, 280, 90, 1150363, LabelColor); // You have fertile dirt ... do you want to use it?

        AddHtmlLocalized(10, 150, 80, 16, 1150361, LabelColor); // Needed:
        AddHtmlLocalized(10, 180, 80, 16, 1150360, LabelColor); // You Have:

        AddHtml(80, 150, 60, 16, $"<BASEFONT COLOR=#{FontColor:X6}>20</BASEFONT>");
        AddHtml(80, 180, 60, 16, $"<BASEFONT COLOR=#{FontColor:X6}>{amount}</BASEFONT>");

        AddButton(165, 150, 4023, 4025, 1);
        AddButton(165, 180, 4017, 4019, 2);

        AddHtml(205, 150, 100, 16, $"<BASEFONT COLOR=#{FontColor:X6}>Use</BASEFONT>");
        AddHtmlLocalized(205, 180, 100, 16, 1150364, LabelColor); // Not Use
    }

    public override void OnResponse(NetState state, in RelayInfo info)
    {
        if (info.ButtonID == 0)
        {
            return;
        }

        Plant(state.Mobile, _seed, _attachTo, info.ButtonID == 1);
    }

    /// <summary>Plants the seed in a new mound on the plot, fertile or not. False when the fertile dirt was asked for and missing.</summary>
    public static bool Plant(Mobile from, Seed seed, GardenAddonComponent comp, bool fertile)
    {
        if (fertile && from.Backpack?.ConsumeTotal(typeof(FertileDirt), 20, false) != true)
        {
            from.SendLocalizedMessage(1150366); // You don't have enough fertile dirt in the top level of your backpack.
            return false;
        }

        if (comp == null || seed == null || comp.Deleted || seed.Deleted)
        {
            return false;
        }

        var dirt = new RaisedGardenPlantItem(fertile);
        dirt.MoveToWorld(new Point3D(comp.X, comp.Y, comp.Z + 5), comp.Map);
        dirt.Component = comp;
        comp.Plant = dirt;
        dirt.PlantSeed(from, seed);
        return true;
    }
}
