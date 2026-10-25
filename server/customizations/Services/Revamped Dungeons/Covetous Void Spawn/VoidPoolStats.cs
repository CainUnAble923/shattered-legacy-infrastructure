// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/VoidPoolStats.cs (cc-P194).
//
// The scoreboards behind the Void Pool gump, one set per controller (one per facet): each player's best single
// battle, each player's overall total (the battles' scores plus the level-3 kills' points), the twenty best
// battles, and the record wave (announced shard-wide when beaten: "A new Void Pool Invasion record has been
// made: N!"). Saved with the world through GenericPersistence "VoidPoolStats" (ServUO: Saves/VoidPoolStats/
// VoidPoolStats.bin, version 1: the controller, the record wave, the three tables), fresh content so only
// version 1 is read.
//
// Note on CheckAddTop20, carried as ServUO wrote it: when the finished battle beats one of the twenty, the
// LOWEST of the twenty is removed and the one it beat is added again (so the new battle is never stored). That is
// ServUO's behaviour, copied rather than fixed, and recorded in the notes as a question for Chase (the D-4 rule).

using System;
using System.Collections.Generic;
using System.Linq;

namespace Server.Engines.VoidPool;

public class VoidPoolStats
{
    private static VoidPoolStatsPersistence _persistence;

    public VoidPoolStats(VoidPoolController controller)
    {
        BestSingle = new Dictionary<Mobile, long>();
        OverallTotal = new Dictionary<Mobile, long>();
        Top20 = new List<Dictionary<Mobile, long>>();

        Controller = controller;
        Stats.Add(this);
    }

    private VoidPoolStats()
    {
        BestSingle = new Dictionary<Mobile, long>();
        OverallTotal = new Dictionary<Mobile, long>();
        Top20 = new List<Dictionary<Mobile, long>>();
    }

    public static List<VoidPoolStats> Stats { get; } = new();

    public VoidPoolController Controller { get; private set; }

    public Dictionary<Mobile, long> BestSingle { get; }
    public Dictionary<Mobile, long> OverallTotal { get; }
    public List<Dictionary<Mobile, long>> Top20 { get; }
    public BestWave BestWave { get; set; }

    public static void Configure()
    {
        _persistence = new VoidPoolStatsPersistence();
    }

    public static VoidPoolStats GetStats(VoidPoolController controller)
    {
        var stats = Stats.FirstOrDefault(s => s.Controller == controller);

        return stats ?? new VoidPoolStats(controller);
    }

    /// <summary>Test support: forgets every scoreboard.</summary>
    public static void Clear() => Stats.Clear();

    // cc-P610 (P489 #6): every player's best is checked; ServUO returned after the first one that improved.
    public static bool CheckBestSingle(VoidPoolController controller)
    {
        var stats = GetStats(controller);
        var changed = false;

        foreach (var (m, score) in controller.CurrentScore)
        {
            if (!stats.BestSingle.TryGetValue(m, out var best) || score > best)
            {
                stats.BestSingle[m] = score;
                changed = true;
            }
        }

        return changed;
    }

    public static void AddToOverallTotal(VoidPoolController controller)
    {
        var stats = GetStats(controller);

        foreach (var (m, score) in controller.CurrentScore)
        {
            stats.OverallTotal[m] = stats.OverallTotal.GetValueOrDefault(m) + score;
        }
    }

    public static bool CheckAddTop20(VoidPoolController controller)
    {
        var total = GetCollectiveScore(controller.CurrentScore);
        var stats = GetStats(controller);

        var copy = new List<Dictionary<Mobile, long>>(stats.Top20);

        foreach (var s in copy.OrderBy(dic => -GetCollectiveScore(dic)))
        {
            if (total > GetCollectiveScore(s))
            {
                stats.Top20.Remove(copy[^1]);
                stats.Top20.Add(s);
                return true;
            }
        }

        return false;
    }

    public static long GetCollectiveScore(Dictionary<Mobile, long> score)
    {
        if (score == null)
        {
            return 0;
        }

        long s = 0;

        foreach (var i in score.Values)
        {
            s += i;
        }

        return s;
    }

    public static void CheckBestWave(VoidPoolController controller)
    {
        var stats = GetStats(controller);
        var wave = controller.Wave;

        if (stats.BestWave == null || wave > stats.BestWave.Waves)
        {
            stats.BestWave = new BestWave(controller.CurrentScore, wave);
            Timer.DelayCall(
                TimeSpan.FromSeconds(1.5),
                () => World.Broadcast(2072, false, $"A new Void Pool Invasion record has been made: {wave}!")
            );
        }
    }

    public static void OnInvasionEnd(VoidPoolController controller)
    {
        CheckAddTop20(controller);
        CheckBestSingle(controller);
        AddToOverallTotal(controller);
        CheckBestWave(controller);
    }

    private static void WriteTable(IGenericWriter writer, Dictionary<Mobile, long> table)
    {
        writer.Write(table.Count);

        foreach (var (m, score) in table)
        {
            writer.Write(m);
            writer.Write(score);
        }
    }

    private static void ReadTable(IGenericReader reader, Dictionary<Mobile, long> table)
    {
        var count = reader.ReadInt();

        for (var i = 0; i < count; i++)
        {
            var m = reader.ReadEntity<Mobile>();
            var score = reader.ReadLong();

            if (m != null)
            {
                table[m] = score;
            }
        }
    }

    public void Serialize(IGenericWriter writer)
    {
        writer.Write(1); // version

        writer.Write(Controller);

        if (BestWave != null)
        {
            writer.Write(1);
            BestWave.Serialize(writer);
        }
        else
        {
            writer.Write(0);
        }

        WriteTable(writer, BestSingle);
        WriteTable(writer, OverallTotal);

        writer.Write(Top20.Count);

        foreach (var dic in Top20)
        {
            WriteTable(writer, dic);
        }
    }

    private static VoidPoolStats Deserialize(IGenericReader reader)
    {
        var stats = new VoidPoolStats();

        var version = reader.ReadInt();

        if (version != 1)
        {
            throw new System.IO.InvalidDataException($"VoidPoolStats version {version} is not one this build reads (1).");
        }

        stats.Controller = reader.ReadEntity<VoidPoolController>();

        if (reader.ReadInt() == 1)
        {
            stats.BestWave = new BestWave(reader);
        }

        ReadTable(reader, stats.BestSingle);
        ReadTable(reader, stats.OverallTotal);

        var count = reader.ReadInt();

        for (var i = 0; i < count; i++)
        {
            var dic = new Dictionary<Mobile, long>();
            ReadTable(reader, dic);

            if (dic.Count > 0)
            {
                stats.Top20.Add(dic);
            }
        }

        return stats;
    }

    public static void SerializeAll(IGenericWriter writer)
    {
        writer.Write(1); // version

        writer.Write(Stats.Count);

        foreach (var stats in Stats)
        {
            stats.Serialize(writer);
        }
    }

    public static void DeserializeAll(IGenericReader reader)
    {
        Stats.Clear();

        var version = reader.ReadInt();

        if (version != 1)
        {
            throw new System.IO.InvalidDataException($"VoidPoolStats save version {version} is not one this build reads (1).");
        }

        var count = reader.ReadInt();

        for (var i = 0; i < count; i++)
        {
            var stats = Deserialize(reader);

            if (stats.Controller != null)
            {
                Stats.Add(stats);
            }
        }
    }
}

public sealed class VoidPoolStatsPersistence : GenericPersistence
{
    public VoidPoolStatsPersistence() : base("VoidPoolStats", 10)
    {
    }

    public override void Serialize(IGenericWriter writer) => VoidPoolStats.SerializeAll(writer);

    public override void Deserialize(IGenericReader reader) => VoidPoolStats.DeserializeAll(reader);
}

public class BestWave
{
    public BestWave(Dictionary<Mobile, long> score, int waves)
    {
        // cc-P610 (P489 #7): a copy. ServUO kept the battle's own table, which the next battle's start clears.
        Score = new Dictionary<Mobile, long>(score);

        TotalScore = VoidPoolStats.GetCollectiveScore(score);
        Waves = waves;
        Date = DateTime.Now.ToShortDateString();
    }

    public BestWave(IGenericReader reader)
    {
        reader.ReadInt(); // version
        Waves = reader.ReadInt();
        TotalScore = reader.ReadLong();
        Date = reader.ReadString();

        Score = new Dictionary<Mobile, long>();

        var cnt = reader.ReadInt();

        for (var i = 0; i < cnt; i++)
        {
            var m = reader.ReadEntity<Mobile>();
            var score = reader.ReadLong();

            if (m != null)
            {
                Score[m] = score;
            }
        }
    }

    public Dictionary<Mobile, long> Score { get; }
    public int Waves { get; }
    public long TotalScore { get; }
    public string Date { get; }

    public void Serialize(IGenericWriter writer)
    {
        writer.Write(0);
        writer.Write(Waves);
        writer.Write(TotalScore);
        writer.Write(Date);

        writer.Write(Score.Count);

        foreach (var (m, score) in Score)
        {
            writer.Write(m);
            writer.Write(score);
        }
    }
}
