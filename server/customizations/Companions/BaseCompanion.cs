// cc-P264, companions B1: the companion base and its record (docs/companions/companions-squire-evo-research.md, the
// review's corrections in docs/companions/p41-companions-review.md, and the B1 line of notes/cc-P45-companion-probes.md
// section 2; all in shard-migration).
//
// The record lives on the creature itself (P41 2.5, review item 6): a companion has no account to key on, the record
// moves with it on a transfer, and it keeps its own loops and rank when its owner resets. Sex is Mobile.Female.
//
// What B1 makes the same as a player (P41 section 2):
// - Skill caps: each skill's cap is the companion's max level and Skills.Cap is 58 times that, as F-12 sets a player's
//   (ClusterFSkillCaps.ApplyTo(BaseCompanion)); the creature default of 700.0 total would floor every gain past it.
// - Hits, stamina and mana are the player's formulas (PlayerMobile.HitsMax: Str / 2 + 50 + Hit Point Increase, at most
//   25 under ML), never the creature seeds.
// - Melee comes from the weapon: a companion never calls SetDamage (BaseWeapon.GetBaseDamageRange uses the creature's
//   DamageMin/Max whenever they are set), and equipping a weapon copies its damage-type split, which pinned otherwise
//   reads off the creature's own fields for any BaseCreature (BaseWeapon.GetDamageTypes).
// - Gains: ClusterFSkillGain.ShouldBoost takes a controlled, living companion as it takes a player.
//
// Engine seams it relies on (P45): CanAutoStable => false (ours, BaseCreature-PlayerMobile-can-auto-stable.patch) and
// StandsDownOnCommand => false (so it answers a direct attack). Logging out with the owner is CompanionLogout.
//
// Slot cost follows rank (CompanionSettings.SlotCostForRank). ControlSlots is a plain field with no follower adjustment,
// so a rank change takes the old cost off the owner, sets the new one and adds it back (P41 11.3); otherwise the owner's
// Followers drifts until a restart rebuilds it.

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Companions;

[SerializationGenerator(0, false)]
public partial class BaseCompanion : BaseCreature
{
    // --- the record ----------------------------------------------------------------------------------------------------

    // Never reused, never a Serial (P41 section 8). CompanionLineage hands it out.
    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private long _lineageId;

    // Picked at purchase (design, "Buying a companion"); locked.
    [SerializableField(1, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private CompanionPath _path;

    // Chosen at loop 3 within the path; locked.
    [SerializableField(2, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private CompanionBranch _branch;

    // Rolled at creation, fixed for life; passed down by breeding.
    [SerializableField(3, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private Personality _personality;

    // The companion's own loops, never past its owner's (the gate is the looping step's). Max level follows the ladder.
    [SerializableField(4, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private int _loop;

    // 0 Squire ... 5 Legend (design: rank and loop are one track; how ranks fall on F-12's 22-step ladder is open).
    [SerializableField(5, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private int _rank;

    [SerializableField(6, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private int _level;

    // Earned XP; the leveling step writes it.
    [SerializableField(7, setter: "internal")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private long _xp;

    // Shown in the lore window or as a title, never in the name (design, decided 12:52). Set from speech.json in B2.
    [SerializableField(8)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string? _epithet;

    // Breeding readiness (design, "Breeding"): parents by lineage id, 0 for none; generation 0 for a bought companion.
    [SerializableField(9, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private long _parent1Id;

    [SerializableField(10, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private long _parent2Id;

    [SerializableField(11, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private int _generation;

    [SerializableField(12)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _nextBreedTime;

    // Each skill's highest base, in tenths, indexed by SkillName: the loop-memory input (P45 B1, "per-skill peaks").
    [SerializableField(13, setter: "private")]
    private int[] _skillPeaks;

    // Left the world with its owner at logout (CompanionLogout); comes back at the owner's login.
    [SerializableField(14, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private bool _awayWithOwner;

    // --- construction --------------------------------------------------------------------------------------------------

    [Constructible]
    public BaseCompanion() : this(CompanionPath.Blade, Personality.Commoner, Utility.RandomBool())
    {
    }

    public BaseCompanion(CompanionPath path, Personality personality, bool female)
        : base(AIFor(path), FightMode.Aggressor)
    {
        _lineageId = CompanionLineage.NextId();
        _path = path;
        _personality = personality;
        _skillPeaks = new int[SkillInfo.Table.Length];

        Female = female;
        Body = female ? 0x191 : 0x190;
        Name = NameList.RandomName(female ? "female" : "male");
        Hue = Race.Human.RandomSkinHue();
        HairItemID = Race.Human.RandomHair(female);
        HairHue = Race.Human.RandomHairHue();

        // A new character's spread until classes.json (B2) gives each path its own.
        InitStats(50, 40, 30);

        ControlSlots = CompanionSettings.SlotCostForRank(0);
        Tamable = false;

        AddItem(new Backpack());
        ClusterFSkillCaps.ApplyTo(this);
    }

    private static AIType AIFor(CompanionPath path) =>
        path switch
        {
            CompanionPath.Bow    => AIType.AI_Archer,
            CompanionPath.Arcane => AIType.AI_Mage,
            _                    => AIType.AI_Melee // Blade, Support, Rogue until the companion AI (B6, B7)
        };

    // --- the record's rules ----------------------------------------------------------------------------------------------

    /// <summary>The max level at this companion's loop, on F-12's ladder: each skill's cap.</summary>
    [CommandProperty(AccessLevel.GameMaster)]
    public int MaxLevel => CompanionSettings.MaxLevelAtLoop(_loop);

    /// <summary>Breeding marks squires breedable (design, "Breeding"); the breeding chat decides who may.</summary>
    public virtual bool IsBreedable => true;

    /// <summary>The peak of one skill, as a value (100.0 for 1000 tenths).</summary>
    public double SkillPeak(SkillName skill) =>
        (int)skill < _skillPeaks.Length ? _skillPeaks[(int)skill] / 10.0 : 0.0;

    /// <summary>Sets the loop (0 to the ladder's top): the caps move with it and the level stays inside them.</summary>
    public void SetLoop(int loop)
    {
        Loop = Math.Clamp(loop, 0, CompanionSettings.TopLoop);
        Level = Math.Min(_level, MaxLevel);
        ClusterFSkillCaps.ApplyTo(this);
    }

    public void SetLevel(int level) => Level = Math.Clamp(level, 0, MaxLevel);

    /// <summary>Sets the rank (0 to 5) and moves the owner's follower count to the new slot cost.</summary>
    public void SetRank(int rank)
    {
        Rank = Math.Clamp(rank, 0, CompanionSettings.MaxRank);
        ApplySlotCost();
    }

    public void SetBranch(CompanionBranch branch) => Branch = branch;

    /// <summary>Records breeding parents and generation (the breeding chat's heir calls this).</summary>
    public void SetLineage(long parent1Id, long parent2Id, int generation)
    {
        Parent1Id = parent1Id;
        Parent2Id = parent2Id;
        Generation = Math.Max(0, generation);
    }

    /// <summary>Re-counts the owner's followers when the slot cost for this rank differs from ControlSlots.</summary>
    public void ApplySlotCost()
    {
        var cost = CompanionSettings.SlotCostForRank(_rank);

        if (ControlSlots == cost)
        {
            return;
        }

        if (Controlled && ControlMaster != null)
        {
            RemoveFollowers();
            ControlSlots = cost;
            AddFollowers();
        }
        else
        {
            ControlSlots = cost;
        }
    }

    internal void SetAway(bool away) => AwayWithOwner = away;

    // --- player parity -------------------------------------------------------------------------------------------------

    public override int HitsMax
    {
        get
        {
            var bonus = AosAttributes.GetValue(this, AosAttribute.BonusHits);

            if (Core.ML && bonus > 25)
            {
                bonus = 25;
            }

            return Str / 2 + 50 + bonus;
        }
    }

    public override int StamMax => Dex + AosAttributes.GetValue(this, AosAttribute.BonusStam);

    public override int ManaMax => Int + AosAttributes.GetValue(this, AosAttribute.BonusMana);

    public override bool CanAutoStable => false;

    public override bool StandsDownOnCommand => false;

    public override void OnSkillChange(SkillName skill, double oldBase)
    {
        base.OnSkillChange(skill, oldBase);

        var i = (int)skill;
        var now = Skills[skill].BaseFixedPoint;

        if (i < _skillPeaks.Length && now > _skillPeaks[i])
        {
            _skillPeaks[i] = now;
            this.MarkDirty();
        }
    }

    public override void OnItemAdded(Item item)
    {
        base.OnItemAdded(item);

        if (item is BaseWeapon weapon)
        {
            TakeDamageTypes(weapon);
        }
    }

    public override void OnItemRemoved(Item item)
    {
        base.OnItemRemoved(item);

        if (item is BaseWeapon)
        {
            TakeDamageTypes(Weapon as BaseWeapon);
        }
    }

    /// <summary>
    /// The weapon's split as a player would deal it (BaseWeapon.GetDamageTypes' non-creature branch, reached by passing
    /// no wielder), onto the fields pinned reads for any creature. Bare hands: all physical.
    /// </summary>
    public void TakeDamageTypes(BaseWeapon? weapon)
    {
        int phys = 100, fire = 0, cold = 0, pois = 0, nrgy = 0, chaos = 0, direct = 0;

        if (weapon != null && weapon.Parent == this && weapon is not Fists)
        {
            weapon.GetDamageTypes(null, out phys, out fire, out cold, out pois, out nrgy, out chaos, out direct);
        }

        PhysicalDamage = phys;
        FireDamage = fire;
        ColdDamage = cold;
        PoisonDamage = pois;
        EnergyDamage = nrgy;
        ChaosDamage = chaos;
        DirectDamage = direct;
    }

    [AfterDeserialization]
    private void AfterCompanionLoad()
    {
        CompanionLineage.Observe(_lineageId);

        if (_skillPeaks == null || _skillPeaks.Length != SkillInfo.Table.Length)
        {
            var peaks = new int[SkillInfo.Table.Length];
            Array.Copy(_skillPeaks ?? [], peaks, Math.Min(peaks.Length, _skillPeaks?.Length ?? 0));
            _skillPeaks = peaks;
        }

        // IsStabled is not saved: a companion away with its owner gets it back, which keeps the dead-pet sweep from
        // deleting a ghost companion while its owner is logged out (P45 probe 6g).
        if (_awayWithOwner)
        {
            IsStabled = true;
        }

        TakeDamageTypes(Weapon as BaseWeapon);
        ClusterFSkillCaps.ApplyTo(this);
    }
}
