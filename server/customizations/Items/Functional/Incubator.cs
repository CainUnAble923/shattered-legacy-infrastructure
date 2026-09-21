// ServUO: Items/Functional/Incubator.cs, 158 lines (CC6 follow-up, Q-053, P12). Values and logic verbatim; serialization
// by the generator. The one input the chicken lizard egg (batch 4) had no driver for: a house container that starts an
// egg incubating when it is dropped in, holds at most six, refuses everything else, and checks its eggs' stages
// 60 s after a world load and 10 s after every world save. It must be SECURED in a house (not locked down) to accept
// an egg at all - CheckHold says so, as ServUO's does - and CheckEggs_Callback also does nothing unless it is secured.
//
// Two ServUO drivers that lived inside hand-written Serialize/Deserialize, carried over as the hooks pinned ModernUO
// has for the same moments:
//   - Deserialize: `if (Items.Count > 0) Timer.DelayCall(60 s, CheckEggs_Callback)` -> [AfterDeserialization].
//   - Serialize:   `if (Items.Count > 0) Timer.DelayCall(10 s, CheckEggs_Callback)` -> EventSink.WorldSave, which
//     World.Save invokes on the game thread before the snapshot (Server/World/World.cs:308), walking World.Items for
//     incubators with something in them. The cadence is therefore ServUO's: an egg crosses a stage threshold within
//     one save interval (autosave.saveDelay, 5 min on this shard) of reaching it, and an unsecured incubator's eggs
//     stop advancing until it is secured again.
//   The [IncreaseStage] Counselor command (+24 h on a targeted egg, then CheckStatus) is ServUO's and is kept.
//
// The egg needed one line back (ChickenLizardEgg.DropToItem's `Parent is not Incubator` clause), see its header.
// ServUO's Incubator is a core Container plus ISecurable, not a BaseContainer, so it has no house-access check on
// opening and no secure-level context entry of its own; the house's own SecureInfo and gump cover securing it. Same here.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Commands;
using Server.Gumps;
using Server.Multis;
using Server.Targeting;

namespace Server.Items;

[Flippable(0x407C, 0x407D)]
[SerializationGenerator(0, false)]
public partial class Incubator : Container, ISecurable
{
    public static readonly int MaxEggs = 6;

    private static readonly TimeSpan CheckAfterSave = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan CheckAfterLoad = TimeSpan.FromSeconds(60);

    private static bool _initialized;

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private SecureLevel _level;

    public override int LabelNumber => 1112479; // an incubator

    public override int DefaultGumpID => 1156;
    public override int DefaultDropSound => 66;

    [Constructible]
    public Incubator() : base(0x407C) => _level = SecureLevel.CoOwners;

    public override bool OnDragDropInto(Mobile from, Item item, Point3D p)
    {
        var canDrop = base.OnDragDropInto(from, item, p);

        if (canDrop && item is ChickenLizardEgg egg)
        {
            StartIncubating(egg);
        }

        return canDrop;
    }

    public override bool OnDragDrop(Mobile from, Item item)
    {
        var canDrop = base.OnDragDrop(from, item);

        if (canDrop && item is ChickenLizardEgg egg)
        {
            StartIncubating(egg);
        }

        return canDrop;
    }

    // ServUO repeats this block in both drop overrides; one copy here, same statements.
    private static void StartIncubating(ChickenLizardEgg egg)
    {
        if (egg.TotalIncubationTime > TimeSpan.FromHours(120))
        {
            egg.BurnEgg();
        }
        else
        {
            egg.IncubationStart = Core.Now;
            egg.Incubating = true;
        }
    }

    public override bool CheckHold(Mobile m, Item item, bool message, bool checkItems, int plusItems, int plusWeight)
    {
        if (!BaseHouse.CheckSecured(this))
        {
            m.SendLocalizedMessage(1113711); // The incubator must be secured for the egg to grow, not locked down.
            return false;
        }

        if (item is not ChickenLizardEgg)
        {
            m.SendMessage("This will only accept chicken eggs.");
            return false;
        }

        if (MaxEggs > -1 && Items.Count >= MaxEggs)
        {
            m.SendMessage($"You can only put {MaxEggs} chicken eggs in the incubator at a time."); // TODO: Get Message
            return false;
        }

        return true;
    }

    public void CheckEggs_Callback()
    {
        if (Deleted || !BaseHouse.CheckSecured(this))
        {
            return;
        }

        foreach (var item in Items)
        {
            if (item is ChickenLizardEgg egg)
            {
                egg.CheckStatus();
            }
        }
    }

    public static void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        CommandSystem.Register("IncreaseStage", AccessLevel.Counselor, IncreaseStage_OnCommand);
        EventSink.WorldSave += OnWorldSave;
    }

    // ServUO: Incubator.Serialize schedules CheckEggs_Callback 10 s out for every incubator with items in it.
    //
    // EventSink.WorldSave fires on the game thread while World.WorldState is Saving, and Timer.Start logs an error
    // with a stack trace for any timer started in that state (Server/Timer/Timer.cs:85; the first headless run of
    // this port produced one per save). So the incubators are chosen here and the timers are started from the
    // game loop's next iteration through Core.LoopContext (Main.cs:480), by which point Save() has returned and the
    // state is WritingSave.
    public static void OnWorldSave()
    {
        List<Incubator> pending = null;

        foreach (var incubator in World.Items.Values.OfType<Incubator>())
        {
            if (!incubator.Deleted && incubator.Items.Count > 0)
            {
                (pending ??= new List<Incubator>()).Add(incubator);
            }
        }

        if (pending == null)
        {
            return;
        }

        Core.LoopContext.Post(() =>
        {
            foreach (var incubator in pending)
            {
                if (!incubator.Deleted)
                {
                    Timer.DelayCall(CheckAfterSave, incubator.CheckEggs_Callback);
                }
            }
        });
    }

    // ServUO: Incubator.Deserialize schedules CheckEggs_Callback 60 s out when there are items in it.
    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (Items.Count > 0)
        {
            Timer.DelayCall(CheckAfterLoad, CheckEggs_Callback);
        }
    }

    public static void IncreaseStage_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target the egg.");
        e.Mobile.BeginTarget(12, false, TargetFlags.None, IncreaseStage_OnTarget);
    }

    public static void IncreaseStage_OnTarget(Mobile from, object targeted)
    {
        if (targeted is ChickenLizardEgg egg)
        {
            egg.TotalIncubationTime += TimeSpan.FromHours(24);
            egg.CheckStatus();
        }
    }
}
