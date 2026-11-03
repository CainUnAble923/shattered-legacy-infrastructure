// cc-P776: random encounters. Notes in shard-migration notes/cc-P776-random-encounters.md; the idea, the three starters
// and the never-do list are P765's assessment (notes/cc-P765-random-encounters-assessment.md). Our own code: nothing is
// copied from the RunUO engine P765 read.
//
//   The roll     Every TickSeconds (encounters.json), each online player is checked (WhyNot) and, when eligible, rolls
//                Chance; a hit picks one encounter by weight from those the player qualifies for (facet, time of day at
//                the spot, level, League, season) and starts it near them (Start).
//   Never        WhyNot refuses: the off switch; staff; the dead; Young characters and trainees (loop 0); hidden or
//                idle players (no MinTravelTiles move in TravelWindowSeconds: afk); anyone fighting; a character or
//                account inside its cooldown, or already in an encounter; a crowded spot (another encounter within
//                CrowdRange). WhyNotHere refuses the place: the Fold (the Wakening Isle) and its copies, any facet not in
//                Facets, the DenyRegions (Haven Island and New Haven by default), guarded or town regions, dungeons,
//                houses, boats, jail, apprentice training rooms and the isle's own regions. The spot the creatures
//                appear on must pass the same place check, out of the player's line of sight when one allows.
//   Rewards      Creatures carry their own loot. The Toll keeper's top damager gets a loot box through
//                ClusterFLootBoxes.Award (source encounter:toll: once per loop, the repeat line after); the Procession's
//                leader gives Festival Tokens (FestivalTokens.Grant). A Boss (cc-P786, the Halloween witch) pays its own
//                reward. Nothing grants a skill or stat point, nothing is tamable.
//   Cleanup      Sweep, every SweepSeconds: an encounter creature standing anywhere WhyNotHere refuses (a town, a
//                house, the isle) is removed at once, so none follows a player into town; after LeaveAfterMinutes, each
//                one not fighting leaves (fighting ones leave when they stop). A night encounter leaves at dawn. Only
//                the encounter's own creatures are removed: never a corpse, never an item a player holds.
//   Restart      The serials of what is out are saved (ClusterFEncounterState); at world load every one still in the
//                world is removed, so nothing outlives a restart.
//   Commands     [Encounters status | reload | force <id> | check (GameMaster).

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Commands;
using Server.Engines.RewardStores;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Regions;

namespace Server.Encounters;

public enum EncounterStage
{
    /// <summary>Toll: waiting for "pay". Surveyor: the fight is on. Procession: walking.</summary>
    Open,

    /// <summary>Toll: the brigands have turned on the player.</summary>
    Hostile,

    /// <summary>Over: paid, saved, or the leader is down. The rest leave on the next sweep.</summary>
    Done
}

/// <summary>One encounter that is out in the world.</summary>
public sealed class ActiveEncounter
{
    public EncounterDef Def { get; init; } = null!;
    public PlayerMobile Player { get; init; } = null!;
    public Map Map { get; init; } = null!;
    public Point3D Spot { get; init; }
    public DateTime Started { get; init; }
    public DateTime LeaveAt { get; set; }
    public DateTime TollDeadline { get; set; }
    public EncounterStage Stage { get; set; }
    public List<BaseCreature> Creatures { get; } = [];
    public BaseCreature? Leader { get; set; }

    /// <summary>Surveyor: the one to save.</summary>
    public BaseCreature? Ally { get; set; }

    public IEnumerable<BaseCreature> All => Ally == null ? Creatures : Creatures.Append(Ally);

    public bool Has(BaseCreature bc) => bc == Ally || Creatures.Contains(bc);
}

public static class ClusterFEncounters
{
    // -- Seams a fact replaces --

    public static Func<DateTime> Clock { get; set; } = () => Core.Now;
    public static Func<double> Roll { get; set; } = Utility.RandomDouble;

    /// <summary>The hour (0-23) at a spot, the in-game clock's (Clock.GetTime).</summary>
    public static Func<Map, Point3D, int> HourAt { get; set; } = (map, p) =>
    {
        Items.Clock.GetTime(map, p.X, p.Y, out int hours, out int _);
        return hours;
    };

    /// <summary>Whether a season switch (by its loot-table name) is on now.</summary>
    public static Func<string, bool> InSeason { get; set; } = name =>
        ClusterFLootBoxes.SeasonSwitch(name) is { } sw && sw.IsInSeason(ClusterFLootBoxes.SeasonClock());

    public static Func<PlayerMobile, bool> IsLeague { get; set; } = pm => ClusterFLeagueRanks.GetRank(pm) > 0;

    public static Func<PlayerMobile, int> LoopOf { get; set; } = Tutorial.ClusterFLoops.LoopOf;

    /// <summary>Where an encounter's creatures appear, or null when no spot passes. A fact sets a fixed spot.</summary>
    public static Func<PlayerMobile, EncounterTable, Point3D?> FindSpot { get; set; } = DefaultFindSpot;

    // -- State --

    private static readonly List<ActiveEncounter> _active = [];
    private static readonly Dictionary<Serial, DateTime> _characterCooldown = new();
    private static readonly Dictionary<string, DateTime> _accountCooldown = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<Serial, (Map? Map, Point3D At)> _travelSample = new();
    private static readonly Dictionary<Serial, DateTime> _lastTraveled = new();
    private static Timer? _rollTimer;
    private static Timer? _sweepTimer;

    public static IReadOnlyList<ActiveEncounter> Active => _active;

    public static EncounterTable Table => EncounterTable.Current;

    public static void Configure()
    {
        ClusterFEncounterState.Configure();
        CommandSystem.Register("Encounters", AccessLevel.GameMaster, OnCommand);
        EventSink.Speech += OnSpeech;
        EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () =>
        {
            var swept = SweepSaved();
            if (swept > 0)
            {
                Console.WriteLine($"[Encounters] removed {swept} encounter creature{(swept == 1 ? "" : "s")} left from before the restart");
            }

            StartTimers();
        });
    }

    // The table is read here, not in Configure: its checks need the facets and every creature type registered.
    public static void Initialize()
    {
        foreach (var line in EncounterTable.LoadOrWriteDefaults(EncounterTable.FilePath))
        {
            Console.WriteLine($"[Encounters] {line}");
        }
    }

    public static void StartTimers()
    {
        _rollTimer?.Stop();
        _sweepTimer?.Stop();
        _rollTimer = Timer.DelayCall(TimeSpan.FromSeconds(Table.TickSeconds), TimeSpan.FromSeconds(Table.TickSeconds), () => Tick());
        _sweepTimer = Timer.DelayCall(TimeSpan.FromSeconds(Table.SweepSeconds), TimeSpan.FromSeconds(Table.SweepSeconds), () => Sweep());
    }

    /// <summary>Forgets every encounter, cooldown and travel sample (facts start clean). Does not delete creatures.</summary>
    public static void Reset()
    {
        _active.Clear();
        _characterCooldown.Clear();
        _accountCooldown.Clear();
        _travelSample.Clear();
        _lastTraveled.Clear();
        ClusterFEncounterState.Spawned.Clear();
    }

    // -- The roll --

    /// <summary>One tick: every online player's travel is sampled, and each eligible one rolls. Returns what started.</summary>
    public static List<ActiveEncounter> Tick()
    {
        var started = new List<ActiveEncounter>();
        var now = Clock();
        foreach (var ns in NetState.Instances.ToList())
        {
            if (ns.Mobile is PlayerMobile pm)
            {
                var e = TryEncounter(pm, now);
                if (e != null)
                {
                    started.Add(e);
                }
            }
        }

        return started;
    }

    /// <summary>One player's turn: sample travel, check, roll, pick, start. Null when nothing started.</summary>
    public static ActiveEncounter? TryEncounter(PlayerMobile pm, DateTime now)
    {
        NoteTravel(pm, now);
        if (WhyNot(pm, now) != null || Roll() >= Table.Chance)
        {
            return null;
        }

        var def = Pick(pm, now);
        return def == null ? null : Start(pm, def, now);
    }

    /// <summary>Records where the player is; a move of MinTravelTiles or more since the last sample counts as travel now.</summary>
    public static void NoteTravel(PlayerMobile pm, DateTime now)
    {
        if (!_travelSample.TryGetValue(pm.Serial, out var was) || was.Map != pm.Map)
        {
            _travelSample[pm.Serial] = (pm.Map, pm.Location);
            return;
        }

        // The sample stays put until the player is far enough from it, so a slow walk adds up.
        if (Math.Max(Math.Abs(was.At.X - pm.X), Math.Abs(was.At.Y - pm.Y)) >= Table.MinTravelTiles)
        {
            _lastTraveled[pm.Serial] = now;
            _travelSample[pm.Serial] = (pm.Map, pm.Location);
        }
    }

    public static bool Traveled(PlayerMobile pm, DateTime now) =>
        _lastTraveled.TryGetValue(pm.Serial, out var at) && now - at <= TimeSpan.FromSeconds(Table.TravelWindowSeconds);

    public static bool InCombat(Mobile m) =>
        m.Combatant != null || m.Aggressors.Count > 0 || m.Aggressed.Count > 0;

    public static ActiveEncounter? EncounterOf(PlayerMobile pm) => _active.FirstOrDefault(e => e.Player == pm);

    public static ActiveEncounter? EncounterOf(BaseCreature bc) => _active.FirstOrDefault(e => e.Has(bc));

    /// <summary>Why this player cannot have an encounter now, or null when they can.</summary>
    public static string? WhyNot(PlayerMobile pm, DateTime now)
    {
        var t = Table;
        if (!t.Enabled)
        {
            return "encounters are off";
        }

        if (pm.Deleted || pm.Map == null || pm.Map == Map.Internal)
        {
            return "not in the world";
        }

        if (pm.AccessLevel > AccessLevel.Player)
        {
            return "staff";
        }

        if (!pm.Alive)
        {
            return "dead";
        }

        if (pm.Young || pm.Account is Account { Young: true })
        {
            return "Young";
        }

        if (LoopOf(pm) <= 0)
        {
            return "a trainee (loop 0)";
        }

        if (pm.Hidden)
        {
            return "hidden";
        }

        if (!Traveled(pm, now))
        {
            return "idle (not traveling)";
        }

        if (InCombat(pm))
        {
            return "in combat";
        }

        if (EncounterOf(pm) != null)
        {
            return "already in an encounter";
        }

        if (_characterCooldown.TryGetValue(pm.Serial, out var until) && now < until)
        {
            return "cooldown";
        }

        if (pm.Account is IAccount acct && _accountCooldown.TryGetValue(acct.Username, out var acctUntil) && now < acctUntil)
        {
            return "cooldown (account)";
        }

        if (NearBoat(pm.Map, pm.Location))
        {
            return "on a boat";
        }

        if (BaseHouse.FindHouseAt(pm) != null)
        {
            return "in a house";
        }

        var place = WhyNotHere(pm.Map, pm.Location);
        if (place != null)
        {
            return place;
        }

        if (_active.Any(e => e.Map == pm.Map && Utility.InRange(e.Spot, pm.Location, t.CrowdRange)))
        {
            return "another encounter is nearby";
        }

        return null;
    }

    /// <summary>Why no encounter may start, or an encounter creature stand, at this spot; null when it may.</summary>
    public static string? WhyNotHere(Map? map, Point3D p)
    {
        var t = Table;
        if (map == null || map == Map.Internal)
        {
            return "not in the world";
        }

        if (map.MapIndex >= FoldMaps.FoldIndex)
        {
            return "the Wakening Isle";
        }

        if (!t.Facets.Any(f => string.Equals(f, map.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return $"{map.Name} is not an encounter facet";
        }

        var region = Region.Find(p, map);
        foreach (var name in t.DenyRegions)
        {
            if (region.IsPartOf(name))
            {
                return $"in {name}";
            }
        }

        if (region.GetRegion<GuardedRegion>() is { } guarded && (!guarded.IsDisabled() || guarded is TownRegion))
        {
            return "in a town";
        }

        if (region.IsPartOf<DungeonRegion>())
        {
            return "in a dungeon";
        }

        if (region.IsPartOf<HouseRegion>() || BaseHouse.FindHouseAt(p, map, 16) != null)
        {
            return "in a house";
        }

        if (region.IsPartOf<JailRegion>())
        {
            return "in jail";
        }

        if (region.IsPartOf<ApprenticeRegion>() || region.IsPartOf<FoldRegion>() || region.IsPartOf<FoldMirrorRegion>())
        {
            return "in a safe zone";
        }

        if (NearBoat(map, p))
        {
            return "on a boat";
        }

        return null;
    }

    /// <summary>Tiles from a boat's centre that count as aboard or alongside it (a galleon is about 12 tiles long).</summary>
    public const int BoatRange = 8;

    /// <summary>On a boat, or close enough to one (boarding, docked alongside) that an encounter would land on it.</summary>
    public static bool NearBoat(Map map, Point3D p)
    {
        if (BaseBoat.FindBoatAt(p, map) != null)
        {
            return true;
        }

        foreach (var boat in map.GetMultisInRange<BaseBoat>(p, BoatRange))
        {
            if (!boat.Deleted && Utility.InRange(boat.Location, p, BoatRange))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether this player qualifies for this encounter here and now (beyond WhyNot).</summary>
    public static bool Qualifies(PlayerMobile pm, EncounterDef def, DateTime now)
    {
        if (!def.Enabled || def.Weight <= 0)
        {
            return false;
        }

        if (def.Facets.Count > 0 && !def.Facets.Any(f => string.Equals(f, pm.Map?.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (def.Time != EncounterTime.Any && IsNight(pm.Map!, pm.Location) != (def.Time == EncounterTime.Night))
        {
            return false;
        }

        var level = ClusterFCreatureLevels.PlayerLevel(pm);
        if (level < def.MinLevel || level > def.MaxLevel)
        {
            return false;
        }

        if (def.LeagueOnly && !IsLeague(pm))
        {
            return false;
        }

        return def.Season == null || InSeason(def.Season);
    }

    public static bool IsNight(Map map, Point3D p)
    {
        var hour = HourAt(map, p);
        var t = Table;
        return t.DayStartHour < t.NightStartHour
            ? hour < t.DayStartHour || hour >= t.NightStartHour
            : hour >= t.NightStartHour && hour < t.DayStartHour;
    }

    /// <summary>One of the encounters this player qualifies for, by weight; null when none.</summary>
    public static EncounterDef? Pick(PlayerMobile pm, DateTime now)
    {
        var options = Table.Encounters.Where(d => Qualifies(pm, d, now)).ToList();
        var total = options.Sum(d => d.Weight);
        if (total <= 0)
        {
            return null;
        }

        var roll = Roll() * total;
        foreach (var d in options)
        {
            roll -= d.Weight;
            if (roll < 0)
            {
                return d;
            }
        }

        return options[^1];
    }

    /// <summary>A spot SpawnDistanceMin to Max tiles away that passes WhyNotHere, out of sight when one allows.</summary>
    public static Point3D? DefaultFindSpot(PlayerMobile pm, EncounterTable t)
    {
        var map = pm.Map;
        if (map == null)
        {
            return null;
        }

        Point3D? inSight = null;
        for (var i = 0; i < t.SpawnTries; i++)
        {
            var angle = Utility.RandomDouble() * Math.PI * 2;
            var d = Utility.RandomMinMax(t.SpawnDistanceMin, t.SpawnDistanceMax);
            var x = pm.X + (int)Math.Round(Math.Cos(angle) * d);
            var y = pm.Y + (int)Math.Round(Math.Sin(angle) * d);
            var z = map.GetAverageZ(x, y);
            var p = new Point3D(x, y, z);
            if (!map.CanSpawnMobile(p) || WhyNotHere(map, p) != null)
            {
                continue;
            }

            if (!pm.InLOS(p))
            {
                return p;
            }

            inSight ??= p;
        }

        return inSight;
    }

    // -- Starting one --

    /// <summary>
    /// Starts this encounter near the player, whatever the rules say (the roll has checked them; [Encounters force
    /// skips them). Null when no spot or no creature could be made.
    /// </summary>
    public static ActiveEncounter? Start(PlayerMobile pm, EncounterDef def, DateTime now)
    {
        var t = Table;
        if (FindSpot(pm, t) is not { } spot || pm.Map is not { } map)
        {
            return null;
        }

        var e = new ActiveEncounter
        {
            Def = def,
            Player = pm,
            Map = map,
            Spot = spot,
            Started = now,
            LeaveAt = now + TimeSpan.FromMinutes(t.LeaveAfterMinutes),
            TollDeadline = now + TimeSpan.FromSeconds(def.TollWaitSeconds)
        };

        // Where each creature stands: a short column for a procession, a loose knot otherwise.
        var dx = Math.Sign(pm.X - spot.X);
        var dy = Math.Sign(pm.Y - spot.Y);
        var index = 0;
        foreach (var group in def.Creatures)
        {
            var n = Utility.RandomMinMax(group.Min, group.Max);
            for (var i = 0; i < n; i++)
            {
                var bc = Make(group.Types[Utility.Random(group.Types.Count)]);
                if (bc == null)
                {
                    continue;
                }

                var at = def.Kind == EncounterKind.Procession
                    ? new Point3D(spot.X - dy * index, spot.Y + dx * index, spot.Z)
                    : new Point3D(spot.X + (index % 3) - 1, spot.Y + (index / 3) - 1, spot.Z);
                Place(bc, def, at, map);
                e.Creatures.Add(bc);
                index++;
            }
        }

        if (e.Creatures.Count == 0)
        {
            return null;
        }

        e.Leader = e.Creatures[0];
        if (def.LeaderName.Length > 0)
        {
            e.Leader.Name = def.LeaderName;
        }

        if (def.LeaderTitle.Length > 0)
        {
            e.Leader.Title = def.LeaderTitle;
        }

        switch (def.Kind)
        {
            case EncounterKind.Toll:
                {
                    // Peaceful until the deadline, or until struck.
                    foreach (var bc in e.Creatures)
                    {
                        bc.FightMode = FightMode.Aggressor;
                        bc.Home = pm.Location;
                        bc.RangeHome = 3;
                    }

                    break;
                }
            case EncounterKind.Surveyor:
                {
                    var ally = new EncounterSurveyor();
                    if (def.LeaderName.Length > 0)
                    {
                        ally.Name = def.LeaderName;
                    }

                    if (def.LeaderTitle.Length > 0)
                    {
                        ally.Title = def.LeaderTitle;
                    }

                    ally.MoveToWorld(spot, map);
                    ClusterFEncounterState.Spawned.Add(ally.Serial);
                    e.Ally = ally;
                    e.Leader = null;
                    foreach (var bc in e.Creatures)
                    {
                        bc.FightMode = FightMode.Closest;
                        bc.Combatant = ally;
                    }

                    break;
                }
            case EncounterKind.Boss:
                {
                    // cc-P786: one strong creature (the Halloween witch) that comes for the player and stays near the spot.
                    foreach (var bc in e.Creatures)
                    {
                        bc.FightMode = FightMode.Closest;
                        bc.Home = spot;
                        bc.RangeHome = 6;
                        bc.Combatant = pm;
                    }

                    break;
                }
            case EncounterKind.Procession:
                {
                    // Harmless unless attacked; walks on across the player's path.
                    foreach (var bc in e.Creatures)
                    {
                        bc.FightMode = FightMode.Aggressor;
                        bc.Home = new Point3D(spot.X + (pm.X - spot.X) * 2, spot.Y + (pm.Y - spot.Y) * 2, spot.Z);
                        bc.RangeHome = 2;
                        bc.CurrentSpeed = bc.PassiveSpeed;
                    }

                    break;
                }
        }

        _active.Add(e);
        var cooldown = now + TimeSpan.FromMinutes(t.CooldownMinutes);
        _characterCooldown[pm.Serial] = cooldown;
        if (pm.Account is IAccount acct)
        {
            _accountCooldown[acct.Username] = cooldown;
        }

        if (def.SystemLine.Length > 0)
        {
            pm.SendMessage(0x44, def.SystemLine);
        }

        var speaker = e.Ally ?? e.Leader;
        if (def.OpeningLine.Length > 0 && speaker != null)
        {
            speaker.Say(def.OpeningLine);
        }

        return e;
    }

    /// <summary>A stock creature by type name, or null.</summary>
    public static BaseCreature? Make(string typeName)
    {
        var type = AssemblyHandler.FindTypeByName(typeName);
        if (type == null || !typeof(BaseCreature).IsAssignableFrom(type) || type.IsAbstract)
        {
            return null;
        }

        try
        {
            return Activator.CreateInstance(type) as BaseCreature;
        }
        catch
        {
            return null;
        }
    }

    private static void Place(BaseCreature bc, EncounterDef def, Point3D at, Map map)
    {
        bc.Tamable = false;
        if (def.CreatureName.Length > 0)
        {
            bc.Name = def.CreatureName;
        }

        if (def.CreatureHue != 0)
        {
            bc.Hue = def.CreatureHue;
        }

        bc.Home = at;
        bc.RangeHome = 6;
        bc.MoveToWorld(at, map);
        ClusterFEncounterState.Spawned.Add(bc.Serial);
    }

    // -- The toll --

    /// <summary>The player said something: "pay" answers a waiting toll.</summary>
    public static void OnSpeech(SpeechEventArgs e)
    {
        if (e.Mobile is PlayerMobile pm && Said(e.Speech, "pay"))
        {
            TryPayToll(pm);
        }
    }

    private static bool Said(string speech, string word) =>
        speech.Split([' ', ',', '.', '!', '?', '\'', '"'], StringSplitOptions.RemoveEmptyEntries)
            .Any(w => string.Equals(w, word, StringComparison.OrdinalIgnoreCase));

    /// <summary>The toll, for this much gold in the pack: TollPercent of it, at most TollCap.</summary>
    public static int TollFor(EncounterDef def, int gold) => Math.Min(def.TollCap, gold * def.TollPercent / 100);

    /// <summary>Pays a waiting toll: the gold goes, the brigands leave. Returns the gold taken, or -1 when there was no toll.</summary>
    public static int TryPayToll(PlayerMobile pm)
    {
        var e = EncounterOf(pm);
        if (e == null || e.Def.Kind != EncounterKind.Toll || e.Stage != EncounterStage.Open || e.Leader is not { Alive: true } leader ||
            leader.Map != pm.Map || !pm.InRange(leader, 20) || e.Creatures.Any(Fighting))
        {
            return -1;
        }

        var gold = pm.Backpack?.GetAmount(typeof(Gold)) ?? 0;
        var toll = TollFor(e.Def, gold);
        if (toll > 0)
        {
            pm.Backpack!.ConsumeTotal(typeof(Gold), toll);
        }

        leader.Say(toll > 0 ? "Pleasure doing business. Mind the road." : "An empty purse. Go on, then.");
        pm.SendMessage(0x44, toll > 0
            ? $"System: You paid the road tax ({toll} gold). The receipt is a memory."
            : "System: You had nothing to tax. The brigands find this professionally disappointing.");
        e.Stage = EncounterStage.Done;
        e.LeaveAt = Clock() + TimeSpan.FromSeconds(5);
        return toll;
    }

    private static void TurnHostile(ActiveEncounter e)
    {
        e.Stage = EncounterStage.Hostile;
        foreach (var bc in e.Creatures.Where(c => !c.Deleted && c.Alive))
        {
            bc.FightMode = FightMode.Closest;
            if (e.Player.Alive && e.Player.Map == bc.Map && !e.Player.Hidden)
            {
                bc.Combatant = e.Player;
            }
        }

        e.Leader?.Say("No coin? Then we take it the other way.");
    }

    // -- Deaths --

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        var e = EncounterOf(bc);
        if (e == null)
        {
            return;
        }

        OnEncounterDeath(e, bc);
    }

    public static void OnEncounterDeath(ActiveEncounter e, BaseCreature bc)
    {
        if (bc == e.Ally)
        {
            e.Stage = EncounterStage.Done;
            e.LeaveAt = Clock();
            e.Player.SendMessage(0x22, "System: The surveyor's findings will be published posthumously. Briefly.");
            return;
        }

        var top = FestivalTokens.TopDamager(bc);
        if (bc == e.Leader)
        {
            switch (e.Def.Kind)
            {
                case EncounterKind.Toll:
                    {
                        e.Stage = EncounterStage.Done;
                        if (top != null && e.Def.BoxSource.Length > 0)
                        {
                            top.SendMessage(0x44, "System: The toll booth is closed for good. You may keep the proceeds.");
                            ClusterFLootBoxes.Award(top, e.Def.BoxSource, BoxTheme.Hunter, e.Def.BoxGrade, null, e.Def.Name, out _);
                        }

                        break;
                    }
                case EncounterKind.Boss:
                    {
                        // cc-P786: the boss pays its own reward (the witch's drop and tokens, HalloweenWitch.OnDeath).
                        e.Stage = EncounterStage.Done;
                        break;
                    }
                case EncounterKind.Procession:
                    {
                        e.Stage = EncounterStage.Done;
                        if (top != null && e.Def.LeaderTokens > 0)
                        {
                            FestivalTokens.Grant(top, e.Def.LeaderTokens, e.Def.Name);
                        }

                        break;
                    }
            }
        }
    }

    // -- Cleanup --

    /// <summary>Whether this creature is in a fight now (it leaves only when it is not).</summary>
    public static bool Fighting(BaseCreature bc) => bc.Combatant != null;

    /// <summary>
    /// The sweep: creatures standing where they may not are removed; a toll past its deadline turns; a saved surveyor
    /// thanks and leaves; past LeaveAt (or dawn, for a night encounter) every creature not fighting leaves. An encounter
    /// with nothing left is over. Returns the number of creatures removed.
    /// </summary>
    public static int Sweep(DateTime? at = null)
    {
        var now = at ?? Clock();
        var removed = 0;
        foreach (var e in _active.ToList())
        {
            // Never into a town, a house or anywhere else encounters may not be.
            foreach (var bc in e.All.ToList())
            {
                if (!bc.Deleted && bc.Alive && WhyNotHere(bc.Map, bc.Location) != null)
                {
                    removed += Remove(e, bc);
                }
            }

            // The dead are gone from the encounter (their corpses are never touched) and from the saved set.
            foreach (var gone in e.All.Where(c => c.Deleted || !c.Alive).ToList())
            {
                ClusterFEncounterState.Spawned.Remove(gone.Serial);
            }

            e.Creatures.RemoveAll(c => c.Deleted || !c.Alive);
            if (e.Ally is { } ally && (ally.Deleted || !ally.Alive))
            {
                e.Ally = null;
            }

            switch (e.Def.Kind)
            {
                case EncounterKind.Toll when e.Stage == EncounterStage.Open && now >= e.TollDeadline:
                    {
                        TurnHostile(e);
                        break;
                    }
                case EncounterKind.Surveyor when e.Stage == EncounterStage.Open && e.Creatures.Count == 0 && e.Ally != null:
                    {
                        e.Stage = EncounterStage.Done;
                        e.Ally.Say("The League thanks you. I'll file you under 'helpful' and the wolves under 'not'.");
                        e.Player.SendMessage(0x44, "System: Surveyor recovered. The League notes your contribution in a ledger nobody reads.");
                        e.LeaveAt = now + TimeSpan.FromSeconds(5);
                        break;
                    }
                case EncounterKind.Procession when e.Def.Time == EncounterTime.Night && e.Creatures.Count > 0 &&
                                                   !IsNight(e.Map, e.Creatures[0].Location):
                    {
                        e.LeaveAt = now;
                        break;
                    }
            }

            // A toll's brigands with no leader keep fighting; peaceful ones leave once the deal is off the table.
            if (e.Def.Kind == EncounterKind.Toll && e.Stage == EncounterStage.Open && e.Leader is { } l && (l.Deleted || !l.Alive))
            {
                e.Stage = EncounterStage.Done;
            }

            if (now >= e.LeaveAt)
            {
                foreach (var bc in e.All.ToList())
                {
                    if (!Fighting(bc))
                    {
                        removed += Remove(e, bc);
                    }
                }
            }

            if (e.Creatures.Count == 0 && e.Ally == null)
            {
                _active.Remove(e);
            }
        }

        return removed;
    }

    private static int Remove(ActiveEncounter e, BaseCreature bc)
    {
        e.Creatures.Remove(bc);
        if (e.Ally == bc)
        {
            e.Ally = null;
        }

        ClusterFEncounterState.Spawned.Remove(bc.Serial);
        if (bc.Deleted)
        {
            return 0;
        }

        if (bc.Map != null && bc.Map != Map.Internal)
        {
            Effects.SendLocationEffect(bc.Location, bc.Map, 0x3728, 13);
        }

        bc.Delete();
        return 1;
    }

    /// <summary>World load: removes every saved encounter creature still in the world. Returns how many.</summary>
    public static int SweepSaved()
    {
        var removed = 0;
        foreach (var serial in ClusterFEncounterState.Spawned.ToList())
        {
            if (World.FindMobile(serial) is BaseCreature { Deleted: false, Controlled: false } bc)
            {
                bc.Delete();
                removed++;
            }
        }

        ClusterFEncounterState.Spawned.Clear();
        return removed;
    }

    // -- The staff command --

    [Usage("Encounters [status | check | reload | force <id>]")]
    [Description("Random encounters (cc-P776): status lists what is out; check says whether you, as a player, could have one here " +
                 "and why not; reload rereads Configuration/encounters.json; force <id> starts one near you, skipping the rules.")]
    [ShardCommand(CommandCategory.DevTool, Summary = "Random encounters: status, check, reload, or force one near you for testing (cc-P776).")]
    private static void OnCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm)
        {
            return;
        }

        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
        switch (verb)
        {
            case "reload":
                {
                    foreach (var line in EncounterTable.Reload(EncounterTable.FilePath))
                    {
                        pm.SendMessage(line);
                    }

                    StartTimers();
                    break;
                }
            case "force":
                {
                    var def = e.Length > 1 ? Table.Find(e.GetString(1)) : null;
                    if (def == null)
                    {
                        pm.SendMessage($"Usage: [Encounters force <{string.Join("|", Table.Encounters.Select(d => d.Id))}>");
                        return;
                    }

                    var started = Start(pm, def, Clock());
                    pm.SendMessage(started == null
                        ? "No spot near you passed the place rules (towns, houses, the isle...)."
                        : $"{def.Name}: {started.Creatures.Count} creatures at {started.Spot}.");
                    break;
                }
            case "check":
                {
                    var why = WhyNot(pm, Clock());
                    pm.SendMessage(why == null ? "You could have an encounter here." : $"No encounter: {why}.");
                    break;
                }
            default:
                {
                    var t = Table;
                    pm.SendMessage($"Encounters: {(t.Enabled ? "on" : "OFF")}, {t.Source}; chance {t.Chance:0.###} per {t.TickSeconds}s, " +
                                   $"cooldown {t.CooldownMinutes} min; {_active.Count} out.");
                    foreach (var a in _active)
                    {
                        pm.SendMessage($"  {a.Def.Name} for {a.Player.Name} at {a.Spot} ({a.Map}), {a.Stage}, {a.Creatures.Count} creatures, " +
                                       $"leaves {a.LeaveAt:HH:mm:ss}");
                    }

                    break;
                }
        }
    }
}

/// <summary>
/// The serials of every encounter creature out in the world, saved so that a restart can remove them (cc-P776). Saved
/// through pinned's GenericPersistence, versioned; an unknown version fails loudly (the ClusterFSeasonalState rule).
/// </summary>
public class ClusterFEncounterState : GenericPersistence
{
    public const int CurrentVersion = 0;

    private static ClusterFEncounterState? _instance;

    public static HashSet<Serial> Spawned { get; } = [];

    public ClusterFEncounterState() : base("RandomEncounters", 10)
    {
    }

    public static void Configure()
    {
        _instance ??= new ClusterFEncounterState();
    }

    public override void Serialize(IGenericWriter writer) => WriteTo(writer);

    public override void Deserialize(IGenericReader reader) => ReadFrom(reader);

    public static void WriteTo(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);
        writer.WriteEncodedInt(Spawned.Count);
        foreach (var serial in Spawned)
        {
            writer.Write(serial);
        }
    }

    public static void ReadFrom(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new System.IO.InvalidDataException(
                $"ClusterFEncounterState version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        Spawned.Clear();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            Spawned.Add(reader.ReadSerial());
        }
    }
}
