using System;
using System.Collections.Generic;
using System.IO;

namespace Server;

/// <summary>
/// One chopped-grove record: where the player stood when they cut the tree, when
/// it was first logged, and how many logs have been pulled from that spot.
///
/// Two chops are the same grove if they are on the same facet, in the same region,
/// and within 15 tiles of each other (WoodDiscoveryEntry.FindNearbyLocation).
///
/// FacetName is Map.Name, so Map.Parse turns it back into a facet. A record
/// migrated from version 0 has facet and region <see cref="Unknown"/> and
/// Point3D.Zero: it says when, not where.
/// </summary>
public class WoodLocationRecord
{
    public const string Unknown = "Unknown";

    public string   FacetName     { get; }
    public string   RegionName    { get; }
    public Point3D  Location      { get; }
    public DateTime DiscoveredAt  { get; }
    public int      AmountChopped { get; set; }

    public bool HasKnownLocation => FacetName != Unknown;

    public WoodLocationRecord(string facetName, string regionName, Point3D location)
        : this(facetName, regionName, location, DateTime.UtcNow, 0)
    {
    }

    /// <summary>Migration constructor — preserves the original discovery timestamp.</summary>
    public WoodLocationRecord(string facetName, string regionName, Point3D location, DateTime discoveredAt, int amountChopped)
    {
        FacetName     = facetName;
        RegionName    = regionName;
        Location      = location;
        DiscoveredAt  = discoveredAt;
        AmountChopped = amountChopped;
    }

    public WoodLocationRecord(IGenericReader r)
    {
        FacetName     = r.ReadString();
        RegionName    = r.ReadString();
        Location      = r.ReadPoint3D();
        DiscoveredAt  = r.ReadDateTime();
        AmountChopped = r.ReadInt();
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(FacetName);
        w.Write(RegionName);
        w.Write(Location);
        w.Write(DiscoveredAt);
        w.Write(AmountChopped);
    }

    /// <summary>
    /// The region label a grove is logged under. A named region gives its name. The
    /// facet's default region, where no region applies, is "Wilderness". An unnamed
    /// region that is not the default gives its type name, so a chop inside a house
    /// (HouseRegion has no name) is logged as "HouseRegion" and never as "Wilderness".
    /// Parents are not consulted: a named town would hide an unnamed house inside it.
    /// </summary>
    public static string RegionLabel(string? regionName, bool isDefaultRegion, string regionTypeName) =>
        !string.IsNullOrWhiteSpace(regionName) ? regionName
        : isDefaultRegion ? "Wilderness"
        : regionTypeName;
}

/// <summary>
/// Tracks when and where a player found a wood type during lumberjacking: one
/// WoodLocationRecord per grove, a new one each time the type is found again
/// somewhere else.
///
/// Stored in ClusterFAccountData.WoodDiscoveries, keyed by canonical wood key
/// (e.g. "Ironwood", "OakWood", "Starwood").
///
/// State lifecycle (extended timbers only):
///   Discovered → timber chopped for the first time; regular Log substituted.
///   Reported   → player has visited the Foresters' Guildmaster; real log drops normally.
///
/// Vanilla colored woods (Oak → Frostwood) are auto-set to Reported on first find
/// and do not gate log delivery.
/// </summary>
public class WoodDiscoveryEntry
{
    public string                   WoodKey      { get; }
    public List<WoodLocationRecord> Locations    { get; } = new();
    public int                      TotalChopped { get; set; }
    public DiscoveryState           State        { get; set; }

    /// <summary>Earliest grove's timestamp, or DateTime.MinValue with no groves.</summary>
    public DateTime FirstFound
    {
        get
        {
            var first = DateTime.MaxValue;
            foreach (var loc in Locations)
                if (loc.DiscoveredAt < first)
                    first = loc.DiscoveredAt;
            return Locations.Count > 0 ? first : DateTime.MinValue;
        }
    }

    /// <summary>Brand-new discovery constructor (no locations yet — caller adds them).</summary>
    public WoodDiscoveryEntry(string woodKey)
    {
        WoodKey      = woodKey;
        TotalChopped = 0;
        State        = DiscoveryState.Discovered;
    }

    /// <summary>Appends a new location record and returns it for immediate mutation.</summary>
    public WoodLocationRecord AddLocation(string facetName, string regionName, Point3D location)
    {
        var record = new WoodLocationRecord(facetName, regionName, location);
        Locations.Add(record);
        return record;
    }

    /// <summary>
    /// The first grove on <paramref name="facetName"/> in <paramref name="regionName"/>
    /// within <paramref name="radius"/> tiles of <paramref name="pt"/> (2-D distance),
    /// or null. Facet and region are part of the match so that each record's labels
    /// describe every chop counted under it.
    /// </summary>
    public WoodLocationRecord? FindNearbyLocation(string facetName, string regionName, Point3D pt, int radius)
    {
        var rSq = radius * radius;
        foreach (var loc in Locations)
        {
            if (loc.FacetName != facetName || loc.RegionName != regionName)
                continue;
            var dx = loc.Location.X - pt.X;
            var dy = loc.Location.Y - pt.Y;
            if (dx * dx + dy * dy <= rSq)
                return loc;
        }
        return null;
    }

    /// <summary>
    /// Serialization versions:
    ///   v0 — WoodKey + TotalChopped + State + FirstFound. Read forward as one
    ///        (Unknown, Unknown, Point3D.Zero, FirstFound, TotalChopped) grove.
    ///   v1 — WoodKey + TotalChopped + State + a list of WoodLocationRecord
    ///        (facet, region, Point3D, DateTime, amount). What this file writes.
    ///        Same layout the 2026-06-29 build wrote (D40), so its saves read as they are.
    /// Any other version throws.
    /// </summary>
    public WoodDiscoveryEntry(IGenericReader r)
    {
        var version  = r.ReadInt();
        WoodKey      = r.ReadString();
        TotalChopped = r.ReadInt();
        State        = (DiscoveryState)r.ReadInt();

        switch (version)
        {
            case 0:
                var firstFound = r.ReadDateTime();
                Locations.Add(new WoodLocationRecord(
                    WoodLocationRecord.Unknown, WoodLocationRecord.Unknown, Point3D.Zero, firstFound, TotalChopped));
                break;
            case 1:
                var count = r.ReadInt();
                for (var i = 0; i < count; i++)
                    Locations.Add(new WoodLocationRecord(r));
                break;
            default:
                throw new InvalidDataException(
                    $"WoodDiscoveryEntry '{WoodKey}' has version {version}; this build reads 0 and 1.");
        }
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(1); // version
        w.Write(WoodKey);
        w.Write(TotalChopped);
        w.Write((int)State);
        w.Write(Locations.Count);
        foreach (var loc in Locations)
            loc.Serialize(w);
    }
}
