// cc-P262 (spec section 4): Tinkering makes the toe ring, beside the rings in pinned's Jewelry group (1044049), at a gold
// ring's skill (40 to 90) from 2 iron ingots, appended at ServerStarted (the craft-registrations shape, as
// AlchemyCraftRegistrations). The recipe gate (cc-P78, ClusterFRecipeGate) gates it as it gates every Tinkering item
// above skill 30: by the Tinkering band book its minimum skill falls in. No extra key is needed.

using System;
using Server.Items;

namespace Server.Engines.Craft;

public static class AccessoryCraftRegistrations
{
    private static bool _registered;

    public static void Configure()
    {
        EventSink.ServerStarted += Register;
    }

    /// <summary>Idempotent; the test host calls it directly because ServerStarted never fires there.</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        var tinkering = DefTinkering.CraftSystem;

        if (tinkering == null)
        {
            Console.WriteLine("[AccessoryCraftRegistrations] WARNING: DefTinkering.CraftSystem is null; toe rings are not craftable.");
            return;
        }

        _registered = true;

        tinkering.AddCraft(typeof(ToeRing), 1044049, "toe ring", 40.0, 90.0, typeof(IronIngot), 1044036, 2, 1044037);
    }
}
