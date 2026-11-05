// cc-P795: Calm. A wild animal stops fighting, everyone and not only the drover, as peacemaking does, for 3 + Herding / 20 seconds
// (13 at 200). It uses the creature's pacify state, so hurting it can break the calm as it breaks a bard's. The same animal cannot
// be calmed again for 30 seconds from the calm that worked. That wait is in memory only: it is shorter than any restart.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Mobiles;

namespace Server.Spells.Drover;

public class CalmSpell : DroverSpell
{
    private static readonly SpellInfo _info = new("Calm", "", 212);
    private static readonly Dictionary<BaseCreature, DateTime> _calmAgain = new();

    public CalmSpell(Mobile caster, Item scroll = null) : base(caster, scroll, _info)
    {
    }

    public override int StaminaCost => DroverSettings.CalmStamina;

    public static TimeSpan DurationFor(Mobile caster) =>
        TimeSpan.FromSeconds(DroverSettings.CalmBase + caster.Skills.Herding.Value / DroverSettings.CalmDivisor);

    public static bool Waiting(BaseCreature bc) => _calmAgain.TryGetValue(bc, out var when) && Core.Now < when;

    protected override bool Refuses(BaseCreature bc)
    {
        if (bc.BardPacified)
        {
            Caster.SendMessage("That animal is already calm.");
            return true;
        }

        if (Waiting(bc))
        {
            Caster.SendMessage("That animal will not be calmed again so soon.");
            return true;
        }

        return false;
    }

    protected override void OnCreature(BaseCreature bc)
    {
        if (!Persuade(bc))
        {
            Caster.SendLocalizedMessage(502472); // You don't seem to be able to persuade that to move.
            return;
        }

        foreach (var old in _calmAgain.Where(e => e.Value <= Core.Now).Select(e => e.Key).ToList())
        {
            _calmAgain.Remove(old);
        }

        var duration = DurationFor(Caster);
        bc.Pacify(Caster, Core.Now + duration);
        bc.Warmode = false;
        _calmAgain[bc] = Core.Now.AddSeconds(DroverSettings.CalmCooldownSeconds);
        Caster.SendMessage(0x44, $"You calm {bc.Name} for {duration.TotalSeconds:0} seconds.");
    }
}
