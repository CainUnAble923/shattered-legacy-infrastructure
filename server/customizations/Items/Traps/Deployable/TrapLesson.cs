// cc-P275 (D6): the Rootdeep Burrows' trap lesson in The Stacks (the design's section 6.2). Notes: shard-migration
// notes/cc-P275-deployable-traps-build.md.
//
// The Stacks (x 5156-5168, y 1132-1142, inside the Vault) is a child region of the Vault, so a trainee's step into the
// aisles is the region's OnEnter. Every rule it does not override is the Vault's, and the Vault's are the Burrows'
// (RootdeepVault.cs's header): the walk-in refusal and the teleport refusal are asked of the parent outright here.
//
//   1. A tutorial participant who has not done the lesson and carries no practice net walks in: the Echo drops two
//      practice nets in the pack and names the Hoarding Rat.
//   2. Any rat (a Penny Rat or the Hoarding Rat) held by the participant's net, a practice net or a real one, completes
//      the lesson: the key LessonKey lands in TutorialRecord.StationsDone (saved strings, no schema change; not a
//      station row, so CoreDone is unchanged, as RootdeepVault.VaultClearedKey).

#nullable enable

using Server.Items;
using Server.Mobiles;
using Server.Spells;
using Server.Tutorial;

namespace Server.Regions;

public class RootdeepStacksRegion : BaseRegion
{
    public const string RegionName = "the Stacks";

    /// <summary>x 5156-5168, y 1132-1142 inclusive (Rectangle3D's end is exclusive).</summary>
    public static readonly Rectangle3D Area = new(new Point3D(5156, 1132, MinZ), new Point3D(5169, 1143, MaxZ));

    public static RootdeepStacksRegion? Instance { get; private set; }

    public RootdeepStacksRegion(Region parent) : base(RegionName, Map.Trammel, parent, DefaultPriority + 2, Area)
    {
    }

    public static void Initialize()
    {
        RootdeepVaultRegion.Initialize(); // idempotent; the parent first

        if (Instance != null || RootdeepVaultRegion.Instance == null)
        {
            return;
        }

        Instance = new RootdeepStacksRegion(RootdeepVaultRegion.Instance);
        Instance.Register();
    }

    public override bool OnMoveInto(Mobile m, Direction d, Point3D newLocation, Point3D oldLocation) =>
        Parent.OnMoveInto(m, d, newLocation, oldLocation);

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message) =>
        Parent is BaseRegion parent
            ? parent.CheckTravel(m, newLocation, travelType, out message)
            : base.CheckTravel(m, newLocation, travelType, out message);

    public override void OnEnter(Mobile m)
    {
        base.OnEnter(m);

        if (m is PlayerMobile pm)
        {
            TrapLesson.OnEnterStacks(pm);
        }
    }
}

public static class TrapLesson
{
    public const string LessonKey = "burrows.trap";
    public const int PracticeNets = 2;

    // The Echo's lines (the "System:" voice), the design's section 6.2.
    public const string NetsLine =
        "The Stacks. The rats run the aisles, and one of them runs with the money: the Hoarding Rat. Here. Two practice nets. " +
        "Put one down in an aisle, step back, and let the rat come to you.";

    public const string HeldLine =
        "Held. That is the whole trick: the floor does the waiting. Remember the floor. It does not only wait for rats.";

    public static bool HasDone(PlayerMobile pm) => ClusterFTutorial.RecordOf(pm)?.StationsDone.Contains(LessonKey) == true;

    /// <summary>Gives the practice nets once the trainee needs them. Returns how many it gave.</summary>
    public static int OnEnterStacks(PlayerMobile pm)
    {
        if (!ClusterFTutorial.IsParticipant(pm) || HasDone(pm) || pm.Backpack == null || CarriesPracticeNet(pm))
        {
            return 0;
        }

        for (var i = 0; i < PracticeNets; i++)
        {
            pm.AddToBackpack(SnareNetKit.MakePractice());
        }

        ClusterFTutorial.SystemLine(pm, NetsLine);
        return PracticeNets;
    }

    public static bool CarriesPracticeNet(PlayerMobile pm) =>
        pm.Backpack?.FindItemByType<SnareNetKit>(true, kit => kit.Practice) != null;

    /// <summary>A net held something: a rat held by a participant's net completes the lesson.</summary>
    public static void OnNetted(Mobile owner, Mobile held)
    {
        if (owner is not PlayerMobile pm || held is not (PennyRat or HoardingRat) || !ClusterFTutorial.IsParticipant(pm))
        {
            return;
        }

        if (ClusterFTutorial.RecordOf(pm)!.StationsDone.Add(LessonKey))
        {
            ClusterFTutorial.SystemLine(pm, HeldLine);
        }
    }
}
