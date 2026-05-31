using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Mobiles;

namespace Server.Items;

/// <summary>
/// Shattered Legacy — Seasoned Lumber Satchel (Tier 2).
///
/// Upgrade from Foresters' Lumber Satchel (Tier 1) via the Foresters' Guildmaster.
///
/// Properties:
///   - 55% weight reduction on contents
///   - 600-stone content capacity (270 effective after reduction)
///   - Accepts: same as T1 (all log and board types)
///   - Blessed — not dropped on death
///
/// Tier 2 Feature — Process All Logs:
///   Right-click context menu "Smelt Metal" option (displayed label; TODO: use 'Process Logs' cliloc).
///   Requires the player to have a hatchet or axe equipped (Layer.TwoHanded).
///   Converts all logs in the satchel to their matching board types at a 1:1 ratio.
///   Boards are placed back into the satchel first; overflow goes to backpack.
///   The player does not need to be near a saw bench — this tier represents
///   field-grade milling proficiency.
/// </summary>
[SerializationGenerator(0, false)]
public partial class SeasonedLumberSatchel : ForestersLumberSatchel
{
    private const int TierHue = 0x0060; // forest green — matches Forester's Logbook

    protected override int TierWeightReductionPct => 80;   // 80% reduction
    protected override int TierMaxContentWeight    => 4000; // ~2,000 boards/logs
    public    override double DefaultWeight        => 3.5;

    [Constructible]
    public SeasonedLumberSatchel() : base()
    {
        Hue  = TierHue;
        Name = "Seasoned Lumber Satchel";
    }

    // ── Log → Board conversion map ────────────────────────────────────────────

    private static readonly Dictionary<Type, Func<Item>> _logToBoard = new()
    {
        { typeof(Log),           () => new Board()          },
        { typeof(OakLog),        () => new OakBoard()       },
        { typeof(AshLog),        () => new AshBoard()       },
        { typeof(YewLog),        () => new YewBoard()       },
        { typeof(HeartwoodLog),  () => new HeartwoodBoard() },
        { typeof(BloodwoodLog),  () => new BloodwoodBoard() },
        { typeof(FrostwoodLog),  () => new FrostwoodBoard() },
        { typeof(IronwoodLog),   () => new IronwoodBoard()  },
        { typeof(GhostwoodLog),  () => new GhostwoodBoard() },
        { typeof(EmberbarkLog),  () => new EmberbarkBoard() },
        { typeof(FrostbarkLog),  () => new FrostbarkBoard() },
        { typeof(ShadowbarkLog), () => new ShadowbarkBoard()},
        { typeof(RunewoodLog),   () => new RunewoodBoard()  },
        { typeof(VoidwoodLog),   () => new VoidwoodBoard()  },
        { typeof(StarwoodLog),   () => new StarwoodBoard()  },
    };

    // ── Context menu ──────────────────────────────────────────────────────────

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from.Alive)
        {
            var canProcess = IsChildOf(from.Backpack) && HasProcessableLogs() && HasAxeEquipped(from);
            list.Add(new ProcessAllLogsEntry(canProcess));
        }
    }

    private bool HasProcessableLogs()
    {
        foreach (var item in Items)
            if (item is Log) return true;
        return false;
    }

    private static bool HasAxeEquipped(Mobile m)
    {
        return m.FindItemOnLayer(Layer.TwoHanded) is BaseAxe;
    }

    // ── Process All Logs ──────────────────────────────────────────────────────

    public void ProcessAllLogs(Mobile from)
    {
        if (!from.CheckAlive()) return;

        if (!IsChildOf(from.Backpack))
        {
            from.SendMessage("The satchel must be in your backpack to process logs.");
            return;
        }

        if (from.FindItemOnLayer(Layer.TwoHanded) is not BaseAxe axe)
        {
            from.SendMessage("You must have a hatchet or axe equipped to process logs.");
            return;
        }

        // Gather all logs currently in the satchel.
        var logs = new List<Log>();
        foreach (var item in Items)
            if (item is Log log) logs.Add(log);

        if (logs.Count == 0)
        {
            from.SendMessage("There are no logs in the satchel to process.");
            return;
        }

        var processed  = 0;
        var skipped    = 0;
        var totalBoards = 0;

        foreach (var log in logs)
        {
            if (!_logToBoard.TryGetValue(log.GetType(), out var factory))
            {
                // Unknown log subtype — skip rather than silently lose it.
                skipped++;
                continue;
            }

            var board  = factory();
            board.Amount = log.Amount;
            log.Delete();

            // Place board back into satchel; fall back to backpack if full.
            if (!TryDropItem(from, board, false))
                from.AddToBackpack(board);

            totalBoards += board.Amount;
            processed++;
        }

        from.PlaySound(0x23D); // chopping/sawing sound

        if (processed > 0)
            from.SendMessage($"You process {processed} stack(s) of logs into {totalBoards} board(s).");
        if (skipped > 0)
            from.SendMessage($"{skipped} log type(s) could not be processed and were left in the satchel.");
    }

    // ── Context menu entry ────────────────────────────────────────────────────

    private sealed class ProcessAllLogsEntry : ContextMenuEntry
    {
        // TODO: replace 6277 ("Smelt Metal") with a 'Process Logs' or 'Chop Wood' cliloc
        // when the appropriate cliloc number is confirmed for the client build.
        public ProcessAllLogsEntry(bool enabled) : base(6277) => Enabled = enabled;

        public override void OnClick(Mobile from, IEntity target)
        {
            if (from.CheckAlive() && target is SeasonedLumberSatchel { Deleted: false } satchel)
                satchel.ProcessAllLogs(from);
        }
    }

    // ── Serialization (v0 — no custom fields) ─────────────────────────────────

    private void Deserialize(IGenericReader reader, int version) { }
}
