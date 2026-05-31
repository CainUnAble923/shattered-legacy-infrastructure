using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;
using Server.Targeting;

namespace Server.Items;

public enum MimicCategory
{
    None,
    WeaponSwordsmanship,
    WeaponMaceFighting,
    WeaponFencing,
    WeaponArchery,
    WeaponWrestling,
    Shield,
    Jewelry,
    Armor,
    Clothing,
    Tool
}

/// <summary>
/// Art constants for the Pet Mimic item.
/// Client patcher must inject itm_mimic_pouch.png at DormantItemID before this
/// item will display correctly. Until the patcher runs it will appear blank.
/// </summary>
public static class PetMimicArt
{
    // 0xE79 = vanilla pouch, tinted hue 1154 — visible everywhere without client changes.
    // Phase 6 will replace this with injected custom art at a reserved ID.
    public const int DormantItemID = 0xE79;
    public const int MimicHue = 1154;
}

[SerializationGenerator(9, false)]
public partial class PetMimic : Item, IAosItem, IWearableDurability
{
    // ── Serialized: HP system ─────────────────────────────────────────────

    [SerializableProperty(0)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int CurrentHP
    {
        get => _currentHP;
        set
        {
            var clamped = Math.Clamp(value, 0, MaxHP);
            if (_currentHP == clamped) return;
            _currentHP = clamped;
            InvalidateProperties();
            if (_currentHP == 0)
                HandleHPDepleted();
        }
    }

    // Stored as ticks so the serialization generator handles it as a long.
    [SerializableProperty(1)]
    [CommandProperty(AccessLevel.GameMaster)]
    public long LastRegenTicks
    {
        get => _lastRegenTicks;
        set => _lastRegenTicks = value;
    }

    // Permanent HP pool bonus accumulated from feeding gems.
    [SerializableProperty(2)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int BonusMaxHP
    {
        get => _bonusMaxHP;
        set
        {
            _bonusMaxHP = Math.Max(0, value);
            InvalidateProperties();
        }
    }

    // ── Serialized: Category & forms ──────────────────────────────────────

    [SerializableProperty(3)]
    [CommandProperty(AccessLevel.GameMaster)]
    public MimicCategory LockedCategory
    {
        get => _lockedCategory;
        set
        {
            _lockedCategory = value;
            InvalidateProperties();
        }
    }

    [SerializableProperty(4)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int ActiveFormItemID
    {
        get => _activeFormItemID;
        set => _activeFormItemID = value;
    }

    [SerializableProperty(5)]
    public List<int> KnownFormItemIDs
    {
        get => _knownFormItemIDs;
        set => _knownFormItemIDs = value;
    }

    // ── Serialized: Accumulated attribute stats ───────────────────────────

    [SerializableProperty(6)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccStrBonus { get => _accStrBonus; set { _accStrBonus = value; RebuildAosAttributes(); } }

    [SerializableProperty(7)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccDexBonus { get => _accDexBonus; set { _accDexBonus = value; RebuildAosAttributes(); } }

    [SerializableProperty(8)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccIntBonus { get => _accIntBonus; set { _accIntBonus = value; RebuildAosAttributes(); } }

    [SerializableProperty(9)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccLRC { get => _accLRC; set { _accLRC = value; RebuildAosAttributes(); } }

    [SerializableProperty(10)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccFC { get => _accFC; set { _accFC = value; RebuildAosAttributes(); } }

    [SerializableProperty(11)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccFCR { get => _accFCR; set { _accFCR = value; RebuildAosAttributes(); } }

    [SerializableProperty(12)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccDI { get => _accDI; set { _accDI = value; RebuildAosAttributes(); } }

    // ── Serialized: Accumulated resistances ───────────────────────────────

    [SerializableProperty(13)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccPhysResist { get => _accPhysResist; set { _accPhysResist = value; } }

    [SerializableProperty(14)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccFireResist { get => _accFireResist; set { _accFireResist = value; } }

    [SerializableProperty(15)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccColdResist { get => _accColdResist; set { _accColdResist = value; } }

    [SerializableProperty(16)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccPoisonResist { get => _accPoisonResist; set { _accPoisonResist = value; } }

    [SerializableProperty(17)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccEnergyResist { get => _accEnergyResist; set { _accEnergyResist = value; } }

    // ── Serialized: Accumulated skill bonuses (parallel lists) ────────────

    [SerializableProperty(18)]
    public List<int> AccSkillBonusIds
    {
        get => _accSkillBonusIds;
        set => _accSkillBonusIds = value;
    }

    [SerializableProperty(19)]
    public List<double> AccSkillBonusValues
    {
        get => _accSkillBonusValues;
        set => _accSkillBonusValues = value;
    }

    // ── Serialized: Form display names (parallel to KnownFormItemIDs) ─────

    // Custom or type name captured at eat-time (e.g. "Legendary Kryss of Doom")
    [SerializableProperty(20)]
    public List<string> KnownFormNames
    {
        get => _knownFormNames;
        set => _knownFormNames = value;
    }

    // Base type name captured at eat-time (e.g. "Kryss")
    [SerializableProperty(21)]
    public List<string> KnownFormBaseNames
    {
        get => _knownFormBaseNames;
        set => _knownFormBaseNames = value;
    }

    // ── Serialized: Additional AOS attributes ─────────────────────────────

    [SerializableProperty(22)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHCI { get => _accHCI; set { _accHCI = value; RebuildAosAttributes(); } }

    [SerializableProperty(23)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccDCI { get => _accDCI; set { _accDCI = value; RebuildAosAttributes(); } }

    [SerializableProperty(24)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccSSI { get => _accSSI; set { _accSSI = value; RebuildAosAttributes(); } }

    [SerializableProperty(25)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccSDI { get => _accSDI; set { _accSDI = value; RebuildAosAttributes(); } }

    [SerializableProperty(26)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccLMC { get => _accLMC; set { _accLMC = value; RebuildAosAttributes(); } }

    [SerializableProperty(27)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccRP { get => _accRP; set { _accRP = value; RebuildAosAttributes(); } }

    [SerializableProperty(28)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccEP { get => _accEP; set { _accEP = value; RebuildAosAttributes(); } }

    [SerializableProperty(29)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccLuck { get => _accLuck; set { _accLuck = value; RebuildAosAttributes(); } }

    [SerializableProperty(30)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccRegenHits { get => _accRegenHits; set { _accRegenHits = value; RebuildAosAttributes(); } }

    [SerializableProperty(31)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccRegenStam { get => _accRegenStam; set { _accRegenStam = value; RebuildAosAttributes(); } }

    [SerializableProperty(32)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccRegenMana { get => _accRegenMana; set { _accRegenMana = value; RebuildAosAttributes(); } }

    // ── Serialized: Weapon hit-proc attributes (synced to phantom weapon) ─

    [SerializableProperty(33)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitDispel { get => _accHitDispel; set => _accHitDispel = value; }

    [SerializableProperty(34)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitFireball { get => _accHitFireball; set => _accHitFireball = value; }

    [SerializableProperty(35)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitHarm { get => _accHitHarm; set => _accHitHarm = value; }

    [SerializableProperty(36)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitMagicArrow { get => _accHitMagicArrow; set => _accHitMagicArrow = value; }

    [SerializableProperty(37)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitLightning { get => _accHitLightning; set => _accHitLightning = value; }

    [SerializableProperty(38)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitLowerAttack { get => _accHitLowerAttack; set => _accHitLowerAttack = value; }

    [SerializableProperty(39)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitLowerDefend { get => _accHitLowerDefend; set => _accHitLowerDefend = value; }

    // ── Serialized: Armor-specific attributes ─────────────────────────────

    // Each point of accumulated SelfRepair accelerates the mimic's HP regen.
    [SerializableProperty(40)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccSelfRepair { get => _accSelfRepair; set => _accSelfRepair = value; }

    // Tracked for display; has no mechanical effect from the talisman slot.
    [SerializableProperty(41)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccMageArmor { get => _accMageArmor; set => _accMageArmor = value; }

    // ── Serialized: Per-form category list (parallel to KnownFormItemIDs) ─

    // Stores the exact MimicCategory of each known form so the phantom weapon
    // can dynamically switch skill, type, animation, and range when forms change.
    [SerializableProperty(50)]
    public List<MimicCategory> KnownFormCategories
    {
        get => _knownFormCategories;
        set => _knownFormCategories = value;
    }

    // ── Serialized: Additional weapon hit-proc attributes ─────────────────

    [SerializableProperty(42)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitLeechHits { get => _accHitLeechHits; set => _accHitLeechHits = value; }

    [SerializableProperty(43)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitLeechStam { get => _accHitLeechStam; set => _accHitLeechStam = value; }

    [SerializableProperty(44)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitLeechMana { get => _accHitLeechMana; set => _accHitLeechMana = value; }

    [SerializableProperty(45)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitColdArea { get => _accHitColdArea; set => _accHitColdArea = value; }

    [SerializableProperty(46)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitFireArea { get => _accHitFireArea; set => _accHitFireArea = value; }

    [SerializableProperty(47)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitPoisonArea { get => _accHitPoisonArea; set => _accHitPoisonArea = value; }

    [SerializableProperty(48)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitEnergyArea { get => _accHitEnergyArea; set => _accHitEnergyArea = value; }

    [SerializableProperty(49)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccHitPhysicalArea { get => _accHitPhysicalArea; set => _accHitPhysicalArea = value; }

    // ── Serialized: Weapon slayer properties ──────────────────────────────

    // First slayer absorbed from weapon forms (fills on first non-None slayer eaten).
    [SerializableProperty(51)]
    [CommandProperty(AccessLevel.GameMaster)]
    public SlayerName AccSlayer { get => _accSlayer; set => _accSlayer = value; }

    // Second unique slayer absorbed (fills when a weapon with a different slayer is eaten).
    [SerializableProperty(52)]
    [CommandProperty(AccessLevel.GameMaster)]
    public SlayerName AccSlayer2 { get => _accSlayer2; set => _accSlayer2 = value; }

    // ── Serialized: Hit Point / Stamina / Mana increase ───────────────────
    // These come from AosAttributes on any item type (armor, jewelry, clothing, weapons).
    // Absent from earlier versions — added in v9.

    [SerializableProperty(53)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccBonusHits { get => _accBonusHits; set { _accBonusHits = value; RebuildAosAttributes(); } }

    [SerializableProperty(54)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccBonusStam { get => _accBonusStam; set { _accBonusStam = value; RebuildAosAttributes(); } }

    [SerializableProperty(55)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int AccBonusMana { get => _accBonusMana; set { _accBonusMana = value; RebuildAosAttributes(); } }

    // ── Non-serialized: AOS attribute object and phantom weapon ──────────

    private AosAttributes  _aosAttributes;
    private PetMimicWeapon _weapon; // managed weapon for weapon-category forms; not serialized

    // IAosItem — engine aggregates LRC/FC/FCR/DI from this via AOS.cs GetValue
    public AosAttributes Attributes => _aosAttributes;

    // ── Resistance overrides — picked up automatically by Mobile.ComputeResistances ──

    public override int PhysicalResistance => _accPhysResist;
    public override int FireResistance     => _accFireResist;
    public override int ColdResistance     => _accColdResist;
    public override int PoisonResistance   => _accPoisonResist;
    public override int EnergyResistance   => _accEnergyResist;

    // ── IWearableDurability — combat decay, same trigger path as armor ────
    // BaseWeapon.AbsorbDamage calls OnHit on equipped IWearableDurability items.
    // A patch adds the talisman slot to that loop (BaseWeapon-talisman-durability.patch).

    bool IDurability.CanFortify => false;
    int IDurability.InitMinHits  => ClusterFPetMimicSettings.BaseMaxHP;
    int IDurability.InitMaxHits  => ClusterFPetMimicSettings.BaseMaxHP;
    void IDurability.ScaleDurability()   { }
    void IDurability.UnscaleDurability() { }

    int IDurability.HitPoints
    {
        get => _currentHP;
        set => CurrentHP = value;
    }

    int IDurability.MaxHitPoints
    {
        get => MaxHP;
        set => _bonusMaxHP = Math.Max(0, value - ClusterFPetMimicSettings.BaseMaxHP);
    }

    public int OnHit(BaseWeapon weapon, int damageTaken)
    {
        // Decay HP by configured amount per hit — only fires while equipped (patch ensures that).
        var decay = ClusterFPetMimicSettings.DecayHPPerHit;
        if (decay > 0)
        {
            CurrentHP -= decay;
        }

        return damageTaken; // mimic does not absorb damage, just takes wear
    }

    // ── Computed ──────────────────────────────────────────────────────────

    public int MaxHP => ClusterFPetMimicSettings.BaseMaxHP + _bonusMaxHP;

    // Returns the MimicCategory of whichever form is currently active.
    // Falls back to LockedCategory if the per-form list hasn't been populated yet
    // (migration from saves that pre-date v7).
    public MimicCategory ActiveFormCategory
    {
        get
        {
            if (_knownFormItemIDs == null || _knownFormCategories == null)
                return _lockedCategory;
            var idx = _knownFormItemIDs.IndexOf(_activeFormItemID);
            if (idx >= 0 && idx < _knownFormCategories.Count)
                return _knownFormCategories[idx];
            return _lockedCategory;
        }
    }

    // Weapon damage scales with the number of distinct forms eaten.
    // Base is weak; each new form adds a point of min and slightly more max.
    public int EffectiveWeaponMinDamage
    {
        get
        {
            var forms = _knownFormItemIDs?.Count ?? 0;
            var dmg   = ClusterFPetMimicSettings.WeaponBaseDamageMin
                      + forms * ClusterFPetMimicSettings.WeaponDamagePerFormMin;
            return Math.Min(dmg, ClusterFPetMimicSettings.WeaponMaxDamage);
        }
    }

    public int EffectiveWeaponMaxDamage
    {
        get
        {
            var forms = _knownFormItemIDs?.Count ?? 0;
            var dmg   = ClusterFPetMimicSettings.WeaponBaseDamageMax
                      + forms * ClusterFPetMimicSettings.WeaponDamagePerFormMax;
            return Math.Min(dmg, ClusterFPetMimicSettings.WeaponMaxDamage + 15);
        }
    }

    // ── Constructor ───────────────────────────────────────────────────────

    [Constructible]
    public PetMimic() : base(PetMimicArt.DormantItemID)
    {
        Weight = 1.0;
        Name = "a pet mimic";
        LootType = LootType.Blessed;
        Layer = Layer.Talisman;
        Hue = PetMimicArt.MimicHue;
        _currentHP = MaxHP;
        _lastRegenTicks = Core.Now.Ticks;
        _activeFormItemID = PetMimicArt.DormantItemID;
        _knownFormItemIDs = new List<int>();
        _knownFormCategories = new List<MimicCategory>();
        _accSkillBonusIds = new List<int>();
        _accSkillBonusValues = new List<double>();
        _knownFormNames = new List<string>();
        _knownFormBaseNames = new List<string>();
        _aosAttributes = new AosAttributes(this);
    }

    // ── Version migration ─────────────────────────────────────────────────

    private void MigrateFrom(V0Content content)
    {
        _currentHP = content.CurrentHP;
        _lastRegenTicks = content.LastRegenTicks;
        _bonusMaxHP = 0;
        InitPhase2Defaults();
    }

    private void MigrateFrom(V1Content content)
    {
        _currentHP = content.CurrentHP;
        _lastRegenTicks = content.LastRegenTicks;
        _bonusMaxHP = content.BonusMaxHP;
        InitPhase2Defaults();
    }

    private void MigrateFrom(V8Content content)
    {
        _currentHP        = content.CurrentHP;
        _lastRegenTicks   = content.LastRegenTicks;
        _bonusMaxHP       = content.BonusMaxHP;
        _lockedCategory   = content.LockedCategory;
        _activeFormItemID = content.ActiveFormItemID;
        _knownFormItemIDs = content.KnownFormItemIDs ?? new List<int>();
        _accStrBonus      = content.AccStrBonus;
        _accDexBonus      = content.AccDexBonus;
        _accIntBonus      = content.AccIntBonus;
        _accLRC           = content.AccLRC;
        _accFC            = content.AccFC;
        _accFCR           = content.AccFCR;
        _accDI            = content.AccDI;
        _accPhysResist    = content.AccPhysResist;
        _accFireResist    = content.AccFireResist;
        _accColdResist    = content.AccColdResist;
        _accPoisonResist  = content.AccPoisonResist;
        _accEnergyResist  = content.AccEnergyResist;
        _accSkillBonusIds    = content.AccSkillBonusIds    ?? new List<int>();
        _accSkillBonusValues = content.AccSkillBonusValues ?? new List<double>();
        _knownFormNames     = content.KnownFormNames     ?? new List<string>();
        _knownFormBaseNames = content.KnownFormBaseNames ?? new List<string>();
        _accHCI = content.AccHCI; _accDCI = content.AccDCI; _accSSI = content.AccSSI;
        _accSDI = content.AccSDI; _accLMC = content.AccLMC; _accRP  = content.AccRP;
        _accEP  = content.AccEP;  _accLuck = content.AccLuck;
        _accRegenHits = content.AccRegenHits; _accRegenStam = content.AccRegenStam; _accRegenMana = content.AccRegenMana;
        _accHitDispel = content.AccHitDispel; _accHitFireball = content.AccHitFireball;
        _accHitHarm = content.AccHitHarm; _accHitMagicArrow = content.AccHitMagicArrow;
        _accHitLightning = content.AccHitLightning;
        _accHitLowerAttack = content.AccHitLowerAttack; _accHitLowerDefend = content.AccHitLowerDefend;
        _accSelfRepair = content.AccSelfRepair; _accMageArmor = content.AccMageArmor;
        _accHitLeechHits = content.AccHitLeechHits; _accHitLeechStam = content.AccHitLeechStam; _accHitLeechMana = content.AccHitLeechMana;
        _accHitColdArea = content.AccHitColdArea; _accHitFireArea = content.AccHitFireArea;
        _accHitPoisonArea = content.AccHitPoisonArea; _accHitEnergyArea = content.AccHitEnergyArea;
        _accHitPhysicalArea = content.AccHitPhysicalArea;
        _knownFormCategories = content.KnownFormCategories ?? new List<MimicCategory>();
        while (_knownFormCategories.Count < _knownFormItemIDs.Count)
            _knownFormCategories.Add(_lockedCategory);
        _accSlayer  = content.AccSlayer;
        _accSlayer2 = content.AccSlayer2;
        // v9 additions
        _accBonusHits = 0; _accBonusStam = 0; _accBonusMana = 0;
        RebuildAosAttributes();
    }

    private void MigrateFrom(V7Content content)
    {
        _currentHP        = content.CurrentHP;
        _lastRegenTicks   = content.LastRegenTicks;
        _bonusMaxHP       = content.BonusMaxHP;
        _lockedCategory   = content.LockedCategory;
        _activeFormItemID = content.ActiveFormItemID;
        _knownFormItemIDs = content.KnownFormItemIDs ?? new List<int>();
        _accStrBonus      = content.AccStrBonus;
        _accDexBonus      = content.AccDexBonus;
        _accIntBonus      = content.AccIntBonus;
        _accLRC           = content.AccLRC;
        _accFC            = content.AccFC;
        _accFCR           = content.AccFCR;
        _accDI            = content.AccDI;
        _accPhysResist    = content.AccPhysResist;
        _accFireResist    = content.AccFireResist;
        _accColdResist    = content.AccColdResist;
        _accPoisonResist  = content.AccPoisonResist;
        _accEnergyResist  = content.AccEnergyResist;
        _accSkillBonusIds    = content.AccSkillBonusIds    ?? new List<int>();
        _accSkillBonusValues = content.AccSkillBonusValues ?? new List<double>();
        _knownFormNames     = content.KnownFormNames     ?? new List<string>();
        _knownFormBaseNames = content.KnownFormBaseNames ?? new List<string>();
        _accHCI = content.AccHCI; _accDCI = content.AccDCI; _accSSI = content.AccSSI;
        _accSDI = content.AccSDI; _accLMC = content.AccLMC; _accRP  = content.AccRP;
        _accEP  = content.AccEP;  _accLuck = content.AccLuck;
        _accRegenHits = content.AccRegenHits; _accRegenStam = content.AccRegenStam; _accRegenMana = content.AccRegenMana;
        _accHitDispel = content.AccHitDispel; _accHitFireball = content.AccHitFireball;
        _accHitHarm = content.AccHitHarm; _accHitMagicArrow = content.AccHitMagicArrow;
        _accHitLightning = content.AccHitLightning;
        _accHitLowerAttack = content.AccHitLowerAttack; _accHitLowerDefend = content.AccHitLowerDefend;
        _accSelfRepair = content.AccSelfRepair; _accMageArmor = content.AccMageArmor;
        _accHitLeechHits = content.AccHitLeechHits; _accHitLeechStam = content.AccHitLeechStam; _accHitLeechMana = content.AccHitLeechMana;
        _accHitColdArea = content.AccHitColdArea; _accHitFireArea = content.AccHitFireArea;
        _accHitPoisonArea = content.AccHitPoisonArea; _accHitEnergyArea = content.AccHitEnergyArea;
        _accHitPhysicalArea = content.AccHitPhysicalArea;
        _knownFormCategories = content.KnownFormCategories ?? new List<MimicCategory>();
        // Backfill if the list is shorter than ItemIDs (gap from migration)
        while (_knownFormCategories.Count < _knownFormItemIDs.Count)
            _knownFormCategories.Add(_lockedCategory);
        // v8 additions
        _accSlayer  = SlayerName.None;
        _accSlayer2 = SlayerName.None;
        // v9 additions
        _accBonusHits = 0; _accBonusStam = 0; _accBonusMana = 0;
        RebuildAosAttributes();
    }

    private void MigrateFrom(V6Content content)
    {
        _currentHP        = content.CurrentHP;
        _lastRegenTicks   = content.LastRegenTicks;
        _bonusMaxHP       = content.BonusMaxHP;
        _lockedCategory   = content.LockedCategory;
        _activeFormItemID = content.ActiveFormItemID;
        _knownFormItemIDs = content.KnownFormItemIDs ?? new List<int>();
        _accStrBonus      = content.AccStrBonus;
        _accDexBonus      = content.AccDexBonus;
        _accIntBonus      = content.AccIntBonus;
        _accLRC           = content.AccLRC;
        _accFC            = content.AccFC;
        _accFCR           = content.AccFCR;
        _accDI            = content.AccDI;
        _accPhysResist    = content.AccPhysResist;
        _accFireResist    = content.AccFireResist;
        _accColdResist    = content.AccColdResist;
        _accPoisonResist  = content.AccPoisonResist;
        _accEnergyResist  = content.AccEnergyResist;
        _accSkillBonusIds    = content.AccSkillBonusIds    ?? new List<int>();
        _accSkillBonusValues = content.AccSkillBonusValues ?? new List<double>();
        _knownFormNames     = content.KnownFormNames     ?? new List<string>();
        _knownFormBaseNames = content.KnownFormBaseNames ?? new List<string>();
        _accHCI = content.AccHCI; _accDCI = content.AccDCI; _accSSI = content.AccSSI;
        _accSDI = content.AccSDI; _accLMC = content.AccLMC; _accRP  = content.AccRP;
        _accEP  = content.AccEP;  _accLuck = content.AccLuck;
        _accRegenHits = content.AccRegenHits; _accRegenStam = content.AccRegenStam; _accRegenMana = content.AccRegenMana;
        _accHitDispel = content.AccHitDispel; _accHitFireball = content.AccHitFireball;
        _accHitHarm = content.AccHitHarm; _accHitMagicArrow = content.AccHitMagicArrow;
        _accHitLightning = content.AccHitLightning;
        _accHitLowerAttack = content.AccHitLowerAttack; _accHitLowerDefend = content.AccHitLowerDefend;
        _accSelfRepair = content.AccSelfRepair; _accMageArmor = content.AccMageArmor;
        _accHitLeechHits = content.AccHitLeechHits; _accHitLeechStam = content.AccHitLeechStam; _accHitLeechMana = content.AccHitLeechMana;
        _accHitColdArea = content.AccHitColdArea; _accHitFireArea = content.AccHitFireArea;
        _accHitPoisonArea = content.AccHitPoisonArea; _accHitEnergyArea = content.AccHitEnergyArea;
        _accHitPhysicalArea = content.AccHitPhysicalArea;
        // v7: per-form categories — best-effort default to LockedCategory for old saves
        _knownFormCategories = new List<MimicCategory>(_knownFormItemIDs.Count);
        for (var i = 0; i < _knownFormItemIDs.Count; i++)
            _knownFormCategories.Add(_lockedCategory);
        // v8 additions
        _accSlayer  = SlayerName.None;
        _accSlayer2 = SlayerName.None;
        // v9 additions
        _accBonusHits = 0; _accBonusStam = 0; _accBonusMana = 0;
        RebuildAosAttributes();
    }

    private void MigrateFrom(V5Content content)
    {
        _currentHP        = content.CurrentHP;
        _lastRegenTicks   = content.LastRegenTicks;
        _bonusMaxHP       = content.BonusMaxHP;
        _lockedCategory   = content.LockedCategory;
        _activeFormItemID = content.ActiveFormItemID;
        _knownFormItemIDs = content.KnownFormItemIDs ?? new List<int>();
        _accStrBonus      = content.AccStrBonus;
        _accDexBonus      = content.AccDexBonus;
        _accIntBonus      = content.AccIntBonus;
        _accLRC           = content.AccLRC;
        _accFC            = content.AccFC;
        _accFCR           = content.AccFCR;
        _accDI            = content.AccDI;
        _accPhysResist    = content.AccPhysResist;
        _accFireResist    = content.AccFireResist;
        _accColdResist    = content.AccColdResist;
        _accPoisonResist  = content.AccPoisonResist;
        _accEnergyResist  = content.AccEnergyResist;
        _accSkillBonusIds    = content.AccSkillBonusIds    ?? new List<int>();
        _accSkillBonusValues = content.AccSkillBonusValues ?? new List<double>();
        _knownFormNames     = content.KnownFormNames     ?? new List<string>();
        _knownFormBaseNames = content.KnownFormBaseNames ?? new List<string>();
        _accHCI = content.AccHCI; _accDCI = content.AccDCI; _accSSI = content.AccSSI;
        _accSDI = content.AccSDI; _accLMC = content.AccLMC; _accRP  = content.AccRP;
        _accEP  = content.AccEP;  _accLuck = content.AccLuck;
        _accRegenHits = content.AccRegenHits; _accRegenStam = content.AccRegenStam; _accRegenMana = content.AccRegenMana;
        _accHitDispel = content.AccHitDispel; _accHitFireball = content.AccHitFireball;
        _accHitHarm = content.AccHitHarm; _accHitMagicArrow = content.AccHitMagicArrow;
        _accHitLightning = content.AccHitLightning;
        _accHitLowerAttack = content.AccHitLowerAttack; _accHitLowerDefend = content.AccHitLowerDefend;
        _accSelfRepair = content.AccSelfRepair;
        _accMageArmor  = content.AccMageArmor;
        // v6 additions
        _accHitLeechHits = 0; _accHitLeechStam = 0; _accHitLeechMana = 0;
        _accHitColdArea = 0; _accHitFireArea = 0; _accHitPoisonArea = 0;
        _accHitEnergyArea = 0; _accHitPhysicalArea = 0;
        // v7 additions
        _knownFormCategories = new List<MimicCategory>(_knownFormItemIDs.Count);
        for (var i = 0; i < _knownFormItemIDs.Count; i++)
            _knownFormCategories.Add(_lockedCategory);
        // v8 additions
        _accSlayer  = SlayerName.None;
        _accSlayer2 = SlayerName.None;
        // v9 additions
        _accBonusHits = 0; _accBonusStam = 0; _accBonusMana = 0;
        RebuildAosAttributes();
    }

    private void MigrateFrom(V4Content content)
    {
        _currentHP        = content.CurrentHP;
        _lastRegenTicks   = content.LastRegenTicks;
        _bonusMaxHP       = content.BonusMaxHP;
        _lockedCategory   = content.LockedCategory;
        _activeFormItemID = content.ActiveFormItemID;
        _knownFormItemIDs = content.KnownFormItemIDs ?? new List<int>();
        _accStrBonus      = content.AccStrBonus;
        _accDexBonus      = content.AccDexBonus;
        _accIntBonus      = content.AccIntBonus;
        _accLRC           = content.AccLRC;
        _accFC            = content.AccFC;
        _accFCR           = content.AccFCR;
        _accDI            = content.AccDI;
        _accPhysResist    = content.AccPhysResist;
        _accFireResist    = content.AccFireResist;
        _accColdResist    = content.AccColdResist;
        _accPoisonResist  = content.AccPoisonResist;
        _accEnergyResist  = content.AccEnergyResist;
        _accSkillBonusIds     = content.AccSkillBonusIds     ?? new List<int>();
        _accSkillBonusValues  = content.AccSkillBonusValues  ?? new List<double>();
        _knownFormNames     = content.KnownFormNames     ?? new List<string>();
        _knownFormBaseNames = content.KnownFormBaseNames ?? new List<string>();
        _accHCI = content.AccHCI; _accDCI = content.AccDCI; _accSSI = content.AccSSI;
        _accSDI = content.AccSDI; _accLMC = content.AccLMC; _accRP  = content.AccRP;
        _accEP  = content.AccEP;  _accLuck = content.AccLuck;
        _accRegenHits = content.AccRegenHits; _accRegenStam = content.AccRegenStam; _accRegenMana = content.AccRegenMana;
        _accHitDispel = content.AccHitDispel; _accHitFireball = content.AccHitFireball;
        _accHitHarm = content.AccHitHarm; _accHitMagicArrow = content.AccHitMagicArrow;
        _accHitLightning = content.AccHitLightning;
        _accHitLowerAttack = content.AccHitLowerAttack; _accHitLowerDefend = content.AccHitLowerDefend;
        // v5 additions
        _accSelfRepair = 0;
        _accMageArmor  = 0;
        // v6 additions
        _accHitLeechHits = 0; _accHitLeechStam = 0; _accHitLeechMana = 0;
        _accHitColdArea = 0; _accHitFireArea = 0; _accHitPoisonArea = 0;
        _accHitEnergyArea = 0; _accHitPhysicalArea = 0;
        // v7 additions
        _knownFormCategories = new List<MimicCategory>(_knownFormItemIDs.Count);
        for (var i = 0; i < _knownFormItemIDs.Count; i++)
            _knownFormCategories.Add(_lockedCategory);
        // v8 additions
        _accSlayer  = SlayerName.None;
        _accSlayer2 = SlayerName.None;
        // v9 additions
        _accBonusHits = 0; _accBonusStam = 0; _accBonusMana = 0;
        RebuildAosAttributes();
    }

    private void MigrateFrom(V3Content content)
    {
        _currentHP        = content.CurrentHP;
        _lastRegenTicks   = content.LastRegenTicks;
        _bonusMaxHP       = content.BonusMaxHP;
        _lockedCategory   = content.LockedCategory;
        _activeFormItemID = content.ActiveFormItemID;
        _knownFormItemIDs = content.KnownFormItemIDs ?? new List<int>();
        _accStrBonus      = content.AccStrBonus;
        _accDexBonus      = content.AccDexBonus;
        _accIntBonus      = content.AccIntBonus;
        _accLRC           = content.AccLRC;
        _accFC            = content.AccFC;
        _accFCR           = content.AccFCR;
        _accDI            = content.AccDI;
        _accPhysResist    = content.AccPhysResist;
        _accFireResist    = content.AccFireResist;
        _accColdResist    = content.AccColdResist;
        _accPoisonResist  = content.AccPoisonResist;
        _accEnergyResist  = content.AccEnergyResist;
        _accSkillBonusIds     = content.AccSkillBonusIds     ?? new List<int>();
        _accSkillBonusValues  = content.AccSkillBonusValues  ?? new List<double>();
        _knownFormNames     = content.KnownFormNames     ?? new List<string>();
        _knownFormBaseNames = content.KnownFormBaseNames ?? new List<string>();
        // v4 additions — zero-initialised on upgrade
        _accHCI = 0; _accDCI = 0; _accSSI = 0; _accSDI = 0; _accLMC = 0;
        _accRP  = 0; _accEP  = 0; _accLuck = 0;
        _accRegenHits = 0; _accRegenStam = 0; _accRegenMana = 0;
        _accHitDispel = 0; _accHitFireball = 0; _accHitHarm = 0;
        _accHitMagicArrow = 0; _accHitLightning = 0;
        _accHitLowerAttack = 0; _accHitLowerDefend = 0;
        // v5 additions
        _accSelfRepair = 0; _accMageArmor = 0;
        // v6 additions
        _accHitLeechHits = 0; _accHitLeechStam = 0; _accHitLeechMana = 0;
        _accHitColdArea = 0; _accHitFireArea = 0; _accHitPoisonArea = 0;
        _accHitEnergyArea = 0; _accHitPhysicalArea = 0;
        // v7 additions
        _knownFormCategories = new List<MimicCategory>(_knownFormItemIDs.Count);
        for (var i = 0; i < _knownFormItemIDs.Count; i++)
            _knownFormCategories.Add(_lockedCategory);
        // v8 additions
        _accSlayer  = SlayerName.None;
        _accSlayer2 = SlayerName.None;
        // v9 additions
        _accBonusHits = 0; _accBonusStam = 0; _accBonusMana = 0;
        RebuildAosAttributes();
    }

    private void MigrateFrom(V2Content content)
    {
        _currentHP        = content.CurrentHP;
        _lastRegenTicks   = content.LastRegenTicks;
        _bonusMaxHP       = content.BonusMaxHP;
        _lockedCategory   = content.LockedCategory;
        _activeFormItemID = content.ActiveFormItemID;
        _knownFormItemIDs = content.KnownFormItemIDs ?? new List<int>();
        _accStrBonus      = content.AccStrBonus;
        _accDexBonus      = content.AccDexBonus;
        _accIntBonus      = content.AccIntBonus;
        _accLRC           = content.AccLRC;
        _accFC            = content.AccFC;
        _accFCR           = content.AccFCR;
        _accDI            = content.AccDI;
        _accPhysResist    = content.AccPhysResist;
        _accFireResist    = content.AccFireResist;
        _accColdResist    = content.AccColdResist;
        _accPoisonResist  = content.AccPoisonResist;
        _accEnergyResist  = content.AccEnergyResist;
        _accSkillBonusIds     = content.AccSkillBonusIds     ?? new List<int>();
        _accSkillBonusValues  = content.AccSkillBonusValues  ?? new List<double>();
        // Phase 3: synthesize names from item IDs for existing forms
        _knownFormNames     = new List<string>(_knownFormItemIDs.Count);
        _knownFormBaseNames = new List<string>(_knownFormItemIDs.Count);
        foreach (var id in _knownFormItemIDs)
        {
            var tileName = TileData.ItemTable[id & TileData.MaxItemValue].Name ?? "";
            _knownFormNames.Add(tileName.Length > 0 ? tileName : $"Form #{id:X4}");
            _knownFormBaseNames.Add(tileName);
        }
        // v8 additions
        _accSlayer  = SlayerName.None;
        _accSlayer2 = SlayerName.None;
        // v9 additions
        _accBonusHits = 0; _accBonusStam = 0; _accBonusMana = 0;
        RebuildAosAttributes();
    }

    private void InitPhase2Defaults()
    {
        _lockedCategory = MimicCategory.None;
        _activeFormItemID = PetMimicArt.DormantItemID;
        _knownFormItemIDs = new List<int>();
        _knownFormCategories = new List<MimicCategory>();
        _accSkillBonusIds = new List<int>();
        _accSkillBonusValues = new List<double>();
        _knownFormNames = new List<string>();
        _knownFormBaseNames = new List<string>();
        // v8 additions
        _accSlayer  = SlayerName.None;
        _accSlayer2 = SlayerName.None;
        // v9 additions
        _accBonusHits = 0; _accBonusStam = 0; _accBonusMana = 0;
        RebuildAosAttributes();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _knownFormItemIDs    ??= new List<int>();
        _knownFormCategories ??= new List<MimicCategory>();
        _accSkillBonusIds    ??= new List<int>();
        _accSkillBonusValues ??= new List<double>();
        _knownFormNames      ??= new List<string>();
        _knownFormBaseNames  ??= new List<string>();
        // Backfill if the list is shorter than ItemIDs (gap from migration)
        while (_knownFormCategories.Count < _knownFormItemIDs.Count)
            _knownFormCategories.Add(_lockedCategory);
        RebuildAosAttributes();
    }

    // ── AOS attribute management ──────────────────────────────────────────

    private void RebuildAosAttributes()
    {
        _aosAttributes ??= new AosAttributes(this);
        _aosAttributes.BonusStr      = _accStrBonus;
        _aosAttributes.BonusDex      = _accDexBonus;
        _aosAttributes.BonusInt      = _accIntBonus;
        _aosAttributes.BonusHits     = _accBonusHits;
        _aosAttributes.BonusStam     = _accBonusStam;
        _aosAttributes.BonusMana     = _accBonusMana;
        _aosAttributes.LowerRegCost  = _accLRC;
        _aosAttributes.CastSpeed     = _accFC;
        _aosAttributes.CastRecovery  = _accFCR;
        _aosAttributes.WeaponDamage  = _accDI;
        _aosAttributes.AttackChance  = _accHCI;
        _aosAttributes.DefendChance  = _accDCI;
        _aosAttributes.WeaponSpeed   = _accSSI;
        _aosAttributes.SpellDamage   = _accSDI;
        _aosAttributes.LowerManaCost = _accLMC;
        _aosAttributes.ReflectPhysical = _accRP;
        _aosAttributes.EnhancePotions  = _accEP;
        _aosAttributes.Luck          = _accLuck;
        _aosAttributes.RegenHits     = _accRegenHits;
        _aosAttributes.RegenStam     = _accRegenStam;
        _aosAttributes.RegenMana     = _accRegenMana;
    }

    // Pushes accumulated weapon hit-proc stats to the phantom weapon's WeaponAttributes.
    internal void SyncWeaponAttributes(PetMimicWeapon weapon)
    {
        weapon.WeaponAttributes.HitDispel      = _accHitDispel;
        weapon.WeaponAttributes.HitFireball    = _accHitFireball;
        weapon.WeaponAttributes.HitHarm        = _accHitHarm;
        weapon.WeaponAttributes.HitMagicArrow  = _accHitMagicArrow;
        weapon.WeaponAttributes.HitLightning   = _accHitLightning;
        weapon.WeaponAttributes.HitLowerAttack = _accHitLowerAttack;
        weapon.WeaponAttributes.HitLowerDefend = _accHitLowerDefend;
        weapon.WeaponAttributes.HitLeechHits   = _accHitLeechHits;
        weapon.WeaponAttributes.HitLeechStam   = _accHitLeechStam;
        weapon.WeaponAttributes.HitLeechMana   = _accHitLeechMana;
        weapon.WeaponAttributes.HitColdArea    = _accHitColdArea;
        weapon.WeaponAttributes.HitFireArea    = _accHitFireArea;
        weapon.WeaponAttributes.HitPoisonArea  = _accHitPoisonArea;
        weapon.WeaponAttributes.HitEnergyArea  = _accHitEnergyArea;
        weapon.WeaponAttributes.HitPhysicalArea = _accHitPhysicalArea;
        weapon.Slayer  = _accSlayer;
        weapon.Slayer2 = _accSlayer2;
    }

    // ── Phantom weapon management ─────────────────────────────────────────

    // Exposed for PetMimicWeapon.Configure world-load scanner
    internal PetMimicWeapon Weapon => _weapon;
    internal void SetWeapon(PetMimicWeapon w) => _weapon = w;

    internal static bool IsWeaponCategory(MimicCategory cat) => cat is
        MimicCategory.WeaponSwordsmanship or
        MimicCategory.WeaponMaceFighting  or
        MimicCategory.WeaponFencing       or
        MimicCategory.WeaponArchery;
    // Wrestling is unarmed — no phantom weapon needed.

    // Groups: Weapons / Armor+Shield / Jewelry / Clothing
    private static int SuperGroup(MimicCategory cat) => cat switch
    {
        MimicCategory.WeaponSwordsmanship or
        MimicCategory.WeaponMaceFighting  or
        MimicCategory.WeaponFencing       or
        MimicCategory.WeaponArchery       or
        MimicCategory.WeaponWrestling     => 1,
        MimicCategory.Armor or
        MimicCategory.Shield              => 2,
        MimicCategory.Jewelry             => 3,
        MimicCategory.Clothing            => 4,
        _                                 => 0
    };

    private static bool AreCompatibleCategories(MimicCategory locked, MimicCategory incoming) =>
        SuperGroup(locked) != 0 && SuperGroup(locked) == SuperGroup(incoming);

    private static string SuperCategoryName(MimicCategory cat) => SuperGroup(cat) switch
    {
        1 => "Weapons",
        2 => "Armor",
        3 => "Jewelry",
        4 => "Clothing",
        _ => cat.ToString()
    };

    // Returns the layer the mimic should occupy for the given form.
    // Weapon forms stay in Talisman (phantom weapon owns the hand slots).
    // All other forms use the layer encoded in TileData (same formula as BaseArmor).
    private static Layer LayerForForm(MimicCategory formCategory, int itemID) =>
        IsWeaponCategory(formCategory) || formCategory == MimicCategory.WeaponWrestling
            ? Layer.Talisman
            : (Layer)TileData.ItemTable[itemID & TileData.MaxItemValue].Quality;

    // Moves the mimic to a new layer, displacing any occupant to backpack.
    // If the mimic is in the backpack (not equipped), just updates the stored Layer
    // so the next equip lands in the right slot.
    private void UpdateFormLayer(Layer newLayer)
    {
        if (Layer == newLayer) return;

        if (Parent is Mobile m)
        {
            // Fully equipped — remove, change layer, displace occupant, re-equip
            m.RemoveItem(this);
            Layer = newLayer;
            var existing = m.FindItemOnLayer(newLayer);
            if (existing != null)
                m.AddToBackpack(existing);
            m.AddItem(this);
        }
        else
        {
            // In backpack or not held — just update the layer so the next equip is correct
            Layer = newLayer;
        }
    }

    internal void AttachWeapon(Mobile m)
    {
        if (!IsWeaponCategory(_lockedCategory)) return;
        if (_weapon != null && !_weapon.Deleted) return;

        var weapon = new PetMimicWeapon(this);
        _weapon = weapon;

        // Defer to next server tick — calling AddItem from inside OnAdded fires while
        // the mobile's equip event chain is still running, which can cause state issues.
        Timer.DelayCall(TimeSpan.Zero, () =>
        {
            if (weapon.Deleted || Deleted || m.FindItemOnLayer(Layer.Talisman) != this)
            {
                if (!weapon.Deleted) weapon.Delete();
                if (_weapon == weapon) _weapon = null;
                return;
            }

            // EquipItem fails here: CheckEquip rejects the call whenever anything is
            // already in the target layer (base CheckConflictingLayer: m_Layer == layer).
            // For a server-managed attachment, displace the occupant to backpack and
            // use AddItem directly, which skips CanEquip / CheckEquip entirely.
            var existing = m.FindItemOnLayer(weapon.Layer);
            if (existing != null)
                m.AddToBackpack(existing);

            m.AddItem(weapon);
            SyncWeaponAttributes(weapon);
        });
    }

    private void DetachWeapon(Mobile m)
    {
        if (_weapon == null || _weapon.Deleted) { _weapon = null; return; }
        if (_weapon.Parent != null)
            m.RemoveItem(_weapon);
        _weapon.Delete();
        _weapon = null;
    }

    // ── OnAdded / OnRemoved ───────────────────────────────────────────────

    public override void OnAdded(IEntity parent)
    {
        if (Core.AOS && parent is Mobile m)
        {
            _aosAttributes?.AddStatBonuses(m);
            ApplySkillMods(m);
            m.CheckStatTimers();
            AttachWeapon(m);
        }

        base.OnAdded(parent);
    }

    public override void OnRemoved(IEntity parent)
    {
        if (Core.AOS && parent is Mobile m)
        {
            _aosAttributes?.RemoveStatBonuses(m);
            RemoveSkillMods(m);
            m.CheckStatTimers();
            DetachWeapon(m);
        }

        base.OnRemoved(parent);
    }

    private void ApplySkillMods(Mobile m)
    {
        for (var i = 0; i < _accSkillBonusIds.Count; i++)
        {
            var skill = (SkillName)_accSkillBonusIds[i];
            var bonus = _accSkillBonusValues[i];
            if (bonus > 0)
            {
                m.AddSkillMod(new DefaultSkillMod(skill, SkillModName(skill), true, bonus));
            }
        }
    }

    private void RemoveSkillMods(Mobile m)
    {
        for (var i = 0; i < _accSkillBonusIds.Count; i++)
        {
            m.RemoveSkillMod(SkillModName((SkillName)_accSkillBonusIds[i]));
        }
    }

    private string SkillModName(SkillName skill) => $"PetMimic-{Serial}-{(int)skill}";

    // ── Category detection ────────────────────────────────────────────────

    public static MimicCategory DetectCategory(Item item) => item switch
    {
        BaseShield   => MimicCategory.Shield,
        BaseWeapon w when w.Skill == SkillName.Swords    => MimicCategory.WeaponSwordsmanship,
        BaseWeapon w when w.Skill == SkillName.Macing    => MimicCategory.WeaponMaceFighting,
        BaseWeapon w when w.Skill == SkillName.Fencing   => MimicCategory.WeaponFencing,
        BaseWeapon w when w.Skill == SkillName.Archery   => MimicCategory.WeaponArchery,
        BaseWeapon w when w.Skill == SkillName.Wrestling => MimicCategory.WeaponWrestling,
        BaseArmor    => MimicCategory.Armor,
        BaseJewel    => MimicCategory.Jewelry,
        BaseClothing => MimicCategory.Clothing,
        _            => MimicCategory.None  // Tool and others not accepted
    };

    // ── Eating flow ───────────────────────────────────────────────────────

    private void TryEat(Mobile from, Item item)
    {
        var category = DetectCategory(item);
        if (category == MimicCategory.None)
        {
            from.SendMessage(0x59, "The mimic sniffs the item curiously but cannot eat it — it only accepts weapons, armor, jewelry, or clothing.");
            return;
        }

        if (_lockedCategory != MimicCategory.None && !AreCompatibleCategories(_lockedCategory, category))
        {
            var locked   = SuperCategoryName(_lockedCategory);
            var incoming = SuperCategoryName(category);
            from.SendMessage(0x22, $"Your pet mimic is bound to {locked} and cannot eat {incoming} items. Use a purge potion to reset it.");
            return;
        }

        from.SendGump(new PetMimicFeedGump(from, this, item));
    }

    public void ConfirmEat(Mobile from, Item item)
    {
        if (item.Deleted || item.RootParent != from)
        {
            from.SendMessage(0x22, "You no longer have that item.");
            return;
        }

        // Talismans absorb stats only — no category lock, no form change.
        if (item is BaseTalisman)
        {
            AccumulateStatsFrom(item);
            item.Delete();
            InvalidateProperties();
            from.SendMessage(0x44, $"Your pet mimic absorbs the essence of the {item.Name ?? "talisman"}!");
            Effects.PlaySound(from.Location, from.Map, 0x57);
            from.FixedParticles(0x376A, 9, 32, 5005, EffectLayer.Waist);
            return;
        }

        var category = DetectCategory(item);
        if (category == MimicCategory.None) { return; }

        // Stale-gump safety: re-validate super-category compatibility.
        if (_lockedCategory != MimicCategory.None && !AreCompatibleCategories(_lockedCategory, category))
        {
            from.SendMessage(0x22, "Your pet mimic can no longer eat that type of item.");
            return;
        }

        if (_lockedCategory == MimicCategory.None)
        {
            _lockedCategory = category;
            // Move mimic into the form's natural slot on first lock.
            // Weapon mimics stay in Talisman — phantom weapon owns the hand slots.
            UpdateFormLayer(LayerForForm(category, item.ItemID));
        }

        // Track form — store names at eat-time so the journal shows useful labels
        if (!_knownFormItemIDs.Contains(item.ItemID))
        {
            _knownFormItemIDs.Add(item.ItemID);
            _knownFormCategories.Add(category);

            var baseName    = TileData.ItemTable[item.ItemID & TileData.MaxItemValue].Name ?? "";
            var displayName = !string.IsNullOrWhiteSpace(item.Name) ? item.Name : baseName;
            if (string.IsNullOrWhiteSpace(displayName)) displayName = $"Form #{item.ItemID:X4}";

            _knownFormNames.Add(displayName);
            _knownFormBaseNames.Add(baseName);
        }

        _activeFormItemID = item.ItemID;
        ItemID = item.ItemID;
        Hue = PetMimicArt.MimicHue;

        AccumulateStatsFrom(item);

        item.Delete();

        // Attach phantom weapon on first weapon-category eat (category just locked in above)
        if (IsWeaponCategory(_lockedCategory) && Parent is Mobile equipper)
            AttachWeapon(equipper);

        InvalidateProperties();
        from.SendMessage(0x44, $"Your pet mimic devours the {item.Name ?? "item"}, absorbing its essence!");
        Effects.PlaySound(from.Location, from.Map, 0x57);
        from.FixedParticles(0x376A, 9, 32, 5005, EffectLayer.Waist);
    }

    public void SwitchForm(Mobile from, int index)
    {
        if (index < 0 || index >= _knownFormItemIDs.Count)
        {
            from.SendMessage(0x22, "That form is no longer available.");
            return;
        }

        var newItemID = _knownFormItemIDs[index];
        if (_activeFormItemID == newItemID)
        {
            from.SendMessage(0x59, "Your pet mimic is already in that form.");
            return;
        }

        _activeFormItemID = newItemID;
        ItemID = newItemID;
        Hue = PetMimicArt.MimicHue;

        // Keep the equipped slot in sync with the active form's layer.
        var formCat = index < _knownFormCategories.Count ? _knownFormCategories[index] : _lockedCategory;
        UpdateFormLayer(LayerForForm(formCat, newItemID));

        _weapon?.SyncFromMimic(this);

        InvalidateProperties();

        var formName = index < _knownFormNames.Count ? _knownFormNames[index] : $"Form #{newItemID:X4}";
        from.SendMessage(0x44, $"Your pet mimic shifts into the form of {formName}.");
        Effects.PlaySound(from.Location, from.Map, 0x1FE);
    }

    private void AccumulateStatsFrom(Item item)
    {
        AosAttributes attrs = item switch
        {
            BaseWeapon w   => w.Attributes,
            BaseArmor a    => a.Attributes,
            BaseJewel j    => j.Attributes,
            BaseClothing c => c.Attributes,
            BaseTalisman t => t.Attributes,
            _              => null
        };

        AosSkillBonuses skillBonuses = item switch
        {
            BaseWeapon w   => w.SkillBonuses,
            BaseArmor a    => a.SkillBonuses,
            BaseJewel j    => j.SkillBonuses,
            BaseClothing c => c.SkillBonuses,
            BaseTalisman t => t.SkillBonuses,
            _              => null
        };

        if (attrs != null)
        {
            _accStrBonus  = Math.Min(_accStrBonus  + attrs.BonusStr,  ClusterFPetMimicSettings.MaxStrBonus);
            _accDexBonus  = Math.Min(_accDexBonus  + attrs.BonusDex,  ClusterFPetMimicSettings.MaxDexBonus);
            _accIntBonus  = Math.Min(_accIntBonus  + attrs.BonusInt,  ClusterFPetMimicSettings.MaxIntBonus);
            _accBonusHits = Math.Min(_accBonusHits + attrs.BonusHits, ClusterFPetMimicSettings.MaxBonusHits);
            _accBonusStam = Math.Min(_accBonusStam + attrs.BonusStam, ClusterFPetMimicSettings.MaxBonusStam);
            _accBonusMana = Math.Min(_accBonusMana + attrs.BonusMana, ClusterFPetMimicSettings.MaxBonusMana);
            _accLRC      = Math.Min(_accLRC      + attrs.LowerRegCost,   ClusterFPetMimicSettings.MaxLRC);
            _accFC       = Math.Min(_accFC       + attrs.CastSpeed,      ClusterFPetMimicSettings.MaxFasterCasting);
            _accFCR      = Math.Min(_accFCR      + attrs.CastRecovery,   ClusterFPetMimicSettings.MaxFasterCastRecovery);
            _accDI       = Math.Min(_accDI       + attrs.WeaponDamage,   ClusterFPetMimicSettings.MaxDamageIncrease);
            _accHCI      = Math.Min(_accHCI      + attrs.AttackChance,   ClusterFPetMimicSettings.MaxHCI);
            _accDCI      = Math.Min(_accDCI      + attrs.DefendChance,   ClusterFPetMimicSettings.MaxDCI);
            _accSSI      = Math.Min(_accSSI      + attrs.WeaponSpeed,    ClusterFPetMimicSettings.MaxSSI);
            _accSDI      = Math.Min(_accSDI      + attrs.SpellDamage,    ClusterFPetMimicSettings.MaxSDI);
            _accLMC      = Math.Min(_accLMC      + attrs.LowerManaCost,  ClusterFPetMimicSettings.MaxLMC);
            _accRP       = Math.Min(_accRP       + attrs.ReflectPhysical, ClusterFPetMimicSettings.MaxRP);
            _accEP       = Math.Min(_accEP       + attrs.EnhancePotions, ClusterFPetMimicSettings.MaxEP);
            _accLuck     = Math.Min(_accLuck     + attrs.Luck,           ClusterFPetMimicSettings.MaxLuck);
            _accRegenHits = Math.Min(_accRegenHits + attrs.RegenHits,    ClusterFPetMimicSettings.MaxRegenHits);
            _accRegenStam = Math.Min(_accRegenStam + attrs.RegenStam,    ClusterFPetMimicSettings.MaxRegenStam);
            _accRegenMana = Math.Min(_accRegenMana + attrs.RegenMana,    ClusterFPetMimicSettings.MaxRegenMana);
        }

        // Armor-specific attributes — only on BaseArmor
        if (item is BaseArmor ba)
        {
            _accSelfRepair = Math.Min(_accSelfRepair + ba.ArmorAttributes.SelfRepair, ClusterFPetMimicSettings.MaxSelfRepair);
            _accMageArmor  = Math.Min(_accMageArmor  + ba.ArmorAttributes.MageArmor,  ClusterFPetMimicSettings.MaxMageArmor);
        }

        // Weapon hit-proc attributes — only on BaseWeapon
        if (item is BaseWeapon bw)
        {
            var cap = ClusterFPetMimicSettings.MaxWeaponHit;
            _accHitDispel      = Math.Min(_accHitDispel      + bw.WeaponAttributes.HitDispel,      cap);
            _accHitFireball    = Math.Min(_accHitFireball    + bw.WeaponAttributes.HitFireball,    cap);
            _accHitHarm        = Math.Min(_accHitHarm        + bw.WeaponAttributes.HitHarm,        cap);
            _accHitMagicArrow  = Math.Min(_accHitMagicArrow  + bw.WeaponAttributes.HitMagicArrow,  cap);
            _accHitLightning   = Math.Min(_accHitLightning   + bw.WeaponAttributes.HitLightning,   cap);
            _accHitLowerAttack = Math.Min(_accHitLowerAttack + bw.WeaponAttributes.HitLowerAttack, cap);
            _accHitLowerDefend = Math.Min(_accHitLowerDefend + bw.WeaponAttributes.HitLowerDefend, cap);
            _accHitLeechHits   = Math.Min(_accHitLeechHits   + bw.WeaponAttributes.HitLeechHits,   cap);
            _accHitLeechStam   = Math.Min(_accHitLeechStam   + bw.WeaponAttributes.HitLeechStam,   cap);
            _accHitLeechMana   = Math.Min(_accHitLeechMana   + bw.WeaponAttributes.HitLeechMana,   cap);
            _accHitColdArea    = Math.Min(_accHitColdArea    + bw.WeaponAttributes.HitColdArea,    cap);
            _accHitFireArea    = Math.Min(_accHitFireArea    + bw.WeaponAttributes.HitFireArea,    cap);
            _accHitPoisonArea  = Math.Min(_accHitPoisonArea  + bw.WeaponAttributes.HitPoisonArea,  cap);
            _accHitEnergyArea  = Math.Min(_accHitEnergyArea  + bw.WeaponAttributes.HitEnergyArea,  cap);
            _accHitPhysicalArea = Math.Min(_accHitPhysicalArea + bw.WeaponAttributes.HitPhysicalArea, cap);

            // Slayer absorption: fill up to 2 unique non-None slayer slots.
            // First-come-first-served; the mimic permanently retains any slayer it absorbs.
            AbsorbSlayer(bw.Slayer);
            AbsorbSlayer(bw.Slayer2);
        }

        _accPhysResist   = Math.Min(_accPhysResist   + item.PhysicalResistance, ClusterFPetMimicSettings.MaxResistance);
        _accFireResist   = Math.Min(_accFireResist   + item.FireResistance,    ClusterFPetMimicSettings.MaxResistance);
        _accColdResist   = Math.Min(_accColdResist   + item.ColdResistance,    ClusterFPetMimicSettings.MaxResistance);
        _accPoisonResist = Math.Min(_accPoisonResist + item.PoisonResistance,  ClusterFPetMimicSettings.MaxResistance);
        _accEnergyResist = Math.Min(_accEnergyResist + item.EnergyResistance,  ClusterFPetMimicSettings.MaxResistance);

        if (skillBonuses != null)
        {
            for (var i = 0; i < 5; i++)
            {
                if (skillBonuses.GetValues(i, out var skill, out var bonus) && bonus > 0)
                {
                    AccumulateSkillBonus(skill, bonus);
                }
            }
        }

        RebuildAosAttributes();

        if (_weapon != null && !_weapon.Deleted)
            SyncWeaponAttributes(_weapon);

        if (Parent is Mobile equippedBy)
        {
            _aosAttributes.RemoveStatBonuses(equippedBy);
            _aosAttributes.AddStatBonuses(equippedBy);
            RemoveSkillMods(equippedBy);
            ApplySkillMods(equippedBy);
            equippedBy.CheckStatTimers();
            equippedBy.UpdateResistances();
        }
    }

    private void AccumulateSkillBonus(SkillName skill, double bonus)
    {
        var max = (double)ClusterFPetMimicSettings.MaxSkillBonus;
        var idx = _accSkillBonusIds.IndexOf((int)skill);
        if (idx >= 0)
        {
            _accSkillBonusValues[idx] = Math.Min(_accSkillBonusValues[idx] + bonus, max);
        }
        else
        {
            _accSkillBonusIds.Add((int)skill);
            _accSkillBonusValues.Add(Math.Min(bonus, max));
        }
    }

    // Absorbs one slayer name into the first vacant slot (AccSlayer, then AccSlayer2).
    // Ignores None and duplicates — a mimic can hold at most 2 distinct slayers.
    private void AbsorbSlayer(SlayerName slayer)
    {
        if (slayer == SlayerName.None) return;
        if (_accSlayer == SlayerName.None)
            { _accSlayer = slayer; return; }
        if (_accSlayer == slayer) return;     // already have it in slot 1
        if (_accSlayer2 == SlayerName.None)
            { _accSlayer2 = slayer; return; }
        // Both slots filled — silently ignore the extra slayer
    }

    // ── Purge (Phase 5) ───────────────────────────────────────────────────

    public bool HasAccumulatedStats =>
        _lockedCategory != MimicCategory.None ||
        _accStrBonus > 0 || _accDexBonus > 0 || _accIntBonus > 0 ||
        _accBonusHits > 0 || _accBonusStam > 0 || _accBonusMana > 0 ||
        _accLRC > 0 || _accFC > 0 || _accFCR > 0 || _accDI > 0 ||
        _accHCI > 0 || _accDCI > 0 || _accSSI > 0 || _accSDI > 0 ||
        _accLMC > 0 || _accRP > 0 || _accEP > 0 || _accLuck > 0 ||
        _accRegenHits > 0 || _accRegenStam > 0 || _accRegenMana > 0 ||
        _accPhysResist > 0 || _accFireResist > 0 || _accColdResist > 0 ||
        _accPoisonResist > 0 || _accEnergyResist > 0 ||
        _accHitDispel > 0 || _accHitFireball > 0 || _accHitHarm > 0 ||
        _accHitMagicArrow > 0 || _accHitLightning > 0 ||
        _accHitLowerAttack > 0 || _accHitLowerDefend > 0 ||
        _accHitLeechHits > 0 || _accHitLeechStam > 0 || _accHitLeechMana > 0 ||
        _accHitColdArea > 0 || _accHitFireArea > 0 || _accHitPoisonArea > 0 ||
        _accHitEnergyArea > 0 || _accHitPhysicalArea > 0 ||
        _accSelfRepair > 0 || _accMageArmor > 0 ||
        _accSlayer != SlayerName.None || _accSlayer2 != SlayerName.None ||
        (_accSkillBonusIds?.Count ?? 0) > 0;

    public void Purge(Mobile from)
    {
        if (Parent is Mobile owner)
        {
            DetachWeapon(owner);
            RebuildAosAttributes();
            _aosAttributes.RemoveStatBonuses(owner);
            _aosAttributes.AddStatBonuses(owner); // adds zeros, completing the update cycle
            RemoveSkillMods(owner);
            owner.CheckStatTimers();
            owner.UpdateResistances();
        }
        else
        {
            RebuildAosAttributes();
        }

        // Zero all accumulations
        _accStrBonus = 0; _accDexBonus = 0; _accIntBonus = 0;
        _accBonusHits = 0; _accBonusStam = 0; _accBonusMana = 0;
        _accLRC = 0; _accFC = 0; _accFCR = 0; _accDI = 0;
        _accHCI = 0; _accDCI = 0; _accSSI = 0; _accSDI = 0;
        _accLMC = 0; _accRP = 0; _accEP = 0; _accLuck = 0;
        _accRegenHits = 0; _accRegenStam = 0; _accRegenMana = 0;
        _accPhysResist = 0; _accFireResist = 0; _accColdResist = 0;
        _accPoisonResist = 0; _accEnergyResist = 0;
        _accHitDispel = 0; _accHitFireball = 0; _accHitHarm = 0;
        _accHitMagicArrow = 0; _accHitLightning = 0;
        _accHitLowerAttack = 0; _accHitLowerDefend = 0;
        _accHitLeechHits = 0; _accHitLeechStam = 0; _accHitLeechMana = 0;
        _accHitColdArea = 0; _accHitFireArea = 0; _accHitPoisonArea = 0;
        _accHitEnergyArea = 0; _accHitPhysicalArea = 0;
        _accSelfRepair = 0; _accMageArmor = 0;
        _accSlayer  = SlayerName.None;
        _accSlayer2 = SlayerName.None;
        _accSkillBonusIds.Clear();
        _accSkillBonusValues.Clear();
        _knownFormCategories.Clear();

        // Reset category and appearance — forms journal kept as cosmetic collection
        _lockedCategory   = MimicCategory.None;
        _activeFormItemID = PetMimicArt.DormantItemID;
        ItemID            = PetMimicArt.DormantItemID;
        Hue               = PetMimicArt.MimicHue;

        // Return mimic to the neutral Talisman slot now that it's dormant
        UpdateFormLayer(Layer.Talisman);

        RebuildAosAttributes();
        InvalidateProperties();
    }

    // ── HP regen (lazy — computed on access points) ───────────────────────

    // Regen rate scales down (faster) as meals eaten and gem HP grow.
    // Formula: base / (1 + meals*mealScale + bonusMaxHP*gemScale), floored at MinRegenMinutesPerHP.
    private double EffectiveRegenMinutesPerHP()
    {
        var meals      = (double)(_knownFormItemIDs?.Count ?? 0);
        var gemBonus   = (double)_bonusMaxHP;
        var selfRepair = (double)_accSelfRepair;
        var divisor    = 1.0
            + meals      * ClusterFPetMimicSettings.MealRegenScale
            + gemBonus   * ClusterFPetMimicSettings.GemRegenScale
            + selfRepair * ClusterFPetMimicSettings.SelfRepairRegenScale;
        var rate = ClusterFPetMimicSettings.RegenMinutesPerHP / divisor;
        return Math.Max(rate, ClusterFPetMimicSettings.MinRegenMinutesPerHP);
    }

    private void ApplyPendingRegen()
    {
        if (_currentHP >= MaxHP) { return; }

        var elapsed = Core.Now - new DateTime(_lastRegenTicks, DateTimeKind.Utc);
        var gain = (int)(elapsed.TotalMinutes / EffectiveRegenMinutesPerHP());
        if (gain <= 0) { return; }

        _currentHP = Math.Min(_currentHP + gain, MaxHP);
        _lastRegenTicks = Core.Now.Ticks;
    }

    private void HandleHPDepleted()
    {
        if (Parent is not Mobile m) { return; }

        DetachWeapon(m);
        m.RemoveItem(this);
        m.AddToBackpack(this);
        m.SendMessage(0x22, "Your pet mimic collapses from exhaustion and falls into your pack!");
        m.SendMessage(0x59, "Restore its health with potions or bandages before re-equipping it.");
        Effects.PlaySound(m.Location, m.Map, 0x1FE);
    }

    // ── Double-click to equip ─────────────────────────────────────────────

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack) || IsChildOf(from))
        {
            from.EquipItem(this);
        }
        else
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
        }
    }

    // ── Equip guard ───────────────────────────────────────────────────────

    public override bool CanEquip(Mobile m)
    {
        ApplyPendingRegen();

        if (_currentHP <= 0)
        {
            m.SendMessage(0x22, "Your pet mimic is too exhausted to be equipped. Restore its health first.");
            return false;
        }

        return base.CanEquip(m);
    }

    // ── Feeding logic (healing items) ─────────────────────────────────────

    public void TryFeed(Mobile from, Item item)
    {
        ApplyPendingRegen();

        var gemBonus = GetGemBonus(item);
        if (gemBonus > 0)
        {
            TryGrowMaxHP(from, item, gemBonus, item.Name ?? "gem");
            return;
        }

        if (item is LesserHealPotion)  { TryRestoreHP(from, item, ClusterFPetMimicSettings.LesserHealPotionAmount, "lesser heal potion"); return; }
        if (item is HealPotion)        { TryRestoreHP(from, item, ClusterFPetMimicSettings.HealPotionAmount, "heal potion"); return; }
        if (item is GreaterHealPotion) { TryRestoreHP(from, item, ClusterFPetMimicSettings.GreaterHealPotionAmount, "greater heal potion"); return; }
        if (item is Bandage)           { TryRestoreHP(from, item, ClusterFPetMimicSettings.BandageHealAmount, "bandage"); return; }

        // Talismans can always be eaten regardless of locked category — bypass TryEat's category check.
        if (item is BaseTalisman)
        {
            from.SendGump(new PetMimicFeedGump(from, this, item));
            return;
        }

        TryEat(from, item);
    }

    // Gems permanently grow the maximum HP pool.
    private void TryGrowMaxHP(Mobile from, Item consumed, int amount, string sourceName)
    {
        _bonusMaxHP += amount;
        _currentHP = Math.Min(_currentHP + amount, MaxHP);
        InvalidateProperties();

        consumed.Delete();

        from.SendMessage(0x44, $"Your pet mimic devours the {sourceName}! Maximum health grows to {MaxHP}. ({_currentHP}/{MaxHP})");
        Effects.PlaySound(from.Location, from.Map, 0x57);
        from.FixedParticles(0x376A, 9, 32, 5030, EffectLayer.Waist);
    }

    // Potions and bandages restore missing current HP.
    private void TryRestoreHP(Mobile from, Item consumed, int amount, string sourceName)
    {
        if (_currentHP >= MaxHP)
        {
            from.SendMessage(0x59, $"Your pet mimic is already at full health. ({_currentHP}/{MaxHP})");
            return;
        }

        var before = _currentHP;
        _currentHP = Math.Min(_currentHP + amount, MaxHP);
        var healed = _currentHP - before;

        consumed.Consume();
        InvalidateProperties();

        from.SendMessage(0x44, $"Your pet mimic absorbs the {sourceName}, recovering {healed} health. ({_currentHP}/{MaxHP})");
        Effects.PlaySound(from.Location, from.Map, 0x57);
        from.FixedParticles(0x376A, 9, 32, 5030, EffectLayer.Waist);
    }

    private static int GetGemBonus(Item item) => item switch
    {
        Citrine      => ClusterFPetMimicSettings.CitrineHeal,
        Amber        => ClusterFPetMimicSettings.AmberHeal,
        Tourmaline   => ClusterFPetMimicSettings.TourmalineHeal,
        Amethyst     => ClusterFPetMimicSettings.AmethystHeal,
        Sapphire     => ClusterFPetMimicSettings.SapphireHeal,
        StarSapphire => ClusterFPetMimicSettings.StarSapphireHeal,
        Ruby         => ClusterFPetMimicSettings.RubyHeal,
        Emerald      => ClusterFPetMimicSettings.EmeraldHeal,
        Diamond      => ClusterFPetMimicSettings.DiamondHeal,
        _            => 0
    };

    // ── Context menu ──────────────────────────────────────────────────────

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        ApplyPendingRegen();
        base.GetContextMenuEntries(from, ref list);
        list.Add(new FeedEntry(this));
        list.Add(new CheckStatusEntry(this));
    }

    // "Eat" (6135)
    private class FeedEntry : ContextMenuEntry
    {
        private readonly PetMimic _mimic;

        public FeedEntry(PetMimic mimic) : base(6135)
        {
            _mimic = mimic;
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (_mimic.Deleted) { return; }
            from.SendMessage(0x44, "What would you like to feed your pet mimic?");
            from.Target = new FeedTarget(_mimic);
        }
    }

    // "Talk" (6146) — opens status gump
    private class CheckStatusEntry : ContextMenuEntry
    {
        private readonly PetMimic _mimic;

        public CheckStatusEntry(PetMimic mimic) : base(6146)
        {
            _mimic = mimic;
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (_mimic.Deleted) { return; }
            from.SendGump(new PetMimicStatusGump(from, _mimic));
        }
    }

    private class FeedTarget : Target
    {
        private readonly PetMimic _mimic;

        public FeedTarget(PetMimic mimic) : base(3, false, TargetFlags.None)
        {
            _mimic = mimic;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_mimic.Deleted) { return; }

            if (targeted is Item item)
            {
                _mimic.TryFeed(from, item);
            }
            else
            {
                from.SendMessage(0x59, "You can only feed items to your pet mimic.");
            }
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            from.SendMessage(0x59, "You decide not to feed the mimic anything.");
        }
    }

    // ── Tooltip ───────────────────────────────────────────────────────────

    public override void AddNameProperties(IPropertyList list)
    {
        ApplyPendingRegen();
        base.AddNameProperties(list);
        list.Add($"Health: {_currentHP} / {MaxHP}");

        if (_bonusMaxHP > 0)
        {
            list.Add($"Max HP Bonus: +{_bonusMaxHP}");
        }

        if (_lockedCategory != MimicCategory.None)
        {
            list.Add($"Type: {SuperCategoryName(_lockedCategory)} ({_lockedCategory})");
            list.Add($"Known forms: {_knownFormItemIDs.Count}");
        }
        else
        {
            list.Add("Dormant — feed it a weapon or armor piece to awaken it");
        }

        if (_accStrBonus > 0 || _accDexBonus > 0 || _accIntBonus > 0)
        {
            list.Add($"Stats: Str+{_accStrBonus} Dex+{_accDexBonus} Int+{_accIntBonus}");
        }

        if (_accLRC > 0 || _accFC > 0 || _accFCR > 0 || _accDI > 0)
        {
            list.Add($"LRC:{_accLRC}% FC:{_accFC} FCR:{_accFCR} DI:{_accDI}%");
        }
    }
}
