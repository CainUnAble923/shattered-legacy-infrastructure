// cc-P18, F-1: Craft X, keep going on failure. Ours (Chase, 2026-09-29/30); in the register.
//
// Builds on our make-X (CraftCountGump.cs, the CraftGumpItem-MakeX patch), which stopped on the first failed attempt
// and, after a refused attempt, stayed armed and said nothing (cc-P17 PT-04). A run now:
//   - counts finished items (default) or attempts, or with "exceptional only" finished exceptional items;
//   - is capped: ItemsCap in items mode (exceptional only included), AttemptsCap in attempts mode. A larger number is
//     lowered to the cap and the summary says so (Chase, 2026-09-30);
//   - keeps the normal craft delay: every attempt is an ordinary CraftSystem.CreateItem, timer and all, and uses
//     materials exactly as a hand-made attempt does;
//   - stops on the first hard stop with a message: out of materials, tool worn out, backpack full, overweight, moved,
//     died, or the progress gump closed (its Stop button, or closing it; Chase, 2026-09-30);
//   - ends with a summary line, e.g. "Made 20 (7 failed, 1 exceptional)."
// This file is the loop, counts, caps, stops and summary. What happens to a non-exceptional result in "exceptional
// only" is ClusterFCraftRejects.cs, so either can change without the other.
//
// CraftItem.cs (our replacement) calls AfterAttempt when an attempt ends and Refused when an attempt is refused before
// or at its end; neither does anything when no run is going, so a single craft is unchanged.

using System;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Network;

namespace Server.Engines.Craft;

public enum MakeXStop
{
    Done,
    Materials,
    Tool,
    PackFull,
    Overweight,
    Moved,
    Died,
    Stopped,
    CannotCraft,
    OtherCraft
}

public sealed class MakeXRun
{
    public Mobile From { get; init; }
    public CraftSystem System { get; init; }
    public CraftItem Item { get; init; }
    public Type TypeRes { get; init; }
    public BaseTool Tool { get; init; }

    public int Requested { get; init; }
    public int Target { get; init; }
    public bool Capped { get; init; }
    public bool CountAttempts { get; init; }
    public bool ExceptionalOnly { get; init; }
    public Container RejectsBag { get; init; }

    public Point3D Start { get; init; }
    public Map StartMap { get; init; }

    public int Attempts { get; set; }
    public int Made { get; set; }
    public int Failed { get; set; }
    public int Exceptional { get; set; }

    // What happened to the rejects (exceptional only): ClusterFCraftRejects.
    public int Smelted { get; set; }
    public int Cut { get; set; }
    public int ToSalvageBag { get; set; }
    public int Trashed { get; set; } // into the crafter's own trash bag (cc-P33, F-3)
    public int Bagged { get; set; }
    public SalvageBag SalvageBag { get; set; }

    public bool StopRequested { get; set; }
    public MakeXStop? StoppedBy { get; set; }
    public string Summary { get; set; }

    public int Counted => CountAttempts ? Attempts : ExceptionalOnly ? Exceptional : Made;

    public int Remaining => Math.Max(Target - Counted, 0);
}

public static class ClusterFCraftRun
{
    // Chase, 2026-09-29: "for now", one constant each.
    public const int ItemsCap = 100;
    public const int AttemptsCap = 500;

    // An item that can carry a maker's mark is one that has a quality; IsMarkable is also false for a recipe the
    // craft system forces non-exceptional (CraftItem.cs, ForceNonExceptional).
    public static bool CanBeExceptional(CraftItem item) => item.IsMarkable(item.ItemType);

    public static int CapFor(bool countAttempts) => countAttempts ? AttemptsCap : ItemsCap;

    public static MakeXRun Begin(
        Mobile from, CraftSystem system, CraftItem item, BaseTool tool, Type typeRes, int requested,
        bool countAttempts, bool exceptionalOnly, Container rejectsBag
    )
    {
        var context = system.GetContext(from);

        if (context == null)
        {
            return null;
        }

        End(context, MakeXStop.OtherCraft);

        var cap = CapFor(countAttempts);
        requested = Math.Max(requested, 1);

        var run = new MakeXRun
        {
            From = from,
            System = system,
            Item = item,
            TypeRes = typeRes,
            Tool = tool,
            Requested = requested,
            Target = Math.Min(requested, cap),
            Capped = requested > cap,
            CountAttempts = countAttempts,
            ExceptionalOnly = exceptionalOnly && CanBeExceptional(item),
            RejectsBag = rejectsBag,
            Start = from.Location,
            StartMap = from.Map
        };

        context.Run = run;
        from.SendGump(new MakeXProgressGump(run));
        system.CreateItem(from, item.ItemType, typeRes, tool, item);
        return run;
    }

    /// <summary>
    /// An attempt has ended: `made` is the item, or null for a failed attempt. Returns false when no run is going,
    /// and the caller does what it always did. Otherwise the run counts it, handles a reject, and either starts the
    /// next attempt or ends.
    /// </summary>
    public static bool AfterAttempt(Mobile from, CraftSystem system, BaseTool tool, Item made, bool exceptional)
    {
        var context = system.GetContext(from);
        var run = context?.Run;

        if (run == null)
        {
            return false;
        }

        run.Attempts++;

        if (made == null)
        {
            run.Failed++;
        }
        else
        {
            run.Made++;

            if (exceptional)
            {
                run.Exceptional++;
            }
            else if (run.ExceptionalOnly && !made.Deleted)
            {
                ClusterFCraftRejects.Handle(run, made);
            }
        }

        var stop = CheckStops(run, made);

        if (stop != null)
        {
            End(context, stop.Value);
            return true;
        }

        from.SendGump(new MakeXProgressGump(run));
        system.CreateItem(from, run.Item.ItemType, run.TypeRes, tool, run.Item);
        return true;
    }

    /// <summary>
    /// An attempt was refused (before it started, or at its end): not enough material, the skill, the anvil or forge
    /// out of reach. Ends a run with `reason`, showing `message` as the attempt would have; false when no run is going.
    /// </summary>
    public static bool Refused(Mobile from, CraftSystem system, TextDefinition message, MakeXStop reason)
    {
        var context = system.GetContext(from);

        if (context?.Run is not { } run)
        {
            return false;
        }

        // Refused because the crafter died (the tool went to the corpse) or walked away from the anvil: say that.
        if (from.Deleted || !from.Alive)
        {
            reason = MakeXStop.Died;
        }
        else if (from.Map != run.StartMap || from.Location != run.Start)
        {
            reason = MakeXStop.Moved;
        }

        End(context, reason, message);
        return true;
    }

    private static MakeXStop? CheckStops(MakeXRun run, Item made)
    {
        var from = run.From;

        if (run.Remaining <= 0)
        {
            return MakeXStop.Done;
        }

        if (run.StopRequested)
        {
            return MakeXStop.Stopped;
        }

        if (from.Deleted || !from.Alive)
        {
            return MakeXStop.Died;
        }

        if (from.Map != run.StartMap || from.Location != run.Start)
        {
            return MakeXStop.Moved;
        }

        if (run.Tool == null || run.Tool.Deleted || run.Tool.UsesRemaining < 1)
        {
            return MakeXStop.Tool;
        }

        var pack = from.Backpack;

        // AddToBackpack drops an item that does not fit at the crafter's feet.
        if (pack == null || made?.Deleted == false && !made.IsChildOf(pack) || pack.TotalItems >= pack.MaxItems)
        {
            return MakeXStop.PackFull;
        }

        if (StaminaSystem.IsOverloaded(from))
        {
            return MakeXStop.Overweight;
        }

        return null;
    }

    /// <summary>Ends the run in progress, if any: summary to the journal, the craft gump back.</summary>
    public static void End(CraftContext context, MakeXStop reason, TextDefinition message = null)
    {
        var run = context?.Run;

        if (run == null)
        {
            return;
        }

        context.Run = null;
        context.LastRun = run;
        run.StoppedBy = reason;

        var from = run.From;
        from.CloseGump<MakeXProgressGump>();

        ClusterFCraftRejects.Finish(run);

        run.Summary = Summary(run);

        if (message != null && !message.IsEmpty)
        {
            if (message.Number > 0)
            {
                from.SendLocalizedMessage(message.Number);
            }
            else
            {
                from.SendMessage(message.String);
            }
        }

        var line = $"{StopText(reason)} {run.Summary}";
        from.SendMessage(reason == MakeXStop.Done ? 0x44 : 0x22, line);

        if (reason != MakeXStop.OtherCraft && run.Tool?.Deleted == false && run.Tool.UsesRemaining > 0 && !from.Deleted)
        {
            from.SendGump(new CraftGump(from, run.System, run.Tool, message ?? line));
        }
    }

    public static string StopText(MakeXStop reason) => reason switch
    {
        MakeXStop.Done        => "Craft X done.",
        MakeXStop.Materials   => "Craft X stopped: out of materials.",
        MakeXStop.Tool        => "Craft X stopped: your tool wore out.",
        MakeXStop.PackFull    => "Craft X stopped: your backpack is full.",
        MakeXStop.Overweight  => "Craft X stopped: you are carrying too much.",
        MakeXStop.Moved       => "Craft X stopped: you moved.",
        MakeXStop.Died        => "Craft X stopped: you died.",
        MakeXStop.Stopped     => "Craft X stopped.",
        MakeXStop.CannotCraft => "Craft X stopped: you cannot make that here now.",
        MakeXStop.OtherCraft  => "Craft X stopped: you started another craft.",
        _                     => "Craft X stopped."
    };

    public static string Summary(MakeXRun run)
    {
        var made = run.CountAttempts
            ? $"Made {run.Made} in {run.Attempts} attempts ({run.Failed} failed, {run.Exceptional} exceptional)."
            : $"Made {run.Made} ({run.Failed} failed, {run.Exceptional} exceptional).";

        if (run.Capped)
        {
            made += $" Capped at {run.Target}.";
        }

        var rejects = ClusterFCraftRejects.Describe(run);
        return rejects.Length > 0 ? $"{made} {rejects}" : made;
    }
}

/// <summary>
/// Shown while a Craft X run is going. Stop, or closing it, ends the run when the attempt in hand finishes. Sent again
/// after every attempt to update it (a singleton, so the old one is replaced, not answered).
/// </summary>
public class MakeXProgressGump : DynamicGump
{
    private const int LabelHue = 0x480;

    private readonly MakeXRun _run;

    public override bool Singleton => true;

    public MakeXProgressGump(MakeXRun run) : base(40, 40) => _run = run;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage();
        builder.AddBackground(0, 0, 300, 120, 9200);
        builder.AddAlphaRegion(10, 10, 280, 100);

        builder.AddLabel(15, 12, LabelHue, "Craft X");

        if (_run.Item.NameNumber > 0)
        {
            builder.AddHtmlLocalized(75, 12, 215, 20, _run.Item.NameNumber, 0x7FFF);
        }
        else
        {
            builder.AddLabel(75, 12, LabelHue, _run.Item.NameString);
        }

        var what = _run.CountAttempts ? "attempts" : _run.ExceptionalOnly ? "exceptional" : "made";
        builder.AddLabel(15, 38, LabelHue, $"{_run.Counted} of {_run.Target} {what}");
        builder.AddLabel(15, 58, LabelHue, $"Made {_run.Made}, {_run.Failed} failed, {_run.Exceptional} exceptional");

        builder.AddButton(15, 84, 4017, 4019, 1);
        builder.AddLabel(50, 86, LabelHue, "Stop");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        // Stop, or the gump closed: either way the run ends when the attempt in hand finishes.
        if (_run.StoppedBy == null && !_run.StopRequested)
        {
            _run.StopRequested = true;
            sender.Mobile?.SendMessage(0x22, "Craft X will stop when this attempt finishes.");
        }
    }
}
