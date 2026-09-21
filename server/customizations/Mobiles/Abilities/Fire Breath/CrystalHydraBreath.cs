// ServUO: Services/Pet Training/SpecialAbility.cs:1045-1063, the Crystal Hydra's own DragonBreathDefinition (CC6
// follow-up, Q-054). ServUO chooses a creature's breath by type (GetDefinition, :1065): the default is fire, and
// CrystalHydra is the one type in the table with a definition of its own. Read against the ctor at :1085-1122:
//
//   scalar 0.13 (default 0.16), 100% cold (default 100% fire), 5.0-7.0 s between breaths (default 30-45 s),
//   effect hue 0x47E (the hydra's own hue; the stock cold breath is 0x480), effect sound 0x56D (default 0x227),
//   item 0x36D4, anger animation 12, stall 1.0 s, effect delay 1.3 s, damage delay 1.0 s as the default,
//   and attacksMultiples = true: one trigger breathes at the combatant AND at up to four more mobiles within
//   5 tiles of it (SpellHelper.AcquireIndirectTargets(creature, target, map, 5), then InRange(creature, 12),
//   five draws with the first fixed on the target, :742-756).
//
// Pinned ModernUO has the mechanism (Mobiles/Abilities/Fire Breath/FireBreath.cs, ColdBreath.cs) and every default
// number, so this is ColdBreath with the five values above overridden and the fan-out added. FireBreath.Trigger
// handles the first breath exactly as stock (stall, anger sound, animation, facing, the 1.3 s effect timer, and the
// cooldown through MonsterAbility.Trigger); this class schedules the four secondary breaths beside it.
//
// Not reproduced, the same two differences every breath on this shard carries (CC4 Shame, batch 5 §2): ServUO
// rolls the breath on think at 10% per think off cooldown and charges 30 mana, pinned rolls it on a combat action
// at 50% with no mana; and the range is 12 there and RangePerception here. ServUO's SA-era animation
// (AnimationType.Pillage) is likewise pinned's legacy action 12 for every breath.

using System;
using System.Collections.Generic;
using Server.Spells;

namespace Server.Mobiles;

public class CrystalHydraBreath : ColdBreath
{
    // ServUO: five breaths per trigger, the first at the combatant, from mobiles within 5 tiles of it.
    public const int SecondaryBreaths = 4;
    public const int SecondaryRange = 5;

    public override double BreathDamageScalar => 0.13;

    public override TimeSpan MinTriggerCooldown => TimeSpan.FromSeconds(5.0);
    public override TimeSpan MaxTriggerCooldown => TimeSpan.FromSeconds(7.0);

    public override int BreathEffectHue => 0x47E;
    public override int BreathEffectSound => 0x56D;

    public override void Trigger(MonsterAbilityTrigger trigger, BaseCreature source, Mobile target)
    {
        if (!CanFireBreathTarget(source, target))
        {
            return;
        }

        // Chosen before the first breath is scheduled, as ServUO's DoBreath does; the list excludes the target.
        var others = AcquireSecondaryTargets(source, target);

        base.Trigger(trigger, source, target);

        var delay = TimeSpan.FromSeconds(BreathEffectDelay);

        for (var i = 0; i < SecondaryBreaths && others.Count > 0; i++)
        {
            var index = Utility.Random(others.Count);
            var m = others[index];
            others.RemoveAt(index);

            Timer.StartTimer(delay, () => BreathEffect_Callback(source, m));
        }
    }

    // ServUO: SpellHelper.AcquireIndirectTargets(creature, target, map, 5) (Spells/Base/SpellHelper.cs:638): every
    // damageable within 5 tiles of the target, not the creature, alive, in the creature's LOS, CanBeHarmful and a
    // valid indirect target; then .Where(m => m.InRange(creature.Location, MaxRange)).
    public List<Mobile> AcquireSecondaryTargets(BaseCreature source, Mobile target)
    {
        var list = new List<Mobile>();
        var map = source.Map;

        if (map == null || map == Map.Internal)
        {
            return list;
        }

        var range = BreathRange(source);

        foreach (var m in map.GetMobilesInRange(target.Location, SecondaryRange))
        {
            if (m == source || m == target || !m.Alive || m.IsDeadBondedPet)
            {
                continue;
            }

            if (!source.InLOS(m) || !source.CanBeHarmful(m, false) || !SpellHelper.ValidIndirectTarget(source, m))
            {
                continue;
            }

            if (!m.InRange(source.Location, range))
            {
                continue;
            }

            list.Add(m);
        }

        return list;
    }
}
