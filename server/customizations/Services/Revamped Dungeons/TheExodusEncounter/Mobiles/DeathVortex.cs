// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Mobiles/DeathVortex.cs (cc-P195): the death vortex Clockwork
// Exodus casts, a trap that lives fifteen seconds and, every two seconds, drains three fifths of the mana or seven
// tenths of the stamina of anyone within one tile (but staff and Exodus). IsStaff is AccessLevel > Player here.

using System;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class DeathVortexTrap : BaseTrap
{
    public static readonly TimeSpan Life = TimeSpan.FromSeconds(15.0);

    private Timer _timer;

    [Constructible]
    public DeathVortexTrap() : base(0x3789)
    {
        Hue = 2070;
        Movable = false;

        StartTimer();
    }

    public override bool PassivelyTriggered => true;
    public override TimeSpan PassiveTriggerDelay => TimeSpan.FromSeconds(2.0);
    public override int PassiveTriggerRange => 3;
    public override TimeSpan ResetDelay => TimeSpan.FromSeconds(0.2);

    public override void OnTrigger(Mobile from)
    {
        if (from.AccessLevel > AccessLevel.Player)
        {
            return;
        }

        if (from.Alive && CheckRange(from.Location, 1) && from is not ClockworkExodus)
        {
            StamManaDrain(from);
        }
    }

    public void StamManaDrain(Mobile defender)
    {
        if (!defender.Alive)
        {
            return;
        }

        if (Utility.RandomBool())
        {
            var manaToLeech = (int)(defender.Mana * 0.6);
            defender.Mana -= manaToLeech;
        }
        else
        {
            var stamToLeech = (int)(defender.Stam * 0.7);
            defender.Stam -= stamToLeech;
        }

        defender.SendLocalizedMessage(1152694, "", 0x22); // Your life force is drained by the death vortex!
    }

    private void StartTimer()
    {
        _timer?.Stop();
        _timer = Timer.DelayCall(Life, () =>
        {
            if (!Deleted)
            {
                Delete();
            }
        });
    }

    public override void OnAfterDelete()
    {
        _timer?.Stop();
        _timer = null;
        base.OnAfterDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        StartTimer();
    }
}
