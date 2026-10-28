// cc-P720: fixes from Chase's walk of the Wakening Isle (round 2). Notes: shard-migration
// notes/cc-P720-isle-walkthrough-fixes-2.md.
//
// Facts, each seen failing first:
//   1. The farmhouse is OSI's cottage laid at one height (foundation at 0, walls, windows and floor at 7, the roof above),
//      with nothing stray (no loose chimney or coop); it is furnished and its sign is named; a farm laid with an older
//      layout is taken up with its door and laid again.
//   2. The field fence is one closed ring of the one fence set, the pen inside it closed, the only way in the server's
//      field gate at Wren's spot.
//   3. Every core lesson is marked by a standing signpost beside its stop, named "Lesson N: <title>"; no rune is left; a
//      rune marker from before is turned into the sign and moved; the sign starts its lesson.
//   4. The bank's and the lodge's doors fold back into the wall line when open (WestCCW), and an older door is rehung;
//      the bank lesson's arrow points at the door until the trainee is inside, then at Merrin.
//   5. The League's notice board stands in the lodge and opens the League's window in range.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Gumps;
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
public class TutorialIsleWalkFixes2Verification : IDisposable
{
    private readonly Func<Point3D, Map, bool> _fits = TutorialCorePlacement.FitsOn;
    private readonly Func<bool> _testShard = TutorialCorePlacement.IsTestShard;
    private readonly Func<bool> _farmTestShard = TutorialFarm.IsTestShard;
    private readonly Func<bool> _patched = TutorialFarm.MapPatched;
    private readonly HashSet<Item> _items = new(World.Items.Values);
    private readonly HashSet<Mobile> _mobiles = new(World.Mobiles.Values);

    public TutorialIsleWalkFixes2Verification()
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
        public readonly Account Account = new($"p720{Guid.NewGuid():N}"[..16], "p720-test-only");
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

        public TutorialRecord Record => ClusterFTutorial.RecordOf(Pm)!;

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

    private static List<T> ItemsAt<T>(Point3D p) where T : Item
    {
        var found = new List<T>();
        foreach (var item in TutorialWorld.Isle.GetItemsInRange<T>(p, 0))
        {
            if (!item.Deleted)
            {
                found.Add(item);
            }
        }

        return found;
    }

    private static HashSet<(int, int)> Ring(int x1, int y1, int x2, int y2)
    {
        var ring = new HashSet<(int, int)>();
        for (var x = x1; x <= x2; x++)
        {
            for (var y = y1; y <= y2; y++)
            {
                if (x == x1 || x == x2 || y == y1 || y == y2)
                {
                    ring.Add((x, y));
                }
            }
        }

        return ring;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheFarmhouseIsTheCottageAtOneHeightFurnishedAndAnOldFarmIsLaidAgain()
    {
        // The cottage: walls x 6687-6693, y 3972-3978, its door in the south wall.
        var house = TutorialIsleFarmLayout.Pieces.Where(p => p.X is >= 6687 and <= 6693 && p.Y is >= 3972 and <= 3978).ToArray();
        int[] foundation = [0x0063, 0x0064, 0x0065];
        int[] walls = [0x00A6, 0x00A7, 0x00A8, 0x00A9, 0x00AB, 0x00AD, 0x00B9];
        int[] thatch = [0x05A4, 0x05A5, 0x05A6];
        Assert.Contains(house, p => foundation.Contains(p.Id));
        Assert.All(house.Where(p => foundation.Contains(p.Id)), p => Assert.Equal(0, p.Z));
        Assert.All(house.Where(p => walls.Contains(p.Id)), p => Assert.Equal(7, p.Z));
        Assert.All(house.Where(p => p.Id is >= 0x04A9 and <= 0x04B4), p => Assert.Equal(7, p.Z));
        Assert.All(TutorialIsleFarmLayout.Pieces.Where(p => thatch.Contains(p.Id)), p => Assert.True(p.Z >= 27, $"thatch at {p.Z}"));
        Assert.Equal(new Point3D(6690, 3978, 7), TutorialIsleFarmLayout.DoorSpot);
        // nothing stray: no loose chimney, no chicken coop on the roof
        Assert.DoesNotContain(TutorialIsleFarmLayout.Pieces, p => p.Id is 0x08D5 or 0x4514);
        // furnished: a hearth, a bed, a table; the house sign is named
        Assert.Contains(house, p => p.Id == 0x0959 && p.Z == 7);
        Assert.Contains(house, p => p.Id == 0x0A63 && p.Z == 7);
        Assert.Contains(house, p => p.Id == 0x0B90 && p.Z == 7);

        // A farm laid before (one piece off, as P663's hillside heights were) is taken up with its door and laid again.
        var isle = TutorialWorld.Isle;
        TutorialFarm.FindAddon(isle)?.Delete();
        TutorialFarm.FindDoor(isle)?.Delete();
        var old = new TutorialIsleFarmAddon();
        var piece = old.Components.First(c => c.ItemID == 0x0063);
        piece.Offset = new Point3D(piece.Offset.X, piece.Offset.Y, piece.Offset.Z + 9);
        old.MoveToWorld(TutorialIsleFarmLayout.Anchor, isle);
        var oldDoor = new DarkWoodDoor(DoorFacing.SouthCW);
        oldDoor.MoveToWorld(TutorialIsleFarmLayout.OldDoorSpot, isle);
        Assert.False(TutorialFarm.IsCurrent(old));

        var r = TutorialFarm.Seed(isle, false);
        Assert.Equal(1, r.Addons);
        Assert.True(old.Deleted);
        Assert.True(oldDoor.Deleted);
        var laid = TutorialFarm.FindAddon(isle)!;
        Assert.True(TutorialFarm.IsCurrent(laid));
        Assert.Contains(laid.Components, c => c.ItemID == TutorialFarm.HouseSignId && c.Name == TutorialFarm.HouseSignName);
        var door = TutorialFarm.FindDoor(isle)!;
        Assert.Equal(0x06A5, door.ClosedId); // the cottage's door, WestCW in its south wall
        var again = TutorialFarm.Seed(isle, false);
        Assert.Equal(0, again.Addons);
        Assert.Same(laid, TutorialFarm.FindAddon(isle));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheFieldFenceIsOneClosedRingOfOneSetWithTheGateAtWrens()
    {
        int[] fence = [0x0835, 0x0836, 0x0837];
        var tiles = TutorialIsleFarmLayout.Pieces.Where(p => fence.Contains(p.Id)).Select(p => (p.X, p.Y)).ToHashSet();
        Assert.All(TutorialIsleFarmLayout.Pieces.Where(p => fence.Contains(p.Id)), p => Assert.Equal(0, p.Z));
        var field = Ring(6695, 3967, 6713, 3983);
        var pen = Ring(6706, 3967, 6713, 3973);
        var gate = (TutorialFarm.FieldGateSpots[0].X, TutorialFarm.FieldGateSpots[0].Y);
        var fencePiece = (TutorialFarm.FieldFenceSpot.X, TutorialFarm.FieldFenceSpot.Y);
        Assert.Contains(gate, field);
        Assert.Contains(fencePiece, field);
        // every tile of the field ring and the pen ring has a fence piece, but the two the farm seeder fills itself
        var expected = field.Union(pen).Where(c => c != gate && c != fencePiece).ToHashSet();
        Assert.Empty(expected.Except(tiles));
        Assert.Empty(tiles.Except(expected)); // and no fence piece anywhere else (no stubs)
        // Wren stands just outside the gate
        Assert.Equal(1, Math.Max(Math.Abs(TutorialFarm.FarmhandSpot.X - gate.X), Math.Abs(TutorialFarm.FarmhandSpot.Y - gate.Y)));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void EveryLessonIsMarkedByANamedSignpostBesideItsStop()
    {
        var isle = TutorialWorld.Isle;
        // a rune marker from before, on its stop
        var stop = TutorialCorePlacement.MarkerSpot("core.farm");
        var rune = new TutorialStationMarker("core.farm") { ItemID = TutorialCorePlacement.OldMarkerId, Name = "old" };
        rune.MoveToWorld(TutorialWorld.Ground(new Point2D(stop.X, stop.Y)), isle);

        Seed();
        var core = TutorialStations.Core.ToList();
        for (var i = 0; i < core.Count; i++)
        {
            var key = core[i].Key;
            var sign = TutorialCorePlacement.FindMarker(key, isle);
            Assert.NotNull(sign);
            Assert.Equal(TutorialCorePlacement.SignpostId, sign!.ItemID);
            Assert.Equal($"Lesson {i + 1}: {core[i].Title}", sign.Name);
            Assert.Equal(TutorialCorePlacement.SignSpot(key), sign.Location);
            var at = TutorialCorePlacement.MarkerSpot(key);
            Assert.NotEqual((at.X, at.Y), (sign.X, sign.Y)); // beside the stop, never on it
            Assert.True(Math.Max(Math.Abs(at.X - sign.X), Math.Abs(at.Y - sign.Y)) <= 2);
        }

        // the rune became the farm's sign: moved, renamed, one per lesson; no rune is left anywhere on the isle
        Assert.False(rune.Deleted);
        Assert.Same(rune, TutorialCorePlacement.FindMarker("core.farm", isle));
        var markers = new List<TutorialStationMarker>();
        foreach (var m in isle.GetItemsInRange<TutorialStationMarker>(new Point3D(6730, 3990, 0), 120))
        {
            markers.Add(m);
        }

        Assert.Equal(core.Count, markers.Count);
        Assert.DoesNotContain(markers, m => m.ItemID == TutorialCorePlacement.OldMarkerId);

        // the sign starts its lesson for a trainee beside it
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        PlayCoreUntil(p.Pm, "core.chat");
        TutorialCoreTrials.Cancel(p.Pm);
        var lessons = TutorialWorld.MapOf(p.Pm)!; // the isle, or the trainee's copy of it
        if (lessons != isle)
        {
            TutorialCorePlacement.SeedOn(lessons, false, withHaven: false);
        }

        var chat = TutorialCorePlacement.FindMarker("core.chat", lessons)!;
        p.Pm.MoveToWorld(new Point3D(chat.X + 1, chat.Y, chat.Z), lessons);
        chat.OnDoubleClick(p.Pm);
        Assert.True(TutorialCoreTrials.IsDoing(p.Pm, "core.chat"));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheBankAndLodgeDoorsFoldIntoTheWallAndAnOldDoorIsRehung()
    {
        var isle = TutorialWorld.Isle;
        var bankDoor = TutorialWorld.Ground(TutorialWorld.BankDoor);
        var old = new DarkWoodDoor(DoorFacing.WestCW);
        old.MoveToWorld(bankDoor, isle);

        Seed();
        Assert.True(old.Deleted);
        foreach (var spot in new[] { bankDoor, TutorialWorld.Ground(TutorialWorld.LodgeDoor) })
        {
            var door = Assert.Single(ItemsAt<BaseDoor>(spot));
            Assert.Equal(TutorialCorePlacement.DoorClosedId, door.ClosedId);
            door.Open = true;
            Assert.Equal(spot.Y, door.Y); // open, it stays in the wall's line
            Assert.Equal(spot.X - 1, door.X);
            door.Open = false;
        }

        Assert.Contains("0 doors", Seed());
    }

    [Fact]
    public void TheBankArrowPointsAtTheDoorThenAtMerrin()
    {
        using var p = new Player();
        TutorialTestStart.Begin(p.Pm);
        PlayCoreUntil(p.Pm, "core.banking");
        var isle = TutorialWorld.MapOf(p.Pm)!;
        p.Pm.MoveToWorld(TutorialWorld.Ground(isle, TutorialWorld.BankStop), isle);
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.banking")!);
        var trial = TutorialCoreTrials.Current(p.Pm)!;
        Assert.Equal(TutorialWorld.Ground(isle, TutorialWorld.BankDoor), trial.ArrowAt);
        Assert.NotNull(p.Pm.QuestArrow);

        p.Pm.MoveToWorld(new Point3D(6762, 3978, 0), isle); // through the door
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.6));
        Assert.Equal(TutorialCorePlacement.BankerSpot, trial.ArrowAt);

        p.Pm.MoveToWorld(TutorialWorld.Ground(isle, TutorialWorld.BankStop), isle); // and out again
        ShardTestClock.Advance(TimeSpan.FromSeconds(0.6));
        Assert.Equal(TutorialWorld.Ground(isle, TutorialWorld.BankDoor), trial.ArrowAt);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheLeagueNoticeBoardStandsInTheLodgeAndOpensTheLeaguesWindow()
    {
        Seed();
        var board = Assert.Single(ItemsAt<TutorialLeagueBoard>(TutorialWorld.Ground(TutorialCorePlacement.LeagueBoard)));
        Assert.Equal(TutorialLeagueBoard.BoardName, board.Name);
        Assert.Contains("0 props", Seed());

        using var p = new Player();
        p.Pm.MoveToWorld(new Point3D(board.X + 6, board.Y, board.Z), board.Map);
        board.OnDoubleClick(p.Pm);
        Assert.False(p.Pm.HasGump<LeagueRegistrarGump>());
        p.Pm.MoveToWorld(new Point3D(board.X + 1, board.Y + 1, board.Z), board.Map);
        board.OnDoubleClick(p.Pm);
        Assert.True(p.Pm.HasGump<LeagueRegistrarGump>());
    }
}
