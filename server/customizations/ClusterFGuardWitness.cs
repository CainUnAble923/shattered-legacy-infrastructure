// cc-P628: guards that patrol and see, stage 1 of shard-migration notes/cc-P608-guards-patrol-design.md (Chase 2026-10-25,
// option C: physical patrols and line of sight in one test town first; New Haven qualified separately later; Persuasion
// never erases a crime). Stage 1 is the design's first prompt: the inventory (in the notes) and the incident/witness
// contract, with its sight and report tests. Nothing here moves, spawns or fights; stock guards are untouched.
//
// The contract, as the design states it:
//   - A crime is recorded at the event, before anything can move, hide or delete the actor: kind, town, place, time,
//     culprit, victim, and every qualified witness with the culprit's place as they last saw it. Server events supply it;
//     a Criminal flag read later, a player's words or a target someone names supply nothing.
//   - Someone sees an actor only when alive, a player-access mobile, on the same map, within 14 tiles, able to CanSee
//     the actor (not hidden to them), and in actual line of sight of the actor and of the action's place. No facing cone.
//   - A living player saying "guards" reports the most recent incident they themselves witnessed in the last 15 seconds;
//     a caller who saw nothing raises only an investigation at their own spot, with no suspect.
//   - A report is heard only by guards within speech range of the caller: 15 tiles spoken, 18 yelled, 1 whispered, in the
//     same town. Hearing needs no line of sight. It carries the incident and the last seen place, never a live feed.
//
// The pilot town is Cove on Trammel (a small settlement in the design's staffing table). The service is off by default
// and observes only: server/patches/GuardedRegion-witness-observe.patch calls it from the stock crime and guard-call
// paths of a guarded region before the stock response, which still runs. [GuardWitness on (test shard only) switches it
// on; [GuardWitness log shows what it recorded: who would have seen each crime and which guard would have heard each
// call. Stage 2 builds the guard that acts on it (drafted in shard-migration notes/cc-P628-build-guard-patrols-pilot.md).

using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Mobiles;
using Server.Regions;

namespace Server;

public enum IncidentKind
{
    Aggression,
    CriminalAction,
    CriminalAssist,
    Investigation
}

public enum WitnessRole
{
    Guard,
    Civilian,
    Player
}

public sealed class Witness
{
    public Witness(Mobile mobile, WitnessRole role, Point3D lastSeen)
    {
        Mobile = mobile;
        Role = role;
        LastSeen = lastSeen;
    }

    public Mobile Mobile { get; }
    public WitnessRole Role { get; }
    public Point3D LastSeen { get; }
}

public sealed class Incident
{
    public int Id { get; init; }
    public IncidentKind Kind { get; set; }
    public string Town { get; init; }
    public Map Map { get; init; }
    public Point3D Location { get; init; }
    public DateTime Time { get; init; }
    public Mobile Culprit { get; init; }
    public Mobile Victim { get; set; }
    public List<Witness> Witnesses { get; } = new();

    public bool SeenBy(Mobile m) => Witnesses.Exists(w => w.Mobile == m);

    public bool SeenByGuard => Witnesses.Exists(w => w.Role == WitnessRole.Guard);
}

public sealed class GuardReport
{
    public Mobile Caller { get; init; }
    public DateTime Time { get; init; }
    public Point3D Location { get; init; }
    public Incident Incident { get; init; }   // null: an investigation, no suspect
    public List<Mobile> HeardBy { get; } = new();
}

/// <summary>One town on one facet, by its existing region names. Separate facets never share a jurisdiction.</summary>
public sealed record GuardJurisdiction(string Id, Map Map, string[] RegionNames)
{
    public bool Covers(Region region)
    {
        if (region == null || region.Map != Map)
        {
            return false;
        }

        for (var r = region; r != null; r = r.Parent)
        {
            if (Array.IndexOf(RegionNames, r.Name) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}

public static class ClusterFGuardWitness
{
    public const int SightRange = 14;
    public const int SpeechRange = 15;
    public const int YellRange = 18;
    public const int WhisperRange = 1;
    public const int MaxIncidentsPerTown = 64;
    public static readonly TimeSpan ReportWindow = TimeSpan.FromSeconds(15.0);
    public static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds(1.0);
    public static readonly TimeSpan CallerLimit = TimeSpan.FromSeconds(2.0);
    public const int MaxReportsKept = 64;

    /// <summary>The pilot manifest: Cove, Trammel (Distribution/Data/regions.json, two areas, priority 50).</summary>
    public static readonly List<GuardJurisdiction> Jurisdictions = new()
    {
        new GuardJurisdiction("cove-trammel", Map.Trammel, ["Cove"])
    };

    public static bool Enabled { get; set; }

    private static readonly Dictionary<string, List<Incident>> _incidents = new();
    private static readonly List<GuardReport> _reports = new();
    private static readonly Dictionary<Mobile, DateTime> _lastCall = new();
    private static int _nextId;

    public static void Configure()
    {
        CommandSystem.Register("GuardWitness", AccessLevel.Administrator, OnCommand);
    }

    /// <summary>Tests only: forget everything, switch off, restore the pilot manifest.</summary>
    public static void ResetForTests()
    {
        Enabled = false;
        _incidents.Clear();
        _reports.Clear();
        _lastCall.Clear();
        Jurisdictions.Clear();
        Jurisdictions.Add(new GuardJurisdiction("cove-trammel", Map.Trammel, ["Cove"]));
    }

    public static GuardJurisdiction JurisdictionOf(Region region)
    {
        foreach (var j in Jurisdictions)
        {
            if (j.Covers(region))
            {
                return j;
            }
        }

        return null;
    }

    public static IReadOnlyList<Incident> IncidentsIn(string town) =>
        _incidents.TryGetValue(town, out var list) ? list : Array.Empty<Incident>();

    public static IReadOnlyList<GuardReport> Reports => _reports;

    // -- Sight ----------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Whether <paramref name="observer"/> sees <paramref name="actor"/> now: alive, player access (staff sight is never a
    /// guard's), same map, within 14 tiles, CanSee (a hidden actor is unseen), and line of sight to the actor and to the
    /// place of the action where it differs.
    /// </summary>
    public static bool Sees(Mobile observer, Mobile actor, Point3D actionAt)
    {
        if (observer == null || actor == null || observer == actor || observer.Deleted || actor.Deleted || !observer.Alive)
        {
            return false;
        }

        if (observer.AccessLevel > AccessLevel.Player || observer.Map == null || observer.Map != actor.Map || actor.Hidden)
        {
            return false;
        }

        if (!observer.InRange(actor, SightRange) || !observer.CanSee(actor) || !observer.InLOS(actor))
        {
            return false;
        }

        return actionAt == actor.Location || observer.InRange(actionAt, SightRange) && observer.InLOS(actionAt);
    }

    public static bool IsGuard(Mobile m) => m is BaseGuard;

    /// <summary>A townsperson who may witness: a human, uncontrolled, unsummoned creature of the town (not a guard).</summary>
    public static bool IsCivilian(Mobile m) =>
        m is BaseCreature { Controlled: false, Summoned: false } bc && !bc.IsDeadBondedPet && bc.IsHumanInTown();

    private static WitnessRole? RoleOf(Mobile m)
    {
        if (IsGuard(m))
        {
            return WitnessRole.Guard;
        }

        if (m.Player)
        {
            return WitnessRole.Player;
        }

        return IsCivilian(m) ? WitnessRole.Civilian : null;
    }

    // -- Incidents ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// Records a crime at its event, with every qualified witness. Null when the service is off or the region is not a
    /// pilot jurisdiction. The same culprit's crimes within one second at one town are one incident (an attack raises
    /// both the aggression and the criminal callbacks).
    /// </summary>
    public static Incident Record(IncidentKind kind, Mobile culprit, Mobile victim, Region region)
    {
        if (!Enabled || culprit == null || culprit.Deleted || culprit.Map == null)
        {
            return null;
        }

        var town = JurisdictionOf(region);
        if (town == null)
        {
            return null;
        }

        if (!_incidents.TryGetValue(town.Id, out var list))
        {
            _incidents[town.Id] = list = new List<Incident>();
        }

        var location = culprit.Location;
        var incident = list.FindLast(i => i.Culprit == culprit && Core.Now - i.Time <= MergeWindow);
        if (incident == null)
        {
            incident = new Incident
            {
                Id = ++_nextId,
                Kind = kind,
                Town = town.Id,
                Map = culprit.Map,
                Location = location,
                Time = Core.Now,
                Culprit = culprit,
                Victim = victim
            };

            list.Add(incident);
            if (list.Count > MaxIncidentsPerTown)
            {
                list.RemoveAt(0);
            }
        }
        else
        {
            if (kind == IncidentKind.Aggression)
            {
                incident.Kind = kind;
            }

            incident.Victim ??= victim;
        }

        var found = new List<Mobile>();
        foreach (var m in culprit.Map.GetMobilesInRange(location, SightRange))
        {
            found.Add(m);
        }

        foreach (var m in found)
        {
            if (incident.SeenBy(m) || RoleOf(m) is not { } role || !Sees(m, culprit, location))
            {
                continue;
            }

            incident.Witnesses.Add(new Witness(m, role, location));
        }

        return incident;
    }

    // -- Reports ------------------------------------------------------------------------------------------------------

    public static int HearingRange(MessageType type) =>
        type switch
        {
            MessageType.Yell    => YellRange,
            MessageType.Whisper => WhisperRange,
            _                   => SpeechRange
        };

    /// <summary>
    /// A living player's call for the guards, in a pilot town: the most recent incident they witnessed in the last 15
    /// seconds (or an investigation at their spot when they saw none), heard by the guards within speech range. Null when
    /// the service is off, the caller is not a living player, outside a pilot town, or called within the last two seconds.
    /// </summary>
    public static GuardReport Report(Mobile caller, MessageType type, Region region)
    {
        if (!Enabled || caller == null || !caller.Player || !caller.Alive || caller.Map == null)
        {
            return null;
        }

        var town = JurisdictionOf(region);
        if (town == null)
        {
            return null;
        }

        if (_lastCall.TryGetValue(caller, out var last) && Core.Now - last < CallerLimit)
        {
            return null;
        }

        _lastCall[caller] = Core.Now;

        Incident seen = null;
        foreach (var i in IncidentsIn(town.Id))
        {
            if (Core.Now - i.Time <= ReportWindow && i.SeenBy(caller) && (seen == null || i.Time >= seen.Time))
            {
                seen = i;
            }
        }

        var report = new GuardReport { Caller = caller, Time = Core.Now, Location = caller.Location, Incident = seen };

        var range = HearingRange(type);
        var found = new List<Mobile>();
        foreach (var m in caller.Map.GetMobilesInRange(caller.Location, range))
        {
            found.Add(m);
        }

        foreach (var m in found)
        {
            if (IsGuard(m) && m.Alive && !m.Deleted && caller.InRange(m, range) && town.Covers(m.Region))
            {
                report.HeardBy.Add(m);
            }
        }

        _reports.Add(report);
        if (_reports.Count > MaxReportsKept)
        {
            _reports.RemoveAt(0);
        }

        return report;
    }

    // -- The observe-only hooks (GuardedRegion-witness-observe.patch) ---------------------------------------------------

    public static void OnAggressed(GuardedRegion region, Mobile aggressor, Mobile aggressed) =>
        Record(IncidentKind.Aggression, aggressor, aggressed, region);

    public static void OnCriminalAction(GuardedRegion region, Mobile m) =>
        Record(IncidentKind.CriminalAction, m, null, region);

    public static void OnCriminalAssist(GuardedRegion region, Mobile helper, Mobile helped) =>
        Record(IncidentKind.CriminalAssist, helper, helped, region);

    public static void OnGuardsCalled(GuardedRegion region, SpeechEventArgs args) =>
        Report(args.Mobile, args.Type, region);

    // -- Staff --------------------------------------------------------------------------------------------------------

    [Usage("GuardWitness [status|on|off|log]")]
    [Description("Guard witness pilot (cc-P628, test shard only): switch the observe-only witness service on or off in Cove, Trammel, and list what it recorded.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.TestOnly)]
    private static void OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var arg = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";

        switch (arg)
        {
            case "on":
                Enabled = true;
                break;
            case "off":
                Enabled = false;
                break;
            case "log":
                foreach (var j in Jurisdictions)
                {
                    foreach (var i in IncidentsIn(j.Id))
                    {
                        var names = string.Join(", ", i.Witnesses.ConvertAll(w => $"{w.Mobile.Name} ({w.Role})"));
                        from.SendMessage(
                            $"#{i.Id} {i.Kind} by {i.Culprit?.Name} at {i.Location}, {(Core.Now - i.Time).TotalSeconds:F0}s ago; " +
                            $"seen by {(names.Length > 0 ? names : "nobody")}"
                        );
                    }
                }

                foreach (var r in _reports)
                {
                    var heard = string.Join(", ", r.HeardBy.ConvertAll(g => g.Name));
                    from.SendMessage(
                        $"Call by {r.Caller?.Name}: {(r.Incident != null ? $"incident #{r.Incident.Id}" : "investigation, no suspect")}; " +
                        $"heard by {(heard.Length > 0 ? heard : "no guard")}"
                    );
                }

                return;
        }

        var count = 0;
        foreach (var j in Jurisdictions)
        {
            count += IncidentsIn(j.Id).Count;
        }

        from.SendMessage($"Guard witness pilot is {(Enabled ? "on" : "off")} (observe only; stock guards still answer). " +
                         $"{count} incident(s), {_reports.Count} call(s) recorded.");
    }
}
