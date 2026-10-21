// cc-P199: ServUO pub57 Scripts/Items/Functional/BaseDecayingItem.cs. The base class of ServUO's short-lived quest and
// dungeon items (forty-two subclasses in pub57: Exploring the Deep, Shadowguard, Exodus, the Underworld puzzle keys,
// the Ritual, the Mad Scientist, the guardsman's rope). A blessed item whose virtual Lifespan (seconds; 0 means it never
// decays) is counted down by a 10-second timer into the TimeLeft property line, shown in seconds or, with UseSeconds
// false, as the largest of weeks, days, hours or minutes. At zero, Decay() tells the holder "The ~1_name~ expired...",
// puffs smoke with a sound at the holder (or the item) and deletes it. Nothing on this shard derives from it yet.
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

        if (Lifespan <= 0)
        {
            return;
        }

        if (UseSeconds)
        {
            list.Add(1072517, _timeLeft.ToString()); // Lifespan: ~1_val~ seconds
            return;
        }

        var t = TimeSpan.FromSeconds(_timeLeft);

        var weeks = t.Days / 7;
        var days = t.Days;
        var hours = t.Hours;
        var minutes = t.Minutes;

        if (weeks > 1)
        {
            list.Add(1153092, (t.Days / 7).ToString()); // Lifespan: ~1_val~ weeks
        }
        else if (days > 1)
        {
            list.Add(1153091, t.Days.ToString()); // Lifespan: ~1_val~ days
        }
        else if (hours > 1)
        {
            list.Add(1153090, t.Hours.ToString()); // Lifespan: ~1_val~ hours
        }
        else if (minutes > 1)
        {
            list.Add(1153089, t.Minutes.ToString()); // Lifespan: ~1_val~ minutes
        }
        else
        {
            list.Add(1072517, t.Seconds.ToString()); // Lifespan: ~1_val~ seconds
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
        if (RootParent is Mobile parent)
        {
            if (Name == null)
            {
                parent.SendLocalizedMessage(1072515, "#" + LabelNumber); // The ~1_name~ expired...
            }
            else
            {
                parent.SendLocalizedMessage(1072515, Name); // The ~1_name~ expired...
            }

            Effects.SendLocationParticles(
                EffectItem.Create(parent.Location, parent.Map, EffectItem.DefaultDuration), 0x3728, 8, 20, 5042
            );
            Effects.PlaySound(parent.Location, parent.Map, 0x201);
        }
        else
        {
            Effects.SendLocationParticles(EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x3728, 8, 20, 5042);
            Effects.PlaySound(Location, Map, 0x201);
        }

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
