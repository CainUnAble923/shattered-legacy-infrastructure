using System;
using System.Collections.Generic;
using System.Linq;
using Server.Network;

namespace Server.Items
{
    /// <summary>
    ///     Ported from ServUO <c>Scripts/Abilities/SAPropEffects.cs</c> (768 lines), reduced to the
    ///     damage eater and the framework it needs.
    ///     <para>
    ///         ServUO's file carries nine effect kinds. Eight of them - Soul Charge, Splintering,
    ///         Searing, Bane, Bone Breaker, Swarm, Sparks and Battle Lust - are weapon properties that
    ///         belong to systems this shard does not have, and every one of them would drag in further
    ///         dependencies. Only <see cref="DamageEaterContext" /> is needed to make
    ///         <see cref="SAAbsorptionAttribute" /> do something, so only it is here. The
    ///         <see cref="EffectsType" /> enum keeps all nine members so a later port of any of the
    ///         others is additive rather than a renumbering.
    ///     </para>
    ///     <para>See <c>shard-migration/notes/s6-absorption.md</c>.</para>
    /// </summary>
    public enum EffectsType
    {
        BattleLust,
        SoulCharge,
        DamageEater,
        Splintering,
        Searing,
        Bane,
        BoneBreaker,
        Swarm,
        Sparks
    }

    /// <summary>
    ///     Used for complex weapon/armor properties introduced in Stygian Abyss.
    /// </summary>
    public class PropertyEffect
    {
        private Timer _timer;

        private static readonly List<PropertyEffect> _effects = new();

        public static List<PropertyEffect> Effects => _effects;

        public Mobile Mobile { get; }

        public Mobile Victim { get; }

        public Item Owner { get; }

        public EffectsType Effect { get; }

        public TimeSpan Duration { get; }

        public TimeSpan TickDuration { get; }

        public Timer Timer => _timer;

        public PropertyEffect(
            Mobile from, Mobile victim, Item owner, EffectsType effect, TimeSpan duration,
            TimeSpan tickduration
        )
        {
            Mobile = from;
            Victim = victim;
            Owner = owner;
            Effect = effect;
            Duration = duration;
            TickDuration = tickduration;

            _effects.Add(this);

            if (TickDuration > TimeSpan.MinValue)
            {
                StartTimer();
            }
        }

        public virtual void RemoveEffects()
        {
            StopTimer();

            _effects.Remove(this);
        }

        public void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new InternalTimer(this);
                _timer.Start();
            }
        }

        public void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }
        }

        public bool IsEquipped() => Owner != null && Mobile?.FindItemOnLayer(Owner.Layer) == Owner;

        public virtual void OnTick()
        {
        }

        public virtual void OnDamaged(int damage)
        {
        }

        public virtual void OnDamage(int damage, int phys, int fire, int cold, int poison, int energy, int direct)
        {
        }

        private class InternalTimer : Timer
        {
            private readonly PropertyEffect _effect;
            private readonly DateTime _expires;

            public InternalTimer(PropertyEffect effect) : base(effect.TickDuration, effect.TickDuration)
            {
                _effect = effect;

                _expires = effect.Duration > TimeSpan.MinValue ? Core.Now + effect.Duration : DateTime.MinValue;
            }

            protected override void OnTick()
            {
                var m = _effect.Mobile;

                if (m == null || m.Deleted || !m.Alive || m.IsDeadBondedPet)
                {
                    _effect.RemoveEffects();
                }
                else if (_effect.Victim != null &&
                         (_effect.Victim.Deleted || !_effect.Victim.Alive || m.IsDeadBondedPet))
                {
                    _effect.RemoveEffects();
                }
                else
                {
                    _effect.OnTick();

                    if (_expires > DateTime.MinValue && _expires <= Core.Now)
                    {
                        _effect.RemoveEffects();
                    }
                }
            }
        }

        public static bool IsUnderEffects(Mobile from, EffectsType effect)
        {
            for (var i = 0; i < _effects.Count; i++)
            {
                var e = _effects[i];

                if (e.Mobile == from && e.Effect == effect)
                {
                    return true;
                }
            }

            return false;
        }

        public static T GetContext<T>(Mobile from, EffectsType type) where T : PropertyEffect =>
            _effects.FirstOrDefault(e => e.Mobile == from && e.Effect == type) as T;

        public static T GetContext<T>(Mobile from, Mobile victim, EffectsType type) where T : PropertyEffect =>
            _effects.FirstOrDefault(e => e.Mobile == from && e.Victim == victim && e.Effect == type) as T;

        public static IEnumerable<T> GetContexts<T>(Mobile victim, EffectsType type) where T : PropertyEffect =>
            _effects.OfType<T>().Where(e => e.Victim == victim);
    }

    public enum DamageType
    {
        Kinetic,
        Fire,
        Cold,
        Poison,
        Energy,
        AllTypes
    }

    /// <summary>
    ///     The Stygian Abyss damage eater.
    ///     <para>
    ///         <b>It does not reduce damage.</b> The name is misleading and this is worth being exact
    ///         about, because "the eater reduces damage taken" is how it is usually described: the
    ///         victim takes the damage in full, and a share of it comes back three seconds later as a
    ///         heal, capped at 30% of that damage component for a matching eater and 18% for the
    ///         generic Damage Eater. Charges cap the effect at 20 pending heals.
    ///     </para>
    ///     <para>Ported verbatim from ServUO <c>Scripts/Abilities/SAPropEffects.cs:229-392</c>.</para>
    /// </summary>
    public class DamageEaterContext : PropertyEffect
    {
        private int _charges;

        public DamageEaterContext(Mobile mobile)
            : base(mobile, null, null, EffectsType.DamageEater, TimeSpan.MinValue, TimeSpan.FromSeconds(10))
        {
            _charges = 0;
        }

        public override void OnDamage(int damage, int phys, int fire, int cold, int poison, int energy, int direct)
        {
            if (_charges >= 20)
            {
                return;
            }

            var k = GetValue(DamageType.Kinetic, Mobile) / 100.0;
            var f = GetValue(DamageType.Fire, Mobile) / 100.0;
            var c = GetValue(DamageType.Cold, Mobile) / 100.0;
            var p = GetValue(DamageType.Poison, Mobile) / 100.0;
            var e = GetValue(DamageType.Energy, Mobile) / 100.0;
            var a = GetValue(DamageType.AllTypes, Mobile) / 100.0;

            if (phys > 0 && (k > 0 || a > 0))
            {
                var pd = damage * (phys / 100.0);

                DelayHeal(k >= a ? Math.Min(pd * k, pd * .3) : Math.Min(pd * a, pd * .18));

                _charges++;
            }

            if (fire > 0 && (f > 0 || a > 0))
            {
                var fd = damage * (fire / 100.0);

                DelayHeal(f >= a ? Math.Min(fd * f, fd * .3) : Math.Min(fd * a, fd * .18));

                _charges++;
            }

            if (cold > 0 && (c > 0 || a > 0))
            {
                var cd = damage * (cold / 100.0);

                DelayHeal(c >= a ? Math.Min(cd * c, cd * .3) : Math.Min(cd * a, cd * .18));

                _charges++;
            }

            if (poison > 0 && (p > 0 || a > 0))
            {
                var pod = damage * (poison / 100.0);

                DelayHeal(p >= a ? Math.Min(pod * p, pod * .3) : Math.Min(pod * a, pod * .18));

                _charges++;
            }

            if (energy > 0 && (e > 0 || a > 0))
            {
                var ed = damage * (energy / 100.0);

                DelayHeal(e >= a ? Math.Min(ed * e, ed * .3) : Math.Min(ed * a, ed * .18));

                _charges++;
            }

            if (direct > 0 && a > 0)
            {
                var dd = damage * (direct / 100.0);

                DelayHeal(Math.Min(dd * a, dd * .18));
                _charges++;
            }
        }

        public void DelayHeal(double toHeal)
        {
            // Server.Timer, not the inherited PropertyEffect.Timer property. C#'s Color Color rule
            // would resolve the bare name correctly, but the two readings are one keystroke apart.
            Server.Timer.DelayCall(TimeSpan.FromSeconds(3), DoHeal, toHeal);
        }

        public void DoHeal(double dam)
        {
            if (dam < 0)
            {
                return;
            }

            Mobile.Heal((int)dam, Mobile, false);
            Mobile.SendLocalizedMessage(1113617); // Some of the damage you received has been converted to heal you.

            SendEatenEffect();

            _charges--;
        }

        /// <summary>
        ///     ServUO sends this as a raw <c>ParticleEffect</c> packet rather than through
        ///     <c>Effects.SendTargetParticles</c>, because it wants <c>fixedDirection: false</c> and a
        ///     specific explode effect, and ModernUO's <c>CreateTargetParticleEffect</c> helper
        ///     hardcodes both. Building the same packet keeps the visual identical; the helper would
        ///     have changed it.
        /// </summary>
        private void SendEatenEffect()
        {
            var map = Mobile.Map;

            if (map == null)
            {
                return;
            }

            var buffer = stackalloc byte[OutgoingEffectPackets.ParticleEffectLength].InitializePacket();

            OutgoingEffectPackets.CreateParticleEffect(
                buffer,
                EffectType.FixedFrom,
                Mobile.Serial,
                Serial.Zero,
                0x375A,
                Mobile.Location,
                Mobile.Location,
                1,
                10,
                false,
                false,
                33,
                0,
                2,
                6889,
                1,
                Mobile.Serial,
                45,
                0
            );

            // Server.Effects, not PropertyEffect.Effects — the inherited static list shadows the
            // class name inside this type, and the bare name does not compile.
            Server.Effects.SendPacket(Mobile.Location, map, buffer);
        }

        public override void OnTick()
        {
            if (_charges <= 0)
            {
                RemoveEffects();
            }
        }

        public static bool HasValue(Mobile from) =>
            GetValue(DamageType.Kinetic, from) > 0 ||
            GetValue(DamageType.Fire, from) > 0 ||
            GetValue(DamageType.Cold, from) > 0 ||
            GetValue(DamageType.Poison, from) > 0 ||
            GetValue(DamageType.Energy, from) > 0 ||
            GetValue(DamageType.AllTypes, from) > 0;

        public static int GetValue(DamageType type, Mobile from)
        {
            if (from == null)
            {
                return 0;
            }

            return type switch
            {
                DamageType.Kinetic  => SAAbsorptionAttributes.GetValue(from, SAAbsorptionAttribute.EaterKinetic),
                DamageType.Fire     => SAAbsorptionAttributes.GetValue(from, SAAbsorptionAttribute.EaterFire),
                DamageType.Cold     => SAAbsorptionAttributes.GetValue(from, SAAbsorptionAttribute.EaterCold),
                DamageType.Poison   => SAAbsorptionAttributes.GetValue(from, SAAbsorptionAttribute.EaterPoison),
                DamageType.Energy   => SAAbsorptionAttributes.GetValue(from, SAAbsorptionAttribute.EaterEnergy),
                DamageType.AllTypes => SAAbsorptionAttributes.GetValue(from, SAAbsorptionAttribute.EaterDamage),
                _                   => 0
            };
        }

        /// <summary>
        ///     The entry point <c>AOS.Damage</c> calls, through
        ///     <c>server/patches/AOS-damage-eater-hook.patch</c>. Without that patch every eater
        ///     property on every item is inert while still showing on the tooltip, which is a worse
        ///     outcome than not having the property at all.
        /// </summary>
        public static void CheckDamage(
            Mobile from, int damage, int phys, int fire, int cold, int pois, int ergy, int direct
        )
        {
            var context = GetContext<DamageEaterContext>(from, EffectsType.DamageEater);

            if (context == null && HasValue(from))
            {
                context = new DamageEaterContext(from);
            }

            context?.OnDamage(damage, phys, fire, cold, pois, ergy, direct);
        }
    }
}
