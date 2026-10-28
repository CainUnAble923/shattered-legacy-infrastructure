// cc-P664 (P481 F4, design section 12): private copies of the Wakening Isle. Notes: shard-migration
// notes/cc-P664-private-isle-copies.md.
//
// Each trainee, or a party, gets one of the Fold's 16 copies (maps 33 to 48, FoldMaps), so nobody else's pests, doors,
// pets or sounds reach a lesson. Every way onto the isle goes through TutorialStart.SendToIsle, which asks Claim:
//
//   - a trainee already on a current copy (or logged out on one) keeps it; a party member joins the copy a member of
//     their party is on; anyone else gets the first free copy, seeded (TutorialCorePlacement.SeedOn) if it is not;
//   - a copy is held while anyone is on it online, and for 30 minutes after the last occupant left or logged out
//     (HoldAfterLeaving; cc-P707: the setting clusterf.tutorial.copyHoldMinutes, 30); then Tick frees it: it removes exactly what its seeding placed (Placed), sweeps anything else
//     left on it (corpses, dropped items; a pet left behind goes home to New Haven), raises its generation and reseeds;
//   - a trainee who logs in on a copy whose generation moved on (it was freed while they were away) goes to a fresh copy
//     at their first undone lesson (OnLogin);
//   - when all 16 are in use, the trainee waits on the Fold's beach (map 32) and is taken to a copy as soon as one frees.
//
// Who is on a copy is read from the world (a player's map, or the map they logged out on), never kept as a list that
// could drift. What the world cannot say is saved here (TutorialCopiesStore): each copy's generation, whether it is
// seeded and in use, when it was last occupied, the serials its seeding placed, each trainee's copy and generation, and
// the waiting line. MapOf (TutorialWorld) reads CopyOf and has no side effects.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server.Engines.PartySystem;
using Server.Logging;
using Server.Mobiles;

namespace Server.Tutorial;

public static class TutorialCopies
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(TutorialCopies));

    /// <summary>
    /// How long a copy stays held after its last occupant left or logged out. cc-P707: the setting
    /// clusterf.tutorial.copyHoldMinutes (30, at least 1), so a test world can watch a copy reset.
    /// </summary>
    public static TimeSpan HoldAfterLeaving =>
        TimeSpan.FromMinutes(Math.Max(1, ServerConfiguration.GetOrUpdateSetting("clusterf.tutorial.copyHoldMinutes", 30)));

    public const string WaitLine =
        "Every copy of the Wakening Isle is in use right now. Wait here on the beach: you will be taken to your own as soon as one is free.";

    public const string FreshCopyLine =
        "Your copy of the Wakening Isle was reset while you were away. You are on a fresh one, at your next lesson.";

    public sealed class Slot
    {
        public Slot(int n) => N = n;
        public int N { get; }
        public int Generation { get; set; }
        public bool Seeded { get; set; }
        public bool InUse { get; set; }
        public DateTime LastSeen { get; set; }
        public List<Serial> Placed { get; } = new();
    }

    internal static readonly Slot[] Slots = Enumerable.Range(1, FoldMaps.CopyCount).Select(n => new Slot(n)).ToArray();

    /// <summary>Each trainee's copy and its generation when they were given it.</summary>
    internal static readonly Dictionary<Serial, (int N, int Generation)> Assigned = new();

    /// <summary>Trainees waiting on the Fold's beach for a copy, first come first served.</summary>
    internal static readonly List<Serial> Waiting = new();

    /// <summary>Seeds a copy; a seam for tests. The default is the isle seeder without New Haven's parts.</summary>
    public static Func<Map, string> SeedCopy { get; set; } = map => TutorialCorePlacement.SeedOn(map, false, false);

    public static void Configure() => _ = TutorialCopiesStore.Instance;

    public static void Initialize() =>
        Timer.DelayCall(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1), () => Tick(Core.Now));

    public static bool IsCopy(Map? map) => map != null && map.MapIndex is >= FoldMaps.FirstCopyIndex and <= FoldMaps.LastIndex;

    public static Slot SlotOf(Map copy) => Slots[copy.MapIndex - FoldMaps.FirstCopyIndex];

    public static Map MapOf(Slot slot) => FoldMaps.Copy(slot.N);

    /// <summary>The copy a character is on, or logged out on; null when neither.</summary>
    public static Map? CopyOf(PlayerMobile pm) =>
        IsCopy(pm.Map) ? pm.Map : pm.Map == Map.Internal && IsCopy(pm.LogoutMap) ? pm.LogoutMap : null;

    /// <summary>The character's copy if it is still the one they were given (same generation, still in use).</summary>
    public static Map? CurrentCopyOf(PlayerMobile pm) =>
        CopyOf(pm) is { } copy && IsCurrent(pm, copy) ? copy : null;

    private static bool IsCurrent(PlayerMobile pm, Map copy)
    {
        var slot = SlotOf(copy);
        return slot.InUse && Assigned.TryGetValue(pm.Serial, out var a) && a.N == slot.N && a.Generation == slot.Generation;
    }

    /// <summary>Players on the copy, online or logged out on it.</summary>
    public static List<PlayerMobile> Occupants(Slot slot)
    {
        var map = MapOf(slot);
        var list = new List<PlayerMobile>();
        foreach (var m in World.Mobiles.Values)
        {
            if (m is PlayerMobile { Deleted: false } pm && CopyOf(pm) == map)
            {
                list.Add(pm);
            }
        }

        return list;
    }

    /// <summary>
    /// The copy this trainee goes to: theirs if current, their party's, or the first free one (seeded first). Null when
    /// all are in use. Records the trainee's copy and generation.
    /// </summary>
    public static Map? Claim(PlayerMobile pm)
    {
        var copy = CurrentCopyOf(pm) ?? PartyCopy(pm);
        if (copy == null)
        {
            var free = Array.Find(Slots, s => !s.InUse);
            if (free == null)
            {
                return null;
            }

            if (!free.Seeded)
            {
                Seed(free);
            }

            free.InUse = true;
            copy = MapOf(free);
        }

        var slot = SlotOf(copy);
        slot.LastSeen = Core.Now;
        Assigned[pm.Serial] = (slot.N, slot.Generation);
        Waiting.Remove(pm.Serial);
        return copy;
    }

    /// <summary>The copy a member of this trainee's party holds now, if any.</summary>
    private static Map? PartyCopy(PlayerMobile pm)
    {
        if (Party.Get(pm) is not { } party)
        {
            return null;
        }

        foreach (var info in party.Members)
        {
            if (info.Mobile is PlayerMobile other && other != pm && CurrentCopyOf(other) is { } copy)
            {
                return copy;
            }
        }

        return null;
    }

    /// <summary>Puts a trainee on the Fold's beach to wait for a copy, and in line.</summary>
    public static void Wait(PlayerMobile pm)
    {
        if (pm.Map != TutorialWorld.Isle)
        {
            TutorialWorld.Cross(pm, TutorialWorld.Arrival, TutorialWorld.Isle);
        }

        if (!Waiting.Contains(pm.Serial))
        {
            Waiting.Add(pm.Serial);
        }

        ClusterFTutorial.SystemLine(pm, WaitLine);
    }

    /// <summary>Where a trainee resumes on a copy: their first undone core lesson's stop, or the beach.</summary>
    public static Point3D NextLessonSpot(PlayerMobile pm, Map copy)
    {
        var done = ClusterFTutorial.RecordOf(pm)?.StationsDone;
        var next = done == null ? null : TutorialStations.Core.FirstOrDefault(s => !done.Contains(s.Key));
        if (next == null || done!.Count == 0)
        {
            return TutorialWorld.Ground(copy, TutorialWorld.Beach);
        }

        var at = TutorialCorePlacement.MarkerSpot(next.Key);
        return TutorialWorld.Ground(copy, new Point2D(at.X, at.Y));
    }

    /// <summary>At login: a trainee on a copy that was freed while they were away goes to a fresh copy.</summary>
    public static void OnLogin(PlayerMobile pm)
    {
        if (!IsCopy(pm.Map))
        {
            return;
        }

        if (IsCurrent(pm, pm.Map))
        {
            SlotOf(pm.Map).LastSeen = Core.Now;
            return;
        }

        if (ClusterFTutorial.IsParticipant(pm))
        {
            TutorialStart.SendToIsle(pm, atNextLesson: true);
            if (IsCopy(pm.Map))
            {
                ClusterFTutorial.SystemLine(pm, FreshCopyLine);
            }
        }
        else
        {
            TutorialStart.SendHome(pm);
        }
    }

    /// <summary>Frees every copy whose occupants have all been gone HoldAfterLeaving, then serves the line.</summary>
    public static void Tick(DateTime now)
    {
        foreach (var slot in Slots)
        {
            if (!slot.InUse)
            {
                continue;
            }

            var map = MapOf(slot);
            var last = slot.LastSeen;
            foreach (var pm in Occupants(slot))
            {
                if (pm.NetState != null && pm.Map == map)
                {
                    last = now;
                    break;
                }

                if (pm.LastOnline > last)
                {
                    last = pm.LastOnline;
                }
            }

            slot.LastSeen = last;
            if (now - last >= HoldAfterLeaving)
            {
                Free(slot);
            }
        }

        ServeLine();
    }

    /// <summary>Takes waiting trainees, oldest first, to copies that are free.</summary>
    public static void ServeLine()
    {
        while (Waiting.Count > 0 && Array.Exists(Slots, s => !s.InUse))
        {
            var serial = Waiting[0];
            Waiting.RemoveAt(0);
            if (World.FindMobile(serial) is PlayerMobile { Deleted: false, NetState: not null } pm &&
                pm.Map == TutorialWorld.Isle && ClusterFTutorial.IsParticipant(pm))
            {
                TutorialStart.SendToIsle(pm, atNextLesson: true);
            }
        }
    }

    /// <summary>Empties a copy (Reset), raises its generation and reseeds it. Returns what Reset removed.</summary>
    public static (int Placed, int Strays, int PetsHome) Free(Slot slot)
    {
        var removed = Reset(slot);
        slot.Generation++;
        slot.InUse = false;
        Seed(slot);
        logger.Information("Tutorial copy {N} freed: removed {Placed} seeded pieces and {Strays} strays, sent {Pets} pets home; reseeded",
            slot.N, removed.Placed, removed.Strays, removed.PetsHome);
        return removed;
    }

    /// <summary>
    /// Removes exactly what the copy's seeding placed, then anything else left on the copy (a corpse, a dropped item, a
    /// spawned pest); a controlled pet left behind goes to New Haven's start rather than being deleted.
    /// </summary>
    public static (int Placed, int Strays, int PetsHome) Reset(Slot slot)
    {
        var map = MapOf(slot);
        var placed = 0;
        foreach (var serial in slot.Placed)
        {
            if (World.FindEntity(serial) is { Deleted: false } e && e.Map == map)
            {
                e.Delete();
                placed++;
            }
        }

        slot.Placed.Clear();
        slot.Seeded = false;
        var strays = 0;
        var pets = 0;
        foreach (var item in World.Items.Values.Where(i => !i.Deleted && i.Map == map && i.Parent == null).ToList())
        {
            item.Delete();
            strays++;
        }

        foreach (var m in World.Mobiles.Values.Where(m => !m.Deleted && m.Map == map && !m.Player).ToList())
        {
            if (m is BaseCreature { Controlled: true, ControlMaster: not null } pet)
            {
                pet.MoveToWorld(HavenSpots.Start, HavenSpots.Map);
                pets++;
            }
            else
            {
                m.Delete();
                strays++;
            }
        }

        return (placed, strays, pets);
    }

    /// <summary>Seeds a free copy and records what it placed.</summary>
    private static void Seed(Slot slot)
    {
        var map = MapOf(slot);
        var result = SeedCopy(map);
        slot.Placed.Clear();
        foreach (var item in World.Items.Values)
        {
            if (!item.Deleted && item.Map == map && item.Parent == null)
            {
                slot.Placed.Add(item.Serial);
            }
        }

        foreach (var m in World.Mobiles.Values)
        {
            if (!m.Deleted && m.Map == map && !m.Player)
            {
                slot.Placed.Add(m.Serial);
            }
        }

        slot.Seeded = true;
        logger.Information("Tutorial copy {N} seeded ({Count} pieces): {Result}", slot.N, slot.Placed.Count, result);
    }

    /// <summary>Staff: [Tutorial copies.</summary>
    public static IEnumerable<string> Report()
    {
        foreach (var slot in Slots)
        {
            var names = Occupants(slot).Select(p => p.NetState != null ? p.Name : $"{p.Name} (away)").ToList();
            yield return $"Copy {slot.N} (map {MapOf(slot)?.MapIndex}): {(slot.InUse ? "in use" : "free")}, " +
                         $"{(slot.Seeded ? $"seeded ({slot.Placed.Count} pieces)" : "not seeded")}, generation {slot.Generation}" +
                         (names.Count > 0 ? $"; {string.Join(", ", names)}" : "");
        }

        yield return $"Waiting on the Fold's beach: {Waiting.Count}.";
    }
}

/// <summary>The copies' saved state (TutorialCopies' header).</summary>
public class TutorialCopiesStore : GenericPersistence
{
    // 0: cc-P664.
    public const int CurrentVersion = 0;

    private static TutorialCopiesStore? _instance;

    public TutorialCopiesStore() : base("TutorialCopies", 10)
    {
    }

    public static TutorialCopiesStore Instance => _instance ??= new TutorialCopiesStore();

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);
        writer.WriteEncodedInt(TutorialCopies.Slots.Length);
        foreach (var slot in TutorialCopies.Slots)
        {
            writer.WriteEncodedInt(slot.Generation);
            writer.Write(slot.Seeded);
            writer.Write(slot.InUse);
            writer.Write(slot.LastSeen);
            writer.WriteEncodedInt(slot.Placed.Count);
            foreach (var serial in slot.Placed)
            {
                writer.Write(serial);
            }
        }

        writer.WriteEncodedInt(TutorialCopies.Assigned.Count);
        foreach (var (serial, (n, generation)) in TutorialCopies.Assigned)
        {
            writer.Write(serial);
            writer.WriteEncodedInt(n);
            writer.WriteEncodedInt(generation);
        }

        writer.WriteEncodedInt(TutorialCopies.Waiting.Count);
        foreach (var serial in TutorialCopies.Waiting)
        {
            writer.Write(serial);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"TutorialCopiesStore version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var generation = reader.ReadEncodedInt();
            var seeded = reader.ReadBool();
            var inUse = reader.ReadBool();
            var lastSeen = reader.ReadDateTime();
            var placed = reader.ReadEncodedInt();
            var serials = new List<Serial>(placed);
            for (var j = 0; j < placed; j++)
            {
                serials.Add(reader.ReadSerial());
            }

            if (i < TutorialCopies.Slots.Length) // the pool only ever grows (FoldMaps)
            {
                var slot = TutorialCopies.Slots[i];
                slot.Generation = generation;
                slot.Seeded = seeded;
                slot.InUse = inUse;
                slot.LastSeen = lastSeen;
                slot.Placed.Clear();
                slot.Placed.AddRange(serials);
            }
        }

        TutorialCopies.Assigned.Clear();
        var assigned = reader.ReadEncodedInt();
        for (var i = 0; i < assigned; i++)
        {
            var serial = reader.ReadSerial();
            var n = reader.ReadEncodedInt();
            var generation = reader.ReadEncodedInt();
            TutorialCopies.Assigned[serial] = (n, generation);
        }

        TutorialCopies.Waiting.Clear();
        var waiting = reader.ReadEncodedInt();
        for (var i = 0; i < waiting; i++)
        {
            TutorialCopies.Waiting.Add(reader.ReadSerial());
        }
    }
}
