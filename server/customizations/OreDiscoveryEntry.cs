using System;
using System.Collections.Generic;

namespace Server;

/// <summary>
/// Tracks when and where a player discovered a colored ore type, including
/// ALL individual vein locations that have been mined.
///
/// Stored in ClusterFAccountData.OreDiscoveries, keyed by canonical ore key
/// (e.g. "DullCopper", "ShadowIron", "Platinum").
///
/// Iron is intentionally excluded — only colored and extended ores are logged.
///
/// Serialization versions:
///   v0 — flat single-location fields (legacy; auto-migrated to v2 on next save)
///   v1 — OreKey + TotalMined + State + List of OreLocationRecord (no Reported flag)
///   v2 — adds OreLocationRecord.Reported per-vein flag; v1/v0 locations migrated to
///        Reported=true if entry.State==Reported, false otherwise
///
/// State lifecycle:
///   Discovered → ore mined for the first time; data recorded
///   Reported   → at least one vein submitted to the Survey Archivist; used as the
///                ore-availability gate for extended ores (Platinum → Celestial)
///
/// Per-vein reporting:
///   Every distinct vein location (OreLocationRecord) carries its own Reported flag.
///   Newly logged veins start Reported=false and can be turned in at the Survey
///   Archivist for Standing, Voucher, and Gold rewards even after the ore type's
///   entry.State has been set to Reported.
/// </summary>
public enum DiscoveryState
{
    Discovered = 0,
    Reported   = 1,
}

/// <summary>
/// One mined-vein record: where it is, when it was first logged, and how
/// much ore has been pulled from that specific vein location.
///
/// Two separate mines are considered the same vein if they are within
/// 12 tiles of each other (checked in OreDiscoveryEntry.FindNearbyLocation).
/// </summary>
public class OreLocationRecord
{
    public string   FacetName    { get; }
    public string   RegionName   { get; }
    public Point3D  Location     { get; }
    public DateTime DiscoveredAt { get; }
    public int      AmountMined  { get; set; }

    /// <summary>
    /// Whether this individual vein location has been reported to the Survey Archivist
    /// for Standing/Voucher/Gold rewards. Defaults to false on new veins.
    /// Set to true by SurveyArchivistGump.HandleReportAll().
    /// </summary>
    public bool Reported { get; set; }

    /// <summary>New vein constructor.</summary>
    public OreLocationRecord(string facetName, string regionName, Point3D location)
    {
        FacetName    = facetName;
        RegionName   = regionName;
        Location     = location;
        DiscoveredAt = DateTime.UtcNow;
        AmountMined  = 0;
        Reported     = false;
    }

    /// <summary>Migration constructor — preserves the original discovery timestamp.</summary>
    public OreLocationRecord(string facetName, string regionName, Point3D location, DateTime discoveredAt, int amountMined)
    {
        FacetName    = facetName;
        RegionName   = regionName;
        Location     = location;
        DiscoveredAt = discoveredAt;
        AmountMined  = amountMined;
        Reported     = false;
    }

    /// <summary>
    /// Deserialization constructor.
    /// <paramref name="version"/> is the parent OreDiscoveryEntry's version:
    ///   v2+ reads the Reported flag; v0/v1 default it to false (caller handles migration).
    /// </summary>
    public OreLocationRecord(IGenericReader r, int version)
    {
        FacetName    = r.ReadString();
        RegionName   = r.ReadString();
        Location     = r.ReadPoint3D();
        DiscoveredAt = r.ReadDateTime();
        AmountMined  = r.ReadInt();
        Reported     = version >= 2 && r.ReadBool();
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(FacetName);
        w.Write(RegionName);
        w.Write(Location);
        w.Write(DiscoveredAt);
        w.Write(AmountMined);
        w.Write(Reported); // added in v2
    }
}

public class OreDiscoveryEntry
{
    public string                  OreKey     { get; }
    public List<OreLocationRecord> Locations  { get; } = new();
    public int                     TotalMined { get; set; }
    public DiscoveryState          State      { get; set; }

    /// <summary>Timestamp of the very first location in the list (convenience).</summary>
    public DateTime FirstFound => Locations.Count > 0 ? Locations[0].DiscoveredAt : DateTime.MinValue;

    // ── Construction ──────────────────────────────────────────────────────────

    /// <summary>Brand-new discovery constructor (no locations yet — caller adds them).</summary>
    public OreDiscoveryEntry(string oreKey)
    {
        OreKey     = oreKey;
        TotalMined = 0;
        State      = DiscoveryState.Discovered;
    }

    // ── Location helpers ──────────────────────────────────────────────────────

    /// <summary>Appends a new location record and returns it for immediate mutation.</summary>
    public OreLocationRecord AddLocation(string facetName, string regionName, Point3D location)
    {
        var record = new OreLocationRecord(facetName, regionName, location);
        Locations.Add(record);
        return record;
    }

    /// <summary>
    /// Returns the first existing location within <paramref name="radius"/> tiles of
    /// <paramref name="pt"/> (2-D distance only), or null if none qualifies.
    /// </summary>
    public OreLocationRecord? FindNearbyLocation(Point3D pt, int radius)
    {
        var rSq = radius * radius;
        foreach (var loc in Locations)
        {
            var dx = loc.Location.X - pt.X;
            var dy = loc.Location.Y - pt.Y;
            if (dx * dx + dy * dy <= rSq)
                return loc;
        }
        return null;
    }

    // ── Serialization ─────────────────────────────────────────────────────────

    public OreDiscoveryEntry(IGenericReader r)
    {
        var version = r.ReadInt();

        if (version == 0)
        {
            // ── v0 migration: flat single-location fields ─────────────────────
            OreKey           = r.ReadString();
            var facetName    = r.ReadString();
            var regionName   = r.ReadString();
            var location     = r.ReadPoint3D();
            var firstFound   = r.ReadDateTime();
            TotalMined       = r.ReadInt();
            State            = (DiscoveryState)r.ReadInt();

            // Preserve original timestamp and attribute all mined ore to that location.
            var loc = new OreLocationRecord(facetName, regionName, location, firstFound, TotalMined);
            // If the ore was already reported, mark the location as reported too
            // so it doesn't appear as a new pending turn-in.
            if (State == DiscoveryState.Reported)
                loc.Reported = true;
            Locations.Add(loc);
        }
        else if (version == 1)
        {
            // ── v1 migration: locations without per-vein Reported flag ────────
            OreKey     = r.ReadString();
            TotalMined = r.ReadInt();
            State      = (DiscoveryState)r.ReadInt();

            var count = r.ReadInt();
            for (var i = 0; i < count; i++)
                Locations.Add(new OreLocationRecord(r, 1));

            // If the ore entry was already fully reported, backfill Reported=true
            // on all locations so they don't surface as newly pending veins.
            if (State == DiscoveryState.Reported)
                foreach (var loc in Locations)
                    loc.Reported = true;
        }
        else // version >= 2
        {
            OreKey     = r.ReadString();
            TotalMined = r.ReadInt();
            State      = (DiscoveryState)r.ReadInt();

            var count = r.ReadInt();
            for (var i = 0; i < count; i++)
                Locations.Add(new OreLocationRecord(r, 2));
        }
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(2); // version 2 — adds per-location Reported flag
        w.Write(OreKey);
        w.Write(TotalMined);
        w.Write((int)State);
        w.Write(Locations.Count);
        foreach (var loc in Locations)
            loc.Serialize(w);
    }
}
