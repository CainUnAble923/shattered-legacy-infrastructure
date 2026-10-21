// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/WaveInfo.cs (cc-P194). One wave of the battle for the
// Void Pool: its number, the creatures still standing, and everyone who earned credit on it. Not saved; the
// controller rebuilds its waves when a battle starts.

using System.Collections.Generic;
using Server.Mobiles;

namespace Server.Engines.VoidPool;

public class WaveInfo
{
    public WaveInfo(int index, List<BaseCreature> list)
    {
        Wave = index;
        Creatures = list;
        Credit = new List<Mobile>();
    }

    public int Wave { get; set; }
    public bool Cleared { get; set; }
    public List<BaseCreature> Creatures { get; }
    public List<Mobile> Credit { get; set; }
}
