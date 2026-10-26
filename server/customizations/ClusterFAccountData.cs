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
///   - League rank, promotion jobs and trials done (CharacterLeagueData, v16, cc-P48)
///
/// Account-wide again: learned craft recipes (LearnedRecipes, v17, cc-P78 F-43): band books, single recipes and the
/// Masonry and Glassblowing books, kept through loops and character changes (ClusterFRecipeStore).
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

    // Every Renown this account has earned that counts toward League rank (v16, cc-P48). It only rises:
    // spending Renown never lowers it, so buying from a shop never costs rank. Fed by GrantRenown; a save
    // before v16 starts it at the account's Renown then.
    public int LifetimeRenown    { get; set; }

    // Learned craft recipes, account-wide (v17, cc-P78 F-43): keys from ClusterFRecipeStore ("book:Blacksmith:2",
    // "recipe:12", "item:Blacksmith:PlatinumKryss", "flag:Masonry"). Never cleared by a loop.
    public HashSet<string> LearnedRecipes { get; } = new(StringComparer.Ordinal);

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

    // -- League rank per character (v16, cc-P48) --------------------------------
    // Keyed by character serial like the guild data, and kept apart from it so a guild reset never touches a
    // League rank. Registration stays the account flag "league.joined": a character with no record here on a
    // registered account is Iron (ClusterFLeagueRanks.GetRank), which is how a pre-v16 save reads.
    private readonly Dictionary<uint, CharacterLeagueData> _leagueData = new();

    public CharacterLeagueData GetOrCreateLeagueData(Serial serial)
    {
        var key = (uint)serial;
        if (!_leagueData.TryGetValue(key, out var record))
            _leagueData[key] = record = new CharacterLeagueData();
        return record;
    }

    public CharacterLeagueData? GetLeagueData(Serial serial) =>
        _leagueData.TryGetValue((uint)serial, out var record) ? record : null;

    public int LeagueDataCount => _leagueData.Count;

    /// <summary>Clears every character's League rank record on this account (the reset's "League, flags").</summary>
    public void ClearLeagueData() => _leagueData.Clear();

    // -- Per character: loot box firsts and fanfare mute (v18, cc-P79 F-40 / F-41) --
    private readonly Dictionary<uint, CharacterLootRecord> _lootData = new();

    public CharacterLootRecord GetOrCreateLootRecord(Serial serial)
    {
        var key = (uint)serial;
        if (!_lootData.TryGetValue(key, out var record))
            _lootData[key] = record = new CharacterLootRecord();
        return record;
    }

    public CharacterLootRecord? GetLootRecord(Serial serial) =>
        _lootData.TryGetValue((uint)serial, out var record) ? record : null;

    public int LootRecordCount => _lootData.Count;

    public IEnumerable<CharacterLootRecord> LootRecords => _lootData.Values;

    // -- Requisitions per character (v20, cc-P150, F-47 / F-48) --------------------------------------
    // Per character like the guild data, because standing (and so the tier) is per character.
    private readonly Dictionary<uint, CharacterRequisitionData> _requisitionData = new();

    public CharacterRequisitionData GetOrCreateRequisitionData(Serial serial)
    {
        var key = (uint)serial;
        if (!_requisitionData.TryGetValue(key, out var record))
            _requisitionData[key] = record = new CharacterRequisitionData();
        return record;
    }

    public CharacterRequisitionData? GetRequisitionData(Serial serial) =>
        _requisitionData.TryGetValue((uint)serial, out var record) ? record : null;

    public int RequisitionDataCount => _requisitionData.Count;

    /// <summary>Drops every character's requisitions (the test wipe, with the guild data they are keyed to).</summary>
    public void ClearRequisitionData() => _requisitionData.Clear();

    // -- The New Haven tutorial, once per account (v19, cc-P80) --------------
    public TutorialRecord Tutorial { get; private set; } = new();

    public void ResetTutorial() => Tutorial = new TutorialRecord();

    // -- League titles, once per account (v21, cc-P229 F-13) ---------------
    // Titles bought in the League shop or unlocked by AP, and the one each character shows. LeagueTitles reads it.
    public LeagueTitleRecord LeagueTitles { get; private set; } = new();

    // -- Mined blocks, once per account (v22, cc-P238 F-36) ------------------
    // Every 8 x 8 ore bank the account has mined, with when and which ores: the Prospector's Logbook's "blocks mined",
    // and the climbing boots' gate. Written by ClusterFMinedBlocks (from Mining.Give).
    public MinedBlockRecord MinedBlocks { get; private set; } = new();

    // -- The League Echo, once per account (v23, cc-P630) -------------------
    // When the account may call its next Echo (LeagueEchoSystem): an absolute UTC time, set on a successful call.
    // DateTime.MinValue = ready. Shared by every character and Tome on the account.
    public DateTime LeagueEchoNextUseUtc { get; set; } = DateTime.MinValue;

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
        var version = r.ReadInt(); // 0..16

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

        // v16 (cc-P48): lifetime Renown, then each character's League record. Before v16 there was no
        // lifetime figure, so it starts at the Renown the account holds (nothing had ever been spent:
        // SpendRenown had no callers). A registered account's characters read as Iron with no record.
        if (version >= 16)
        {
            LifetimeRenown = r.ReadInt();

            var ldCount = r.ReadInt();
            for (var i = 0; i < ldCount; i++)
            {
                var serial = r.ReadUInt();
                _leagueData[serial] = new CharacterLeagueData(r);
            }
        }
        else
        {
            LifetimeRenown = Renown;
        }

        // v17 (cc-P78, F-43): the account's learned recipes, by key (ClusterFRecipeStore). Before v17 there were none
        // on the account; pinned's per-character recipes and the Masonry and Glassblowing flags are copied in when each
        // character next logs in or crafts (ClusterFRecipeStore.Sync).
        if (version >= 17)
        {
            var recipeCount = r.ReadInt();
            for (var i = 0; i < recipeCount; i++)
                LearnedRecipes.Add(r.ReadString());
        }

        // v18 (cc-P79, F-40 / F-41): each character's loot box firsts (source and loops) and fanfare mute.
        if (version >= 18)
        {
            var lootCount = r.ReadInt();
            for (var i = 0; i < lootCount; i++)
            {
                var serial = r.ReadUInt();
                _lootData[serial] = new CharacterLootRecord(r);
            }
        }

        // v19 (cc-P80): the account's tutorial record. Before v19 no account had started it.
        if (version >= 19)
            Tutorial = new TutorialRecord(r);

        // v20 (cc-P150, F-47 / F-48): each character's requisitions. Before v20 no character had a board; each rolls
        // its first set when it next reads one.
        if (version >= 20)
        {
            var reqCount = r.ReadInt();
            for (var i = 0; i < reqCount; i++)
            {
                var serial = r.ReadUInt();
                _requisitionData[serial] = new CharacterRequisitionData(r);
            }
        }

        // v21 (cc-P229, F-13): the account's League titles. Before v21 no account held one; AP titles are unlocked the
        // next time the account's titles are read (LeagueTitles.Sync).
        if (version >= 21)
            LeagueTitles = new LeagueTitleRecord(r);

        // v22 (cc-P238, F-36): the blocks the account has mined. Before v22 none were recorded; the count starts at 0.
        if (version >= 22)
            MinedBlocks = new MinedBlockRecord(r);

        // v23 (cc-P630): the League Echo's next use. Before v23 no account had called one: ready.
        if (version >= 23)
            LeagueEchoNextUseUtc = r.ReadDateTime();

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

    // 15: cc-P18, guild data per character. 16: cc-P48, LifetimeRenown and CharacterLeagueData.
    // 17: cc-P78, LearnedRecipes. 18: cc-P79, CharacterLootRecord per character. 19: cc-P80, TutorialRecord.
    // 20: cc-P150, CharacterRequisitionData per character. 21: cc-P229, LeagueTitleRecord. 22: cc-P238, MinedBlockRecord.
    // 23: cc-P630, LeagueEchoNextUseUtc.
    public const int CurrentVersion = 23;

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

        // v16: lifetime Renown and per-character League records
        w.Write(LifetimeRenown);
        w.Write(_leagueData.Count);
        foreach (var (serial, record) in _leagueData)
        {
            w.Write(serial);
            record.Serialize(w);
        }

        // v17: learned recipes
        w.Write(LearnedRecipes.Count);
        foreach (var key in LearnedRecipes)
            w.Write(key);

        // v18: loot box records
        w.Write(_lootData.Count);
        foreach (var (serial, record) in _lootData)
        {
            w.Write(serial);
            record.Serialize(w);
        }

        // v19: the tutorial record
        Tutorial.Serialize(w);

        // v20: requisitions per character
        w.Write(_requisitionData.Count);
        foreach (var (serial, record) in _requisitionData)
        {
            w.Write(serial);
            record.Serialize(w);
        }

        // v21: League titles
        LeagueTitles.Serialize(w);

        // v22: mined blocks
        MinedBlocks.Serialize(w);

        // v23: the League Echo's next use
        w.Write(LeagueEchoNextUseUtc);
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

    /// <summary>
    /// The one way Renown is earned (cc-P48). Adds to the spendable balance, and to LifetimeRenown unless the
    /// source is excluded from League rank. Batch 2's League jobs call it with LeagueJob; batch 4's Citizen
    /// Commissions must call it with Commission, which never counts: Renown passed between players would
    /// otherwise buy League rank (cc-P47 section 7.4).
    /// </summary>
    public void GrantRenown(int amount, RenownSource source)
    {
        if (amount <= 0) return;

        Renown += amount;

        if (CountsTowardLeagueRank(source))
            LifetimeRenown += amount;
    }

    public static bool CountsTowardLeagueRank(RenownSource source) => source != RenownSource.Commission;

    /// <summary>Spends from the balance only. LifetimeRenown, and so League rank, never goes down.</summary>
    public bool SpendRenown(int amount)
    {
        if (Renown < amount) return false;
        Renown -= amount;
        return true;
    }
}

/// <summary>Where a Renown grant came from (cc-P48). Only Commission is kept out of LifetimeRenown.</summary>
public enum RenownSource
{
    Achievement,
    LeagueJob,   // batch 2
    Commission,  // batch 4: escrowed Renown paid by another player; never counts toward rank
    Staff,       // a GM grant
}

/// <summary>
/// One character's League record (cc-P48): rank on the 17-rank metal ladder (ClusterFLeagueRanks), the ranks whose
/// promotion job is done, and the trial chapters done. Stored in ClusterFAccountData v16 keyed by character serial.
/// Carries its own version; an unknown version fails loudly (the D40 rule).
/// </summary>
public sealed class CharacterLeagueData
{
    // v1 (cc-P69): the Compact Mule errand's stage and whether this character was granted its mule.
    // v2 (cc-P280): the League Register: its bound guild keys and the serial of the copy in force; the backpack tier.
    // v3 (cc-P630): the serial of the Tome of Echoes in force.
    public const int CurrentVersion = 3;

    // 0 = not promoted past what registration gives (Iron on a registered account); 1 = Iron ... 17 = Celestial.
    public int Rank { get; set; }

    // Rank indexes whose promotion job this character has turned in (batch 2 sets these).
    public HashSet<int> PromotionDone { get; } = new();

    // Story trial chapters this character has finished (1 to 6).
    public HashSet<int> TrialsDone { get; } = new();

    // cc-P69 (F-33): the Compact Mule errand (MuleErrand): 0 not taken, 1 taken, 2 delivered (mule waiting), 3 done.
    public int MuleErrandStage { get; set; }

    // The errand's mule was handed over (one per character; a lost one is replaced, for a fee, not re-earned).
    public bool MuleGranted { get; set; }

    // cc-P280: the guilds bound into this character's League Register (their guild books were consumed).
    public HashSet<string> RegisterGuilds { get; } = new(StringComparer.Ordinal);

    // cc-P280: the serial of the League Register in force; an older copy (re-issued) stops working. 0 = none issued.
    public uint RegisterSerial { get; set; }

    // cc-P280: backpack upgrades bought, 0 (none, 125 items) to LeagueBackpack.Tiers.
    public int PackTier { get; set; }

    // cc-P630: the serial of the Tome of Echoes in force; an older copy (re-issued) stops working. 0 = none issued.
    public uint EchoTomeSerial { get; set; }

    public CharacterLeagueData() { }

    public CharacterLeagueData(IGenericReader r)
    {
        var version = r.ReadInt();
        if (version < 0 || version > CurrentVersion)
            throw new System.IO.InvalidDataException(
                $"CharacterLeagueData version {version} is not one this build reads (0 to {CurrentVersion}).");

        Rank = r.ReadInt();

        var jobs = r.ReadInt();
        for (var i = 0; i < jobs; i++)
            PromotionDone.Add(r.ReadInt());

        var trials = r.ReadInt();
        for (var i = 0; i < trials; i++)
            TrialsDone.Add(r.ReadInt());

        if (version >= 1)
        {
            MuleErrandStage = r.ReadInt();
            MuleGranted = r.ReadBool();
        }

        if (version >= 2)
        {
            var guilds = r.ReadInt();
            for (var i = 0; i < guilds; i++)
                RegisterGuilds.Add(r.ReadString());

            RegisterSerial = r.ReadUInt();
            PackTier = r.ReadInt();
        }

        if (version >= 3)
            EchoTomeSerial = r.ReadUInt();
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(CurrentVersion);
        w.Write(Rank);

        w.Write(PromotionDone.Count);
        foreach (var rank in PromotionDone) w.Write(rank);

        w.Write(TrialsDone.Count);
        foreach (var chapter in TrialsDone) w.Write(chapter);

        w.Write(MuleErrandStage);
        w.Write(MuleGranted);

        w.Write(RegisterGuilds.Count);
        foreach (var key in RegisterGuilds) w.Write(key);

        w.Write(RegisterSerial);
        w.Write(PackTier);

        w.Write(EchoTomeSerial);
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
    // 0: cc-P18. 1: cc-P46 Part B, SmithTeachingOrders. 2: cc-P55 Part H, SmithTurnIn.
    // 3: cc-P78 Part E, RankCap, PromotedRank, TrialsPassed.
    public const int CurrentVersion = 3;

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

    // -- Society of Smiths: orders that still teach (v1, cc-P46 Part B) -----
    // On (the default): the Society's bulk orders and commissions ask for items that can still raise this
    // character's Blacksmithy (ClusterFSmithTeaching). Off: any item the character can make. A v0 record reads as on.
    public bool SmithTeachingOrders { get; set; } = true;

    // -- Society of Smiths: bank or cash out at turn-in (v2, cc-P55 Part H) -
    // Bank (the default) pays the deed into Smithing Seals; CashOut pays gold and a chance at OSI's item at the counter;
    // Ask shows the two-button choice at each turn-in (ClusterFSmithBODPayout). A v0 or v1 record reads as Bank.
    public SmithTurnInMode SmithTurnIn { get; set; } = SmithTurnInMode.Bank;

    // -- Craft guild promotion (v3, cc-P78 Part E, F-43) ----------------------
    // RankCap: per craft guild, the highest rank index this character's craft skill (and, for rank 5, the guild trial)
    // has ever allowed. It only rises, so a loop's skill reset never lowers a rank. A guild with no entry is not
    // tracked and ranks by standing alone, as before cc-P78 (non-craft guilds, and records built by hand).
    // PromotedRank: the highest rank whose grants were handed over. TrialsPassed: guild trials done (rank 5).
    // ClusterFGuildPromotion keeps all three.
    public Dictionary<string, int> RankCap      { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> PromotedRank { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string>         TrialsPassed { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEmpty =>
        JoinedGuilds.Count == 0 && ApprenticeGuilds.Count == 0 &&
        GuildReputation.Count == 0 && GuildCurrency.Count == 0 &&
        ActiveWorkOrders.Count == 0 && CompletedWorkOrders.Count == 0 &&
        SmithCommissions.Count == 0 && SmithLargeCommissions.Count == 0 &&
        !HasActiveArtificerOrder && SmithTeachingOrders && SmithTurnIn == SmithTurnInMode.Bank &&
        RankCap.Count == 0 && PromotedRank.Count == 0 && TrialsPassed.Count == 0;

    public CharacterGuildData() { }

    public CharacterGuildData(IGenericReader r)
    {
        var version = r.ReadInt();
        if (version is < 0 or > CurrentVersion)
            throw new System.IO.InvalidDataException(
                $"CharacterGuildData version {version} is not one this build reads (0 to {CurrentVersion}).");

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

        if (version >= 1)
            SmithTeachingOrders = r.ReadBool();

        if (version >= 2)
        {
            var mode = r.ReadByte();
            if (mode > (byte)SmithTurnInMode.Ask)
                throw new System.IO.InvalidDataException($"CharacterGuildData turn-in mode {mode} is not one this build knows.");
            SmithTurnIn = (SmithTurnInMode)mode;
        }

        if (version >= 3)
        {
            var caps = r.ReadInt();
            for (var i = 0; i < caps; i++)
                RankCap[r.ReadString()] = r.ReadInt();

            var promoted = r.ReadInt();
            for (var i = 0; i < promoted; i++)
                PromotedRank[r.ReadString()] = r.ReadInt();

            var trials = r.ReadInt();
            for (var i = 0; i < trials; i++)
                TrialsPassed.Add(r.ReadString());
        }
        else
        {
            // Before v3, craft guild rank was standing alone. Each joined craft guild starts tracked at the rank it had,
            // so nobody loses a rank; higher ranks need the craft skill from now on.
            ClusterFGuildPromotion.SeedFromStanding(this);
        }
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

        w.Write(SmithTeachingOrders); // v1
        w.Write((byte)SmithTurnIn);   // v2

        // v3
        w.Write(RankCap.Count);
        foreach (var (k, v) in RankCap) { w.Write(k); w.Write(v); }
        w.Write(PromotedRank.Count);
        foreach (var (k, v) in PromotedRank) { w.Write(k); w.Write(v); }
        w.Write(TrialsPassed.Count);
        foreach (var k in TrialsPassed) w.Write(k);
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
    /// Drops every account's record: League, achievements' points, discoveries, restorations, guild and
    /// exploration data. Only the test account wipe calls it (cc-P73, Misc/TestAccountWipe.cs), after every
    /// account is deleted, since an account recreated under the same name would otherwise inherit its record.
    /// </summary>
    internal static int ClearAll()
    {
        var n = _data.Count;
        _data.Clear();
        return n;
    }

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

/// <summary>
/// One character's loot box record (cc-P79, F-40 / F-41; ClusterFAccountData v18): for each box source (an achievement,
/// a boss, a League rank, a dungeon clear, a loop) the loops it was earned on, and whether the character has muted its
/// own fanfare. Carries its own version (the D40 rule).
/// </summary>
public sealed class CharacterLootRecord
{
    public const int CurrentVersion = 0;

    // Source key ("ach:mining.first_ore", "boss:Harrower", "league:5", "clear:<dungeon>", "loop:3") to the loops earned on.
    public Dictionary<string, List<int>> Earned { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool FanfareMuted { get; set; }

    public CharacterLootRecord() { }

    public CharacterLootRecord(IGenericReader r)
    {
        var version = r.ReadInt();
        if (version is < 0 or > CurrentVersion)
            throw new System.IO.InvalidDataException(
                $"CharacterLootRecord version {version} is not one this build reads (0 to {CurrentVersion}).");

        var count = r.ReadInt();
        for (var i = 0; i < count; i++)
        {
            var key = r.ReadString();
            var loops = new List<int>();
            var n = r.ReadInt();
            for (var j = 0; j < n; j++)
                loops.Add(r.ReadInt());
            Earned[key] = loops;
        }

        FanfareMuted = r.ReadBool();
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(CurrentVersion);
        w.Write(Earned.Count);
        foreach (var (key, loops) in Earned)
        {
            w.Write(key);
            w.Write(loops.Count);
            foreach (var loop in loops)
                w.Write(loop);
        }

        w.Write(FanfareMuted);
    }

    public int CountOf(string source) => Earned.TryGetValue(source, out var loops) ? loops.Count : 0;

    /// <summary>Records an earn on a loop. Returns true when this is the first earn of the source on that loop.</summary>
    public bool Record(string source, int loop)
    {
        if (!Earned.TryGetValue(source, out var loops))
            Earned[source] = loops = new List<int>();
        if (loops.Contains(loop))
            return false;
        loops.Add(loop);
        return true;
    }
}

/// <summary>Where an account is in the New Haven tutorial (cc-P80).</summary>
public enum TutorialState : byte
{
    NotStarted,
    InCore,
    InWings,
    Graduated,
    Skipped,
    Remedial
}

/// <summary>
/// The account's New Haven tutorial record (cc-P80; ClusterFAccountData v19). One per account: the tutorial runs once
/// per account, for the character that started it. Carries its own version (the D40 rule).
/// v1 (cc-P453): PetClaimed, PetClaimPending and BandagesIssued, kept apart from StationsDone, which remedial training
/// and the Fountain of Youth clear. A v0 record takes the first two from P276's and P480's done keys.
/// </summary>
public sealed class TutorialRecord
{
    public const int CurrentVersion = 1;

    // The v0 done keys the v1 fields replace (TutorialFarm.PetKey, ClusterFTutorial's companion keys).
    public const string LegacyFarmPetKey = "farm.pet";
    public const string LegacyCompanionTakenKey = "graduation.pet";
    public const string LegacyCompanionPendingKey = "graduation.petPending";

    public TutorialState State { get; set; } = TutorialState.NotStarted;

    // The character doing it (0 when none).
    public uint CharacterSerial { get; set; }

    // Station keys finished (TutorialStations).
    public HashSet<string> StationsDone { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Set once the account skipped; remedial completion then earns "Read the Manual Eventually".
    public bool WasSkipped { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime FinishedAt { get; set; }

    // The account's one tutorial pet (the Farmhand's or the graduation pick) was given. Never cleared.
    public bool PetClaimed { get; set; }

    // Graduation earned a pet that is not chosen yet; cleared only when one is given.
    public bool PetClaimPending { get; set; }

    // Stacks of the Farmhand's bandages handed out on this account (capped, TutorialFarm.BandageIssues).
    public int BandagesIssued { get; set; }

    public TutorialRecord() { }

    public TutorialRecord(IGenericReader r)
    {
        var version = r.ReadInt();
        if (version is < 0 or > CurrentVersion)
            throw new System.IO.InvalidDataException(
                $"TutorialRecord version {version} is not one this build reads (0 to {CurrentVersion}).");

        var state = r.ReadByte();
        if (state > (byte)TutorialState.Remedial)
            throw new System.IO.InvalidDataException($"TutorialRecord state {state} is not one this build knows.");
        State = (TutorialState)state;
        CharacterSerial = r.ReadUInt();
        var n = r.ReadInt();
        for (var i = 0; i < n; i++)
            StationsDone.Add(r.ReadString());
        WasSkipped = r.ReadBool();
        StartedAt = r.ReadDateTime();
        FinishedAt = r.ReadDateTime();

        if (version >= 1)
        {
            PetClaimed = r.ReadBool();
            PetClaimPending = r.ReadBool();
            BandagesIssued = r.ReadInt();
        }
        else
        {
            PetClaimed = StationsDone.Contains(LegacyFarmPetKey) || StationsDone.Contains(LegacyCompanionTakenKey);
            PetClaimPending = StationsDone.Remove(LegacyCompanionPendingKey) && !PetClaimed;
            StationsDone.Remove(LegacyCompanionTakenKey);
        }
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(CurrentVersion);
        w.Write((byte)State);
        w.Write(CharacterSerial);
        w.Write(StationsDone.Count);
        foreach (var key in StationsDone)
            w.Write(key);
        w.Write(WasSkipped);
        w.Write(StartedAt);
        w.Write(FinishedAt);
        w.Write(PetClaimed);
        w.Write(PetClaimPending);
        w.Write(BandagesIssued);
    }

    /// <summary>In the tutorial now: the core, the wings, or remedial training.</summary>
    public bool IsActive => State is TutorialState.InCore or TutorialState.InWings or TutorialState.Remedial;
}

/// <summary>
/// The account's League titles (cc-P229, F-13; ClusterFAccountData v21): every title it holds, bought in the League shop
/// or unlocked by an AP threshold (kept once unlocked, so a later AP change never takes one away), and the one title each
/// character shows, by character serial. Carries its own version (the D40 rule).
/// </summary>
public sealed class LeagueTitleRecord
{
    public const int CurrentVersion = 0;

    // Title keys (LeagueTitles), bought or unlocked.
    public HashSet<string> Owned { get; } = new(StringComparer.Ordinal);

    // The title key each character shows, by character serial. No entry: no League title.
    public Dictionary<uint, string> Shown { get; } = new();

    public LeagueTitleRecord() { }

    public LeagueTitleRecord(IGenericReader r)
    {
        var version = r.ReadInt();
        if (version is < 0 or > CurrentVersion)
            throw new System.IO.InvalidDataException(
                $"LeagueTitleRecord version {version} is not one this build reads (0 to {CurrentVersion}).");

        var owned = r.ReadInt();
        for (var i = 0; i < owned; i++)
            Owned.Add(r.ReadString());

        var shown = r.ReadInt();
        for (var i = 0; i < shown; i++)
        {
            var serial = r.ReadUInt();
            Shown[serial] = r.ReadString();
        }
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(CurrentVersion);
        w.Write(Owned.Count);
        foreach (var key in Owned)
            w.Write(key);
        w.Write(Shown.Count);
        foreach (var (serial, key) in Shown)
        {
            w.Write(serial);
            w.Write(key);
        }
    }
}

/// <summary>
/// cc-P238 (F-36): every 8 x 8 ore bank an account has mined: facet, bank (tile / 8, as pinned's HarvestDefinition banks
/// are keyed), the first time it was mined, and the ores found there. Its own version; an unknown one is refused (D40).
/// </summary>
public sealed class MinedBlockRecord
{
    public const int CurrentVersion = 0;

    public sealed class Block
    {
        public DateTime FirstMined { get; set; }
        public SortedSet<string> Ores { get; } = new(StringComparer.Ordinal);
    }

    private readonly Dictionary<(string Facet, int X, int Y), Block> _blocks = new();

    public IReadOnlyDictionary<(string Facet, int X, int Y), Block> Blocks => _blocks;

    /// <summary>How many distinct blocks the account has mined.</summary>
    public int Count => _blocks.Count;

    public MinedBlockRecord() { }

    public MinedBlockRecord(IGenericReader r)
    {
        var version = r.ReadInt();
        if (version is < 0 or > CurrentVersion)
            throw new System.IO.InvalidDataException(
                $"MinedBlockRecord version {version} is not one this build reads (0 to {CurrentVersion}).");

        var count = r.ReadInt();
        for (var i = 0; i < count; i++)
        {
            var facet = r.ReadString();
            var x = r.ReadInt();
            var y = r.ReadInt();
            var block = new Block { FirstMined = r.ReadDateTime() };
            var ores = r.ReadInt();
            for (var j = 0; j < ores; j++)
                block.Ores.Add(r.ReadString());
            _blocks[(facet, x, y)] = block;
        }
    }

    /// <summary>Records ore found in a block. Returns true when the block is new to the account.</summary>
    public bool Record(string facet, int bankX, int bankY, string ore, DateTime now)
    {
        var isNew = false;
        if (!_blocks.TryGetValue((facet, bankX, bankY), out var block))
        {
            _blocks[(facet, bankX, bankY)] = block = new Block { FirstMined = now };
            isNew = true;
        }

        if (!string.IsNullOrEmpty(ore))
            block.Ores.Add(ore);

        return isNew;
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(CurrentVersion);
        w.Write(_blocks.Count);
        foreach (var ((facet, x, y), block) in _blocks)
        {
            w.Write(facet);
            w.Write(x);
            w.Write(y);
            w.Write(block.FirstMined);
            w.Write(block.Ores.Count);
            foreach (var ore in block.Ores)
                w.Write(ore);
        }
    }
}
