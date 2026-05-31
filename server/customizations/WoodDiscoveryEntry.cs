using System;

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

    public WoodDiscoveryEntry(IGenericReader r)
    {
        var version  = r.ReadInt();
        WoodKey      = r.ReadString();
        TotalChopped = r.ReadInt();
        State        = (DiscoveryState)r.ReadInt();
        FirstFound   = r.ReadDateTime();
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(0); // version
        w.Write(WoodKey);
        w.Write(TotalChopped);
        w.Write((int)State);
        w.Write(FirstFound);
    }
}
