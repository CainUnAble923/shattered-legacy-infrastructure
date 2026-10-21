// cc-P199: ServUO pub57 Scripts/Items/Functional/ScaleCollar.cs. A tinkered collar (art 4235, hue 2125) for catching a
// battle chicken lizard: from the pack it asks "Which battle chicken do you wish to ensnare?", and an untamed
// BattleChickenLizard (ours, Mobiles/Normal/BattleChickenLizard.cs) is caught with chance 50 / distance percent
// (50% adjacent, 25% at two tiles, under 1% past fifty). A caught lizard is frozen for 30 seconds with a sparkle every
// second, the window in which to tame it; it is freed at once if tamed, otherwise after 30 seconds with "The chicken
// frees itself of the collar!!" to the thrower. A miss says the chicken avoids it. The collar is not used up. While a
// catch is running the collar says "You must wait before trying again."
//
// It sets only Mobile.Frozen, which pinned has (Mobile.cs:617) and which is not a saved BaseCreature field, so there is
// no subsystem here. ServUO crafts it in Tinkering (DefTinkering.cs:455: 50-100 skill, 4 red scales and 1 scourge);
// that registration is not made here (see the batch report).
//
// One difference from ServUO: ServUO creates the 30-second timer (ScaleCollar.cs:50) and never starts it, so in ServUO a
// caught lizard stays frozen until a restart and the collar refuses every later use. The timer is started here, which
// is what ScaleCollar.cs:64-89 and its messages are written for. DateTime.UtcNow is Core.Now.

using System;
using ModernUO.Serialization;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ScaleCollar : Item
{
    private Timer _timer;

    [Constructible]
    public ScaleCollar() : base(4235) => Hue = 2125;

    public override int LabelNumber => 1112480; // a scale collar

    public override void OnDoubleClick(Mobile from)
    {
        if (IsChildOf(from.Backpack) && _timer == null)
        {
            from.Target = new InternalTarget(this);
            from.SendLocalizedMessage(1112481); // Which battle chicken do you wish to ensnare?
        }
        else if (IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(501789); // You must wait before trying again.
        }
        else
        {
            from.SendLocalizedMessage(1042004); // That must be in your pack for you to use it.
        }
    }

    public void OnTarget(Mobile from, object targeted)
    {
        if (targeted is not BattleChickenLizard { Controlled: false } bcl)
        {
            return;
        }

        var chance = 50 / (int)Math.Max(1, from.GetDistanceToSqrt(bcl.Location));

        if (chance > Utility.Random(100))
        {
            bcl.Frozen = true;

            _timer = new InternalTimer(this, bcl, from);
            _timer.Start();
            from.SendLocalizedMessage(1112484); // You successfully ensnare the chicken! You best hurry before it frees itself from it!
        }
        else
        {
            from.SendLocalizedMessage(1112483); // The collar falls to the ground as the chicken deftly avoids it.
        }
    }

    public void OnTick(BaseCreature lizard, Mobile owner)
    {
        if (lizard?.Controlled == true)
        {
            lizard.Frozen = false;

            _timer?.Stop();
            _timer = null;
        }
        else
        {
            lizard?.FixedEffect(0x376A, 1, 32);
        }
    }

    public void EndTimer(BaseCreature lizard, Mobile owner)
    {
        if (lizard?.Alive == true)
        {
            lizard.Frozen = false;

            if (owner != null && !lizard.Controlled)
            {
                owner.SendLocalizedMessage(1112482); // The chicken frees itself of the collar!!
            }
        }

        _timer?.Stop();
        _timer = null;
    }

    private class InternalTarget : Target
    {
        private readonly ScaleCollar _collar;

        public InternalTarget(ScaleCollar collar) : base(-1, false, TargetFlags.None) => _collar = collar;

        protected override void OnTarget(Mobile from, object targeted)
        {
            _collar?.OnTarget(from, targeted);
        }
    }

    private class InternalTimer : Timer
    {
        private readonly ScaleCollar _collar;
        private readonly BattleChickenLizard _lizard;
        private readonly DateTime _endTime;
        private readonly Mobile _owner;

        public InternalTimer(ScaleCollar collar, BattleChickenLizard lizard, Mobile owner)
            : base(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))
        {
            _collar = collar;
            _lizard = lizard;
            _owner = owner;
            _endTime = Core.Now + TimeSpan.FromSeconds(30);

            lizard.FixedEffect(0x376A, 1, 32);
        }

        protected override void OnTick()
        {
            if (_endTime < Core.Now)
            {
                _collar.EndTimer(_lizard, _owner);
            }
            else
            {
                _collar.OnTick(_lizard, _owner);
            }
        }
    }
}
