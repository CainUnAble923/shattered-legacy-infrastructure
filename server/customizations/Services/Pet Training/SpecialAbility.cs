// cc-P121: Pet Training part 1, the ability core. Ported from ServUO Scripts/Services/Pet Training/SpecialAbility.cs
// (2,688 lines). ServUO's table holds 29 abilities (_Abilities = new SpecialAbility[29], :236-268), not the 30 the P120
// survey and the P121 brief state; counted from the array.
//
// Chase's decision 1a (2026-10-15): one ability set. Seven abilities have a pinned twin and run through pinned's class:
//   DragonBreath -> FireBreath (per-type definitions below), GraspingClaw, TailSwipe, RuneCorruption, ColossalBlow,
//   LifeLeech -> DrainLifeAttack; and in AreaEffects.cs, AuraDamage -> BaseCreature.HasAura/AuraDamage().
// Heal is an eighth, found here: ServUO's Heal.Trigger calls BaseCreature.CheckHeal, whose pinned equivalent is the
// CanHeal/HealStart block in BaseCreature.OnThink (BaseCreature.cs:3897-3916). Same rule, so the same treatment.
// A twin the creature carries by nature (its stock GetMonsterAbilities() has it, or CanHeal) is listed in its profile
// as a natural ability and fires through pinned's own pipeline only; the profile's trigger dispatch skips it, so a tamed
// Dragon breathes once. A twin set on a creature that lacks it (SetSpecialAbility, or training in P130) fires through
// this file's dispatch, which hands the pinned class its own trigger, chance and cooldown.
//
// The other 22 are ported line for line, with these changes, each argued in notes/cc-P121-pet-ability-core.md:
//  - ServUO's DamageType is AbilityDamageType here: Server.Items.DamageType (SAPropEffects.cs) already exists, and a
//    Server.Mobiles.DamageType would silently take precedence over that import in every Server.Mobiles file.
//  - CheckCombatTrigger: ServUO writes "(melee && doMelee) || (spell && doSpell) && !IsInCooldown", so && binds first
//    and a melee-triggered ability is never checked for cooldown (Trigger does not check it either). Its 30 s cooldown is
//    computed, tracked and ignored. Parenthesised here so it is honoured; the D-4 rule (broken, not missing, so fixed).
//  - A recursion guard: an ability's own AOS.Damage re-enters CheckCombatTrigger (ServUO too); depth is capped at 4.
//  - ViciousBite sends cliloc 1234567, which is not a cliloc. The text of ServUO's own comment is sent instead.
//  - LifeDrain loops over every target in range but drains, messages and tests the Succubus rule on the defender each
//    time; it acts on each target here. LifeShieldLotion does not exist on this shard, so its reduction is not ported
//    (no player can have it; the result is identical).
//  - Timers lose ServUO's TimerPriority (pinned has none); ResistanceMods take a name (pinned requires one).

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.BuffIcons;
using Server.Items;
using Server.Network;

namespace Server.Mobiles;

// ServUO's Server.DamageType (Misc/AOS.cs). Renamed; see the header.
public enum AbilityDamageType
{
    Melee,
    Ranged,
    Spell,
    SpellAOE
}

public abstract class SpecialAbility
{
    public virtual int ManaCost => 10;
    public virtual int MaxRange => 1;
    public virtual double TriggerChance => 0.1;
    public virtual bool RequiresCombatant => true;
    public virtual TimeSpan CooldownDuration => TimeSpan.FromSeconds(30);

    public virtual MagicalAbility RequiredSchool => MagicalAbility.None;
    public virtual bool NaturalAbility => false;

    public virtual bool TriggerOnGotMeleeDamage => false;
    public virtual bool TriggerOnDoMeleeDamage => false;
    public virtual bool TriggerOnGotSpellDamage => false;
    public virtual bool TriggerOnDoSpellDamage => false;
    public virtual bool TriggerOnThink => false;
    public virtual bool TriggerOnApproach => false;

    /// <summary>ServUO's class name, the key the profile store saves by.</summary>
    public virtual string Name => GetType().Name;

    public abstract void DoEffects(BaseCreature creature, Mobile defender, ref int damage);

    private const int MaxTriggerDepth = 4;

    [ThreadStatic]
    private static int _triggerDepth;

    public static void CheckCombatTrigger(Mobile attacker, Mobile defender, ref int damage, AbilityDamageType type)
    {
        if (defender == null || _triggerDepth >= MaxTriggerDepth)
        {
            return;
        }

        _triggerDepth++;

        try
        {
            if (attacker is BaseCreature { Summoned: false } bc)
            {
                var profile = PetTrainingHelper.GetAbilityProfile(bc);

                if (profile != null)
                {
                    var abilities = profile.EnumerateDispatchableSpecialAbilities().Where(
                        m =>
                            (type == AbilityDamageType.Melee && m.TriggerOnDoMeleeDamage ||
                             type >= AbilityDamageType.Spell && m.TriggerOnDoSpellDamage) &&
                            !m.IsInCooldown(attacker)
                    ).ToArray();

                    if (abilities.Length > 0)
                    {
                        abilities[Utility.Random(abilities.Length)].Trigger(bc, defender, ref damage);
                    }
                }
            }

            if (defender is BaseCreature { Summoned: false } dbc)
            {
                var profile = PetTrainingHelper.GetAbilityProfile(dbc);

                if (profile != null)
                {
                    var abilities = profile.EnumerateDispatchableSpecialAbilities().Where(
                        m =>
                            (type == AbilityDamageType.Melee && m.TriggerOnGotMeleeDamage ||
                             type >= AbilityDamageType.Spell && m.TriggerOnGotSpellDamage) &&
                            !m.IsInCooldown(defender)
                    ).ToArray();

                    if (abilities.Length > 0)
                    {
                        abilities[Utility.Random(abilities.Length)].Trigger(dbc, attacker, ref damage);
                    }
                }
            }
        }
        finally
        {
            _triggerDepth--;
        }
    }

    public static bool CheckThinkTrigger(BaseCreature bc)
    {
        var profile = PetTrainingHelper.GetAbilityProfile(bc);

        if (profile == null)
        {
            return false;
        }

        if (bc.Combatant is { } combatant)
        {
            var abilities = profile.EnumerateDispatchableSpecialAbilities()
                .Where(m => m.TriggerOnThink && !m.IsInCooldown(bc)).ToArray();

            if (abilities.Length > 0)
            {
                var d = 0;
                abilities[Utility.Random(abilities.Length)].Trigger(bc, combatant, ref d);
            }
        }
        else
        {
            var abilities = profile.EnumerateDispatchableSpecialAbilities()
                .Where(m => m.TriggerOnThink && !m.IsInCooldown(bc) && !m.RequiresCombatant).ToArray();

            if (abilities.Length > 0)
            {
                var d = 0;
                return abilities[Utility.Random(abilities.Length)].Trigger(bc, bc, ref d);
            }
        }

        return false;
    }

    public static bool CheckApproachTrigger(BaseCreature bc, Mobile mobile, Point3D oldLocation)
    {
        var profile = PetTrainingHelper.GetAbilityProfile(bc);

        if (profile == null)
        {
            return false;
        }

        var abilities = profile.EnumerateDispatchableSpecialAbilities().Where(
            m =>
                m.TriggerOnApproach &&
                bc.InRange(mobile.Location, m.MaxRange) &&
                !bc.InRange(oldLocation, m.MaxRange) &&
                !m.IsInCooldown(bc)
        ).ToArray();

        if (abilities.Length > 0)
        {
            var d = 0;
            return abilities[Utility.Random(abilities.Length)].Trigger(bc, mobile, ref d);
        }

        return false;
    }

    public virtual bool Trigger(BaseCreature creature, Mobile defender, ref int damage)
    {
        if (CheckMana(creature) && Validate(creature, defender) && TriggerChance >= Utility.RandomDouble())
        {
            creature.Mana -= ManaCost;

            DoEffects(creature, defender, ref damage);
            AddToCooldown(creature);

            return true;
        }

        return false;
    }

    public virtual bool Validate(BaseCreature attacker, Mobile defender)
    {
        if (RequiredSchool != MagicalAbility.None)
        {
            var profile = PetTrainingHelper.GetAbilityProfile(attacker);

            if (profile?.HasAbility(RequiredSchool) != true)
            {
                return false;
            }
        }

        return defender is { Alive: true, Deleted: false, IsDeadBondedPet: false } &&
               attacker.Alive && !attacker.IsDeadBondedPet && defender.InRange(attacker.Location, MaxRange) &&
               defender.Map == attacker.Map && attacker.InLOS(defender) && !attacker.BardPacified &&
               attacker.CanBeHarmful(defender);
    }

    public bool CheckMana(Mobile m) => m.Mana >= ManaCost;

    protected List<Mobile> _Cooldown;

    public bool IsInCooldown(Mobile m) => _Cooldown?.Contains(m) == true;

    public virtual void AddToCooldown(BaseCreature m)
    {
        var duration = CooldownDuration;

        if (duration != TimeSpan.MinValue)
        {
            _Cooldown ??= new List<Mobile>();
            _Cooldown.Add(m);
            Timer.DelayCall(duration, RemoveFromCooldown, (Mobile)m);
        }
    }

    public void RemoveFromCooldown(Mobile m) => _Cooldown?.Remove(m);

    private static readonly SpecialAbility[] _abilities =
    {
        new AngryFire(),           // 0
        new ConductiveBlast(),     // 1
        new DragonBreathTwin(),    // 2
        new GraspingClawTwin(),    // 3
        new Inferno(),             // 4
        new LightningForce(),      // 5
        new ManaDrain(),           // 6
        new RagingBreath(),        // 7
        new Repel(),               // 8
        new SearingWounds(),       // 9
        new StealLife(),           // 10
        new VenomousBite(),        // 11
        new ViciousBite(),         // 12
        new RuneCorruptionTwin(),  // 13
        new LifeLeechTwin(),       // 14
        new StickySkin(),          // 15
        new TailSwipeTwin(),       // 16
        new FlurryForce(),         // 17
        new Rage(),                // 18

        // Non-Trainable
        new HealTwin(),            // 19
        new HowlOfCacophony(),     // 20
        new Webbing(),             // 21
        new Anemia(),              // 22
        new BloodDisease(),        // 23
        new PoisonSpit(),          // 24
        new TrueFear(),            // 25
        new ColossalBlowTwin(),    // 26
        new LifeDrain(),           // 27
        new ColossalRage()         // 28
    };

    public static SpecialAbility[] Abilities => _abilities;

    public static SpecialAbility GetByName(string name)
    {
        for (var i = 0; i < _abilities.Length; i++)
        {
            if (_abilities[i].Name == name)
            {
                return _abilities[i];
            }
        }

        return null;
    }

    public static SpecialAbility AngryFire => _abilities[0];
    public static SpecialAbility ConductiveBlast => _abilities[1];
    public static SpecialAbility DragonBreath => _abilities[2];
    public static SpecialAbility GraspingClaw => _abilities[3];
    public static SpecialAbility Inferno => _abilities[4];
    public static SpecialAbility LightningForce => _abilities[5];
    public static SpecialAbility ManaDrain => _abilities[6];
    public static SpecialAbility RagingBreath => _abilities[7];
    public static SpecialAbility Repel => _abilities[8];
    public static SpecialAbility SearingWounds => _abilities[9];
    public static SpecialAbility StealLife => _abilities[10];
    public static SpecialAbility VenomousBite => _abilities[11];
    public static SpecialAbility ViciousBite => _abilities[12];
    public static SpecialAbility RuneCorruption => _abilities[13];
    public static SpecialAbility LifeLeech => _abilities[14];
    public static SpecialAbility StickySkin => _abilities[15];
    public static SpecialAbility TailSwipe => _abilities[16];
    public static SpecialAbility FlurryForce => _abilities[17];
    public static SpecialAbility Rage => _abilities[18];
    public static SpecialAbility Heal => _abilities[19];
    public static SpecialAbility HowlOfCacophony => _abilities[20];
    public static SpecialAbility Webbing => _abilities[21];
    public static SpecialAbility Anemia => _abilities[22];
    public static SpecialAbility BloodDisease => _abilities[23];
    public static SpecialAbility PoisonSpit => _abilities[24];
    public static SpecialAbility TrueFear => _abilities[25];
    public static SpecialAbility ColossalBlow => _abilities[26];
    public static SpecialAbility LifeDrain => _abilities[27];
    public static SpecialAbility ColossalRage => _abilities[28];
}

// -------------------------------------------------------------------------------------------------------------------
// The twins (decision 1a). Each runs through one pinned MonsterAbility instance, shared by every creature as stock
// creatures share theirs; pinned keeps cooldowns and effect tables per creature inside the instance.
// -------------------------------------------------------------------------------------------------------------------

public abstract class PinnedTwinAbility : SpecialAbility
{
    /// <summary>The pinned class this ability runs through, for this creature.</summary>
    public abstract MonsterAbility GetPinned(BaseCreature bc);

    /// <summary>True when the creature has it by nature: pinned's own pipeline fires it, so the profile does not.</summary>
    public abstract bool IsNatural(BaseCreature bc);

    // Pinned's own trigger, chance and cooldown, not ServUO's mana and 30 s.
    public override bool Trigger(BaseCreature creature, Mobile defender, ref int damage)
    {
        var pinned = GetPinned(creature);
        var trigger = pinned.AbilityTrigger;

        if (defender == null || !pinned.CanTrigger(creature, trigger))
        {
            return false;
        }

        pinned.Trigger(trigger, creature, defender);
        return true;
    }

    // Called directly only by ColossalRage's rage hit (BaseCreature.DoRageHit in ServUO): the effect, no chance roll.
    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        var pinned = GetPinned(creature);
        pinned.Trigger(pinned.AbilityTrigger, creature, defender);
    }

    protected static bool StockHas(BaseCreature bc, MonsterAbilityType type) => bc.GetAbility(type) != null;
}

public class DragonBreathTwin : PinnedTwinAbility
{
    public override string Name => "DragonBreath";
    public override int MaxRange => 12;
    public override bool TriggerOnThink => true;
    public override int ManaCost => 30;

    public override bool IsNatural(BaseCreature bc) => StockHas(bc, MonsterAbilityType.FireBreath);

    public override MonsterAbility GetPinned(BaseCreature bc) =>
        bc.GetAbility(MonsterAbilityType.FireBreath) ?? DragonBreathDefinitions.For(bc);
}

/// <summary>
/// ServUO's DragonBreathDefinition table (SpecialAbility.cs:885-1063), carried onto pinned's FireBreath. ServUO picks by
/// exact type and falls back to Definitions[0], which pinned FireBreath already is line for line
/// (CC6FollowupBreathIncubatorVerification). Only a creature that lacks a stock breath reaches this.
/// </summary>
public static class DragonBreathDefinitions
{
    private static readonly FireBreath _default = new();
    private static Dictionary<string, FireBreath> _byType;

    public static FireBreath For(BaseCreature bc)
    {
        _byType ??= Build();
        return _byType.TryGetValue(bc.GetType().Name, out var breath) ? breath : _default;
    }

    private static Dictionary<string, FireBreath> Build()
    {
        var cold = new ColdBreath(); // Skeletal Dragon / Renowned: cold 100, hue 0x480 - pinned ColdBreath exactly.
        var frost = new DefinedBreath(Core.AOS ? 0.16 : 0.05, 0, 0, 100, 0, 0, 0, 30.0, 45.0, 1264, 0x227);

        return new Dictionary<string, FireBreath>
        {
            ["SkeletalDragon"] = cold,
            ["SkeletalDragonRenowned"] = cold,
            ["Leviathan"] = new DefinedBreath(0.05, 70, 0, 30, 0, 0, 0, 5.0, 7.5, 0x1ED, 0x227),
            ["RedDeath"] = new DefinedBreath(0.16, 0, 0, 0, 0, 0, 100, 5.0, 7.5, 0x1ED, 0x227),
            ["FrostDragon"] = frost,
            ["ColdDrake"] = frost,
            ["AntLion"] = new DefinedBreath(Core.AOS ? 0.16 : 0.05, 0, 0, 0, 100, 0, 0, 30.0, 45.0, 0x3F, 0),
            ["Rend"] = new DefinedBreath(0.06, 0, 100, 0, 0, 0, 0, 30.0, 45.0, 0, 0x227),
            ["CrystalSeaSerpent"] = new DefinedBreath(0.55, 0, 0, 50, 0, 50, 0, 30.0, 45.0, 0x1ED, 0x227),
            ["CrystalHydra"] = new CrystalHydraBreath()
        };
    }

    // ServUO's constructor assigns its effectDuration argument (5) to EffectSpeed and never sets EffectDuration, so every
    // definition runs at speed 5, duration 0: pinned FireBreath's own values. The anger animation is 12 in all of them.
    private class DefinedBreath : FireBreath
    {
        private readonly double _scalar;
        private readonly int _phys, _fire, _cold, _pois, _nrgy, _chaos, _hue, _sound;
        private readonly TimeSpan _min, _max;

        public DefinedBreath(
            double scalar, int phys, int fire, int cold, int pois, int nrgy, int chaos, double min, double max, int hue,
            int sound
        )
        {
            _scalar = scalar;
            _phys = phys;
            _fire = fire;
            _cold = cold;
            _pois = pois;
            _nrgy = nrgy;
            _chaos = chaos;
            _min = TimeSpan.FromSeconds(min);
            _max = TimeSpan.FromSeconds(max);
            _hue = hue;
            _sound = sound;
        }

        public override double BreathDamageScalar => _scalar;
        public override int PhysicalDamage => _phys;
        public override int FireDamage => _fire;
        public override int ColdDamage => _cold;
        public override int PoisonDamage => _pois;
        public override int EnergyDamage => _nrgy;
        public override int ChaosDamage => _chaos;
        public override TimeSpan MinTriggerCooldown => _min;
        public override TimeSpan MaxTriggerCooldown => _max;
        public override int BreathEffectHue => _hue;
        public override int BreathEffectSound => _sound;
    }
}

public class GraspingClawTwin : PinnedTwinAbility
{
    private static readonly GraspingClaw _pinned = new();

    public override string Name => "GraspingClaw";
    public override bool TriggerOnDoMeleeDamage => true;
    public override int ManaCost => 30;

    public override bool IsNatural(BaseCreature bc) => StockHas(bc, MonsterAbilityType.GraspingClaw);

    public override MonsterAbility GetPinned(BaseCreature bc) => bc.GetAbility(MonsterAbilityType.GraspingClaw) ?? _pinned;
}

public class TailSwipeTwin : PinnedTwinAbility
{
    private static readonly TailSwipe _pinned = new();

    public override string Name => "TailSwipe";
    public override bool TriggerOnDoMeleeDamage => true;
    public override int ManaCost => 30;

    public override bool IsNatural(BaseCreature bc) => StockHas(bc, MonsterAbilityType.TailSwipe);

    public override MonsterAbility GetPinned(BaseCreature bc) => bc.GetAbility(MonsterAbilityType.TailSwipe) ?? _pinned;
}

public class RuneCorruptionTwin : PinnedTwinAbility
{
    private static readonly RuneCorruption _pinned = new();

    public override string Name => "RuneCorruption";
    public override bool TriggerOnDoMeleeDamage => true;
    public override int ManaCost => 30;

    public override bool IsNatural(BaseCreature bc) => StockHas(bc, MonsterAbilityType.RuneCorruption);

    public override MonsterAbility GetPinned(BaseCreature bc) =>
        bc.GetAbility(MonsterAbilityType.RuneCorruption) ?? _pinned;
}

public class LifeLeechTwin : PinnedTwinAbility
{
    private static readonly DrainLifeAttack _pinned = new();

    public override string Name => "LifeLeech";
    public override int ManaCost => 5;
    public override bool TriggerOnDoMeleeDamage => true;

    public override bool IsNatural(BaseCreature bc) => StockHas(bc, MonsterAbilityType.DrainLife);

    public override MonsterAbility GetPinned(BaseCreature bc) => bc.GetAbility(MonsterAbilityType.DrainLife) ?? _pinned;
}

public class ColossalBlowTwin : PinnedTwinAbility
{
    private static readonly ColossalBlow _pinned = new();

    public override string Name => "ColossalBlow";
    public override bool TriggerOnDoMeleeDamage => true;
    public override bool NaturalAbility => true;
    public override int ManaCost => 0;
    public override double TriggerChance => 0.3;
    public override TimeSpan CooldownDuration => TimeSpan.FromSeconds(10);

    public override bool IsNatural(BaseCreature bc) => bc.GetAbility(MonsterAbilityType.Stun) is ColossalBlow;

    public override MonsterAbility GetPinned(BaseCreature bc) =>
        bc.GetAbility(MonsterAbilityType.Stun) as ColossalBlow ?? _pinned;
}

/// <summary>
/// ServUO's Heal (SpecialAbility.cs:2405-2437) calls BaseCreature.CheckHeal; pinned's equivalent is CanHeal with
/// HealStart, run from OnThink. A creature that is CanHeal has it by nature. Set on one that is not, it keeps ServUO's
/// trigger (think, no combatant needed, 15 mana, no cooldown) and starts pinned's heal.
/// </summary>
public class HealTwin : PinnedTwinAbility
{
    public override string Name => "Heal";
    public override bool TriggerOnThink => true;
    public override double TriggerChance => 1.0;
    public override bool RequiresCombatant => false;
    public override int ManaCost => 15;
    public override TimeSpan CooldownDuration => TimeSpan.MinValue;
    public override bool NaturalAbility => true;

    public override bool IsNatural(BaseCreature bc) => bc.CanHeal;

    public override MonsterAbility GetPinned(BaseCreature bc) => null;

    public override bool Trigger(BaseCreature creature, Mobile defender, ref int damage)
    {
        if (CheckMana(creature) && Validate(creature, defender) && TriggerChance >= Utility.RandomDouble() &&
            CheckHeal(creature))
        {
            creature.Mana -= ManaCost;
            AddToCooldown(creature);
            return true;
        }

        return false;
    }

    public override void DoEffects(BaseCreature creature, Mobile target, ref int damage) => CheckHeal(creature);

    // Pinned's own self-heal condition (BaseCreature.cs:3909-3913), without its private interval.
    private static bool CheckHeal(BaseCreature bc)
    {
        if (!bc.Alive || bc.IsHealing || bc.BardPacified || !bc.CanBeBeneficial(bc) ||
            bc.Hits >= bc.HealTrigger * bc.HitsMax && !bc.Poisoned)
        {
            return false;
        }

        bc.HealStart(bc);
        return true;
    }
}

// -------------------------------------------------------------------------------------------------------------------
// The 22 ported from ServUO.
// -------------------------------------------------------------------------------------------------------------------

public class AngryFire : SpecialAbility
{
    public override bool TriggerOnDoMeleeDamage => true;
    public override int ManaCost => 30;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        var d = Utility.RandomMinMax(30, 40);

        AOS.Damage(defender, creature, d, 60, 20, 0, 0, 20);

        defender.FixedParticles(0x3709, 10, 30, 5052, EffectLayer.LeftFoot);
        defender.PlaySound(0x208);

        defender.SendLocalizedMessage(1070823); // The creature hits you with its Angry Fire.
    }
}

public class ConductiveBlast : SpecialAbility
{
    public override bool TriggerOnDoMeleeDamage => true;
    public override int ManaCost => 30;

    private static Dictionary<Mobile, ExpireTimer> _table;

    public static bool IsUnderEffects(Mobile m) => _table?.ContainsKey(m) == true;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        _table ??= new Dictionary<Mobile, ExpireTimer>();

        if (_table.TryGetValue(defender, out var timer))
        {
            timer.DoExpire();
            defender.SendLocalizedMessage(1070828); // The creature continues to hinder your energy resistance!
        }
        else
        {
            defender.SendLocalizedMessage(1070827); // The creature's attack has made you more susceptible to energy attacks!
        }

        var effect = -(defender.EnergyResistance / 2);

        var mod = new ResistanceMod(ResistanceType.Energy, "PetConductiveBlast", effect);

        defender.FixedEffect(0x37B9, 10, 5);
        defender.AddResistanceMod(mod);

        timer = new ExpireTimer(defender, mod, TimeSpan.FromSeconds(10.0));
        timer.Start();
        _table[defender] = timer;
    }

    public static void RemoveFromTable(Mobile m) => _table?.Remove(m);

    private class ExpireTimer : Timer
    {
        private readonly Mobile _mobile;
        private readonly ResistanceMod _mod;

        public ExpireTimer(Mobile m, ResistanceMod mod, TimeSpan delay) : base(delay)
        {
            _mobile = m;
            _mod = mod;
        }

        public void DoExpire()
        {
            _mobile.RemoveResistanceMod(_mod);
            Stop();
            RemoveFromTable(_mobile);
        }

        protected override void OnTick()
        {
            _mobile.SendLocalizedMessage(1070829); // Your resistance to energy attacks has returned.
            DoExpire();
        }
    }
}

public class FlurryForce : SpecialAbility
{
    public override bool TriggerOnDoMeleeDamage => true;
    public override bool NaturalAbility => true;
    public override int ManaCost => 0;

    private static Dictionary<Mobile, ExpireTimer> _table;

    public static bool IsUnderEffects(Mobile m) => _table?.ContainsKey(m) == true;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        _table ??= new Dictionary<Mobile, ExpireTimer>();

        if (_table.TryGetValue(defender, out var timer))
        {
            timer.DoExpire();
            defender.SendLocalizedMessage(1070851); // The creature lands another blow in your weakened state.
        }
        else
        {
            // The creature's flurry of twigs has made you more susceptible to physical attacks!
            defender.SendLocalizedMessage(1070850);
        }

        var effect = -(defender.PhysicalResistance * 15 / 100);

        var mod = new ResistanceMod(ResistanceType.Physical, "PetFlurryForce", effect);

        defender.FixedEffect(0x37B9, 10, 5);
        defender.AddResistanceMod(mod);

        timer = new ExpireTimer(defender, mod, TimeSpan.FromSeconds(10.0));
        timer.Start();
        _table[defender] = timer;
    }

    public static void RemoveFromTable(Mobile m) => _table?.Remove(m);

    private class ExpireTimer : Timer
    {
        private readonly Mobile _mobile;
        private readonly ResistanceMod _mod;

        public ExpireTimer(Mobile m, ResistanceMod mod, TimeSpan delay) : base(delay)
        {
            _mobile = m;
            _mod = mod;
        }

        public void DoExpire()
        {
            _mobile.RemoveResistanceMod(_mod);
            Stop();
            RemoveFromTable(_mobile);
        }

        protected override void OnTick()
        {
            // ServUO sends the energy line here too (SpecialAbility.cs:683); copied.
            _mobile.SendLocalizedMessage(1070829); // Your resistance to energy attacks has returned.
            DoExpire();
        }
    }
}

public class Inferno : SpecialAbility
{
    public override bool TriggerOnDoMeleeDamage => true;
    public override int ManaCost => 30;

    private static Dictionary<Mobile, ExpireTimer> _table;

    public static bool IsUnderEffects(Mobile m) => _table?.ContainsKey(m) == true;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        _table ??= new Dictionary<Mobile, ExpireTimer>();

        if (_table.TryGetValue(defender, out var timer))
        {
            timer.DoExpire();
        }

        defender.SendLocalizedMessage(1070833); // The creature fans you with fire, reducing your resistance to fire attacks.

        var mod = new ResistanceMod(ResistanceType.Fire, "PetInferno", -25);

        Effects.SendLocationParticles(defender, 0x3709, 10, 30, 5052);
        Effects.PlaySound(defender.Location, defender.Map, 0x208);

        timer = new ExpireTimer(defender, mod, TimeSpan.FromSeconds(6.0));
        timer.Start();

        AOS.Damage(defender, creature, Utility.RandomMinMax(46, 79), 0, 100, 0, 0, 0);

        _table[defender] = timer;
    }

    public static void Expire(Mobile m) => _table?.Remove(m);

    private class ExpireTimer : Timer
    {
        private readonly Mobile _mobile;
        private readonly ResistanceMod _mod;

        public ExpireTimer(Mobile m, ResistanceMod mod, TimeSpan delay) : base(delay)
        {
            _mobile = m;
            _mod = mod;

            m.AddResistanceMod(mod);
        }

        public void DoExpire()
        {
            _mobile.RemoveResistanceMod(_mod);
            Stop();

            Expire(_mobile);
        }

        protected override void OnTick()
        {
            _mobile.SendLocalizedMessage(1070834); // Your resistance to fire attacks has returned.
            DoExpire();
        }
    }
}

public class LightningForce : SpecialAbility
{
    public override bool TriggerOnDoMeleeDamage => true;
    public override int ManaCost => 30;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        Effects.SendBoltEffect(defender);
        AOS.Damage(defender, creature, Utility.RandomMinMax(15, 20), 0, 0, 0, 0, 100);
    }
}

public class ManaDrain : SpecialAbility
{
    public override bool TriggerOnGotMeleeDamage => true;
    public override bool TriggerOnDoMeleeDamage => true;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        if (creature.Map == null)
        {
            return;
        }

        var list = new List<Mobile>();

        foreach (var m in creature.GetMobilesInRange(8))
        {
            if (AreaEffect.ValidTarget(creature, m))
            {
                list.Add(m);
            }
        }

        foreach (var m in list)
        {
            creature.DoHarmful(m);

            m.FixedParticles(0x374A, 10, 15, 5013, 0x496, 0, EffectLayer.Waist);
            m.PlaySound(0x231);

            m.SendMessage("You feel the mana drain out of you!");

            var toDrain = Utility.RandomMinMax(40, 60);

            creature.Mana += toDrain;
            m.Mana -= toDrain;
        }
    }
}

public class RagingBreath : SpecialAbility
{
    private static Dictionary<Mobile, InternalTimer> _table;

    public override bool TriggerOnDoMeleeDamage => true;
    public override int ManaCost => 30;

    public static bool IsUnderEffects(Mobile m) => _table?.ContainsKey(m) == true;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        _table ??= new Dictionary<Mobile, InternalTimer>();

        if (_table.TryGetValue(defender, out var timer))
        {
            timer.Stop();
            defender.SendLocalizedMessage(1070841); // The creature's breath continues to burn you!
        }
        else
        {
            defender.SendLocalizedMessage(1070842); // The creature's breath is burning you!
        }

        SendFlame(defender, 1, 15, 2735, 4, 9502);
        SendFlame(defender, 10, 30, 0, 0, 52);
        defender.PlaySound(520);

        timer = new InternalTimer(creature, defender);
        timer.Start();

        _table[defender] = timer;
    }

    // ServUO's two ParticleEffect packets (SpecialAbility.cs:1518-1519), built by pinned's own packet writer.
    private static void SendFlame(Mobile defender, int speed, int duration, int hue, int effect, int explodeEffect)
    {
        var layer = defender is BaseCreature ? 163 : 45;

        Span<byte> buffer = stackalloc byte[OutgoingEffectPackets.ParticleEffectLength].InitializePacket();
        OutgoingEffectPackets.CreateParticleEffect(
            buffer,
            EffectType.FixedFrom, defender.Serial, Serial.Zero, 0x3709, defender.Location, defender.Location,
            speed, duration, false, false, hue, 0, effect, explodeEffect, 1, defender.Serial, layer, 0
        );
        Effects.SendPacket(defender.Location, defender.Map, buffer);
    }

    public static void EndFire(Mobile m)
    {
        if (_table?.Remove(m, out var timer) == true)
        {
            timer.Stop();
            m.SendLocalizedMessage(1070843); // The fiery breath dissipates.
        }
    }

    private class InternalTimer : Timer
    {
        private readonly Mobile _attacker;
        private readonly Mobile _defender;
        private int _tick;

        public InternalTimer(Mobile attacker, Mobile defender) : base(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))
        {
            _attacker = attacker;
            _defender = defender;
        }

        protected override void OnTick()
        {
            _tick++;

            if (_tick > 10 || !_defender.Alive)
            {
                EndFire(_defender);
            }
            else
            {
                SendFlame(_defender, 1, 15, 2735, 4, 9502);
                AOS.Damage(_defender, _attacker, 10, 0, 100, 0, 0, 0);
            }
        }
    }
}

public class Repel : SpecialAbility
{
    public override bool TriggerOnGotMeleeDamage => true;
    public override bool TriggerOnGotSpellDamage => true;
    public override int ManaCost => 30;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        defender.SendLocalizedMessage(1070844); // The creature repels the attack back at you.
        defender.FixedEffect(0x37B9, 10, 5);
        AOS.Damage(defender, creature, damage / 2, false, 0, 0, 0, 0, 0, 0, 100);

        damage = 0;
    }
}

public class SearingWounds : SpecialAbility
{
    private static Dictionary<Mobile, InternalTimer> _table;

    public override int ManaCost => 25;
    public override bool TriggerOnDoMeleeDamage => true;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        if (_table?.ContainsKey(defender) == true)
        {
            return;
        }

        _table ??= new Dictionary<Mobile, InternalTimer>();
        _table[defender] = new InternalTimer(defender);

        defender.FixedParticles(0x374A, 10, 15, 5013, 0x496, 0, EffectLayer.Waist);
        defender.SendLocalizedMessage(1151177); // The searing attack cauterizes the wound on impact.
    }

    /// <summary>Read by Bandage and SpellHelper.Heal (Bandage-/SpellHelper-searing-wounds patches): heals halve.</summary>
    public static bool IsUnderEffects(Mobile m) => _table?.ContainsKey(m) == true;

    private class InternalTimer : Timer
    {
        private readonly Mobile _defender;

        public InternalTimer(Mobile defender) : base(TimeSpan.FromSeconds(10))
        {
            _defender = defender;
            Start();
        }

        protected override void OnTick()
        {
            _table?.Remove(_defender);
            Stop();
        }
    }
}

public class StealLife : SpecialAbility
{
    public override bool TriggerOnDoMeleeDamage => true;
    public override int ManaCost => 30;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        defender.FixedParticles(0x374A, 1, 15, 5054, 23, 7, EffectLayer.Head);
        defender.PlaySound(0x1F9);

        AOS.Damage(defender, creature, damage, 0, 0, 0, 0, 100);

        new InternalTimer(creature, defender, damage).Start();
    }

    private class InternalTimer : Timer
    {
        private readonly BaseCreature _attacker;
        private readonly Mobile _defender;
        private readonly int _toHeal;

        public InternalTimer(BaseCreature creature, Mobile defender, int toHeal)
            : base(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5), 5)
        {
            _attacker = creature;
            _defender = defender;
            _toHeal = toHeal;

            defender.FixedParticles(0x374A, 10, 15, 5013, 0x496, 0, EffectLayer.Waist);
            defender.PlaySound(0x231);
        }

        protected override void OnTick()
        {
            _defender.FixedParticles(0x374A, 10, 15, 5013, 0x496, 0, EffectLayer.Waist);
            _attacker.Hits = Math.Min(_attacker.HitsMax, _attacker.Hits + _toHeal / 5);
        }
    }
}

public class VenomousBite : SpecialAbility
{
    public override int ManaCost => 30;
    public override bool TriggerOnDoMeleeDamage => true;
    public override MagicalAbility RequiredSchool => MagicalAbility.Poisoning;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        var list = new List<Mobile> { defender };

        foreach (var m in creature.GetMobilesInRange(3))
        {
            if (AreaEffect.ValidTarget(creature, m))
            {
                list.Add(m);
            }
        }

        var p = PetTrainingHooks.GetHitPoison(creature);

        if (p == null)
        {
            return;
        }

        foreach (var m in list)
        {
            // ServUO plays these on the defender for every target (SpecialAbility.cs:1731-1732); copied.
            defender.PlaySound(0xDD);
            defender.FixedParticles(0x3728, 244, 25, 9941, 1266, 0, EffectLayer.Waist);

            m.SendLocalizedMessage(1008097, false, creature.Name); //  : poisoned you!

            m.ApplyPoison(creature, p);
        }

        if (creature.Controlled && list.Count > 0)
        {
            var profile = PetTrainingHelper.GetAbilityProfile(creature);

            if (profile?.HasAbility(MagicalAbility.Poisoning) == true || 0.2 > Utility.RandomDouble())
            {
                creature.CheckSkill(SkillName.Poisoning, 0, creature.Skills.Poisoning.Cap);
            }
        }
    }
}

public class ViciousBite : SpecialAbility
{
    public override bool TriggerOnDoMeleeDamage => true;
    public override int ManaCost => 20;

    private static Dictionary<Mobile, InternalTimer> _table;

    public static bool IsUnderEffects(Mobile m) => _table?.ContainsKey(m) == true;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        if (_table?.ContainsKey(defender) == true)
        {
            return;
        }

        _table ??= new Dictionary<Mobile, InternalTimer>();

        defender.PlaySound(0x1324);
        // ServUO: SendLocalizedMessage(1234567), not a cliloc. The text of its own comment instead.
        defender.SendMessage("The creature gives you a particular vicious bite.");
        Effects.SendLocationParticles(
            EffectItem.Create(defender.Location, defender.Map, EffectItem.DefaultDuration),
            0x37CC, 1, 40, 97, 3, 9917, 0
        );

        _table[defender] = new InternalTimer(creature, defender);
    }

    private class InternalTimer : Timer
    {
        private readonly BaseCreature _attacker;
        private readonly Mobile _defender;
        private int _tick;

        public InternalTimer(BaseCreature creature, Mobile defender)
            : base(TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(20))
        {
            _attacker = creature;
            _defender = defender;
            Start();
        }

        protected override void OnTick()
        {
            _tick++;

            AOS.Damage(_defender, _attacker, _tick * 5, false, 0, 0, 0, 0, 0, 0, 100);
            _defender.SendLocalizedMessage(1112473); // Your vicious wound is festering!

            if (_tick >= 20 || !_defender.Alive || _defender.IsDeadBondedPet)
            {
                Stop();
                _table?.Remove(_defender);
            }
        }
    }
}

public class StickySkin : SpecialAbility
{
    public override int ManaCost => 5;
    public override bool TriggerOnDoMeleeDamage => true;
    public override bool TriggerOnGotMeleeDamage => true;

    private static List<Mobile> _table;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        _table ??= new List<Mobile>();

        defender.SendLocalizedMessage(1153752); // Your attack speed has been slowed.

        _table.Add(defender);
        Timer.DelayCall(TimeSpan.FromSeconds(10), () => RemoveEffects(defender));
    }

    /// <summary>Read by AosAttributes.GetValue (AOS-pet-ability-triggers.patch): -30 swing speed.</summary>
    public static bool IsUnderEffects(Mobile m) => _table?.Contains(m) == true;

    public static void RemoveEffects(Mobile m) => _table?.Remove(m);
}

public class Rage : SpecialAbility
{
    public override bool TriggerOnDoMeleeDamage => true;
    public override bool NaturalAbility => true;
    public override int ManaCost => 0;

    private static Dictionary<Mobile, ExpireTimer> _table;

    public static bool IsUnderEffects(Mobile m) => _table?.ContainsKey(m) == true;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        _table ??= new Dictionary<Mobile, ExpireTimer>();

        if (_table.TryGetValue(defender, out var timer))
        {
            timer.DoExpire();
            defender.SendLocalizedMessage(1070825); // The creature continues to rage!
        }
        else
        {
            defender.SendLocalizedMessage(1070826); // The creature goes into a rage, inflicting heavy damage!
        }

        timer = new ExpireTimer(defender, creature);
        timer.Start();
        _table[defender] = timer;
    }

    private class ExpireTimer : Timer
    {
        private readonly Mobile _mobile;
        private readonly Mobile _from;
        private int _count;

        public ExpireTimer(Mobile m, Mobile from) : base(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0))
        {
            _mobile = m;
            _from = from;
        }

        public void DoExpire()
        {
            Stop();
            _table?.Remove(_mobile);
        }

        private void DrainLife()
        {
            if (_mobile.Alive)
            {
                _mobile.Damage(2, _from);
            }
            else
            {
                DoExpire();
            }
        }

        protected override void OnTick()
        {
            DrainLife();

            if (++_count >= 5)
            {
                DoExpire();
                _mobile.SendLocalizedMessage(1070824); // The creature's rage subsides.
            }
        }
    }
}

public class HowlOfCacophony : SpecialAbility
{
    private static Dictionary<Mobile, InternalTimer> _table;

    public override int ManaCost => 25;
    public override bool TriggerOnDoMeleeDamage => true;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        if (_table?.ContainsKey(defender) == true)
        {
            return;
        }

        _table ??= new Dictionary<Mobile, InternalTimer>();
        _table[defender] = new InternalTimer(defender);

        defender.NetState.SendSpeedControl(SpeedControlSetting.Walk);
        defender.SendLocalizedMessage(1072069); // A cacophonic sound lambastes you, suppressing your ability to move.
        defender.PlaySound(0x584);

        (defender as PlayerMobile)?.AddBuff(
            new BuffInfo(BuffIcon.HowlOfCacophony, 1153793, 1153820, TimeSpan.FromSeconds(30), "60\t5\t5")
        );
    }

    /// <summary>Read by AosAttributes.GetValue (AOS-pet-ability-triggers.patch).</summary>
    public static bool IsUnderEffects(Mobile m) => _table?.ContainsKey(m) == true;

    private class InternalTimer : Timer
    {
        private readonly Mobile _defender;

        public InternalTimer(Mobile defender) : base(TimeSpan.FromSeconds(30))
        {
            _defender = defender;
            Start();
        }

        protected override void OnTick()
        {
            if (_table?.Remove(_defender) == true)
            {
                (_defender as PlayerMobile)?.RemoveBuff(BuffIcon.HowlOfCacophony);
                _defender.NetState.SendSpeedControl(SpeedControlSetting.Disable);
            }

            Stop();
        }
    }
}

public class Webbing : SpecialAbility
{
    public override int ManaCost => 0;
    public override bool NaturalAbility => true;
    public override bool TriggerOnDoMeleeDamage => true;
    public override bool TriggerOnGotMeleeDamage => true;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        if (creature.Map == null)
        {
            return;
        }

        var list = new List<Mobile>();

        foreach (var m in creature.GetMobilesInRange(12))
        {
            if (AreaEffect.ValidTarget(creature, m))
            {
                list.Add(m);
            }
        }

        if (list.Count > 0)
        {
            var m = list.RandomElement();

            creature.DoHarmful(m);
            creature.Direction = creature.GetDirectionTo(m);

            var web = new SpiderWebbing(m);
            Effects.SendMovingParticles(
                creature, m, web.ItemID, 12, 0, false, false, 0, 0, 9502, 1, 0, (EffectLayer)255, 0x100
            );
            Timer.DelayCall(TimeSpan.FromSeconds(0.5), () => web.MoveToWorld(m.Location, m.Map));
        }
    }
}

public class Anemia : SpecialAbility
{
    public override bool TriggerOnDoMeleeDamage => true;
    public override bool TriggerOnGotMeleeDamage => true;
    public override bool NaturalAbility => true;
    public override int ManaCost => 0;

    private static Dictionary<Mobile, ExpireTimer> _table;

    public static bool IsUnderEffects(Mobile m) => _table?.ContainsKey(m) == true;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        _table ??= new Dictionary<Mobile, ExpireTimer>();

        if (_table.ContainsKey(defender))
        {
            defender.PublicOverheadMessage(MessageType.Regular, 0x25, 1111668); // * The creature is repulsed by your diseased blood. *
        }
        else
        {
            // *The creature drains some of your blood to replenish its health.*
            defender.PublicOverheadMessage(MessageType.Regular, 0x25, 1111698);

            creature.Hits = Math.Min(creature.HitsMax, creature.Hits + Utility.RandomMinMax(50, 70));
        }

        TryInfect(creature, defender);
    }

    private static void TryInfect(BaseCreature creature, Mobile attacker)
    {
        if (!_table.ContainsKey(attacker) && creature.InRange(attacker, 1) && 0.25 > Utility.RandomDouble() &&
            !FountainOfFortune.UnderProtection(attacker))
        {
            attacker.SendLocalizedMessage(1111669); // The creature's attack weakens you. You have become anemic.

            Span<byte> effect = stackalloc byte[OutgoingEffectPackets.HuedEffectLength].InitializePacket();
            OutgoingEffectPackets.CreateHuedEffect(
                effect,
                EffectType.FixedFrom, attacker.Serial, Serial.Zero, 0x375A, attacker.Location, attacker.Location,
                9, 20, true, false, 0, 0
            );
            Effects.SendPacket(attacker.Location, attacker.Map, effect);
            Effects.SendTargetParticles(attacker, 0x373A, 1, 15, 0x26B9, EffectLayer.Head);
            Effects.SendLocationParticles(attacker, 0x11A6, 9, 32, 0x253A);

            attacker.PlaySound(0x1ED);

            var timer = new ExpireTimer(attacker);
            timer.Start();

            var str = attacker.RawStr / 3;
            var dex = attacker.RawDex / 3;
            var intel = attacker.RawInt / 3;

            attacker.AddStatMod(new StatMod(StatType.Str, "BloodWorm_Str", -str, TimeSpan.FromSeconds(60)));
            attacker.AddStatMod(new StatMod(StatType.Dex, "BloodWorm_Dex", -dex, TimeSpan.FromSeconds(60)));
            attacker.AddStatMod(new StatMod(StatType.Int, "BloodWorm_Int", -intel, TimeSpan.FromSeconds(60)));

            // -~1_STR~ strength.<br>-~2_INT~ intelligence.<br>-~3_DEX~ dexterity.<br> Drains all stamina.
            (attacker as PlayerMobile)?.AddBuff(
                new BuffInfo(BuffIcon.BloodwormAnemia, 1153797, 1153824, default, $"{str}\t{dex}\t{intel}")
            );

            _table.Add(attacker, timer);
        }
    }

    private class ExpireTimer : Timer
    {
        private readonly DateTime _expires;
        private readonly Mobile _victim;

        public ExpireTimer(Mobile m) : base(TimeSpan.FromSeconds(2.0), TimeSpan.FromSeconds(2.0))
        {
            _victim = m;
            _expires = Core.Now + TimeSpan.FromMinutes(1);
        }

        protected override void OnTick()
        {
            if (_expires < Core.Now || _victim.Deleted || !_victim.Alive || _victim.IsDeadBondedPet)
            {
                _victim.SendLocalizedMessage(1111670); // You recover from your anemia.

                _table.Remove(_victim);

                (_victim as PlayerMobile)?.RemoveBuff(BuffIcon.BloodwormAnemia);

                Stop();
            }
            else
            {
                _victim.Stam -= _victim.Stam < 2 ? 0 : Utility.RandomMinMax(2, 5);
            }
        }
    }
}

public class BloodDisease : SpecialAbility
{
    public override bool TriggerOnDoMeleeDamage => true;
    public override bool TriggerOnGotMeleeDamage => true;
    public override bool NaturalAbility => true;
    public override int ManaCost => 0;

    private static Dictionary<Mobile, ExpireTimer> _table;

    public static bool IsUnderEffects(Mobile m) => _table?.ContainsKey(m) == true;

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        _table ??= new Dictionary<Mobile, ExpireTimer>();

        if (!_table.ContainsKey(defender) && creature.InRange(defender, 1) && 0.25 > Utility.RandomDouble() &&
            !FountainOfFortune.UnderProtection(defender))
        {
            // The rotworm has infected you with blood disease!!
            defender.SendLocalizedMessage(1111672, "", 0x25);

            defender.PlaySound(0x213);
            Effects.SendTargetParticles(defender, 0x373A, 1, 15, 0x26B9, EffectLayer.Head);

            var timer = new ExpireTimer(defender, creature);
            timer.Start();

            // TODO: 2nd cliloc (ServUO's own)
            (defender as PlayerMobile)?.AddBuff(new BuffInfo(BuffIcon.RotwormBloodDisease, 1153798, 1153798));

            _table.Add(defender, timer);
        }
    }

    private class ExpireTimer : Timer
    {
        private const int MaxCount = 8;

        private int _count;
        private readonly Mobile _victim;
        private readonly Mobile _attacker;

        public ExpireTimer(Mobile victim, Mobile attacker) : base(TimeSpan.FromSeconds(2.0), TimeSpan.FromSeconds(2.0))
        {
            _victim = victim;
            _attacker = attacker;
        }

        protected override void OnTick()
        {
            if (_count == MaxCount || _victim.Deleted || !_victim.Alive || _victim.IsDeadBondedPet)
            {
                // You no longer feel sick.
                _victim.SendLocalizedMessage(1111673);

                _table.Remove(_victim);

                (_victim as PlayerMobile)?.RemoveBuff(BuffIcon.RotwormBloodDisease);

                Stop();
            }
            else if (_count > 0)
            {
                AOS.Damage(_victim, _attacker, Utility.RandomMinMax(10, 20), 0, 0, 0, 100, 0);
                _victim.Combatant = null;
            }

            _count++;
        }
    }
}

public class PoisonSpit : SpecialAbility
{
    public override bool TriggerOnGotMeleeDamage => true;
    public override bool TriggerOnGotSpellDamage => true;
    public override int MaxRange => 10;

    public override bool NaturalAbility => true;
    public override int ManaCost => 0;

    public override void DoEffects(BaseCreature creature, Mobile target, ref int damage)
    {
        Effects.SendMovingEffect(creature, target, 0x36D4, 5, 0, false, true, 63, 0);

        Timer.DelayCall(
            TimeSpan.FromSeconds(1),
            () =>
            {
                creature.DoHarmful(target);
                target.ApplyPoison(creature, creature.HitPoison ?? Poison.Regular);
                target.SendLocalizedMessage(1070821, creature.Name); // ~1_CREATURE~ spits a poisonous substance at you!
            }
        );
    }
}

public class TrueFear : SpecialAbility
{
    public override bool TriggerOnApproach => true;
    public override int MaxRange => 8;

    public override bool NaturalAbility => true;
    public override int ManaCost => 0;
    public override TimeSpan CooldownDuration => TimeSpan.FromMinutes(Utility.RandomMinMax(1, 3));

    public override void DoEffects(BaseCreature creature, Mobile target, ref int damage)
    {
        var seconds = (int)Math.Max(1, 13.0 - target.Skills.MagicResist.Value / 10.0);

        int number;

        if (seconds <= 2)
        {
            number = 1080339; // A sense of discomfort passes through you, but it fades quickly
        }
        else if (seconds <= 4)
        {
            number = 1080340; // An unfamiliar fear washes over you, and for a moment you're unable to move
        }
        else if (seconds <= 7)
        {
            number = 1080341; // Panic grips you! You're unable to move, to think, to feel anything but fear!
        }
        else if (seconds <= 10)
        {
            // Terror slices into your very being, destroying any chance of resisting ~1_name~ you might have had
            number = 1080342;
        }
        else
        {
            // Everything around you dissolves into darkness as ~1_name~'s burning eyes fill your vision
            number = 1080343;
        }

        target.SendLocalizedMessage(number, creature.Name, 0x21);
        target.Frozen = true;

        (target as PlayerMobile)?.AddBuff(
            new BuffInfo(BuffIcon.TrueFear, 1153791, 1153827, TimeSpan.FromSeconds(seconds))
        );

        Timer.DelayCall(
            TimeSpan.FromSeconds(seconds),
            () =>
            {
                target.Frozen = false;
                target.SendLocalizedMessage(1005603); // You can move again!
            }
        );
    }
}

public class LifeDrain : SpecialAbility
{
    public override bool TriggerOnDoMeleeDamage => true;
    public override bool TriggerOnGotMeleeDamage => true;
    public override bool NaturalAbility => true;
    public override TimeSpan CooldownDuration => TimeSpan.FromSeconds(1);

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        foreach (var m in AreaEffect.FindValidTargets(creature, 2).ToList())
        {
            // ServUO tests, drains and messages `defender` here for every m (SpecialAbility.cs:2567-2592); each target.
            if (!CanDrainLife(creature, m))
            {
                continue;
            }

            creature.DoHarmful(m);

            m.FixedParticles(0x374A, 10, 15, 5013, 0x496, 0, EffectLayer.Waist);
            m.PlaySound(0x231);

            m.SendMessage("You feel the life drain out of you!");

            var toDrain = GetDrainAmount(creature, m);

            creature.Hits += toDrain;
            AOS.Damage(m, creature, toDrain, false, 0, 0, 0, 0, 0, 0, 100);
        }
    }

    public static bool CanDrainLife(BaseCreature bc, Mobile victim) => !(bc is Succubus && victim.Female);

    public static int GetDrainAmount(BaseCreature bc, Mobile victim)
    {
        var amount = Utility.RandomMinMax(10, 40);

        if (bc is Semidar && !victim.Female)
        {
            amount *= 2;
        }

        return amount;
    }
}

public class ColossalRage : SpecialAbility
{
    public override bool TriggerOnGotMeleeDamage => true;
    public override bool NaturalAbility => true;
    public override int ManaCost => 0;
    public override double TriggerChance => .5;
    public override TimeSpan CooldownDuration => TimeSpan.FromSeconds(15);

    public override bool Validate(BaseCreature attacker, Mobile defender) =>
        defender.Hits >= defender.HitsMax * .33 && base.Validate(attacker, defender);

    public override void DoEffects(BaseCreature creature, Mobile defender, ref int damage)
    {
        AddRage(creature);

        Timer.DelayCall(
            TimeSpan.FromSeconds(.25),
            () => creature.PublicOverheadMessage(MessageType.Regular, 0x20, 1113587) // The creature goes into a frenzied rage!
        );

        Timer.DelayCall(TimeSpan.FromSeconds(10), () => RemoveRage(creature));
    }

    public static void AddRage(BaseCreature bc)
    {
        InRage ??= new List<BaseCreature>();

        InRage.Add(bc);
        bc.HueMod = 1157;
        bc.Stam = bc.StamMax;
    }

    public static void RemoveRage(BaseCreature bc)
    {
        if (InRage?.Remove(bc) == true)
        {
            bc.HueMod = -1;

            if (InRage.Count == 0)
            {
                InRage = null;
            }
        }
    }

    public static bool HasRage(BaseCreature bc) => InRage?.Contains(bc) == true;

    public static List<BaseCreature> InRage { get; set; }
}
