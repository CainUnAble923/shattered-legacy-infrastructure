// cc-P121: Pet Training part 1, the ability core. Ported from ServUO Scripts/Services/Pet Training/PetTrainingHelper.cs,
// the profile-access half only: the MagicalAbility enum (:62-115), Enabled (:119), GetAbilityProfile (:196-204), the
// MagicalAbilities table (:228-248) and RuleBreakers (:600-603).
//
// cc-P130: the training half, the rest of that file: PetStat (:13-26) and Class (:28-59), the accessors (:127-224), the
// special-ability, area-effect and weapon-ability packages (:250-357, :365-595, :605-651), the per-type definitions
// (:655-783), the training points (:786-920), the training helpers (:924-1313), the skill categories (:1316-1354), the
// localizations (:1357-1667) and the [PetTrainTest command (:1669-1743). The tables are the same entries in the same
// order; three shapes change, each recorded in notes/cc-P130-pet-training.md:
//  - the definitions are keyed by type NAME and resolved on first use (MissingDefinitionTypes lists the ServUO types this
//    shard does not have; DragonTurtleHatchling was one until cc-P198), where ServUO's typeof() needs every type at
//    compile time;
//  - ColdWind is not a pinned weapon ability, so it is absent from WeaponAbilities and the Wep packages that list it
//    (2, 5, 6, 7, 8, 10, 11, 12), and from the localization table (it was the last row);
//  - the tables build lazily on first use, since the test host does not run Configure/Initialize.
// VvVMount's definition (:155-166) is dropped: no such creature here. Decision 4: EvoPet and PackMule are excluded by
// type (IsExcluded): no definition, no training profile, so no points and no training pages.
//
// One change of shape, by Chase's decision 2a (2026-10-15): ServUO keeps the profile in a BaseCreature field and saves it
// in BaseCreature's own save. Here it lives in AbilityProfileStore, a side table keyed by the creature, with its own save
// file, so BaseCreature's save format is untouched. Training state lives in PetTrainingStore the same way.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Commands;
using Server.Items;
using Server.Network;
using Server.SkillHandlers;

namespace Server.Mobiles;

[Flags]
public enum PetStat
{
    Str,
    Dex,
    Int,
    Hits,
    Stam,
    Mana,
    RegenHits,
    RegenStam,
    RegenMana,
    BaseDamage
}

[Flags]
public enum Class
{
    None = 0x00000000,
    Untrainable = 0x00000001,
    Magical = 0x00000002,
    Necromantic = 0x00000004,
    Tokuno = 0x00000008,
    StickySkin = 0x00000010,
    Clawed = 0x00000020,
    Tailed = 0x00000040,
    Insectoid = 0x00000080,
    Restricted = 0x00000100,

    MagicalAndNecromantic = Magical | Necromantic,
    MagicalAndClawed = Magical | Clawed,
    MagicalAndTailed = Magical | Tailed,
    ClawedAndTailed = Clawed | Tailed,
    MagicalAndInsectoid = Magical | Insectoid,
    StickySkinAndTailed = StickySkin | Tailed,
    TailedAndNecromantic = Tailed | Necromantic,

    ClawedNecromanticAndTokuno = Clawed | Necromantic | Tokuno,
    MagicalClawedAndTailed = MagicalAndClawed | Tailed,
    MagicalNecromanticAndTokuno = MagicalAndNecromantic | Tokuno,
    ClawedTailedAndTokuno = ClawedAndTailed | Tokuno,
    ClawedTailedAndNecromantic = ClawedAndTailed | Necromantic,

    MagicalClawedTailedAndNecromantic = MagicalClawedAndTailed | Necromantic,
    ClawedTailedNecromanticAndTokuno = ClawedNecromanticAndTokuno | Tailed,
    ClawedTailedMagicalAndTokuno = MagicalClawedAndTailed | Tokuno,

    MagicalClawedTailedNecromanticAndTokuno = MagicalClawedTailedAndNecromantic | Tokuno
}

[Flags]
public enum MagicalAbility
{
    None = 0x00000000,

    // Magical Ability
    Piercing = 0x00000001,
    Bashing = 0x00000002,
    Slashing = 0x00000004,
    BattleDefense = 0x00000008,
    WrestlingMastery = 0x00000010,

    // Magical Schools
    Poisoning = 0x00000020,
    Bushido = 0x00000040,
    Ninjitsu = 0x00000080,
    Discordance = 0x00000100,
    MageryMastery = 0x00000200,
    Mysticism = 0x00000400,
    Spellweaving = 0x00000800,
    Chivalry = 0x00001000,
    Necromage = 0x00002000,
    Necromancy = 0x00004000,
    Magery = 0x00008000,

    Tokuno = Bushido | Ninjitsu,
    SabreToothedTiger = Bashing | Piercing | Poisoning,

    Vartiety = Piercing | Slashing | WrestlingMastery,
    Variety1 = Bashing | Vartiety,
    Variety2 = Bashing | Chivalry | Discordance | MageryMastery | Mysticism | Necromage | Necromancy | Piercing | Poisoning |
               Slashing | Spellweaving | WrestlingMastery,
    Variety3 = Bashing | Chivalry | Discordance | MageryMastery | Mysticism | Necromage | Necromancy | Piercing | Poisoning |
               Slashing | Spellweaving | WrestlingMastery | BattleDefense,

    StandardClawedOrTailed = SabreToothedTiger | Slashing | WrestlingMastery,
    Tokuno1 = Tokuno | Chivalry | Discordance | MageryMastery | Mysticism | Poisoning | Spellweaving,
    Dragon1 = Bashing | BattleDefense | Chivalry | Discordance | MageryMastery | Mysticism | Piercing | Poisoning | Slashing |
              Spellweaving | WrestlingMastery,
    Dragon2 = Bashing | Chivalry | Discordance | MageryMastery | Mysticism | Piercing | Poisoning | Slashing | Spellweaving |
              WrestlingMastery,
    ColdDrake = Chivalry | Discordance | Poisoning | Mysticism | Spellweaving,
    Cusidhe = ColdDrake | WrestlingMastery,
    Wolf = Bashing | Tokuno | Necromage | Necromancy | Piercing | Poisoning | Slashing | WrestlingMastery,
    DragonWolf = Bashing | BattleDefense | Piercing | Poisoning | Slashing | WrestlingMastery,
    DreadSpider = Bashing | Tokuno | Chivalry | Discordance | MageryMastery | Mysticism | Necromage | Necromancy | Piercing |
                  Slashing | Spellweaving | WrestlingMastery,
    DreadWarhorse = Bashing | BattleDefense | Chivalry | Discordance | MageryMastery | Mysticism | Necromage | Necromancy |
                    Piercing | Poisoning | Slashing | Spellweaving | WrestlingMastery,
    GreaterDragon = Chivalry | Discordance | MageryMastery | Mysticism | Poisoning | Spellweaving,
    Hellcat = Bashing | Necromage | Necromancy | Piercing | Poisoning | Slashing | WrestlingMastery,
    GrizzledMare = BattleDefense | Bashing | Necromage | Necromancy | Piercing | Poisoning | Slashing | WrestlingMastery,
    Hiryu = Tokuno | Chivalry | Discordance | Poisoning | Spellweaving | WrestlingMastery,
    LavaLizard = Tokuno | Chivalry | Bashing,
    RuneBeetle = Chivalry | Discordance | MageryMastery | Mysticism | Spellweaving,
    StygianDrake = Bashing | Chivalry | Discordance | Mysticism | Piercing | Poisoning | Slashing | Spellweaving |
                   WrestlingMastery,
    Triceratops = Bashing | Poisoning | Slashing | WrestlingMastery,
    TsukiWolf = Tokuno | Chivalry | Discordance | Mysticism | Necromage | Necromancy | Poisoning | Spellweaving |
                WrestlingMastery,
    Triton = Chivalry | Discordance | MageryMastery | Mysticism | Poisoning | Spellweaving | Bushido | Ninjitsu |
             BattleDefense | Bashing | Piercing | Slashing | WrestlingMastery,
    CoconutCrab = GreaterDragon | BattleDefense | Bashing | Piercing | Slashing | WrestlingMastery
}

public static class PetTrainingHelper
{
    // ServUO: Core.TOL. The shard runs Endless Journey (Configuration/expansion.json), so this is true there.
    public static bool Enabled => Core.TOL;

    /// <summary>Decision 4: evo pets (cc-P65) and pack mules are outside training. Companions and squires do not exist.</summary>
    public static bool IsExcluded(BaseCreature bc) => bc is EvoPet || bc is PackMule;

    // -- Accessors ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// ServUO GetTrainingDefinition (:127-173): the creature's own, else the table's, else an Untrainable default for a
    /// tamable with its own slots as the range. <paramref name="slots" /> stands in for ControlSlots while the slots are
    /// being assigned (BaseCreature.OnControlSlotsChanged), so the default never reads back through ControlSlotsMin.
    /// </summary>
    public static TrainingDefinition GetTrainingDefinition(BaseCreature bc, int? slots = null)
    {
        if (bc == null || IsExcluded(bc))
        {
            return null;
        }

        // First, see if the creature has its own
        var def = bc.TrainingDefinition;

        if (def != null)
        {
            return def;
        }

        // Next, we see if there is a pre-defined def
        Definitions.TryGetValue(bc.GetType(), out def);

        // Last, we give it a default that cannot be trained
        if (def == null && bc.Tamable)
        {
            var s = slots ?? bc.ControlSlots;

            def = new TrainingDefinition(
                bc.GetType(), Class.Untrainable, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, s, s
            );
        }

        return def;
    }

    public static TrainingPoint GetTrainingPoint(object o)
    {
        if (o == null)
        {
            return null;
        }

        foreach (var tp in TrainingPoints)
        {
            // ServUO compares kind by kind (:176-194); boxed enums and the ability singletons compare the same way here.
            if (Equals(tp.TrainPoint, o))
            {
                return tp;
            }
        }

        return null;
    }

    /// <summary>
    /// The creature's stored profile, made and stored when <paramref name="create" /> is set and it has none. A profile
    /// also answers for the abilities the creature has by nature (AbilityProfile's "natural" members), so a stock
    /// creature with no stored profile still lists its breath once asked with create set.
    /// </summary>
    public static AbilityProfile GetAbilityProfile(BaseCreature bc, bool create = false) =>
        bc == null ? null : AbilityProfileStore.Get(bc, create);

    public static TrainingProfile GetTrainingProfile(BaseCreature bc, bool create = false) =>
        bc == null || IsExcluded(bc) ? null : PetTrainingStore.GetTrainingProfile(bc, create);

    public static PlanningProfile GetPlanningProfile(BaseCreature bc, bool create = false) =>
        GetTrainingProfile(bc, create)?.PlanningProfile;

    // -- Magical Ability Table ---------------------------------------------------------------------------------------

    public static readonly MagicalAbility[] MagicalAbilities =
    {
        MagicalAbility.Piercing,
        MagicalAbility.Bashing,
        MagicalAbility.Slashing,
        MagicalAbility.BattleDefense,
        MagicalAbility.WrestlingMastery,
        // Magical Schools
        MagicalAbility.Poisoning,
        MagicalAbility.Bushido,
        MagicalAbility.Ninjitsu,
        MagicalAbility.Discordance,
        MagicalAbility.MageryMastery,
        MagicalAbility.Mysticism,
        MagicalAbility.Spellweaving,
        MagicalAbility.Chivalry,
        MagicalAbility.Necromage,
        MagicalAbility.Necromancy,
        MagicalAbility.Magery
    };

    // -- Special Ability Packages (ServUO LoadDefinitions, :365-595) ---------------------------------------------------

    public static readonly SpecialAbility[] SpecialAbilityNone = Array.Empty<SpecialAbility>();

    public static readonly SpecialAbility[] SpecialAbilityMagical1 =
    {
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.RagingBreath
    };

    public static readonly SpecialAbility[] SpecialAbilityMagical2 =
    {
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.RagingBreath, SpecialAbility.StealLife
    };

    public static readonly SpecialAbility[] SpecialAbilityMagical3 =
    {
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.RagingBreath
    };

    public static readonly SpecialAbility[] SpecialAbilityMagical4 =
    {
        SpecialAbility.AngryFire, SpecialAbility.DragonBreath, SpecialAbility.Inferno, SpecialAbility.RagingBreath
    };

    public static readonly SpecialAbility[] SpecialAbilityNecroMagical =
    {
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.RagingBreath, SpecialAbility.LifeLeech
    };

    public static readonly SpecialAbility[] SpecialAbilityBites =
    {
        SpecialAbility.VenomousBite, SpecialAbility.ViciousBite
    };

    public static readonly SpecialAbility[] SpecialAbilityAnimalStandard =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds
    };

    public static readonly SpecialAbility[] SpecialAbilityBitingAnimal =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.VenomousBite,
        SpecialAbility.ViciousBite
    };

    public static readonly SpecialAbility[] SpecialAbilityClawed =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.GraspingClaw
    };

    public static readonly SpecialAbility[] SpecialAbilityTailed =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.TailSwipe
    };

    public static readonly SpecialAbility[] SpecialAbilityClawedAndTailed =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.GraspingClaw,
        SpecialAbility.TailSwipe
    };

    public static readonly SpecialAbility[] SpecialAbilityInsectoid =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.RuneCorruption
    };

    public static readonly SpecialAbility[] SpecialAbilityMagicalInsectoid =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.RuneCorruption,
        SpecialAbility.AngryFire, SpecialAbility.DragonBreath, SpecialAbility.Inferno, SpecialAbility.RagingBreath
    };

    public static readonly SpecialAbility[] SpecialAbilityStickySkin =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds,
        SpecialAbility.StickySkin
    };

    public static readonly SpecialAbility[] SpecialAbilityBitingStickySkin =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds,
        SpecialAbility.StickySkin, SpecialAbility.VenomousBite, SpecialAbility.ViciousBite
    };

    public static readonly SpecialAbility[] SpecialAbilityTailedAndStickySkin =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.TailSwipe,
        SpecialAbility.StickySkin, SpecialAbility.VenomousBite, SpecialAbility.ViciousBite
    };

    public static readonly SpecialAbility[] SpecialAbilityBitingTailed =
    {
        SpecialAbility.VenomousBite, SpecialAbility.ViciousBite, SpecialAbility.TailSwipe
    };

    public static readonly SpecialAbility[] SpecialAbilityBitingClawedAndTailed =
    {
        SpecialAbility.VenomousBite, SpecialAbility.ViciousBite,
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.GraspingClaw,
        SpecialAbility.TailSwipe
    };

    public static readonly SpecialAbility[] SpecialAbilityClawedAndNecromantic =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.GraspingClaw,
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.RagingBreath, SpecialAbility.LifeLeech
    };

    public static readonly SpecialAbility[] SpecialAbilityClawedTailedAndMagical1 =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.GraspingClaw,
        SpecialAbility.TailSwipe,
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.RagingBreath
    };

    public static readonly SpecialAbility[] SpecialAbilityClawedTailedAndMagical2 =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.GraspingClaw,
        SpecialAbility.TailSwipe,
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.RagingBreath, SpecialAbility.StealLife
    };

    public static readonly SpecialAbility[] SpecialAbilityBaneDragon =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.GraspingClaw,
        SpecialAbility.TailSwipe,
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.RagingBreath, SpecialAbility.VenomousBite, SpecialAbility.ViciousBite
    };

    public static readonly SpecialAbility[] SpecialAbilityDreadSpider =
    {
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.RagingBreath, SpecialAbility.LifeLeech,
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.VenomousBite,
        SpecialAbility.ViciousBite
    };

    public static readonly SpecialAbility[] SpecialAbilityUnicorn =
    {
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.ManaDrain, SpecialAbility.RagingBreath,
        SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.StealLife
    };

    public static readonly SpecialAbility[] SpecialAbilityFireBeetle =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.RuneCorruption,
        SpecialAbility.AngryFire, SpecialAbility.DragonBreath, SpecialAbility.Inferno, SpecialAbility.RagingBreath
    };

    public static readonly SpecialAbility[] SpecialAbilityImp =
    {
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.RagingBreath, SpecialAbility.LifeLeech,
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.GraspingClaw,
        SpecialAbility.TailSwipe,
        SpecialAbility.ViciousBite
    };

    public static readonly SpecialAbility[] SpecialAbilityTsukiWolf =
    {
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.RagingBreath, SpecialAbility.LifeLeech,
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.GraspingClaw,
        SpecialAbility.TailSwipe
    };

    public static readonly SpecialAbility[] SpecialAbilityTriton =
    {
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.ManaDrain, SpecialAbility.RagingBreath, SpecialAbility.Repel,
        SpecialAbility.SearingWounds, SpecialAbility.StealLife, SpecialAbility.StickySkin, SpecialAbility.TailSwipe,
        SpecialAbility.VenomousBite, SpecialAbility.ViciousBite
    };

    public static readonly SpecialAbility[] SpecialAbilitySabreTri =
    {
        SpecialAbility.SearingWounds, SpecialAbility.TailSwipe
    };

    private static SpecialAbility[] _ruleBreakers;

    // ServUO builds this in LoadDefinitions (:555-558); AbilityProfile.IsRuleBreaker reads it.
    public static IReadOnlyList<SpecialAbility> RuleBreakers => _ruleBreakers ??= new[]
    {
        SpecialAbility.SearingWounds, SpecialAbility.TailSwipe, SpecialAbility.DragonBreath, SpecialAbility.LifeLeech,
        SpecialAbility.ViciousBite
    };

    public static readonly SpecialAbility[] SpecialAbilityGrizzledMare =
    {
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LifeLeech, SpecialAbility.LightningForce, SpecialAbility.ManaDrain, SpecialAbility.RagingBreath,
        SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.StealLife, SpecialAbility.VenomousBite,
        SpecialAbility.ViciousBite
    };

    public static readonly SpecialAbility[] SpecialAbilitySkeletalCat =
    {
        SpecialAbility.ManaDrain, SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.GraspingClaw,
        SpecialAbility.TailSwipe, SpecialAbility.ViciousBite, SpecialAbility.VenomousBite, SpecialAbility.LifeLeech
    };

    public static readonly SpecialAbility[] SpecialAbilityCoconutCrab =
    {
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.Inferno,
        SpecialAbility.LightningForce, SpecialAbility.ManaDrain, SpecialAbility.RagingBreath, SpecialAbility.Repel,
        SpecialAbility.SearingWounds, SpecialAbility.VenomousBite, SpecialAbility.ViciousBite
    };

    public static readonly SpecialAbility[] SpecialAbilityPhoenix =
    {
        SpecialAbility.AngryFire, SpecialAbility.ConductiveBlast, SpecialAbility.DragonBreath, SpecialAbility.GraspingClaw,
        SpecialAbility.Inferno, SpecialAbility.LightningForce, SpecialAbility.ManaDrain, SpecialAbility.RagingBreath,
        SpecialAbility.Repel, SpecialAbility.SearingWounds, SpecialAbility.VenomousBite, SpecialAbility.ViciousBite
    };

    // -- Area Effect Packages (:597-651) -----------------------------------------------------------------------------

    public static readonly AreaEffect[] AreaEffectNone = Array.Empty<AreaEffect>();

    public static readonly AreaEffect[] AreaEffectExplosiveGoo =
    {
        AreaEffect.ExplosiveGoo
    };

    public static readonly AreaEffect[] AreaEffectEarthen =
    {
        AreaEffect.EssenceOfEarth, AreaEffect.ExplosiveGoo
    };

    public static readonly AreaEffect[] AreaEffectDisease =
    {
        AreaEffect.AuraOfNausea, AreaEffect.EssenceOfDisease
    };

    public static readonly AreaEffect[] AreaEffectArea1 =
    {
        AreaEffect.EssenceOfEarth, AreaEffect.ExplosiveGoo, AreaEffect.AuraOfEnergy
    };

    public static readonly AreaEffect[] AreaEffectArea2 =
    {
        AreaEffect.EssenceOfEarth, AreaEffect.ExplosiveGoo, AreaEffect.AuraOfEnergy,
        AreaEffect.AuraOfNausea, AreaEffect.EssenceOfDisease,
        AreaEffect.PoisonBreath
    };

    public static readonly AreaEffect[] AreaEffectArea3 =
    {
        AreaEffect.AuraOfNausea, AreaEffect.EssenceOfDisease, AreaEffect.PoisonBreath
    };

    public static readonly AreaEffect[] AreaEffectArea4 =
    {
        AreaEffect.AuraOfEnergy, AreaEffect.ExplosiveGoo, AreaEffect.AuraOfNausea,
        AreaEffect.PoisonBreath, AreaEffect.EssenceOfDisease
    };

    public static readonly AreaEffect[] AreaEffectArea5 =
    {
        AreaEffect.ExplosiveGoo, AreaEffect.AuraOfNausea, AreaEffect.PoisonBreath, AreaEffect.EssenceOfDisease
    };

    // -- Weapon Ability Defs (:250-357; ColdWind absent, see the header) ---------------------------------------------

    public static readonly WeaponAbility[] WeaponAbilities =
    {
        WeaponAbility.NerveStrike,
        WeaponAbility.WhirlwindAttack,
        WeaponAbility.LightningArrow,
        WeaponAbility.InfusedThrow,
        WeaponAbility.MysticArc,
        WeaponAbility.TalonStrike,
        WeaponAbility.PsychicAttack,
        WeaponAbility.ArmorIgnore,
        WeaponAbility.ArmorPierce,
        WeaponAbility.Bladeweave,
        WeaponAbility.BleedAttack,
        WeaponAbility.Block,
        WeaponAbility.ConcussionBlow,
        WeaponAbility.CrushingBlow,
        WeaponAbility.Disarm,
        WeaponAbility.Dismount,
        WeaponAbility.DoubleStrike,
        WeaponAbility.DualWield,
        WeaponAbility.Feint,
        WeaponAbility.ForceOfNature,
        WeaponAbility.FrenziedWhirlwind,
        WeaponAbility.MortalStrike,
        WeaponAbility.ParalyzingBlow
    };

    public static readonly WeaponAbility[] WepAbilityNone = Array.Empty<WeaponAbility>();

    public static readonly WeaponAbility[] WepAbility1 =
    {
        WeaponAbility.ArmorIgnore, WeaponAbility.ArmorPierce, WeaponAbility.Bladeweave, WeaponAbility.BleedAttack,
        WeaponAbility.ConcussionBlow, WeaponAbility.CrushingBlow, WeaponAbility.Dismount, WeaponAbility.Feint,
        WeaponAbility.ForceOfNature, WeaponAbility.FrenziedWhirlwind, WeaponAbility.MortalStrike, WeaponAbility.NerveStrike,
        WeaponAbility.ParalyzingBlow, WeaponAbility.PsychicAttack, WeaponAbility.TalonStrike
    };

    public static readonly WeaponAbility[] WepAbility2 =
    {
        WeaponAbility.ArmorIgnore, WeaponAbility.ArmorPierce, WeaponAbility.Bladeweave, WeaponAbility.BleedAttack,
        WeaponAbility.ConcussionBlow, WeaponAbility.CrushingBlow, WeaponAbility.Dismount, WeaponAbility.Feint,
        WeaponAbility.ForceOfNature, WeaponAbility.FrenziedWhirlwind, WeaponAbility.MortalStrike, WeaponAbility.NerveStrike,
        WeaponAbility.ParalyzingBlow, WeaponAbility.PsychicAttack, WeaponAbility.TalonStrike
    };

    public static readonly WeaponAbility[] WepAbility3 =
    {
        WeaponAbility.ArmorIgnore, WeaponAbility.ArmorPierce, WeaponAbility.Bladeweave, WeaponAbility.ConcussionBlow,
        WeaponAbility.CrushingBlow, WeaponAbility.Dismount, WeaponAbility.Feint, WeaponAbility.ForceOfNature,
        WeaponAbility.FrenziedWhirlwind, WeaponAbility.MortalStrike, WeaponAbility.NerveStrike, WeaponAbility.ParalyzingBlow,
        WeaponAbility.PsychicAttack, WeaponAbility.TalonStrike
    };

    public static readonly WeaponAbility[] WepAbility4 =
    {
        WeaponAbility.ArmorIgnore, WeaponAbility.ArmorPierce, WeaponAbility.Bladeweave, WeaponAbility.BleedAttack,
        WeaponAbility.ConcussionBlow, WeaponAbility.CrushingBlow, WeaponAbility.Dismount, WeaponAbility.Feint,
        WeaponAbility.ForceOfNature, WeaponAbility.MortalStrike, WeaponAbility.NerveStrike, WeaponAbility.ParalyzingBlow,
        WeaponAbility.PsychicAttack, WeaponAbility.TalonStrike
    };

    public static readonly WeaponAbility[] WepAbility5 =
    {
        WeaponAbility.ArmorIgnore, WeaponAbility.ArmorPierce, WeaponAbility.Bladeweave, WeaponAbility.ConcussionBlow,
        WeaponAbility.CrushingBlow, WeaponAbility.Dismount, WeaponAbility.Feint, WeaponAbility.ForceOfNature,
        WeaponAbility.FrenziedWhirlwind, WeaponAbility.MortalStrike, WeaponAbility.NerveStrike, WeaponAbility.ParalyzingBlow,
        WeaponAbility.PsychicAttack, WeaponAbility.TalonStrike
    };

    public static readonly WeaponAbility[] WepAbility6 =
    {
        WeaponAbility.ArmorIgnore, WeaponAbility.ArmorPierce, WeaponAbility.Bladeweave, WeaponAbility.BleedAttack,
        WeaponAbility.ConcussionBlow, WeaponAbility.CrushingBlow, WeaponAbility.Disarm, WeaponAbility.Feint,
        WeaponAbility.ForceOfNature, WeaponAbility.FrenziedWhirlwind, WeaponAbility.MortalStrike, WeaponAbility.NerveStrike,
        WeaponAbility.ParalyzingBlow, WeaponAbility.PsychicAttack, WeaponAbility.TalonStrike
    };

    public static readonly WeaponAbility[] WepAbility7 =
    {
        WeaponAbility.ArmorIgnore, WeaponAbility.ArmorPierce, WeaponAbility.Bladeweave, WeaponAbility.BleedAttack,
        WeaponAbility.ConcussionBlow, WeaponAbility.CrushingBlow, WeaponAbility.Feint, WeaponAbility.ForceOfNature,
        WeaponAbility.FrenziedWhirlwind, WeaponAbility.MortalStrike, WeaponAbility.NerveStrike, WeaponAbility.ParalyzingBlow,
        WeaponAbility.PsychicAttack, WeaponAbility.TalonStrike
    };

    public static readonly WeaponAbility[] WepAbility8 =
    {
        WeaponAbility.ArmorIgnore, WeaponAbility.ArmorPierce, WeaponAbility.Bladeweave, WeaponAbility.BleedAttack,
        WeaponAbility.ConcussionBlow, WeaponAbility.CrushingBlow, WeaponAbility.Feint, WeaponAbility.ForceOfNature,
        WeaponAbility.FrenziedWhirlwind, WeaponAbility.MortalStrike, WeaponAbility.NerveStrike, WeaponAbility.ParalyzingBlow,
        WeaponAbility.PsychicAttack, WeaponAbility.TalonStrike
    };

    public static readonly WeaponAbility[] WepAbility9 =
    {
        WeaponAbility.ArmorIgnore, WeaponAbility.ArmorPierce, WeaponAbility.Bladeweave, WeaponAbility.BleedAttack,
        WeaponAbility.Block, WeaponAbility.ConcussionBlow, WeaponAbility.CrushingBlow, WeaponAbility.Dismount,
        WeaponAbility.Feint, WeaponAbility.ForceOfNature, WeaponAbility.FrenziedWhirlwind, WeaponAbility.MortalStrike,
        WeaponAbility.NerveStrike, WeaponAbility.ParalyzingBlow, WeaponAbility.PsychicAttack, WeaponAbility.TalonStrike
    };

    public static readonly WeaponAbility[] WepAbility10 =
    {
        WeaponAbility.ArmorIgnore, WeaponAbility.ArmorPierce, WeaponAbility.Bladeweave, WeaponAbility.BleedAttack,
        WeaponAbility.ConcussionBlow, WeaponAbility.CrushingBlow, WeaponAbility.Dismount, WeaponAbility.Feint,
        WeaponAbility.ForceOfNature, WeaponAbility.FrenziedWhirlwind, WeaponAbility.MortalStrike, WeaponAbility.NerveStrike,
        WeaponAbility.PsychicAttack, WeaponAbility.TalonStrike
    };

    public static readonly WeaponAbility[] WepAbility11 =
    {
        WeaponAbility.ArmorIgnore, WeaponAbility.Bladeweave, WeaponAbility.BleedAttack, WeaponAbility.ConcussionBlow,
        WeaponAbility.CrushingBlow, WeaponAbility.Dismount, WeaponAbility.Feint, WeaponAbility.ForceOfNature,
        WeaponAbility.FrenziedWhirlwind, WeaponAbility.MortalStrike, WeaponAbility.NerveStrike, WeaponAbility.ParalyzingBlow,
        WeaponAbility.PsychicAttack, WeaponAbility.TalonStrike
    };

    public static readonly WeaponAbility[] WepAbility12 =
    {
        WeaponAbility.ArmorIgnore, WeaponAbility.ArmorPierce, WeaponAbility.Bladeweave, WeaponAbility.BleedAttack,
        WeaponAbility.ConcussionBlow, WeaponAbility.CrushingBlow, WeaponAbility.Dismount, WeaponAbility.Feint,
        WeaponAbility.ForceOfNature, WeaponAbility.FrenziedWhirlwind, WeaponAbility.MortalStrike, WeaponAbility.NerveStrike,
        WeaponAbility.ParalyzingBlow, WeaponAbility.PsychicAttack, WeaponAbility.TalonStrike
    };

    // -- Creature Training Defs (:655-783), by type name -------------------------------------------------------------

    private readonly record struct DefinitionRow(
        string TypeName, Class Class, MagicalAbility Magic, SpecialAbility[] Special, WeaponAbility[] Weapon,
        AreaEffect[] Area, int Min, int Max
    );

    private static readonly AreaEffect[] AreaEffectColdDrake =
    {
        AreaEffect.AuraOfEnergy, AreaEffect.ExplosiveGoo, AreaEffect.EssenceOfEarth
    };

    private static readonly SpecialAbility[] SpecialAbilityDragonBreathOnly = { SpecialAbility.DragonBreath };
    private static readonly SpecialAbility[] SpecialAbilityLifeLeechOnly = { SpecialAbility.LifeLeech };

    /// <summary>ServUO's Definitions array, entry for entry, in its order; the commented-out ServUO rows are omitted here too.</summary>
    public static readonly IReadOnlyList<string> DefinitionTypeNames;

    private static readonly DefinitionRow[] DefinitionRows =
    {
        new("Alligator", Class.ClawedAndTailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawedAndTailed, WepAbility1, AreaEffectNone, 1, 4),
        new("BakeKitsune", Class.ClawedTailedAndTokuno, MagicalAbility.Tokuno1, SpecialAbilityClawedTailedAndMagical2, WepAbility2, AreaEffectArea1, 3, 5),
        new("BaneDragon", Class.MagicalClawedAndTailed, MagicalAbility.Dragon1, SpecialAbilityBaneDragon, WepAbility2, AreaEffectArea2, 3, 5),
        new("BattleChickenLizard", Class.Untrainable, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 1, 1),
        new("Bird", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 2),
        new("BlackBear", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 3),
        new("BloodFox", Class.None, MagicalAbility.Poisoning, SpecialAbilityNone, WepAbility3, AreaEffectNone, 2, 3),
        new("Boar", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("BrownBear", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 3),
        new("Bull", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 4),
        new("BullFrog", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("Cat", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility4, AreaEffectNone, 1, 3),
        new("Chicken", Class.Clawed, MagicalAbility.None, SpecialAbilityClawed, WepAbilityNone, AreaEffectNone, 1, 2),
        new("ChickenLizard", Class.Untrainable, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 1, 1),
        new("LeatherWolf", Class.None, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 1, 1),
        new("ColdDrake", Class.None, MagicalAbility.ColdDrake, SpecialAbilityNone, WepAbility2, AreaEffectColdDrake, 3, 5),
        new("CorrosiveSlime", Class.StickySkin, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 1, 3),
        new("Cougar", Class.ClawedAndTailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawedAndTailed, WepAbility1, AreaEffectNone, 1, 3),
        new("Cow", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("CrimsonDrake", Class.None, MagicalAbility.Dragon2, SpecialAbilityNone, WepAbility2, AreaEffectArea1, 2, 5), // CrimsonDrake[Poison] = AreaEffectArea2
        new("CuSidhe", Class.MagicalClawedAndTailed, MagicalAbility.Cusidhe, SpecialAbilityClawedTailedAndMagical2, WepAbility5, AreaEffectArea1, 3, 5),
        new("DeathwatchBeetle", Class.Insectoid, MagicalAbility.Poisoning, SpecialAbilityMagicalInsectoid, WepAbility5, AreaEffectNone, 1, 4),
        new("DesertOstard", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 3),
        new("Dimetrosaur", Class.None, MagicalAbility.Poisoning, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 3, 5),
        new("DireWolf", Class.ClawedNecromanticAndTokuno, MagicalAbility.Wolf, SpecialAbilityClawedAndNecromantic, WepAbility1, AreaEffectNone, 1, 4),
        new("Dog", Class.Tailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityTailed, WepAbility1, AreaEffectNone, 1, 3),
        new("Dragon", Class.MagicalClawedAndTailed, MagicalAbility.Dragon2, SpecialAbilityNone, WepAbility2, AreaEffectArea1, 4, 5),
        new("DragonTurtleHatchling", Class.MagicalAndTailed, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 4, 5),
        new("DragonWolf", Class.None, MagicalAbility.DragonWolf, SpecialAbilityNone, WepAbility1, AreaEffectNone, 4, 5),
        new("Drake", Class.MagicalAndTailed, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 2, 5),
        new("DreadSpider", Class.MagicalNecromanticAndTokuno, MagicalAbility.DreadSpider, SpecialAbilityDreadSpider, WepAbility2, AreaEffectArea2, 3, 5),
        new("DreadWarhorse", Class.MagicalAndNecromantic, MagicalAbility.DreadWarhorse, SpecialAbilityDragonBreathOnly, WepAbility2, AreaEffectArea2, 3, 5),
        new("Eagle", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 3),
        new("Ferret", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 2),
        new("FireBeetle", Class.MagicalAndInsectoid, MagicalAbility.StandardClawedOrTailed, SpecialAbilityMagicalInsectoid, WepAbility1, AreaEffectArea5, 1, 5),
        new("FireSteed", Class.Magical, MagicalAbility.Dragon2, SpecialAbilityNone, WepAbility2, AreaEffectArea1, 2, 5),
        new("ForestOstard", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 3),
        new("FrenziedOstard", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 3),
        new("FrostDragon", Class.None, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 5, 5),
        new("FrostDrake", Class.None, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 3, 5),
        new("FrostMite", Class.Insectoid, MagicalAbility.Poisoning, SpecialAbilityMagicalInsectoid, WepAbility1, AreaEffectNone, 2, 5),
        new("FrostSpider", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility2, AreaEffectNone, 1, 4),
        new("Gallusaurus", Class.None, MagicalAbility.Poisoning, SpecialAbilityNone, WepAbility1, AreaEffectNone, 3, 5),
        new("Gaman", Class.Tailed, MagicalAbility.Poisoning, SpecialAbilityTailed, WepAbility1, AreaEffectNone, 1, 4),
        new("Beetle", Class.Insectoid, MagicalAbility.StandardClawedOrTailed, SpecialAbilityMagicalInsectoid, WepAbility1, AreaEffectNone, 1, 5),
        new("GiantIceWorm", Class.Tailed, MagicalAbility.Variety1, SpecialAbilityBitingTailed, WepAbility2, AreaEffectArea3, 1, 2),
        new("GiantRat", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 3),
        new("GiantSpider", Class.None, MagicalAbility.Variety1, SpecialAbilityBitingAnimal, WepAbility1, AreaEffectArea3, 1, 3),
        new("GiantToad", Class.StickySkin, MagicalAbility.StandardClawedOrTailed, SpecialAbilityStickySkin, WepAbility1, AreaEffectNone, 1, 3),
        new("Goat", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("Gorilla", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("GreatHart", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("GreaterDragon", Class.MagicalClawedAndTailed, MagicalAbility.GreaterDragon, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 4, 5),
        new("GreaterMongbat", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 3),
        new("GreyWolf", Class.ClawedAndTailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawedAndTailed, WepAbility1, AreaEffectNone, 1, 3),
        new("GrizzlyBear", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 3),
        new("HellHound", Class.ClawedTailedNecromanticAndTokuno, MagicalAbility.Wolf, SpecialAbilityNone, WepAbility1, AreaEffectExplosiveGoo, 2, 4),
        new("HellCat", Class.ClawedTailedAndNecromantic, MagicalAbility.Hellcat, SpecialAbilityNone, WepAbility1, AreaEffectExplosiveGoo, 2, 3),
        new("HighPlainsBoura", Class.Tailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityNone, WepAbility1, AreaEffectNone, 2, 5),
        new("Hind", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("Hiryu", Class.ClawedTailedMagicalAndTokuno, MagicalAbility.Hiryu, SpecialAbilityNone, WepAbility7, AreaEffectArea1, 3, 5),
        new("Horse", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("Imp", Class.MagicalClawedTailedAndNecromantic, MagicalAbility.Variety2, SpecialAbilityImp, WepAbility2, AreaEffectArea2, 2, 4),
        new("IronBeetle", Class.Insectoid, MagicalAbility.StandardClawedOrTailed, SpecialAbilityMagicalInsectoid, WepAbility1, AreaEffectNone, 2, 5),
        new("JackRabbit", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 2),
        new("Kirin", Class.MagicalClawedAndTailed, MagicalAbility.Dragon2, SpecialAbilityClawedTailedAndMagical2, WepAbility2, AreaEffectArea1, 2, 5),
        new("Lasher", Class.Magical, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 2, 4),
        new("LavaLizard", Class.ClawedTailedMagicalAndTokuno, MagicalAbility.LavaLizard, SpecialAbilityNone, WepAbility6, AreaEffectArea1, 1, 4),
        new("LesserHiryu", Class.ClawedTailedMagicalAndTokuno, MagicalAbility.Tokuno1, SpecialAbilityNone, WepAbility7, AreaEffectArea1, 1, 5),
        new("Lion", Class.ClawedAndTailed, MagicalAbility.Poisoning, SpecialAbilityBitingClawedAndTailed, WepAbility1, AreaEffectArea3, 2, 5),
        new("Llama", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility9, AreaEffectNone, 1, 3),
        new("LowlandBoura", Class.Tailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityTailed, WepAbility1, AreaEffectNone, 2, 3),
        new("Mongbat", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 2),
        new("MountainGoat", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("Najasaurus", Class.StickySkinAndTailed, MagicalAbility.Variety1, SpecialAbilityTailedAndStickySkin, WepAbility1, AreaEffectArea3, 2, 5),
        new("Nightmare", Class.MagicalAndNecromantic, MagicalAbility.Variety2, SpecialAbilityNone, WepAbility2, AreaEffectArea1, 3, 5),
        new("OsseinRam", Class.None, MagicalAbility.Variety3, SpecialAbilityLifeLeechOnly, WepAbility12, AreaEffectArea2, 2, 5),
        new("PackHorse", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("PackLlama", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("Palomino", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("Panther", Class.ClawedAndTailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawedAndTailed, WepAbility1, AreaEffectNone, 1, 3),
        new("Paralithode", Class.None, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 1, 1),
        new("ParoxysmusSwampDragon", Class.Untrainable, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 1, 1),
        new("Phoenix", Class.MagicalAndClawed, MagicalAbility.Dragon1, SpecialAbilityPhoenix, WepAbility1, AreaEffectArea2, 3, 5),
        new("Pig", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("PlatinumDrake", Class.None, MagicalAbility.Dragon2, SpecialAbilityNone, WepAbility2, AreaEffectArea1, 2, 5),
        new("PolarBear", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 3),
        new("PredatorHellCat", Class.ClawedTailedAndNecromantic, MagicalAbility.Hellcat, SpecialAbilityNone, WepAbility1, AreaEffectExplosiveGoo, 2, 3),
        new("Rabbit", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 2),
        new("Raptor", Class.ClawedAndTailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawedAndTailed, WepAbility1, AreaEffectNone, 2, 5),
        new("Rat", Class.ClawedAndTailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawedAndTailed, WepAbility1, AreaEffectNone, 1, 2),
        new("Reptalon", Class.MagicalAndTailed, MagicalAbility.Cusidhe, SpecialAbilityNone, WepAbility10, AreaEffectArea2, 2, 5),
        new("RidableLlama", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("Ridgeback", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 3),
        new("RuddyBoura", Class.Tailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityTailed, WepAbility1, AreaEffectNone, 2, 3),
        new("RuneBeetle", Class.Insectoid, MagicalAbility.RuneBeetle, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 3, 5),
        new("SabertoothedTiger", Class.ClawedAndTailed, MagicalAbility.SabreToothedTiger, SpecialAbilitySabreTri, WepAbility1, AreaEffectNone, 2, 5),
        new("Saurosaurus", Class.Tailed, MagicalAbility.Poisoning, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 3, 5),
        new("SavageRidgeback", Class.Clawed, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 1, 3),
        new("Scorpion", Class.Tailed, MagicalAbility.Variety1, SpecialAbilityBitingTailed, WepAbility1, AreaEffectArea3, 1, 3),
        new("SerpentineDragon", Class.MagicalClawedAndTailed, MagicalAbility.Dragon2, SpecialAbilityNone, WepAbility2, AreaEffectArea2, 3, 5),
        new("Sewerrat", Class.ClawedAndTailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawedAndTailed, WepAbility3, AreaEffectNone, 1, 2),
        new("ShadowWyrm", Class.TailedAndNecromantic, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 5, 5),
        new("Sheep", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 2),
        new("SilverSteed", Class.None, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 1, 2),
        new("SkitteringHopper", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 2),
        new("Skree", Class.MagicalClawedAndTailed, MagicalAbility.Dragon2, SpecialAbilityClawedTailedAndMagical1, WepAbility2, AreaEffectArea1, 3, 5),
        new("Slime", Class.StickySkin, MagicalAbility.Variety1, SpecialAbilityBitingStickySkin, WepAbility1, AreaEffectArea3, 1, 2),
        new("Slith", Class.ClawedAndTailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityNone, WepAbility1, AreaEffectNone, 1, 4),
        new("Snake", Class.Tailed, MagicalAbility.Variety1, SpecialAbilityBitingTailed, WepAbility1, AreaEffectArea3, 1, 2),
        new("SnowLeopard", Class.ClawedAndTailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawedAndTailed, WepAbility1, AreaEffectNone, 1, 3),
        new("Squirrel", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 2),
        new("StoneSlith", Class.ClawedAndTailed, MagicalAbility.Poisoning, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 1, 5),
        new("StygianDrake", Class.MagicalClawedAndTailed, MagicalAbility.StygianDrake, SpecialAbilityClawedTailedAndMagical1, WepAbility2, AreaEffectArea1, 4, 5),
        new("SwampDragon", Class.Untrainable, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 1, 1),
        new("TimberWolf", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 3),
        new("Triceratops", Class.Tailed, MagicalAbility.Triceratops, SpecialAbilitySabreTri, WepAbilityNone, AreaEffectNone, 3, 5),
        new("TropicalBird", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 2),
        new("TsukiWolf", Class.MagicalClawedTailedNecromanticAndTokuno, MagicalAbility.TsukiWolf, SpecialAbilityTsukiWolf, WepAbility2, AreaEffectArea1, 3, 5),
        new("Turkey", Class.Clawed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawed, WepAbility1, AreaEffectNone, 1, 2),
        new("Unicorn", Class.Magical, MagicalAbility.Dragon2, SpecialAbilityUnicorn, WepAbility11, AreaEffectArea1, 2, 5),
        new("Vollem", Class.MagicalAndTailed, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 3, 5),
        new("Walrus", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 3),
        new("WhiteWolf", Class.ClawedAndTailed, MagicalAbility.StandardClawedOrTailed, SpecialAbilityClawedAndTailed, WepAbility1, AreaEffectNone, 1, 3),
        new("WhiteWyrm", Class.MagicalClawedAndTailed, MagicalAbility.Dragon1, SpecialAbilityClawedTailedAndMagical2, WepAbility2, AreaEffectEarthen, 4, 5),
        new("WildTiger", Class.ClawedAndTailed, MagicalAbility.Poisoning, SpecialAbilityNone, WepAbility3, AreaEffectNone, 2, 5),
        new("WildWhiteTiger", Class.ClawedAndTailed, MagicalAbility.Poisoning, SpecialAbilityNone, WepAbility3, AreaEffectNone, 2, 5),
        new("WildBlackTiger", Class.ClawedAndTailed, MagicalAbility.Poisoning, SpecialAbilityNone, WepAbility3, AreaEffectNone, 2, 5),
        new("Windrunner", Class.TailedAndNecromantic, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 2, 4),
        new("WolfSpider", Class.None, MagicalAbility.Vartiety, SpecialAbilityBitingAnimal, WepAbility1, AreaEffectDisease, 1, 3),
        new("Triton", Class.None, MagicalAbility.Triton, SpecialAbilityTriton, WepAbility9, AreaEffectArea2, 2, 5),
        new("Eowmu", Class.Clawed, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 1, 3),
        new("TigerCub", Class.ClawedAndTailed, MagicalAbility.None, SpecialAbilityNone, WepAbilityNone, AreaEffectNone, 1, 3),
        new("GrizzledMare", Class.ClawedTailedAndNecromantic, MagicalAbility.GrizzledMare, SpecialAbilityGrizzledMare, WepAbility2, AreaEffectArea4, 1, 3),
        new("HungryCoconutCrab", Class.None, MagicalAbility.StandardClawedOrTailed, SpecialAbilityAnimalStandard, WepAbility1, AreaEffectNone, 1, 2),
        new("SkeletalCat", Class.ClawedTailedAndNecromantic, MagicalAbility.Hellcat, SpecialAbilitySkeletalCat, WepAbility4, AreaEffectArea3, 2, 3),
        new("CoconutCrab", Class.None, MagicalAbility.CoconutCrab, SpecialAbilityCoconutCrab, WepAbility2, AreaEffectArea2, 1, 3)
    };

    static PetTrainingHelper()
    {
        DefinitionTypeNames = DefinitionRows.Select(r => r.TypeName).ToArray();
    }

    private static Dictionary<Type, TrainingDefinition> _definitions;
    private static List<string> _missingDefinitionTypes;

    /// <summary>The table, keyed by the resolved creature type. Built on first use; a row whose type this shard lacks is skipped.</summary>
    public static IReadOnlyDictionary<Type, TrainingDefinition> Definitions
    {
        get
        {
            EnsureDefinitions();
            return _definitions;
        }
    }

    /// <summary>The ServUO rows whose creature type does not exist on this shard (for the notes and the tests).</summary>
    public static IReadOnlyList<string> MissingDefinitionTypes
    {
        get
        {
            EnsureDefinitions();
            return _missingDefinitionTypes;
        }
    }

    private static void EnsureDefinitions()
    {
        if (_definitions != null)
        {
            return;
        }

        var defs = new Dictionary<Type, TrainingDefinition>();
        var missing = new List<string>();

        foreach (var row in DefinitionRows)
        {
            var type = AssemblyHandler.FindTypeByName($"Server.Mobiles.{row.TypeName}", true);

            if (type == null || !type.IsSubclassOf(typeof(BaseCreature)))
            {
                missing.Add(row.TypeName);
                continue;
            }

            defs[type] = new TrainingDefinition(type, row.Class, row.Magic, row.Special, row.Weapon, row.Area, row.Min, row.Max);
        }

        _missingDefinitionTypes = missing;
        _definitions = defs;
    }

    // -- Training Points Configuration (ServUO Configure, :786-920) ----------------------------------------------------

    private static List<TrainingPoint> _trainingPoints;

    public static List<TrainingPoint> TrainingPoints => _trainingPoints ??= BuildTrainingPoints();

    private static List<TrainingPoint> BuildTrainingPoints()
    {
        var points = new List<TrainingPoint>
        {
            new(PetStat.Str, 3.0, 1, 700, 1061146, 1157507),
            new(PetStat.Dex, 0.1, 1, 150, 1061147, 1157508),
            new(PetStat.Int, 0.5, 1, 700, 1061148, 1157509),

            new(PetStat.Hits, 3.0, 1, 1100, 1061149, 1157510),
            new(PetStat.Stam, 0.5, 1, 150, 1061150, 1157511),
            new(PetStat.Mana, 0.5, 1, 1500, 1061151, 1157512),

            new(PetStat.RegenHits, 18.0, 1, 20, 1075627, 1157513),
            new(PetStat.RegenStam, 12.0, 1, 30, 1079411, 1157514),
            new(PetStat.RegenMana, 12.0, 1, 30, 1079410, 1157515),

            new(PetStat.BaseDamage, 5.0, 1, 22, 1157506, 1157516),

            new(ResistanceType.Physical, 3.0, 1, 80, 1061158, 1157517),
            new(ResistanceType.Fire, 3.0, 1, 80, 1061159, 1157518),
            new(ResistanceType.Cold, 3.0, 1, 80, 1061160, 1157519),
            new(ResistanceType.Poison, 3.0, 1, 80, 1061161, 1157520),
            new(ResistanceType.Energy, 3.0, 1, 80, 1061162, 1157521),

            new(SkillName.Magery, 0.5, 50, 200, 1002106, 1157522),
            new(SkillName.EvalInt, 1.0, 50, 200, 1044076, 1157522),
            new(SkillName.Necromancy, 0.5, 50, 200, 1044109, 1157522),
            new(SkillName.SpiritSpeak, 1.0, 50, 200, 1044092, 1157522),
            new(SkillName.Chivalry, 0.5, 50, 200, 1044111, 1157522),
            new(SkillName.Focus, 0.1, 50, 200, 1044110, 1157522),
            new(SkillName.Bushido, 0.5, 50, 200, 1044112, 1157522),
            new(SkillName.Ninjitsu, 0.5, 50, 200, 1044113, 1157522),
            new(SkillName.Spellweaving, 0.5, 50, 200, 1044114, 1157522),
            new(SkillName.Mysticism, 0.5, 50, 200, 1044115, 1157522),
            new(SkillName.Meditation, 0.1, 50, 200, 1044106, 1157522),
            new(SkillName.MagicResist, 0.1, 50, 200, 1044086, 1157522),

            new(SkillName.Wrestling, 1.0, 50, 200, 1044103, 1157522),
            new(SkillName.Tactics, 1.0, 50, 200, 1044087, 1157522),
            new(SkillName.Anatomy, 0.1, 50, 200, 1044061, 1157522),
            new(SkillName.Parry, 0.1, 50, 200, 1002118, 1157522),
            new(SkillName.Healing, 0.1, 50, 200, 1002082, 1157522),
            new(SkillName.Discordance, 0.1, 50, 200, 1044075, 1157522)
        };

        var loc = _magicalAbilityLocalizations;

        points.Add(new TrainingPoint(MagicalAbility.Piercing, 1.0, 1, 1, loc[0][0], loc[0][1],
            new TrainingPointRequirement(WeaponAbility.ArmorIgnore, 100, 1028838),
            new TrainingPointRequirement(WeaponAbility.ParalyzingBlow, 100, 1028848),
            new TrainingPointRequirement(WeaponAbility.BleedAttack, 100, 1028839)));

        points.Add(new TrainingPoint(MagicalAbility.Bashing, 1.0, 1, 1, loc[1][0], loc[1][1],
            new TrainingPointRequirement(WeaponAbility.MortalStrike, 100, 1028846),
            new TrainingPointRequirement(WeaponAbility.ConcussionBlow, 100, 1028840),
            new TrainingPointRequirement(WeaponAbility.Disarm, 100, 1028842)));

        points.Add(new TrainingPoint(MagicalAbility.Slashing, 1.0, 1, 1, loc[2][0], loc[2][1],
            new TrainingPointRequirement(SkillName.Bushido, 500, 1044112),
            new TrainingPointRequirement(WeaponAbility.ArmorIgnore, 100, 1028838),
            new TrainingPointRequirement(WeaponAbility.Disarm, 100, 1028842),
            new TrainingPointRequirement(WeaponAbility.NerveStrike, 100, 1028855)));

        points.Add(new TrainingPoint(MagicalAbility.BattleDefense, 1.0, 1, 1, loc[3][0], loc[3][1],
            new TrainingPointRequirement(WeaponAbility.Disarm, 100, 1028842),
            new TrainingPointRequirement(WeaponAbility.ParalyzingBlow, 100, 1028848)));

        points.Add(new TrainingPoint(MagicalAbility.WrestlingMastery, 1.0, 1, 1, loc[4][0], loc[4][1],
            new TrainingPointRequirement(WeaponAbility.Disarm, 100, 1028842),
            new TrainingPointRequirement(WeaponAbility.ParalyzingBlow, 100, 1028848)));

        points.Add(new TrainingPoint(MagicalAbility.Poisoning, 1.0, 1, 1, loc[5][0], loc[5][1],
            new TrainingPointRequirement(SkillName.Poisoning, 100, 1044090)));

        points.Add(new TrainingPoint(MagicalAbility.Bushido, 1.0, 1, 1, loc[6][0], loc[6][1],
            new TrainingPointRequirement(SkillName.Bushido, 500, 1044112)));

        points.Add(new TrainingPoint(MagicalAbility.Ninjitsu, 1.0, 1, 1, loc[7][0], loc[7][1],
            new TrainingPointRequirement(SkillName.Ninjitsu, 500, 1044113),
            new TrainingPointRequirement(SkillName.Hiding, 100, 1002088)));

        points.Add(new TrainingPoint(MagicalAbility.Discordance, 1.0, 1, 1, loc[8][0], loc[8][1],
            new TrainingPointRequirement(SkillName.Discordance, 500, 1044075)));

        points.Add(new TrainingPoint(MagicalAbility.MageryMastery, 1.0, 1, 1, loc[9][0], loc[9][1],
            new TrainingPointRequirement(SkillName.Magery, 100, 1044085),
            new TrainingPointRequirement(SkillName.EvalInt, 1000, 1044076)));

        points.Add(new TrainingPoint(MagicalAbility.Mysticism, 1.0, 1, 1, loc[10][0], loc[10][1],
            new TrainingPointRequirement(SkillName.Mysticism, 500, 1044115)));

        points.Add(new TrainingPoint(MagicalAbility.Spellweaving, 1.0, 1, 1, loc[11][0], loc[11][1],
            new TrainingPointRequirement(SkillName.Spellweaving, 500, 1044114)));

        points.Add(new TrainingPoint(MagicalAbility.Chivalry, 1.0, 1, 1, loc[12][0], loc[12][1],
            new TrainingPointRequirement(SkillName.Chivalry, 500, 1044111)));

        points.Add(new TrainingPoint(MagicalAbility.Necromage, 1.0, 1, 1, loc[13][0], loc[13][1],
            new TrainingPointRequirement(SkillName.Magery, 100, 1044085),
            new TrainingPointRequirement(SkillName.Necromancy, 100, 1044109),
            new TrainingPointRequirement(SkillName.SpiritSpeak, 100, 1044092),
            new TrainingPointRequirement(SkillName.EvalInt, 100, 1044076)));

        points.Add(new TrainingPoint(MagicalAbility.Necromancy, 1.0, 1, 1, loc[14][0], loc[14][1],
            new TrainingPointRequirement(SkillName.Necromancy, 100, 1044109),
            new TrainingPointRequirement(SkillName.SpiritSpeak, 100, 1044092)));

        loc = _specialAbilityLocalizations;

        for (var i = 0; i < SpecialAbility.Abilities.Length; i++)
        {
            if (i >= loc.Length)
            {
                break;
            }

            points.Add(new TrainingPoint(SpecialAbility.Abilities[i], 1.0, 100, 100, loc[i][0], loc[i][1]));
        }

        loc = _areaEffectLocalizations;

        for (var i = 0; i < AreaEffect.Effects.Length; i++)
        {
            if (i >= loc.Length)
            {
                break;
            }

            points.Add(new TrainingPoint(AreaEffect.Effects[i], 1.0, 100, 100, loc[i][0], loc[i][1]));
        }

        loc = _weaponAbilityLocalizations;

        for (var i = 0; i < WeaponAbilities.Length; i++)
        {
            var ability = WeaponAbilities[i];
            TrainingPointRequirement requirement = null;

            if (ability == WeaponAbility.NerveStrike)
            {
                requirement = new TrainingPointRequirement(SkillName.Bushido, 500, 1044112);
            }
            else if (ability == WeaponAbility.TalonStrike)
            {
                requirement = new TrainingPointRequirement(SkillName.Ninjitsu, 500, 1044113);
            }
            else if (ability == WeaponAbility.Feint)
            {
                requirement = new TrainingPointRequirement(SkillName.Bushido, 500, 1044112);
            }
            else if (ability == WeaponAbility.FrenziedWhirlwind)
            {
                requirement = new TrainingPointRequirement(SkillName.Ninjitsu, 500, 1044113);
            }
            else if (ability == WeaponAbility.Bladeweave)
            {
                requirement = new TrainingPointRequirement(SkillName.Bushido, 500, 1044112);
            }
            else if (ability == WeaponAbility.Block)
            {
                requirement = new TrainingPointRequirement(SkillName.Bushido, 500, 1044112);
            }

            points.Add(new TrainingPoint(ability, 1.0, 100, 100, loc[i][0], loc[i][1], requirement));
        }

        return points;
    }

    // -- Training Helpers (:924-1313) ----------------------------------------------------------------------------------

    public static bool CanControl(Mobile m, BaseCreature bc, TrainingProfile trainProfile)
    {
        var projected = Math.Min(
            BaseCreature.MaxTameRequirement,
            bc.CurrentTameSkill + trainProfile.GetRequirementIncrease(!trainProfile.HasIncreasedControlSlot)
        );

        return m.Skills[SkillName.AnimalTaming].Value >= projected;
    }

    public static int GetTrainingCapTotal(PetStat stat)
    {
        switch (stat)
        {
            case PetStat.Str:
            case PetStat.Int:
            case PetStat.Dex: return 2300;
            case PetStat.Hits:
            case PetStat.Stam:
            case PetStat.Mana: return 3300;
        }

        return 0;
    }

    public static int GetTrainingCapTotal(ResistanceType resist) => 1095;

    public static int GetTotalStatWeight(BaseCreature bc)
    {
        var str = GetTrainingPoint(PetStat.Str);
        var dex = GetTrainingPoint(PetStat.Dex);
        var intel = GetTrainingPoint(PetStat.Int);

        return (int)(Math.Min(bc.RawStr * str.Weight, str.GetMax(bc) * str.Weight) +
                     Math.Min(bc.RawDex * dex.Weight, dex.GetMax(bc) * dex.Weight) +
                     Math.Min(bc.RawInt * intel.Weight, intel.GetMax(bc) * intel.Weight));
    }

    public static int GetTotalAttributeWeight(BaseCreature bc)
    {
        var hits = GetTrainingPoint(PetStat.Hits);
        var stam = GetTrainingPoint(PetStat.Stam);
        var mana = GetTrainingPoint(PetStat.Mana);

        return (int)(Math.Min(bc.HitsMax * hits.Weight, hits.GetMax(bc) * hits.Weight) +
                     Math.Min(bc.StamMax * stam.Weight, stam.GetMax(bc) * stam.Weight) +
                     Math.Min(bc.ManaMax * mana.Weight, mana.GetMax(bc) * mana.Weight));
    }

    public static int GetTotalResistWeight(BaseCreature bc)
    {
        var phys = GetTrainingPoint(ResistanceType.Physical);
        var fire = GetTrainingPoint(ResistanceType.Fire);
        var cold = GetTrainingPoint(ResistanceType.Cold);
        var pois = GetTrainingPoint(ResistanceType.Poison);
        var nrgy = GetTrainingPoint(ResistanceType.Energy);

        return (int)(bc.PhysicalResistanceSeed * phys.Weight +
                     bc.FireResistSeed * fire.Weight +
                     bc.ColdResistSeed * cold.Weight +
                     bc.PoisonResistSeed * pois.Weight +
                     bc.EnergyResistSeed * nrgy.Weight);
    }

    public static int GetMaxDamagePerSecond(BaseCreature bc)
    {
        var slots = bc.ControlSlots;

        var profile = GetTrainingProfile(bc);

        if (profile != null && !profile.HasIncreasedControlSlot)
        {
            slots++;
        }

        return slots switch
        {
            2 => 8,
            3 => 13,
            4 => 17,
            _ => 22
        };
    }

    public static bool SetDamage(BaseCreature bc, int value)
    {
        if (value >= 0 && value < _damageTable.Length)
        {
            bc.SetDamage(_damageTable[value][0], _damageTable[value][1]);

            GetAbilityProfile(bc, true).DamageIndex = value;

            return true;
        }

        return false;
    }

    private static readonly int[][] _damageTable =
    {
        // slot 1 => 2
        new[] { 0, 1 },
        new[] { 2, 3 },
        new[] { 3, 4 },
        new[] { 4, 6 },
        new[] { 5, 7 },
        new[] { 6, 9 },
        new[] { 7, 10 },
        new[] { 9, 12 },
        // slot 2 => 3
        new[] { 9, 13 },
        new[] { 11, 15 },
        new[] { 12, 16 },
        new[] { 13, 18 },
        new[] { 14, 19 },
        // slot 3 => 4
        new[] { 15, 21 },
        new[] { 16, 22 },
        new[] { 18, 24 },
        new[] { 18, 25 },
        // slot 4 => 5
        new[] { 19, 26 },
        new[] { 20, 28 },
        new[] { 21, 29 },
        new[] { 23, 31 },
        new[] { 24, 33 }
    };

    public static void GetStartValue(TrainingPoint tp, BaseCreature bc, ref int value)
    {
        if (tp.Start == tp.Max)
        {
            return;
        }

        if (tp.TrainPoint is PetStat stat)
        {
            var profile = GetAbilityProfile(bc, true);

            switch (stat)
            {
                case PetStat.Str: value = Math.Max(bc.RawStr, value); break;
                case PetStat.Dex: value = Math.Max(bc.RawDex, value); break;
                case PetStat.Int: value = Math.Max(bc.RawInt, value); break;
                case PetStat.Hits: value = Math.Max(bc.HitsMax, value); break;
                case PetStat.Stam: value = Math.Max(bc.StamMax, value); break;
                case PetStat.Mana: value = Math.Max(bc.ManaMax, value); break;
                case PetStat.RegenHits: value = Math.Max(profile.RegenHits, value); break;
                case PetStat.RegenStam: value = Math.Max(profile.RegenStam, value); break;
                case PetStat.RegenMana: value = Math.Max(profile.RegenMana, value); break;
                case PetStat.BaseDamage:
                    {
                        if (profile.DamageIndex > -1)
                        {
                            value = profile.DamageIndex + 1;
                            break;
                        }

                        if (bc.DamageMin <= 1)
                        {
                            value = 1;
                            break;
                        }

                        for (var i = 0; i < _damageTable.Length; i++)
                        {
                            var list = _damageTable[i];

                            if (list[0] >= bc.DamageMin && bc.DamageMin <= list[1])
                            {
                                value = i + 1;
                                return;
                            }
                        }

                        value = _damageTable.Length - 1;
                    }
                    break;
            }
        }
        else if (tp.TrainPoint is ResistanceType resist)
        {
            switch (resist)
            {
                case ResistanceType.Physical: value = Math.Max(bc.PhysicalResistanceSeed, value); break;
                case ResistanceType.Fire: value = Math.Max(bc.FireResistSeed, value); break;
                case ResistanceType.Cold: value = Math.Max(bc.ColdResistSeed, value); break;
                case ResistanceType.Poison: value = Math.Max(bc.PoisonResistSeed, value); break;
                case ResistanceType.Energy: value = Math.Max(bc.EnergyResistSeed, value); break;
            }
        }
        else if (tp.TrainPoint is SkillName skillName)
        {
            var skill = bc.Skills[skillName];
            var cap = (int)skill.Cap;

            if (cap < 105)
            {
                value = 0;
            }
            else if (cap < 110)
            {
                value = 50;
            }
            else if (cap < 115)
            {
                value = 100;
            }
            else if (cap < 120)
            {
                value = 150;
            }
        }
    }

    public static int GetTotalCost(TrainingPoint tp, BaseCreature bc, int value, int startValue)
    {
        var cost = (int)(value * tp.Weight);

        if (tp.Requirements is { Length: > 0 })
        {
            foreach (var req in tp.Requirements.Where(r => r != null))
            {
                if (req.Requirement is SkillName skill && bc.Skills[skill].Base > 0)
                {
                    continue;
                }

                cost += req.Cost;
            }
        }

        if (startValue > 0)
        {
            cost -= (int)(startValue * tp.Weight);
        }

        return cost;
    }

    public static bool ApplyTrainingPoint(BaseCreature bc, TrainingPoint trainingPoint, int value)
    {
        var profile = GetAbilityProfile(bc, true);

        switch (trainingPoint.TrainPoint)
        {
            case PetStat stat:
                {
                    switch (stat)
                    {
                        case PetStat.Str:
                            if (bc.HitsMaxSeed == -1)
                            {
                                bc.HitsMaxSeed = bc.HitsMax;
                            }

                            bc.SetStr(value);
                            break;
                        case PetStat.Dex:
                            if (bc.StamMaxSeed == -1)
                            {
                                bc.StamMaxSeed = bc.StamMax;
                            }

                            bc.SetDex(value);
                            break;
                        case PetStat.Int:
                            if (bc.ManaMaxSeed == -1)
                            {
                                bc.ManaMaxSeed = bc.ManaMax;
                            }

                            bc.SetInt(value);
                            break;
                        case PetStat.Hits: bc.SetHits(value); break;
                        case PetStat.Stam: bc.SetStam(value); break;
                        case PetStat.Mana: bc.SetMana(value); break;
                        case PetStat.RegenHits: profile.RegenHits = value; break;
                        case PetStat.RegenStam: profile.RegenStam = value; break;
                        case PetStat.RegenMana: profile.RegenMana = value; break;
                        case PetStat.BaseDamage: return SetDamage(bc, value - 1);
                    }

                    return true;
                }
            case MagicalAbility magical:
                {
                    if (ValidateTrainingPoint(bc, magical) && profile.AddAbility(magical))
                    {
                        return true;
                    }

                    break;
                }
            case SpecialAbility special:
                {
                    if (ValidateTrainingPoint(bc, special) && profile.AddAbility(special))
                    {
                        return true;
                    }

                    break;
                }
            case AreaEffect area:
                {
                    if (ValidateTrainingPoint(bc, area) && profile.AddAbility(area))
                    {
                        return true;
                    }

                    break;
                }
            case WeaponAbility weapon:
                {
                    if (ValidateTrainingPoint(bc, weapon) && profile.AddAbility(weapon))
                    {
                        return true;
                    }

                    break;
                }
            case SkillName skill:
                {
                    if (ValidateTrainingPoint(bc, skill) && profile.AddAbility(skill))
                    {
                        bc.Skills[skill].Cap = 100 + value / 10;

                        return true;
                    }

                    break;
                }
            case ResistanceType resist:
                {
                    switch (resist)
                    {
                        case ResistanceType.Physical: bc.PhysicalResistanceSeed = value; break;
                        case ResistanceType.Fire: bc.FireResistSeed = value; break;
                        case ResistanceType.Cold: bc.ColdResistSeed = value; break;
                        case ResistanceType.Poison: bc.PoisonResistSeed = value; break;
                        case ResistanceType.Energy: bc.EnergyResistSeed = value; break;
                    }

                    return true;
                }
        }

        return false;
    }

    public static bool ValidateTrainingPoint(BaseCreature bc, MagicalAbility ability)
    {
        var def = GetTrainingDefinition(bc);

        return def != null && (def.MagicalAbilities & ability) != 0;
    }

    public static bool ValidateTrainingPoint(BaseCreature bc, SpecialAbility ability)
    {
        var def = GetTrainingDefinition(bc);

        if (def == null)
        {
            return false;
        }

        if (def.SpecialAbilities.Any(a => a == ability))
        {
            return true;
        }

        var profile = GetAbilityProfile(bc);

        return profile != null && (ability == SpecialAbility.ViciousBite || ability == SpecialAbility.VenomousBite) &&
               profile.HasAbility(MagicalAbility.Poisoning);
    }

    public static bool ValidateTrainingPoint(BaseCreature bc, AreaEffect ability)
    {
        var def = GetTrainingDefinition(bc);

        if (def == null)
        {
            return false;
        }

        if (def.AreaEffects.Any(a => a == ability))
        {
            return true;
        }

        var profile = GetAbilityProfile(bc);

        return profile != null && ability == AreaEffect.PoisonBreath && profile.HasAbility(MagicalAbility.Poisoning);
    }

    public static bool ValidateTrainingPoint(BaseCreature bc, WeaponAbility ability)
    {
        var def = GetTrainingDefinition(bc);

        return def != null && def.WeaponAbilities.Any(a => a == ability);
    }

    public static bool ValidateTrainingPoint(BaseCreature bc, SkillName skill) => bc.Skills[skill].Base > 0;

    // ServUO's Bladeweave reads this (Abilities/Bladeweave.cs:50); pinned's weapon abilities are not patched for it
    // (notes, "Deviations"), so it is here for the port's completeness and for a later patch.
    public static bool CheckSecondarySkill(BaseCreature bc, SkillName skill)
    {
        if (Enabled && bc.Controlled)
        {
            var profile = GetAbilityProfile(bc);

            return profile is { TokunoTame: true };
        }

        return true;
    }

    // ServUO's Block, Feint, FrenziedWhirlwind, NerveStrike and TalonStrike call this on a pet's use; same note as above.
    public static void OnWeaponAbilityUsed(BaseCreature bc, SkillName skill)
    {
        if (Enabled && bc.Controlled)
        {
            var profile = GetAbilityProfile(bc);

            if (profile != null)
            {
                bc.CheckSkill(skill, 0.0, bc.Skills[skill].Cap);
            }
        }
    }

    // -- Skill Categories (:1316-1354) -----------------------------------------------------------------------------------

    public static readonly SkillName[] MagicSkills =
    {
        SkillName.Magery,
        SkillName.EvalInt,
        SkillName.Necromancy,
        SkillName.SpiritSpeak,
        SkillName.Chivalry,
        SkillName.Focus,
        SkillName.Bushido,
        SkillName.Ninjitsu,
        SkillName.Spellweaving,
        SkillName.Mysticism,
        SkillName.Meditation,
        SkillName.MagicResist,
        SkillName.Discordance
    };

    public static readonly SkillName[] CombatSkills =
    {
        SkillName.Wrestling,
        SkillName.Tactics,
        SkillName.Anatomy,
        SkillName.Parry,
        SkillName.Healing
    };

    // ServUO also counts AI_NecroMage, AI_Spellweaving and AI_Mystic as casters for Meditation; pinned has only AI_Mage.
    public static bool CommonSkill(BaseCreature bc, SkillName skill)
    {
        if (bc.Skills[SkillName.Wrestling].Base >= 100 && skill == SkillName.Parry)
        {
            return true;
        }

        if (skill == SkillName.Meditation && bc.AI == AIType.AI_Mage)
        {
            return true;
        }

        return skill == SkillName.Focus || skill == SkillName.Tactics || skill == SkillName.Anatomy ||
               skill == SkillName.MagicResist || skill == SkillName.Wrestling;
    }

    // -- Localizations (:1357-1667) ------------------------------------------------------------------------------------
    // Indexed order: do not reorder or remove. An entry with a cliloc or string shows the ability in the animal lore
    // gump; null keeps it out.

    public static TextDefinition[][] MagicalAbilityLocalizations => _magicalAbilityLocalizations;

    private static readonly TextDefinition[][] _magicalAbilityLocalizations =
    {
        new TextDefinition[] { 1157559, 1157392 }, // piercing
        new TextDefinition[] { 1157560, 1157471 }, // bashing
        new TextDefinition[] { 1157561, 1157396 }, // slashing
        new TextDefinition[] { 1157562, 1157395 }, // battle defense
        new TextDefinition[] { 1155784, 1157397 }, // wrestling mastery
        new TextDefinition[] { 1002122, 1157389 }, // poisoning
        new TextDefinition[] { 1044112, 1157383 }, // bushido
        new TextDefinition[] { 1044113, 1157388 }, // ninjitsu
        new TextDefinition[] { 1044075, 1157385 }, // discordance
        new TextDefinition[] { 1155771, 1157472 }, // magery mastery
        new TextDefinition[] { 1044115, 1157386 }, // mysticism
        new TextDefinition[] { 1044114, 1157390 }, // spellweaving
        new TextDefinition[] { 1044111, 1157384 }, // chivalry - must have pos karma
        new TextDefinition[] { 1157473, 1157474 }, // necromage
        new TextDefinition[] { 1044109, 1157387 }, // necromancy - must have neg karma
        new TextDefinition[] { 1002106, 1157391 }  // magery
    };

    public static TextDefinition[][] SpecialAbilityLocalizations => _specialAbilityLocalizations;

    private static readonly TextDefinition[][] _specialAbilityLocalizations =
    {
        new TextDefinition[] { 1157412, 1157413 }, // Angry Fire
        new TextDefinition[] { 1157406, 1157407 }, // Conductive Blast
        new TextDefinition[] { 1157414, 1157415 }, // Dragon Breath
        new TextDefinition[] { 1157400, 1157401 }, // Grasping Claw
        new TextDefinition[] { 1157416, 1157417 }, // Inferno
        new TextDefinition[] { 1157408, 1157409 }, // Lightning Force
        new TextDefinition[] { 1157432, 1157433 }, // Mana Drain
        new TextDefinition[] { 1157404, 1157405 }, // Raging Breath
        new TextDefinition[] { 1157434, 1157435 }, // Repel
        new TextDefinition[] { 1157422, 1157423 }, // Searing Wounds
        new TextDefinition[] { 1157410, 1157411 }, // Steal Life
        new TextDefinition[] { 1157430, 1157431 }, // Venomous Bite
        new TextDefinition[] { 1157420, 1157421 }, // Vicious Bite
        new TextDefinition[] { 1157398, 1157399 }, // Rune Corruption
        new TextDefinition[] { 1157424, 1157425 }, // Life Leech
        new TextDefinition[] { 1157426, 1157427 }, // Sticky Skin
        new TextDefinition[] { 1157428, 1157429 }, // Tail Swipe
        new TextDefinition[] { 1157418, 1157419 }, // Flurry Force
        new TextDefinition[] { 1150005, 0 },       // Rage
        new TextDefinition[] { 1151311, 0 },       // Heal
        new TextDefinition[] { 1153793, 0 },       // Howl of Cacophony
        new TextDefinition[] { 1153789, 0 },       // Webbing
        new TextDefinition[] { 1153797, 0 },       // Anemia
        new TextDefinition[] { 1153798, 0 }        // Blood Disease
    };

    public static TextDefinition[][] AreaEffectLocalizations => _areaEffectLocalizations;

    private static readonly TextDefinition[][] _areaEffectLocalizations =
    {
        new TextDefinition[] { 1157459, 1157460 }, // Aura of Energy
        new TextDefinition[] { 1157467, 1157468 }, // Aura of Nausea
        new TextDefinition[] { 1157469, 1157470 }, // Essence of Disease
        new TextDefinition[] { 1157465, 1157466 }, // Essence of Earth
        new TextDefinition[] { 1157463, 1157464 }, // Explosive Goo
        new TextDefinition[] { null, null },       // Aura Damage
        new TextDefinition[] { 1157475, 1157476 }  // Poison Breath
    };

    public static TextDefinition[][] WeaponAbilityLocalizations => _weaponAbilityLocalizations;

    private static readonly TextDefinition[][] _weaponAbilityLocalizations =
    {
        new TextDefinition[] { 1028855, 1157436 }, // Nerve Strike
        new TextDefinition[] { 1028850, 1157437 }, // Whirlwind Attack
        new TextDefinition[] { 1028863, 1157438 }, // Lightning Arrow
        new TextDefinition[] { 1113283, 1157439 }, // Infused Throw
        new TextDefinition[] { 1113282, 1157440 }, // Mystic Arc
        new TextDefinition[] { 1028856, 1157441 }, // Talon Strike
        new TextDefinition[] { 1028864, 1157442 }, // Psychic Attack
        new TextDefinition[] { 1028838, 1157443 }, // Armor Ignore
        new TextDefinition[] { 1028860, 1157444 }, // Armor Pierce
        new TextDefinition[] { 1028861, 1157445 }, // Bladeweave
        new TextDefinition[] { 1028839, 1157446 }, // Bleed Attack
        new TextDefinition[] { 1028853, 1157447 }, // Block
        new TextDefinition[] { 1028840, 1157448 }, // Concussion Blow
        new TextDefinition[] { 1028841, 1157449 }, // Crushing Blow
        new TextDefinition[] { 1028842, 1157450 }, // Disarm
        new TextDefinition[] { 1028843, 1157451 }, // Dismount
        new TextDefinition[] { 1028844, 1157452 }, // Double Strike
        new TextDefinition[] { 1028858, 1157453 }, // Dual Wield
        new TextDefinition[] { 1028857, 1157454 }, // Feint
        new TextDefinition[] { 1028866, 1157455 }, // Force of Nature
        new TextDefinition[] { 1028852, 1157456 }, // Frenzied Whirlwind
        new TextDefinition[] { 1028846, 1157457 }, // Mortal Strike
        new TextDefinition[] { 1028848, 1157458 }  // Paralyzing Blow
    };

    // ServUO: pet.Skills[sk].Info.Localization. Pinned's SkillInfo has no cliloc; the skill-name clilocs run 1044060 +
    // skill (Alchemy 1044060 .. Throwing 1044117), which is the number ServUO's table holds.
    public static int GetLocalization(BaseCreature pet, SkillName sk) => 1044060 + (int)sk;

    public static TextDefinition[] GetLocalization(object o)
    {
        var tp = GetTrainingPoint(o);

        if (tp != null)
        {
            return new[] { tp.Name, tp.Description };
        }

        return o switch
        {
            MagicalAbility magical => GetLocalization(magical),
            SpecialAbility special => GetLocalization(special),
            AreaEffect area => GetLocalization(area),
            WeaponAbility weapon => GetLocalization(weapon),
            SkillName skill => GetLocalization(skill),
            _ => new TextDefinition[] { 0, 0 }
        };
    }

    public static TextDefinition[] GetLocalization(MagicalAbility ability)
    {
        foreach (var abil in Enum.GetValues<MagicalAbility>())
        {
            if ((ability & abil) != 0)
            {
                return GetMagicialAbilityLocalization(abil);
            }
        }

        return null;
    }

    public static TextDefinition[] GetMagicialAbilityLocalization(MagicalAbility ability) =>
        ability switch
        {
            MagicalAbility.Piercing => _magicalAbilityLocalizations[0],
            MagicalAbility.Bashing => _magicalAbilityLocalizations[1],
            MagicalAbility.Slashing => _magicalAbilityLocalizations[2],
            MagicalAbility.BattleDefense => _magicalAbilityLocalizations[3],
            MagicalAbility.WrestlingMastery => _magicalAbilityLocalizations[4],
            MagicalAbility.Poisoning => _magicalAbilityLocalizations[5],
            MagicalAbility.Bushido => _magicalAbilityLocalizations[6],
            MagicalAbility.Ninjitsu => _magicalAbilityLocalizations[7],
            MagicalAbility.Discordance => _magicalAbilityLocalizations[8],
            MagicalAbility.MageryMastery => _magicalAbilityLocalizations[9],
            MagicalAbility.Mysticism => _magicalAbilityLocalizations[10],
            MagicalAbility.Spellweaving => _magicalAbilityLocalizations[11],
            MagicalAbility.Chivalry => _magicalAbilityLocalizations[12],
            MagicalAbility.Necromage => _magicalAbilityLocalizations[13],
            MagicalAbility.Necromancy => _magicalAbilityLocalizations[14],
            MagicalAbility.Magery => _magicalAbilityLocalizations[15],
            _ => new TextDefinition[] { null, null }
        };

    public static TextDefinition[] GetLocalization(SpecialAbility ability)
    {
        var index = Array.IndexOf(SpecialAbility.Abilities, ability);

        if (index < 0 || index >= _specialAbilityLocalizations.Length)
        {
            return new TextDefinition[] { null, null };
        }

        return _specialAbilityLocalizations[index];
    }

    public static TextDefinition[] GetLocalization(WeaponAbility effect)
    {
        var index = Array.IndexOf(WeaponAbilities, effect);

        if (index < 0 || index >= _weaponAbilityLocalizations.Length)
        {
            return new TextDefinition[] { null, null };
        }

        return _weaponAbilityLocalizations[index];
    }

    public static TextDefinition[] GetLocalization(AreaEffect effect)
    {
        var index = Array.IndexOf(AreaEffect.Effects, effect);

        if (index < 0 || index >= _areaEffectLocalizations.Length)
        {
            return new TextDefinition[] { null, null };
        }

        return _areaEffectLocalizations[index];
    }

    public static TextDefinition[] GetLocalization(SkillName skill)
    {
        var tp = TrainingPoints.FirstOrDefault(t => t.TrainPoint is SkillName sk && sk == skill);

        if (tp != null)
        {
            return new[] { tp.Name, tp.Description };
        }

        return new TextDefinition[] { SkillInfo.Table[(int)skill].Name, 1157522 };
    }

    public static int GetCategoryLocalization(object o)
    {
        switch (o)
        {
            case MagicalAbility: return 1157481;
            case SpecialAbility: return 1157480;
            case AreaEffect: return 1157482;
            case WeaponAbility: return 1157479;
            case PetStat: return 1114262;
            case ResistanceType: return 1154539;
            case SkillName skill:
                {
                    foreach (var combat in CombatSkills)
                    {
                        if (skill == combat)
                        {
                            return 1157496;
                        }
                    }

                    return 1157495;
                }
        }

        return 0;
    }

    // -- [PetTrainTest <type> (:1669-1743) ---------------------------------------------------------------------------------

    public static void Initialize()
    {
        CommandSystem.Register("PetTrainTest", AccessLevel.GameMaster, PetTrainTest_OnCommand);
    }

    [Usage("PetTrainTest <type>")]
    [Description("Spawns a creature of the given type, tamed and bonded to you, its skills scaled as a tame's are.")]
    [ShardCommand(CommandCategory.DevTool)]
    private static void PetTrainTest_OnCommand(CommandEventArgs e)
    {
        var m = e.Mobile;
        var arg = e.GetString(0);

        if (string.IsNullOrEmpty(arg))
        {
            m.SendMessage("PetTrainTest <type>");
            return;
        }

        var t = AssemblyHandler.FindTypeByName(arg);

        if (t == null || !t.IsSubclassOf(typeof(BaseCreature)))
        {
            return;
        }

        if (Activator.CreateInstance(t) is not BaseCreature bc)
        {
            return;
        }

        if (!bc.Tamable)
        {
            m.SendMessage("That cannot be tamed!");
            bc.Delete();
        }
        else if (m.Followers + bc.ControlSlots > m.FollowersMax)
        {
            m.SendLocalizedMessage(1049611); // You have too many followers to tame that creature.
            bc.Delete();
        }
        else
        {
            bc.MoveToWorld(m.Location, m.Map);

            if (bc is GreaterDragon)
            {
                AnimalTaming.ScaleSkills(bc, 0.72, 0.90); // 72% of original skills trainable to 90%
                bc.Skills[SkillName.Magery].Base = bc.Skills[SkillName.Magery].Cap;
                // Greater dragons have a 90% cap reduction and 90% skill reduction on magery
            }
            else
            {
                AnimalTaming.ScaleSkills(bc, 0.90);
            }

            Timer.DelayCall(
                TimeSpan.FromSeconds(.25),
                () =>
                {
                    bc.PrivateOverheadMessage(MessageType.Regular, 0x3B2, 502799, m.NetState);
                    // It seems to accept you as master.
                    bc.Owners.Add(m);

                    bc.SetControlMaster(m);
                    bc.IsBonded = true;

                    PetTrainingHooks.OnTamed(bc, m);
                }
            );
        }
    }
}
