// ServUO: Services/Revamped Dungeons/TheExodusEncounter/VerLorRegController.cs (cc-P195). The invisible item in Ver Lor
// Reg that owns Clockwork Exodus: Start raises the boss at its tile when none is alive, Stop deletes it; the region's
// death hook stops it and the exit timer starts it again. ServUO keeps Active and the boss as statics written by
// whichever instance saves, and Initialize places a controller on every boot if none exists; here the one instance is
// the state (IlshenarInstance is the controller on Ilshenar, claimed on placement and after a load), the statics read
// through it, and nothing is placed at startup: [GenExodusEncounter places it (the shard's rule: the command, never
// the boot). [props on it: IsRunning (ServUO's Active, Start and Stop), Exodus (ServUO's Mobile).

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Engines.Exodus;

[SerializationGenerator(0, false)]
public partial class VerLorRegController : Item
{
    [SerializableField(0, setter: "private")]
    private bool _running;

    [SerializableField(1, setter: "private")]
    [SerializedCommandProperty(AccessLevel.Administrator, readOnly: true)]
    private ClockworkExodus _exodus;

    [Constructible]
    public VerLorRegController() : base(7107)
    {
        Name = "Ver Lor Reg Controller";
        Visible = false;
        Movable = false;
    }

    public static VerLorRegController IlshenarInstance { get; private set; }

    /// <summary>ServUO's static Active: the Ilshenar controller is running a boss.</summary>
    public static bool Active => IlshenarInstance is { Deleted: false, _running: true };

    /// <summary>ServUO's static Mobile: the Ilshenar controller's Clockwork Exodus, or null.</summary>
    public static ClockworkExodus CurrentExodus => IlshenarInstance is { Deleted: false } c ? c._exodus : null;

    [CommandProperty(AccessLevel.GameMaster)]
    public bool IsRunning
    {
        get => _running;
        set
        {
            if (value)
            {
                StartHere();
            }
            else
            {
                StopHere();
            }
        }
    }

    public static void Start() => IlshenarInstance?.StartHere();

    public static void Stop() => IlshenarInstance?.StopHere();

    /// <summary>ServUO Start: mark active and raise Clockwork Exodus at 854, 642, -40 with home range 4 if none lives.</summary>
    public void StartHere()
    {
        if (Deleted || _running)
        {
            return;
        }

        Running = true;

        if (_exodus is { Deleted: false })
        {
            return;
        }

        var map = Map == null || Map == Map.Internal ? Map.Ilshenar : Map;

        var m = new ClockworkExodus
        {
            Home = ExodusEncounter.ExodusLocation,
            RangeHome = 4
        };

        m.MoveToWorld(ExodusEncounter.ExodusLocation, map);
        Exodus = m;
    }

    /// <summary>ServUO Stop: inactive, and the boss is deleted.</summary>
    public void StopHere()
    {
        if (!_running)
        {
            return;
        }

        Running = false;
        _exodus?.Delete();
        Exodus = null;
    }

    private void Claim()
    {
        if (Map == Map.Ilshenar && !Deleted)
        {
            IlshenarInstance = this;
        }
    }

    public override void OnMapChange()
    {
        base.OnMapChange();
        Claim();
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        _running = false;
        _exodus?.Delete();
        _exodus = null;

        if (IlshenarInstance == this)
        {
            IlshenarInstance = null;
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Claim();

        if (_exodus is { Deleted: true })
        {
            _exodus = null;
        }
    }
}
