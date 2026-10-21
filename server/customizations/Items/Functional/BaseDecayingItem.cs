// ServUO: Items/Functional/BaseDecayingItem.cs (cc-P195): an item that counts down a lifespan in seconds and expires.
// Ported as the leaf base the Exodus Encounter's ritual items need (ExodusSummoningRite, ExodusSummoningAlter,
// ExodusTomeAltar, ExoticToolkit, MechanicalComponent, PunchCard); the Exodus dagger and robe reproduce the same count on
// their own bases through DecayingLifespan. Written as pinned writes an item: the generator carries the one saved field
// (ServUO version 0 wrote the lifespan after the base), the timer is rebuilt after a load (ServUO restarted it in
// Deserialize), and TimerPriority is dropped (pinned's wheel has none). The property line uses ServUO's clilocs.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public abstract partial class BaseDecayingItem : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _timeLeft;

    private Timer _timer;

    public BaseDecayingItem(int itemID) : base(itemID)
    {
        LootType = LootType.Blessed;

        if (Lifespan > 0)
        {
            _timeLeft = Lifespan;
            StartTimer();
        }
    }

    /// <summary>Seconds the item lives from creation; 0 never expires.</summary>
    public virtual int Lifespan => 0;

    /// <summary>True shows the time left in seconds; false in weeks, days, hours or minutes.</summary>
    public virtual bool UseSeconds => true;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (Lifespan > 0)
        {
            DecayingLifespan.AddProperty(list, _timeLeft, UseSeconds);
        }
    }

    public virtual void StartTimer()
    {
        if (_timer != null || Lifespan == 0)
        {
            return;
        }

        _timer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), Slice);
    }

    public virtual void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    public virtual void Slice()
    {
        TimeLeft -= 10;
        InvalidateProperties();

        if (TimeLeft <= 0)
        {
            Decay();
        }
    }

    public virtual void Decay()
    {
        DecayingLifespan.Expire(this);
        StopTimer();
        Delete();
    }

    public override void OnAfterDelete()
    {
        StopTimer();
        base.OnAfterDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (Lifespan > 0)
        {
            StartTimer();
        }
    }
}

/// <summary>The lifespan line and the expiry effect ServUO's decaying items share (BaseDecayingItem.cs:37-66, :95-117).</summary>
public static class DecayingLifespan
{
    public static void AddProperty(IPropertyList list, int secondsLeft, bool useSeconds)
    {
        if (useSeconds)
        {
            list.Add(1072517, secondsLeft.ToString()); // Lifespan: ~1_val~ seconds
            return;
        }

        var t = TimeSpan.FromSeconds(secondsLeft);
        var weeks = t.Days / 7;

        if (weeks > 1)
        {
            list.Add(1153092, weeks.ToString()); // Lifespan: ~1_val~ weeks
        }
        else if (t.Days > 1)
        {
            list.Add(1153091, t.Days.ToString()); // Lifespan: ~1_val~ days
        }
        else if (t.Hours > 1)
        {
            list.Add(1153090, t.Hours.ToString()); // Lifespan: ~1_val~ hours
        }
        else if (t.Minutes > 1)
        {
            list.Add(1153089, t.Minutes.ToString()); // Lifespan: ~1_val~ minutes
        }
        else
        {
            list.Add(1072517, t.Seconds.ToString()); // Lifespan: ~1_val~ seconds
        }
    }

    /// <summary>The "expired" message to the holder and the puff of smoke where the item was.</summary>
    public static void Expire(Item item)
    {
        if (item.RootParent is Mobile parent)
        {
            if (item.Name == null)
            {
                parent.SendLocalizedMessage(1072515, $"#{item.LabelNumber}"); // The ~1_name~ expired...
            }
            else
            {
                parent.SendLocalizedMessage(1072515, item.Name); // The ~1_name~ expired...
            }

            Effects.SendLocationParticles(
                EffectItem.Create(parent.Location, parent.Map, EffectItem.DefaultDuration), 0x3728, 8, 20, 5042
            );
            Effects.PlaySound(parent.Location, parent.Map, 0x201);
        }
        else if (item.Map != null && item.Map != Map.Internal)
        {
            Effects.SendLocationParticles(
                EffectItem.Create(item.Location, item.Map, EffectItem.DefaultDuration), 0x3728, 8, 20, 5042
            );
            Effects.PlaySound(item.Location, item.Map, 0x201);
        }
    }
}
