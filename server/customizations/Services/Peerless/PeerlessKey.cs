// ServUO: Services/Peerless/PeerlessKey.cs (CC6 batch 4, P9 / Q-051). Values verbatim; serialization by the generator.
//
// This is the KEY BASE, not the peerless system. It is an Item with a blessed loot type, a per-key Map that adds a
// facet line to the property list, and a Lifespan (default 604,800 s, one week) counted down by a 10-second timer into
// a TimeLeft property line, ending in Decay(): an expiry message, a puff of smoke and Delete(). Nothing here reads a
// PeerlessAltar; on ServUO the altars read the key. No altar exists on this shard (gap register B14, WON'T), so a key
// is a one-week blessed drop with a lifespan line and nothing to put it in, which is exactly what a ServUO player
// holds until they reach an altar. Ported so that ShatteredCrystals, DraconicOrb and SerpentFangKey get their final
// parent the first time (AGENTS.md: re-parenting after a save exists is not a migration).
//
// Renames forced by ModernUO: ServUO's `_Map` property is `KeyMap` here, because the generator names a property after
// its backing field and a `Map` property would hide Item.Map. ServUO's `m_Lifespan` field is the TimeLeft property's
// `_timeLeft`; the virtual `Lifespan` is the total, as in ServUO. Both persisted members are [SerializableProperty]
// (stock Beverage.cs:327 shape) because their setters call InvalidateProperties. TimerPriority does not exist here.
// The timer restarts in [AfterDeserialization], where ServUO restarts it at the end of Deserialize (stock
// BaseFish.cs:75 does the same), and is stopped in OnAfterDelete so a deleted key stops ticking; ServUO never stops it,
// which is invisible to a player and is the one housekeeping line this file adds.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class PeerlessKey : Item
{
    private Timer _timer;

    [Constructible]
    public PeerlessKey(int itemID) : base(itemID)
    {
        LootType = LootType.Blessed;

        if (Lifespan > 0)
        {
            _timeLeft = Lifespan;
            StartTimer();
        }
    }

    public virtual int Lifespan => 604800;
    public virtual bool UseSeconds => false;

    [SerializableProperty(0)]
    [CommandProperty(AccessLevel.GameMaster)]
    public Map KeyMap
    {
        get => _keyMap;
        set
        {
            _keyMap = value;
            InvalidateProperties();
            this.MarkDirty();
        }
    }

    [SerializableProperty(1)]
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

        if (_keyMap != null)
        {
            if (_keyMap == Map.Felucca)
            {
                list.Add(1012001); // Felucca
            }
            else if (_keyMap == Map.Trammel)
            {
                list.Add(1012000); // Trammel
            }
            else if (_keyMap == Map.Ilshenar)
            {
                list.Add(1012002); // Ilshenar
            }
            else if (_keyMap == Map.Malas)
            {
                list.Add(1060643); // Malas
            }
            else if (_keyMap == Map.Tokuno)
            {
                list.Add(1063258); // Tokuno Islands
            }
        }

        if (Lifespan > 0)
        {
            if (UseSeconds)
            {
                list.Add(1072517, _timeLeft.ToString()); // Lifespan: ~1_val~ seconds
            }
            else
            {
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
        }
    }

    public virtual void StartTimer()
    {
        if (_timer != null)
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

            Effects.SendLocationParticles(EffectItem.Create(parent.Location, parent.Map, EffectItem.DefaultDuration), 0x3728, 8, 20, 5042);
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
    private void AfterDeserialization() => StartTimer();
}
