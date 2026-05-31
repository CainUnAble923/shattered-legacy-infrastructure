using System;
using System.Collections.Generic;
using System.Linq;
using Server.Items;
using Server.Mobiles;

namespace Server;

// ── Work order type ───────────────────────────────────────────────────────────

public enum WorkOrderType
{
    ResourceContract,
    CraftedSupply,
    RefinementContract,
    SurveyContract,
    ExpeditionContract,
    GemContract,
    StoneContract,
    HuntingContract,
    TamingContract,
    TimberContract,
    CivicContract,
}

// ── Requirement: one line of a work order ─────────────────────────────────────

/// <summary>A single item/amount requirement line inside a work order definition.</summary>
public class WorkOrderRequirement
{
    public Type   ItemType { get; }
    public int    Amount   { get; }
    public string Label    { get; }  // human-readable (e.g. "Iron Ingots")

    public WorkOrderRequirement(Type itemType, int amount, string label)
    {
        ItemType = itemType;
        Amount   = amount;
        Label    = label;
    }
}

/// <summary>
/// A taming work order requirement: the player must tame and deliver
/// <see cref="Amount"/> creatures of type <see cref="WorkOrderRequirement.ItemType"/>
/// using their Outrider's Crook. Progress is tracked in
/// <see cref="WorkOrderEntry.TamingProgress"/> rather than the player's backpack.
/// </summary>
public class WorkOrderTamingRequirement : WorkOrderRequirement
{
    public WorkOrderTamingRequirement(Type creatureType, int amount, string label)
        : base(creatureType, amount, label) { }
}

// ── Work order definition (static, registered at startup) ────────────────────

/// <summary>
/// Defines a guild work order that players can accept and turn in.
///
/// Definitions are immutable and registered once in ClusterFWorkOrderSystem.Configure().
/// Player state (active/completed instances) lives in ClusterFAccountData.
/// </summary>
public class WorkOrderDef
{
    public string        Key         { get; }
    public string        GuildKey    { get; }
    public string        Title       { get; }
    public string        Description { get; }
    public WorkOrderType Type        { get; }

    public List<WorkOrderRequirement> Requirements { get; }

    // Eligibility gates
    public int        MinStanding       { get; }
    public SkillName? SkillRequired     { get; }
    public double     MinSkill          { get; }

    /// <summary>
    /// Ore discovery key that must be in Reported state before this order appears.
    /// Null = no discovery gate. Matches OreDiscoveryEntry keys (e.g. "Valorite", "Frost").
    /// </summary>
    public string?    RequiredDiscovery { get; }

    /// <summary>
    /// Creature type name that must appear in the player's EncounteredCreatures before
    /// this order is visible. Null = no encounter gate. Matches creature class names
    /// as reported by Type.Name (e.g. "Ridgeback", "Drake", "Kirin").
    /// </summary>
    public string?    RequiredCreature      { get; }

    /// <summary>
    /// Wood discovery key that must be in Reported state before this order appears.
    /// Null = no discovery gate. Matches WoodDiscoveryEntry keys (e.g. "Ironwood", "Starwood").
    /// </summary>
    public string?    RequiredWoodDiscovery { get; }

    // Rewards
    public int StandingReward { get; }
    public int VoucherReward  { get; }
    public int GoldReward     { get; }

    public bool Repeatable { get; }

    public WorkOrderDef(
        string key, string guildKey, string title, string description,
        WorkOrderType type, List<WorkOrderRequirement> requirements,
        int minStanding, SkillName? skillRequired, double minSkill,
        int standingReward, int voucherReward, int goldReward,
        bool repeatable = true, string? requiredDiscovery = null,
        string? requiredCreature = null, string? requiredWoodDiscovery = null)
    {
        Key                   = key;
        GuildKey              = guildKey;
        Title                 = title;
        Description           = description;
        Type                  = type;
        Requirements          = requirements;
        MinStanding           = minStanding;
        SkillRequired         = skillRequired;
        MinSkill              = minSkill;
        StandingReward        = standingReward;
        VoucherReward         = voucherReward;
        GoldReward            = goldReward;
        Repeatable            = repeatable;
        RequiredDiscovery     = requiredDiscovery;
        RequiredCreature      = requiredCreature;
        RequiredWoodDiscovery = requiredWoodDiscovery;
    }

    public string TypeLabel => Type switch
    {
        WorkOrderType.ResourceContract   => "Resource Contract",
        WorkOrderType.CraftedSupply      => "Crafted Supply",
        WorkOrderType.RefinementContract => "Refinement",
        WorkOrderType.SurveyContract     => "Survey",
        WorkOrderType.ExpeditionContract => "Expedition",
        WorkOrderType.GemContract        => "Gem Cache",
        WorkOrderType.StoneContract      => "Stone Contract",
        WorkOrderType.HuntingContract    => "Hunting Contract",
        WorkOrderType.TamingContract     => "Taming Contract",
        WorkOrderType.TimberContract     => "Timber Contract",
        WorkOrderType.CivicContract      => "Civic Contract",
        _                                => "Contract"
    };

    /// <summary>Returns true if the player meets standing, skill, discovery, and encounter eligibility.</summary>
    public bool IsEligible(PlayerMobile pm, ClusterFAccountData data)
    {
        if (data.GetReputation(GuildKey) < MinStanding) return false;
        if (SkillRequired.HasValue && pm.Skills[SkillRequired.Value].Value < MinSkill) return false;

        if (RequiredDiscovery != null)
        {
            if (!data.OreDiscoveries.TryGetValue(RequiredDiscovery, out var entry)) return false;
            if (entry.State != DiscoveryState.Reported) return false;
        }

        if (RequiredCreature != null && !data.HasEncountered(RequiredCreature)) return false;

        if (RequiredWoodDiscovery != null)
        {
            if (!data.WoodDiscoveries.TryGetValue(RequiredWoodDiscovery, out var wdEntry)) return false;
            if (wdEntry.State != DiscoveryState.Reported) return false;
        }

        return true;
    }

    /// <summary>
    /// Human-readable reason why the player is ineligible, for use in gump tooltips.
    /// Returns null if the player is eligible.
    /// </summary>
    public string? IneligibleReason(PlayerMobile pm, ClusterFAccountData data)
    {
        if (data.GetReputation(GuildKey) < MinStanding)
        {
            var guildName = ClusterFGuildSystem.GetDef(GuildKey)?.Name ?? GuildKey;
            return $"Requires {MinStanding:N0} {guildName} standing";
        }

        if (SkillRequired.HasValue && pm.Skills[SkillRequired.Value].Value < MinSkill)
            return $"Requires {MinSkill:N0} {SkillRequired.Value}";

        if (RequiredDiscovery != null)
        {
            if (!data.OreDiscoveries.TryGetValue(RequiredDiscovery, out var entry))
                return $"Requires {RequiredDiscovery} discovery reported to Velara Thorne (Survey Archivist)";
            if (entry.State != DiscoveryState.Reported)
                return $"Report your {RequiredDiscovery} discovery to Velara Thorne at the south mine";
        }

        if (RequiredCreature != null && !data.HasEncountered(RequiredCreature))
            return $"You must hunt a {RequiredCreature} before this contract is available";

        if (RequiredWoodDiscovery != null)
        {
            if (!data.WoodDiscoveries.TryGetValue(RequiredWoodDiscovery, out var wdEntry))
                return $"Requires {RequiredWoodDiscovery} discovery reported to Cedric Rowanwood (Foresters' Guildmaster)";
            if (wdEntry.State != DiscoveryState.Reported)
                return $"Report your {RequiredWoodDiscovery} discovery to Cedric Rowanwood at the carpenter's yard";
        }

        return null;
    }

    /// <summary>
    /// Returns true if the player meets all requirements.
    /// For taming contracts, pass the active <see cref="WorkOrderEntry"/> so
    /// delivery progress can be checked. Bypassed by DevTestingCrystal.
    /// </summary>
    public bool RequirementsMet(PlayerMobile pm, WorkOrderEntry? entry = null)
    {
        if (Items.DevTestingCrystal.IsActive(pm)) return true;
        var pack = pm.Backpack;
        foreach (var req in Requirements)
        {
            if (req is WorkOrderTamingRequirement tamingReq)
            {
                if (entry == null) return false;
                entry.TamingProgress.TryGetValue(tamingReq.ItemType.Name, out var progress);
                if (progress < tamingReq.Amount) return false;
            }
            else
            {
                if (pack == null) return false;
                if (pack.GetAmount(req.ItemType) < req.Amount) return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Consumes all required items from the player's backpack.
    /// Taming requirements are skipped (animals were already consumed via crook delivery).
    /// No-op if the player carries an active DevTestingCrystal (bypass mode).
    /// Should only be called after RequirementsMet() returns true.
    /// </summary>
    public void ConsumeRequirements(PlayerMobile pm)
    {
        if (Items.DevTestingCrystal.IsActive(pm)) return; // testing bypass — nothing consumed
        var pack = pm.Backpack;
        if (pack == null) return;
        foreach (var req in Requirements)
        {
            if (req is WorkOrderTamingRequirement) continue; // already consumed via crook delivery
            pack.ConsumeTotal(req.ItemType, req.Amount);
        }
    }
}

// ── Player's active work order instance (serialized in account data) ──────────

/// <summary>
/// One accepted work order on a player's account.
/// Stored in ClusterFAccountData.ActiveWorkOrders / CompletedWorkOrders.
/// </summary>
public class WorkOrderEntry
{
    public string    DefKey      { get; }
    public string    GuildKey    { get; }
    public DateTime  AcceptedAt  { get; }
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Tracks delivery progress for taming contracts.
    /// Key = creature type name (e.g. "Horse"), Value = count delivered via crook.
    /// </summary>
    public Dictionary<string, int> TamingProgress { get; } = new();

    public WorkOrderEntry(string defKey, string guildKey)
    {
        DefKey     = defKey;
        GuildKey   = guildKey;
        AcceptedAt = DateTime.UtcNow;
    }

    // Deserialization — version 1 adds TamingProgress
    public WorkOrderEntry(IGenericReader r)
    {
        var version = r.ReadInt(); // 0 or 1
        DefKey      = r.ReadString();
        GuildKey    = r.ReadString();
        AcceptedAt  = r.ReadDateTime();
        CompletedAt = r.ReadBool() ? r.ReadDateTime() : null;

        if (version >= 1)
        {
            var count = r.ReadInt();
            for (var i = 0; i < count; i++)
                TamingProgress[r.ReadString()] = r.ReadInt();
        }
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(1); // version 1
        w.Write(DefKey);
        w.Write(GuildKey);
        w.Write(AcceptedAt);
        w.Write(CompletedAt.HasValue);
        if (CompletedAt.HasValue) w.Write(CompletedAt.Value);

        w.Write(TamingProgress.Count);
        foreach (var (key, val) in TamingProgress)
        {
            w.Write(key);
            w.Write(val);
        }
    }
}

// ── Static registry ───────────────────────────────────────────────────────────

/// <summary>
/// Shattered Legacy Guild Work Order System.
///
/// All work order definitions are registered here at startup via Configure().
/// Player state lives in ClusterFAccountData (ActiveWorkOrders / CompletedWorkOrders).
///
/// Work order flow:
///   1. Player opens the Guild Contract Ledger gump via a guild liaison.
///   2. Available tab shows eligible orders the player doesn't already have active.
///   3. Player accepts — a WorkOrderEntry is added to their account data.
///   4. Player gathers required items.
///   5. Player returns to the liaison, opens Active tab, clicks Turn In.
///   6. Items consumed, rewards applied, entry moved to completed history.
///
/// Limits:
///   - Max 3 active work orders across all guilds combined.
///   - Each definition may only have one active copy at a time.
///   - Completed history capped at the last 20 entries (oldest pruned).
/// </summary>
public static class ClusterFWorkOrderSystem
{
    private static readonly Dictionary<string, WorkOrderDef> _defs =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Maximum active work orders a player may hold per guild at any time.</summary>
    public const int MaxActiveOrdersPerGuild = 3;

    /// <summary>Maximum completed entries retained per account (oldest pruned).</summary>
    public const int MaxHistoryEntries = 20;

    public static void Configure()
    {
        RegisterMinersCompactOrders();
        RegisterMinersCompactGemOrders();
        RegisterMinersCompactStoneOrders();
        RegisterSocietyOfSmithsOrders();
        RegisterRangersOrders();
        RegisterForestersOrders();
        RegisterCustodiansOrders();
    }

    // ── Registry ──────────────────────────────────────────────────────────────

    public static void Register(WorkOrderDef def) => _defs[def.Key] = def;

    public static WorkOrderDef? Get(string key) =>
        _defs.TryGetValue(key, out var d) ? d : null;

    public static IEnumerable<WorkOrderDef> GetForGuild(string guildKey) =>
        _defs.Values
             .Where(d => d.GuildKey.Equals(guildKey, StringComparison.OrdinalIgnoreCase));

    // ── Account data helpers ──────────────────────────────────────────────────

    /// <summary>Returns true if the player already has an active copy of this order.</summary>
    public static bool HasActive(ClusterFAccountData data, string defKey) =>
        data.ActiveWorkOrders.Exists(e => e.DefKey.Equals(defKey, StringComparison.OrdinalIgnoreCase));

    /// <summary>Returns true if the player can accept another work order of this key.</summary>
    public static bool CanAccept(ClusterFAccountData data, string defKey, string guildKey) =>
        data.ActiveWorkOrders.Count(e => e.GuildKey.Equals(guildKey, StringComparison.OrdinalIgnoreCase)) < MaxActiveOrdersPerGuild
        && !HasActive(data, defKey);

    // ── Miners' Compact order definitions ─────────────────────────────────────

    private static void RegisterMinersCompactOrders()
    {
        // ── Entry-level: no standing or skill required ────────────────────────
        // Iron orders have no discovery gate — iron is excluded from discovery tracking by design.

        Register(new WorkOrderDef(
            key:               "mining.iron_ingots_s",
            guildKey:          "mining",
            title:             "Iron Shipment",
            description:       "Deliver a standard iron ingot shipment to the Compact depot.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(IronIngot), 50, "Iron Ingots") },
            minStanding:       0,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    100,
            voucherReward:     5,
            goldReward:        75
        ));

        Register(new WorkOrderDef(
            key:               "mining.iron_ore_s",
            guildKey:          "mining",
            title:             "Raw Ore Sample",
            description:       "The Survey Archivist needs fresh raw iron ore for quality assessment.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(IronOre), 100, "Iron Ore") },
            minStanding:       0,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    75,
            voucherReward:     4,
            goldReward:        60
        ));

        // ── Intermediate: Mining 30+ required ────────────────────────────────

        Register(new WorkOrderDef(
            key:               "mining.iron_ingots_m",
            guildKey:          "mining",
            title:             "Iron Bulk Order",
            description:       "A larger iron ingot request from the Compact forge. More effort, better pay.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(IronIngot), 250, "Iron Ingots") },
            minStanding:       0,
            skillRequired:     SkillName.Mining,
            minSkill:          30.0,
            standingReward:    600,   // 2.4/ingot vs 2.0 for the small — bulk orders reward effort
            voucherReward:     30,    // 0.12/ingot vs 0.10 for the small
            goldReward:        350
        ));

        // ── Apprentice tier: 1,000 Standing + Mining 40+ ─────────────────────
        // Discovery gate: must have reported the ore type to the Survey Archivist.

        Register(new WorkOrderDef(
            key:               "mining.dull_copper_ingots_s",
            guildKey:          "mining",
            title:             "Dull Copper Shipment",
            description:       "The forge is running low on Dull Copper. Deliver a batch to the depot.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(DullCopperIngot), 30, "Dull Copper Ingots") },
            minStanding:       1000,
            skillRequired:     SkillName.Mining,
            minSkill:          40.0,
            standingReward:    250,
            voucherReward:     12,
            goldReward:        350,
            requiredDiscovery: "DullCopper"
        ));

        Register(new WorkOrderDef(
            key:               "mining.forge_resupply_m",
            guildKey:          "mining",
            title:             "Forge Resupply",
            description:       "A mixed metals shipment for the Compact forge.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new()
            {
                new(typeof(IronIngot),       200, "Iron Ingots"),
                new(typeof(DullCopperIngot),  50, "Dull Copper Ingots"),
            },
            minStanding:       1000,
            skillRequired:     SkillName.Mining,
            minSkill:          45.0,
            standingReward:    600,
            voucherReward:     28,
            goldReward:        500,
            requiredDiscovery: "DullCopper"
        ));

        // ── Journeyman tier: 5,000 Standing ──────────────────────────────────

        Register(new WorkOrderDef(
            key:               "mining.shadow_iron_s",
            guildKey:          "mining",
            title:             "Shadow Iron Shipment",
            description:       "The Compact forge needs shadow iron for specialty work. Deliver a batch.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(ShadowIronIngot), 20, "Shadow Iron Ingots") },
            minStanding:       5000,
            skillRequired:     SkillName.Mining,
            minSkill:          50.0,
            standingReward:    300,
            voucherReward:     14,
            goldReward:        500,
            requiredDiscovery: "ShadowIron"
        ));

        Register(new WorkOrderDef(
            key:               "mining.copper_s",
            guildKey:          "mining",
            title:             "Copper Delivery",
            description:       "The depot quartermaster needs copper ingots for a standing supply order.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(CopperIngot), 20, "Copper Ingots") },
            minStanding:       5000,
            skillRequired:     SkillName.Mining,
            minSkill:          50.0,
            standingReward:    300,
            voucherReward:     14,
            goldReward:        500,
            requiredDiscovery: "Copper"
        ));

        Register(new WorkOrderDef(
            key:               "mining.ore_refinement_s",
            guildKey:          "mining",
            title:             "Ore Refinement Run",
            description:       "Process a batch of raw iron ore for the Compact's refinement record.",
            type:              WorkOrderType.RefinementContract,
            requirements:      new() { new(typeof(IronOre), 150, "Iron Ore") },
            minStanding:       5000,
            skillRequired:     SkillName.Mining,
            minSkill:          50.0,
            standingReward:    350,
            voucherReward:     16,
            goldReward:        400
            // No discovery gate — iron ore, iron is excluded from tracking.
        ));

        Register(new WorkOrderDef(
            key:               "mining.deep_veins_m",
            guildKey:          "mining",
            title:             "Deep Veins Run",
            description:       "A large pull from the deep veins — shadow iron and copper for the master forges.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new()
            {
                new(typeof(ShadowIronIngot), 40, "Shadow Iron Ingots"),
                new(typeof(CopperIngot),     25, "Copper Ingots"),
            },
            minStanding:       5000,
            skillRequired:     SkillName.Mining,
            minSkill:          55.0,
            standingReward:    900,
            voucherReward:     40,
            goldReward:        750,
            requiredDiscovery: "ShadowIron"
        ));

        // ── Surveyor tier: 15,000 Standing ───────────────────────────────────

        Register(new WorkOrderDef(
            key:               "mining.bronze_s",
            guildKey:          "mining",
            title:             "Bronze Consignment",
            description:       "Bronze ingots are in short supply at the depot. Bring a consignment.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(BronzeIngot), 15, "Bronze Ingots") },
            minStanding:       15000,
            skillRequired:     SkillName.Mining,
            minSkill:          60.0,
            standingReward:    500,
            voucherReward:     22,
            goldReward:        1000,
            requiredDiscovery: "Bronze"
        ));

        Register(new WorkOrderDef(
            key:               "mining.agapite_s",
            guildKey:          "mining",
            title:             "Agapite Cache",
            description:       "The master forger has requested agapite ingots for a new alloy project.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(AgapiteIngot), 12, "Agapite Ingots") },
            minStanding:       15000,
            skillRequired:     SkillName.Mining,
            minSkill:          65.0,
            standingReward:    600,
            voucherReward:     26,
            goldReward:        1500,
            requiredDiscovery: "Agapite"
        ));

        Register(new WorkOrderDef(
            key:               "mining.mid_tier_metals_m",
            guildKey:          "mining",
            title:             "Mixed Mid-Tier Metals",
            description:       "The Compact forge needs a diverse alloy shipment for its most complex commissions.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new()
            {
                new(typeof(ShadowIronIngot), 50, "Shadow Iron Ingots"),
                new(typeof(BronzeIngot),     25, "Bronze Ingots"),
            },
            minStanding:       15000,
            skillRequired:     SkillName.Mining,
            minSkill:          65.0,
            standingReward:    1200,
            voucherReward:     52,
            goldReward:        1500,
            requiredDiscovery: "Bronze"
        ));

        // ── Master Delver tier: 40,000 Standing ──────────────────────────────

        Register(new WorkOrderDef(
            key:               "mining.verite_s",
            guildKey:          "mining",
            title:             "Verite Haul",
            description:       "Only the most experienced delvers reach the verite seams. The forge is waiting.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(VeriteIngot), 8, "Verite Ingots") },
            minStanding:       40000,
            skillRequired:     SkillName.Mining,
            minSkill:          70.0,
            standingReward:    1000,
            voucherReward:     44,
            goldReward:        2500,
            requiredDiscovery: "Verite"
        ));

        Register(new WorkOrderDef(
            key:               "mining.valorite_s",
            guildKey:          "mining",
            title:             "Valorite Delivery",
            description:       "Valorite ingots for the Compact vault. The rarest metal, the highest honour.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(ValoriteIngot), 6, "Valorite Ingots") },
            minStanding:       40000,
            skillRequired:     SkillName.Mining,
            minSkill:          80.0,
            standingReward:    1500,
            voucherReward:     60,
            goldReward:        4000,
            requiredDiscovery: "Valorite"
        ));

        Register(new WorkOrderDef(
            key:               "mining.high_metals_m",
            guildKey:          "mining",
            title:             "Rare Metals Commission",
            description:       "The Compact's grandmaster forger needs a curated selection of rare alloys.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new()
            {
                new(typeof(BronzeIngot),  20, "Bronze Ingots"),
                new(typeof(AgapiteIngot), 10, "Agapite Ingots"),
                new(typeof(VeriteIngot),   8, "Verite Ingots"),
            },
            minStanding:       40000,
            skillRequired:     SkillName.Mining,
            minSkill:          80.0,
            standingReward:    2800,
            voucherReward:     112,
            goldReward:        3000,
            requiredDiscovery: "Verite"
        ));

        // ── Deepwarden tier: 80,000 Standing ─────────────────────────────────

        Register(new WorkOrderDef(
            key:               "mining.valorite_cache",
            guildKey:          "mining",
            title:             "Valorite Cache",
            description:       "A substantial valorite reserve for the Compact's legendary weapon project.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(ValoriteIngot), 20, "Valorite Ingots") },
            minStanding:       80000,
            skillRequired:     SkillName.Mining,
            minSkill:          90.0,
            standingReward:    6000,   // 300/ingot vs 250 for the small — 20-ingot haul earns a premium
            voucherReward:     240,    // 12/ingot vs 10 for the small
            goldReward:        10000,  // significant gold — 20 valorite is serious effort; helps fund T4/T5 restoration
            requiredDiscovery: "Valorite"
        ));

        Register(new WorkOrderDef(
            key:               "mining.grand_tribute",
            guildKey:          "mining",
            title:             "Grand Forge Tribute",
            description:       "The highest honour a Compact member can perform — a full-spectrum metals tribute to the forge.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new()
            {
                new(typeof(IronIngot),       100, "Iron Ingots"),
                new(typeof(ShadowIronIngot),  50, "Shadow Iron Ingots"),
                new(typeof(BronzeIngot),      30, "Bronze Ingots"),
                new(typeof(AgapiteIngot),     20, "Agapite Ingots"),
                new(typeof(VeriteIngot),      10, "Verite Ingots"),
                new(typeof(ValoriteIngot),     5, "Valorite Ingots"),
            },
            minStanding:       80000,
            skillRequired:     SkillName.Mining,
            minSkill:          95.0,
            standingReward:    5000,
            voucherReward:     200,
            goldReward:        5000,
            requiredDiscovery: "Valorite"
        ));

        // ── Extended ore tier — gated by facet discovery reports ─────────────
        // These ores come from Ilshenar, Malas, Tokuno, and Ter Mur.
        // Players must have reported the relevant discovery to Velara Thorne
        // before these contracts appear in their ledger.

        Register(new WorkOrderDef(
            key:               "mining.platinum_s",
            guildKey:          "mining",
            title:             "Platinum Consignment",
            description:       "Platinum veins run deep in Tokuno and Ilshenar. The Compact needs a supply.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(PlatinumIngot), 10, "Platinum Ingots") },
            minStanding:       15000,
            skillRequired:     SkillName.Mining,
            minSkill:          65.0,
            standingReward:    800,
            voucherReward:     32,
            goldReward:        500,
            requiredDiscovery: "Platinum"
        ));

        Register(new WorkOrderDef(
            key:               "mining.toxic_s",
            guildKey:          "mining",
            title:             "Toxic Ore Delivery",
            description:       "Toxic ore from Ilshenar's corrupted seams. Handle with care.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(ToxicIngot), 10, "Toxic Ingots") },
            minStanding:       15000,
            skillRequired:     SkillName.Mining,
            minSkill:          65.0,
            standingReward:    800,
            voucherReward:     32,
            goldReward:        500,
            requiredDiscovery: "Toxic"
        ));

        Register(new WorkOrderDef(
            key:               "mining.blaze_s",
            guildKey:          "mining",
            title:             "Blaze Metal Consignment",
            description:       "Blaze ore burns hot even as ingots. The Compact has a buyer waiting.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(BlazeIngot), 10, "Blaze Ingots") },
            minStanding:       15000,
            skillRequired:     SkillName.Mining,
            minSkill:          65.0,
            standingReward:    1000,
            voucherReward:     40,
            goldReward:        750,
            requiredDiscovery: "Blaze"
        ));

        Register(new WorkOrderDef(
            key:               "mining.frost_s",
            guildKey:          "mining",
            title:             "Frost Alloy Run",
            description:       "Frost ore is prized by enchanters. Recover a batch from the cold seams.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(FrostIngot), 10, "Frost Ingots") },
            minStanding:       15000,
            skillRequired:     SkillName.Mining,
            minSkill:          65.0,
            standingReward:    1000,
            voucherReward:     40,
            goldReward:        750,
            requiredDiscovery: "Frost"
        ));

        Register(new WorkOrderDef(
            key:               "mining.obsidian_s",
            guildKey:          "mining",
            title:             "Obsidian Cache",
            description:       "Obsidian veins in Malas are dangerous to work. The Compact rewards those who do.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(ObsidianIngot), 8, "Obsidian Ingots") },
            minStanding:       40000,
            skillRequired:     SkillName.Mining,
            minSkill:          75.0,
            standingReward:    1500,
            voucherReward:     60,
            goldReward:        1000,
            requiredDiscovery: "Obsidian"
        ));

        Register(new WorkOrderDef(
            key:               "mining.mythril_s",
            guildKey:          "mining",
            title:             "Mythril Reserve",
            description:       "Mythril is the pride of Malas and Tokuno. The forge has an urgent commission.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(MythrilIngot), 6, "Mythril Ingots") },
            minStanding:       40000,
            skillRequired:     SkillName.Mining,
            minSkill:          80.0,
            standingReward:    1800,
            voucherReward:     72,
            goldReward:        1500,
            requiredDiscovery: "Mythril"
        ));

        Register(new WorkOrderDef(
            key:               "mining.adamantium_s",
            guildKey:          "mining",
            title:             "Adamantium Delivery",
            description:       "Adamantium from Ter Mur's deepest reaches. Only the finest delvers find it.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(AdamantiumIngot), 5, "Adamantium Ingots") },
            minStanding:       80000,
            skillRequired:     SkillName.Mining,
            minSkill:          90.0,
            standingReward:    3000,
            voucherReward:     120,
            goldReward:        2000,
            requiredDiscovery: "Adamantium"
        ));

        Register(new WorkOrderDef(
            key:               "mining.celestial_s",
            guildKey:          "mining",
            title:             "Celestial Tribute",
            description:       "Celestial ore is the rarest known metal. The Compact will honour whoever brings it.",
            type:              WorkOrderType.ResourceContract,
            requirements:      new() { new(typeof(CelestialIngot), 3, "Celestial Ingots") },
            minStanding:       80000,
            skillRequired:     SkillName.Mining,
            minSkill:          95.0,
            standingReward:    5000,
            voucherReward:     200,
            goldReward:        5000,
            requiredDiscovery: "Celestial"
        ));
    }

    // ── Miners' Compact — Gem Cache orders ───────────────────────────────────
    //
    // Gems are a bonus yield during ore and stone mining. These high-value orders
    // reward players who save their gem finds. Rewards are weighted heavily toward
    // gold since gems have inherent trade value.
    //
    // Gem orders are gated by Compact Standing + Mining skill only — no discovery
    // gate, since gems drop from any ore vein regardless of type.

    private static void RegisterMinersCompactGemOrders()
    {
        // ── Journeyman tier: 5,000 Standing + Mining 50 ──────────────────────

        Register(new WorkOrderDef(
            key:               "mining.common_gems_s",
            guildKey:          "mining",
            title:             "Common Gem Cache",
            description:       "The Compact has a standing buyer for common gems recovered during mining operations.",
            type:              WorkOrderType.GemContract,
            requirements:      new()
            {
                new(typeof(Citrine),    5, "Citrine"),
                new(typeof(Amber),      5, "Amber"),
                new(typeof(Tourmaline), 5, "Tourmaline"),
            },
            minStanding:       5000,
            skillRequired:     SkillName.Mining,
            minSkill:          50.0,
            standingReward:    800,
            voucherReward:     35,
            goldReward:        2000
        ));

        // ── Master Delver tier: 40,000 Standing + Mining 70 ──────────────────

        Register(new WorkOrderDef(
            key:               "mining.fine_gems_s",
            guildKey:          "mining",
            title:             "Fine Gem Consignment",
            description:       "An enchanters' guild has placed an order for quality gems through the Compact. Only experienced miners reach the deep seams where these are found.",
            type:              WorkOrderType.GemContract,
            requirements:      new()
            {
                new(typeof(Amethyst), 5, "Amethyst"),
                new(typeof(Sapphire), 5, "Sapphire"),
                new(typeof(Ruby),     5, "Rubies"),
            },
            minStanding:       40000,
            skillRequired:     SkillName.Mining,
            minSkill:          70.0,
            standingReward:    2500,
            voucherReward:     100,
            goldReward:        8000
        ));

        // ── Deepwarden tier: 80,000 Standing + Mining 80/90 ──────────────────

        Register(new WorkOrderDef(
            key:               "mining.rare_gems_s",
            guildKey:          "mining",
            title:             "Rare Gem Tribute",
            description:       "Star sapphires and emeralds for the Compact jeweler's vault. The rarest faceted stones, found only by the most experienced delvers.",
            type:              WorkOrderType.GemContract,
            requirements:      new()
            {
                new(typeof(StarSapphire), 3, "Star Sapphires"),
                new(typeof(Emerald),      3, "Emeralds"),
            },
            minStanding:       80000,
            skillRequired:     SkillName.Mining,
            minSkill:          80.0,
            standingReward:    5000,
            voucherReward:     200,
            goldReward:        15000
        ));

        Register(new WorkOrderDef(
            key:               "mining.diamond_cache",
            guildKey:          "mining",
            title:             "Diamond Reserve",
            description:       "Diamonds are the rarest gem recovered from the deepest seams. The Compact pays the highest possible premium for a confirmed delivery.",
            type:              WorkOrderType.GemContract,
            requirements:      new() { new(typeof(Diamond), 3, "Diamonds") },
            minStanding:       80000,
            skillRequired:     SkillName.Mining,
            minSkill:          90.0,
            standingReward:    8000,
            voucherReward:     320,
            goldReward:        25000
        ));
    }

    // ── Miners' Compact — Stone Contract orders ───────────────────────────────
    //
    // Stone mining yields granite of various types depending on the vein worked.
    // Colored granite matches the ore vein it comes from and shares the same
    // discovery gate as the parallel ore — if you've reported the vein, you can
    // take the stone order. Basic granite (iron-tier stone) has no discovery gate.

    private static void RegisterMinersCompactStoneOrders()
    {
        // ── Entry / Intermediate: no discovery gate ───────────────────────────

        Register(new WorkOrderDef(
            key:               "mining.granite_haul_s",
            guildKey:          "mining",
            title:             "Granite Haul",
            description:       "The Compact's masonry depot needs a steady supply of cut granite for construction work.",
            type:              WorkOrderType.StoneContract,
            requirements:      new() { new(typeof(Granite), 50, "Granite") },
            minStanding:       0,
            skillRequired:     SkillName.Mining,
            minSkill:          30.0,
            standingReward:    250,
            voucherReward:     12,
            goldReward:        400
        ));

        Register(new WorkOrderDef(
            key:               "mining.granite_haul_m",
            guildKey:          "mining",
            title:             "Granite Bulk Order",
            description:       "A larger stone delivery for the depot's fortification project. More granite, better compensation.",
            type:              WorkOrderType.StoneContract,
            requirements:      new() { new(typeof(Granite), 200, "Granite") },
            minStanding:       0,
            skillRequired:     SkillName.Mining,
            minSkill:          40.0,
            standingReward:    1200,
            voucherReward:     52,
            goldReward:        1500
        ));

        // ── Apprentice tier: 1,000 Standing — colored stone begins ────────────

        Register(new WorkOrderDef(
            key:               "mining.dull_copper_granite_s",
            guildKey:          "mining",
            title:             "Dull Copper Stone Delivery",
            description:       "Dull Copper granite carries a faint reddish hue prized by the Compact's master masons.",
            type:              WorkOrderType.StoneContract,
            requirements:      new() { new(typeof(DullCopperGranite), 30, "Dull Copper Granite") },
            minStanding:       1000,
            skillRequired:     SkillName.Mining,
            minSkill:          40.0,
            standingReward:    300,
            voucherReward:     14,
            goldReward:        600,
            requiredDiscovery: "DullCopper"
        ));

        // ── Journeyman tier: 5,000 Standing ──────────────────────────────────

        Register(new WorkOrderDef(
            key:               "mining.shadow_stone_s",
            guildKey:          "mining",
            title:             "Shadow Stone Cache",
            description:       "Shadow Iron granite's dark finish is sought for premium construction. Cut and deliver a batch.",
            type:              WorkOrderType.StoneContract,
            requirements:      new() { new(typeof(ShadowIronGranite), 20, "Shadow Iron Granite") },
            minStanding:       5000,
            skillRequired:     SkillName.Mining,
            minSkill:          50.0,
            standingReward:    400,
            voucherReward:     18,
            goldReward:        900,
            requiredDiscovery: "ShadowIron"
        ));

        Register(new WorkOrderDef(
            key:               "mining.mixed_stone_m",
            guildKey:          "mining",
            title:             "Mixed Stone Commission",
            description:       "The depot wants a variety of colored stone for decorative inlay work.",
            type:              WorkOrderType.StoneContract,
            requirements:      new()
            {
                new(typeof(Granite),          100, "Granite"),
                new(typeof(DullCopperGranite), 30, "Dull Copper Granite"),
                new(typeof(ShadowIronGranite), 20, "Shadow Iron Granite"),
            },
            minStanding:       5000,
            skillRequired:     SkillName.Mining,
            minSkill:          55.0,
            standingReward:    1200,
            voucherReward:     52,
            goldReward:        2000,
            requiredDiscovery: "ShadowIron"
        ));

        // ── Surveyor tier: 15,000 Standing ───────────────────────────────────

        Register(new WorkOrderDef(
            key:               "mining.bronze_stone_s",
            guildKey:          "mining",
            title:             "Bronze Granite Consignment",
            description:       "Bronze granite is prized for its warm tones. The mason's guild has placed a standing order.",
            type:              WorkOrderType.StoneContract,
            requirements:      new() { new(typeof(BronzeGranite), 15, "Bronze Granite") },
            minStanding:       15000,
            skillRequired:     SkillName.Mining,
            minSkill:          60.0,
            standingReward:    600,
            voucherReward:     26,
            goldReward:        1800,
            requiredDiscovery: "Bronze"
        ));

        Register(new WorkOrderDef(
            key:               "mining.gold_granite_s",
            guildKey:          "mining",
            title:             "Gold Granite Cache",
            description:       "Gold-veined granite is used by the Compact's jewelers as a display surface. Rare and valuable.",
            type:              WorkOrderType.StoneContract,
            requirements:      new() { new(typeof(GoldGranite), 12, "Gold Granite") },
            minStanding:       15000,
            skillRequired:     SkillName.Mining,
            minSkill:          62.0,
            standingReward:    700,
            voucherReward:     30,
            goldReward:        2200,
            requiredDiscovery: "Gold"
        ));

        // ── Master Delver tier: 40,000 Standing ──────────────────────────────

        Register(new WorkOrderDef(
            key:               "mining.agapite_granite_s",
            guildKey:          "mining",
            title:             "Agapite Stone Delivery",
            description:       "Agapite granite's deep purple hue is reserved for the Compact's ceremonial chambers.",
            type:              WorkOrderType.StoneContract,
            requirements:      new() { new(typeof(AgapiteGranite), 10, "Agapite Granite") },
            minStanding:       40000,
            skillRequired:     SkillName.Mining,
            minSkill:          70.0,
            standingReward:    1200,
            voucherReward:     50,
            goldReward:        4000,
            requiredDiscovery: "Agapite"
        ));

        Register(new WorkOrderDef(
            key:               "mining.verite_granite_s",
            guildKey:          "mining",
            title:             "Verite Stone Haul",
            description:       "Verite granite shimmers with an inner light. Only master delvers reach the seams where it forms.",
            type:              WorkOrderType.StoneContract,
            requirements:      new() { new(typeof(VeriteGranite), 8, "Verite Granite") },
            minStanding:       40000,
            skillRequired:     SkillName.Mining,
            minSkill:          75.0,
            standingReward:    1800,
            voucherReward:     72,
            goldReward:        6000,
            requiredDiscovery: "Verite"
        ));

        // ── Deepwarden tier: 80,000 Standing ─────────────────────────────────

        Register(new WorkOrderDef(
            key:               "mining.valorite_granite_s",
            guildKey:          "mining",
            title:             "Valorite Stone Tribute",
            description:       "Valorite granite is the pinnacle of masonry. The Compact vault's inner sanctum is lined with it.",
            type:              WorkOrderType.StoneContract,
            requirements:      new() { new(typeof(ValoriteGranite), 5, "Valorite Granite") },
            minStanding:       80000,
            skillRequired:     SkillName.Mining,
            minSkill:          90.0,
            standingReward:    4000,
            voucherReward:     160,
            goldReward:        12000,
            requiredDiscovery: "Valorite"
        ));
    }

    // ── Society of Smiths order definitions ───────────────────────────────────

    private static void RegisterSocietyOfSmithsOrders()
    {
        // ── Initiate tier: no standing or skill required ──────────────────────

        Register(new WorkOrderDef(
            key:           "smithing.iron_ingots_s",
            guildKey:      "smithing",
            title:         "Iron Ingot Delivery",
            description:   "The forge always needs iron. Deliver a basic supply to prove your dedication.",
            type:          WorkOrderType.ResourceContract,
            requirements:  new() { new(typeof(IronIngot), 250, "Iron Ingots") },
            minStanding:   0,
            skillRequired: null,
            minSkill:      0.0,
            standingReward: 100,
            voucherReward:  5,
            goldReward:     50
        ));

        Register(new WorkOrderDef(
            key:           "smithing.pickaxes_s",
            guildKey:      "smithing",
            title:         "Pickaxe Commission",
            description:   "The Miners' Compact needs tools. Forge a set of pickaxes and deliver them.",
            type:          WorkOrderType.CraftedSupply,
            requirements:  new() { new(typeof(Pickaxe), 5, "Pickaxes") },
            minStanding:   0,
            skillRequired: SkillName.Blacksmith,
            minSkill:      30.0,
            standingReward: 150,
            voucherReward:  8,
            goldReward:     75
        ));

        Register(new WorkOrderDef(
            key:           "smithing.shovels_s",
            guildKey:      "smithing",
            title:         "Shovel Order",
            description:   "Expedition crews need shovels. Shape them and turn them in.",
            type:          WorkOrderType.CraftedSupply,
            requirements:  new() { new(typeof(Shovel), 5, "Shovels") },
            minStanding:   0,
            skillRequired: SkillName.Blacksmith,
            minSkill:      30.0,
            standingReward: 150,
            voucherReward:  8,
            goldReward:     75
        ));

        // ── Apprentice tier: 1,000 Standing + Blacksmith 40+ ─────────────────

        Register(new WorkOrderDef(
            key:           "smithing.smith_hammers_s",
            guildKey:      "smithing",
            title:         "Smith Hammer Supply",
            description:   "We need smith hammers for the journeymen. Forge a batch.",
            type:          WorkOrderType.CraftedSupply,
            requirements:  new() { new(typeof(SmithHammer), 5, "Smith Hammers") },
            minStanding:   1000,
            skillRequired: SkillName.Blacksmith,
            minSkill:      40.0,
            standingReward: 200,
            voucherReward:  10,
            goldReward:     100
        ));

        Register(new WorkOrderDef(
            key:           "smithing.forge_resupply_m",
            guildKey:      "smithing",
            title:         "Forge Resupply Order",
            description:   "A full resupply for the forge yard — ingots, picks, shovels, and hammers.",
            type:          WorkOrderType.CraftedSupply,
            requirements:  new()
            {
                new(typeof(IronIngot),   250, "Iron Ingots"),
                new(typeof(Pickaxe),      10, "Pickaxes"),
                new(typeof(Shovel),       10, "Shovels"),
                new(typeof(SmithHammer),   5, "Smith Hammers"),
            },
            minStanding:   1000,
            skillRequired: SkillName.Blacksmith,
            minSkill:      40.0,
            standingReward: 600,
            voucherReward:  30,
            goldReward:     300
        ));

        // ── Journeyman tier: 5,000 Standing + Blacksmith 50+ ─────────────────

        Register(new WorkOrderDef(
            key:           "smithing.broadswords_s",
            guildKey:      "smithing",
            title:         "Broadsword Commission",
            description:   "The militia has placed a blade order. Craft and deliver three broadswords.",
            type:          WorkOrderType.CraftedSupply,
            requirements:  new() { new(typeof(Broadsword), 3, "Broadswords") },
            minStanding:   5000,
            skillRequired: SkillName.Blacksmith,
            minSkill:      50.0,
            standingReward: 300,
            voucherReward:  15,
            goldReward:     200
        ));

        Register(new WorkOrderDef(
            key:           "smithing.longswords_s",
            guildKey:      "smithing",
            title:         "Longsword Order",
            description:   "Three longswords for the guard rotation. Reliable steel, properly weighted.",
            type:          WorkOrderType.CraftedSupply,
            requirements:  new() { new(typeof(Longsword), 3, "Longswords") },
            minStanding:   5000,
            skillRequired: SkillName.Blacksmith,
            minSkill:      50.0,
            standingReward: 300,
            voucherReward:  15,
            goldReward:     200
        ));

        Register(new WorkOrderDef(
            key:           "smithing.plate_gorgets_s",
            guildKey:      "smithing",
            title:         "Plate Gorget Supply",
            description:   "Throat protection for the town guard. Forge and deliver three plate gorgets.",
            type:          WorkOrderType.CraftedSupply,
            requirements:  new() { new(typeof(PlateGorget), 3, "Plate Gorgets") },
            minStanding:   5000,
            skillRequired: SkillName.Blacksmith,
            minSkill:      50.0,
            standingReward: 300,
            voucherReward:  15,
            goldReward:     200
        ));

        Register(new WorkOrderDef(
            key:           "smithing.militia_arms_m",
            guildKey:      "smithing",
            title:         "Militia Arms Order",
            description:   "Outfit a militia unit — blades, gloves, and shields in one contract.",
            type:          WorkOrderType.CraftedSupply,
            requirements:  new()
            {
                new(typeof(Broadsword),  3, "Broadswords"),
                new(typeof(PlateGorget), 3, "Plate Gorgets"),
                new(typeof(PlateGloves), 3, "Plate Gloves"),
                new(typeof(HeaterShield),3, "Heater Shields"),
            },
            minStanding:   5000,
            skillRequired: SkillName.Blacksmith,
            minSkill:      55.0,
            standingReward: 1200,
            voucherReward:  60,
            goldReward:     600
        ));

        // ── Master tier: 15,000 Standing + Blacksmith 65+ ────────────────────

        Register(new WorkOrderDef(
            key:           "smithing.plate_chest_s",
            guildKey:      "smithing",
            title:         "Plate Chest Commission",
            description:   "A full chest piece takes skill and patience. The Society needs two.",
            type:          WorkOrderType.CraftedSupply,
            requirements:  new() { new(typeof(PlateChest), 2, "Plate Chests") },
            minStanding:   15000,
            skillRequired: SkillName.Blacksmith,
            minSkill:      65.0,
            standingReward: 600,
            voucherReward:  30,
            goldReward:     400
        ));

        Register(new WorkOrderDef(
            key:           "smithing.ingot_stockpile_m",
            guildKey:      "smithing",
            title:         "Ingot Stockpile Order",
            description:   "Stock the Society vault with a mixed ingot delivery.",
            type:          WorkOrderType.ResourceContract,
            requirements:  new()
            {
                new(typeof(IronIngot),       500, "Iron Ingots"),
                new(typeof(DullCopperIngot), 100, "Dull Copper Ingots"),
                new(typeof(BronzeIngot),      50, "Bronze Ingots"),
            },
            minStanding:   15000,
            skillRequired: null,
            minSkill:      0.0,
            standingReward: 1000,
            voucherReward:  50,
            goldReward:     500
        ));

        Register(new WorkOrderDef(
            key:           "smithing.full_plate_set_m",
            guildKey:      "smithing",
            title:         "Full Plate Set Commission",
            description:   "A complete plate set for a senior officer. Every piece must be present.",
            type:          WorkOrderType.CraftedSupply,
            requirements:  new()
            {
                new(typeof(PlateChest),  1, "Plate Chest"),
                new(typeof(PlateGorget), 1, "Plate Gorget"),
                new(typeof(PlateGloves), 1, "Plate Gloves"),
                new(typeof(HeaterShield),1, "Heater Shield"),
            },
            minStanding:   15000,
            skillRequired: SkillName.Blacksmith,
            minSkill:      70.0,
            standingReward: 2000,
            voucherReward:  100,
            goldReward:     1000
        ));

        // ── Grandmaster tier: 50,000 Standing + Blacksmith 80+ ───────────────

        Register(new WorkOrderDef(
            key:           "smithing.valorite_ingots_s",
            guildKey:      "smithing",
            title:         "Valorite Delivery",
            description:   "Only the finest metal will do. The Society requests a Valorite ingot delivery.",
            type:          WorkOrderType.ResourceContract,
            requirements:  new() { new(typeof(ValoriteIngot), 10, "Valorite Ingots") },
            minStanding:   50000,
            skillRequired: null,
            minSkill:      0.0,
            standingReward: 2000,
            voucherReward:  100,
            goldReward:     1500
        ));

        Register(new WorkOrderDef(
            key:           "smithing.grand_armament_m",
            guildKey:      "smithing",
            title:         "Grand Armament Commission",
            description:   "The Society's highest crafted-goods order. Weapons, armor, and tools for the vault.",
            type:          WorkOrderType.CraftedSupply,
            requirements:  new()
            {
                new(typeof(Longsword),   3, "Longswords"),
                new(typeof(PlateChest),  2, "Plate Chests"),
                new(typeof(PlateGorget), 2, "Plate Gorgets"),
                new(typeof(SmithHammer), 5, "Smith Hammers"),
                new(typeof(Pickaxe),     5, "Pickaxes"),
            },
            minStanding:   50000,
            skillRequired: SkillName.Blacksmith,
            minSkill:      80.0,
            standingReward: 5000,
            voucherReward:  250,
            goldReward:     3000
        ));

        // ── Cross-guild: Miners' Compact requests from Smiths ─────────────────

        Register(new WorkOrderDef(
            key:           "smithing.compact_tools_cross",
            guildKey:      "smithing",
            title:         "Compact Tool Commission",
            description:   "The Miners' Compact needs field tools forged to their specifications.",
            type:          WorkOrderType.CraftedSupply,
            requirements:  new()
            {
                new(typeof(Pickaxe), 10, "Pickaxes"),
                new(typeof(Shovel),  10, "Shovels"),
            },
            minStanding:   5000,
            skillRequired: SkillName.Blacksmith,
            minSkill:      50.0,
            standingReward: 800,
            voucherReward:  40,
            goldReward:     400
        ));
    }

    // ── Rangers' League order definitions ─────────────────────────────────────
    //
    // Rangers earn Trail Marks (voucherReward) and Outriders Standing
    // (standingReward) by delivering wilderness resources: leather, hides,
    // feathers, lumber, and arrows.
    //
    // Tier gates mirror the Outriders rank ladder:
    //   Wanderer (0) → Scout (1,000) → Outrider (5,000) → Trailblazer (15,000)
    //
    // Skill gates reflect which rangers are realistically producing
    // the required materials. Higher hides require AnimalTaming to reach
    // the creatures that drop them.

    private static void RegisterRangersOrders()
    {
        // ── Wanderer tier: no standing or skill required ───────────────────────

        Register(new WorkOrderDef(
            key:               "rangers.leather_s",
            guildKey:          "rangers",
            title:             "Leather Delivery",
            description:       "The League's outfitters need basic leather for pack straps and pouches. Deliver a supply.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new() { new(typeof(Leather), 50, "Leather") },
            minStanding:       0,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    75,
            voucherReward:     4,
            goldReward:        60
        ));

        Register(new WorkOrderDef(
            key:               "rangers.raw_bird_s",
            guildKey:          "rangers",
            title:             "Bird Harvest",
            description:       "The League's kitchens need fresh game bird meat. Hunt and deliver a supply.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new() { new(typeof(RawBird), 20, "Raw Birds") },
            minStanding:       0,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    75,
            voucherReward:     4,
            goldReward:        60
        ));

        // ── Scout tier: 1,000 standing ────────────────────────────────────────

        Register(new WorkOrderDef(
            key:               "rangers.feathers_s",
            guildKey:          "rangers",
            title:             "Feather Collection",
            description:       "Fletchers and alchemists both need feathers. Bring in a collected batch from your field work.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new() { new(typeof(Feather), 40, "Feathers") },
            minStanding:       1000,
            skillRequired:     SkillName.AnimalLore,
            minSkill:          20.0,
            standingReward:    150,
            voucherReward:     8,
            goldReward:        150
        ));

        Register(new WorkOrderDef(
            key:               "rangers.spined_leather_s",
            guildKey:          "rangers",
            title:             "Spined Hide Delivery",
            description:       "The League's armorer needs spined leather for medium-grade field gear.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new() { new(typeof(SpinedLeather), 20, "Spined Leather") },
            minStanding:       1000,
            skillRequired:     SkillName.AnimalLore,
            minSkill:          30.0,
            standingReward:    250,
            voucherReward:     12,
            goldReward:        250,
            requiredCreature:  "Ridgeback"   // verify against server scripts: spined-leather animal
        ));

        Register(new WorkOrderDef(
            key:               "rangers.camp_supply_s",
            guildKey:          "rangers",
            title:             "Field Camp Supplies",
            description:       "Rangers in the field need construction materials for camp shelters and rigging.",
            type:              WorkOrderType.ExpeditionContract,
            requirements:      new()
            {
                new(typeof(Board),   50, "Boards"),
                new(typeof(Leather), 30, "Leather"),
            },
            minStanding:       1000,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    300,
            voucherReward:     14,
            goldReward:        300
        ));

        Register(new WorkOrderDef(
            key:               "rangers.wool_s",
            guildKey:          "rangers",
            title:             "Wool Collection",
            description:       "The League's outfitters need raw wool for bedrolls and pack lining. Shear and deliver a supply.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new() { new(typeof(Wool), 20, "Wool") },
            minStanding:       1000,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    150,
            voucherReward:     8,
            goldReward:        120
        ));

        Register(new WorkOrderDef(
            key:               "rangers.mutton_s",
            guildKey:          "rangers",
            title:             "Mutton Delivery",
            description:       "Fresh lamb meat for the League's field kitchens. The camp cooks have a standing order.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new() { new(typeof(RawLambLeg), 20, "Raw Lamb Legs") },
            minStanding:       1000,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    150,
            voucherReward:     8,
            goldReward:        120
        ));

        // ── Outrider tier: 5,000 standing ─────────────────────────────────────

        Register(new WorkOrderDef(
            key:               "rangers.horned_leather_s",
            guildKey:          "rangers",
            title:             "Horned Hide Cache",
            description:       "Horned leather comes from creatures deeper in the wilderness. The armorer will pay well for a batch.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new() { new(typeof(HornedLeather), 15, "Horned Leather") },
            minStanding:       5000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          30.0,
            standingReward:    400,
            voucherReward:     18,
            goldReward:        450,
            requiredCreature:  "Kirin"   // verify against server scripts: horned-leather creature
        ));

        Register(new WorkOrderDef(
            key:               "rangers.wilderness_bounty_m",
            guildKey:          "rangers",
            title:             "Wilderness Bounty",
            description:       "A mixed field haul — spined hides and feathers for the League's combined supply contracts.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new()
            {
                new(typeof(SpinedLeather), 30, "Spined Leather"),
                new(typeof(Feather),       20, "Feathers"),
            },
            minStanding:       5000,
            skillRequired:     SkillName.Archery,
            minSkill:          30.0,
            standingReward:    600,
            voucherReward:     26,
            goldReward:        600
        ));

        Register(new WorkOrderDef(
            key:               "rangers.expedition_lumber_m",
            guildKey:          "rangers",
            title:             "Expedition Lumber Run",
            description:       "Expanded camp construction needs lumber and spined leather lashing. Trackers know where the stands are.",
            type:              WorkOrderType.ExpeditionContract,
            requirements:      new()
            {
                new(typeof(Board),         100, "Boards"),
                new(typeof(SpinedLeather),  20, "Spined Leather"),
            },
            minStanding:       5000,
            skillRequired:     SkillName.Tracking,
            minSkill:          30.0,
            standingReward:    600,
            voucherReward:     26,
            goldReward:        600
        ));

        Register(new WorkOrderDef(
            key:               "rangers.ribs_s",
            guildKey:          "rangers",
            title:             "Game Meat Cache",
            description:       "The League's cooks need ribs from larger game. Hunt and deliver a batch.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new() { new(typeof(RawRibs), 15, "Raw Ribs") },
            minStanding:       5000,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    350,
            voucherReward:     16,
            goldReward:        350
        ));

        Register(new WorkOrderDef(
            key:               "rangers.field_provisions_m",
            guildKey:          "rangers",
            title:             "Field Provisions",
            description:       "A full provisions run for the League's extended camps — birds, mutton, and wool in one delivery.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new()
            {
                new(typeof(RawBird),    15, "Raw Birds"),
                new(typeof(RawLambLeg), 15, "Raw Lamb Legs"),
                new(typeof(Wool),       20, "Wool"),
            },
            minStanding:       5000,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    700,
            voucherReward:     30,
            goldReward:        700
        ));

        // ── Trailblazer tier: 15,000 standing ─────────────────────────────────

        Register(new WorkOrderDef(
            key:               "rangers.barbed_leather_s",
            guildKey:          "rangers",
            title:             "Barbed Hide Delivery",
            description:       "Only the most dangerous creatures carry barbed hides. The League's master armorer needs them.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new() { new(typeof(BarbedLeather), 10, "Barbed Leather") },
            minStanding:       15000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          50.0,
            standingReward:    700,
            voucherReward:     30,
            goldReward:        900,
            requiredCreature:  "Drake"   // verify against server scripts: barbed-leather creature
        ));

        Register(new WorkOrderDef(
            key:               "rangers.deep_territory_m",
            guildKey:          "rangers",
            title:             "Deep Territory Cache",
            description:       "Experienced tamers bring back hides from the deep wilderness that scouts cannot reach.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new()
            {
                new(typeof(HornedLeather), 20, "Horned Leather"),
                new(typeof(SpinedLeather), 30, "Spined Leather"),
            },
            minStanding:       15000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          40.0,
            standingReward:    1000,
            voucherReward:     44,
            goldReward:        1200
        ));

        Register(new WorkOrderDef(
            key:               "rangers.expedition_cache_m",
            guildKey:          "rangers",
            title:             "Expedition Cache",
            description:       "A full expedition resupply — lumber, hides, and feathers for a sustained field operation.",
            type:              WorkOrderType.ExpeditionContract,
            requirements:      new()
            {
                new(typeof(Board),         100, "Boards"),
                new(typeof(SpinedLeather),  50, "Spined Leather"),
                new(typeof(Feather),        20, "Feathers"),
            },
            minStanding:       15000,
            skillRequired:     SkillName.Archery,
            minSkill:          50.0,
            standingReward:    1100,
            voucherReward:     48,
            goldReward:        1500
        ));

        Register(new WorkOrderDef(
            key:               "rangers.wilderness_provisions_m",
            guildKey:          "rangers",
            title:             "Wilderness Provisions Cache",
            description:       "A large provisions run for a sustained campaign — every kind of wilderness fare the League needs.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new()
            {
                new(typeof(RawBird),    20, "Raw Birds"),
                new(typeof(RawLambLeg), 20, "Raw Lamb Legs"),
                new(typeof(RawRibs),    15, "Raw Ribs"),
                new(typeof(Wool),       30, "Wool"),
            },
            minStanding:       15000,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    1200,
            voucherReward:     52,
            goldReward:        1500
        ));

        Register(new WorkOrderDef(
            key:               "rangers.full_predator_cache_m",
            guildKey:          "rangers",
            title:             "Full Predator Cache",
            description:       "The armorer needs hides from the entire predator tier — spined, horned, and barbed in one delivery.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new()
            {
                new(typeof(SpinedLeather), 20, "Spined Leather"),
                new(typeof(HornedLeather), 15, "Horned Leather"),
                new(typeof(BarbedLeather),  8, "Barbed Leather"),
            },
            minStanding:       15000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          55.0,
            standingReward:    1500,
            voucherReward:     65,
            goldReward:        2000
        ));

        // ── Beastmaster tier: 40,000 standing ────────────────────────────────

        Register(new WorkOrderDef(
            key:               "rangers.wool_cache_m",
            guildKey:          "rangers",
            title:             "Wool Reserve",
            description:       "The League's outfitters need a large wool reserve for the cold season. Only patient hunters keep the flocks nearby.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new() { new(typeof(Wool), 80, "Wool") },
            minStanding:       40000,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    2000,
            voucherReward:     80,
            goldReward:        2500
        ));

        Register(new WorkOrderDef(
            key:               "rangers.grand_hunting_tribute",
            guildKey:          "rangers",
            title:             "Grand Hunting Tribute",
            description:       "The highest honour a ranger can earn — a full-spectrum wilderness haul covering every material the League needs.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new()
            {
                new(typeof(Leather),       50, "Leather"),
                new(typeof(SpinedLeather), 30, "Spined Leather"),
                new(typeof(HornedLeather), 15, "Horned Leather"),
                new(typeof(BarbedLeather),  5, "Barbed Leather"),
                new(typeof(Feather),       40, "Feathers"),
                new(typeof(RawBird),       20, "Raw Birds"),
                new(typeof(RawLambLeg),    20, "Raw Lamb Legs"),
                new(typeof(RawRibs),       10, "Raw Ribs"),
                new(typeof(Wool),          30, "Wool"),
            },
            minStanding:       40000,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    5000,
            voucherReward:     200,
            goldReward:        8000
        ));

        // ── Cross-guild: Rangers supply raw hides to the Society of Smiths ────

        Register(new WorkOrderDef(
            key:               "rangers.smiths_hide_cross",
            guildKey:          "rangers",
            title:             "Society of Smiths Hide Order",
            description:       "The smiths need raw materials for leather armor commissions. Supply them directly.",
            type:              WorkOrderType.HuntingContract,
            requirements:      new()
            {
                new(typeof(Leather),       30, "Leather"),
                new(typeof(SpinedLeather), 10, "Spined Leather"),
            },
            minStanding:       1000,
            skillRequired:     null,
            minSkill:          0.0,
            standingReward:    500,
            voucherReward:     22,
            goldReward:        400
        ));

        // ── Taming contracts (delivered via Outrider's Crook) ──────────────────
        // Progress is tracked in WorkOrderEntry.TamingProgress, NOT the backpack.
        // Requirements use WorkOrderTamingRequirement so RequirementsMet checks
        // the delivery counter rather than pack contents.

        // ── Wanderer tier: 30+ taming ─────────────────────────────────────────

        Register(new WorkOrderDef(
            key:               "rangers.tame_horse",
            guildKey:          "rangers",
            title:             "Wild Horse Taming",
            description:       "Wild horses roam the open plains and forests. Tame five and deliver them to the League's stables.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(Horse), 5, "Horses") },
            minStanding:       0,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          29.0,
            standingReward:    200,
            voucherReward:     10,
            goldReward:        150
        ));

        Register(new WorkOrderDef(
            key:               "rangers.tame_blackbear",
            guildKey:          "rangers",
            title:             "Black Bear Survey",
            description:       "Black bears are common in wooded highlands. Tame five for the League's beast handlers.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(BlackBear), 5, "Black Bears") },
            minStanding:       0,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          35.1,
            standingReward:    200,
            voucherReward:     10,
            goldReward:        150
        ));

        Register(new WorkOrderDef(
            key:               "rangers.tame_ostard",
            guildKey:          "rangers",
            title:             "Frenzied Ostard Drive",
            description:       "Frenzied ostards make excellent patrol mounts once broken. Tame three for the League's stable.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(FrenziedOstard), 3, "Frenzied Ostards") },
            minStanding:       0,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          48.0,
            standingReward:    300,
            voucherReward:     14,
            goldReward:        250
        ));

        // ── Scout tier: 1,000 standing + 50 taming ────────────────────────────

        Register(new WorkOrderDef(
            key:               "rangers.tame_wolf",
            guildKey:          "rangers",
            title:             "Wolf Pack Contract",
            description:       "Timber wolves make excellent trackers when properly tamed. Deliver four to the League compound.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(TimberWolf), 4, "Timber Wolves") },
            minStanding:       1000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          50.0,
            standingReward:    400,
            voucherReward:     18,
            goldReward:        300
        ));

        Register(new WorkOrderDef(
            key:               "rangers.tame_ridgeback",
            guildKey:          "rangers",
            title:             "Ridgeback Service",
            description:       "The League's wilderness scouts ride ridgebacks. Tame three to expand the stable.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(Ridgeback), 3, "Ridgebacks") },
            minStanding:       1000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          50.0,
            standingReward:    450,
            voucherReward:     20,
            goldReward:        350
        ));

        // ── Outrider tier: 5,000 standing + 65 taming ────────────────────────

        Register(new WorkOrderDef(
            key:               "rangers.tame_direwolf",
            guildKey:          "rangers",
            title:             "Dire Wolf Procurement",
            description:       "Only experienced tamers can handle dire wolves. Deliver three to the League's war-beast stable.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(DireWolf), 3, "Dire Wolves") },
            minStanding:       5000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          65.0,
            standingReward:    700,
            voucherReward:     30,
            goldReward:        600
        ));

        Register(new WorkOrderDef(
            key:               "rangers.tame_grizzly",
            guildKey:          "rangers",
            title:             "Bear Acquisition",
            description:       "Three grizzly bears for the League's beast handlers. Approach carefully.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(GrizzlyBear), 3, "Grizzly Bears") },
            minStanding:       5000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          65.0,
            standingReward:    700,
            voucherReward:     30,
            goldReward:        600
        ));

        Register(new WorkOrderDef(
            key:               "rangers.tame_beetle",
            guildKey:          "rangers",
            title:             "Pack Beetle Draft",
            description:       "Pack beetles carry enormous loads and make tireless supply animals. Three for the League's logistics corps.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(Beetle), 3, "Pack Beetles") },
            minStanding:       5000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          63.0,
            standingReward:    900,
            voucherReward:     38,
            goldReward:        800
        ));

        // ── Trailblazer tier: 15,000 standing + 80 taming ────────────────────

        Register(new WorkOrderDef(
            key:               "rangers.tame_nightmare",
            guildKey:          "rangers",
            title:             "Nightmare Taming Contract",
            description:       "Nightmares are the mount of legend. Tame two and deliver them — the League will put them to use.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(Nightmare), 2, "Nightmares") },
            minStanding:       15000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          80.0,
            standingReward:    1500,
            voucherReward:     60,
            goldReward:        1500,
            requiredCreature:  "Nightmare"
        ));

        Register(new WorkOrderDef(
            key:               "rangers.tame_kirin",
            guildKey:          "rangers",
            title:             "Kirin Concord",
            description:       "The noble kirin is rarely seen and rarer still to tame. Two specimens for the League's honor guard.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(Kirin), 2, "Kirins") },
            minStanding:       15000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          80.0,
            standingReward:    1500,
            voucherReward:     60,
            goldReward:        1500,
            requiredCreature:  "Kirin"
        ));

        // ── Beastmaster tier: 40,000 standing + 90+ taming ──────────────────

        Register(new WorkOrderDef(
            key:               "rangers.tame_white_wyrm",
            guildKey:          "rangers",
            title:             "White Wyrm Contract",
            description:       "A white wyrm for the League's war-beast handlers. Cold-blooded, calculated, and devastating in the field.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(WhiteWyrm), 1, "White Wyrm") },
            minStanding:       40000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          90.0,
            standingReward:    3000,
            voucherReward:     120,
            goldReward:        4000,
            requiredCreature:  "WhiteWyrm"
        ));

        Register(new WorkOrderDef(
            key:               "rangers.tame_greater_dragon",
            guildKey:          "rangers",
            title:             "Dragon Ward",
            description:       "One greater dragon for the League's vaults. The hazard bonus is substantial.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(GreaterDragon), 1, "Greater Dragon") },
            minStanding:       40000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          95.0,
            standingReward:    4000,
            voucherReward:     150,
            goldReward:        5000,
            requiredCreature:  "GreaterDragon"
        ));

        Register(new WorkOrderDef(
            key:               "rangers.tame_cusidhe",
            guildKey:          "rangers",
            title:             "Cu Sidhe Compact",
            description:       "The great hound of legend. One Cu Sidhe for the League's most elite scouts.",
            type:              WorkOrderType.TamingContract,
            requirements:      new() { new WorkOrderTamingRequirement(typeof(CuSidhe), 1, "Cu Sidhe") },
            minStanding:       40000,
            skillRequired:     SkillName.AnimalTaming,
            minSkill:          99.0,
            standingReward:    5000,
            voucherReward:     180,
            goldReward:        6000,
            requiredCreature:  "CuSidhe"
        ));
    }

    // ── Foresters' Union order definitions ────────────────────────────────────
    //
    // Foresters earn Timber Tokens (voucherReward) and Foresters' Standing
    // (standingReward) by delivering timber of increasing quality.
    //
    // Tier gates mirror the Foresters' rank ladder:
    //   Woodcutter (0) → Sawyer (1,000) → Forester (5,000) → Arborist (15,000)
    //   Grove Warden (40,000) → Master of the Wood (80,000)
    //
    // Wood type progression:
    //   Common Boards → Oak/Ash → Yew → Heartwood → Bloodwood/Frostwood

    private static void RegisterForestersOrders()
    {
        // ── Woodcutter tier: no standing or skill required ────────────────────

        Register(new WorkOrderDef(
            key:            "foresters.boards_s",
            guildKey:       "foresters",
            title:          "Timber Delivery",
            description:    "Deliver a standard supply of boards to the Union depot.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(Board), 50, "Boards") },
            minStanding:    0,
            skillRequired:  null,
            minSkill:       0.0,
            standingReward: 75,
            voucherReward:  4,
            goldReward:     60
        ));

        Register(new WorkOrderDef(
            key:            "foresters.boards_m",
            guildKey:       "foresters",
            title:          "Timber Bulk Order",
            description:    "A larger timber delivery for the depot's construction stockpile. More effort, better pay.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(Board), 250, "Boards") },
            minStanding:    0,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       30.0,
            standingReward: 600,
            voucherReward:  30,
            goldReward:     350
        ));

        // ── Sawyer tier: 1,000 standing ───────────────────────────────────────

        Register(new WorkOrderDef(
            key:            "foresters.oak_boards_s",
            guildKey:       "foresters",
            title:          "Oak Timber Delivery",
            description:    "Oak boards for the Union's quality joinery contracts. Bring a supply.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(OakBoard), 30, "Oak Boards") },
            minStanding:    1000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       40.0,
            standingReward: 250,
            voucherReward:  12,
            goldReward:     350
        ));

        Register(new WorkOrderDef(
            key:            "foresters.ash_boards_s",
            guildKey:       "foresters",
            title:          "Ash Timber Delivery",
            description:    "Ash boards are in demand at the depot for tool handles and medium-grade construction.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(AshBoard), 30, "Ash Boards") },
            minStanding:    1000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       40.0,
            standingReward: 250,
            voucherReward:  12,
            goldReward:     350
        ));

        Register(new WorkOrderDef(
            key:            "foresters.camp_lumber_s",
            guildKey:       "foresters",
            title:          "Field Camp Lumber",
            description:    "Rangers' camps need common boards for shelters and rigging. Supply the Rangers' League.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(Board), 100, "Boards") },
            minStanding:    1000,
            skillRequired:  null,
            minSkill:       0.0,
            standingReward: 300,
            voucherReward:  14,
            goldReward:     300
        ));

        // ── Forester tier: 5,000 standing ─────────────────────────────────────

        Register(new WorkOrderDef(
            key:            "foresters.yew_boards_s",
            guildKey:       "foresters",
            title:          "Yew Consignment",
            description:    "Yew is the bowyer's choice and commands a premium from the Union's trade partners.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(YewBoard), 20, "Yew Boards") },
            minStanding:    5000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       50.0,
            standingReward: 400,
            voucherReward:  18,
            goldReward:     700
        ));

        Register(new WorkOrderDef(
            key:            "foresters.mixed_timber_m",
            guildKey:       "foresters",
            title:          "Mixed Timber Commission",
            description:    "The carpenter's guild wants a varied timber delivery — common stock and specialty grades combined.",
            type:           WorkOrderType.TimberContract,
            requirements:   new()
            {
                new(typeof(Board),    100, "Boards"),
                new(typeof(OakBoard),  30, "Oak Boards"),
                new(typeof(AshBoard),  20, "Ash Boards"),
            },
            minStanding:    5000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       50.0,
            standingReward: 900,
            voucherReward:  40,
            goldReward:     800
        ));

        // ── Arborist tier: 15,000 standing ────────────────────────────────────

        Register(new WorkOrderDef(
            key:            "foresters.heartwood_s",
            guildKey:       "foresters",
            title:          "Heartwood Delivery",
            description:    "Heartwood comes from the oldest forest giants. The Union pays well for every board.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(HeartwoodBoard), 15, "Heartwood Boards") },
            minStanding:    15000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       65.0,
            standingReward: 700,
            voucherReward:  30,
            goldReward:     1500
        ));

        Register(new WorkOrderDef(
            key:            "foresters.quality_timber_m",
            guildKey:       "foresters",
            title:          "Quality Timber Cache",
            description:    "A combined quality timber delivery for the master carpenters' commission board.",
            type:           WorkOrderType.TimberContract,
            requirements:   new()
            {
                new(typeof(YewBoard),       30, "Yew Boards"),
                new(typeof(HeartwoodBoard), 15, "Heartwood Boards"),
            },
            minStanding:    15000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       60.0,
            standingReward: 1200,
            voucherReward:  52,
            goldReward:     2000
        ));

        // ── Grove Warden tier: 40,000 standing ───────────────────────────────

        Register(new WorkOrderDef(
            key:            "foresters.bloodwood_s",
            guildKey:       "foresters",
            title:          "Bloodwood Haul",
            description:    "Bloodwood grows in the deepest groves and fetches the highest prices. Only veteran foresters find it.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(BloodwoodBoard), 10, "Bloodwood Boards") },
            minStanding:    40000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       75.0,
            standingReward: 1500,
            voucherReward:  60,
            goldReward:     4000
        ));

        Register(new WorkOrderDef(
            key:            "foresters.frostwood_s",
            guildKey:       "foresters",
            title:          "Frostwood Cache",
            description:    "Frostwood has an icy sheen and is prized by enchanters. The Union has a standing buyer.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(FrostwoodBoard), 8, "Frostwood Boards") },
            minStanding:    40000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       75.0,
            standingReward: 1500,
            voucherReward:  60,
            goldReward:     4000
        ));

        Register(new WorkOrderDef(
            key:            "foresters.rare_timber_m",
            guildKey:       "foresters",
            title:          "Rare Timber Commission",
            description:    "The finest timber the wilderness yields — bloodwood, frostwood, and heartwood for the vault.",
            type:           WorkOrderType.TimberContract,
            requirements:   new()
            {
                new(typeof(BloodwoodBoard),  10, "Bloodwood Boards"),
                new(typeof(FrostwoodBoard),   8, "Frostwood Boards"),
                new(typeof(HeartwoodBoard),  10, "Heartwood Boards"),
            },
            minStanding:    40000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       75.0,
            standingReward: 2800,
            voucherReward:  112,
            goldReward:     6000
        ));

        // ── Master of the Wood tier: 80,000 standing ─────────────────────────

        Register(new WorkOrderDef(
            key:            "foresters.grand_tribute",
            guildKey:       "foresters",
            title:          "Grand Forest Tribute",
            description:    "The highest tribute a Forester can give — every grade of timber for the Union's ceremonial vault.",
            type:           WorkOrderType.TimberContract,
            requirements:   new()
            {
                new(typeof(Board),           100, "Boards"),
                new(typeof(YewBoard),         50, "Yew Boards"),
                new(typeof(HeartwoodBoard),   20, "Heartwood Boards"),
                new(typeof(BloodwoodBoard),    8, "Bloodwood Boards"),
                new(typeof(FrostwoodBoard),    5, "Frostwood Boards"),
            },
            minStanding:    80000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       90.0,
            standingReward: 5000,
            voucherReward:  200,
            goldReward:     10000
        ));

        // ── Cross-guild: Foresters supply lumber to the Rangers' League ───────

        Register(new WorkOrderDef(
            key:            "foresters.rangers_supply_cross",
            guildKey:       "foresters",
            title:          "Rangers' Field Lumber",
            description:    "The Rangers' League needs construction timber for their field camps and expedition depots.",
            type:           WorkOrderType.TimberContract,
            requirements:   new()
            {
                new(typeof(Board),    100, "Boards"),
                new(typeof(OakBoard),  30, "Oak Boards"),
            },
            minStanding:    1000,
            skillRequired:  null,
            minSkill:       0.0,
            standingReward: 500,
            voucherReward:  22,
            goldReward:     400
        ));

        // ── Extended lumber: discovery-gated, Grove Warden tier+ ─────────────

        Register(new WorkOrderDef(
            key:            "foresters.ironwood_s",
            guildKey:       "foresters",
            title:          "Ironwood Consignment",
            description:    "The smiths want ironwood for tool handles — harder than heartwood and doesn't splinter.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(IronwoodBoard), 8, "Ironwood Boards") },
            minStanding:    40000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       100.0,
            standingReward: 1800,
            voucherReward:  70,
            goldReward:     3500,
            requiredWoodDiscovery: "Ironwood"
        ));

        Register(new WorkOrderDef(
            key:            "foresters.ghostwood_s",
            guildKey:       "foresters",
            title:          "Ghostwood Consignment",
            description:    "Healers prize ghostwood for ritual implements. Its pale grain unnerves the uninitiated.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(GhostwoodBoard), 6, "Ghostwood Boards") },
            minStanding:    40000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       100.0,
            standingReward: 2000,
            voucherReward:  80,
            goldReward:     4000,
            requiredWoodDiscovery: "Ghostwood"
        ));

        Register(new WorkOrderDef(
            key:            "foresters.emberbark_s",
            guildKey:       "foresters",
            title:          "Emberbark Consignment",
            description:    "Emberbark burns without ash and holds heat for hours. The forge masters want it.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(EmberbarkBoard), 6, "Emberbark Boards") },
            minStanding:    40000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       100.0,
            standingReward: 2000,
            voucherReward:  80,
            goldReward:     4000,
            requiredWoodDiscovery: "Emberbark"
        ));

        Register(new WorkOrderDef(
            key:            "foresters.frostbark_s",
            guildKey:       "foresters",
            title:          "Frostbark Consignment",
            description:    "Frostbark keeps its chill even hewn and dried. Useful for cold-storage construction.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(FrostbarkBoard), 5, "Frostbark Boards") },
            minStanding:    40000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       100.0,
            standingReward: 2200,
            voucherReward:  88,
            goldReward:     4500,
            requiredWoodDiscovery: "Frostbark"
        ));

        Register(new WorkOrderDef(
            key:            "foresters.shadowbark_s",
            guildKey:       "foresters",
            title:          "Shadowbark Consignment",
            description:    "Shadowbark absorbs light. The tinkers have architectural uses for it nobody explains.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(ShadowbarkBoard), 5, "Shadowbark Boards") },
            minStanding:    80000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       100.0,
            standingReward: 2500,
            voucherReward:  100,
            goldReward:     5000,
            requiredWoodDiscovery: "Shadowbark"
        ));

        Register(new WorkOrderDef(
            key:            "foresters.runewood_s",
            guildKey:       "foresters",
            title:          "Runewood Consignment",
            description:    "Runewood holds enchantment better than any other wood. The arcane scholars will pay well.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(RunewoodBoard), 4, "Runewood Boards") },
            minStanding:    80000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       100.0,
            standingReward: 3000,
            voucherReward:  120,
            goldReward:     6000,
            requiredWoodDiscovery: "Runewood"
        ));

        Register(new WorkOrderDef(
            key:            "foresters.voidwood_s",
            guildKey:       "foresters",
            title:          "Voidwood Consignment",
            description:    "Voidwood silences the air around it. The mages have asked for it three times and won't say why.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(VoidwoodBoard), 3, "Voidwood Boards") },
            minStanding:    80000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       100.0,
            standingReward: 3500,
            voucherReward:  140,
            goldReward:     7000,
            requiredWoodDiscovery: "Voidwood"
        ));

        Register(new WorkOrderDef(
            key:            "foresters.starwood_s",
            guildKey:       "foresters",
            title:          "Starwood Consignment",
            description:    "Starwood gleams faintly in the dark. The rarest timber in Britannia — a single plank is worth a fortune.",
            type:           WorkOrderType.TimberContract,
            requirements:   new() { new(typeof(StarwoodBoard), 2, "Starwood Boards") },
            minStanding:    80000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       100.0,
            standingReward: 4000,
            voucherReward:  160,
            goldReward:     8000,
            requiredWoodDiscovery: "Starwood"
        ));

        Register(new WorkOrderDef(
            key:            "foresters.exotic_timber_m",
            guildKey:       "foresters",
            title:          "Exotic Timber Collection",
            description:    "A mixed consignment of rare and exotic woods for the League's finest craftsmen.",
            type:           WorkOrderType.TimberContract,
            requirements:   new()
            {
                new(typeof(IronwoodBoard),  3, "Ironwood Boards"),
                new(typeof(EmberbarkBoard), 2, "Emberbark Boards"),
                new(typeof(RunewoodBoard),  2, "Runewood Boards"),
            },
            minStanding:    80000,
            skillRequired:  SkillName.Lumberjacking,
            minSkill:       100.0,
            standingReward: 4500,
            voucherReward:  180,
            goldReward:     9000,
            requiredWoodDiscovery: "Ironwood"
        ));
    }

    // ── Custodians order definitions ──────────────────────────────────────────
    //
    // Civic Contracts use CleanedDebris (civic waste bundles) as the turn-in
    // currency.  Bundles are earned at a rate of 1 per 5 items cleaned in any
    // batch operation ([cleanupall or TrashBag dump).
    //
    // Five tiers aligned to the Custodian rank ladder:
    //   Volunteer (0) → Junior Custodian (100) → Custodian (500)
    //   → Senior Custodian (2,000) → Chief Custodian (5,000)

    private static void RegisterCustodiansOrders()
    {
        // ── Volunteer tier: no standing required ──────────────────────────────

        Register(new WorkOrderDef(
            key:            "custodians.refuse_collection",
            guildKey:       "custodians",
            title:          "Refuse Collection",
            description:    "The Sanitation Warden needs proof of cleanup effort. Deliver a bundle of civic waste.",
            type:           WorkOrderType.CivicContract,
            requirements:   new() { new(typeof(CleanedDebris), 5, "Civic Waste Bundles") },
            minStanding:    0,
            skillRequired:  null,
            minSkill:       0.0,
            standingReward: 200,
            voucherReward:  10,
            goldReward:     100
        ));

        // ── Junior Custodian tier: 100 standing ───────────────────────────────

        Register(new WorkOrderDef(
            key:            "custodians.sanitation_detail",
            guildKey:       "custodians",
            title:          "Sanitation Detail",
            description:    "A district supervisor has posted a cleanup order. Deliver a larger batch of civic waste bundles.",
            type:           WorkOrderType.CivicContract,
            requirements:   new() { new(typeof(CleanedDebris), 15, "Civic Waste Bundles") },
            minStanding:    100,
            skillRequired:  null,
            minSkill:       0.0,
            standingReward: 600,
            voucherReward:  30,
            goldReward:     300
        ));

        // ── Custodian tier: 500 standing ──────────────────────────────────────

        Register(new WorkOrderDef(
            key:            "custodians.district_cleanup",
            guildKey:       "custodians",
            title:          "District Cleanup",
            description:    "A full district sweep is required. This is steady work for a committed Custodian.",
            type:           WorkOrderType.CivicContract,
            requirements:   new() { new(typeof(CleanedDebris), 40, "Civic Waste Bundles") },
            minStanding:    500,
            skillRequired:  null,
            minSkill:       0.0,
            standingReward: 1600,
            voucherReward:  80,
            goldReward:     750
        ));

        // ── Senior Custodian tier: 2,000 standing ─────────────────────────────

        Register(new WorkOrderDef(
            key:            "custodians.ward_sanitation",
            guildKey:       "custodians",
            title:          "Ward Sanitation Contract",
            description:    "A major city ward contract. Only experienced Custodians can handle the volume required.",
            type:           WorkOrderType.CivicContract,
            requirements:   new() { new(typeof(CleanedDebris), 80, "Civic Waste Bundles") },
            minStanding:    2000,
            skillRequired:  null,
            minSkill:       0.0,
            standingReward: 3500,
            voucherReward:  175,
            goldReward:     1500
        ));

        // ── Chief Custodian tier: 5,000 standing ──────────────────────────────

        Register(new WorkOrderDef(
            key:            "custodians.grand_sanitation",
            guildKey:       "custodians",
            title:          "Grand Sanitation Commission",
            description:    "Britannia's highest civic honour. Reserved for Chief Custodians whose dedication is beyond question.",
            type:           WorkOrderType.CivicContract,
            requirements:   new() { new(typeof(CleanedDebris), 200, "Civic Waste Bundles") },
            minStanding:    5000,
            skillRequired:  null,
            minSkill:       0.0,
            standingReward: 10000,
            voucherReward:  500,
            goldReward:     4000
        ));
    }
}
