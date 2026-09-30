using System;
using System.IO;

namespace Server;

/// <summary>
/// Tracks when a player first encountered an extended wood type during lumberjacking.
///
/// Stored in ClusterFAccountData.WoodDiscoveries, keyed by canonical wood key
/// (e.g. "Ironwood", "Ghostwood", "Starwood").
///
/// Unlike OreDiscoveryEntry there is no per-location tracking — trees are
/// distributed throughout the world so only the type matters, not the spot.
///
/// State lifecycle:
///   Discovered → wood chopped for the first time; regular log substituted.
///   Reported   → player has visited the Foresters' Guildmaster and reported
///                the find; extended wood now drops normally; gates work orders.
/// </summary>
public class WoodDiscoveryEntry
{
    public string         WoodKey      { get; }
    public int            TotalChopped { get; set; }
    public DiscoveryState State        { get; set; }
    public DateTime       FirstFound   { get; }

    public WoodDiscoveryEntry(string woodKey)
    {
        WoodKey      = woodKey;
        TotalChopped = 0;
        State        = DiscoveryState.Discovered;
        FirstFound   = DateTime.UtcNow;
    }

    /// <summary>
    /// Serialization versions:
    ///   v0 — WoodKey + TotalChopped + State + FirstFound. What this file writes.
    ///   v1 — WoodKey + TotalChopped + State + a list of grove locations
    ///        (facet, region, Point3D, DateTime, amount) in place of FirstFound.
    ///        Written only by the 2026-06-29 build, which ran live until
    ///        2026-09-29 and never reached server/customizations (D40). Read
    ///        here and collapsed to v0: FirstFound is the earliest location.
    /// Any other version throws. Never reuse 1 for a different layout.
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
                FirstFound = r.ReadDateTime();
                break;
            case 1:
                var count = r.ReadInt();
                FirstFound = count > 0 ? DateTime.MaxValue : DateTime.MinValue;
                for (var i = 0; i < count; i++)
                {
                    r.ReadString();  // facet
                    r.ReadString();  // region
                    r.ReadPoint3D();
                    var discoveredAt = r.ReadDateTime();
                    r.ReadInt();     // amount chopped at this grove
                    if (discoveredAt < FirstFound)
                        FirstFound = discoveredAt;
                }
                break;
            default:
                throw new InvalidDataException(
                    $"WoodDiscoveryEntry '{WoodKey}' has version {version}; this build reads 0 and 1.");
        }
    }

    public void Serialize(IGenericWriter w)
    {
        // Stays 0: the rollback image (sl-modernuo:rollback-live-20260927) reads v0 too.
        w.Write(0); // version
        w.Write(WoodKey);
        w.Write(TotalChopped);
        w.Write((int)State);
        w.Write(FirstFound);
    }
}
