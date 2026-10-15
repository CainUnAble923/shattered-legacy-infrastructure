// cc-P121: Pet Training part 1, the ability core. Ported from ServUO Scripts/Services/Pet Training/AreaEffects.cs (596
// lines): the AreaEffect base and its seven effects, AuraOfEnergy, AuraOfNausea, EssenceOfDisease, EssenceOfEarth,
// ExplosiveGoo, AuraDamage and PoisonBreath (_Effects = new AreaEffect[7], :166-174; counted).
//
// AuraDamage is a twin (Chase's decision 1a): pinned has it as BaseCreature.HasAura with the Aura* virtuals and
// AuraDamage(), which already calls the creature's AuraEffect(Mobile) where ServUO calls IAuraCreature.AuraEffect. A
// creature with HasAura lists it as a natural ability and pinned's OnThink fires it (BaseCreature.cs:3937-3941); the
// profile dispatch skips it. Set on a creature without HasAura, it runs that same AuraDamage() every AuraInterval.
// ServUO's AuraDefinition table (:511-594: Cora, the fire and cold auras) is therefore not ported: in pinned each of
// those creatures states its aura through the Aura* virtuals, as batch 3's FireDaemon does.
//
// ServUO's ExplosiveGoo keeps its "doing the effect" flag on the shared instance, so one creature's goo holds every
// other's for a second; copied. FindValidTargets returns a list: pinned's GetMobilesInRange is a ref struct that an
// iterator cannot hold.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Engines.BuffIcons;
using Server.Items;
using Server.Spells;

namespace Server.Mobiles;

public abstract class AreaEffect
{
    public virtual int ManaCost => 20;
    public virtual int MaxRange => 3;
    public virtual double TriggerChance => 1.0;
    public virtual TimeSpan CooldownDuration => TimeSpan.FromSeconds(30);
    public virtual bool RequiresCombatant => true;

    public virtual int EffectRange => 5;

    /// <summary>ServUO's class name, the key the profile store saves by.</summary>
    public virtual string Name => GetType().Name;

    /// <summary>True when the creature has it by nature and pinned fires it; only AuraDamage can be.</summary>
    public virtual bool IsNatural(BaseCreature bc) => false;

    public static bool CheckThinkTrigger(BaseCreature bc)
    {
        var profile = PetTrainingHelper.GetAbilityProfile(bc);

        if (profile != null)
        {
            var effects = profile.EnumerateDispatchableAreaEffects().Where(a => !a.IsInCooldown(bc)).ToArray();

            if (effects.Length > 0)
            {
                return effects[Utility.Random(effects.Length)].Trigger(bc, bc.Combatant as Mobile);
            }
        }

        return false;
    }

    public virtual bool Trigger(BaseCreature creature, Mobile combatant)
    {
        if (CheckMana(creature) && Validate(creature, combatant) && TriggerChance >= Utility.RandomDouble())
        {
            creature.Mana -= ManaCost;

            DoEffects(creature, combatant);
            AddToCooldown(creature);
            return true;
        }

        return false;
    }

    public virtual bool Validate(BaseCreature attacker, Mobile defender)
    {
        if (!attacker.Alive || attacker.Deleted || attacker.IsDeadBondedPet || attacker.BardPacified)
        {
            return false;
        }

        return !RequiresCombatant || defender is { Alive: true, Deleted: false, IsDeadBondedPet: false } &&
            defender.InRange(attacker.Location, MaxRange) && defender.Map == attacker.Map && attacker.InLOS(defender);
    }

    public bool CheckMana(BaseCreature bc) => !bc.Controlled || bc.Mana >= ManaCost;

    public virtual void DoEffects(BaseCreature creature, Mobile combatant)
    {
        if (creature.Map == null || creature.Map == Map.Internal)
        {
            return;
        }

        var count = 0;

        foreach (var m in FindValidTargets(creature, EffectRange))
        {
            count++;
            DoEffect(creature, m);
        }

        if (count > 0)
        {
            OnAfterEffects(creature, combatant);
        }
    }

    public virtual void DoEffect(BaseCreature creature, Mobile defender)
    {
    }

    public virtual void OnAfterEffects(BaseCreature creature, Mobile defender)
    {
    }

    public static List<Mobile> FindValidTargets(BaseCreature creature, int range)
    {
        var list = new List<Mobile>();

        foreach (var m in creature.GetMobilesInRange(range))
        {
            if (ValidTarget(creature, m))
            {
                list.Add(m);
            }
        }

        return list;
    }

    public static bool ValidTarget(Mobile from, Mobile to) =>
        to != from && to.Alive && !to.IsDeadBondedPet &&
        from.CanBeHarmful(to, false) &&
        SpellHelper.ValidIndirectTarget(from, to) &&
        from.InLOS(to);

    public List<BaseCreature> _Cooldown;

    public bool IsInCooldown(BaseCreature m) => _Cooldown?.Contains(m) == true;

    public void AddToCooldown(BaseCreature bc)
    {
        var cooldown = GetCooldown(bc);

        if (cooldown != TimeSpan.MinValue)
        {
            _Cooldown ??= new List<BaseCreature>();
            _Cooldown.Add(bc);
            Timer.DelayCall(cooldown, RemoveFromCooldown, bc);
        }
    }

    public virtual TimeSpan GetCooldown(BaseCreature m) => CooldownDuration;

    public void RemoveFromCooldown(BaseCreature m) => _Cooldown?.Remove(m);

    private static readonly AreaEffect[] _effects =
    {
        new AuraOfEnergy(),     // 0
        new AuraOfNausea(),     // 1
        new EssenceOfDisease(), // 2
        new EssenceOfEarth(),   // 3
        new ExplosiveGoo(),     // 4
        new AuraDamageTwin(),   // 5
        new PoisonBreath()      // 6
    };

    public static AreaEffect[] Effects => _effects;

    public static AreaEffect GetByName(string name)
    {
        for (var i = 0; i < _effects.Length; i++)
        {
            if (_effects[i].Name == name)
            {
                return _effects[i];
            }
        }

        return null;
    }

    public static AreaEffect AuraOfEnergy => _effects[0];
    public static AreaEffect AuraOfNausea => _effects[1];
    public static AreaEffect EssenceOfDisease => _effects[2];
    public static AreaEffect EssenceOfEarth => _effects[3];
    public static AreaEffect ExplosiveGoo => _effects[4];
    public static AreaEffect AuraDamage => _effects[5];
    public static AreaEffect PoisonBreath => _effects[6];
}

public class AuraOfEnergy : AreaEffect
{
    public override void DoEffect(BaseCreature creature, Mobile defender)
    {
        AOS.Damage(defender, creature, Utility.RandomMinMax(20, 30), 0, 0, 0, 0, 100);

        defender.SendLocalizedMessage(1072073, false, creature.Name); //  : The creature's aura of energy is damaging you!

        creature.DoHarmful(defender);
        defender.FixedParticles(0x374A, 10, 30, 5052, 1278, 0, EffectLayer.Waist);
        defender.PlaySound(0x51D);
    }
}

public class AuraOfNausea : AreaEffect
{
    public override TimeSpan CooldownDuration => TimeSpan.FromSeconds(40 + Utility.RandomDouble() * 30);
    public override int MaxRange => 4;
    public override int EffectRange => 4;
    public override int ManaCost => 100;

    private static Dictionary<Mobile, Timer> _table;

    public override void DoEffect(BaseCreature creature, Mobile defender)
    {
        _table ??= new Dictionary<Mobile, Timer>();

        if (_table.TryGetValue(defender, out var timer))
        {
            timer?.Stop();
        }

        _table[defender] = Timer.DelayCall(TimeSpan.FromSeconds(30), EndNausea, defender);

        defender.Animate(32, 5, 1, true, false, 0); // bow animation
        defender.SendLocalizedMessage(1072068); // Your enemy's putrid presence envelops you, overwhelming you with nausea.

        (defender as PlayerMobile)?.AddBuff(
            new BuffInfo(BuffIcon.AuraOfNausea, 1153792, 1153819, TimeSpan.FromSeconds(30), "60\t60\t60\t5")
        );
    }

    public static void EndNausea(Mobile m)
    {
        if (_table?.Remove(m) == true)
        {
            (m as PlayerMobile)?.RemoveBuff(BuffIcon.AuraOfNausea);
            m.Delta(MobileDelta.WeaponDamage);

            if (_table.Count == 0)
            {
                _table = null;
            }
        }
    }

    /// <summary>Read by AosAttributes.GetValue (AOS-pet-ability-triggers.patch).</summary>
    public static bool UnderNausea(Mobile m) => _table?.ContainsKey(m) == true;
}

public class EssenceOfDisease : AreaEffect
{
    public override void DoEffect(BaseCreature creature, Mobile defender)
    {
        AOS.Damage(defender, creature, Utility.RandomMinMax(20, 30), 0, 0, 0, 100, 0);

        defender.SendLocalizedMessage(1072074, false, creature.Name);

        creature.DoHarmful(defender);
        defender.FixedParticles(0x374A, 10, 30, 5052, 1272, 0, EffectLayer.Waist);
        defender.PlaySound(0x476);
    }
}

public class EssenceOfEarth : AreaEffect
{
    public override void DoEffect(BaseCreature creature, Mobile defender)
    {
        AOS.Damage(defender, creature, Utility.RandomMinMax(20, 30), 100, 0, 0, 0, 0);

        defender.SendLocalizedMessage(1072075, false, creature.Name);

        creature.DoHarmful(defender);
        defender.FixedParticles(0x374A, 10, 30, 5052, 1836, 0, EffectLayer.Waist);
        defender.PlaySound(0x22C);
    }
}

public class ExplosiveGoo : AreaEffect
{
    public override int ManaCost => 30;

    private bool _doingEffect;

    public override void DoEffects(BaseCreature creature, Mobile combatant)
    {
        if (_doingEffect)
        {
            return;
        }

        Server.Effects.SendTargetParticles(creature, 0x3709, 10, 15, 2724, 0, 9907, EffectLayer.LeftFoot, 0);
        creature.PlaySound(0x348);

        _doingEffect = true;

        Timer.DelayCall(
            TimeSpan.FromSeconds(1.0),
            () =>
            {
                base.DoEffects(creature, combatant);
                _doingEffect = false;
            }
        );
    }

    public override void DoEffect(BaseCreature creature, Mobile defender)
    {
        Timer.DelayCall(
            TimeSpan.FromMilliseconds(Utility.RandomMinMax(10, 1000)),
            m =>
            {
                if (m.Alive && !m.Deleted && m.Map != null)
                {
                    var p = m.Location;

                    for (var x = -1; x <= 1; x++)
                    {
                        for (var y = -1; y <= 1; y++)
                        {
                            Server.Effects.SendLocationEffect(new Point3D(p.X + x, p.Y + y, p.Z), m.Map, 0x3728, 13, 1921, 3);
                        }
                    }

                    creature.DoHarmful(defender);
                    AOS.Damage(m, creature, Utility.RandomMinMax(30, 40), 0, 100, 0, 0, 0);
                    m.SendLocalizedMessage(1112366); // The flammable goo covering you bursts into flame!
                }
            },
            defender
        );
    }
}

public class PoisonBreath : AreaEffect
{
    public override double TriggerChance => 0.4;
    public override int EffectRange => 10;
    public override int ManaCost => 50;

    public override void DoEffect(BaseCreature creature, Mobile m)
    {
        m.ApplyPoison(creature, GetPoison(creature));

        Server.Effects.SendLocationParticles(
            EffectItem.Create(m.Location, m.Map, EffectItem.DefaultDuration),
            0x36B0, 1, 14, 63, 7, 9915, 0
        );

        Server.Effects.PlaySound(m.Location, m.Map, 0x229);
        var damage = GetDamage(creature);

        if (damage > 0)
        {
            creature.DoHarmful(m);
            AOS.Damage(m, creature, damage, 0, 0, 0, 100, 0);
        }
    }

    public override void OnAfterEffects(BaseCreature creature, Mobile defender)
    {
        if (creature.Controlled)
        {
            var profile = PetTrainingHelper.GetAbilityProfile(creature);

            if (profile?.HasAbility(MagicalAbility.Poisoning) == true || 0.2 > Utility.RandomDouble())
            {
                creature.CheckSkill(SkillName.Poisoning, 0, creature.Skills.Poisoning.Cap);
            }
        }
    }

    public static Poison GetPoison(BaseCreature bc)
    {
        var level = 0;
        var total = bc.Skills.Poisoning.Value;

        if (total >= 100)
        {
            level = 4;
        }
        else if (total > 60)
        {
            level = 3;
        }
        else if (total > 40)
        {
            level = 2;
        }
        else if (total > 20)
        {
            level = 1;
        }

        return Poison.GetPoison(level);
    }

    // ServUO compares types; by name here, since not all four are in this shard's build.
    private static readonly HashSet<string> _damageCreatures =
        new() { "ValoriteElemental", "BronzeElemental", "Dimetrosaur", "ChiefParoxysmus" };

    public static int GetDamage(BaseCreature bc) => _damageCreatures.Contains(bc.GetType().Name) ? 50 : 0;
}

public class AuraDamageTwin : AreaEffect
{
    public override string Name => "AuraDamage";
    public override double TriggerChance => 1.0;
    public override int ManaCost => 0;
    public override bool RequiresCombatant => false;

    public override bool IsNatural(BaseCreature bc) => bc.HasAura;

    // Pinned's interval, not ServUO's AuraDefinition cooldown.
    public override TimeSpan GetCooldown(BaseCreature bc) => bc.AuraInterval;

    public override void DoEffects(BaseCreature creature, Mobile combatant)
    {
        if (creature.Map == null || creature.Map == Map.Internal)
        {
            return;
        }

        creature.AuraDamage();
    }
}
