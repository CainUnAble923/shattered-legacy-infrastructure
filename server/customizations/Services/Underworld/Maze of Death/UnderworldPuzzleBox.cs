// ServUO: Services/Underworld/Maze of Death/UnderworldPuzzleBox.cs (cc-P197). The chest at the far end of the
// Maze of Death (1068,1026,-37, a MetalChest showing as 3712, unmovable): within three tiles it hands a player an
// UnderworldPuzzleItem ("You recieve a puzzle piece.", ServUO's spelling), one at a time ("You already have a
// puzzle board.") and once a day for a player (1113413; staff are not held to it). The cooldown table is static
// and never saved, as ServUO's (a restart forgives it). DateTime is Core.Now.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class UnderworldPuzzleBox : MetalChest
{
    private static readonly Dictionary<Mobile, DateTime> _table = new();

    [Constructible]
    public UnderworldPuzzleBox()
    {
        Movable = false;
        ItemID = 3712;
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from.InRange(Location, 3))
        {
            if (from.AccessLevel == AccessLevel.Player && IsInCooldown(from))
            {
                from.SendLocalizedMessage(1113413); // You have recently participated in this challenge. You must wait 24 hours to try again.
            }
            else if (from.Backpack != null)
            {
                var item = from.Backpack.FindItemByType<UnderworldPuzzleItem>();

                if (item != null)
                {
                    from.SendMessage("You already have a puzzle board.");
                }
                else
                {
                    from.AddToBackpack(new UnderworldPuzzleItem());
                    from.SendMessage("You recieve a puzzle piece.");

                    if (from.AccessLevel == AccessLevel.Player)
                    {
                        _table[from] = Core.Now + TimeSpan.FromHours(24);
                    }
                }
            }
        }
    }

    public static bool IsInCooldown(Mobile from)
    {
        if (_table.TryGetValue(from, out var until) && until < Core.Now)
        {
            _table.Remove(from);
        }

        return _table.ContainsKey(from);
    }

    /// <summary>Test support: forgets every cooldown, as a restart does.</summary>
    public static void ClearTable() => _table.Clear();
}
