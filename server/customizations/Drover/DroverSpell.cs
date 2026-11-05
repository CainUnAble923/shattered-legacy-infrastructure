// cc-P795: the Drover's Handbook, as decided in shard-migration notes/skills-design.md (Animal, Chase 2026-10-02 16:24): a book of
// Herding abilities cast from the book, the [Corral command or a client macro, for a little stamina, no crook needed. The crook
// keeps working as OSI's command item.
//
// DroverSpell is the base of the abilities: a Spell on Herding with no mana and no reagents. Casting never fizzles or gains;
// the roll and the gain belong to the target, as the crook's do (Persuade).
//
// Gain: the crook's window, MinTameSkill - 30 to + 30 + 0..9, then (the shard's rule that a skill trains to its cap, cc-P66 hook
// G) a gain-only roll past it, up to MinTameSkill + clusterf.drover.gainTop (92). At 92 the hardest tame, the Dread Warhorse at
// 108, trains Herding to 200, a dragon to about 186, and a rabbit stops near 91. The gain still follows the creature, so the
// ladder creatures to come extend it by themselves.
//
// Structure after the druid-spell-system pack (Hammerhand): a book, its gump, registry IDs in a free range, a base spell on a
// non-Magery skill. Reference only, none of its code. Notes: notes/cc-P795-drovers-handbook.md.

using System;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Spells.Drover;

public abstract class DroverSpell : Spell
{
    protected DroverSpell(Mobile caster, Item scroll, SpellInfo info) : base(caster, scroll, info)
    {
    }

    /// <summary>pinned's limit on a herded walk: BaseAI.CheckHerding drops any walk over 15 tiles, by straight-line distance.</summary>
    public const double WalkLimit = 15.0;

    public abstract int StaminaCost { get; }
    public virtual int TargetRange => DroverSettings.Range;
    public virtual bool TargetNeedsLineOfSight => true;

    public override SkillName CastSkill => SkillName.Herding;
    public override SkillName DamageSkill => SkillName.Herding;
    public override bool ClearHandsOnCast => false;
    public override TimeSpan CastDelayBase => TimeSpan.FromSeconds(DroverSettings.CastDelaySeconds);

    public override int GetMana() => 0;

    public override bool CheckCast()
    {
        if (!base.CheckCast())
        {
            return false;
        }

        if (Caster.Stam < StaminaCost)
        {
            Caster.SendMessage($"You need {StaminaCost} stamina to do that.");
            return false;
        }

        return true;
    }

    // Reached through CheckSequence, once the target is chosen: the stamina goes here, and the cast itself cannot fail.
    public override bool CheckFizzle()
    {
        if (Caster.Stam < StaminaCost)
        {
            Caster.SendMessage($"You need {StaminaCost} stamina to do that.");
            return false;
        }

        Caster.Stam -= StaminaCost;
        return true;
    }

    /// <summary>A wild animal the crook could herd: tamable, not a paragon, and no one's pet.</summary>
    public static bool IsWildAnimal(BaseCreature bc) => bc is { Tamable: true, IsParagon: false, Controlled: false, Deleted: false };

    /// <summary>The roll on a creature: the crook's own, then the gain-only roll past the crook's window.</summary>
    public bool Persuade(BaseCreature bc)
    {
        var min = bc.MinTameSkill - 30;
        var max = bc.MinTameSkill + 30 + Utility.Random(10);
        var persuaded = Caster.CheckTargetSkill(SkillName.Herding, bc, min, max);

        if (Caster.Skills.Herding.Value >= max)
        {
            Caster.CheckTargetSkill(SkillName.Herding, bc, max, bc.MinTameSkill + DroverSettings.GainTop);
        }

        return persuaded;
    }

    /// <summary>The ability, on a creature that passed IsWildAnimal, after the stamina is paid.</summary>
    protected abstract void OnCreature(BaseCreature bc);

    /// <summary>The ability's own refusal, which tells the caster why. Reached before the stamina is paid.</summary>
    protected virtual bool Refuses(BaseCreature bc) => false;

    /// <summary>After the refusals: pay and act. Herd asks for a spot first.</summary>
    protected virtual void Begin(BaseCreature bc)
    {
        if (CheckSequence())
        {
            OnCreature(bc);
        }
    }

    public override void OnCast() => Caster.Target = new AnimalTarget(this);

    private void Target(BaseCreature bc)
    {
        if (!IsWildAnimal(bc))
        {
            Caster.SendLocalizedMessage(502468); // That is not a herdable animal.
        }
        else if (!Refuses(bc))
        {
            Begin(bc);
        }
    }

    private class AnimalTarget : Target
    {
        private readonly DroverSpell _spell;

        public AnimalTarget(DroverSpell spell) : base(spell.TargetRange, false, TargetFlags.None)
        {
            _spell = spell;
            CheckLOS = spell.TargetNeedsLineOfSight;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is BaseCreature bc)
            {
                _spell.Target(bc);
            }
            else
            {
                from.SendLocalizedMessage(502468); // That is not a herdable animal.
            }
        }

        // Herd's second target keeps the cast going.
        protected override void OnTargetFinish(Mobile from)
        {
            if (from.Target == null)
            {
                _spell.FinishSequence();
            }
        }
    }
}
