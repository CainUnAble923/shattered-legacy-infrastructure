// cc-P795: Corral. The target's tame difficulty drops by Herding / 8 (25 at 200) for the caster's next attempt within 60 seconds
// (notes/skills-design.md, Chase 2026-10-02; the numbers stand). One corral per tamer at a time, the newest. Nothing here is
// saved: it lasts a minute, and a restart ends it.
//
// AnimalTaming-drover-corral.patch reads it twice: Bonus at the "no chance" gate, which does not use it up, and Spend at the
// taming roll, which does. A taming takes some seconds, so the roll may land a little after the minute
// (clusterf.drover.corralAttemptGraceSeconds). The anger chance still reads the creature's own difficulty (ClusterFTamingAnger).

using System;
using System.Collections.Generic;
using Server.Mobiles;

namespace Server.Spells.Drover;

public static class DroverCorral
{
    private readonly record struct Corral(BaseCreature Creature, double Amount, DateTime Until);

    private static readonly Dictionary<Mobile, Corral> _corrals = new();

    public static double AmountFor(Mobile caster) => caster.Skills.Herding.Value / DroverSettings.CorralDivisor;

    public static void Set(Mobile tamer, BaseCreature creature, double amount, DateTime until) =>
        _corrals[tamer] = new Corral(creature, amount, until);

    /// <summary>What the corral takes off this creature for this tamer now; 0 without one. Does not use it up.</summary>
    public static double Bonus(Mobile tamer, BaseCreature creature) =>
        _corrals.TryGetValue(tamer, out var c) && c.Creature == creature && Core.Now <= c.Until ? c.Amount : 0.0;

    /// <summary>The taming roll: what the corral takes off, and it is used up, whichever creature it was on.</summary>
    public static double Spend(Mobile tamer, BaseCreature creature)
    {
        if (!_corrals.Remove(tamer, out var c))
        {
            return 0.0;
        }

        return c.Creature == creature && Core.Now <= c.Until.AddSeconds(DroverSettings.CorralAttemptGraceSeconds) ? c.Amount : 0.0;
    }
}

public class CorralSpell : DroverSpell
{
    private static readonly SpellInfo _info = new("Corral", "", 212);

    public CorralSpell(Mobile caster, Item scroll = null) : base(caster, scroll, _info)
    {
    }

    public override int StaminaCost => DroverSettings.CorralStamina;

    protected override void OnCreature(BaseCreature bc)
    {
        if (!Persuade(bc))
        {
            Caster.SendLocalizedMessage(502472); // You don't seem to be able to persuade that to move.
            return;
        }

        var amount = DroverCorral.AmountFor(Caster);
        DroverCorral.Set(Caster, bc, amount, Core.Now.AddSeconds(DroverSettings.CorralSeconds));
        Caster.SendMessage(0x44, $"You corral {bc.Name}: {amount:0.0} easier for your next taming attempt, within {DroverSettings.CorralSeconds} seconds.");
    }
}
