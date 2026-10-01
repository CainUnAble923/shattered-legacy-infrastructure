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
///   - Restoration registry (set of unlocked legacy item keys)
///   - Last-seen bulletin ID (for MOTD/Dispatch unread tracking)
///   - Flags, discoveries (ore, wood, imbuing), encountered creatures
///
/// Keyed by account username. Per character, keyed by character serial inside the account record:
///   - Guild data (CharacterGuildData, v15, cc-P18 F-7): membership, Apprentice marks, reputation,
///     scrip, work orders, smith commissions, the Artificer order
///   - Guild starter records (GuildStarterRecord, v14, cc-P15)
///   - Exploration (fog of war chunks)
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
    // -- Currencies -------------------------------------------------------
    public int Renown            { get; set; }
    public int AchievementPoints { get; set; }

    // -- Guild systems ----------------------------------------------------
    // Membership, reputation, scrip, work orders, commissions and the Artificer order are per
    // character since v15 (cc-P18, F-7): see CharacterGuildData and GetOrCreateGuildData below.

    // -- Restoration registry ---------------------------------------------
    public Dictionary<string, RestorationEntry> RestorationRegistry { get; } = new(StringComparer.OrdinalIgnoreCase);

    // -- Bulletin tracking ------------------------------------------------
    public int LastSeenBulletinId { get; set; }

    // -- Account flags -----------------------------------------------------
    // Generic boolean flags keyed by string (e.g. "league.joined").
    // Use ClusterFLeagueSystem constants -- do not use raw strings directly.
    public HashSet<string>            Flags      { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> FlagValues { get; } = new(StringComparer.OrdinalIgnoreCase);

    // -- Ore discoveries (Phase 3) - Prospector's Logbook ---------------------
    // Keyed by canonical ore key (e.g. "DullCopper", "Valorite", "Celestial").
    // Iron is excluded by convention - only colored and extended ores are tracked.
    public Dictionary<string, OreDiscoveryEntry> OreDiscoveries { get; } = new(StringComparer.OrdinalIgnoreCase);

    // -- Wood discoveries (Phase 4) - Foresters' Discoveries ------------------
    // Keyed by canonical wood key (e.g. "Ironwood", "Ghostwood", "Starwood").
    // Regular and vanilla colored woods excluded - only extended woods tracked.
    public Dictionary<string, WoodDiscoveryEntry> WoodDiscoveries { get; } = new(StringComparer.OrdinalIgnoreCase);

    // -- Imbuing property discoveries - Artificers' Order ---------------------
    // Tracks how many times a player has successfully imbued each property using
    // a PropertyEssence. Once the count reaches ImbuePropertyDef.DiscoveryThreshold,
    // the property is "mastered" and no essence is required for future imbues.
    //
    // Keyed by ImbuePropertyDef.Name (e.g. "Hit Chance Increase", "Slayer: Silver").
    public Dictionary<string, int> ImbuingDiscoveries { get; } = new(StringComparer.OrdinalIgnoreCase);

    // -- Creature encounters (bestiary) -------------------------------------------
    // Records creature type names on first kill (e.g. "Dragon", "Ridgeback", "Drake").
    // Used to gate hunting work orders - parallel to OreDiscoveries for mining orders.
    public HashSet<string> EncounteredCreatures { get; } = new(StringComparer.OrdinalIgnoreCase);

    // -- Fog of war (exploration) ----------------------------------------------
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

    public int ExploredCharacterCount => _exploredChunks.Count;

    /// <summary>Forgets every character's exploration on this account (the reset, cc-P18).</summary>
    public void ClearExploration() => _exploredChunks.Clear();

    // -- Guild starter path (v14, cc-P15) ---------------------------------------
    // Per character, keyed by character serial like _exploredChunks: which guilds' tools and which
    // New Haven starter items this character has taken, and whether it has seen the first-login
    // welcome. Membership is per character too since v15 (CharacterGuildData). "Once per loop" is once
    // per character until loops exist, so clearing a character's record starts its loop again.
    private readonly Dictionary<uint, GuildStarterRecord> _guildStarter = new();

    public GuildStarterRecord GetOrCreateGuildStarter(Serial serial)
    {
        var key = (uint)serial;
        if (!_guildStarter.TryGetValue(key, out var record))
            _guildStarter[key] = record = new GuildStarterRecord();
        return record;
    }

    public GuildStarterRecord? GetGuildStarter(Serial serial) =>
        _guildStarter.TryGetValue((uint)serial, out var record) ? record : null;

    public int GuildStarterRecordCount => _guildStarter.Count;

    public void ClearGuildStarterRecords() => _guildStarter.Clear();

    // -- Guild data per character (v15, cc-P18, F-7) ---------------------------
    // Keyed by character serial like _exploredChunks, so two characters on one account are in
    // different guilds with separate reputation and scrip. Chase, 2026-09-30: per character "for now";
    // F-4 (one character per account) may fold it back. To fold it back, key every character of an
    // account to one record here (for example serial 0); no call site needs to change.
    private readonly Dictionary<uint, CharacterGuildData> _guildData = new();

    public CharacterGuildData GetOrCreateGuildData(Serial serial)
    {
        var key = (uint)serial;
        if (!_guildData.TryGetValue(key, out var record))
            _guildData[key] = record = new CharacterGuildData();
        return record;
    }

    public CharacterGuildData? GetGuildData(Serial serial) =>
        _guildData.TryGetValue((uint)serial, out var record) ? record : null;

    public int GuildDataCount => _guildData.Count;

    /// <summary>Every character's guild data on this account, by character serial. Read only (cc-P23).</summary>
    public IReadOnlyDictionary<uint, CharacterGuildData> AllGuildData => _guildData;

    /// <summary>Clears every character's guild data on this account (the reset, cc-P18).</summary>
    public void ClearGuildData() => _guildData.Clear();

    /// <summary>
    /// True when this record was read from a save before v15 that held account-level guild data
    /// (membership, Apprentice marks, reputation, scrip, work orders, commissions or an Artificer
    /// order). That data was dropped, not given to a character: the save does not say which character
    /// earned it (cc-P18, Chase's decision). Not saved; ClusterFAccountPersistence logs it once.
    /// </summary>
    public bool DroppedAccountGuildData { get; private set; }

    // -- Constructors -----------------------------------------------------
    public ClusterFAccountData() { }

    public ClusterFAccountData(IGenericReader r)
    {
        var version = r.ReadInt(); // 0..15

        // D40 (cc-P11): a version this reader does not know is a save from a newer build. Reading it as
        // this version misreads every field after the first difference, so refuse it loudly instead.
        if (version > CurrentVersion)
            throw new System.IO.InvalidDataException(
                $"ClusterFAccountData version {version} is newer than this build reads (up to {CurrentVersion}).");

        Renown              = r.ReadInt();
        AchievementPoints   = r.ReadInt();
        LastSeenBulletinId  = r.ReadInt();

        // Before v15 the account held one copy of the guild data. It is read to stay aligned with the
        // bytes and then dropped (cc-P18): the save does not say which character it belonged to.
        var legacy = new CharacterGuildData();

        if (version < 15)
        {
            var repCount = r.ReadInt();
            for (var i = 0; i < repCount; i++)
                legacy.GuildReputation[r.ReadString()] = r.ReadInt();

            var curCount = r.ReadInt();
            for (var i = 0; i < curCount; i++)
                legacy.GuildCurrency[r.ReadString()] = r.ReadInt();
        }

        var regCount = r.ReadInt();
        for (var i = 0; i < regCount; i++)
        {
            if (version < 2)
            {
                // v0/v1 stored a plain HashSet<string> - migrate to RestorationEntry
                var key = r.ReadString();
                RestorationRegistry[key] = new RestorationEntry(key, "legacy_migration");
            }
            else
            {
                var entry = new RestorationEntry(r);
                RestorationRegistry[entry.Key] = entry;
            }
        }

        if (version is >= 1 and < 15)
        {
            var joinCount = r.ReadInt();
            for (var i = 0; i < joinCount; i++)
                legacy.JoinedGuilds.Add(r.ReadString());
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

        if (version is >= 4 and < 15)
        {
            var aoCount = r.ReadInt();
            for (var i = 0; i < aoCount; i++)
                legacy.ActiveWorkOrders.Add(new WorkOrderEntry(r));

            var coCount = r.ReadInt();
            for (var i = 0; i < coCount; i++)
                legacy.CompletedWorkOrders.Add(new WorkOrderEntry(r));
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

        if (version is >= 6 and < 15)
        {
            var commCount = r.ReadInt();
            for (var i = 0; i < commCount; i++)
                legacy.SmithCommissions.Add(new SmithCommissionEntry(r));
        }

        if (version is >= 7 and < 15)
        {
            var lCount = r.ReadInt();
            for (var i = 0; i < lCount; i++)
                legacy.SmithLargeCommissions.Add(new SmithLargeCommissionEntry(r));
        }

        // v9 had a bug: EncounteredCreatures was serialized BEFORE ExploredChunks,
        // but the deserialize expected ExploredChunks first. v10 fixes the order.
        if (version == 9)
        {
            // Read in the order v9 actually wrote them: EC first, then chunks.
            var ecCount = r.ReadInt();
            for (var i = 0; i < ecCount; i++)
                EncounteredCreatures.Add(r.ReadString());

            ReadExploration(r);
        }

        if (version >= 10)
        {
            // v10+: correct order - ExploredChunks then EncounteredCreatures.
            ReadExploration(r);

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

        if (version is >= 13 and < 15)
        {
            var orderKey   = r.ReadString();
            var itemSerial = r.ReadUInt();
            if (!string.IsNullOrEmpty(orderKey))
                legacy.AcceptArtificerOrder(orderKey, itemSerial);
        }

        if (version >= 14)
        {
            var gsCount = r.ReadInt();
            for (var i = 0; i < gsCount; i++)
            {
                var serial = r.ReadUInt();
                _guildStarter[serial] = new GuildStarterRecord(r);
            }
        }

        if (version >= 15)
        {
            var gdCount = r.ReadInt();
            for (var i = 0; i < gdCount; i++)
            {
                var serial = r.ReadUInt();
                _guildData[serial] = new CharacterGuildData(r);
            }
        }

        // v8-only saves (no EncounteredCreatures yet): just read chunks.
        if (version == 8)
            ReadExploration(r);

        // The Apprentice marks were account flags before v15. They are guild data, dropped with it.
        var apprenticeFlags = Flags.RemoveWhere(
            f => f.StartsWith(LegacyApprenticeFlagPrefix, StringComparison.OrdinalIgnoreCase));

        DroppedAccountGuildData = !legacy.IsEmpty || apprenticeFlags > 0;
    }

    // The account flag prefix that marked a finished Apprentice task before v15.
    public const string LegacyApprenticeFlagPrefix = "guild.apprentice.";

    private void ReadExploration(IGenericReader r)
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

    public const int CurrentVersion = 15;

    public void Serialize(IGenericWriter w)
    {
        w.Write(CurrentVersion);

        w.Write(Renown);
        w.Write(AchievementPoints);
        w.Write(LastSeenBulletinId);

        // v15: guild reputation and currency moved to CharacterGuildData.

        w.Write(RestorationRegistry.Count);
        foreach (var entry in RestorationRegistry.Values) entry.Serialize(w);

        // v15: joined guilds moved to CharacterGuildData.

        w.Write(Flags.Count);
        foreach (var f in Flags) w.Write(f);

        w.Write(FlagValues.Count);
        foreach (var (k, v) in FlagValues) { w.Write(k); w.Write(v); }

        // v15: work orders moved to CharacterGuildData.

        w.Write(OreDiscoveries.Count);
        foreach (var entry in OreDiscoveries.Values) entry.Serialize(w);

        // v15: smith commissions moved to CharacterGuildData.

        // v10: correct order - ExploredChunks before EncounteredCreatures.
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

        // v13's active artificer work order moved to CharacterGuildData in v15.

        // v14: per-character guild starter records
        w.Write(_guildStarter.Count);
        foreach (var (serial, record) in _guildStarter)
        {
            w.Write(serial);
            record.Serialize(w);
        }

        // v15: per-character guild data
        w.Write(_guildData.Count);
        foreach (var (serial, record) in _guildData)
        {
            w.Write(serial);
            record.Serialize(w);
        }
    }

    // -- Flag helpers ------------------------------------------------------
    public bool    HasFlag(string key)                    => Flags.Contains(key);
    public void    SetFlag(string key)                    => Flags.Add(key);
    public void    ClearFlag(string key)                  => Flags.Remove(key);
    public string? GetFlagValue(string key)               => FlagValues.TryGetValue(key, out var v) ? v : null;
    public void    SetFlagValue(string key, string value) => FlagValues[key] = value;

    // -- Restoration registry helpers -------------------------------------
    // Prefer ClusterFRestorationRegistry for richer API (TryRestore, ClearActiveCopy, etc.)
    public bool HasUnlocked(string key) => RestorationRegistry.ContainsKey(key);

    // -- Creature encounter helpers ----------------------------------------
    public bool HasEncountered(string creatureTypeName) => EncounteredCreatures.Contains(creatureTypeName);

    // -- Imbuing discovery helpers ---------------------------------------------
    public int  GetDiscoveryCount(string propertyKey) =>
        ImbuingDiscoveries.TryGetValue(propertyKey, out var v) ? v : 0;

    public void IncrementDiscovery(string propertyKey) =>
        ImbuingDiscoveries[propertyKey] = GetDiscoveryCount(propertyKey) + 1;

    public bool IsMastered(string propertyKey, int threshold) =>
        GetDiscoveryCount(propertyKey) >= threshold;

    // -- Renown helpers ---------------------------------------------------
    public bool SpendRenown(int amount)
    {
        if (Renown < amount) return false;
        Renown -= amount;
        return true;
    }
}

/// <summary>
/// One character's guild data (cc-P18, F-7): membership, Apprentice marks, reputation, scrip, work
/// orders, smith commissions and the Artificer order. Stored in ClusterFAccountData v15, keyed by
/// character serial like the exploration chunks and GuildStarterRecord. Until v15 all of this was one
/// copy per account, so a second character on an account was already in its first character's guilds.
///
/// The member names are the ones ClusterFAccountData had, so a call site changes from the account
/// record to this one and nothing else. Reach it with ClusterFAccountPersistence.GetOrCreateGuild(m).
///
/// Carries its own version so a later field can be added without touching the account record's
/// reader; an unknown version fails loudly (the D40 rule).
/// </summary>
public sealed class CharacterGuildData
{
    public const int CurrentVersion = 0;

    // -- Membership ---------------------------------------------------------
    public HashSet<string> JoinedGuilds { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Guild keys whose Apprentice task this character has finished (the account flag
    // "guild.apprentice.<key>" before v15).
    public HashSet<string> ApprenticeGuilds { get; } = new(StringComparer.OrdinalIgnoreCase);

    // -- Standing and scrip -------------------------------------------------
    public Dictionary<string, int> GuildReputation { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> GuildCurrency   { get; } = new(StringComparer.OrdinalIgnoreCase);

    // -- Guild work orders (Phase 3) ---------------------------------------
    public List<WorkOrderEntry> ActiveWorkOrders    { get; } = new();
    public List<WorkOrderEntry> CompletedWorkOrders { get; } = new();

    // -- Smith Commissions (Phase 4C-ii) -----------------------------------
    // Active single-piece crafting commissions from named adventurers.
    // Separate from BODs; max 3 small + 1 large concurrent.
    public List<SmithCommissionEntry>      SmithCommissions      { get; } = new();
    public List<SmithLargeCommissionEntry> SmithLargeCommissions { get; } = new();

    // -- Artificer work order ----------------------------------------------
    // At most one commission active at a time.  ItemSerial == 0 for combo orders
    // (player crafts the item themselves; no serial to track).
    public string? ActiveArtificerOrderKey   { get; private set; }
    public uint    ActiveArtificerItemSerial { get; private set; }
    public bool    HasActiveArtificerOrder   => ActiveArtificerOrderKey != null;

    public bool IsEmpty =>
        JoinedGuilds.Count == 0 && ApprenticeGuilds.Count == 0 &&
        GuildReputation.Count == 0 && GuildCurrency.Count == 0 &&
        ActiveWorkOrders.Count == 0 && CompletedWorkOrders.Count == 0 &&
        SmithCommissions.Count == 0 && SmithLargeCommissions.Count == 0 &&
        !HasActiveArtificerOrder;

    public CharacterGuildData() { }

    public CharacterGuildData(IGenericReader r)
    {
        var version = r.ReadInt();
        if (version != 0)
            throw new System.IO.InvalidDataException(
                $"CharacterGuildData version {version} is not one this build reads (0).");

        var joined = r.ReadInt();
        for (var i = 0; i < joined; i++)
            JoinedGuilds.Add(r.ReadString());

        var apprentice = r.ReadInt();
        for (var i = 0; i < apprentice; i++)
            ApprenticeGuilds.Add(r.ReadString());

        var rep = r.ReadInt();
        for (var i = 0; i < rep; i++)
            GuildReputation[r.ReadString()] = r.ReadInt();

        var cur = r.ReadInt();
        for (var i = 0; i < cur; i++)
            GuildCurrency[r.ReadString()] = r.ReadInt();

        var active = r.ReadInt();
        for (var i = 0; i < active; i++)
            ActiveWorkOrders.Add(new WorkOrderEntry(r));

        var completed = r.ReadInt();
        for (var i = 0; i < completed; i++)
            CompletedWorkOrders.Add(new WorkOrderEntry(r));

        var small = r.ReadInt();
        for (var i = 0; i < small; i++)
            SmithCommissions.Add(new SmithCommissionEntry(r));

        var large = r.ReadInt();
        for (var i = 0; i < large; i++)
            SmithLargeCommissions.Add(new SmithLargeCommissionEntry(r));

        var orderKey = r.ReadString();
        ActiveArtificerOrderKey   = string.IsNullOrEmpty(orderKey) ? null : orderKey;
        ActiveArtificerItemSerial = r.ReadUInt();
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(CurrentVersion);

        w.Write(JoinedGuilds.Count);
        foreach (var key in JoinedGuilds) w.Write(key);

        w.Write(ApprenticeGuilds.Count);
        foreach (var key in ApprenticeGuilds) w.Write(key);

        w.Write(GuildReputation.Count);
        foreach (var (k, v) in GuildReputation) { w.Write(k); w.Write(v); }

        w.Write(GuildCurrency.Count);
        foreach (var (k, v) in GuildCurrency) { w.Write(k); w.Write(v); }

        w.Write(ActiveWorkOrders.Count);
        foreach (var e in ActiveWorkOrders) e.Serialize(w);

        w.Write(CompletedWorkOrders.Count);
        foreach (var e in CompletedWorkOrders) e.Serialize(w);

        w.Write(SmithCommissions.Count);
        foreach (var e in SmithCommissions) e.Serialize(w);

        w.Write(SmithLargeCommissions.Count);
        foreach (var e in SmithLargeCommissions) e.Serialize(w);

        w.Write(ActiveArtificerOrderKey ?? "");
        w.Write(ActiveArtificerItemSerial);
    }

    // -- Guild helpers -----------------------------------------------------
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
}

/// <summary>
/// One character's progress on the guild starter path (cc-P15, F-9). Stored in ClusterFAccountData
/// v14, keyed by character serial. Carries its own version so a later field can be added without
/// touching the account record's reader; an unknown version fails loudly (the D40 rule).
/// </summary>
public sealed class GuildStarterRecord
{
    public const int CurrentVersion = 0;

    // Guild keys whose joining tools (creation kit plus join bonus) this character has taken.
    public HashSet<string> ToolsTaken { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Starter items this character has taken from a guild, by the name of the New Haven quest that
    // also gives them (e.g. "EnGuarde"). A quest turned in to a trainer is not recorded here; its
    // MLQuest done-record is the other half of the one ledger.
    public HashSet<string> ItemsTaken { get; } = new(StringComparer.Ordinal);

    public bool WelcomeShown { get; set; }

    public GuildStarterRecord() { }

    public GuildStarterRecord(IGenericReader r)
    {
        var version = r.ReadInt();
        if (version != 0)
            throw new System.IO.InvalidDataException(
                $"GuildStarterRecord version {version} is not one this build reads (0).");

        var tools = r.ReadInt();
        for (var i = 0; i < tools; i++)
            ToolsTaken.Add(r.ReadString());

        var items = r.ReadInt();
        for (var i = 0; i < items; i++)
            ItemsTaken.Add(r.ReadString());

        WelcomeShown = r.ReadBool();
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(CurrentVersion);

        w.Write(ToolsTaken.Count);
        foreach (var key in ToolsTaken) w.Write(key);

        w.Write(ItemsTaken.Count);
        foreach (var key in ItemsTaken) w.Write(key);

        w.Write(WelcomeShown);
    }
}

/// <summary>
/// Singleton persistence Item that serializes all ClusterFAccountData entries
/// into the world save. One instance lives in the world with no map assigned.
///
/// Access data via:
///   ClusterFAccountPersistence.GetOrCreate(mobile.Account)
///   ClusterFAccountPersistence.Get(mobile.Account)
///   ClusterFAccountPersistence.GetOrCreateGuild(mobile)   (one character's guild data)
/// </summary>
public class ClusterFAccountPersistence : Item
{
    private static ClusterFAccountPersistence _instance;

    // All account data keyed by account username (OrdinalIgnoreCase for safety).
    private static readonly Dictionary<string, ClusterFAccountData> _data =
        new(StringComparer.OrdinalIgnoreCase);

    // -- Public API --------------------------------------------------------

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

    /// <summary>Every account's record, by username. Read only: for reports (cc-P23).</summary>
    public static IReadOnlyDictionary<string, ClusterFAccountData> All => _data;

    /// <summary>
    /// This character's guild data (cc-P18, F-7), created if missing. The character must have an
    /// account, as for GetOrCreate(IAccount).
    /// </summary>
    public static CharacterGuildData GetOrCreateGuild(Mobile m) =>
        GetOrCreate(m.Account).GetOrCreateGuildData(m.Serial);

    /// <summary>This character's guild data, or null if it has no account or no record yet.</summary>
    public static CharacterGuildData? GetGuild(Mobile m) =>
        m.Account is { } acct ? Get(acct)?.GetGuildData(m.Serial) : null;

    /// <summary>This character's guild starter record (cc-P15), or null if it has none.</summary>
    public static GuildStarterRecord? GetGuildStarter(Mobile m) =>
        m.Account is { } acct ? Get(acct)?.GetGuildStarter(m.Serial) : null;

    // -- Lifecycle ---------------------------------------------------------

    public static void Configure()
    {
        // Item creation must happen after world load - not during Configure.
        EventSink.WorldLoad += EnsureExistence;
    }

    private static void EnsureExistence()
    {
        _instance ??= new ClusterFAccountPersistence();
    }

    private ClusterFAccountPersistence() : base(1) => Movable = false;

    public ClusterFAccountPersistence(Serial serial) : base(serial) => _instance = this;

    public override string DefaultName => "ClusterF Account Persistence - Internal";

    // -- Serialization -----------------------------------------------------

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

        var dropped = 0;
        var count = r.ReadInt();
        for (var i = 0; i < count; i++)
        {
            var username = r.ReadString();
            var d = new ClusterFAccountData(r);
            _data[username] = d;
            if (d.DroppedAccountGuildData)
                dropped++;
        }

        // cc-P18 (F-7): guild data became per character in ClusterFAccountData v15. An older save's
        // account-level guild data is cleared, not guessed onto a character. Say so once.
        if (dropped > 0)
            Console.WriteLine(
                $"[ClusterFAccountPersistence] Cleared account-level guild data on {dropped} account(s): " +
                "guild membership, reputation and scrip are per character from this build (cc-P18).");
    }
}
