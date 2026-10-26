// cc-P625: Tracking Ping, stage 1 of shard-migration notes/cc-P563-tracking-ping-design.md (Chase 2026-10-25, option C:
// live moving dots on the mini map for every found creature, same floor only, limits on showing players, no exact spot
// for hidden ones; chat count as the fallback). An invented shard ability, not an OSI port.
//
// Stage 1 is the server's half with no client change: who a ping finds, what it costs, the chat count every client gets,
// and a paged results gump. At 90 Base Tracking (trained skill, never gear) a player focuses for a second, then pays 15
// stamina, is revealed, and the ping holds for 6 to 10 seconds, re-reading its contacts once a second. Later stages add
// authored dungeon floors, picking a quarry for the arrow, the client capability and the mini map and world map dots
// (drafted in shard-migration notes/cc-P625-build-tracking-ping.md).
//
// Two entry points share every check: [TrackingPing, and a small companion menu the Tracking skill sends beside the stock
// one from 90 Base (one call added to pinned Skills/Tracking/Tracking.cs by server/patches/Tracking-ping-offer.patch).
//
// Who is found: living, not hidden, not staff, on the caster's facet, inside the square radius around the spot the ping
// fired from, within 16 Z of it, outside any dungeon (no dungeon floor is mapped yet, so a caster in one is refused and
// nobody in one is found) and outside every house but the caster's own. Players only from 120 Base, then only within 12
// tiles of the caster's current spot and in line of sight, each warned once. A pet or summon of another player who is not
// in the caster's party follows the same player rule and is never found while its owner is hidden. Ping grants no skill
// gain and never reveals anyone.

using System;
using System.Collections.Generic;
using System.Text;
using Server.Collections;
using Server.Commands;
using Server.Engines.PartySystem;
using Server.Gumps;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Regions;

namespace Server;

public enum PingGroup
{
    Hostile,
    NeutralAnimal,
    NeutralOther,
    Friendly,
    Player
}

public enum PingThreat
{
    None,
    EasyOrBelow,
    FairEven,
    Tough,
    DangerousDeadly
}

public sealed class PingContact
{
    public PingContact(Mobile mobile, PingGroup group, PingThreat threat, string label)
    {
        Mobile = mobile;
        Group = group;
        Threat = threat;
        Label = label;
    }

    public Mobile Mobile { get; }
    public PingGroup Group { get; }
    public PingThreat Threat { get; }
    public string Label { get; }
}

public static class ClusterFTrackingPing
{
    public const double MinBase = 90.0;
    public const double PlayerBase = 120.0;
    public const int PlayerRange = 12;
    public const int StaminaCost = 15;
    public const int ZTolerance = 16;

    public static readonly TimeSpan FocusDelay = TimeSpan.FromSeconds(1.0);
    public static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(30.0);
    public static readonly TimeSpan SkillDelay = TimeSpan.FromSeconds(10.0);
    public static readonly TimeSpan WarnInterval = TimeSpan.FromSeconds(10.0);

    public const string WatchedWarning = "You feel watched. Someone nearby is reading the trail.";
    public const string EmptyScan = "Ping: no eligible contacts on this floor.";
    public const string UnmappedFloor = "Ping unavailable here: dungeon floor not mapped.";
    public const string Overloaded = "Too many trails to read here.";
    public const string Ended = "Ping ended.";

    // An operational safeguard, not a number players are told: above it the ping is cancelled and costs nothing.
    public static int OverloadCeiling { get; set; } = 1024;

    // Every line Ping says to a player passes through here as well, so the shard's tests can read them.
    public static Action<Mobile, string> Heard { get; set; }

    private static readonly Dictionary<Mobile, FocusTimer> _focus = new();
    private static readonly Dictionary<Mobile, PingPulse> _pulses = new();
    private static readonly Dictionary<Mobile, DateTime> _cooldownUntil = new();
    private static readonly Dictionary<Mobile, DateTime> _lastWarned = new();
    private static int _nextPulseId;

    public static void Configure()
    {
        CommandSystem.Register("TrackingPing", AccessLevel.Player, OnCommand);
    }

    public static void Initialize()
    {
        EventSink.Logout += m => Stop(m);
        EventSink.Disconnected += m => Stop(m);
    }

    [Usage("TrackingPing [results]")]
    [Description("From 90 Tracking: sense the creatures around you for a few seconds. 'results' lists what the ping found.")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm)
        {
            return;
        }

        if (e.Length > 0 && e.GetString(0).InsensitiveEquals("results"))
        {
            ShowResults(pm);
            return;
        }

        Begin(pm, false);
    }

    // -- The numbers --------------------------------------------------------------------------------------------------

    // Skills keep Base in tenths, so integer tenths keep each step exact (0.6 x 10 is not exactly 6 in a double).
    private static int Tenths(double baseSkill) => (int)Math.Round(Math.Min(baseSkill, 200.0) * 10.0);

    /// <summary>Square radius in tiles: 18 at 90, 24 at 100, then one more per 5 points, 44 at 200. 0 below 90.</summary>
    public static int Radius(double baseSkill)
    {
        var t = Tenths(baseSkill);
        if (t < 900)
        {
            return 0;
        }

        return t <= 1000 ? 18 + 6 * (t - 900) / 100 : 24 + (t - 1000) / 50;
    }

    /// <summary>Seconds a ping holds: 6, plus one per 25 points past 100, 10 at 200. 0 below 90.</summary>
    public static int Lifetime(double baseSkill)
    {
        var t = Tenths(baseSkill);
        return t < 900 ? 0 : 6 + Math.Max(0, t - 1000) / 250;
    }

    // -- State the tests and the gumps read ---------------------------------------------------------------------------

    public static bool IsFocusing(Mobile m) => _focus.ContainsKey(m);

    public static bool IsActive(Mobile m) => _pulses.ContainsKey(m);

    public static IReadOnlyList<PingContact> ContactsOf(Mobile m) =>
        _pulses.TryGetValue(m, out var pulse) ? pulse.Contacts : Array.Empty<PingContact>();

    public static int SecondsLeft(Mobile m) =>
        _pulses.TryGetValue(m, out var pulse) ? Math.Max(0, (int)Math.Ceiling((pulse.End - Core.Now).TotalSeconds)) : 0;

    public static TimeSpan CooldownLeft(Mobile m) =>
        _cooldownUntil.TryGetValue(m, out var until) && until > Core.Now ? until - Core.Now : TimeSpan.Zero;

    /// <summary>Forget every ping, focus, cooldown and warning (tests only; the server never calls it).</summary>
    public static void ResetAll()
    {
        foreach (var f in new List<FocusTimer>(_focus.Values))
        {
            f.Break(null);
        }

        foreach (var p in new List<PingPulse>(_pulses.Values))
        {
            p.Stop();
        }

        _focus.Clear();
        _pulses.Clear();
        _cooldownUntil.Clear();
        _lastWarned.Clear();
    }

    private static void Tell(Mobile m, int hue, string text)
    {
        m.SendMessage(hue, text);
        Heard?.Invoke(m, text);
    }

    // -- Starting a ping ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Starts the one second focus. fromSkillUse is true only for the companion menu the Tracking skill itself sent: that
    /// use already passed the skill delay check and set its own 10 seconds, so checking it again would refuse every press.
    /// </summary>
    public static bool Begin(PlayerMobile pm, bool fromSkillUse)
    {
        if (pm == null || pm.Deleted)
        {
            return false;
        }

        if (!pm.Alive)
        {
            Tell(pm, 0x3B2, "You cannot read a trail while dead.");
            return false;
        }

        if (pm.Skills.Tracking.Base < MinBase)
        {
            Tell(pm, 0x3B2, "You need 90 Tracking, trained rather than from gear, to ping.");
            return false;
        }

        if (_focus.ContainsKey(pm) || _pulses.ContainsKey(pm))
        {
            Tell(pm, 0x3B2, "You are already reading the trail.");
            return false;
        }

        var wait = CooldownLeft(pm);
        if (wait > TimeSpan.Zero)
        {
            Tell(pm, 0x3B2, $"You must wait {(int)Math.Ceiling(wait.TotalSeconds)} seconds before you can ping again.");
            return false;
        }

        if (!fromSkillUse && Core.TickCount < pm.NextSkillTime)
        {
            Tell(pm, 0x3B2, "You must wait a few moments to use another skill.");
            return false;
        }

        if (pm.Stam < StaminaCost)
        {
            Tell(pm, 0x3B2, "You are too tired to ping.");
            return false;
        }

        if (pm.Map == null || pm.Map == Map.Internal)
        {
            return false;
        }

        if (InUnmappedDungeon(pm))
        {
            Tell(pm, 0x3B2, UnmappedFloor);
            return false;
        }

        if (pm.Spell != null)
        {
            Tell(pm, 0x3B2, "You cannot ping while casting.");
            return false;
        }

        var focus = new FocusTimer(pm);
        _focus[pm] = focus;
        pm.Spell = focus;
        focus.Start();
        Tell(pm, 0x59, "You still yourself and read the trail...");
        return true;
    }

    private static bool InUnmappedDungeon(Mobile m) => m.Region?.IsPartOf<DungeonRegion>() == true;

    public const string BrokenFocus = "Your concentration breaks. The ping costs you nothing.";

    // The focus sits in the caster's spell slot for its second, so pinned's Mobile.Damage (OnCasterHurt), a step
    // (OnCasterMoving), death and a disconnect reach it as they reach a spell being cast. Reading hit points or damage
    // entries afterwards would miss a hit healed back to full within the second (Mobile.cs clears the entries at full hits).
    private sealed class FocusTimer : Timer, ISpell
    {
        private readonly PlayerMobile _pm;
        private readonly Point3D _location;
        private readonly Map _map;

        public FocusTimer(PlayerMobile pm) : base(FocusDelay)
        {
            _pm = pm;
            _location = pm.Location;
            _map = pm.Map;
        }

        public bool BlocksMovement => false;
        public bool IsCasting => true;

        public void OnCasterHurt() => Break(BrokenFocus);
        public void OnCasterKilled() => Break(null);
        public void OnConnectionChanged() => Break(null);

        public bool OnCasterMoving(Direction d)
        {
            Break(BrokenFocus);
            return true;
        }

        public bool OnCasterEquipping(Item item) => true;
        public bool OnCasterUsingObject(IEntity entity) => true;
        public bool OnCastInTown(Region r) => true;

        public void FinishSequence()
        {
            if (_pm.Spell == this)
            {
                _pm.Spell = null;
            }
        }

        public void Break(string message)
        {
            Stop();
            if (_focus.TryGetValue(_pm, out var current) && current == this)
            {
                _focus.Remove(_pm);
            }

            FinishSequence();
            if (message != null && !_pm.Deleted)
            {
                Tell(_pm, 0x3B2, message);
            }
        }

        protected override void OnTick()
        {
            if (!_focus.TryGetValue(_pm, out var current) || current != this)
            {
                FinishSequence();
                return;
            }

            if (_pm.Deleted || !_pm.Alive)
            {
                Break(null);
                return;
            }

            if (_pm.Map != _map || _pm.Location != _location)
            {
                Break(BrokenFocus);
                return;
            }

            _focus.Remove(_pm);
            FinishSequence();
            Fire(_pm);
        }
    }

    // After a completed focus: recheck what may have changed in that second, read the contacts, then pay.
    private static void Fire(PlayerMobile pm)
    {
        var capturedBase = pm.Skills.Tracking.Base;

        if (capturedBase < MinBase || pm.Stam < StaminaCost || InUnmappedDungeon(pm))
        {
            Tell(pm, 0x3B2, InUnmappedDungeon(pm) ? UnmappedFloor : "The trail slips away. The ping costs you nothing.");
            return;
        }

        var origin = pm.Location;
        var radius = Radius(capturedBase);
        var contacts = Scan(pm, origin, radius, capturedBase);

        if (contacts == null)
        {
            Tell(pm, 0x3B2, Overloaded);
            return;
        }

        pm.RevealingAction();
        pm.Stam -= StaminaCost;
        _cooldownUntil[pm] = Core.Now + Cooldown;
        pm.NextSkillTime = Math.Max(pm.NextSkillTime, Core.TickCount + (long)SkillDelay.TotalMilliseconds);

        var lifetime = Lifetime(capturedBase);
        var pulse = new PingPulse(pm, origin, radius, capturedBase, lifetime, ++_nextPulseId);
        _pulses[pm] = pulse;
        pulse.Accept(contacts);

        Tell(pm, 0x59, Summary(contacts, lifetime));
        pulse.Start();
    }

    // -- Who a ping finds ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Every eligible contact around origin, grouped and banded; null when there are more than OverloadCeiling.
    /// The chat count, the gump and (later) the map dots all read this one list.
    /// </summary>
    public static List<PingContact> Scan(PlayerMobile caster, Point3D origin, int radius, double capturedBase)
    {
        var map = caster.Map;
        var result = new List<PingContact>();

        if (map == null || map == Map.Internal || radius <= 0)
        {
            return result;
        }

        var party = Party.Get(caster);
        var casterHouse = BaseHouse.FindHouseAt(caster);
        var found = new List<Mobile>();

        foreach (var m in map.GetMobilesInRange(origin, radius))
        {
            found.Add(m);
        }

        foreach (var m in found)
        {
            if (!IsEligible(caster, m, origin, radius, capturedBase, party, casterHouse))
            {
                continue;
            }

            if (result.Count >= OverloadCeiling)
            {
                return null;
            }

            var group = Classify(caster, m, party);
            var threat = group is PingGroup.Player or PingGroup.Friendly ? PingThreat.None : ThreatOf(caster, m);
            result.Add(new PingContact(m, group, threat, Label(caster, m)));
        }

        result.Sort((a, b) =>
            a.Group != b.Group ? a.Group.CompareTo(b.Group) : caster.GetDistanceToSqrt(a.Mobile).CompareTo(caster.GetDistanceToSqrt(b.Mobile))
        );

        return result;
    }

    public static bool IsEligible(
        PlayerMobile caster, Mobile m, Point3D origin, int radius, double capturedBase, Party party, BaseHouse casterHouse
    )
    {
        if (m == null || m == caster || m.Deleted || !m.Alive || m.Hidden || m.Map != caster.Map || m.Map == Map.Internal)
        {
            return false;
        }

        if (m.AccessLevel > AccessLevel.Player || m is BaseCreature { IsDeadBondedPet: true })
        {
            return false;
        }

        if (Math.Abs(m.X - origin.X) > radius || Math.Abs(m.Y - origin.Y) > radius || Math.Abs(m.Z - origin.Z) > ZTolerance)
        {
            return false;
        }

        if (InUnmappedDungeon(m))
        {
            return false;
        }

        var house = BaseHouse.FindHouseAt(m);
        if (house != null && house != casterHouse)
        {
            return false;
        }

        if (m.Player)
        {
            return PlayerVisible(caster, m, capturedBase);
        }

        if (m is BaseCreature bc && (bc.Controlled || bc.Summoned))
        {
            var master = bc.GetMaster();

            // Unresolvable ownership fails closed.
            if (master == null || master.Deleted)
            {
                return false;
            }

            if (master.Player && master != caster && party?.Contains(master) != true)
            {
                // Another player's pet would show where its owner is: the owner's rules apply to it.
                if (master.Hidden || master.AccessLevel > AccessLevel.Player)
                {
                    return false;
                }

                return PlayerVisible(caster, m, capturedBase);
            }
        }

        return true;
    }

    // Players (and the pets that would betray them) only from 120, within 12 tiles of where the caster stands now, in sight.
    private static bool PlayerVisible(PlayerMobile caster, Mobile m, double capturedBase) =>
        capturedBase >= PlayerBase && caster.InRange(m, PlayerRange) && caster.InLOS(m);

    public static PingGroup Classify(PlayerMobile caster, Mobile m, Party party)
    {
        if (m.Player)
        {
            return PingGroup.Player;
        }

        if (m is not BaseCreature bc)
        {
            return PingGroup.NeutralOther;
        }

        var master = bc.GetMaster();
        if (master is { Player: true })
        {
            if (master == caster || party?.Contains(master) == true)
            {
                return PingGroup.Friendly;
            }

            if (IsActiveAggressor(caster, party, master) || IsActiveAggressor(caster, party, bc))
            {
                return PingGroup.Hostile;
            }

            return Neutral(bc);
        }

        if (!bc.IsInvulnerable && (IsActiveAggressor(caster, party, bc) || WouldAttack(bc, caster)))
        {
            return PingGroup.Hostile;
        }

        return Neutral(bc);
    }

    private static PingGroup Neutral(BaseCreature bc) => bc.Body.IsAnimal ? PingGroup.NeutralAnimal : PingGroup.NeutralOther;

    // Body.IsMonster alone is not hostility: a creature that only answers attacks (FightMode Aggressor, most animals) or
    // never fights is neutral until it is an active aggressor. An Evil creature only goes for negative karma.
    private static bool WouldAttack(BaseCreature bc, Mobile caster) =>
        bc.FightMode switch
        {
            FightMode.None or FightMode.Aggressor => false,
            FightMode.Evil                        => caster.Karma < 0 && bc.IsEnemy(caster),
            _                                     => bc.IsEnemy(caster)
        };

    private static bool IsActiveAggressor(PlayerMobile caster, Party party, Mobile m)
    {
        if (Attacks(caster, m))
        {
            return true;
        }

        if (party != null)
        {
            foreach (var info in party.Members)
            {
                if (info.Mobile != null && info.Mobile != caster && Attacks(info.Mobile, m))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool Attacks(Mobile victim, Mobile m)
    {
        foreach (var info in victim.Aggressors)
        {
            if (info.Attacker == m && !info.Expired)
            {
                return true;
            }
        }

        return false;
    }

    // ClusterFCreatureLevels' con color, folded into four words. No hit points, resists or loot.
    private static PingThreat ThreatOf(Mobile caster, Mobile m) =>
        ClusterFCreatureLevels.Con(caster, m) switch
        {
            ConColor.Grey or ConColor.Green  => PingThreat.EasyOrBelow,
            ConColor.Blue or ConColor.Yellow => PingThreat.FairEven,
            ConColor.Orange                  => PingThreat.Tough,
            _                                => PingThreat.DangerousDeadly
        };

    private static string Label(PlayerMobile caster, Mobile m)
    {
        if (m.Player)
        {
            return m.Name ?? "someone";
        }

        if (m is BaseCreature bc && bc.GetMaster() is { Player: true } master)
        {
            return master == caster ? "your pet" : "a pet";
        }

        return KindOf(m);
    }

    // A broad kind from the creature's type ("Orc" orc, "GiantSpider" giant spider), never its own name: an orc called
    // Zupgugh, a renamed pet or a paragon's title is not something a remote sense should read.
    public static string KindOf(Mobile m)
    {
        var type = m.GetType().Name;
        var sb = new StringBuilder(type.Length + 4);
        for (var i = 0; i < type.Length; i++)
        {
            if (i > 0 && char.IsUpper(type[i]) && !char.IsUpper(type[i - 1]))
            {
                sb.Append(' ');
            }

            sb.Append(char.ToLowerInvariant(type[i]));
        }

        return sb.ToString();
    }

    // -- Words --------------------------------------------------------------------------------------------------------

    public static string GroupWord(PingGroup group, int count) =>
        group switch
        {
            PingGroup.Hostile       => count == 1 ? "hostile" : "hostiles",
            PingGroup.NeutralAnimal => count == 1 ? "neutral animal" : "neutral animals",
            PingGroup.NeutralOther  => count == 1 ? "neutral NPC" : "neutral NPCs",
            PingGroup.Friendly      => count == 1 ? "friendly" : "friendlies",
            _                       => count == 1 ? "player" : "players"
        };

    public static string ThreatWord(PingThreat threat) =>
        threat switch
        {
            PingThreat.EasyOrBelow     => "easy or below",
            PingThreat.FairEven        => "fair/even",
            PingThreat.Tough           => "tough",
            PingThreat.DangerousDeadly => "dangerous/deadly",
            _                          => ""
        };

    public static string FlagWord(int notoriety) =>
        notoriety switch
        {
            Notoriety.Innocent      => "innocent",
            Notoriety.Ally          => "ally",
            Notoriety.CanBeAttacked => "gray",
            Notoriety.Criminal      => "criminal",
            Notoriety.Enemy         => "enemy",
            Notoriety.Murderer      => "murderer",
            _                       => "invulnerable"
        };

    /// <summary>"Ping: 7 hostiles, 3 neutral animals, 2 friendlies, 1 player. Hostiles: 4 fair/even, 2 tough. 6 seconds."</summary>
    public static string Summary(IReadOnlyList<PingContact> contacts, int lifetime)
    {
        if (contacts.Count == 0)
        {
            return EmptyScan;
        }

        var groups = new int[5];
        var bands = new int[5];
        foreach (var c in contacts)
        {
            groups[(int)c.Group]++;
            if (c.Group == PingGroup.Hostile)
            {
                bands[(int)c.Threat]++;
            }
        }

        var sb = new StringBuilder("Ping: ");
        var first = true;
        for (var g = 0; g < groups.Length; g++)
        {
            if (groups[g] == 0)
            {
                continue;
            }

            sb.Append(first ? "" : ", ").Append(groups[g]).Append(' ').Append(GroupWord((PingGroup)g, groups[g]));
            first = false;
        }

        sb.Append('.');

        if (groups[(int)PingGroup.Hostile] > 0)
        {
            sb.Append(" Hostiles: ");
            first = true;
            for (var b = (int)PingThreat.EasyOrBelow; b < bands.Length; b++)
            {
                if (bands[b] == 0)
                {
                    continue;
                }

                sb.Append(first ? "" : ", ").Append(bands[b]).Append(' ').Append(ThreatWord((PingThreat)b));
                first = false;
            }

            sb.Append('.');
        }

        sb.Append(' ').Append(lifetime).Append(" seconds.");
        return sb.ToString();
    }

    // -- The pulse ----------------------------------------------------------------------------------------------------

    private sealed class PingPulse : Timer
    {
        private readonly PlayerMobile _pm;
        private readonly Point3D _origin;
        private readonly Map _map;
        private readonly int _radius;
        private readonly double _capturedBase;
        private readonly int _lifetime;
        private readonly HashSet<Mobile> _warned = new();
        private int _elapsed;

        public PingPulse(PlayerMobile pm, Point3D origin, int radius, double capturedBase, int lifetime, int id)
            : base(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0))
        {
            _pm = pm;
            _origin = origin;
            _map = pm.Map;
            _radius = radius;
            _capturedBase = capturedBase;
            _lifetime = lifetime;
            Id = id;
            End = Core.Now + TimeSpan.FromSeconds(lifetime);
        }

        public int Id { get; }
        public DateTime End { get; }
        public List<PingContact> Contacts { get; private set; } = new();

        // A player is warned before their first contact goes out, once a pulse, at most once per 10 s across trackers.
        public void Accept(List<PingContact> contacts)
        {
            foreach (var c in contacts)
            {
                if (c.Group != PingGroup.Player || !_warned.Add(c.Mobile))
                {
                    continue;
                }

                if (!_lastWarned.TryGetValue(c.Mobile, out var last) || Core.Now - last >= WarnInterval)
                {
                    _lastWarned[c.Mobile] = Core.Now;
                    Tell(c.Mobile, 0x22, WatchedWarning);
                }
            }

            Contacts = contacts;
        }

        protected override void OnTick()
        {
            if (!_pulses.TryGetValue(_pm, out var current) || current != this)
            {
                Stop();
                return;
            }

            _elapsed++;

            if (_elapsed >= _lifetime)
            {
                Finish(Ended);
                return;
            }

            var why = Broken();
            if (why != null)
            {
                Finish(why);
                return;
            }

            var contacts = Scan(_pm, _origin, _radius, _capturedBase);
            if (contacts == null)
            {
                Finish(Overloaded);
                return;
            }

            Accept(contacts);
        }

        private string Broken()
        {
            if (_pm.Deleted || !_pm.Alive)
            {
                return "Your ping fades.";
            }

            if (_pm.Map != _map || Math.Abs(_pm.Z - _origin.Z) > ZTolerance || InUnmappedDungeon(_pm))
            {
                return "You have left the floor you read. Your ping fades.";
            }

            if (Math.Abs(_pm.X - _origin.X) > _radius || Math.Abs(_pm.Y - _origin.Y) > _radius)
            {
                return "You have left the area you read. Your ping fades.";
            }

            if (_pm.Skills.Tracking.Base < _capturedBase)
            {
                return "Your tracking falters. Your ping fades.";
            }

            return null;
        }

        public void Finish(string message)
        {
            Stop();
            _pulses.Remove(_pm);
            Contacts = new List<PingContact>();
            if (message != null && !_pm.Deleted)
            {
                Tell(_pm, 0x59, message);
            }
        }
    }

    /// <summary>Ends a focus or ping silently (logout, disconnect).</summary>
    public static void Stop(Mobile m)
    {
        if (m == null)
        {
            return;
        }

        if (_focus.TryGetValue(m, out var focus))
        {
            focus.Break(null);
        }

        if (_pulses.TryGetValue(m, out var pulse))
        {
            pulse.Finish(null);
        }
    }

    // -- Gumps --------------------------------------------------------------------------------------------------------

    /// <summary>Called by the patched Tracking skill after its stock menu: from 90 Base, a small Ping menu beside it.</summary>
    public static bool OfferFromTracking(PlayerMobile pm)
    {
        if (pm == null || pm.Skills.Tracking.Base < MinBase)
        {
            return false;
        }

        pm.CloseGump<PingOfferGump>();
        pm.SendGump(new PingOfferGump());
        return true;
    }

    public static void ShowResults(PlayerMobile pm, int page = 0)
    {
        pm.CloseGump<PingResultsGump>();
        pm.SendGump(new PingResultsGump(pm, page));
    }

    public sealed class PingOfferGump : DynamicGump
    {
        public const int PingButton = 1;
        public const int ResultsButton = 2;

        public PingOfferGump() : base(20, 170)
        {
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            builder.AddPage();
            builder.AddBackground(0, 0, 440, 70, 5054);
            builder.AddBackground(10, 10, 420, 50, 3000);
            builder.AddButton(20, 15, 4005, 4007, PingButton);
            builder.AddHtml(55, 16, 160, 20, "Ping: sense creatures near you");
            builder.AddButton(230, 15, 4011, 4013, ResultsButton);
            builder.AddHtml(265, 16, 160, 20, "Ping results");
            builder.AddHtml(20, 38, 400, 20, "15 stamina, once every 30 seconds. Reveals you.");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile is not PlayerMobile pm)
            {
                return;
            }

            if (info.ButtonID == PingButton)
            {
                Begin(pm, true);
            }
            else if (info.ButtonID == ResultsButton)
            {
                ShowResults(pm);
            }
        }
    }

    public sealed class PingResultsGump : DynamicGump
    {
        public const int RefreshButton = 1;
        public const int PreviousButton = 2;
        public const int NextButton = 3;
        public const int RowsPerPage = 10;

        private readonly PlayerMobile _pm;
        private readonly int _page;

        public PingResultsGump(PlayerMobile pm, int page) : base(60, 60)
        {
            _pm = pm;
            _page = Math.Max(0, page);
        }

        protected override void BuildLayout(ref DynamicGumpBuilder builder)
        {
            var contacts = ContactsOf(_pm);
            var pages = Math.Max(1, (contacts.Count + RowsPerPage - 1) / RowsPerPage);
            var page = Math.Min(_page, pages - 1);

            builder.AddPage();
            builder.AddBackground(0, 0, 420, 330, 5054);
            builder.AddBackground(10, 10, 400, 310, 3000);
            builder.AddHtml(20, 15, 380, 20, "Ping results");

            if (!IsActive(_pm))
            {
                builder.AddHtml(20, 45, 380, 60, "No ping is active. Use [TrackingPing, or Ping on the Tracking menu.");
                return;
            }

            builder.AddHtml(20, 40, 380, 40, Summary(contacts, SecondsLeft(_pm)));

            var y = 85;
            for (var i = page * RowsPerPage; i < contacts.Count && i < (page + 1) * RowsPerPage; i++)
            {
                builder.AddHtml(20, y, 380, 20, RowText(_pm, contacts[i]));
                y += 20;
            }

            builder.AddButton(20, 295, 4005, 4007, RefreshButton);
            builder.AddHtml(55, 296, 80, 20, "Refresh");
            if (page > 0)
            {
                builder.AddButton(250, 295, 4014, 4016, PreviousButton);
            }

            builder.AddHtml(285, 296, 60, 20, $"{page + 1} / {pages}");
            if (page < pages - 1)
            {
                builder.AddButton(350, 295, 4005, 4007, NextButton);
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            switch (info.ButtonID)
            {
                case RefreshButton:
                    ShowResults(_pm, _page);
                    break;
                case PreviousButton:
                    ShowResults(_pm, _page - 1);
                    break;
                case NextButton:
                    ShowResults(_pm, _page + 1);
                    break;
            }
        }
    }

    /// <summary>One gump row: label, group, and the threat band (creatures) or the notoriety flag (players).</summary>
    public static string RowText(PlayerMobile viewer, PingContact c)
    {
        var detail = c.Group == PingGroup.Player
            ? FlagWord(Notoriety.Compute(viewer, c.Mobile))
            : ThreatWord(c.Threat);

        var group = GroupWord(c.Group, 1);
        return detail.Length > 0 ? $"{c.Label} - {group}, {detail}" : $"{c.Label} - {group}";
    }
}
