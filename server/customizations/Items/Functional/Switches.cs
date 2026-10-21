// cc-P199: ServUO pub57 Scripts/Items/Functional/Switches.cs. The Underworld's working wall switch (art 0x1091 off,
// 0x1092 on). Its first flip says "You hear a deep rumbling as something seems to happen." (1042901), later flips
// "There seems to be no further effect right now." (1042900), and the first flip lifts every Globe of Sosaria within
// eight tiles (art 0x3660 at hue 1000): one Z a step every 1.5 seconds, up for 8 steps, still for 6, down for 8, back
// where it started 31.5 seconds after the flip (22 ticks, the first at once).
//
// ServUO places two, each beside a globe placed as a hued Static (Data/Decoration/Stygian Abyss/Ter Mur/
// Underworld.cfg:98-104, globe 1070,1197,-39 and switch 1069,1193,-36 "Secret Room SouthWest"; :157-173, globe
// 1004,1052,-31 and switch 1005,1044,-21). Nothing on this shard places them.
//
// Ported as ServUO wrote it. The MoveTimer's second argument (Switches.cs:52, `coord`) is kept; ServUO stores it in a
// field nothing reads, which is dropped here.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class UnderworldSwitchWE : BaseSwitch
{
    // 1042901 = You hear a deep rumbling as something seems to happen.
    // 1042900 = There seems to be no further effect right now.
    // true = it does something; it is not a useless or broken switch.
    [Constructible]
    public UnderworldSwitchWE() : base(0x1091, 0x1092, 1042901, 1042900, true)
    {
    }

    public override void DoSomethingSpecial(Mobile from)
    {
        foreach (var item in GetItemsInRange(8))
        {
            if (item.ItemID == 0x3660 && item.Hue == 1000) // Dark Globe of Sosaria
            {
                new MoveTimer(item, 1).Start();
            }
        }
    }

    private class MoveTimer : Timer
    {
        private readonly Item _item;
        private int _stage;
        private int _cycle;

        public MoveTimer(Item sphere, int coord) : base(TimeSpan.FromSeconds(0.0), TimeSpan.FromSeconds(1.5))
        {
            _item = sphere;
        }

        protected override void OnTick()
        {
            if (_item.Deleted)
            {
                Stop();
                return;
            }

            _stage++;

            if (_cycle == 0)
            {
                _item.Z += 1;
            }
            else if (_cycle == 1)
            {
                _item.Z += 0;
            }
            else
            {
                _item.Z += -1;
            }

            if (_stage == 8)
            {
                _cycle++;
            }
            else if (_stage == 14)
            {
                _cycle++;
            }
            else if (_stage == 22)
            {
                Stop();
            }
        }
    }
}
