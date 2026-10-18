// cc-P141 (F-45, Part D of the Rootdeep Burrows prompts): the tutorial Vault, a room of the Burrows beside the landing.
// Notes: shard-migration notes/cc-P141-rootdeep-burrows.md (the layout spec, the rules, what breaks with two players).
//
// What it is: a child region of OldHavenBurrowsRegion over x 5142-5178, y 1132-1159 (Chase's enlarged draft,
// 2026-10-15). A child BaseRegion hands every rule it does not override to its parent (Region.cs:793-874 at d4531cd9:
// AllowHarmful, OnDeath, light, housing; BaseRegion.CheckTravel, BaseRegion.cs:171-180), so the Burrows' Young limit,
// dungeon light and no-recall rules hold inside it unchanged, and GetRegion<DungeonRegion> still finds the Burrows
// through Parent (Region.cs:458-470), which the Young death teleport needs (PlayerMobile.cs:4078-4081).
//
// The rules (Chase's draft, 2026-10-15):
//   1. Tutorial participants only. Walking in is asked of the region being entered (Region.CanMove -> OnMoveInto,
//      Region.cs:887-900, from Mobile.CanMove, Mobile.cs:4164), so the doorway from the landing (x 5179) refuses anyone
//      else with an Echo line (the "System:" voice is the Echo, decisions-2026-10-14 Tutorial item 3). A pet counts as
//      its master (ClusterFTutorial.IsParticipant). Teleport in from outside is refused too (CheckTravel); recall and
//      gate are already refused by the Burrows.
//   2. When the Overdraft dies, every participant who damaged it (or whose pet did) is credited, and the crawlway in the
//      Strongroom's south wall opens (RootdeepCrawlway). It is one way: its only mouth is inside the Vault and it puts
//      the player in the Warren.
//   3. The Vault seals behind a credited player and refuses a graduate: the doorway refuses them like anyone else. The
//      credit is the key VaultClearedKey in the account's TutorialRecord.StationsDone (saved strings, so no schema
//      change; CoreDone only counts core station keys, ClusterFTutorial.cs:96-104, so it changes nothing else).
//   4. A player who logs in inside the Vault and may not be there is put back on the landing.
// Staff are exempt from every refusal.

#nullable enable

using System;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;
using Server.Spells;
using Server.Tutorial;

namespace Server.Regions;

public class RootdeepVaultRegion : BaseRegion
{
    public const string RegionName = "the Vault";

    /// <summary>x 5142-5178, y 1132-1159 inclusive (Rectangle3D's end is exclusive).</summary>
    public static readonly Rectangle3D Area = new(new Point3D(5142, 1132, MinZ), new Point3D(5179, 1160, MaxZ));

    public static RootdeepVaultRegion? Instance { get; private set; }

    public RootdeepVaultRegion(Region parent) : base(RegionName, Map.Trammel, parent, DefaultPriority + 1, Area)
    {
    }

    public static void Initialize()
    {
        OldHavenBurrowsRegion.Initialize(); // idempotent; the parent must exist first, whatever order Initialize runs in

        if (Instance != null || OldHavenBurrowsRegion.Instance == null)
        {
            return;
        }

        Instance = new RootdeepVaultRegion(OldHavenBurrowsRegion.Instance);
        Instance.Register();
    }

    public override bool OnMoveInto(Mobile m, Direction d, Point3D newLocation, Point3D oldLocation)
    {
        if (!RootdeepVault.MayEnter(m, out var refusal))
        {
            if (refusal != null)
            {
                ClusterFTutorial.SystemLine(m, refusal);
            }

            return false;
        }

        return base.OnMoveInto(m, d, newLocation, oldLocation);
    }

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        if (travelType == TravelCheckType.TeleportTo && m.AccessLevel == AccessLevel.Player && Area.Contains(newLocation)
            && !Area.Contains(m.Location))
        {
            message = RootdeepVault.TeleportInMessage;
            return false;
        }

        return base.CheckTravel(m, newLocation, travelType, out message);
    }
}

public static class RootdeepVault
{
    /// <summary>The account has killed the Overdraft (TutorialRecord.StationsDone; not a station).</summary>
    public const string VaultClearedKey = "burrows.vault";

    /// <summary>The doorway in the wall between the landing and the Vault (x 5179), and where it lets out.</summary>
    public static readonly Point3D Doorway = new(5179, 1137, 0);

    /// <summary>Where the landing puts a player the Vault turns out (beside the doorway, on the landing).</summary>
    public static readonly Point3D LandingSpot = new(5182, 1137, 0);

    /// <summary>The crawlway's mouth (in the Strongroom, against its south wall) and its far end in the Warren.</summary>
    public static readonly Point3D CrawlwayMouth = new(5160, 1159, 0);
    public static readonly Point3D CrawlwayExit = new(5160, 1163, 0);

    /// <summary>How long the crawlway stays open after the Overdraft dies (its spawner waits longer, the seeder's data).</summary>
    public static readonly TimeSpan CrawlwayOpenFor = TimeSpan.FromMinutes(2);

    // The Echo's lines (the "System:" voice). Ours, for Chase's eye.
    public const string NotParticipantLine = "The Vault is for the League's trainees. You are not one. The door has excellent judgement.";
    public const string GraduateLine = "You graduated. The Vault does not do reunions.";
    public const string SealedLine = "The Vault sealed behind you. Nothing in there owes you anything now.";
    public const string CrawlwayOpensLine = "Something gave way in the south wall. A crawlway. One way, I am told. Do not get sentimental about the Vault.";
    public const string CrawlwayClosedLine = "Packed earth and rubble. Whatever holds this shut is still alive.";
    public const string CrawlwayNotYoursLine = "That crawlway opened for whoever fought the Overdraft. That was not you.";
    public const string CrawlLine = "You squeeze through the crawlway. Behind you the earth settles. The Vault is sealed.";
    public const string TurnedOutLine = "You wake on the landing. The Vault is not for you any more.";
    public const string TeleportInMessage = "The Vault's walls are thicker than your spell.";

    public static bool IsInside(Map? map, Point3D p) => map == Map.Trammel && RootdeepVaultRegion.Area.Contains(p);

    /// <summary>Whether <paramref name="m"/> may walk into the Vault; when not, the Echo line to say (null: say nothing).</summary>
    public static bool MayEnter(Mobile m, out string? refusal)
    {
        refusal = null;

        if (m.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        var player = m is BaseCreature { Controlled: true } pet ? pet.ControlMaster : m;

        if (player is not PlayerMobile pm)
        {
            return m is not BaseCreature { Controlled: true }; // a wild creature: the region's ordinary walk rule decides
        }

        if (pm.AccessLevel > AccessLevel.Player)
        {
            return true; // a staff member's pet
        }

        var record = ClusterFTutorial.RecordOf(pm);

        if (!ClusterFTutorial.IsParticipant(pm))
        {
            refusal = m == pm ? record?.State == TutorialState.Graduated ? GraduateLine : NotParticipantLine : null;
            return false;
        }

        if (record!.StationsDone.Contains(VaultClearedKey))
        {
            refusal = m == pm ? SealedLine : null;
            return false;
        }

        return true;
    }

    /// <summary>The player a mobile stands for: a pet's master, or itself.</summary>
    private static PlayerMobile? PlayerOf(Mobile? m) =>
        (m is BaseCreature { Controlled: true } pet ? pet.ControlMaster : m) as PlayerMobile;

    public static bool HasCleared(Mobile m) => PlayerOf(m) is { } pm && ClusterFTutorial.RecordOf(pm)?.StationsDone.Contains(VaultClearedKey) == true;

    /// <summary>
    /// The Overdraft died: credit every participant who damaged it (a pet credits its master), tell them, and open the
    /// crawlway. Called from TheOverdraft.OnDeath. Returns how many players were credited.
    /// </summary>
    public static int OnOverdraftDeath(Mobile overdraft)
    {
        var credited = 0;

        foreach (var entry in overdraft.DamageEntries.ToArray()) // a ValueLinkList has only a ref-struct enumerator
        {
            if (PlayerOf(entry.Damager) is not { } pm || !ClusterFTutorial.IsParticipant(pm))
            {
                continue;
            }

            if (ClusterFTutorial.RecordOf(pm)!.StationsDone.Add(VaultClearedKey))
            {
                credited++;
                ClusterFTutorial.SystemLine(pm, CrawlwayOpensLine);
            }
        }

        if (overdraft.Map is { } map && map != Map.Internal)
        {
            foreach (var crawlway in map.GetItemsInRange<RootdeepCrawlway>(overdraft.Location, 24))
            {
                if (IsInside(map, crawlway.Location))
                {
                    crawlway.Open(CrawlwayOpenFor);
                }
            }
        }

        return credited;
    }

    /// <summary>A mobile at the crawlway's mouth: through it (with its pets) if it is open and the player cleared the Vault.</summary>
    public static bool TryCrawl(Mobile m, RootdeepCrawlway crawlway)
    {
        if (m is not PlayerMobile pm || !pm.Alive)
        {
            return false; // pets go with their master; creatures and ghosts stay
        }

        if (!crawlway.IsOpen)
        {
            ClusterFTutorial.SystemLine(pm, CrawlwayClosedLine);
            return false;
        }

        if (!HasCleared(pm) && pm.AccessLevel == AccessLevel.Player)
        {
            ClusterFTutorial.SystemLine(pm, CrawlwayNotYoursLine);
            return false;
        }

        var to = crawlway.Exit;
        BaseCreature.TeleportPets(pm, to, crawlway.Map);
        pm.MoveToWorld(to, crawlway.Map);
        Effects.PlaySound(to, crawlway.Map, 0x221); // rocks settling
        ClusterFTutorial.SystemLine(pm, CrawlLine);
        return true;
    }

    /// <summary>Puts a player who may not be in the Vault back on the landing. True when it moved them.</summary>
    public static bool TurnOut(PlayerMobile pm)
    {
        if (!IsInside(pm.Map, pm.Location) || MayEnter(pm, out _))
        {
            return false;
        }

        BaseCreature.TeleportPets(pm, LandingSpot, Map.Trammel);
        pm.MoveToWorld(LandingSpot, Map.Trammel);
        ClusterFTutorial.SystemLine(pm, TurnedOutLine);
        return true;
    }

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm) => TurnOut(pm);
}

/// <summary>
/// The cave-in: rocks in the Strongroom's south wall that become a crawlway into the Warren when the Overdraft dies. Its
/// open state is a runtime field, never saved (a restart closes it); the credit that lets a player through is saved
/// (RootdeepVault).
/// </summary>
[SerializationGenerator(0, false)]
public partial class RootdeepCrawlway : Item
{
    public const int ClosedId = 0x1367; // rocks (tiledata: Impassable), so nothing steps on it while it is shut
    public const int OpenId = 0x1B71;   // hole

    private TimerExecutionToken _closeTimer;
    private bool _open;

    [Constructible]
    public RootdeepCrawlway() : base(ClosedId) => Movable = false;

    public override string DefaultName => IsOpen ? "a crawlway" : "a cave-in";

    public bool IsOpen => _open;

    /// <summary>Where it lets out (the Warren side of the wall).</summary>
    public Point3D Exit => new(X, Y + (RootdeepVault.CrawlwayExit.Y - RootdeepVault.CrawlwayMouth.Y), Z);

    public void Open(TimeSpan openFor)
    {
        _closeTimer.Cancel();
        if (!_open)
        {
            _open = true;
            ItemID = OpenId;
            Effects.PlaySound(Location, Map, 0x221);
            Effects.SendLocationEffect(Location, Map, 0x36B0, 16, 1); // a puff of dust
        }

        Timer.StartTimer(openFor, Close, out _closeTimer);
    }

    public void Close()
    {
        _closeTimer.Cancel();
        _open = false;
        ItemID = ClosedId;
    }

    // Through it when allowed (the move onto the mouth is then cancelled, as pinned's Teleporter does, Teleporter.cs:258-267);
    // otherwise a player may stand on the open hole, and nothing else may.
    public override bool OnMoveOver(Mobile m) => !RootdeepVault.TryCrawl(m, this) && m is PlayerMobile;

    public override void OnDoubleClick(Mobile from)
    {
        if (from.InRange(GetWorldLocation(), 1))
        {
            RootdeepVault.TryCrawl(from, this);
        }
        else
        {
            from.SendLocalizedMessage(500446); // That is too far away.
        }
    }

    // A save taken while it was open stored the hole's graphic; it loads shut.
    [AfterDeserialization]
    private void AfterDeserialization() => ItemID = ClosedId;
}
