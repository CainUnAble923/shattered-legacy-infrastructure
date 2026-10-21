// cc-P199: ServUO pub57 Scripts/Items/Functional/BaseDecayingItem.cs. The base class of ServUO's short-lived quest and
// dungeon items (forty-two subclasses in pub57: Exploring the Deep, Shadowguard, Exodus, the Underworld puzzle keys,
// the Ritual, the Mad Scientist, the guardsman's rope). A blessed item whose virtual Lifespan (seconds; 0 means it never
// decays) is counted down by a 10-second timer into the TimeLeft property line, shown in seconds or, with UseSeconds
// false, as the largest of weeks, days, hours or minutes. At zero, Decay() tells the holder "The ~1_name~ expired...",
// puffs smoke with a sound at the holder (or the item) and deletes it.
//
// Pinned has a near relative, TransientItem (Spells/Spellweaving/Items/TransientItem.cs), which is NOT an equivalent:
// it takes the lifespan as a constructor argument, is Nontransferable, is not blessed, shows only seconds and stores an
// expiry time. ServUO's subclasses override Lifespan and UseSeconds, so they need this base as written.
//
// Shape changes forced by ModernUO, none visible to a player: ServUO's m_Lifespan field is the TimeLeft property's
// generated `_timeLeft` ([SerializableProperty], because its setter calls InvalidateProperties; the shape of our
// Services/Peerless/PeerlessKey.cs, which carries the same countdown). TimerPriority (BaseDecayingItem.cs:70) does not
// exist in pinned. The timer restarts in [AfterDeserialization], where ServUO restarts it at the end of Deserialize
// (:136-137). One housekeeping line is added: the timer stops in OnAfterDelete, so an item deleted any other way than
// Decay() stops ticking; ServUO leaves that timer running against a deleted item.
//
// cc-P195 (the Exodus Encounter) derives its ritual items from this base and moved the lifespan line and the expiry
// effect into DecayingLifespan below, so the Exodus dagger and robe, which keep ServUO's own copy of this count on
// other bases, share them. The expiry effect is skipped for an item that has no map.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class BaseDecayingItem : Item
{
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

    public virtual int Lifespan => 0;
    public virtual bool UseSeconds => true;

    [SerializableProperty(0)]
    [CommandProperty(AccessLevel.GameMaster)]
    public int TimeLeft
    {
        get => _timeLeft;
        set
        {
            _timeLeft = value;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

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

        if (_timeLeft <= 0)
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
        base.OnAfterDelete();
        StopTimer();
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
