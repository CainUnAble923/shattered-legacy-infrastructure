// cc-P121: the entry points the Pet Training ability core is called from. Each upstream patch adds one call here, in the
// place ServUO makes the same call, so the patches stay one or two lines each and the logic lives in customizations.
//
//   BaseCreature-pet-ability-hooks.patch  OnThink (ServUO BaseCreature.cs:7416-7420), OnMovement (:4914),
//                                         OnGaveMeleeAttack: hit poison (:3925-3961, 3972-4016) and the rage hit (:3966)
//   AOS-pet-ability-triggers.patch        AOS.Damage: the combat trigger (ServUO Misc/AOS.cs:325-329);
//                                         AosAttributes.GetValue: the debuffs' attribute penalties (:701-817)
//   SpellHelper-pet-ability-hooks.patch   spell damage is marked as spell damage; heals halve under Searing Wounds
//   Bandage-searing-wounds.patch          bandage heals halve under Searing Wounds (ServUO Bandage.cs:548-552)
//   MageAI-pet-magery-mastery.patch       SmartAI for Magery Mastery (ServUO MageAI.cs:40-43)
//   RegenRates.cs (ours, a replacement)   the profile's regeneration bonuses (ServUO BaseCreature.cs:392-427)
//
// ServUO's AOS.Damage takes a DamageType and every caller but SpellHelper (Spell) and BaseWeapon (Melee/Ranged) leaves it
// at its Melee default. Pinned's AOS.Damage has no such parameter, so SpellHelper.Damage marks the pair it is about to
// damage and AOS.Damage takes the mark on entry (TakeDamageType): the same pair is spell damage, anything else is melee,
// as in ServUO. The mark is taken before AOS.Damage's early returns, so it never outlives the call it was set for.

using System;
using Server.Items;

namespace Server.Mobiles;

public static class PetTrainingHooks
{
    [ThreadStatic]
    private static Mobile _spellFrom;

    [ThreadStatic]
    private static Mobile _spellTo;

    // -- BaseCreature ------------------------------------------------------------------------------------------------

    public static void OnThink(BaseCreature bc)
    {
        // ServUO returns from OnThink first when the creature is paralyzed or frozen; pinned's OnThink does not.
        if (bc.Paralyzed || bc.Frozen || bc.Summoned || !AbilityProfileStore.TryGet(bc, out _))
        {
            return;
        }

        SpecialAbility.CheckThinkTrigger(bc);
        AreaEffect.CheckThinkTrigger(bc);
    }

    public static void OnMovement(BaseCreature bc, Mobile m, Point3D oldLocation) =>
        SpecialAbility.CheckApproachTrigger(bc, m, oldLocation);

    public static void OnGaveMeleeAttack(BaseCreature bc, Mobile defender)
    {
        // ServUO BaseCreature.DoRageHit (:7032-7041): a creature in a colossal rage lands a colossal blow a third of the time.
        if (ColossalRage.HasRage(bc) && 0.33 >= Utility.RandomDouble() && defender is { Alive: true })
        {
            var damage = 0;
            SpecialAbility.ColossalBlow.DoEffects(bc, defender, ref damage);
        }
    }

    /// <summary>ServUO BaseCreature.GetHitPoison (:3971-3999): a trained poisoner's poison rises with its skill.</summary>
    public static Poison GetHitPoison(BaseCreature bc)
    {
        if (!PetTrainingHelper.Enabled || !bc.Controlled)
        {
            return bc.HitPoison;
        }

        var current = bc.HitPoison?.Level ?? 0;

        if (current >= 4 || !AbilityProfile.HasMagicalAbility(bc, MagicalAbility.Poisoning))
        {
            return bc.HitPoison;
        }

        var level = 1;
        var total = bc.Skills.Poisoning.Value;

        // natural poisoner retains their poison level. Added spell school is capped at level 2.
        if (total >= 100)
        {
            level = 4;
        }
        else if (total > 85)
        {
            level = 3;
        }
        else if (total > 65)
        {
            level = 2;
        }
        else if (total > 35)
        {
            level = 1;
        }

        return Poison.GetPoison(Math.Max(current, level));
    }

    /// <summary>ServUO BaseCreature.TryHitPoison (:4003-4014): a tame poisons by its skill, not HitPoisonChance.</summary>
    public static bool TryHitPoison(BaseCreature bc)
    {
        if (!PetTrainingHelper.Enabled || !bc.Controlled)
        {
            return bc.HitPoisonChance >= Utility.RandomDouble();
        }

        return AbilityProfile.HasMagicalAbility(bc, MagicalAbility.Poisoning) &&
               bc.Skills.Poisoning.Value >= Utility.Random(300);
    }

    /// <summary>ServUO (:3951-3957): a tame gains Poisoning on a poisoned hit only with the Poisoning school.</summary>
    public static bool GainsPoisoning(BaseCreature bc) =>
        !PetTrainingHelper.Enabled || AbilityProfile.HasMagicalAbility(bc, MagicalAbility.Poisoning);

    // -- AOS ---------------------------------------------------------------------------------------------------------

    public static void MarkSpellDamage(Mobile from, Mobile to)
    {
        _spellFrom = from;
        _spellTo = to;
    }

    /// <summary>Called first thing in AOS.Damage: spell damage when SpellHelper marked this pair, otherwise melee.</summary>
    public static AbilityDamageType TakeDamageType(Mobile from, Mobile to)
    {
        var spell = from != null && _spellFrom == from && _spellTo == to;
        _spellFrom = null;
        _spellTo = null;
        return spell ? AbilityDamageType.Spell : AbilityDamageType.Melee;
    }

    public static void OnDamage(Mobile from, Mobile m, ref int totalDamage, AbilityDamageType type)
    {
        if (from is BaseCreature || m is BaseCreature)
        {
            SpecialAbility.CheckCombatTrigger(from, m, ref totalDamage, type);
        }
    }

    /// <summary>The debuffs' penalties, where ServUO's AosAttributes.GetValue applies them (Misc/AOS.cs:699-817).</summary>
    public static int AttributeModifier(Mobile m, AosAttribute attribute)
    {
        switch (attribute)
        {
            case AosAttribute.CastSpeed:
                return HowlOfCacophony.IsUnderEffects(m) || AuraOfNausea.UnderNausea(m) ? -5 : 0;
            case AosAttribute.CastRecovery:
                return HowlOfCacophony.IsUnderEffects(m) ? -5 : 0;
            case AosAttribute.WeaponSpeed:
                {
                    var value = 0;

                    if (HowlOfCacophony.IsUnderEffects(m) || AuraOfNausea.UnderNausea(m))
                    {
                        value -= 60;
                    }

                    if (StickySkin.IsUnderEffects(m))
                    {
                        value -= 30;
                    }

                    return value;
                }
            case AosAttribute.AttackChance:
            case AosAttribute.DefendChance:
                return AuraOfNausea.UnderNausea(m) ? -60 : 0;
            default:
                return 0;
        }
    }

    // -- Healing -----------------------------------------------------------------------------------------------------

    /// <summary>ServUO Bandage.cs:548-552 and SpellHelper.Heal (:1516-1520): Searing Wounds halves the heal.</summary>
    public static double SearingWoundsHeal(Mobile patient, double toHeal)
    {
        if (!SearingWounds.IsUnderEffects(patient))
        {
            return toHeal;
        }

        patient.SendLocalizedMessage(1151178); // The cauterized wound resists some of your healing.
        return toHeal / 2;
    }

    // -- Regeneration ------------------------------------------------------------------------------------------------

    /// <summary>The profile's regeneration bonuses, ServUO BaseCreature.DefaultHitsRegen/StamRegen/ManaRegen (:392-427).</summary>
    public static int RegenHits(Mobile m) =>
        m is BaseCreature bc && AbilityProfileStore.TryGet(bc, out var p) ? p.RegenHits : 0;

    public static int RegenStam(Mobile m) =>
        m is BaseCreature bc && AbilityProfileStore.TryGet(bc, out var p) ? p.RegenStam : 0;

    public static int RegenMana(Mobile m) =>
        m is BaseCreature bc && AbilityProfileStore.TryGet(bc, out var p) ? p.RegenMana : 0;
}
