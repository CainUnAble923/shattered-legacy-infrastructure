// ClusterFStaffHubSpots.cs
//
// cc-P51 (F-30). The Staff Hub's Travel tab: saved spots and the recent list.
//
//   Saved spots  per staff ACCOUNT, kept across restarts in their own save file (Saves/StaffHubSpots/StaffHubSpots.bin)
//                through pinned's GenericPersistence (Server/Serialization/GenericPersistence.cs), versioned, and
//                deliberately not in ClusterFAccountData (cc-P48 bumped that; this keeps the two independent).
//   Recent       the last RecentCap places a staff member went through the hub, newest first. Memory only, per
//                character: a restart clears it, by design (it is "where was I just now", not a bookmark).
//
// An unknown version fails loudly (the D40 rule), as ClusterFAccountData does.

using System;
using System.Collections.Generic;
using System.IO;
using Server.Accounting;

namespace Server;

public sealed record StaffSpot(string Name, Map Map, Point3D Location);

public class ClusterFStaffHubSpots : GenericPersistence
{
    // 0: cc-P51.
    public const int CurrentVersion = 0;

    public const int RecentCap = 5;
    public const int MaxSpotsPerAccount = 100;
    public const int MaxNameLength = 32;

    private static ClusterFStaffHubSpots _instance;

    // Saved spots by account username, oldest first.
    private static readonly Dictionary<string, List<StaffSpot>> _spots = new(StringComparer.OrdinalIgnoreCase);

    // Recent places by character serial, newest first.
    private static readonly Dictionary<Serial, List<StaffSpot>> _recent = new();

    public ClusterFStaffHubSpots() : base("StaffHubSpots", 10)
    {
    }

    public static void Configure()
    {
        _instance ??= new ClusterFStaffHubSpots();
    }

    private static string Key(Mobile staff) => (staff?.Account as IAccount)?.Username;

    // ---------------------------------------------------------------- saved spots

    public static IReadOnlyList<StaffSpot> SpotsOf(Mobile staff) =>
        Key(staff) is { } key && _spots.TryGetValue(key, out var list) ? list : Array.Empty<StaffSpot>();

    /// <summary>
    /// Saves where the staff member stands under a name (trimmed, 1 to MaxNameLength characters). A spot of the same
    /// name on the account is replaced. Returns a line for the staff member either way.
    /// </summary>
    public static bool SaveHere(Mobile staff, string name, out string message)
    {
        var key = Key(staff);
        name = name?.Trim() ?? "";

        if (key == null)
        {
            message = "Saved spots belong to an account, and this character has none.";
            return false;
        }

        if (name.Length == 0 || name.Length > MaxNameLength)
        {
            message = $"Name the spot (1 to {MaxNameLength} characters).";
            return false;
        }

        if (staff.Map == null || staff.Map == Map.Internal)
        {
            message = "You are not on a facet.";
            return false;
        }

        if (!_spots.TryGetValue(key, out var list))
        {
            _spots[key] = list = [];
        }

        var replaced = list.RemoveAll(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) > 0;

        if (!replaced && list.Count >= MaxSpotsPerAccount)
        {
            message = $"This account already has {MaxSpotsPerAccount} saved spots; delete one first.";
            return false;
        }

        list.Add(new StaffSpot(name, staff.Map, staff.Location));
        message = $"{(replaced ? "Replaced" : "Saved")} '{name}' at {staff.Location} on {staff.Map}.";
        return true;
    }

    /// <summary>
    /// Deletes the spot at an index, only if it still has the name the gump showed there, so a gump drawn before
    /// another delete removes nothing it did not show.
    /// </summary>
    public static bool Delete(Mobile staff, int index, string expectedName)
    {
        if (Key(staff) is not { } key || !_spots.TryGetValue(key, out var list) ||
            index < 0 || index >= list.Count || !list[index].Name.Equals(expectedName, StringComparison.Ordinal))
        {
            return false;
        }

        list.RemoveAt(index);
        if (list.Count == 0)
        {
            _spots.Remove(key);
        }

        return true;
    }

    // ---------------------------------------------------------------- recent

    public static IReadOnlyList<StaffSpot> RecentOf(Mobile staff) =>
        staff != null && _recent.TryGetValue(staff.Serial, out var list) ? list : Array.Empty<StaffSpot>();

    public static void AddRecent(Mobile staff, string label, Map map, Point3D location)
    {
        if (staff == null)
        {
            return;
        }

        if (!_recent.TryGetValue(staff.Serial, out var list))
        {
            _recent[staff.Serial] = list = [];
        }

        list.Insert(0, new StaffSpot(label, map, location));

        if (list.Count > RecentCap)
        {
            list.RemoveRange(RecentCap, list.Count - RecentCap);
        }
    }

    // ---------------------------------------------------------------- persistence

    public override void Serialize(IGenericWriter writer) => WriteTo(writer);

    public override void Deserialize(IGenericReader reader) => ReadFrom(reader);

    public static void WriteTo(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);
        writer.WriteEncodedInt(_spots.Count);

        foreach (var (account, list) in _spots)
        {
            writer.Write(account);
            writer.WriteEncodedInt(list.Count);

            foreach (var spot in list)
            {
                writer.Write(spot.Name);
                writer.Write(spot.Map);
                writer.Write(spot.Location);
            }
        }
    }

    /// <summary>Replaces every saved spot with what the reader holds.</summary>
    public static void ReadFrom(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException(
                $"ClusterFStaffHubSpots version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        _spots.Clear();

        var accounts = reader.ReadEncodedInt();
        for (var i = 0; i < accounts; i++)
        {
            var account = reader.ReadString();
            var count = reader.ReadEncodedInt();
            var list = new List<StaffSpot>(count);

            for (var j = 0; j < count; j++)
            {
                var name = reader.ReadString();
                var map = reader.ReadMap();
                var location = reader.ReadPoint3D();
                list.Add(new StaffSpot(name, map, location));
            }

            if (account != null && list.Count > 0)
            {
                _spots[account] = list;
            }
        }
    }
}
