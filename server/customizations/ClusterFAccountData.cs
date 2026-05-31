using System;
using System.Collections;
using System.Collections.Generic;

namespace Server;

/// <summary>
/// Per-account progression data for Shattered Legacy.
///
/// Holds all account-wide progression state:
///   - Renown (spendable achievement currency)
///   - Achievement points (permanent prestige score, not spendable)
///   - Guild reputation per guild key
///   - Guild currency (scrip) per guild key
///   - Restoration registry (set of unlocked legacy item keys)
///   - Last-seen bulletin ID (for MOTD/Dispatch unread tracking)
///   - Active and completed guild work order entries
///
/// Keyed by account username. Account-wide by design — Shattered Legacy
/// is a single-character shard and all progression belongs to the account.
///
/// Guild keys are short lowercase identifiers:
///   "mining", "smithing", "rangers", "healers", "cartographers",
///   "arcane", "maritime", "mercenary", "tinkers", "tailors", etc.
///
/// Restoration registry keys follow the pattern:
///   "legacy.jacobs_pickaxe", "legacy.hammer_of_hephaestus", etc.
/// </summary>
public class ClusterFAccountData
{
    // ── Currencies ───────────────────────────────────────────────────────
    public int Renown            { get; set; }
    public int AchievementPoints { get; set; }

    // ── Guild systems ────────────────────────────────────────────────────
    public Dictionary<string, int> GuildReputation { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> GuildCurrency   { get; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Guild membership ─────────────────────────────────────────────────
    public HashSet<string> JoinedGuilds { get; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Restoration registry ─────────────────────────────────────────────
    public Dictionary<string, RestorationEntry> RestorationRegistry { get; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Bulletin tracking ────────────────────────────────────────────────
    public int LastSeenBulletinId { get; set; }

    // ── Account flags ─────────────────────────────────────────────────────
    // Generic boolean flags keyed by string (e.g. "league.joined").
    // Use ClusterFLeagueSystem constants -- do not use raw strings directly.
    public HashSet<string>            Flags      { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> FlagValues { get; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Guild work orders (Phase 3) ───────────────────────────────────────────
    public List<WorkOrderEntry> ActiveWorkOrders    { get; } = new();
    public List<WorkOrderEntry> CompletedWorkOrders { get; } = new();

    // ── Ore discoveries (Phase 3) — Prospector's Logbook ─────────────────────
    // Keyed by canonical ore key (e.g. "DullCopper", "Valorite", "Celestial").
    // Iron is excluded by convention — only colored and extended ores are tracked.
    public Dictionary<string, OreDiscoveryEntry> OreDiscoveries { get; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Wood discoveries (Phase 4) — Foresters' Discoveries ──────────────────
    // Keyed by canonical wood key (e.g. "Ironwood", "Ghostwood", "Starwood").
    // Regular and vanilla colored woods excluded — only extended woods tracked.
    public Dictionary<string, WoodDiscoveryEntry> WoodDiscoveries { get; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Imbuing property discoveries — Artificers' Order ─────────────────────
    // Tracks how many times a player has successfully imbued each property using
    // a PropertyEssence. Once the count reaches ImbuePropertyDef.DiscoveryThreshold,
    // the property is "mastered" and no essence is required for future imbues.
    //
    // Keyed by ImbuePropertyDef.Name (e.g. "Hit Chance Increase", "Slayer: Silver").
    public Dictionary<string, int> ImbuingDiscoveries { get; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Smith Commissions (Phase 4C-ii) ──────────────────────────────────────
    // Active single-piece crafting commissions from named adventurers.
    // Separate from BODs; max 3 small + 1 large concurrent.
    public List<SmithCommissionEntry>      SmithCommissions      { get; } = new();
    public List<SmithLargeCommissionEntry> SmithLargeCommissions { get; } = new();


    // ── Creature encounters (bestiary) ───────────────────────────────────────────
    // Records creature type names on first kill (e.g. "Dragon", "Ridgeback", "Drake").
    // Used to gate hunting work orders — parallel to OreDiscoveries for mining orders.
    public HashSet<string> EncounteredCreatures { get; } = new(StringComparer.OrdinalIgnoreCase);

    // ── Fog of war (exploration) ──────────────────────────────────────────────
    // Per-character, per-facet chunk exploration bitmask.
    // Outer key = character serial (uint). Inner array = 6 BitArrays, one per facet.
    // Null inner entry means the facet has never been visited.
    private readonly Dictionary<uint, BitArray?[]> _exploredChunks = new();

    public BitArray GetOrCreateExploration(Serial serial, int facet, int chunkW, int chunkH)
    {
        uint key = (uint)serial;
        if (!_exploredChunks.TryGetValue(key, out var facets))
            _exploredChunks[key] = facets = new BitArray?[6];
        return facets[facet] ??= new BitArray(chunkW * chunkH, false);
    }

    // ── Constructors ─────────────────────────────────────────────────────
    public ClusterFAccountData() { }

    public ClusterFAccountData(IGenericReader r)
    {
        var version = r.ReadInt(); // 0..13

        Renown              = r.ReadInt();
        AchievementPoints   = r.ReadInt();
        LastSeenBulletinId  = r.ReadInt();

        var repCount = r.ReadInt();
        for (var i = 0; i < repCount; i++)
            GuildReputation[r.ReadString()] = r.ReadInt();

        var curCount = r.ReadInt();
        for (var i = 0; i < curCount; i++)
            GuildCurrency[r.ReadString()] = r.ReadInt();

        var regCount = r.ReadInt();
        for (var i = 0; i < regCount; i++)
        {
            if (version < 2)
            {
                // v0/v1 stored a plain HashSet<string> — migrate to RestorationEntry
                var key = r.ReadString();
                RestorationRegistry[key] = new RestorationEntry(key, "legacy_migration");
            }
            else
            {
                var entry = new RestorationEntry(r);
                RestorationRegistry[entry.Key] = entry;
            }
        }

        if (version >= 1)
        {
            var joinCount = r.ReadInt();
            for (var i = 0; i < joinCount; i++)
                JoinedGuilds.Add(r.ReadString());
        }

        if (version >= 3)
        {
            var flagCount = r.ReadInt();
            for (var i = 0; i < flagCount; i++)
                Flags.Add(r.ReadString());

            var fvCount = r.ReadInt();
            for (var i = 0; i < fvCount; i++)
                FlagValues[r.ReadString()] = r.ReadString();
        }

        if (version >= 4)
        {
            var aoCount = r.ReadInt();
            for (var i = 0; i < aoCount; i++)
                ActiveWorkOrders.Add(new WorkOrderEntry(r));

            var coCount = r.ReadInt();
            for (var i = 0; i < coCount; i++)
                CompletedWorkOrders.Add(new WorkOrderEntry(r));
        }

        if (version >= 5)
        {
            var discCount = r.ReadInt();
            for (var i = 0; i < discCount; i++)
            {
                var entry = new OreDiscoveryEntry(r);
                OreDiscoveries[entry.OreKey] = entry;
            }
        }

        if (version >= 6)
        {
            var commCount = r.ReadInt();
            for (var i = 0; i < commCount; i++)
                SmithCommissions.Add(new SmithCommissionEntry(r));
        }

        if (version >= 7)
        {
            var lCount = r.ReadInt();
            for (var i = 0; i < lCount; i++)
                SmithLargeCommissions.Add(new SmithLargeCommissionEntry(r));
        }

        // v9 had a bug: EncounteredCreatures was serialized BEFORE ExploredChunks,
        // but the deserialize expected ExploredChunks first. v10 fixes the order.
        if (version == 9)
        {
            // Read in the order v9 actually wrote them: EC first, then chunks.
            var ecCount = r.ReadInt();
            for (var i = 0; i < ecCount; i++)
                EncounteredCreatures.Add(r.ReadString());

            var charCount = r.ReadInt();
            for (var i = 0; i < charCount; i++)
            {
                var serial = r.ReadUInt();
                var facets = new BitArray?[6];
                for (var f = 0; f < 6; f++)
                {
                    if (r.ReadBool())
                        facets[f] = r.ReadBitArray();
                }
                _exploredChunks[serial] = facets;
            }
        }

        if (version >= 10)
        {
            // v10+: correct order — ExploredChunks then EncounteredCreatures.
            var charCount = r.ReadInt();
            for (var i = 0; i < charCount; i++)
            {
                var serial = r.ReadUInt();
                var facets = new BitArray?[6];
                for (var f = 0; f < 6; f++)
                {
                    if (r.ReadBool())
                        facets[f] = r.ReadBitArray();
                }
                _exploredChunks[serial] = facets;
            }

            var ecCount = r.ReadInt();
            for (var i = 0; i < ecCount; i++)
                EncounteredCreatures.Add(r.ReadString());
        }

        if (version >= 11)
        {
            var wdCount = r.ReadInt();
            for (var i = 0; i < wdCount; i++)
            {
                var entry = new WoodDiscoveryEntry(r);
                WoodDiscoveries[entry.WoodKey] = entry;
            }
        }

        if (version >= 12)
        {
            var idCount = r.ReadInt();
            for (var i = 0; i < idCount; i++)
                ImbuingDiscoveries[r.ReadString()] = r.ReadInt();
        }

        if (version >= 13)
        {
            var orderKey = r.ReadString();
            ActiveArtificerOrderKey   = string.IsNullOrEmpty(orderKey) ? null : orderKey;
            ActiveArtificerItemSerial = r.ReadUInt();
        }

        // v8-only saves (no EncounteredCreatures yet): just read chunks.
        if (version == 8)
        {
            var charCount = r.ReadInt();
            for (var i = 0; i < charCount; i++)
            {
                var serial = r.ReadUInt();
                var facets = new BitArray?[6];
                for (var f = 0; f < 6; f++)
                {
                    if (r.ReadBool())
                        facets[f] = r.ReadBitArray();
                }
                _exploredChunks[serial] = facets;
            }
        }
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(13); // version

        w.Write(Renown);
        w.Write(AchievementPoints);
        w.Write(LastSeenBulletinId);

        w.Write(GuildReputation.Count);
        foreach (var (k, v) in GuildReputation) { w.Write(k); w.Write(v); }

        w.Write(GuildCurrency.Count);
        foreach (var (k, v) in GuildCurrency) { w.Write(k); w.Write(v); }

        w.Write(RestorationRegistry.Count);
        foreach (var entry in RestorationRegistry.Values) entry.Serialize(w);

        w.Write(JoinedGuilds.Count);
        foreach (var key in JoinedGuilds) w.Write(key);

        w.Write(Flags.Count);
        foreach (var f in Flags) w.Write(f);

        w.Write(FlagValues.Count);
        foreach (var (k, v) in FlagValues) { w.Write(k); w.Write(v); }

        w.Write(ActiveWorkOrders.Count);
        foreach (var e in ActiveWorkOrders) e.Serialize(w);

        w.Write(CompletedWorkOrders.Count);
        foreach (var e in CompletedWorkOrders) e.Serialize(w);

        w.Write(OreDiscoveries.Count);
        foreach (var entry in OreDiscoveries.Values) entry.Serialize(w);

        w.Write(SmithCommissions.Count);
        foreach (var e in SmithCommissions) e.Serialize(w);

        w.Write(SmithLargeCommissions.Count);
        foreach (var e in SmithLargeCommissions) e.Serialize(w);

        // v10: correct order — ExploredChunks before EncounteredCreatures.
        // (v9 had these two blocks swapped, which caused a read misalignment on reload.)
        w.Write(_exploredChunks.Count);
        foreach (var (serial, facets) in _exploredChunks)
        {
            w.Write(serial);
            for (var f = 0; f < 6; f++)
            {
                var bits = facets[f];
                if (bits == null)
                    w.Write(false);
                else
                {
                    w.Write(true);
                    w.Write(bits);
                }
            }
        }

        w.Write(EncounteredCreatures.Count);
        foreach (var name in EncounteredCreatures) w.Write(name);

        // v11: wood discoveries
        w.Write(WoodDiscoveries.Count);
        foreach (var entry in WoodDiscoveries.Values) entry.Serialize(w);

        // v12: imbuing property discoveries
        w.Write(ImbuingDiscoveries.Count);
        foreach (var (k, v) in ImbuingDiscoveries) { w.Write(k); w.Write(v); }

        // v13: active artificer work order
        w.Write(ActiveArtificerOrderKey ?? "");
        w.Write(ActiveArtificerItemSerial);
    }

    // ── Guild helpers ─────────────────────────────────────────────────────
    public int  GetReputation(string guild) => GuildReputation.TryGetValue(guild, out var v) ? v : 0;
    public void AddReputation(string guild, int amount) =>
        GuildReputation[guild] = GetReputation(guild) + amount;

    public int  GetCurrency(string guild) => GuildCurrency.TryGetValue(guild, out var v) ? v : 0;
    public bool SpendCurrency(string guild, int amount)
    {
        var have = GetCurrency(guild);
        if (have < amount) return false;
        GuildCurrency[guild] = have - amount;
        return true;
    }
    public void AddCurrency(string guild, int amount) =>
        GuildCurrency[guild] = GetCurrency(guild) + amount;

    // ── Flag helpers ──────────────────────────────────────────────────────
    public bool    HasFlag(string key)                    => Flags.Contains(key);
    public void    SetFlag(string key)                    => Flags.Add(key);
    public void    ClearFlag(string key)                  => Flags.Remove(key);
    public string? GetFlagValue(string key)               => FlagValues.TryGetValue(key, out var v) ? v : null;
    public void    SetFlagValue(string key, string value) => FlagValues[key] = value;

    // ── Restoration registry helpers ─────────────────────────────────────
    // Prefer ClusterFRestorationRegistry for richer API (TryRestore, ClearActiveCopy, etc.)
    public bool HasUnlocked(string key) => RestorationRegistry.ContainsKey(key);

    // ── Creature encounter helpers ────────────────────────────────────────
    public bool HasEncountered(string creatureTypeName) => EncounteredCreatures.Contains(creatureTypeName);

    // ── Imbuing discovery helpers ─────────────────────────────────────────────
    public int  GetDiscoveryCount(string propertyKey) =>
        ImbuingDiscoveries.TryGetValue(propertyKey, out var v) ? v : 0;

    public void IncrementDiscovery(string propertyKey) =>
        ImbuingDiscoveries[propertyKey] = GetDiscoveryCount(propertyKey) + 1;

    public bool IsMastered(string propertyKey, int threshold) =>
        GetDiscoveryCount(propertyKey) >= threshold;

    // ── Artificer work order tracking ─────────────────────────────────────────
    // At most one commission active at a time.  ItemSerial == 0 for combo orders
    // (player crafts the item themselves; no serial to track).
    public string? ActiveArtificerOrderKey   { get; private set; }
    public uint    ActiveArtificerItemSerial { get; private set; }
    public bool    HasActiveArtificerOrder   => ActiveArtificerOrderKey != null;

    public void AcceptArtificerOrder(string key, uint itemSerial)
    {
        ActiveArtificerOrderKey   = key;
        ActiveArtificerItemSerial = itemSerial;
    }

    public void ClearArtificerOrder()
    {
        ActiveArtificerOrderKey   = null;
        ActiveArtificerItemSerial = 0;
    }

    // ── Renown helpers ───────────────────────────────────────────────────
    public bool SpendRenown(int amount)
    {
        if (Renown < amount) return false;
        Renown -= amount;
        return true;
    }
}

/// <summary>
/// Singleton persistence Item that serializes all ClusterFAccountData entries
/// into the world save. One instance lives in the world with no map assigned.
///
/// Access data via:
///   ClusterFAccountPersistence.GetOrCreate(mobile.Account)
///   ClusterFAccountPersistence.Get(mobile.Account)
/// </summary>
public class ClusterFAccountPersistence : Item
{
    private static ClusterFAccountPersistence _instance;

    // All account data keyed by account username (OrdinalIgnoreCase for safety).
    private static readonly Dictionary<string, ClusterFAccountData> _data =
        new(StringComparer.OrdinalIgnoreCase);

    // ── Public API ────────────────────────────────────────────────────────

    /// <summary>Returns existing data or creates a new empty record for the account.</summary>
    public static ClusterFAccountData GetOrCreate(Accounting.IAccount account)
    {
        if (!_data.TryGetValue(account.Username, out var d))
            _data[account.Username] = d = new ClusterFAccountData();
        return d;
    }

    /// <summary>Returns existing data, or null if no record exists yet.</summary>
    public static ClusterFAccountData? Get(Accounting.IAccount account) =>
        _data.TryGetValue(account.Username, out var d) ? d : null;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    public static void Configure()
    {
        // Item creation must happen after world load — not during Configure.
        EventSink.WorldLoad += EnsureExistence;
    }

    private static void EnsureExistence()
    {
        _instance ??= new ClusterFAccountPersistence();
    }

    private ClusterFAccountPersistence() : base(1) => Movable = false;

    public ClusterFAccountPersistence(Serial serial) : base(serial) => _instance = this;

    public override string DefaultName => "ClusterF Account Persistence — Internal";

    // ── Serialization ─────────────────────────────────────────────────────

    public override void Serialize(IGenericWriter w)
    {
        base.Serialize(w);
        w.Write(0); // version

        w.Write(_data.Count);
        foreach (var (username, d) in _data)
        {
            w.Write(username);
            d.Serialize(w);
        }
    }

    public override void Deserialize(IGenericReader r)
    {
        base.Deserialize(r);
        var version = r.ReadInt();

        var count = r.ReadInt();
        for (var i = 0; i < count; i++)
        {
            var username = r.ReadString();
            _data[username] = new ClusterFAccountData(r);
        }
    }
}
