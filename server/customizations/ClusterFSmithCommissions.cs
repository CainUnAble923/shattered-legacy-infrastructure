using System;
using System.Collections.Generic;
using Server.Accounting;
using Server.Commands;
using Server.Engines.BulkOrders;
using Server.Items;
using Server.Mobiles;

namespace Server;

// ─────────────────────────────────────────────────────────────────────────────
// ClusterF Smith Commissions — Phase 4C-ii
//
// Dynamically generated single-piece crafting requests commissioned by named
// adventurers, guards, and mercenaries.  Distinct from BODs (bulk counts) and
// Guild Contracts (static supply orders).
//
// Flow:
//   1. Player opens the Commissions gump from the Guildmaster.
//   2. "Request New Commission" generates a random commission scaled to skill.
//   3. Player crafts the requested item (type + material + optional exceptional).
//   4. Turn in: drag to the Guildmaster, or use the "Turn In" button in the gump.
//   5. Rewards: Smithing Seals + standing + skill check.
//
// Limits: max 3 active commissions, separate from the BOD cap.
//
// Future: cross-guild hook — Warriors/Mercenary guild will eventually post
// commissions here automatically.  RequesterGuild field reserved for that.
// ─────────────────────────────────────────────────────────────────────────────

// ── Serializable commission entry ─────────────────────────────────────────────

public class SmithCommissionEntry
{
    public string        Id                { get; }
    public string        ItemKey           { get; }   // key into SmithCommissionPool
    public CraftResource Material          { get; }
    public bool          RequireExceptional{ get; }
    public string        RequesterName     { get; }
    public string        RequesterNote     { get; }
    public int           SealReward        { get; }
    public int           StandingReward    { get; }
    public DateTime      IssuedAt          { get; }

    // Derived (not serialized — looked up from pool at runtime)
    public Type?   ItemType  => SmithCommissionPool.GetItemType(ItemKey);
    public string  ItemLabel => SmithCommissionPool.GetItemLabel(ItemKey);

    public string FullLabel
    {
        get
        {
            var mat  = SmithCommissionSystem.MaterialName(Material);
            var qual = RequireExceptional ? "Exceptional " : "";
            return $"{qual}{mat} {ItemLabel}";
        }
    }

    public SmithCommissionEntry(
        string id, string itemKey, CraftResource material, bool requireExceptional,
        string requesterName, string requesterNote, int sealReward, int standingReward)
    {
        Id                 = id;
        ItemKey            = itemKey;
        Material           = material;
        RequireExceptional = requireExceptional;
        RequesterName      = requesterName;
        RequesterNote      = requesterNote;
        SealReward         = sealReward;
        StandingReward     = standingReward;
        IssuedAt           = DateTime.UtcNow;
    }

    // Deserialization
    public SmithCommissionEntry(IGenericReader r)
    {
        r.ReadInt(); // version 0
        Id                 = r.ReadString();
        ItemKey            = r.ReadString();
        Material           = (CraftResource)r.ReadInt();
        RequireExceptional = r.ReadBool();
        RequesterName      = r.ReadString();
        RequesterNote      = r.ReadString();
        SealReward         = r.ReadInt();
        StandingReward     = r.ReadInt();
        IssuedAt           = r.ReadDateTime();
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(0); // version
        w.Write(Id);
        w.Write(ItemKey);
        w.Write((int)Material);
        w.Write(RequireExceptional);
        w.Write(RequesterName);
        w.Write(RequesterNote);
        w.Write(SealReward);
        w.Write(StandingReward);
        w.Write(IssuedAt);
    }
}

// ── Static item pool ──────────────────────────────────────────────────────────

public static class SmithCommissionPool
{
    private record PoolEntry(Type ItemType, string Label, bool IsArmor);

    private static readonly Dictionary<string, PoolEntry> _items = new()
    {
        // ── Weapons ───────────────────────────────────────────────────────
        { "longsword",      new(typeof(Longsword),      "Longsword",     false) },
        { "broadsword",     new(typeof(Broadsword),     "Broadsword",    false) },
        { "katana",         new(typeof(Katana),         "Katana",        false) },
        { "kryss",          new(typeof(Kryss),          "Kryss",         false) },
        { "war_mace",       new(typeof(WarMace),        "War Mace",      false) },
        { "war_hammer",     new(typeof(WarHammer),      "War Hammer",    false) },
        { "axe",            new(typeof(Axe),            "Axe",           false) },
        { "battle_axe",     new(typeof(BattleAxe),      "Battle Axe",    false) },
        { "two_hand_axe",   new(typeof(TwoHandedAxe),   "Two-Handed Axe",false) },
        { "halberd",        new(typeof(Halberd),        "Halberd",       false) },
        { "bardiche",       new(typeof(Bardiche),       "Bardiche",      false) },
        { "spear",          new(typeof(Spear),          "Spear",         false) },
        { "short_spear",    new(typeof(ShortSpear),     "Short Spear",   false) },
        // ── Armor ─────────────────────────────────────────────────────────
        { "plate_chest",    new(typeof(PlateChest),     "Plate Chest",    true)  },
        { "plate_arms",     new(typeof(PlateArms),      "Plate Arms",     true)  },
        { "plate_legs",     new(typeof(PlateLegs),      "Plate Legs",     true)  },
        { "plate_helm",     new(typeof(PlateHelm),      "Plate Helm",     true)  },
        { "plate_gorget",   new(typeof(PlateGorget),    "Plate Gorget",   true)  },
        { "plate_gloves",   new(typeof(PlateGloves),    "Plate Gloves",   true)  },
        { "chain_chest",    new(typeof(ChainChest),     "Chain Chest",    true)  },
        { "chain_legs",     new(typeof(ChainLegs),      "Chain Legs",     true)  },
        { "ringmail_chest", new(typeof(RingmailChest),  "Ringmail Chest", true)  },
        { "ringmail_arms",  new(typeof(RingmailArms),   "Ringmail Arms",  true)  },
        { "ringmail_legs",  new(typeof(RingmailLegs),   "Ringmail Legs",  true)  },
        { "ringmail_gloves",new(typeof(RingmailGloves), "Ringmail Gloves",true)  },
        // ── Female plate ───────────────────────────────────────────────────
        { "female_plate_chest", new(typeof(FemalePlateChest), "Female Plate Chest", true) },
        // ── Samurai plate ──────────────────────────────────────────────────
        { "plate_do",              new(typeof(PlateDo),             "Plate Do",              true) },
        { "plate_suneate",         new(typeof(PlateSuneate),        "Plate Suneate",         true) },
        { "plate_haidate",         new(typeof(PlateHaidate),        "Plate Haidate",         true) },
        { "standard_plate_kabuto", new(typeof(StandardPlateKabuto), "Standard Plate Kabuto", true) },
        // ── Dragon scale ───────────────────────────────────────────────────
        // Resource = CraftResource.RedScales — IgnoreMaterial must be true on the CommissionSet.
        { "dragon_chest",  new(typeof(DragonChest),  "Dragon Scale Tunic",   true) },
        { "dragon_arms",   new(typeof(DragonArms),   "Dragon Scale Arms",    true) },
        { "dragon_legs",   new(typeof(DragonLegs),   "Dragon Scale Legs",    true) },
        { "dragon_gloves", new(typeof(DragonGloves), "Dragon Scale Gloves",  true) },
        { "dragon_helm",   new(typeof(DragonHelm),   "Dragon Scale Helm",    true) },
    };

    private static readonly string[] WeaponKeys;
    private static readonly string[] ArmorKeys;
    private static readonly string[] AllKeys;

    static SmithCommissionPool()
    {
        var weapons = new List<string>();
        var armors  = new List<string>();
        foreach (var (key, entry) in _items)
        {
            if (entry.IsArmor) armors.Add(key);
            else               weapons.Add(key);
        }
        WeaponKeys = weapons.ToArray();
        ArmorKeys  = armors.ToArray();
        AllKeys    = new string[WeaponKeys.Length + ArmorKeys.Length];
        WeaponKeys.CopyTo(AllKeys, 0);
        ArmorKeys.CopyTo(AllKeys, WeaponKeys.Length);
    }

    public static Type?  GetItemType (string key) => _items.TryGetValue(key, out var e) ? e.ItemType : null;
    public static string GetItemLabel(string key) => _items.TryGetValue(key, out var e) ? e.Label    : key;

    /// <summary>
    /// Picks a random item key.
    /// Weapons weighted 2:1 over armor (adventurers want blades more than plate).
    /// </summary>
    public static string RandomKey()
    {
        if (Utility.RandomDouble() < 0.65)
            return WeaponKeys[Utility.Random(WeaponKeys.Length)];
        return ArmorKeys[Utility.Random(ArmorKeys.Length)];
    }
}

// ── Requester flavor pool ─────────────────────────────────────────────────────

public static class SmithRequesterPool
{
    private record Requester(string Name, string[] Notes);

    // Notes are picked randomly per commission, flavored to hint at cross-guild
    // integration with Warriors/Mercenaries coming in a later phase.
    private static readonly Requester[] _pool =
    {
        new("Sir Aldric the Wanderer",
            new[]
            {
                "I head north before the week is out. I cannot go without a proper blade.",
                "The Society was recommended. I hope the reputation holds.",
                "I've fought with inferior steel before. Never again.",
            }),
        new("Captain Mira, City Guard",
            new[]
            {
                "My garrison is re-equipping. The militia commander wants it done quietly.",
                "The town watch needs proper arms, not market trash.",
                "I inspect every piece myself. Do not disappoint me.",
            }),
        new("Dirk the Hired Blade",
            new[]
            {
                "A contract requires specific equipment. I pay well for quality.",
                "My last smith retired. Someone vouched for the Society's work.",
                "I can't walk into a dungeon carrying iron rubbish.",
            }),
        new("Sergeant Brennan",
            new[]
            {
                "We lost half our kit in the last skirmish. I need replacements fast.",
                "The captain doesn't know I'm here. I'd like it to stay that way.",
                "My squad's lives depend on what you make. Remember that.",
            }),
        new("Lady Thessalin of Trinsic",
            new[]
            {
                "I've heard the Society does fine work. Prove it.",
                "This is for a ceremony, but it must be functional as well.",
                "I travel alone. I prefer to be the most dangerous thing on the road.",
            }),
        new("Kosta, Mercenary Captain",
            new[]
            {
                "My company ships out in two days. This cannot wait.",
                "I've tried the market stalls. Inferior work, all of it.",
                "Pay me in steel, and I'll tell my crew where to come for theirs.",
            }),
        new("Wren the Expedition Scout",
            new[]
            {
                "The ruins near Despise are worse than last season. I need better gear.",
                "I've heard the monsters there now carry their own weapons. Reassuring.",
                "Light enough to move fast, strong enough to matter.",
            }),
        new("Brother Aldous, Temple Guard",
            new[]
            {
                "The temple needs its protectors properly armed.",
                "Faith is strength, but good steel doesn't hurt.",
                "I've heard your guild's work holds up on pilgrimage routes. Good.",
            }),
    };

    public static (string Name, string Note) Random()
    {
        var r = _pool[Utility.Random(_pool.Length)];
        return (r.Name, r.Notes[Utility.Random(r.Notes.Length)]);
    }
}

// ── Armor set pool for large commissions ─────────────────────────────────────

public static class SmithCommissionSetPool
{
    /// <param name="IgnoreMaterial">
    /// When true, material/resource is NOT checked during piece turn-in.
    /// Use for armor that has a non-metal resource (e.g. DragonScale = RedScales).
    /// </param>
    /// <param name="RewardMultiplier">
    /// Multiplies the base seal/standing reward for the entire set.
    /// Default 1.0; use > 1.0 for special/exotic sets.
    /// </param>
    public record CommissionSet(
        string   Key,
        string   Label,
        string[] ItemKeys,
        bool     IgnoreMaterial   = false,
        double   RewardMultiplier = 1.0);

    private static readonly CommissionSet[] _sets =
    {
        // Light Archer: ring arms/gloves + chain chest/legs
        new("light_archer",  "Light Archer Armor",
            new[] { "ringmail_arms", "ringmail_gloves", "chain_chest", "chain_legs" }),

        // Heavy Archer: plate arms/legs/gorget/gloves + chain chest
        new("heavy_archer",  "Heavy Archer Armor",
            new[] { "plate_arms", "plate_legs", "plate_gorget", "plate_gloves", "chain_chest" }),

        // Full Plate: chest + arms + legs + gorget + gloves
        new("full_plate",    "Full Plate Armor",
            new[] { "plate_chest", "plate_arms", "plate_legs", "plate_gorget", "plate_gloves" }),

        // Ringmail Set: chest + arms + legs + gloves
        new("ringmail_set",  "Ringmail Set",
            new[] { "ringmail_chest", "ringmail_arms", "ringmail_legs", "ringmail_gloves" }),

        // Female Full Plate: female chest + standard plate arms/legs/gorget/gloves
        new("female_plate",  "Female Full Plate",
            new[] { "female_plate_chest", "plate_arms", "plate_legs", "plate_gorget", "plate_gloves" }),

        // Samurai Plate: Do (chest) + Suneate (legs) + Haidate (skirt) + Standard Kabuto (helm)
        new("samurai_plate", "Samurai Plate Armor",
            new[] { "plate_do", "plate_suneate", "plate_haidate", "standard_plate_kabuto" }),

        // Dragon Scale: all five pieces — material check bypassed (resource = RedScales, not metal)
        // Extra reward: 2.5× multiplier to reflect rarity of dragon scale materials.
        new("dragon_scale",  "Dragon Scale Armor",
            new[] { "dragon_chest", "dragon_arms", "dragon_legs", "dragon_gloves", "dragon_helm" },
            IgnoreMaterial: true, RewardMultiplier: 2.5),
    };

    private static readonly Dictionary<string, CommissionSet> _byKey;

    static SmithCommissionSetPool()
    {
        _byKey = new Dictionary<string, CommissionSet>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in _sets) _byKey[s.Key] = s;
    }

    public static CommissionSet? GetSet(string key) =>
        _byKey.TryGetValue(key, out var s) ? s : null;

    public static CommissionSet RandomSet() =>
        _sets[Utility.Random(_sets.Length)];
}

// ── Large commission entry ────────────────────────────────────────────────────

public class SmithLargeCommissionEntry
{
    public string        Id                { get; }
    public string        SetKey            { get; }
    public CraftResource Material          { get; }
    public bool          RequireExceptional{ get; }
    public string        RequesterName     { get; }
    public string        RequesterNote     { get; }
    public int           SealReward        { get; }
    public int           StandingReward    { get; }
    public DateTime      IssuedAt          { get; }
    public List<string>  FulfilledPieces   { get; } = new(); // itemKeys already turned in

    public SmithCommissionSetPool.CommissionSet? SetDef => SmithCommissionSetPool.GetSet(SetKey);
    public string SetLabel   => SetDef?.Label ?? SetKey;
    public bool   Complete   => FulfilledPieces.Count >= (SetDef?.ItemKeys.Length ?? 999);

    public string FullLabel
    {
        get
        {
            var mat  = SmithCommissionSystem.MaterialName(Material);
            var qual = RequireExceptional ? "Exceptional " : "";
            return $"{qual}{mat} {SetLabel}";
        }
    }

    public SmithLargeCommissionEntry(
        string id, string setKey, CraftResource material, bool requireExceptional,
        string requesterName, string requesterNote, int sealReward, int standingReward)
    {
        Id                 = id;
        SetKey             = setKey;
        Material           = material;
        RequireExceptional = requireExceptional;
        RequesterName      = requesterName;
        RequesterNote      = requesterNote;
        SealReward         = sealReward;
        StandingReward     = standingReward;
        IssuedAt           = DateTime.UtcNow;
    }

    public SmithLargeCommissionEntry(IGenericReader r)
    {
        r.ReadInt(); // version 0
        Id                 = r.ReadString();
        SetKey             = r.ReadString();
        Material           = (CraftResource)r.ReadInt();
        RequireExceptional = r.ReadBool();
        RequesterName      = r.ReadString();
        RequesterNote      = r.ReadString();
        SealReward         = r.ReadInt();
        StandingReward     = r.ReadInt();
        IssuedAt           = r.ReadDateTime();
        var pCount = r.ReadInt();
        for (var i = 0; i < pCount; i++)
            FulfilledPieces.Add(r.ReadString());
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(0); // version
        w.Write(Id);
        w.Write(SetKey);
        w.Write((int)Material);
        w.Write(RequireExceptional);
        w.Write(RequesterName);
        w.Write(RequesterNote);
        w.Write(SealReward);
        w.Write(StandingReward);
        w.Write(IssuedAt);
        w.Write(FulfilledPieces.Count);
        foreach (var p in FulfilledPieces) w.Write(p);
    }
}

// ── Commission system ─────────────────────────────────────────────────────────

public static class SmithCommissionSystem
{
    public const int MaxActiveSmallCommissions = 3;
    public const int MaxActiveCommissions      = MaxActiveSmallCommissions; // alias
    public const int MaxActiveLargeCommissions = 1;

    // ── Material selection helper ─────────────────────────────────────────────
    // Shared by Generate and GenerateLarge.  Each bracket blends the unlocked
    // material (70%) with the previous tier (30%) for variety without giving
    // anything the player cannot craft.

    private static CraftResource GetMaterialForSkill(double skill, bool bypass)
    {
        if (bypass)
            return (CraftResource)Utility.RandomList(
                (int)CraftResource.Mythril, (int)CraftResource.Adamantium, (int)CraftResource.Celestial);

        if (skill < 65.0)  return CraftResource.Iron;
        if (skill < 70.0)  return Utility.RandomDouble() < 0.70 ? CraftResource.DullCopper  : CraftResource.Iron;
        if (skill < 75.0)  return Utility.RandomDouble() < 0.70 ? CraftResource.ShadowIron  : CraftResource.DullCopper;
        if (skill < 80.0)  return Utility.RandomDouble() < 0.70 ? CraftResource.Copper      : CraftResource.ShadowIron;
        if (skill < 85.0)  return Utility.RandomDouble() < 0.70 ? CraftResource.Bronze      : CraftResource.Copper;
        if (skill < 90.0)  return Utility.RandomDouble() < 0.70 ? CraftResource.Gold        : CraftResource.Bronze;
        if (skill < 95.0)  return Utility.RandomDouble() < 0.70 ? CraftResource.Agapite     : CraftResource.Gold;
        if (skill < 99.0)  return Utility.RandomDouble() < 0.70 ? CraftResource.Verite      : CraftResource.Agapite;
        if (skill < 105.0) return Utility.RandomDouble() < 0.70 ? CraftResource.Valorite    : CraftResource.Verite;
        if (skill < 115.0) return Utility.RandomDouble() < 0.70 ? CraftResource.Platinum    : CraftResource.Valorite;
        if (skill < 130.0) return Utility.RandomDouble() < 0.70 ? CraftResource.Toxic       : CraftResource.Platinum;
        if (skill < 150.0) return Utility.RandomDouble() < 0.70 ? CraftResource.Blaze       : CraftResource.Toxic;
        if (skill < 175.0) return Utility.RandomDouble() < 0.70 ? CraftResource.Frost       : CraftResource.Blaze;
        if (skill < 200.0) return Utility.RandomDouble() < 0.70 ? CraftResource.Obsidian    : CraftResource.Frost;
        if (skill < 250.0) return Utility.RandomDouble() < 0.70 ? CraftResource.Mythril     : CraftResource.Obsidian;
        if (skill < 300.0) return Utility.RandomDouble() < 0.70 ? CraftResource.Adamantium  : CraftResource.Mythril;
        return               Utility.RandomDouble() < 0.70 ? CraftResource.Celestial   : CraftResource.Adamantium;
    }

    // ── Generation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a new small commission for the player based on their skill.
    /// Returns null if the player is at the active small commission cap.
    /// </summary>
    public static SmithCommissionEntry? Generate(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return null;

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        if (data.SmithCommissions.Count >= MaxActiveSmallCommissions) return null;

        var skill  = pm.Skills.Blacksmith.Base;
        var bypass = Items.DevTestingCrystal.IsActive(pm);
        var mat    = GetMaterialForSkill(skill, bypass);

        var exceptChance = bypass ? 1.0 : Math.Max(0.0, (skill - 40.0) / 100.0);
        var exceptional  = Utility.RandomDouble() < exceptChance;

        var itemKey = SmithCommissionPool.RandomKey();
        var (seals, standing) = ComputeReward(mat, exceptional);
        var (name, note)      = SmithRequesterPool.Random();

        var entry = new SmithCommissionEntry(
            id:                 Guid.NewGuid().ToString("N")[..8],
            itemKey:            itemKey,
            material:           mat,
            requireExceptional: exceptional,
            requesterName:      name,
            requesterNote:      note,
            sealReward:         seals,
            standingReward:     standing);

        data.SmithCommissions.Add(entry);
        return entry;
    }

    // ── Large commission generation ───────────────────────────────────────────

    private static readonly string[] _largeNotes =
    {
        "A full matching set — anything less won't do in the field.",
        "My whole company needs outfitting. Quality and consistency both.",
        "Piecemeal won't work. I need the whole kit, properly made.",
        "The contract specifies a complete set. Can the Society deliver?",
        "I'll take delivery one piece at a time, but I need them all.",
    };

    /// <summary>
    /// Generates a large (full-set) commission. Max 1 active at a time.
    /// Returns null if capped or player account unavailable.
    /// </summary>
    public static SmithLargeCommissionEntry? GenerateLarge(PlayerMobile pm)
    {
        if (pm.Account is not IAccount acct) return null;

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        if (data.SmithLargeCommissions.Count >= MaxActiveLargeCommissions) return null;

        var skill  = pm.Skills.Blacksmith.Base;
        var bypass = Items.DevTestingCrystal.IsActive(pm);
        var mat    = GetMaterialForSkill(skill, bypass);

        var exceptChance = bypass ? 1.0 : Math.Max(0.0, (skill - 40.0) / 100.0);
        var exceptional  = Utility.RandomDouble() < exceptChance;

        var set         = SmithCommissionSetPool.RandomSet();
        var (pieceSeals, pieceStanding) = ComputeReward(mat, exceptional);
        var pieceCount  = set.ItemKeys.Length;

        // Set bonus: 1.5× over equivalent individual pieces, then apply per-set RewardMultiplier.
        // Dragon scale and other exotic sets carry a higher multiplier (e.g. 2.5×).
        var setMult       = set.RewardMultiplier;
        var totalSeals    = Math.Max(5, (int)(pieceSeals    * pieceCount * 1.5 * setMult));
        var totalStanding = Math.Max(50, (int)(pieceStanding * pieceCount * 1.5 * setMult));

        var (name, _) = SmithRequesterPool.Random();
        var note      = _largeNotes[Utility.Random(_largeNotes.Length)];

        var entry = new SmithLargeCommissionEntry(
            id:                 Guid.NewGuid().ToString("N")[..8],
            setKey:             set.Key,
            material:           mat,
            requireExceptional: exceptional,
            requesterName:      name,
            requesterNote:      note,
            sealReward:         totalSeals,
            standingReward:     totalStanding);

        data.SmithLargeCommissions.Add(entry);
        return entry;
    }

    // ── Large commission turn-in ──────────────────────────────────────────────

    /// <summary>
    /// Returns true if the item satisfies the given piece of a large commission.
    /// </summary>
    public static bool IsMatchForPieceKey(SmithLargeCommissionEntry c, string pieceKey, Item item)
    {
        if (item == null || item.Deleted) return false;
        if (c.FulfilledPieces.Contains(pieceKey)) return false;

        var itemType = SmithCommissionPool.GetItemType(pieceKey);
        if (itemType == null || item.GetType() != itemType) return false;

        // Skip material check for sets with IgnoreMaterial (e.g. dragon scale — resource is RedScales).
        var setDef = SmithCommissionSetPool.GetSet(c.SetKey);
        if (setDef == null || !setDef.IgnoreMaterial)
        {
            var resource = item switch
            {
                BaseWeapon bw => bw.Resource,
                BaseArmor  ba => ba.Resource,
                _             => CraftResource.Iron,
            };
            if (resource != c.Material) return false;
        }

        if (c.RequireExceptional)
        {
            var isExceptional = item switch
            {
                BaseWeapon bw => bw.Quality == WeaponQuality.Exceptional,
                BaseArmor  ba => ba.Quality == ArmorQuality.Exceptional,
                _             => false,
            };
            if (!isExceptional) return false;
        }

        return true;
    }

    /// <summary>
    /// Consumes an item as fulfillment for a piece of a large commission.
    /// Completes the commission automatically when all pieces are submitted.
    /// </summary>
    public static void TurnInLargePiece(PlayerMobile pm, SmithLargeCommissionEntry c,
        string pieceKey, Item item)
    {
        c.FulfilledPieces.Add(pieceKey);
        item.Delete();
        pm.PlaySound(0x249);

        var done  = c.FulfilledPieces.Count;
        var total = c.SetDef?.ItemKeys.Length ?? 0;
        pm.SendMessage(0x59, $"[Commission] {SmithCommissionPool.GetItemLabel(pieceKey)} submitted — {done}/{total} pieces.");

        if (c.Complete)
            CompleteLarge(pm, c);
    }

    /// <summary>Awards rewards and removes the completed large commission.</summary>
    public static void CompleteLarge(PlayerMobile pm, SmithLargeCommissionEntry c)
    {
        if (pm.Account is not IAccount acct) return;

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        data.SmithLargeCommissions.Remove(c);
        data.AddReputation("smithing", c.StandingReward);
        data.AddCurrency("smithing",   c.SealReward);

        pm.SendMessage(0x44,
            $"Large commission complete for {c.RequesterName}: {c.FullLabel}. " +
            $"+{c.StandingReward} standing, +{c.SealReward} Smithing Seal{(c.SealReward == 1 ? "" : "s")}.");

        var (skillMin, skillMax) = GetSkillRange(c.Material, c.RequireExceptional);
        pm.CheckSkill(SkillName.Blacksmith, skillMin, skillMax);
        pm.PlaySound(0x3D);
    }

    // ── Turn-in validation ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the first active commission that the given item satisfies,
    /// or null if none match.
    /// </summary>
    public static SmithCommissionEntry? FindMatch(ClusterFAccountData data, Item item)
    {
        foreach (var c in data.SmithCommissions)
        {
            if (IsMatch(c, item)) return c;
        }
        return null;
    }

    public static bool IsMatch(SmithCommissionEntry c, Item item)
    {
        if (item == null || item.Deleted) return false;

        var itemType = c.ItemType;
        if (itemType == null || item.GetType() != itemType) return false;

        // Material check
        var resource = item switch
        {
            BaseWeapon bw => bw.Resource,
            BaseArmor  ba => ba.Resource,
            _             => CraftResource.Iron,
        };

        if (resource != c.Material) return false;

        // Quality check
        if (c.RequireExceptional)
        {
            var isExceptional = item switch
            {
                BaseWeapon bw => bw.Quality == WeaponQuality.Exceptional,
                BaseArmor  ba => ba.Quality == ArmorQuality.Exceptional,
                _             => false,
            };
            if (!isExceptional) return false;
        }

        return true;
    }

    /// <summary>
    /// Completes a commission — consumes the item, awards rewards, fires skill check.
    /// Removes the commission from the player's active list.
    /// </summary>
    public static void Complete(PlayerMobile pm, SmithCommissionEntry c, Item item)
    {
        if (pm.Account is not IAccount acct) return;

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        data.SmithCommissions.Remove(c);

        data.AddReputation("smithing", c.StandingReward);
        data.AddCurrency("smithing",   c.SealReward);

        pm.SendMessage(0x44,
            $"Commission fulfilled for {c.RequesterName}. " +
            $"+{c.StandingReward} standing, +{c.SealReward} Smithing Seal{(c.SealReward == 1 ? "" : "s")}.");

        // Skill check
        var (skillMin, skillMax) = GetSkillRange(c);
        pm.CheckSkill(SkillName.Blacksmith, skillMin, skillMax);

        item.Delete();
        pm.SendSound(0x3D);
    }

    // ── Reward calculation ────────────────────────────────────────────────────
    // Seals derived from the vanilla BOD gold table for a single-item qty-10 BOD
    // (typeIndex=0, quanIndex=0) divided by CommissionSealDivisor.
    // Post-Valorite uses Valorite-equivalent gold × PostValMultiplier.
    // This aligns commission economics with BOD rewards on a per-piece basis.
    //
    // Divisor 100 means commissions give ~4× per piece vs a qty-10 BOD at divisor 400,
    // appropriate for targeted single-item work.
    //
    // Representative commission seal values:
    //   Iron exceptional        →  ~2  seals
    //   Valorite exceptional    →  ~30 seals
    //   Platinum exceptional    →  ~45 seals
    //   Celestial exceptional   → ~150 seals

    private const int CommissionSealDivisor = 100;

    private static double PostValMultiplier(CraftResource mat) => mat switch
    {
        CraftResource.Platinum   => 1.5,
        CraftResource.Toxic      => 2.0,
        CraftResource.Blaze      => 2.5,
        CraftResource.Frost      => 3.0,
        CraftResource.Obsidian   => 3.5,
        CraftResource.Mythril    => 4.0,
        CraftResource.Adamantium => 4.5,
        CraftResource.Celestial  => 5.0,
        _                        => 1.0,
    };

    private static BulkMaterialType ToBulkMaterial(CraftResource mat) => mat switch
    {
        CraftResource.DullCopper => BulkMaterialType.DullCopper,
        CraftResource.ShadowIron => BulkMaterialType.ShadowIron,
        CraftResource.Copper     => BulkMaterialType.Copper,
        CraftResource.Bronze     => BulkMaterialType.Bronze,
        CraftResource.Gold       => BulkMaterialType.Gold,
        CraftResource.Agapite    => BulkMaterialType.Agapite,
        CraftResource.Verite     => BulkMaterialType.Verite,
        CraftResource.Valorite   => BulkMaterialType.Valorite,
        _                        => BulkMaterialType.None,  // Iron or post-Valorite
    };

    private static (int seals, int standing) ComputeReward(CraftResource mat, bool exceptional)
    {
        var tier = MaterialTier(mat);

        // Gold-equivalent for a single-item, qty-10, 1-part BOD
        int gold;
        if (tier >= 9) // post-Valorite
        {
            var valGold = SmithRewardCalculator.Instance.ComputeGold(
                10, exceptional, BulkMaterialType.Valorite, 1, typeof(Longsword));
            gold = (int)(valGold * PostValMultiplier(mat));
        }
        else
        {
            gold = SmithRewardCalculator.Instance.ComputeGold(
                10, exceptional, ToBulkMaterial(mat), 1, typeof(Longsword));
        }

        var seals    = Math.Max(1, (int)Math.Round(gold / (double)CommissionSealDivisor));
        var standing = exceptional
            ? 80 + tier * 40
            : tier == 0 ? 40 : 50 + tier * 25;

        return (seals, standing);
    }

    // ── Skill range for completion check ──────────────────────────────────────

    public static (double min, double max) GetSkillRange(SmithCommissionEntry c) =>
        GetSkillRange(c.Material, c.RequireExceptional);

    public static (double min, double max) GetSkillRange(CraftResource mat, bool exceptional)
    {
        // Ranges are threshold-10 to threshold+20, giving a wide window for skill gain.
        var (min, max) = mat switch
        {
            CraftResource.Iron        => (  0.0,  55.0),
            CraftResource.DullCopper  => ( 40.0,  72.0),
            CraftResource.ShadowIron  => ( 50.0,  80.0),
            CraftResource.Copper      => ( 55.0,  85.0),
            CraftResource.Bronze      => ( 60.0,  88.0),
            CraftResource.Gold        => ( 65.0,  92.0),
            CraftResource.Agapite     => ( 70.0,  96.0),
            CraftResource.Verite      => ( 75.0, 100.0),
            CraftResource.Valorite    => ( 80.0, 105.0),
            // Post-Valorite — threshold-10 to threshold+20 (no 120 cap; extended skill)
            CraftResource.Platinum    => ( 95.0, 125.0),
            CraftResource.Toxic       => (105.0, 135.0),
            CraftResource.Blaze       => (120.0, 155.0),
            CraftResource.Frost       => (140.0, 175.0),
            CraftResource.Obsidian    => (165.0, 200.0),
            CraftResource.Mythril     => (190.0, 225.0),
            CraftResource.Adamantium  => (240.0, 275.0),
            CraftResource.Celestial   => (290.0, 325.0),
            _                         => (  0.0,  55.0),
        };

        if (exceptional)
        {
            min += 10.0;
            // No 120 cap for post-Valorite — extended skill goes well above 120
            max = MaterialTier(mat) >= 9 ? max + 10.0 : Math.Min(120.0, max + 10.0);
        }

        return (min, max);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public static int MaterialTier(CraftResource mat) => mat switch
    {
        CraftResource.DullCopper  =>  1,
        CraftResource.ShadowIron  =>  2,
        CraftResource.Copper      =>  3,
        CraftResource.Bronze      =>  4,
        CraftResource.Gold        =>  5,
        CraftResource.Agapite     =>  6,
        CraftResource.Verite      =>  7,
        CraftResource.Valorite    =>  8,
        CraftResource.Platinum    =>  9,
        CraftResource.Toxic       => 10,
        CraftResource.Blaze       => 11,
        CraftResource.Frost       => 12,
        CraftResource.Obsidian    => 13,
        CraftResource.Mythril     => 14,
        CraftResource.Adamantium  => 15,
        CraftResource.Celestial   => 16,
        _                         =>  0,
    };

    public static string MaterialName(CraftResource mat) => mat switch
    {
        CraftResource.Iron        => "Iron",
        CraftResource.DullCopper  => "Dull Copper",
        CraftResource.ShadowIron  => "Shadow Iron",
        CraftResource.Copper      => "Copper",
        CraftResource.Bronze      => "Bronze",
        CraftResource.Gold        => "Gold",
        CraftResource.Agapite     => "Agapite",
        CraftResource.Verite      => "Verite",
        CraftResource.Valorite    => "Valorite",
        CraftResource.Platinum    => "Platinum",
        CraftResource.Toxic       => "Toxic",
        CraftResource.Blaze       => "Blaze",
        CraftResource.Frost       => "Frost",
        CraftResource.Obsidian    => "Obsidian",
        CraftResource.Mythril     => "Mythril",
        CraftResource.Adamantium  => "Adamantium",
        CraftResource.Celestial   => "Celestial",
        _                         => "Unknown",
    };
}

// ── GM command: [ClearCommissions ────────────────────────────────────────────

public static class SmithCommissionCommands
{
    public static void Configure()
    {
        CommandSystem.Register("ClearCommissions", AccessLevel.GameMaster,
            ClearCommissions_OnCommand);
    }

    [Usage("ClearCommissions")]
    [Description("Clears all active Smith Commissions for yourself (GM testing tool).")]
    private static void ClearCommissions_OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm || pm.Account is not IAccount acct)
            return;

        var data  = ClusterFAccountPersistence.GetOrCreate(acct);
        var count = data.SmithCommissions.Count;
        data.SmithCommissions.Clear();

        pm.SendMessage(0x44, count > 0
            ? $"Cleared {count} active commission{(count == 1 ? "" : "s")}."
            : "No active commissions to clear.");
    }
}
