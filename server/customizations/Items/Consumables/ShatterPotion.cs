// cc-P128: ServUO pub57 Scripts/Items/Consumables/ShatterPotion.cs -> ours. The shatter potion: thrown at someone
// you can harm, it lands a second later and, if their pack holds 20 or more potions, destroys a fifth of them at random
// (fewer than 20: nothing happens). One throw a minute per thrower.
//
// Shape and the delay table follow pinned's BaseConfusionBlastPotion; the burst on the victim is
// Effects.SendLocationParticles with ServUO's ids (ServUO sends a raw ParticleEffect). PotionEffect.Shatter is
// server/patches/BasePotion-sa-potion-effects.patch.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ShatterPotion : BasePotion
{
    private static readonly Dictionary<Mobile, TimerExecutionToken> _delay = new();
    private HashSet<Mobile> _users;

    [Constructible]
    public ShatterPotion() : base(0xF0D, PotionEffect.Shatter)
    {
        Hue = 60;
        Weight = 2.0;
    }

    public override int LabelNumber => 1115759; // Shatter Potion

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

    public virtual void Explode(Mobile from, Mobile m, Map map)
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
        Effects.PlaySound(m.Location, map, 285);

        m.Damage(0);
        Effects.SendLocationParticles(EffectItem.Create(m.Location, m.Map, EffectItem.DefaultDuration), 0x373A, 10, 10, 5051);

        var amount = 0;

        if (m.Backpack != null)
        {
            foreach (var p in m.Backpack.FindItemsByType<BasePotion>())
            {
                amount += p.Amount;
            }
        }

        if (amount < 20)
        {
            m.SendLocalizedMessage(1115760, from.Name); // ~1_NAME~'s shatter potion hits you, but nothing happens.
            return;
        }

        var count = (int)(amount * 0.2);

        if (count > 1)
        {
            m.SendLocalizedMessage(1115762, $"{from.Name}\t{count}"); // ~1_NAME~'s shatter potion destroys ~2_NUM~ potions in your inventory.
        }
        else
        {
            m.SendLocalizedMessage(1115761, from.Name); // ~1_NAME~'s shatter potion destroys a potion in your inventory.
        }

        var potions = new List<BasePotion>();

        for (var i = 0; i < count; i++)
        {
            potions.Clear();

            foreach (var p in m.Backpack.FindItemsByType<BasePotion>())
            {
                potions.Add(p);
            }

            if (potions.Count == 0)
            {
                break;
            }

            potions[Utility.Random(potions.Count)].Consume();
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
        public ThrowTarget(ShatterPotion potion) : base(12, true, TargetFlags.None) => Potion = potion;

        public ShatterPotion Potion { get; }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (Potion.Deleted || Potion.Map == Map.Internal)
            {
                return;
            }

            if (targeted is not Mobile m || from.Map == null || !from.CanBeHarmful(m))
            {
                return;
            }

            // Add delay
            AddDelay(from);

            from.RevealingAction();

            var map = from.Map;

            Effects.SendMovingEffect(from, m, Potion.ItemID, 7, 0, false, false, Potion.Hue);
            Timer.StartTimer(TimeSpan.FromSeconds(1.0), () => Potion.Explode(from, m, map));
        }

        protected override void OnTargetFinish(Mobile from) => Potion._users.Remove(from);
    }
}
