// ServUO: Services/Underworld/ExperimentalRoom/ExperimentalRoomController.cs (cc-P197). The invisible item at the
// Experimental Room's entrance (980,1117,-42) that keeps the 24-hour cooldown table: a player whose gem is deleted
// (used, expired or failed) waits a day before activating another. The table is static and never saved: ServUO
// recreates it empty on load and so does this (a restart forgives every cooldown, ServUO's behaviour). ServUO's
// Serialize calls Defrag for its side effect; there is no hook for that here and it changes nothing a player sees.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExperimentalRoomController : Item
{
    private static Dictionary<Mobile, DateTime> _table = new();

    public static ExperimentalRoomController Instance { get; private set; }

    [Constructible]
    public ExperimentalRoomController() : base(7107)
    {
        _table = new Dictionary<Mobile, DateTime>();
        Visible = false;
        Movable = false;

        Instance = this;
    }

    public static void AddToTable(Mobile from)
    {
        if (from == null)
        {
            return;
        }

        _table[from] = Core.Now + TimeSpan.FromHours(24);
    }

    public static bool IsInCooldown(Mobile from)
    {
        Defrag();

        return _table.ContainsKey(from);
    }

    public static void Defrag()
    {
        var list = new List<Mobile>();

        foreach (var kvp in _table)
        {
            if (kvp.Value <= Core.Now)
            {
                list.Add(kvp.Key);
            }
        }

        foreach (var m in list)
        {
            _table.Remove(m);
        }
    }

    /// <summary>Test support: forgets every cooldown, as a restart does.</summary>
    public static void ClearTable() => _table.Clear();

    public static bool HasItem(Mobile from, Type type)
    {
        if (from?.Backpack == null)
        {
            return false;
        }

        return from.Backpack.FindItemByType(type) != null;
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _table = new Dictionary<Mobile, DateTime>();
        Instance = this;
    }
}
