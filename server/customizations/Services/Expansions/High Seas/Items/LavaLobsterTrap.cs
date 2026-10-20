// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/LavaLobsterTrap.cs -> ours, the High Seas fishing port.
//
// A lobster trap for lava: the same trap, deployed on lava tiles instead of water. ServUO reads the lava tile list from
// its Fishing.LavaTiles; pinned Fishing has none, so the list lives here (ServUO Services/Harvest/Fishing.cs:791-805,
// copied as it is, including 4560-4562, which are not lava tiles in the client but are in ServUO's list). Lava fishing
// with a pole (the lava hook) is left for the next prompt; the trap works on its own.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class LavaLobsterTrap : LobsterTrap
{
    /// <summary>ServUO Fishing.LavaTiles: the land and static tiles a lava hook or a lava trap fishes.</summary>
    public static readonly int[] LavaTiles =
    {
        0x1F4, 0x1F5,
        0x1F6, 0x1F7,

        4846, 4847, 4848, 4849, 4850,
        4852, 4853, 4854, 4855, 4856, 4857, 4858, 4859, 4560, 4561, 4562,
        4864, 4865, 4866, 4867, 4868,
        4870, 4871, 4872, 4873, 4874,
        4876, 4877, 4878, 4879, 4880,
        4882, 4883, 4884, 4885, 4886,
        4888, 4889, 4890, 4891, 4892
    };

    [Constructible]
    public LavaLobsterTrap()
    {
    }

    public override int[] UseableTiles => LavaTiles;

    public override void AddNameProperty(IPropertyList list)
    {
        list.Add(1116474); // lava lobster trap
    }
}
