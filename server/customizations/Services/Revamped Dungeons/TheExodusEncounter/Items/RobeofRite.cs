// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Items/RobeofRite.cs (cc-P195): the vestment of the ritual,
// worn by every member and burnt when the party is carried to Ver Lor Reg. A week, counted on the robe itself
// (ServUO's own copy of the BaseDecayingItem count; DecayingLifespan here). ServUO passes 0x1F03 to Robe's hue
// argument and then sets 2702; pinned's Robe takes the hue only, so the hue is set once.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class RobeofRite : Robe
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _timeLeft;

    private Timer _timer;

    [Constructible]
    public RobeofRite() : base(2702)
    {
        Weight = 3;
        StrRequirement = 10;

        if (Lifespan > 0)
        {
            _timeLeft = Lifespan;
            StartTimer();
        }
    }

    public override int LabelNumber => 1153510; // robe of rite

    public virtual int Lifespan => 604800;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (Lifespan > 0)
        {
            DecayingLifespan.AddProperty(list, _timeLeft, false);
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
        StartTimer();
    }
}
