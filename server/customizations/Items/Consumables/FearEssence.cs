// cc-P128: ServUO pub57 Scripts/Items/Consumables/FearEssence.cs -> ours. Fear essence: thrown, it bursts over a
// 20-tile radius a second later; everyone the thrower can harm rolls Magic Resist against (4 * resist + 150) / 700, and
// those who fail are paralyzed for three seconds (an aggressive act). One throw a minute per thrower.
//
// ServUO builds its two effects as raw packets (a ParticleEffect on each victim, a HuedEffect fan along the ground);
// here they are Effects.SendLocationParticles and Effects.SendMovingEffect with the same ids, speeds and hues.
// Shape and the delay table follow pinned's BaseConfusionBlastPotion; PotionEffect.FearEssence is
// server/patches/BasePotion-sa-potion-effects.patch.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.Spells;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class FearEssence : BasePotion
{
    private static readonly Dictionary<Mobile, TimerExecutionToken> _delay = new();
    private HashSet<Mobile> _users;

    [Constructible]
    public FearEssence() : base(0xF0D, PotionEffect.FearEssence)
    {
        Hue = 5;
        Weight = 2.0;
    }

    public override int LabelNumber => 1115744; // fear essence

    public virtual int Radius => 20;

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

        Timer.StartTimer(TimeSpan.FromSeconds(1), () => TarEffect(loc, map));

        foreach (var mobile in map.GetMobilesInRange(loc, Radius))
        {
            if (mobile == from || !from.CanBeHarmful(mobile, false))
            {
                continue;
            }

            var chance = (4 * mobile.Skills[SkillName.MagicResist].Value + 150) / 700;

            if (chance < Utility.RandomDouble())
            {
                mobile.SendLocalizedMessage(1115815); // You resist the effects of the Fear Essence.
            }
            else
            {
                var p = mobile.Location;
                Effects.SendLocationParticles(EffectItem.Create(p, mobile.Map, EffectItem.DefaultDuration), 0x376A, 9, 32, 5039);
                mobile.Damage(0, from);
                mobile.Paralyze(TimeSpan.FromSeconds(3));
                from.DoHarmful(mobile);
            }
        }
    }

    public virtual void TarEffect(Point3D p, Map map)
    {
        Effects.PlaySound(p, map, 1619);

        for (var x = -4; x <= 4; x++)
        {
            for (var y = -1; y <= 1; y++)
            {
                Effects.SendMovingEffect(map, 0x3E03, p, new Point3D(p.X + x, p.Y + y, p.Z), 0, 0, false, false, 5);
            }
        }
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
        public ThrowTarget(FearEssence potion) : base(12, true, TargetFlags.Harmful) => Potion = potion;

        public FearEssence Potion { get; }

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

            Effects.SendMovingEffect(from, to, Potion.ItemID, 7, 0, false, false, Potion.Hue);
            Timer.StartTimer(TimeSpan.FromSeconds(1.0), () => Potion.Explode(from, loc, map));
        }

        protected override void OnTargetFinish(Mobile from) => Potion._users.Remove(from);
    }
}
