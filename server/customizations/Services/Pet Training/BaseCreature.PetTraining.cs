// cc-P121: ServUO's BaseCreature members for the ability profile (ServUO BaseCreature.cs:577-581 and 635-678), on pinned's
// BaseCreature through a partial declaration (pinned's is `public partial class BaseCreature`, BaseCreature.cs:137),
// so a ported creature calls SetSpecialAbility(SpecialAbility.X) exactly as its ServUO source does. No field is added
// and nothing here is serialized: the profile lives in AbilityProfileStore (decision 2a).
//
// cc-P130: the training members, ServUO BaseCreature.cs:575-760 and 3790-3870: TrainingProfile, the TrainingDefinition
// virtual, ControlSlotsMin and ControlSlotsMax, CurrentTameSkill, MaxTameRequirement, TryGetWeaponAbility (the read
// point pinned's BaseWeapon now calls, server/patches/BaseWeapon-trained-weapon-ability.patch, which is the seam cc-P121
// left for trained weapon abilities), OnAfterTame, the slot calculation and the tame-requirement adjustment. Their
// state is in PetTrainingStore (decision 2a again); min and max slots are derived there (the store's header says how).
// OnControlSlotsChanged is the one hook pinned's generated ControlSlots setter calls
// (server/patches/BaseCreature-pet-training-slots.patch adds `fieldChanged:` to the field), where ServUO's hand-written
// setter computes the slots on a constructor's assignment (BaseCreature.cs:3843-3861). SetAverage is called from
// pinned's SetHits/SetStam/SetMana/SetStr/SetDex/SetInt/SetResistance/SetSkill(min, max) by the same patch, where
// ServUO's own setters call it (:5078-5234).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Server.Items;

namespace Server.Mobiles;

public partial class BaseCreature
{
    /// <summary>
    /// The profile, made if the creature has none, so [props] on a stock dragon shows its breath. A profile holding only
    /// natural abilities is never written to the save (AbilityProfile.IsEmpty), so looking costs nothing.
    /// canModify (cc-P233): the property has no setter, and pinned's props gump draws a row's open arrow only for a
    /// property with a setter or an attribute that says canModify (PropsGump.cs:141, :210), so without it staff saw
    /// "..." and could not open the [PropertyObject]. Opening it edits the same profile [props already made.
    /// </summary>
    [CommandProperty(AccessLevel.GameMaster, canModify: true)]
    public AbilityProfile AbilityProfile => PetTrainingHelper.GetAbilityProfile(this, true);

    public void SetMagicalAbility(MagicalAbility ability) =>
        PetTrainingHelper.GetAbilityProfile(this, true).AddAbility(ability, false);

    public void SetSpecialAbility(SpecialAbility ability) =>
        PetTrainingHelper.GetAbilityProfile(this, true).AddAbility(ability, false);

    public void SetAreaEffect(AreaEffect ability) =>
        PetTrainingHelper.GetAbilityProfile(this, true).AddAbility(ability, false);

    public void RemoveMagicalAbility(MagicalAbility ability) =>
        PetTrainingHelper.GetAbilityProfile(this, true).RemoveAbility(ability);

    public void RemoveSpecialAbility(SpecialAbility ability) =>
        PetTrainingHelper.GetAbilityProfile(this, true).RemoveAbility(ability);

    public void RemoveAreaEffect(AreaEffect ability) =>
        PetTrainingHelper.GetAbilityProfile(this, true).RemoveAbility(ability);

    public bool HasAbility(object o) => PetTrainingHelper.GetAbilityProfile(this, true).HasAbility(o);

    // -- cc-P130: training -------------------------------------------------------------------------------------------

    public static double MaxTameRequirement = 108.0;

    /// <summary>
    /// The training state, or null until a tamer has begun training (the lore gump creates it). canModify as above
    /// (cc-P233): [props opens it once it exists; while it is null the press re-sends the creature's own page.
    /// </summary>
    [CommandProperty(AccessLevel.GameMaster, canModify: true)]
    public TrainingProfile TrainingProfile => PetTrainingStore.GetTrainingProfile(this, false);

    [CommandProperty(AccessLevel.GameMaster)]
    public double BardingDifficulty => BaseInstrument.GetBaseDifficulty(this);

    /// <summary>ServUO: a creature's own definition, before the table's (CrimsonDrake and PlatinumDrake override it).</summary>
    public virtual TrainingDefinition TrainingDefinition => null;

    /// <summary>
    /// ServUO BaseCreature.TryGetWeaponAbility: a trained weapon ability from the profile, else the creature's own
    /// GetWeaponAbility. Pinned's BaseWeapon.OnSwing calls this (the patch) where it called GetWeaponAbility.
    /// </summary>
    public virtual WeaponAbility TryGetWeaponAbility()
    {
        var profile = PetTrainingHelper.GetAbilityProfile(this);

        if (profile?.WeaponAbilities is { Length: > 0 } abilities)
        {
            return abilities[Utility.Random(abilities.Length)];
        }

        return GetWeaponAbility();
    }

    /// <summary>
    /// ServUO BaseCreature.ControlSlotsMin: the definition's minimum, one lower when the creature rolled low at
    /// construction (CalculateSlots), and never above the creature's own slots (ServUO's load rule).
    /// </summary>
    [CommandProperty(AccessLevel.Administrator)]
    public int ControlSlotsMin
    {
        get
        {
            var def = PetTrainingHelper.GetTrainingDefinition(this);

            if (def == null)
            {
                return ControlSlots;
            }

            var min = def.ControlSlotsMin;

            if (PetTrainingStore.Get(this, false)?.SlotsLowered == true)
            {
                min = Math.Max(1, min - 1);
            }

            return Math.Min(min, ControlSlots);
        }
    }

    [CommandProperty(AccessLevel.Administrator)]
    public int ControlSlotsMax
    {
        get
        {
            var def = PetTrainingHelper.GetTrainingDefinition(this);

            return def == null ? ControlSlots : Math.Max(def.ControlSlotsMax, ControlSlots);
        }
    }

    /// <summary>
    /// ServUO BaseCreature.CurrentTameSkill: the taming skill the creature asks for now. MinTameSkill until training
    /// raises it (ServUO's MinTameSkill setter seeds it; TrainingProfile.OnTrain adds 21, 22, 3 or 2 per step).
    /// </summary>
    [CommandProperty(AccessLevel.GameMaster)]
    public double CurrentTameSkill
    {
        get => PetTrainingStore.Get(this, false)?.CurrentTameSkill ?? MinTameSkill;
        set
        {
            var record = PetTrainingStore.Get(this, true);

            if (record != null)
            {
                record.CurrentTameSkill = value;
            }
        }
    }

    /// <summary>
    /// ServUO BaseCreature.OnAfterTame. ServUO's base scales the stats on a first tame when StatLossAfterTame; pinned's
    /// AnimalTaming does that itself before this is called, so the base does nothing here. A creature overrides it for
    /// what it does after taming (ServUO's SabertoothedTiger and FrostDragon do).
    /// </summary>
    public virtual void OnAfterTame(Mobile tamer)
    {
    }

    /// <summary>
    /// OnAfterTame with whether this is the creature's first tame (cc-P458). ServUO calls OnAfterTame before adding
    /// the owner, so its overrides test Owners.Count == 0; pinned adds the owner first, so an override that needs the
    /// first tame overrides this one instead. The default calls ServUO's one-argument hook.
    /// </summary>
    public virtual void OnAfterTame(Mobile tamer, bool firstTame) => OnAfterTame(tamer);

    public virtual double AverageThreshold => 0.33;

    private static readonly ConditionalWeakTable<BaseCreature, List<double>> _initAverage = new();

    // ServUO BaseCreature._SlotLowerables (BaseCreature.cs:696-702), by name: a type that is not on this shard is
    // simply never matched.
    public static readonly string[] SlotLowerableNames =
    {
        "Nightmare", "Najasaurus", "RuneBeetle", "GreaterDragon", "FrostDragon",
        "WhiteWyrm", "Reptalon", "DragonTurtleHatchling", "Phoenix", "FrostMite",
        "DireWolf", "Skree", "HighPlainsBoura", "LesserHiryu", "DragonWolf",
        "BloodFox"
    };

    private bool CanLowerSlot() => SlotLowerableNames.Any(n => n == GetType().Name);

    /// <summary>ServUO BaseCreature.SetAverage: where in its range a constructor's roll landed, for CalculateSlots.</summary>
    internal void SetAverage(double min, double max, double value)
    {
        if (PetTrainingHelper.Enabled && CanLowerSlot() && max > min)
        {
            if (!_initAverage.TryGetValue(this, out var list))
            {
                list = new List<double>();
                _initAverage.Add(this, list);
            }

            list.Add((value - min) / (max - min));
        }
    }

    /// <summary>
    /// ServUO BaseCreature.CalculateSlots: the definition's range, the minimum one lower when the construction rolls
    /// averaged at or under AverageThreshold. Here the result is the store's SlotsLowered flag; the range itself is
    /// derived (ControlSlotsMin, ControlSlotsMax).
    /// </summary>
    public void CalculateSlots(int slots)
    {
        var def = PetTrainingHelper.GetTrainingDefinition(this, slots);

        if (def == null || !_initAverage.TryGetValue(this, out var average))
        {
            return;
        }

        if (average.Count > 0 && average.Sum() / average.Count <= AverageThreshold)
        {
            var record = PetTrainingStore.Get(this, true);

            if (record != null)
            {
                record.SlotsLowered = true;
            }
        }

        _initAverage.Remove(this);
    }

    /// <summary>ServUO BaseCreature.AdjustTameRequirements, line for line.</summary>
    public void AdjustTameRequirements()
    {
        if (ControlSlots <= ControlSlotsMin)
        {
            CurrentTameSkill = MinTameSkill;
        }
        else
        {
            CurrentTameSkill = (ControlSlots - ControlSlotsMin) * 21 + 1;
        }

        if (CurrentTameSkill > MaxTameRequirement)
        {
            CurrentTameSkill = MaxTameRequirement;
        }
    }

    // ServUO BaseCreature.ControlSlots setter (BaseCreature.cs:3843-3861): on the first assignment (a constructor's) with
    // training on, compute the slots and clamp the assignment to the definition's range: any other value becomes the
    // minimum (a stock Dragon's 3 is 4 since Time of Legends, a Skree's 4 is 3), above the maximum becomes the maximum.
    // Later assignments (training's ControlSlots++, staff) pass through, as ServUO's do once min and max are set. The
    // generator calls this after the field is written, so the clamp writes the field itself; no master exists at
    // construction. A creature settles when it is first placed in the world (ServUO's min and max are set by then,
    // or never: a constructor that leaves the default 1 never runs the setter, and training's first increment must
    // not be the assignment that does), and every creature in a loaded world is settled (PetTrainingStore.OnWorldLoad).
    private void OnControlSlotsChanged(int oldValue, int newValue)
    {
        if (!PetTrainingHelper.Enabled || PetTrainingStore.IsSettled(this))
        {
            return;
        }

        PetTrainingStore.MarkSettled(this);

        if (Map != null && Map != Map.Internal)
        {
            return; // already in the world: not a constructor's assignment
        }

        var def = PetTrainingHelper.GetTrainingDefinition(this, newValue);

        CalculateSlots(newValue);

        if (def == null)
        {
            return;
        }

        var min = def.ControlSlotsMin;

        if (PetTrainingStore.Get(this, false)?.SlotsLowered == true)
        {
            min = Math.Max(1, min - 1);
        }

        if (newValue != min)
        {
            _controlSlots = min;
        }
        else if (newValue > def.ControlSlotsMax)
        {
            _controlSlots = def.ControlSlotsMax;
        }
    }
}
