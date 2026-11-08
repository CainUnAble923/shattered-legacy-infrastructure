// cc-P818 item 1 (Chase 11-07: "tutorial island league building needs either to rotate so player can see door or
// something for player to know where door is"). Notes: shard-migration notes/cc-P818-isle-notes-2.md; the guide is
// notes/guides/building-and-decorating.md. The lodge's door moved from its north wall, which the client's camera (from
// the south-east) never shows, to its east wall, which faces the road's east stretch and the camera; a walk of sandstone
// bricks runs to it from the road between two braziers (map statics, the isle driver), the lodge's two signs stand north
// of the road at the walk's mouth, the lesson says the door is on the east side and points the arrow at it, and Elara
// calls where it is to a trainee outside.
//
// Facts, each watched failing first on main's code (red.log):
//   1. The door is in the east wall, hung NorthCW, so open it folds north along the wall line (guide 2.4).
//   2. An existing world's north door is taken away and the east door hung, once.
//   3. The lodge's name sign and the League lesson sign stand north of the road where the walk leaves it; an existing
//      world's name sign is moved, not doubled.
//   4. Elara faces the east door; an existing world's Elara is turned.
//   5. The lesson names the east side, and its arrow points at the door until the trainee is inside, then at Elara.
//   6. Elara says where the door is to a trainee outside the lodge and near it, and not to one inside.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Items;
using Server.LootBoxes;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialLodgeDoorVerification : IDisposable
{
    private readonly Func<Point3D, Map, bool> _fits = TutorialCorePlacement.FitsOn;
    private readonly Func<bool> _testShard = TutorialCorePlacement.IsTestShard;
    private readonly Func<bool> _farmTestShard = TutorialFarm.IsTestShard;
    private readonly Func<bool> _patched = TutorialFarm.MapPatched;
    private readonly HashSet<Item> _items = new(World.Items.Values);
    private readonly HashSet<Mobile> _mobiles = new(World.Mobiles.Values);

    public TutorialLodgeDoorVerification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }

        ClusterFTutorial.Enabled = true;
        ClusterFTutorial.SkipCountsAsGraduation = true;
        FoldMaps.Configure();
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
        TutorialCorePlacement.IsTestShard = () => true;
        TutorialCorePlacement.FitsOn = (_, _) => true;
        TutorialFarm.IsTestShard = () => true;
        TutorialFarm.MapPatched = () => true;
    }

    public void Dispose()
    {
        TutorialCorePlacement.FitsOn = _fits;
        TutorialCorePlacement.IsTestShard = _testShard;
        TutorialFarm.IsTestShard = _farmTestShard;
        TutorialFarm.MapPatched = _patched;
        foreach (var item in World.Items.Values.ToArray())
        {
            if (!_items.Contains(item))
            {
                item.Delete();
            }
        }

        foreach (var mobile in World.Mobiles.Values.ToArray())
        {
            if (!_mobiles.Contains(mobile))
            {
                mobile.Delete();
            }
        }
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p818{Guid.NewGuid():N}"[..16], "p818-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Walker", RawStr = 60, RawDex = 60, RawInt = 60 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(HavenSpots.Start, HavenSpots.Map);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Dispose()
        {
            TutorialCoreTrials.Cancel(Pm);
            ClusterFFanfare.FlushAll();
            Pm.NetState = null;
            Ns.Mobile = null;
            Ns.Dispose();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static TutorialAction[] Evidence(string key) => key switch
    {
        "core.movement" => [TutorialAction.FirstMark, TutorialAction.SecondMark],
        "core.paperdoll" => [TutorialAction.Equipped],
        "core.backpack" => [TutorialAction.OpenedPaperdoll, TutorialAction.OpenedBag],
        "core.chat" => [TutorialAction.SaidPhrase],
        "core.farm" => [TutorialAction.FarmDone],
        "core.healing" => [TutorialAction.Healed],
        "core.death" => [TutorialAction.EchoDeath, TutorialAction.HealerOffer, TutorialAction.Resurrected],
        "core.banking" => [TutorialAction.Deposited],
        "core.travel" => [TutorialAction.UsedGate],
        "core.league" => [TutorialAction.TalkedToLeague],
        "core.loop" => [TutorialAction.VisionStarted, TutorialAction.VisionFinished],
        _ => throw new InvalidOperationException(key)
    };

    private static void PlayCoreUntil(PlayerMobile pm, string stop)
    {
        foreach (var station in TutorialStations.Core.ToList())
        {
            if (station.Key == stop)
            {
                return;
            }

            ClusterFTutorial.RunStation(pm, station);
            foreach (var action in Evidence(station.Key))
            {
                TutorialCoreTrials.Observe(pm, action);
            }
        }
    }

    private static string Seed() => TutorialCorePlacement.SeedOn(TutorialWorld.Isle, false, withHaven: false);

    private static List<T> ItemsAt<T>(Point3D p, int range = 0) where T : Item
    {
        var found = new List<T>();
        foreach (var item in TutorialWorld.Isle.GetItemsInRange<T>(p, range))
        {
            if (!item.Deleted)
            {
                found.Add(item);
            }
        }

        return found;
    }

    private static LeagueRegistrar Elara()
    {
        foreach (var m in TutorialWorld.Isle.GetMobilesInRange(TutorialCorePlacement.ElaraSpot, 2))
        {
            if (m is LeagueRegistrar { Deleted: false } r)
            {
                return r;
            }
        }

        throw new InvalidOperationException("no Elara on the isle");
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void P818_TheLodgeDoorIsInTheEastWallAndFoldsAlongIt()
    {
        // the east wall: x 6787 (the lodge's floor is x 6778-6786, y 3996-4002), not the north wall (y 3995)
        Assert.Equal(new Point2D(6787, 4000), TutorialWorld.LodgeDoor);
        Assert.True(TutorialWorld.LodgeStop.X > TutorialWorld.LodgeDoor.X); // the stop is outside, east of the door
        Assert.Equal(TutorialWorld.LodgeDoor.Y, TutorialWorld.LodgeStop.Y);

        var door = TutorialCorePlacement.MakeLodgeDoor();
        try
        {
            Assert.Equal(0x06A5 + 2 * (int)DoorFacing.NorthCW, door.ClosedId); // DarkWoodDoor: closed art 0x06A5 + 2 x facing
            Assert.Equal(TutorialCorePlacement.LodgeDoorClosedId, door.ClosedId);
            Assert.Equal(new Point3D(0, -1, 0), door.Offset); // open, it folds north along the east wall's line
        }
        finally
        {
            door.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void P818_AnOldNorthDoorIsTakenAwayAndTheEastDoorHungOnce()
    {
        Seed();
        var isle = TutorialWorld.Isle;
        var oldSpot = TutorialWorld.Ground(isle, TutorialCorePlacement.OldLodgeDoor);
        var old = new DarkWoodDoor(DoorFacing.WestCCW); // the world as P720-P817 left it
        old.MoveToWorld(oldSpot, isle);
        var eastSpot = TutorialWorld.Ground(isle, TutorialWorld.LodgeDoor);
        foreach (var d in ItemsAt<BaseDoor>(eastSpot))
        {
            d.Delete(); // an existing world has no east door yet
        }

        Seed();

        Assert.True(old.Deleted);
        Assert.Empty(ItemsAt<BaseDoor>(oldSpot));
        var hung = Assert.Single(ItemsAt<BaseDoor>(eastSpot));
        Assert.Equal(TutorialCorePlacement.LodgeDoorClosedId, hung.ClosedId);
        hung.Open = true;
        Assert.Equal(eastSpot.X, hung.X); // open, it stays in the wall's line
        Assert.Equal(eastSpot.Y - 1, hung.Y);
        hung.Open = false;

        Seed();
        Assert.Same(hung, Assert.Single(ItemsAt<BaseDoor>(eastSpot)));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void P818_TheLodgeSignsStandNorthOfTheRoadAtTheWalkAndAnOldSignMoves()
    {
        var sign = TutorialCorePlacement.LodgeSign;
        var league = TutorialCorePlacement.SignPoint("core.league");
        // where the walk (x 6789, y 3990-4000) leaves the road: the road runs at y 3985-3989 there, so north of it
        Assert.Equal(new Point2D(6789, 3984), league);
        Assert.Equal(1, Math.Max(Math.Abs(league.X - sign.X), Math.Abs(league.Y - sign.Y)));
        Assert.InRange(sign.X, 6787, 6790);
        Assert.True(sign.Y < 3985);

        bool IsSign(Item i) => i.ItemID == TutorialCorePlacement.SignpostId && i.Name == TutorialCorePlacement.LodgeSignName;
        Seed();
        var isle = TutorialWorld.Isle;
        var placed = Assert.Single(ItemsAt<Static>(TutorialWorld.Ground(isle, sign), 12), IsSign);
        Assert.Equal(TutorialWorld.Ground(isle, sign), placed.Location);

        // an existing world's sign by the road's dip, at the old spot, is moved
        placed.MoveToWorld(TutorialWorld.Ground(isle, TutorialCorePlacement.OldLodgeSign), isle);
        Seed();
        var moved = Assert.Single(ItemsAt<Static>(TutorialWorld.Ground(isle, sign), 12), IsSign);
        Assert.Same(placed, moved);
        Assert.Equal(TutorialWorld.Ground(isle, sign), moved.Location);

        // the League lesson's marker stands at its new sign spot too
        var marker = TutorialCorePlacement.FindMarker("core.league");
        Assert.NotNull(marker);
        Assert.Equal(TutorialWorld.Ground(isle, league), marker!.Location);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void P818_ElaraFacesTheEastDoorAndAnOldWorldsElaraIsTurned()
    {
        Seed();
        var elara = Elara();
        Assert.Equal(Direction.East, elara.Direction & Direction.Mask);

        elara.Direction = Direction.North; // she faced the old north door
        Seed();
        Assert.Equal(Direction.East, Elara().Direction & Direction.Mask);
        Assert.Same(elara, Elara());
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void P818_TheLeagueLessonNamesTheEastDoorAndItsArrowPointsAtItUntilInside()
    {
        var hint = TutorialCoreTrials.Hint("core.league");
        Assert.Contains("east side", hint);
        Assert.DoesNotContain("north side", hint);
        Assert.Contains("between the two fires", hint);

        Seed();
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        PlayCoreUntil(p.Pm, "core.league");
        var isle = TutorialWorld.MapOf(p.Pm)!;
        p.Pm.MoveToWorld(TutorialWorld.Ground(isle, new Point2D(6789, 3992)), isle); // on the walk, outside
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.league")!);
        var trial = TutorialCoreTrials.Current(p.Pm)!;
        Assert.Equal(TutorialWorld.Ground(isle, TutorialWorld.LodgeDoor), trial.ArrowAt);
        Assert.NotNull(p.Pm.QuestArrow);

        p.Pm.MoveToWorld(new Point3D(6785, 4000, 0), isle); // through the door
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.6));
        Assert.Equal(TutorialCorePlacement.ElaraSpot, trial.ArrowAt);

        p.Pm.MoveToWorld(TutorialWorld.Ground(isle, TutorialWorld.LodgeStop), isle); // and out again
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.6));
        Assert.Equal(TutorialWorld.Ground(isle, TutorialWorld.LodgeDoor), trial.ArrowAt);
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void P818_ElaraSaysWhereTheDoorIsToATraineeOutside()
    {
        Assert.Contains("east side", TutorialCoreTrials.ElaraDoorLine);

        Seed();
        using var p = new Player();
        var isle = TutorialWorld.Isle;
        p.Pm.MoveToWorld(TutorialWorld.Ground(isle, new Point2D(6782, 3993)), isle); // by the north wall, where the door was
        Assert.True(TutorialCoreTrials.CallFromLodge(p.Pm));

        p.Pm.MoveToWorld(new Point3D(6784, 3999, 0), isle); // inside: she does not call
        Assert.False(TutorialCoreTrials.CallFromLodge(p.Pm));

        p.Pm.MoveToWorld(TutorialWorld.Ground(isle, TutorialWorld.Stones), isle); // far off on the knoll
        Assert.False(TutorialCoreTrials.CallFromLodge(p.Pm));
    }
}
