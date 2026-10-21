// ClusterFSurge.cs
//
// cc-P230 (F-30 second batch, ours). A GM-run monster surge, started from the Staff Hub's Events tab (StaffHubGump.cs).
// Players see the surge; only staff see the tool. Notes: shard-migration/notes/cc-P230-staff-hub-events-tab.md.
//
//   Start      a template (ClusterFSurgeTemplates), a spot and radius, waves, a count per wave and the interval between
//              waves. Start broadcasts, spawns wave 1 at once and the rest on the interval; the named boss of a template
//              comes with the last wave. Spawn points follow pinned's champion spawn (ChampionSpawn.GetSpawnLocation:
//              20 random points, GetAverageZ, CanSpawnMobile, else the centre), and each creature is homed on the centre.
//              Optional: an event moongate in Britain and one in New Haven, both to a staging spot.
//   Dry run    the same checks and the plan as lines, and nothing spawns.
//   Flagged    every creature and gate a surge makes is in its run's sets and, by serial, in this file's own save
//              (Saves/StaffHubSurge/StaffHubSurge.bin). End deletes what is left and closes the gates; a restart mid-surge
//              deletes them at world load (CleanUpAfterRestart), with no prizes.
//   Ends       when the GM presses End, or by itself once the last wave is out and every creature is dead.
//   Prizes     in any mix, per surge: by damage (the top N damage dealers get the top prize, everyone else who did
//              damage the share prize), everyone present (inside the radius at least half the run time: the share
//              prize, once), and extras the GM hands out by target. The GM names every prize item; the defaults are gold.
//              Damage is read from pinned's per-creature DamageEntries (Mobile.cs:974; a pet's damage counts for its
//              master), sampled every second and at each death, because pinned raises no damage event.
//   No loot    optional: a player who dies inside the radius during the surge keeps everything. PlayerDeathEvent
//              (PlayerMobile.cs:2609) runs after the corpse is made (Mobile.Kill), and puts the corpse's contents back in
//              the backpack, which is where a Young player's items go (PlayerMobile.GetParentMoveResultFor). No patch.
//
// One surge runs at a time. Access is the hub's: GameMaster and up, on live and on test.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using Server.Collections;
using Server.Items;
using Server.Mobiles;

namespace Server;

/// <summary>What the Events tab is set to; one per open hub, copied into the run when the surge starts.</summary>
public sealed class SurgeSettings
{
    public const int MinRadius = 1, MaxRadius = 40;
    public const int MaxWaves = 20;
    public const int MaxPerWave = 50;
    public const int MinIntervalSeconds = 10, MaxIntervalSeconds = 3600;
    public const int MaxTopCount = 10;

    public string Template { get; set; }
    public Map Map { get; set; }
    public Point3D Center { get; set; }
    public int Radius { get; set; } = 12;
    public int Waves { get; set; } = 3;
    public int PerWave { get; set; } = 8;
    public int IntervalSeconds { get; set; } = 60;

    public bool Gates { get; set; }
    public Map StagingMap { get; set; }
    public Point3D Staging { get; set; }

    public bool NoLoot { get; set; }

    public bool PrizeDamage { get; set; } = true;
    public bool PrizePresent { get; set; } = true;
    public bool PrizeExtra { get; set; }
    public int TopCount { get; set; } = 3;
    public string TopPrize { get; set; } = "Gold 2000";
    public string SharePrize { get; set; } = "Gold 500";
    public string ExtraPrize { get; set; } = "Gold 1000";

    public bool HasCenter => Map != null && Map != Map.Internal;
    public bool HasStaging => StagingMap != null && StagingMap != Map.Internal;

    public SurgeSettings Clone() => (SurgeSettings)MemberwiseClone();
}

/// <summary>A prize as the GM typed it: an item type and how many ("Gold 2000", "BagOfSending").</summary>
public sealed record SurgePrize(Type Type, int Amount, bool Stackable)
{
    public const int MaxStack = 60000;
    public const int MaxCopies = 10;

    public string Describe() => Amount > 1 ? $"{Type.Name} {Amount}" : Type.Name;

    public static SurgePrize Parse(string text, out string error)
    {
        error = null;
        var parts = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 0 or > 2)
        {
            error = "A prize is an item type and an amount, for example: Gold 2000";
            return null;
        }

        var type = AssemblyHandler.FindTypeByName(parts[0]);
        // GetConstructor(out _): pinned's, which counts a constructor whose parameters are all optional (Gold(int amount = 1)).
        if (type == null || !typeof(Item).IsAssignableFrom(type) || type.IsAbstract || type.GetConstructor(out _) == null)
        {
            error = $"'{parts[0]}' is not an item type.";
            return null;
        }

        var amount = 1;
        if (parts.Length == 2 && (!int.TryParse(parts[1], out amount) || amount < 1))
        {
            error = $"'{parts[1]}' is not an amount.";
            return null;
        }

        Item probe;
        try
        {
            probe = type.CreateInstance<Item>();
        }
        catch
        {
            error = $"{type.Name} cannot be made without arguments.";
            return null;
        }

        var stackable = probe.Stackable;
        probe.Delete();

        if (amount > (stackable ? MaxStack : MaxCopies))
        {
            error = $"{type.Name}: at most {(stackable ? MaxStack : MaxCopies)}.";
            return null;
        }

        return new SurgePrize(type, amount, stackable);
    }

    /// <summary>Makes the prize and puts it in the player's backpack (at their feet if it will not fit).</summary>
    public List<Item> GiveTo(Mobile m)
    {
        var made = new List<Item>();
        for (var i = 0; i < (Stackable ? 1 : Amount); i++)
        {
            var item = Type.CreateInstance<Item>();
            if (Stackable)
            {
                item.Amount = Amount;
            }

            m.AddToBackpack(item);
            made.Add(item);
        }

        return made;
    }
}

/// <summary>One surge, from Start to its end. ClusterFSurge.Active while it runs; ClusterFSurge.Last after.</summary>
public sealed class SurgeRun
{
    // (creature, damager) -> the entry last read and its DamageGiven then, so only the growth since is added. Pinned
    // drops a creature's entries when they expire or it heals to full (Mobile.cs:2043) and the next hit makes a new
    // DamageEntry (RegisterDamage), so a different entry object is counted from 0.
    private readonly Dictionary<(Serial, Serial), (DamageEntry Entry, int Given)> _marks = new();

    public SurgeRun(Mobile startedBy, SurgeSettings settings, SurgeTemplate template)
    {
        StartedBy = startedBy;
        Settings = settings;
        Template = template;
        StartedAt = Core.Now;
    }

    public Mobile StartedBy { get; }
    public SurgeSettings Settings { get; }
    public SurgeTemplate Template { get; }
    public DateTime StartedAt { get; }
    public DateTime? EndedAt { get; internal set; }
    public string EndReason { get; internal set; }

    public int WavesSpawned { get; internal set; }
    public int Spawned { get; internal set; }
    public int Killed { get; internal set; }

    /// <summary>Every creature this surge made that is still in the world.</summary>
    public HashSet<Mobile> Creatures { get; } = [];

    public List<Item> Gates { get; } = [];

    // Seconds counted (one per tick), and each player's seconds inside the radius.
    public int Ticks { get; internal set; }
    public Dictionary<Mobile, int> PresentTicks { get; } = new();

    // Damage dealt by each player to this surge's creatures.
    public Dictionary<Mobile, int> Damage { get; } = new();

    public List<(Mobile Player, string Prize, string Why)> Awards { get; } = [];

    internal Timer Timer { get; set; }

    public DateTime NextWaveAt => StartedAt + TimeSpan.FromSeconds(Settings.IntervalSeconds * WavesSpawned);

    public bool AllWavesOut => WavesSpawned >= Settings.Waves;

    public int Alive => Creatures.Count(c => !c.Deleted && c.Alive);

    /// <summary>Reads a creature's damage entries into the run's totals.</summary>
    public void SampleDamage(Mobile creature)
    {
        if (creature == null)
        {
            return;
        }

        foreach (var entry in creature.DamageEntries)
        {
            var damager = entry.Damager;
            if (damager is BaseCreature { } pet && pet.GetMaster() is { } master)
            {
                damager = master;
            }

            if (damager is not PlayerMobile || damager.AccessLevel > AccessLevel.Player)
            {
                continue;
            }

            var key = (creature.Serial, entry.Damager.Serial);
            var now = entry.DamageGiven;
            var added = _marks.TryGetValue(key, out var mark) && ReferenceEquals(mark.Entry, entry) ? now - mark.Given : now;
            _marks[key] = (entry, now);

            if (added > 0)
            {
                Damage[damager] = Damage.GetValueOrDefault(damager) + added;
            }
        }
    }

    /// <summary>Whether a player was inside the radius at least half the run's counted time.</summary>
    public bool WasPresent(Mobile m) => Ticks > 0 && PresentTicks.GetValueOrDefault(m) * 2 >= Ticks;
}

public class ClusterFSurge : GenericPersistence
{
    // 0: cc-P230. The flagged creature and gate serials of the surge that was running at the save.
    public const int CurrentVersion = 0;

    public const int BroadcastHue = 0x35;

    // pinned Distribution/Data/Locations/trammel.json:216, "Britain" / "Center"; New Haven: three tiles south of the bank
    // anchor (ClusterFNewHavenSeeder.BankAnchor). Both on Trammel.
    public static readonly Point3D BritainGate = new(1475, 1645, 20);

    public static Point3D NewHavenGate =>
        new(ClusterFNewHavenSeeder.BankAnchor.X, ClusterFNewHavenSeeder.BankAnchor.Y + 3, ClusterFNewHavenSeeder.BankAnchor.Z);

    public static Map GateFacet => Map.Trammel;

    private static ClusterFSurge _instance;

    // Read at load, deleted at world load.
    private static readonly List<Serial> _pending = [];

    public ClusterFSurge() : base("StaffHubSurge", 10)
    {
    }

    public static void Configure()
    {
        _instance ??= new ClusterFSurge();
        EventSink.WorldLoad += () => CleanUpAfterRestart();
    }

    public static SurgeRun Active { get; private set; }

    public static SurgeRun Last { get; private set; }

    // ---------------------------------------------------------------- checks and the plan

    /// <summary>Everything wrong with these settings, as lines for the GM; empty when the surge can start.</summary>
    public static List<string> Problems(SurgeSettings s)
    {
        var p = new List<string>();
        var template = ClusterFSurgeTemplates.Find(s.Template);

        if (template == null)
        {
            p.Add("Pick a template.");
        }
        else if (template.Creatures.Any(c => ClusterFSurgeTemplates.CreatureType(c.TypeName) == null) ||
                 template.HasBoss && ClusterFSurgeTemplates.CreatureType(template.BossType) == null)
        {
            p.Add($"Template '{template.Name}' names a creature type this build does not have.");
        }

        if (!s.HasCenter)
        {
            p.Add("Set the spot: Here, or Target.");
        }

        if (s.Radius is < SurgeSettings.MinRadius or > SurgeSettings.MaxRadius)
        {
            p.Add($"Radius is {SurgeSettings.MinRadius} to {SurgeSettings.MaxRadius}.");
        }

        if (s.Waves is < 1 or > SurgeSettings.MaxWaves)
        {
            p.Add($"Waves are 1 to {SurgeSettings.MaxWaves}.");
        }

        if (s.PerWave is < 1 or > SurgeSettings.MaxPerWave)
        {
            p.Add($"Per wave is 1 to {SurgeSettings.MaxPerWave}.");
        }

        if (s.IntervalSeconds is < SurgeSettings.MinIntervalSeconds or > SurgeSettings.MaxIntervalSeconds)
        {
            p.Add($"The interval is {SurgeSettings.MinIntervalSeconds} to {SurgeSettings.MaxIntervalSeconds} seconds.");
        }

        if (s.Gates && !s.HasStaging && !s.HasCenter)
        {
            p.Add("Gates need a staging spot.");
        }

        if (s.PrizeDamage)
        {
            if (s.TopCount is < 1 or > SurgeSettings.MaxTopCount)
            {
                p.Add($"Top damage dealers: 1 to {SurgeSettings.MaxTopCount}.");
            }

            if (SurgePrize.Parse(s.TopPrize, out var e) == null)
            {
                p.Add($"Top prize: {e}");
            }
        }

        if ((s.PrizeDamage || s.PrizePresent) && SurgePrize.Parse(s.SharePrize, out var se) == null)
        {
            p.Add($"Share prize: {se}");
        }

        if (s.PrizeExtra && SurgePrize.Parse(s.ExtraPrize, out var ee) == null)
        {
            p.Add($"Extra prize: {ee}");
        }

        return p;
    }

    public static string Where(Map map, Point3D p)
    {
        var region = map == null ? null : Region.Find(p, map);
        var name = region?.Name is { Length: > 0 } n ? $"{n}, " : "";
        return $"{name}{p.X}, {p.Y}, {p.Z} on {map?.Name ?? "?"}";
    }

    /// <summary>The surge these settings would run, as lines. What Dry run shows; nothing is made.</summary>
    public static List<string> Plan(SurgeSettings s)
    {
        var lines = new List<string>();
        var problems = Problems(s);
        var template = ClusterFSurgeTemplates.Find(s.Template);

        if (template != null)
        {
            lines.Add($"Template '{template.Name}': {template.Describe()}.");
        }

        if (s.HasCenter)
        {
            lines.Add($"At {Where(s.Map, s.Center)}, radius {s.Radius}.");
        }

        var total = s.Waves * s.PerWave + (template?.HasBoss == true ? 1 : 0);
        lines.Add($"{s.Waves} wave(s) of {s.PerWave}, every {s.IntervalSeconds} s: {total} creature(s) in all" +
                  (template?.HasBoss == true ? $", the boss {template.BossType} with wave {s.Waves}" : "") + ".");
        lines.Add($"Waves at {string.Join(", ", Enumerable.Range(0, Math.Clamp(s.Waves, 0, SurgeSettings.MaxWaves)).Select(i => TimeSpan.FromSeconds(i * s.IntervalSeconds).ToString(@"m\:ss")))}.");

        if (s.Gates)
        {
            var stagingMap = s.HasStaging ? s.StagingMap : s.Map;
            var staging = s.HasStaging ? s.Staging : s.Center;
            lines.Add($"Moongates in Britain ({BritainGate}) and New Haven ({NewHavenGate}) to {Where(stagingMap, staging)}.");
        }

        lines.Add(s.NoLoot ? "No-loot zone: players who die inside the radius keep their items." : "No-loot zone: off.");

        var prizes = new List<string>();
        if (s.PrizeDamage)
        {
            prizes.Add($"top {s.TopCount} by damage get {s.TopPrize}, other damage dealers {s.SharePrize}");
        }

        if (s.PrizePresent)
        {
            prizes.Add($"everyone present half the time gets {s.SharePrize}");
        }

        if (s.PrizeExtra)
        {
            prizes.Add($"GM extras: {s.ExtraPrize}");
        }

        lines.Add(prizes.Count == 0 ? "Prizes: none." : $"Prizes: {string.Join("; ", prizes)}.");

        lines.AddRange(problems.Select(p => $"Cannot start: {p}"));
        return lines;
    }

    public static void DryRun(Mobile from, SurgeSettings s)
    {
        from.SendMessage(StaffHubGump.HueGold, "Surge dry run (nothing spawned):");
        foreach (var line in Plan(s))
        {
            from.SendMessage(line.StartsWith("Cannot", StringComparison.Ordinal) ? StaffHubGump.HueRed : StaffHubGump.HueWhite, line);
        }
    }

    // ---------------------------------------------------------------- start

    public static bool Start(Mobile from, SurgeSettings settings, out string message)
    {
        if (Active != null)
        {
            message = "A surge is already running; End it first.";
            return false;
        }

        var problems = Problems(settings);
        if (problems.Count > 0)
        {
            message = problems[0];
            return false;
        }

        var s = settings.Clone();
        var template = ClusterFSurgeTemplates.Find(s.Template);
        var run = new SurgeRun(from, s, template);
        Active = run;
        Last = null;

        World.Broadcast(BroadcastHue, false, $"A surge! {template.Name} gathers near {Where(s.Map, s.Center)}.");

        if (s.Gates)
        {
            OpenGates(run);
        }

        SpawnWave(run);

        run.Timer = new SurgeTimer(run);
        run.Timer.Start();

        message = $"Surge '{template.Name}' started: wave 1 of {s.Waves} is out.";
        return true;
    }

    private static void OpenGates(SurgeRun run)
    {
        var s = run.Settings;
        var stagingMap = s.HasStaging ? s.StagingMap : s.Map;
        var staging = s.HasStaging ? s.Staging : s.Center;

        foreach (var (where, _) in new[] { (BritainGate, "Britain"), (NewHavenGate, "New Haven") })
        {
            var gate = new Moongate(staging, stagingMap, false) { Name = "a surge moongate", Hue = 0x26 };
            gate.MoveToWorld(where, GateFacet);
            run.Gates.Add(gate);
        }

        World.Broadcast(BroadcastHue, false, "Event moongates stand in Britain and New Haven.");
    }

    // ---------------------------------------------------------------- waves

    internal static void SpawnWave(SurgeRun run)
    {
        var s = run.Settings;
        var t = run.Template;
        run.WavesSpawned++;

        for (var i = 0; i < s.PerWave; i++)
        {
            SpawnOne(run, PickType(t));
        }

        if (run.AllWavesOut && t.HasBoss)
        {
            var boss = SpawnOne(run, ClusterFSurgeTemplates.CreatureType(t.BossType));
            if (boss != null && !string.IsNullOrEmpty(t.BossName))
            {
                boss.Name = t.BossName;
            }
        }

        if (run.WavesSpawned > 1)
        {
            World.Broadcast(BroadcastHue, false, $"Wave {run.WavesSpawned} of {s.Waves}: {t.Name} presses on!");
        }
    }

    private static Type PickType(SurgeTemplate t)
    {
        var roll = Utility.Random(t.Creatures.Sum(c => c.Weight));
        foreach (var c in t.Creatures)
        {
            if ((roll -= c.Weight) < 0)
            {
                return ClusterFSurgeTemplates.CreatureType(c.TypeName);
            }
        }

        return ClusterFSurgeTemplates.CreatureType(t.Creatures[0].TypeName);
    }

    private static Mobile SpawnOne(SurgeRun run, Type type)
    {
        Mobile m;
        try
        {
            m = type?.CreateInstance<Mobile>();
        }
        catch
        {
            return null;
        }

        if (m == null)
        {
            return null;
        }

        var s = run.Settings;
        var loc = SpawnPoint(s.Map, s.Center, s.Radius);
        m.OnBeforeSpawn(loc, s.Map);
        m.MoveToWorld(loc, s.Map);

        if (m is BaseCreature bc)
        {
            bc.Tamable = false;
            bc.Home = s.Center;
            bc.HomeMap = s.Map;
            bc.RangeHome = s.Radius;
        }

        run.Creatures.Add(m);
        run.Spawned++;
        return m;
    }

    /// <summary>pinned's ChampionSpawn.GetSpawnLocation over the surge's square: 20 tries, else the centre.</summary>
    public static Point3D SpawnPoint(Map map, Point3D center, int radius)
    {
        for (var i = 0; i < 20; i++)
        {
            var x = center.X + Utility.RandomMinMax(-radius, radius);
            var y = center.Y + Utility.RandomMinMax(-radius, radius);
            var z = map.GetAverageZ(x, y);

            if (map.CanSpawnMobile(x, y, z))
            {
                return new Point3D(x, y, z);
            }
        }

        return center;
    }

    private class SurgeTimer : Timer
    {
        private readonly SurgeRun _run;

        public SurgeTimer(SurgeRun run) : base(TimeSpan.FromSeconds(1.0), TimeSpan.FromSeconds(1.0)) => _run = run;

        protected override void OnTick() => Tick(_run);
    }

    /// <summary>Once a second: count who is present, read damage, drop the dead, send a wave that is due, and end when done.</summary>
    internal static void Tick(SurgeRun run)
    {
        if (run != Active)
        {
            run.Timer?.Stop();
            return;
        }

        var s = run.Settings;
        run.Ticks++;

        foreach (var pm in s.Map.GetMobilesInRange<PlayerMobile>(s.Center, s.Radius))
        {
            if (pm.AccessLevel == AccessLevel.Player)
            {
                run.PresentTicks[pm] = run.PresentTicks.GetValueOrDefault(pm) + 1;
            }
        }

        foreach (var c in run.Creatures.ToList())
        {
            if (c.Deleted)
            {
                run.Creatures.Remove(c);
                continue;
            }

            run.SampleDamage(c);
        }

        if (!run.AllWavesOut && Core.Now >= run.NextWaveAt)
        {
            SpawnWave(run);
        }

        if (run.AllWavesOut && run.Creatures.Count == 0)
        {
            Finish(run, "Every creature of the surge is dead.", true);
        }
    }

    // ---------------------------------------------------------------- deaths

    [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
    public static void OnCreatureDeath(BaseCreature bc)
    {
        var run = Active;
        if (run == null || !run.Creatures.Contains(bc))
        {
            return;
        }

        run.SampleDamage(bc);
        run.Creatures.Remove(bc);
        run.Killed++;
    }

    /// <summary>Whether a death here, now, is inside a running surge's no-loot zone.</summary>
    public static bool InNoLootZone(Mobile m) =>
        Active is { Settings.NoLoot: true } run && m.Map == run.Settings.Map && m.InRange(run.Settings.Center, run.Settings.Radius);

    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile pm)
    {
        if (!InNoLootZone(pm) || pm.Corpse is not { Deleted: false } corpse || pm.Backpack is not { } pack)
        {
            return;
        }

        var kept = 0;
        foreach (var item in corpse.Items.ToList())
        {
            pack.DropItem(item);
            kept++;
        }

        if (kept > 0)
        {
            pm.SendMessage(StaffHubGump.HueGreen, "You fell inside the surge's no-loot zone: your belongings are in your backpack.");
        }
    }

    // ---------------------------------------------------------------- end

    /// <summary>The GM's End: prizes as the surge was set, then everything it made is removed.</summary>
    public static bool End(Mobile from, out string message)
    {
        if (Active is not { } run)
        {
            message = "No surge is running.";
            return false;
        }

        Finish(run, $"Ended by {from?.Name ?? "staff"}.", true);
        message = $"Surge ended: {run.Killed} killed, {run.Awards.Count} prize(s) given.";
        return true;
    }

    private static void Finish(SurgeRun run, string reason, bool prizes)
    {
        run.Timer?.Stop();

        foreach (var c in run.Creatures)
        {
            if (!c.Deleted)
            {
                run.SampleDamage(c);
            }
        }

        if (prizes)
        {
            Award(run);
        }

        RemoveEverything(run);

        run.EndedAt = Core.Now;
        run.EndReason = reason;
        Active = null;
        Last = run;

        World.Broadcast(BroadcastHue, false, $"The surge of {run.Template.Name} is over.");
    }

    private static void RemoveEverything(SurgeRun run)
    {
        foreach (var c in run.Creatures.ToList())
        {
            if (!c.Deleted)
            {
                c.Delete();
            }
        }

        run.Creatures.Clear();

        foreach (var g in run.Gates)
        {
            if (!g.Deleted)
            {
                g.Delete();
            }
        }

        run.Gates.Clear();
    }

    /// <summary>Who gets which prize, without giving anything: (top damage dealers, share prize winners).</summary>
    public static (List<Mobile> Top, List<Mobile> Share) Winners(SurgeRun run)
    {
        var s = run.Settings;
        var top = new List<Mobile>();
        var share = new List<Mobile>();

        if (s.PrizeDamage)
        {
            var ranked = run.Damage.Where(kv => kv.Value > 0 && !kv.Key.Deleted)
                .OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key.Serial.Value).Select(kv => kv.Key).ToList();
            top.AddRange(ranked.Take(s.TopCount));
            share.AddRange(ranked.Skip(s.TopCount));
        }

        if (s.PrizePresent)
        {
            foreach (var m in run.PresentTicks.Keys.Where(m => !m.Deleted && run.WasPresent(m)).OrderBy(m => m.Serial.Value))
            {
                if (!top.Contains(m) && !share.Contains(m))
                {
                    share.Add(m);
                }
            }
        }

        return (top, share);
    }

    private static void Award(SurgeRun run)
    {
        var s = run.Settings;
        var (top, share) = Winners(run);
        var topPrize = s.PrizeDamage ? SurgePrize.Parse(s.TopPrize, out _) : null;
        var sharePrize = s.PrizeDamage || s.PrizePresent ? SurgePrize.Parse(s.SharePrize, out _) : null;

        foreach (var m in top)
        {
            Give(run, m, topPrize, $"top damage ({run.Damage[m]:N0})");
        }

        foreach (var m in share)
        {
            Give(run, m, sharePrize, run.Damage.ContainsKey(m) ? $"damage ({run.Damage[m]:N0})" : "present");
        }
    }

    private static void Give(SurgeRun run, Mobile m, SurgePrize prize, string why)
    {
        if (prize == null || m.Deleted)
        {
            return;
        }

        prize.GiveTo(m);
        run.Awards.Add((m, prize.Describe(), why));
        m.SendMessage(StaffHubGump.HueGreen, $"For the surge of {run.Template.Name} ({why}): {prize.Describe()}, in your backpack.");
    }

    /// <summary>A GM's extra prize to one player, while the surge runs or after it (from its summary).</summary>
    public static bool GiveExtra(Mobile from, Mobile target, out string message)
    {
        var run = Active ?? Last;
        if (run == null || !run.Settings.PrizeExtra)
        {
            message = "Extras are not on for this surge.";
            return false;
        }

        if (target is not PlayerMobile pm || pm.Deleted || pm.AccessLevel > AccessLevel.Player)
        {
            message = "Target a player.";
            return false;
        }

        var prize = SurgePrize.Parse(run.Settings.ExtraPrize, out message);
        if (prize == null)
        {
            return false;
        }

        Give(run, pm, prize, $"an extra from {from?.Name ?? "staff"}");
        message = $"Gave {pm.Name} {prize.Describe()}.";
        return true;
    }

    /// <summary>Forgets the finished surge's summary (the Events tab's New surge).</summary>
    public static void ClearLast() => Last = null;

    // ---------------------------------------------------------------- restart

    public override void Serialize(IGenericWriter writer) => WriteTo(writer);

    public override void Deserialize(IGenericReader reader) => ReadFrom(reader);

    public static void WriteTo(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);

        var serials = Active == null
            ? []
            : Active.Creatures.Where(c => !c.Deleted).Select(c => c.Serial)
                .Concat(Active.Gates.Where(g => !g.Deleted).Select(g => g.Serial)).ToList();

        writer.WriteEncodedInt(serials.Count);
        foreach (var serial in serials)
        {
            writer.Write(serial);
        }
    }

    /// <summary>Reads the flagged serials of a surge that was running at the save, to delete at world load.</summary>
    public static void ReadFrom(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException(
                $"ClusterFSurge version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        _pending.Clear();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            _pending.Add(reader.ReadSerial());
        }
    }

    /// <summary>
    /// World load: deletes every creature and gate a surge had out when the shard last saved. Only a creature or a
    /// moongate is deleted, so a serial that has come to mean anything else is left alone. Returns how many.
    /// </summary>
    public static int CleanUpAfterRestart()
    {
        var deleted = 0;
        foreach (var serial in _pending)
        {
            var e = World.FindEntity(serial);
            if (e is BaseCreature or Moongate && !e.Deleted)
            {
                e.Delete();
                deleted++;
            }
        }

        _pending.Clear();
        if (deleted > 0)
        {
            Console.WriteLine($"Staff Hub: removed {deleted} creature(s) and gate(s) left by a surge that was running at the last save.");
        }

        return deleted;
    }

    /// <summary>For the facts: drops the running surge without cleaning up, as a crash would.</summary>
    public static void ForgetForTests()
    {
        Active?.Timer?.Stop();
        Active = null;
        Last = null;
    }
}
