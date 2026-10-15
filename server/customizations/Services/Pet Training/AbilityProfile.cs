// cc-P121: Pet Training part 1, the ability core. Ported from ServUO Scripts/Services/Pet Training/AbilityProfile.cs
// (759 lines). What a creature can do beyond its stats: one magical school (or, wild, several), its special abilities,
// its area effects and its weapon abilities, plus the regeneration bonuses and the record of what training added.
//
// Two changes of shape, both from Chase's 2026-10-15 decisions:
//  1a. Natural abilities. A profile also answers for what the creature has by nature in pinned: a twin its stock
//      GetMonsterAbilities() carries (a Dragon's FireBreath is its DragonBreath), AuraDamage for HasAura, Heal for
//      CanHeal, Magery for AI_Mage and Poisoning for poisoning skill (ServUO sets those two in every creature's
//      constructor: BaseCreature.InitializeAbilities and SetSkill, ServUO BaseCreature.cs:606-633, 5211-5224).
//      They are computed from the creature, never stored, so a pinned bump that changes a monster changes its profile.
//      SpecialAbilities, AreaEffects, HasAbility and AbilityCount see them; the trigger dispatch does not, since
//      pinned's pipeline fires them. A natural magical school a tame replaces or loses is masked out and the mask saved.
//  2a. Storage. ServUO's Serialize/Deserialize (:613-757) become Write/Read on AbilityProfileStore's own save file,
//      by name rather than by array index. A profile that holds nothing but natural abilities is not saved.
//
// Not here (P130, training): TrainingPoint requirements in OnAddAbility (:248-270, which raise a skill or add a
// weapon ability a trained ability needs), TamingPetQuest in OnTame, and the training gumps that read
// CanChoose*/Advancements. BaseCreature.Mastery and its skill-mastery casting have no pinned system to run in; the
// mastery skill is kept on the profile so nothing training records is lost.
//
// The AI a magical school sets (AddMagicalAbility, :417-483) is kept where pinned has the AI: AI_Melee and AI_Mage.
// AI_Samurai, AI_Ninja, AI_Mystic, AI_Spellweaving, AI_Paladin, AI_NecroMage and AI_Necro do not exist in pinned
// (AIType.cs), so those schools leave the AI as it is.

using System.Collections.Generic;
using System.Linq;
using Server.Items;

namespace Server.Mobiles;

[PropertyObject]
public class AbilityProfile
{
    // Saved format version, AbilityProfileStore writes it per profile.
    public const int Version = 0;

    private MagicalAbility _magicalAbility;
    private MagicalAbility _removedNaturalMagical;
    private SpecialAbility[] _specialAbilities;
    private AreaEffect[] _areaEffects;

    public AbilityProfile(BaseCreature bc)
    {
        Creature = bc;
        DamageIndex = -1;
        Mastery = SkillName.Alchemy; // ServUO's "none"
    }

    /// <summary>Stored and natural magical abilities together; a natural one replaced or removed is masked out.</summary>
    [CommandProperty(AccessLevel.GameMaster)]
    public MagicalAbility MagicalAbility => _magicalAbility | NaturalMagicalAbility & ~_removedNaturalMagical;

    [CommandProperty(AccessLevel.GameMaster)]
    public MagicalAbility StoredMagicalAbility => _magicalAbility;

    /// <summary>Stored and natural area effects together.</summary>
    public AreaEffect[] AreaEffects => EnumerateAreaEffects().ToArray();

    /// <summary>Stored and natural special abilities together.</summary>
    public SpecialAbility[] SpecialAbilities => EnumerateSpecialAbilities().ToArray();

    [CommandProperty(AccessLevel.GameMaster)]
    public string AbilityNames =>
        string.Join(
            ", ",
            EnumerateAllAbilities().Select(
                o => o switch
                {
                    SpecialAbility s => s.Name,
                    AreaEffect a => a.Name,
                    WeaponAbility w => w.GetType().Name,
                    _ => o.ToString()
                }
            )
        );

    public WeaponAbility[] WeaponAbilities { get; private set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool TokunoTame { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public int RegenHits { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public int RegenStam { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public int RegenMana { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public int DamageIndex { get; set; }

    /// <summary>ServUO's BaseCreature.Mastery: kept, unused (pinned has no skill masteries).</summary>
    [CommandProperty(AccessLevel.GameMaster)]
    public SkillName Mastery { get; set; }

    [CommandProperty(AccessLevel.GameMaster)]
    public BaseCreature Creature { get; }

    public List<object> Advancements { get; private set; }

    // -- Natural abilities (decision 1a) --------------------------------------------------------------------------

    public MagicalAbility NaturalMagicalAbility => GetNaturalMagicalAbility(Creature);

    public static MagicalAbility GetNaturalMagicalAbility(BaseCreature bc)
    {
        var natural = MagicalAbility.None;

        if (bc == null)
        {
            return natural;
        }

        if (bc.AI == AIType.AI_Mage)
        {
            natural |= MagicalAbility.Magery;
        }

        if (bc.Skills.Poisoning.Base > 0)
        {
            natural |= MagicalAbility.Poisoning;
        }

        return natural;
    }

    public IEnumerable<SpecialAbility> EnumerateNaturalSpecialAbilities()
    {
        var abilities = SpecialAbility.Abilities;

        for (var i = 0; i < abilities.Length; i++)
        {
            if (abilities[i] is PinnedTwinAbility twin && twin.IsNatural(Creature))
            {
                yield return twin;
            }
        }
    }

    public IEnumerable<AreaEffect> EnumerateNaturalAreaEffects()
    {
        var effects = AreaEffect.Effects;

        for (var i = 0; i < effects.Length; i++)
        {
            if (effects[i].IsNatural(Creature))
            {
                yield return effects[i];
            }
        }
    }

    /// <summary>
    /// Whether this creature has <paramref name="ability" />, stored or natural, without making a profile for it.
    /// </summary>
    public static bool HasMagicalAbility(BaseCreature bc, MagicalAbility ability) =>
        AbilityProfileStore.TryGet(bc, out var profile)
            ? profile.HasAbility(ability)
            : (GetNaturalMagicalAbility(bc) & ability) != 0;

    // -- ServUO's methods -------------------------------------------------------------------------------------------

    public void OnTame()
    {
        // ServUO also calls TamingPetQuest.CheckTame here; that quest is not on this shard. Wired in P130 (AnimalTaming).
        if (Creature.Map == Map.Tokuno)
        {
            TokunoTame = true;
        }
    }

    public bool AddAbility(MagicalAbility ability, bool advancement = true)
    {
        if (Creature.Controlled)
        {
            var oldAbility = MagicalAbility;

            if (IsSpecialMagicalAbility(oldAbility))
            {
                RemoveSpecialMagicalAbility(oldAbility);
            }

            OnRemoveMagicalAbility(oldAbility, ability);

            _magicalAbility = ability;
            _removedNaturalMagical = ~ability; // a tame holds one school: the natural ones are replaced too
        }
        else
        {
            _magicalAbility |= ability;
            _removedNaturalMagical &= ~ability;
        }

        OnAddAbility(ability, advancement);
        return true;
    }

    public bool AddAbility(SpecialAbility ability, bool advancement = true)
    {
        if (!HasAbility(ability))
        {
            _specialAbilities = _specialAbilities == null ? new[] { ability } : [.._specialAbilities, ability];
        }

        OnAddAbility(ability, advancement);

        return true;
    }

    public bool AddAbility(AreaEffect ability, bool advancement = true)
    {
        if (!HasAbility(ability))
        {
            _areaEffects = _areaEffects == null ? new[] { ability } : [.._areaEffects, ability];
        }

        OnAddAbility(ability, advancement);

        return true;
    }

    public bool AddAbility(WeaponAbility ability, bool advancement = true)
    {
        if (WeaponAbilities == null)
        {
            WeaponAbilities = new[] { ability };
        }
        else if (!WeaponAbilities.Any(a => a == ability))
        {
            WeaponAbilities = [..WeaponAbilities, ability];
        }

        OnAddAbility(ability, advancement);

        return true;
    }

    public void RemoveAbility(MagicalAbility ability)
    {
        if ((MagicalAbility & ability) != 0)
        {
            _magicalAbility &= ~ability;
            _removedNaturalMagical |= ability;
            RemovePetAdvancement(ability);
        }
    }

    public void RemoveAbility(SpecialAbility ability)
    {
        if (_specialAbilities == null || !_specialAbilities.Any(a => a == ability))
        {
            return;
        }

        RemovePetAdvancement(ability);
        _specialAbilities = _specialAbilities.Where(a => a != ability).ToArray();
    }

    public void RemoveAbility(WeaponAbility ability)
    {
        if (WeaponAbilities == null || !WeaponAbilities.Any(a => a == ability))
        {
            return;
        }

        RemovePetAdvancement(ability);
        WeaponAbilities = WeaponAbilities.Where(a => a != ability).ToArray();
    }

    public void RemoveAbility(AreaEffect ability)
    {
        if (_areaEffects == null || !_areaEffects.Any(a => a == ability))
        {
            return;
        }

        RemovePetAdvancement(ability);
        _areaEffects = _areaEffects.Where(a => a != ability).ToArray();
    }

    public bool AddAbility(SkillName skill, bool advancement = true)
    {
        OnAddAbility(skill, advancement);
        return true;
    }

    public bool CanAddAbility(object o)
    {
        if (!Creature.Controlled)
        {
            return true;
        }

        if (o is MagicalAbility)
        {
            return true;
        }

        if (o is SpecialAbility && SpecialAbilities.Length == 0)
        {
            return true;
        }

        if (o is AreaEffect && AreaEffects.Length == 0)
        {
            return true;
        }

        if (o is WeaponAbility && (WeaponAbilities == null || WeaponAbilities.Length < 2))
        {
            return true;
        }

        return false;
    }

    public void OnAddAbility(object newAbility, bool advancement)
    {
        if (advancement)
        {
            AddPetAdvancement(newAbility);
        }

        if (newAbility is MagicalAbility magical)
        {
            AddMagicalAbility(magical);
        }

        // ServUO then raises the skills and adds the weapon abilities the ability's TrainingPoint requires (:248-270).
        // Training points arrive with P130.
    }

    public void AddPetAdvancement(object o)
    {
        if (Creature.Controlled)
        {
            Advancements ??= new List<object>();

            if (!Advancements.Contains(o))
            {
                Advancements.Add(o);
            }
        }
    }

    public void RemovePetAdvancement(object o)
    {
        if (Creature.Controlled)
        {
            Advancements?.Remove(o);
        }
    }

    public bool HasAbility(object o)
    {
        switch (o)
        {
            case MagicalAbility magical:
                return (MagicalAbility & magical) != 0;
            case SpecialAbility special:
                return _specialAbilities?.Any(a => a == special) == true ||
                       special is PinnedTwinAbility twin && twin.IsNatural(Creature);
            case AreaEffect effect:
                return _areaEffects?.Any(a => a == effect) == true || effect.IsNatural(Creature);
            case WeaponAbility weapon:
                return WeaponAbilities?.Any(a => a == weapon) == true;
            default:
                return false;
        }
    }

    public int AbilityCount()
    {
        var count = 0;

        if (MagicalAbility != MagicalAbility.None)
        {
            count++;
        }

        count += SpecialAbilities.Count(a => !a.NaturalAbility);
        count += AreaEffects.Length;

        if (WeaponAbilities != null)
        {
            count += WeaponAbilities.Length;
        }

        return count;
    }

    public bool CanChooseMagicalAbility(MagicalAbility ability)
    {
        if (!Creature.Controlled)
        {
            return true;
        }

        return !(HasSpecialMagicalAbility() &&
                 IsSpecialMagicalAbility(ability) &&
                 SpecialAbilities.Any(a => !a.NaturalAbility));
    }

    public bool CanChooseSpecialAbility(SpecialAbility[] list)
    {
        if (!Creature.Controlled)
        {
            return true;
        }

        var special = SpecialAbilities;
        var area = AreaEffects;

        if (HasSpecialMagicalAbility() &&
            list.Any(IsRuleBreaker) &&
            area.Length == 0 &&
            (special.Length == 0 || special.All(a => a.NaturalAbility)))
        {
            return true;
        }

        return !HasSpecialMagicalAbility() && !special.Any(a => !a.NaturalAbility) && AbilityCount() < 3;
    }

    public bool IsRuleBreaker(SpecialAbility ability) => PetTrainingHelper.RuleBreakers.Any(abil => abil == ability);

    public bool CanChooseAreaEffect()
    {
        if (!Creature.Controlled)
        {
            return true;
        }

        var area = AreaEffects;

        if (HasSpecialMagicalAbility() && area.Length == 0 && SpecialAbilities.Length == 0)
        {
            return true;
        }

        return !HasSpecialMagicalAbility() && area.Length == 0 && AbilityCount() < 3;
    }

    public bool CanChooseWeaponAbility()
    {
        if (!Creature.Controlled)
        {
            return true;
        }

        return !HasSpecialMagicalAbility() && (WeaponAbilities == null || WeaponAbilities.Length < 2) &&
               AbilityCount() < 3;
    }

    public bool HasSpecialMagicalAbility()
    {
        var m = MagicalAbility;

        return (m & MagicalAbility.Piercing) != 0 ||
               (m & MagicalAbility.Bashing) != 0 ||
               (m & MagicalAbility.Slashing) != 0 ||
               (m & MagicalAbility.BattleDefense) != 0 ||
               (m & MagicalAbility.WrestlingMastery) != 0;
    }

    public bool IsSpecialMagicalAbility(MagicalAbility ability) =>
        ability != MagicalAbility.None && ability <= MagicalAbility.WrestlingMastery;

    public void AddMagicalAbility(MagicalAbility ability)
    {
        if (IsSpecialMagicalAbility(ability))
        {
            WeaponAbilities = null;
            Creature.AI = AIType.AI_Melee;
        }

        switch (ability)
        {
            case MagicalAbility.Piercing:
                Mastery = SkillName.Fencing;
                break;
            case MagicalAbility.Bashing:
                Mastery = SkillName.Macing;
                break;
            case MagicalAbility.Slashing:
                Mastery = SkillName.Swords;
                break;
            case MagicalAbility.BattleDefense:
                Mastery = SkillName.Parry;
                break;
            case MagicalAbility.WrestlingMastery:
                Mastery = SkillName.Wrestling;
                break;
            case MagicalAbility.Poisoning:
            case MagicalAbility.Discordance:
                if (Creature.Controlled && Creature.AI != AIType.AI_Melee)
                {
                    Creature.AI = AIType.AI_Melee;
                }

                break;
            case MagicalAbility.Bushido:
                // ServUO: AI_Samurai, which pinned lacks.
                if (!HasAbility(WeaponAbility.WhirlwindAttack))
                {
                    AddAbility(WeaponAbility.WhirlwindAttack, false);
                }

                break;
            case MagicalAbility.Ninjitsu:
                // ServUO: AI_Ninja, which pinned lacks.
                if (!HasAbility(WeaponAbility.FrenziedWhirlwind))
                {
                    AddAbility(WeaponAbility.FrenziedWhirlwind, false);
                }

                break;
            case MagicalAbility.Magery:
            case MagicalAbility.MageryMastery:
                if (Creature.Controlled && Creature.AI != AIType.AI_Mage)
                {
                    Creature.AI = AIType.AI_Mage;
                }

                break;
            // Mysticism, Spellweaving, Chivalry, Necromage, Necromancy: ServUO switches to an AI pinned lacks.
        }
    }

    public void OnRemoveMagicalAbility(MagicalAbility oldAbility, MagicalAbility newAbility)
    {
        if ((oldAbility & MagicalAbility.Bushido) != 0 && HasAbility(WeaponAbility.WhirlwindAttack))
        {
            RemoveAbility(WeaponAbility.WhirlwindAttack);
        }

        if ((oldAbility & MagicalAbility.Ninjitsu) != 0 && HasAbility(WeaponAbility.FrenziedWhirlwind))
        {
            RemoveAbility(WeaponAbility.FrenziedWhirlwind);
        }
    }

    public void RemoveSpecialMagicalAbility(MagicalAbility ability)
    {
        WeaponAbilities = null;
        Mastery = SkillName.Alchemy; // default
    }

    public bool HasCustomized() => Advancements?.Count > 0;

    public bool IsNaturalAbility(object o)
    {
        if (Advancements == null)
        {
            return true;
        }

        return o switch
        {
            SpecialAbility special => SpecialAbilities.Length > 0 &&
                                      !Advancements.Any(s => s is SpecialAbility a && a == special),
            WeaponAbility weapon => WeaponAbilities != null &&
                                    !Advancements.Any(s => s is WeaponAbility a && a == weapon),
            _ => false
        };
    }

    public IEnumerable<object> EnumerateAllAbilities()
    {
        var magical = MagicalAbility;

        if (magical != MagicalAbility.None)
        {
            foreach (var abil in PetTrainingHelper.MagicalAbilities)
            {
                if ((magical & abil) != 0)
                {
                    yield return abil;
                }
            }
        }

        foreach (var abil in EnumerateSpecialAbilities())
        {
            yield return abil;
        }

        foreach (var effect in EnumerateAreaEffects())
        {
            yield return effect;
        }

        if (WeaponAbilities != null)
        {
            foreach (var abil in WeaponAbilities)
            {
                yield return abil;
            }
        }
    }

    /// <summary>Stored, then natural; each once.</summary>
    public IEnumerable<SpecialAbility> EnumerateSpecialAbilities()
    {
        if (_specialAbilities != null)
        {
            foreach (var ability in _specialAbilities)
            {
                yield return ability;
            }
        }

        foreach (var ability in EnumerateNaturalSpecialAbilities())
        {
            if (_specialAbilities?.Contains(ability) != true)
            {
                yield return ability;
            }
        }
    }

    /// <summary>What this file's trigger dispatch may fire: stored abilities that pinned is not already firing.</summary>
    public IEnumerable<SpecialAbility> EnumerateDispatchableSpecialAbilities()
    {
        if (_specialAbilities == null)
        {
            yield break;
        }

        foreach (var ability in _specialAbilities)
        {
            if (ability is not PinnedTwinAbility twin || !twin.IsNatural(Creature))
            {
                yield return ability;
            }
        }
    }

    public SpecialAbility[] GetSpecialAbilities() => SpecialAbilities;

    public IEnumerable<AreaEffect> EnumerateAreaEffects()
    {
        if (_areaEffects != null)
        {
            foreach (var effect in _areaEffects)
            {
                yield return effect;
            }
        }

        foreach (var effect in EnumerateNaturalAreaEffects())
        {
            if (_areaEffects?.Contains(effect) != true)
            {
                yield return effect;
            }
        }
    }

    public IEnumerable<AreaEffect> EnumerateDispatchableAreaEffects()
    {
        if (_areaEffects == null)
        {
            yield break;
        }

        foreach (var effect in _areaEffects)
        {
            if (!effect.IsNatural(Creature))
            {
                yield return effect;
            }
        }
    }

    public AreaEffect[] GetAreaEffects() => AreaEffects;

    public override string ToString() => "...";

    // -- Storage (decision 2a) ----------------------------------------------------------------------------------------

    /// <summary>True when nothing here needs saving: no stored ability, mask, bonus or record.</summary>
    public bool IsEmpty =>
        _magicalAbility == MagicalAbility.None && _removedNaturalMagical == MagicalAbility.None &&
        (_specialAbilities?.Length ?? 0) == 0 && (_areaEffects?.Length ?? 0) == 0 &&
        (WeaponAbilities?.Length ?? 0) == 0 && (Advancements?.Count ?? 0) == 0 &&
        !TokunoTame && RegenHits == 0 && RegenStam == 0 && RegenMana == 0 && DamageIndex == -1 &&
        Mastery == SkillName.Alchemy;

    public void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(Version);

        writer.Write(DamageIndex);
        writer.Write((int)_magicalAbility);
        writer.Write((int)_removedNaturalMagical);
        writer.Write(TokunoTame);

        writer.Write(RegenHits);
        writer.Write(RegenStam);
        writer.Write(RegenMana);
        writer.Write((int)Mastery);

        WriteNames(writer, _specialAbilities?.Select(a => a.Name));
        WriteNames(writer, _areaEffects?.Select(a => a.Name));
        WriteNames(writer, WeaponAbilities?.Select(a => a.GetType().Name));

        writer.WriteEncodedInt(Advancements?.Count ?? 0);

        if (Advancements != null)
        {
            foreach (var o in Advancements)
            {
                switch (o)
                {
                    case MagicalAbility magical:
                        writer.WriteEncodedInt(1);
                        writer.Write((int)magical);
                        break;
                    case SpecialAbility special:
                        writer.WriteEncodedInt(2);
                        writer.Write(special.Name);
                        break;
                    case AreaEffect effect:
                        writer.WriteEncodedInt(3);
                        writer.Write(effect.Name);
                        break;
                    case WeaponAbility weapon:
                        writer.WriteEncodedInt(4);
                        writer.Write(weapon.GetType().Name);
                        break;
                    case SkillName skill:
                        writer.WriteEncodedInt(5);
                        writer.Write((int)skill);
                        break;
                    default:
                        writer.WriteEncodedInt(0);
                        break;
                }
            }
        }
    }

    public void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version is < 0 or > Version)
        {
            throw new System.IO.InvalidDataException($"AbilityProfile version {version} is not one this build reads.");
        }

        DamageIndex = reader.ReadInt();
        _magicalAbility = (MagicalAbility)reader.ReadInt();
        _removedNaturalMagical = (MagicalAbility)reader.ReadInt();
        TokunoTame = reader.ReadBool();

        RegenHits = reader.ReadInt();
        RegenStam = reader.ReadInt();
        RegenMana = reader.ReadInt();
        Mastery = (SkillName)reader.ReadInt();

        _specialAbilities = ReadNames(reader, SpecialAbility.GetByName);
        _areaEffects = ReadNames(reader, AreaEffect.GetByName);
        WeaponAbilities = ReadNames(reader, GetWeaponAbilityByName);

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            object o = reader.ReadEncodedInt() switch
            {
                1 => (MagicalAbility)reader.ReadInt(),
                2 => SpecialAbility.GetByName(reader.ReadString()),
                3 => AreaEffect.GetByName(reader.ReadString()),
                4 => GetWeaponAbilityByName(reader.ReadString()),
                5 => (SkillName)reader.ReadInt(),
                _ => null
            };

            if (o != null)
            {
                (Advancements ??= new List<object>()).Add(o);
            }
        }
    }

    private static void WriteNames(IGenericWriter writer, IEnumerable<string> names)
    {
        var list = names?.ToList();
        writer.WriteEncodedInt(list?.Count ?? 0);

        if (list != null)
        {
            foreach (var name in list)
            {
                writer.Write(name);
            }
        }
    }

    // A name this build no longer knows is dropped rather than failing the load.
    private static T[] ReadNames<T>(IGenericReader reader, System.Func<string, T> lookup) where T : class
    {
        var count = reader.ReadEncodedInt();

        if (count == 0)
        {
            return null;
        }

        var list = new List<T>(count);

        for (var i = 0; i < count; i++)
        {
            var found = lookup(reader.ReadString());

            if (found != null)
            {
                list.Add(found);
            }
        }

        return list.Count > 0 ? list.ToArray() : null;
    }

    public static WeaponAbility GetWeaponAbilityByName(string name) =>
        WeaponAbility.Abilities.FirstOrDefault(a => a != null && a.GetType().Name == name);
}
