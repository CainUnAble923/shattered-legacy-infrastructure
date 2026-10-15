// cc-P121: Pet Training part 1, the ability core. Ported from ServUO Scripts/Services/Pet Training/PetTrainingHelper.cs,
// the profile-access half only: the MagicalAbility enum (:62-115), Enabled (:119), GetAbilityProfile (:196-204), the
// MagicalAbilities table (:228-248) and RuleBreakers (:600-603). The training half (TrainingDefinition, TrainingPoint,
// the per-family ability tables, Class, PetStat, the skill categories, CheckSecondarySkill/OnWeaponAbilityUsed) is P130.
//
// One change of shape, by Chase's decision 2a (2026-10-15): ServUO keeps the profile in a BaseCreature field and saves it
// in BaseCreature's own save. Here it lives in AbilityProfileStore, a side table keyed by the creature, with its own save
// file, so BaseCreature's save format is untouched.

using System.Collections.Generic;

namespace Server.Mobiles;

[System.Flags]
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

    /// <summary>
    /// The creature's stored profile, made and stored when <paramref name="create" /> is set and it has none. A profile
    /// also answers for the abilities the creature has by nature (AbilityProfile's "natural" members), so a stock
    /// creature with no stored profile still lists its breath once asked with create set.
    /// </summary>
    public static AbilityProfile GetAbilityProfile(BaseCreature bc, bool create = false) =>
        bc == null ? null : AbilityProfileStore.Get(bc, create);

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

    private static SpecialAbility[] _ruleBreakers;

    // ServUO builds this in PetTrainingHelper.Initialize (:600-603); AbilityProfile.IsRuleBreaker reads it.
    public static IReadOnlyList<SpecialAbility> RuleBreakers => _ruleBreakers ??= new[]
    {
        SpecialAbility.SearingWounds, SpecialAbility.TailSwipe, SpecialAbility.DragonBreath, SpecialAbility.LifeLeech,
        SpecialAbility.ViciousBite
    };
}
