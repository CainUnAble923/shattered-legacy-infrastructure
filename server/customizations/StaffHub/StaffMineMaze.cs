// StaffMineMaze.cs
//
// cc-P810 (P797 rank 3): a mine maze a GM lays out from the Staff Hub's Events tab (StaffEventsGump) or [StaffEvents maze.
// Our own build of the idea in ExX's Landmine maze (third-party, reference only; no code taken, and its one-mine-at-a-time
// placing and token are replaced by a field laid from a seed).
//
// - The GM marks two corners. The maze runs along the longer side, from the end nearest the first corner (the start row)
//   to the far end (the finish row). Hidden mines cover MazeMinePercent of the tiles off one safe path, which wanders but
//   always connects start to finish. The start and finish rows are safe. The layout comes from a seed, so it is the same
//   for everyone in a run, and players learn it by dying in it.
// - The maze is a region (MineMazeRegion). A player may only walk in over the start row; Young players stay out; no recall,
//   gate, mark or teleport in or out. Stepping on a mine: a blast, and death. A death in the maze keeps every item (the
//   corpse's contents go back to the backpack, as a surge's no-loot zone does), and after MazeReviveSeconds the player is
//   raised at the start row.
// - The first MazeWinners living players to reach the finish row win, once each: Festival Tokens, or a small loot box
//   (MazePrize). The shard is told each place.
// - Reset (the fair reset): a new seed, so the mines move; the winners are cleared and everyone inside goes back to the
//   start row. End removes the maze. Off unless a GM lays one; one at a time.
// - A maze survives a restart: its corners, seed and winners are saved (StaffMineMazeStore, GenericPersistence, versioned,
//   D40) and the region is registered again at world load.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using Server.Engines.RewardStores;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Regions;
using Server.Spells;

namespace Server;

public sealed class MineMazeRun
{
    public Map Map;
    public int X1, Y1, X2, Y2;   // inclusive, X1 <= X2, Y1 <= Y2
    public int Z;
    public bool AlongX;          // the maze runs along X (start and finish are columns), else along Y
    public bool FromLow;         // the start row is at the low end of the axis
    public int Seed;
    public int MinePercent;
    public readonly List<Mobile> Winners = [];
    public readonly HashSet<Point2D> Mines = [];
    public List<Point2D> Path = [];
    public MineMazeRegion Region;

    public int Length => AlongX ? X2 - X1 + 1 : Y2 - Y1 + 1;
    public int Width => AlongX ? Y2 - Y1 + 1 : X2 - X1 + 1;

    public bool Contains(int x, int y) => x >= X1 && x <= X2 && y >= Y1 && y <= Y2;

    /// <summary>How far along the maze a tile is: 0 on the start row, Length - 1 on the finish row.</summary>
    public int Step(int x, int y)
    {
        var raw = AlongX ? x - X1 : y - Y1;
        return FromLow ? raw : Length - 1 - raw;
    }

    /// <summary>The tile at a step along the maze and a lane across it.</summary>
    public Point2D Tile(int step, int lane)
    {
        var raw = FromLow ? step : Length - 1 - step;
        return AlongX ? new Point2D(X1 + raw, Y1 + lane) : new Point2D(X1 + lane, Y1 + raw);
    }

    public bool IsStart(int x, int y) => Contains(x, y) && Step(x, y) == 0;
    public bool IsFinish(int x, int y) => Contains(x, y) && Step(x, y) == Length - 1;
    public bool IsMine(int x, int y) => Mines.Contains(new Point2D(x, y));

    /// <summary>The middle of the start row, where the dead are raised and Reset sends everyone.</summary>
    public Point3D StartSpot()
    {
        var p = Tile(0, Width / 2);
        return new Point3D(p.X, p.Y, Map?.GetAverageZ(p.X, p.Y) ?? Z);
    }

    /// <summary>Lays the mines from the seed: one safe path that only moves forward or sideways, then mines elsewhere.</summary>
    public List<Point2D> Generate(int minePercent)
    {
        var rng = new System.Random(Seed);
        var safe = new HashSet<Point2D>();
        var lane = rng.Next(Width);
        var path = new List<Point2D>();

        for (var step = 0; step < Length; step++)
        {
            Mark(step, lane);

            // Wander across the maze before the next step forward: every tile walked is safe, so the path is joined.
            var drift = rng.Next(3) - 1;
            var moves = rng.Next(1, Math.Max(2, Width / 2));
            for (var i = 0; i < moves && step < Length - 1; i++)
            {
                var next = lane + drift;
                if (next < 0 || next >= Width)
                {
                    break;
                }

                lane = next;
                Mark(step, lane);
            }
        }

        Mines.Clear();
        Path = path;
        for (var step = 1; step < Length - 1; step++)
        {
            for (var l = 0; l < Width; l++)
            {
                var t = Tile(step, l);
                if (!safe.Contains(t) && rng.Next(100) < minePercent)
                {
                    Mines.Add(t);
                }
            }
        }

        return path;

        void Mark(int s, int l)
        {
            var t = Tile(s, l);
            if (safe.Add(t))
            {
                path.Add(t);
            }
        }
    }
}

public class MineMazeRegion : BaseRegion
{
    public MineMazeRegion(MineMazeRun run, Region parent)
        : base(null, run.Map, parent, (parent?.Priority ?? DefaultPriority) + 1,
            new Rectangle3D(new Point3D(run.X1, run.Y1, MinZ), new Point3D(run.X2 + 1, run.Y2 + 1, MaxZ)))
    {
        Run = run;
    }

    public MineMazeRun Run { get; }

    private static bool Exempt(Mobile m) => m is not PlayerMobile || m.AccessLevel > AccessLevel.Player;

    public override bool OnMoveInto(Mobile m, Direction d, Point3D newLocation, Point3D oldLocation)
    {
        if (!Exempt(m) && !Contains(oldLocation))
        {
            if (m is PlayerMobile { Young: true })
            {
                m.SendMessage("Young characters may not enter the mine maze.");
                return false;
            }

            if (!Run.IsStart(newLocation.X, newLocation.Y))
            {
                m.SendMessage("The mine maze is entered over its start row.");
                return false;
            }
        }

        return base.OnMoveInto(m, d, newLocation, oldLocation);
    }

    public override void OnLocationChanged(Mobile m, Point3D oldLocation)
    {
        base.OnLocationChanged(m, oldLocation);

        if (!Exempt(m) && m.Alive)
        {
            StaffMineMaze.OnStep((PlayerMobile)m);
        }
    }

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;
        return Exempt(m) && base.CheckTravel(m, newLocation, travelType, out message);
    }

    public override bool AllowHousing(Mobile from, Point3D p) => false;
}

public static class StaffMineMaze
{
    public const int BroadcastHue = 0x35;

    private static StaffEventsSettings S => StaffEventsSettings.Current;

    public static MineMazeRun Active { get; private set; }

    // Each GM's corners, marked before the maze is laid.
    private static readonly Dictionary<Mobile, (Map Map, Point3D First, Point3D? Second)> _corners = new();

    public static Func<int> NewSeed = () => Utility.Random(int.MaxValue);

    public static (Map Map, Point3D First, Point3D? Second)? CornersOf(Mobile gm) =>
        _corners.TryGetValue(gm, out var c) ? c : null;

    public static void MarkCorner(Mobile gm, Map map, Point3D p, bool first)
    {
        if (first || !_corners.TryGetValue(gm, out var c) || c.Map != map)
        {
            _corners[gm] = (map, p, null);
        }
        else
        {
            _corners[gm] = (map, c.First, p);
        }
    }

    /// <summary>Lays a maze between the GM's two corners.</summary>
    public static bool Start(Mobile from, out string message)
    {
        if (CornersOf(from) is not { Second: { } second } c)
        {
            message = "Mark both corners first.";
            return false;
        }

        return Start(c.Map, c.First, second, out message);
    }

    public static bool Start(Map map, Point3D first, Point3D second, out string message)
    {
        if (Active != null)
        {
            message = "A mine maze is already laid out. End it first.";
            return false;
        }

        if (map == null || map == Map.Internal || map == Tutorial.TutorialWorld.Isle)
        {
            message = "Not there: pick a spot on a facet, off the Wakening Isle.";
            return false;
        }

        var run = new MineMazeRun
        {
            Map = map,
            X1 = Math.Min(first.X, second.X), X2 = Math.Max(first.X, second.X),
            Y1 = Math.Min(first.Y, second.Y), Y2 = Math.Max(first.Y, second.Y),
            Z = first.Z
        };

        int w = run.X2 - run.X1 + 1, h = run.Y2 - run.Y1 + 1;
        if (Math.Min(w, h) < S.MazeMinSide || w * h > S.MazeMaxTiles)
        {
            message = $"The maze is {w} x {h}: each side at least {S.MazeMinSide}, at most {S.MazeMaxTiles} tiles in all.";
            return false;
        }

        run.AlongX = w >= h;
        run.FromLow = run.AlongX ? first.X == run.X1 : first.Y == run.Y1;
        run.Seed = NewSeed();
        run.MinePercent = S.MazeMinePercent;
        run.Generate(run.MinePercent);
        Register(run);

        message = $"Mine maze laid: {w} x {h}, {run.Mines.Count} mines, start row nearest your first corner.";
        World.Broadcast(BroadcastHue, false,
            $"A mine maze is open in {map.Name}! The first {S.MazeWinners} to cross it win. Watch your step.");
        return true;
    }

    private static void Register(MineMazeRun run)
    {
        var center = new Point3D((run.X1 + run.X2) / 2, (run.Y1 + run.Y2) / 2, run.Z);
        run.Region = new MineMazeRegion(run, Region.Find(center, run.Map));
        run.Region.Register();
        Active = run;
    }

    /// <summary>The fair reset: the mines move, the winners are cleared, and everyone inside starts again.</summary>
    public static bool Reset(out string message)
    {
        if (Active is not { } run)
        {
            message = "No mine maze is laid out.";
            return false;
        }

        var seed = NewSeed();
        run.Seed = seed == run.Seed ? seed + 1 : seed;
        run.MinePercent = S.MazeMinePercent;
        run.Generate(run.MinePercent);
        run.Winners.Clear();

        foreach (var m in Inside(run))
        {
            if (m is PlayerMobile && m.AccessLevel == AccessLevel.Player)
            {
                m.MoveToWorld(run.StartSpot(), run.Map);
            }
        }

        World.Broadcast(BroadcastHue, false, "The mine maze has been reset: the mines have moved, and every prize is open again.");
        message = $"Mine maze reset: {run.Mines.Count} mines in new places.";
        return true;
    }

    public static bool End(out string message)
    {
        if (Active is not { } run)
        {
            message = "No mine maze is laid out.";
            return false;
        }

        run.Region?.Unregister();
        Active = null;
        message = $"Mine maze removed. Winners: {(run.Winners.Count == 0 ? "none" : string.Join(", ", run.Winners.Select(w => w.Name)))}.";
        World.Broadcast(BroadcastHue, false, "The mine maze is closed.");
        return true;
    }

    /// <summary>A living player's step inside the maze.</summary>
    public static void OnStep(PlayerMobile pm)
    {
        if (Active is not { } run || pm.Map != run.Map)
        {
            return;
        }

        if (run.IsMine(pm.X, pm.Y))
        {
            Blast(pm, run);
        }
        else if (run.IsFinish(pm.X, pm.Y))
        {
            Finish(pm, run);
        }
    }

    private static void Blast(PlayerMobile pm, MineMazeRun run)
    {
        Effects.SendLocationEffect(pm.Location, pm.Map, 0x36BD, 20);
        Effects.PlaySound(pm.Location, pm.Map, 0x307);
        pm.SendMessage(0x22, "You stepped on a mine!");
        pm.Kill();
    }

    private static void Finish(PlayerMobile pm, MineMazeRun run)
    {
        if (run.Winners.Contains(pm))
        {
            return;
        }

        if (run.Winners.Count >= S.MazeWinners)
        {
            pm.SendMessage("You crossed the mine maze, but every prize has been won.");
            return;
        }

        run.Winners.Add(pm);
        var place = run.Winners.Count;
        Award(pm, place);
        World.Broadcast(BroadcastHue, false, $"{pm.Name} has crossed the mine maze, place {place} of {S.MazeWinners}!");
    }

    private static void Award(PlayerMobile pm, int place)
    {
        if (S.PrizeIsBox && Enum.TryParse<BoxTheme>(S.MazeBoxTheme, true, out var theme))
        {
            var box = ClusterFLootBoxes.MakeBox(pm, theme, S.MazeBoxTier, null, "the mine maze", null);
            if (pm.Backpack?.TryDropItem(pm, box, false) != true)
            {
                pm.BankBox.DropItem(box);
            }

            pm.SendMessage(0x44, $"You win a {box.DefaultName} for crossing the mine maze (place {place}).");
        }
        else
        {
            FestivalTokens.Grant(pm, S.MazeTokens, $"the mine maze, place {place}");
        }
    }

    /// <summary>Every mobile standing in the maze now.</summary>
    public static List<Mobile> Inside(MineMazeRun run)
    {
        var list = new List<Mobile>();
        foreach (var m in run.Map.GetMobilesInBounds(new Rectangle2D(run.X1, run.Y1, run.X2 - run.X1 + 1, run.Y2 - run.Y1 + 1)))
        {
            list.Add(m);
        }

        return list;
    }

    public static bool InMaze(Mobile m) =>
        Active is { } run && m.Map == run.Map && run.Contains(m.X, m.Y);

    // A death in the maze keeps every item, and the player is raised at the start row.
    [OnEvent(nameof(PlayerMobile.PlayerDeathEvent))]
    public static void OnPlayerDeath(PlayerMobile pm)
    {
        if (!InMaze(pm))
        {
            return;
        }

        if (pm.Corpse is { Deleted: false } corpse && pm.Backpack is { } pack)
        {
            foreach (var item in corpse.Items.ToList())
            {
                pack.DropItem(item);
            }
        }

        pm.SendMessage(0x44, $"Your belongings are safe in your backpack. You will rise at the maze's start in {S.MazeReviveSeconds} seconds.");
        Timer.DelayCall(TimeSpan.FromSeconds(S.MazeReviveSeconds), Revive, pm);
    }

    public static void Revive(PlayerMobile pm)
    {
        if (pm.Deleted || pm.Alive || Active is not { } run)
        {
            return;
        }

        pm.MoveToWorld(run.StartSpot(), run.Map);
        pm.Resurrect();
        pm.Hits = pm.HitsMax;
    }

    /// <summary>For staff: the safe path from the start row to the finish row, as x,y pairs.</summary>
    public static string PathLine() =>
        Active is { } run ? "Safe path: " + string.Join(" ", run.Path.Select(p => $"{p.X},{p.Y}")) : "No mine maze is laid out.";

    public static List<string> Status()
    {
        if (Active is not { } run)
        {
            return ["Mine maze: none laid out."];
        }

        return
        [
            $"Mine maze: {run.X1},{run.Y1} to {run.X2},{run.Y2} in {run.Map.Name}, {run.Mines.Count} mines",
            $"Winners {run.Winners.Count} of {S.MazeWinners}: {(run.Winners.Count == 0 ? "none yet" : string.Join(", ", run.Winners.Select(w => w.Name)))}"
        ];
    }

    // ---------------------------------------------------------------- save

    internal static void Write(IGenericWriter w)
    {
        var run = Active;
        w.Write(run != null);
        if (run == null)
        {
            return;
        }

        w.Write(run.Map);
        w.Write(run.X1);
        w.Write(run.Y1);
        w.Write(run.X2);
        w.Write(run.Y2);
        w.Write(run.Z);
        w.Write(run.AlongX);
        w.Write(run.FromLow);
        w.Write(run.Seed);
        w.Write(run.MinePercent);
        w.WriteEncodedInt(run.Winners.Count);
        foreach (var m in run.Winners)
        {
            w.Write(m);
        }
    }

    internal static void Read(IGenericReader r)
    {
        ForgetForTests();

        if (!r.ReadBool())
        {
            return;
        }

        var run = new MineMazeRun
        {
            Map = r.ReadMap(),
            X1 = r.ReadInt(), Y1 = r.ReadInt(), X2 = r.ReadInt(), Y2 = r.ReadInt(), Z = r.ReadInt(),
            AlongX = r.ReadBool(), FromLow = r.ReadBool(), Seed = r.ReadInt(), MinePercent = r.ReadInt()
        };

        var count = r.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            if (r.ReadEntity<Mobile>() is { } m)
            {
                run.Winners.Add(m);
            }
        }

        if (run.Map == null || run.Map == Map.Internal)
        {
            return;
        }

        run.Generate(run.MinePercent);
        _pending = run; // registered once the world (and its regions) has loaded
    }

    private static MineMazeRun _pending;

    public static void Configure() => EventSink.WorldLoad += RegisterPending;

    /// <summary>World load: the saved maze's region is registered again. Idempotent; the facts call it directly.</summary>
    public static void RegisterPending()
    {
        if (_pending is { } run && Active == null)
        {
            Register(run);
        }

        _pending = null;
    }

    /// <summary>For the facts: drops the maze and the marked corners without broadcasting.</summary>
    public static void ForgetForTests()
    {
        Active?.Region?.Unregister();
        Active = null;
        _pending = null;
        _corners.Clear();
    }
}

/// <summary>The maze's save; versioned, an unknown version fails loudly (D40).</summary>
public class StaffMineMazeStore : GenericPersistence
{
    public const int CurrentVersion = 0;

    private static StaffMineMazeStore _instance;

    public StaffMineMazeStore() : base("StaffMineMaze", 10)
    {
    }

    public static void Configure() => _instance ??= new StaffMineMazeStore();

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);
        StaffMineMaze.Write(writer);
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"StaffMineMazeStore version {version} is not one this build can read.");
        }

        StaffMineMaze.Read(reader);
    }
}
