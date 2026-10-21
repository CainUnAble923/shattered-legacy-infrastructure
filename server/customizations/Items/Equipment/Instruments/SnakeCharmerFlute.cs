// cc-P199: ServUO pub57 Scripts/Items/Equipment/Instruments/SnakeCharmerFlute.cs. A bamboo flute (pinned BambooFlute,
// hue 0x187, 50-80 uses) that charms a snake: "Target the serpent you wish to entice." within 12 tiles; a Snake,
// LavaSnake, CoralSnake (ours), GiantSerpent or SilverSerpent then takes a ground target within 10 tiles of the snake,
// walks there ("The animal walks where it was instructed to."), and the flute plays well, loses a use (breaking at
// zero: "You broke your snake charmer flute.") and is spent for 5 seconds. A snake someone else has charmed answers
// "Someone else is already taming this." The charm lasts 20 seconds (sparkles every 2 seconds), or until the charmer is
// more than 10 tiles away, and ends with "The charm seems to wear off." over the snake.
//
// ServUO keeps the charm on BaseCreature (Mobiles/Normal/BaseCreature.cs:3533-3599: CharmMaster, CharmTarget, a
// CharmTimer, BeginCharm/EndCharm) and walks the snake from BaseAI.Think (Mobiles/AI/BaseAI.cs:890 and :1420-1431,
// CheckCharming, ahead of fleeing and every action). Pinned has none of these members. They are unsaved, so this is not
// a saved-field subsystem; the port keeps them here instead:
// - The charm state (master, point, timer) lives in a static table in this file, keyed by the creature, with the same
//   20-second timer and end conditions as ServUO's CharmTimer. GetCharmMaster and EndCharm are public statics so a later
//   SerpentNest port (ServUO Items/Functional/SerpentNest.cs:116-168 reads CharmMaster and calls EndCharm) can use them.
// - The walking is pinned's herding: BaseCreature.TargetLocation (BaseCreature.cs:1108), the field the shepherd's crook
//   sets (ShepherdsCrook.cs:148), which pinned's AI walks toward in wander and, at AOS, in combat (BaseAI.cs:411, :479,
//   :653-686). So, unlike ServUO, a snake that is fleeing or in some other action does not walk; and the snake stops
//   when it arrives (pinned clears TargetLocation there) where ServUO keeps it on the spot until the charm ends.
//   Making it ServUO's exactly needs a hook in pinned BaseAI.Think (see the batch report).
// - ServUO's OnCraft takes ITool (SnakeCharmerFlute.cs:144); pinned's takes BaseTool. Same body.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SnakeCharmerFlute : BambooFlute
{
    private static readonly Type[] _snakeTypes =
    {
        typeof(LavaSnake), typeof(Snake),
        typeof(CoralSnake), typeof(GiantSerpent),
        typeof(SilverSerpent)
    };

    private static readonly Dictionary<BaseCreature, CharmTimer> _charms = new();

    [Constructible]
    public SnakeCharmerFlute() => Hue = 0x187;

    public override int LabelNumber => 1112174; // snake charmer flute

    public override int InitMinUses => 50;
    public override int InitMaxUses => 80;

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.CanBeginAction<SnakeCharmerFlute>())
        {
            from.SendLocalizedMessage(1072306); // You must wait a moment for it to recharge.
        }
        else
        {
            from.SendLocalizedMessage(1112175); // Target the serpent you wish to entice.
            from.Target = new CharmTarget(this);
        }
    }

    public override int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        base.OnCraft(quality, makersMark, from, craftSystem, typeRes, tool, craftItem, resHue);

        Hue = 0x187;

        return quality;
    }

    public static bool IsSnake(BaseCreature bc) => Array.IndexOf(_snakeTypes, bc.GetType()) >= 0;

    // ServUO BaseCreature.CharmMaster (BaseCreature.cs:3568).
    public static Mobile GetCharmMaster(BaseCreature bc) =>
        bc != null && _charms.TryGetValue(bc, out var timer) ? timer.Master : null;

    // ServUO BaseCreature.BeginCharm (BaseCreature.cs:3538-3545), the walk through pinned's herding field.
    public static void BeginCharm(BaseCreature bc, Mobile master, Point2D target)
    {
        if (_charms.Remove(bc, out var old))
        {
            old.Stop();
        }

        bc.TargetLocation = target;

        var timer = new CharmTimer(bc, master, target);
        _charms[bc] = timer;
        timer.Start();
    }

    // ServUO BaseCreature.EndCharm (BaseCreature.cs:3547-3565).
    public static void EndCharm(BaseCreature bc)
    {
        if (bc == null || !_charms.Remove(bc, out var timer))
        {
            return;
        }

        timer.Stop();

        if (bc.Deleted)
        {
            return;
        }

        // The charm seems to wear off.
        bc.PrivateOverheadMessage(MessageType.Regular, 0x3B2, 1112181, timer.Master.NetState);

        bc.Frozen = false;

        // Clear the walk only if it is still the charm's (a herder may have set another since).
        if (bc.TargetLocation is { } p && p.X == timer.Target.X && p.Y == timer.Target.Y)
        {
            bc.TargetLocation = null;
        }
    }

    private class CharmTarget : Target
    {
        private readonly SnakeCharmerFlute _flute;

        public CharmTarget(SnakeCharmerFlute flute) : base(12, false, TargetFlags.None) => _flute = flute;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is BaseCreature bc && IsSnake(bc))
            {
                if (GetCharmMaster(bc) != null)
                {
                    bc.PrivateOverheadMessage(MessageType.Regular, 0x3B2, 502802, from.NetState); // Someone else is already taming this.
                }
                else
                {
                    from.SendLocalizedMessage(502475); // Click where you wish the animal to go.
                    from.Target = new InternalTarget(bc, _flute);
                }
            }
            else
            {
                from.SendLocalizedMessage(1112176); // That is not a snake or serpent.
            }
        }
    }

    private class InternalTarget : Target
    {
        private readonly BaseCreature _snake;
        private readonly SnakeCharmerFlute _flute;

        public InternalTarget(BaseCreature snake, SnakeCharmerFlute flute) : base(10, true, TargetFlags.None)
        {
            _snake = snake;
            _flute = flute;
        }

        protected override void OnTarget(Mobile from, object targ)
        {
            if (targ is not IPoint2D ip)
            {
                return;
            }

            var p = new Point2D(ip);

            if (!_snake.InRange(p, 10))
            {
                from.SendLocalizedMessage(500643); // Target is too far away.
                return;
            }

            BeginCharm(_snake, from, p);

            from.SendLocalizedMessage(502479); // The animal walks where it was instructed to.

            from.BeginAction<SnakeCharmerFlute>();
            Timer.StartTimer(TimeSpan.FromSeconds(5.0), from.EndAction<SnakeCharmerFlute>);

            _flute.PlayInstrumentWell(from);
            _flute.UsesRemaining--;

            if (_flute.UsesRemaining == 0)
            {
                from.SendLocalizedMessage(1112177); // You broke your snake charmer flute.

                _flute.Delete();
            }
        }
    }

    // ServUO BaseCreature.CharmTimer (BaseCreature.cs:3573-3598): ten sparkles two seconds apart, the first at once,
    // then the charm ends; it ends early if the charmer is gone or more than 10 tiles from the snake.
    private class CharmTimer : Timer
    {
        private readonly BaseCreature _owner;
        private int _count;

        public CharmTimer(BaseCreature owner, Mobile master, Point2D target)
            : base(TimeSpan.Zero, TimeSpan.FromSeconds(2.0))
        {
            _owner = owner;
            Master = master;
            Target = target;
            _count = 10;
        }

        public Mobile Master { get; }
        public Point2D Target { get; }

        protected override void OnTick()
        {
            if (_count == 0 || Master == null || !Master.InRange(_owner.Location, 10))
            {
                Stop();
                EndCharm(_owner);
            }
            else
            {
                _owner.FixedParticles(0x376A, 9, 32, 5030, EffectLayer.Waist);
                _count--;
            }
        }
    }
}
