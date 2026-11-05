// cc-P795: Lure. One wild animal up to 15 tiles away, seen or not, walks to a free tile beside the drover and stops there: aiming at
// the drover's own tile would leave it pushing against the drover, still herded. Target ranges are square and the engine's walk
// limit is straight-line, so a far corner of the square is refused before the stamina goes.

using Server.Mobiles;

namespace Server.Spells.Drover;

public class LureSpell : DroverSpell
{
    private static readonly SpellInfo _info = new("Lure", "", 212);

    public LureSpell(Mobile caster, Item scroll = null) : base(caster, scroll, _info)
    {
    }

    public override int StaminaCost => DroverSettings.LureStamina;
    public override int TargetRange => DroverSettings.LureRange;
    public override bool TargetNeedsLineOfSight => false;

    /// <summary>The tile beside the drover on the animal's side, or the next free one round; the drover's own if all are blocked.</summary>
    public static Point2D SpotBeside(Mobile drover, Mobile bc)
    {
        var first = (int)drover.GetDirectionTo(bc) & 7;

        for (var i = 0; i < 8; i++)
        {
            var x = drover.X;
            var y = drover.Y;
            Movement.Movement.Offset((Direction)((first + i) & 7), ref x, ref y);

            if (drover.Map.CanFit(x, y, drover.Z, 16))
            {
                return new Point2D(x, y);
            }
        }

        return new Point2D(drover.X, drover.Y);
    }

    protected override bool Refuses(BaseCreature bc)
    {
        if (Caster.GetDistanceToSqrt(bc) > WalkLimit)
        {
            Caster.SendMessage("That animal is too far away to lure.");
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

        bc.TargetLocation = SpotBeside(Caster, bc);
        Caster.SendLocalizedMessage(502479); // The animal walks where it was instructed to.
    }
}
