// cc-P795: Herd. The wild animals within Herding / 25 tiles of the one chosen (8 at 200, 1 at least), up to Herding / 20 of them
// (10 at 200, 1 at least, the nearest first), each roll as the crook does and walk to a spot the drover picks, up to 10 tiles from
// the drover. The stamina goes when the spot is chosen, once. An animal further than the engine walks (WalkLimit) from the spot is
// left out before it rolls, as a walk it could never start would only eat a gain and the message.

using System;
using System.Collections.Generic;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Spells.Drover;

public class HerdSpell : DroverSpell
{
    private static readonly SpellInfo _info = new("Herd", "", 212);

    private BaseCreature _lead;

    public HerdSpell(Mobile caster, Item scroll = null) : base(caster, scroll, _info)
    {
    }

    public override int StaminaCost => DroverSettings.HerdStamina;

    public static int RadiusFor(Mobile caster) => Math.Max(1, (int)(caster.Skills.Herding.Value / DroverSettings.HerdRadiusDivisor));

    public static int CountFor(Mobile caster) => Math.Max(1, (int)(caster.Skills.Herding.Value / DroverSettings.HerdCountDivisor));

    /// <summary>Who would walk: nearest the chosen animal first, within the drover's radius and count, and within a walk of the spot.</summary>
    public static List<BaseCreature> Flock(Mobile caster, BaseCreature lead, IPoint2D spot)
    {
        var flock = new List<BaseCreature>();

        foreach (var bc in lead.Map.GetMobilesInRange<BaseCreature>(lead.Location, RadiusFor(caster)))
        {
            if (IsWildAnimal(bc) && caster.CanSee(bc) && bc.GetDistanceToSqrt(spot) <= WalkLimit)
            {
                flock.Add(bc);
            }
        }

        flock.Sort((a, b) => lead.GetDistanceToSqrt(a).CompareTo(lead.GetDistanceToSqrt(b)));

        var count = CountFor(caster);

        if (flock.Count > count)
        {
            flock.RemoveRange(count, flock.Count - count);
        }

        return flock;
    }

    protected override void Begin(BaseCreature bc)
    {
        _lead = bc;
        Caster.SendLocalizedMessage(502475); // Click where you wish the animal to go.
        Caster.Target = new SpotTarget(this);
    }

    // The ability is the spot; nothing reaches here.
    protected override void OnCreature(BaseCreature bc)
    {
    }

    private void Spot(Point2D spot)
    {
        if (_lead.Deleted || _lead.Map != Caster.Map)
        {
            Caster.SendLocalizedMessage(502468); // That is not a herdable animal.
            return;
        }

        var flock = Flock(Caster, _lead, spot);

        if (flock.Count == 0)
        {
            Caster.SendMessage("Those animals are too far from that spot.");
            return;
        }

        if (!CheckSequence())
        {
            return;
        }

        var walking = 0;

        foreach (var bc in flock)
        {
            if (Persuade(bc))
            {
                bc.TargetLocation = spot;
                walking++;
            }
        }

        if (walking == 0)
        {
            Caster.SendLocalizedMessage(502472); // You don't seem to be able to persuade that to move.
        }
        else
        {
            Caster.SendMessage(0x44, $"{walking} of {flock.Count} walk where you pointed.");
        }
    }

    private class SpotTarget : Target
    {
        private readonly HerdSpell _spell;

        public SpotTarget(HerdSpell spell) : base(DroverSettings.Range, true, TargetFlags.None) => _spell = spell;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is IPoint2D p)
            {
                _spell.Spot(new Point2D(p));
            }
        }

        protected override void OnTargetFinish(Mobile from) => _spell.FinishSequence();
    }
}
