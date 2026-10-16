// cc-P128: ServUO pub57 Scripts/Items/Consumables/BaseExplodingTarPotion.cs -> ours. A thrown tar potion: after a
// one-second flight it bursts in a circle of Radius and everyone inside but the thrower is slowed to a walk for a
// minute (ServUO's SendSpeedControl(WalkSpeed); pinned's is a NetState packet, so a creature with no client is not
// slowed, as in ServUO where the packet goes nowhere either). One throw a minute per thrower.
//
// Shape and the delay table follow pinned's own BaseConfusionBlastPotion (the file ServUO's was copied from);
// PotionEffect.ExplodingTarPotion is server/patches/BasePotion-sa-potion-effects.patch.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Spells;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public abstract partial class BaseExplodingTarPotion : BasePotion
{
    private static readonly Dictionary<Mobile, TimerExecutionToken> _delay = new();
    private HashSet<Mobile> _users;

    public BaseExplodingTarPotion(PotionEffect effect) : base(0xF06, effect) => Hue = 1109;

    public abstract int Radius { get; }

    public override bool RequireFreeHand => false;

    public override bool IsThrowablePotion => true;

    public override bool CanDrink(Mobile from)
    {
        if (!base.CanDrink(from))
        {
            return false;
        }

        if (Core.AOS && (from.Paralyzed || from.Frozen || from.Spell?.IsCasting == true))
        {
            from.SendLocalizedMessage(1062725); // You can not use that potion while paralyzed.
            return false;
        }

        var delay = GetDelay(from);

        if (delay > 0)
        {
            // You cannot use that for another ~1_NUM~ ~2_TIMEUNITS~
            from.SendLocalizedMessage(1072529, $"{delay}\t{(delay > 1 ? "seconds." : "second.")}");
            return false;
        }

        return (from.Target as ThrowTarget)?.Potion != this;
    }

    public override void Drink(Mobile from)
    {
        from.RevealingAction();

        _users ??= [];
        _users.Add(from);

        from.Target = new ThrowTarget(this);
    }

    public virtual void Explode(Mobile from, Point3D loc, Map map)
    {
        if (Deleted || map == null)
        {
            return;
        }

        Consume();

        if (_users is { Count: > 0 })
        {
            using var usersQueue = PooledRefQueue<Mobile>.Create();
            foreach (var user in _users)
            {
                if ((user.Target as ThrowTarget)?.Potion == this)
                {
                    usersQueue.Enqueue(user);
                }
            }

            _users.Clear();

            while (usersQueue.Count > 0)
            {
                Target.Cancel(usersQueue.Dequeue());
            }
        }

        // Effects
        Effects.PlaySound(loc, map, 0x207);

        Geometry.Circle2D(loc, map, Radius, TarEffect, 270, 90);

        Timer.StartTimer(TimeSpan.FromSeconds(1), () => CircleEffect2(loc, map));

        foreach (var mobile in map.GetMobilesInRange(loc, Radius))
        {
            if (mobile == from)
            {
                continue;
            }

            if (mobile is PlayerMobile player)
            {
                player.SendLocalizedMessage(1095151); // * The tar potion slows you down! *
            }

            var ns = mobile.NetState;
            ns?.SendSpeedControl(SpeedControlSetting.Walk);

            Timer.StartTimer(TimeSpan.FromMinutes(1.0), () => mobile.NetState?.SendSpeedControl(SpeedControlSetting.Disable));
        }
    }

    public virtual void TarEffect(Point3D p, Map map)
    {
        if (map.CanFit(p, 12, true, false))
        {
            Effects.SendLocationEffect(p, map, 0x376A, 4, 9);
        }
    }

    public void CircleEffect2(Point3D p, Map m)
    {
        Geometry.Circle2D(p, m, Radius, TarEffect, 90, 270);
    }

    public static void AddDelay(Mobile m)
    {
        _delay.TryGetValue(m, out var timer);
        timer.Cancel();

        Timer.StartTimer(TimeSpan.FromSeconds(60), () => EndDelay(m), out timer);
        _delay[m] = timer;
    }

    public static int GetDelay(Mobile m)
    {
        if (_delay.TryGetValue(m, out var timer) && timer.Next > Core.Now)
        {
            return (int)Math.Round((timer.Next - Core.Now).TotalSeconds);
        }

        return 0;
    }

    public static void EndDelay(Mobile m)
    {
        if (_delay.Remove(m, out var timer))
        {
            timer.Cancel();
        }
    }

    private class ThrowTarget : Target
    {
        public ThrowTarget(BaseExplodingTarPotion potion) : base(12, true, TargetFlags.None) => Potion = potion;

        public BaseExplodingTarPotion Potion { get; }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (Potion.Deleted || Potion.Map == Map.Internal)
            {
                return;
            }

            if (targeted is not IPoint3D p || from.Map == null)
            {
                return;
            }

            // Add delay
            AddDelay(from);

            SpellHelper.GetSurfaceTop(ref p);
            var loc = new Point3D(p);
            var map = from.Map;

            from.RevealingAction();

            IEntity to;

            if (p is Mobile mobile)
            {
                to = mobile;
            }
            else
            {
                to = new Entity(Serial.Zero, loc, map);
            }

            Effects.SendMovingEffect(from, to, 0xF0D, 7, 0, false, false, Potion.Hue);
            Timer.StartTimer(TimeSpan.FromSeconds(1.0), () => Potion.Explode(from, loc, map));
        }

        protected override void OnTargetFinish(Mobile from) => Potion._users.Remove(from);
    }
}
