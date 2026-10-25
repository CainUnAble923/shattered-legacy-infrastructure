// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Region.cs (cc-P195): "Ver Lor Reg", the gargoyle city in
// Ilshenar where the ritual's party lands and Clockwork Exodus waits. ServUO declares it in Regions.xml
// (type VerLorRegCity, priority 50, four rectangles, go 840,571,0); pinned's regions.json has no Ver Lor Reg at all, so
// ExodusRegions registers it at Initialize on Ilshenar (the WrongRegions shape of cc-P194), at DefaultPriority + 1 so a
// runtime region outranks the static one under it (the Despise rule), parented to whatever static region holds the
// go tile. Conversion notes: GetEnumeratedMobiles is GetPlayers; CheckTravel is BaseRegion's with the out message;
// TimerCallback is an Action; the exit timer is ServUO's (a warning eight minutes after the kill, the move two minutes
// later, then the controller starts again).

using System;
using System.Collections.Generic;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;

namespace Server.Engines.Exodus;

public static class ExodusRegions
{
    public const string Name = "Ver Lor Reg";
    public const int Priority = Region.DefaultPriority + 1;

    /// <summary>ServUO Data/Regions.xml:2152-2158.</summary>
    public static readonly Rectangle2D[] Bounds =
    {
        new(824, 552, 32, 176),
        new(752, 576, 72, 128),
        new(856, 576, 48, 128),
        new(904, 624, 24, 32)
    };

    public static readonly Point3D GoLocation = new(840, 571, 0);

    private static readonly Dictionary<Map, VerLorRegCity> _regions = new();

    public static void Initialize()
    {
        Register(Map.Ilshenar);
    }

    public static VerLorRegCity Get(Map map) => _regions.GetValueOrDefault(map);

    /// <summary>Registers Ver Lor Reg on a facet. Safe to call twice: the second call returns the first registration.</summary>
    public static VerLorRegCity Register(Map map)
    {
        if (map == null || map == Map.Internal)
        {
            return null;
        }

        if (_regions.TryGetValue(map, out var existing) && existing?.Registered == true)
        {
            return existing;
        }

        var parent = Region.Find(GoLocation, map);

        if (parent == null || parent.IsDefault)
        {
            parent = null;
        }

        var region = new VerLorRegCity(map, parent)
        {
            GoLocation = GoLocation
        };

        region.Register();
        _regions[map] = region;
        return region;
    }

    /// <summary>Test support: unregisters the facet's region.</summary>
    public static void Unregister(Map map)
    {
        if (_regions.Remove(map, out var region) && region != null)
        {
            region.Unregister();
        }
    }
}

public class VerLorRegCity : DungeonRegion
{
    /// <summary>ServUO Random_Locations: where the exit timer scatters the party, on the party's own facet.</summary>
    public static readonly Point3D[] RandomLocations =
    {
        new(1217, 469, -13),
        new(720, 1356, -60),
        new(748, 728, -29),
        new(287, 1016, 0),
        new(987, 1007, -35),
        new(1175, 1287, -30),
        new(1532, 1341, -3),
        new(527, 218, -44)
    };

    public static readonly TimeSpan ExitWarning = TimeSpan.FromMinutes(8);
    public static readonly TimeSpan ExitDelay = TimeSpan.FromMinutes(2);

    private ExitTimer _exitTimer;
    private Timer _moveTimer;   // cc-P610: the move, two minutes after the warning (ServUO: an untracked DelayCall)

    public VerLorRegCity(Map map, Region parent)
        : base(ExodusRegions.Name, map, parent, ExodusRegions.Priority, ToArea(ExodusRegions.Bounds))
    {
    }

    public bool ExitPending => _exitTimer?.Running == true || _moveTimer?.Running == true;

    private static Rectangle3D[] ToArea(Rectangle2D[] bounds)
    {
        var area = new Rectangle3D[bounds.Length];

        for (var i = 0; i < bounds.Length; i++)
        {
            area[i] = ConvertTo3D(bounds[i]);
        }

        return area;
    }

    /// <summary>ServUO: staff travel freely; players may teleport in and out but not recall, gate or mark.</summary>
    public override bool CheckTravel(Mobile traveller, Point3D p, TravelCheckType type, out TextDefinition message)
    {
        message = null;

        if (traveller.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        return type > TravelCheckType.Mark;
    }

    /// <summary>ServUO: the boss's death stops the controller and starts the exit.</summary>
    public override void OnDeath(Mobile m)
    {
        base.OnDeath(m);

        if (m != null && VerLorRegController.CurrentExodus == m)
        {
            VerLorRegController.Stop();
            StartExit();
        }
    }

    public void StartExit()
    {
        StartWarning(ExitWarning);
    }

    private void StartWarning(TimeSpan delay)
    {
        _exitTimer?.Stop();
        _moveTimer?.Stop();
        _exitTimer = new ExitTimer(this, delay);
        _exitTimer.Start();
        VerLorRegController.IlshenarInstance?.NoteExit(Core.Now + delay, DateTime.MinValue); // cc-P610: saved
    }

    private void StartMove(TimeSpan delay)
    {
        _moveTimer?.Stop();
        _moveTimer = Timer.DelayCall(delay, MoveLocation);
        VerLorRegController.IlshenarInstance?.NoteExit(DateTime.MinValue, Core.Now + delay); // cc-P610: saved
    }

    /// <summary>cc-P610: an exit saved before a restart goes on here: the warning or the move at its saved time, or at
    /// once when that time has passed (an overdue warning still gives its two minutes before the move).</summary>
    public void ResumeExit(DateTime warnAt, DateTime moveAt)
    {
        if (moveAt != DateTime.MinValue)
        {
            StartMove(moveAt > Core.Now ? moveAt - Core.Now : TimeSpan.Zero);
        }
        else if (warnAt != DateTime.MinValue)
        {
            StartWarning(warnAt > Core.Now ? warnAt - Core.Now : TimeSpan.Zero);
        }
    }

    /// <summary>
    /// ServUO Region.GetPlayers walks the sectors' players (every Mobile with Player set, connected or not); pinned's
    /// GetPlayers walks the connected clients only. The players, as ServUO means them.
    /// </summary>
    public List<Mobile> PlayersHere()
    {
        var list = new List<Mobile>();

        foreach (var m in GetMobiles())
        {
            if (m.Player && !m.Deleted && m.AccessLevel == AccessLevel.Player)
            {
                list.Add(m);
            }
        }

        return list;
    }

    /// <summary>ServUO ExitTimer.OnTick: 1010589 to every player in the city, then MoveLocation two minutes later.</summary>
    public void WarnExit()
    {
        foreach (var m in PlayersHere())
        {
            m.SendLocalizedMessage(1010589); // You will be teleported out of this area in 2 minutes
        }

        StartMove(ExitDelay);
    }

    /// <summary>ServUO MoveLocation: every player to one of the eight spots with their pets, and Exodus rises again.</summary>
    public void MoveLocation()
    {
        if (!Registered)
        {
            return; // cc-P610: a city region replaced (a restart, a regeneration) leaves the exit to the new one
        }

        VerLorRegController.IlshenarInstance?.NoteExit(DateTime.MinValue, DateTime.MinValue);

        foreach (var m in PlayersHere())
        {
            var p = RandomLocations.RandomElement();

            m.MoveToWorld(p, m.Map);
            BaseCreature.TeleportPets(m, p, m.Map);
        }

        VerLorRegController.Start();
    }

    private class ExitTimer : Timer
    {
        private readonly VerLorRegCity _region;

        public ExitTimer(VerLorRegCity region, TimeSpan delay) : base(delay) => _region = region;

        protected override void OnTick()
        {
            if (_region.Registered)
            {
                _region.WarnExit();
            }
        }
    }
}
