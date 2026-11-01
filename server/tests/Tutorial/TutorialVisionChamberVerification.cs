// cc-P742 (Chase 10-30): the loop vision plays in a dungeon shrine (TutorialVisionChamber), not where the player stands.
// Notes: shard-migration notes/cc-P742-isle-vision-chamber.md. Each fact was seen failing on a mutant that undoes its
// part only (notes/cc-P742-tools/mutate742.py, mutants.txt).
//
//   1. The room: every piece inside its 9 x 9 box, the dungeon wall laid by the guide's corner rule (post NW, east-west
//      pieces on the north and south rows, north-south pieces on the west and east columns, the corner SE), nothing but
//      floor on the tiles the scene uses, and the slot strip clear of the isle and of Blackthorn's Fold dungeon.
//   2. The vision takes the player into the room: hidden, frozen, at the onlooker's spot facing the pedestal, the room
//      built round them, in the dark; the Gem on the pedestal when the light comes.
//   3. The Stranger walks out of the corner to the Gem, faces it and strikes; the Gem breaks with a flash into shards on
//      the pedestal and round it, and the light comes back.
//   4. At the end the player is back on the exact tile they stood on, unfrozen, hidden as before (seen, or still hidden
//      if they were), and the room is gone.
//   5. A disconnect mid-scene puts them back on that tile at once, the room gone.
//   6. A restart mid-scene: the save keeps the spot, and recovery puts them back on it with the room gone; a save from
//      before cc-P742 (version 0) still reads, and sends them to the stones as before.
//   7. A login inside the chamber strip with no scene (a lost record) goes to the stones, seen and free.
//   8. Two scenes on one map take two slots, out of each other's view.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
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
public class TutorialVisionChamberVerification
{
    private static bool _startupHooksRun;

    public TutorialVisionChamberVerification()
    {
        ShardTestClock.Arm();
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }

        ClusterFTutorial.Enabled = true;
        FoldMaps.Configure();
        TutorialCoreTrials.EnsureHooks();
        TutorialVisionScene.ShowPanels = false;
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account;
        public readonly PlayerMobile Pm;
        public NetState? Ns;

        public Player(string name = "Watcher")
        {
            Account = new Account($"p742{Guid.NewGuid():N}"[..16], "p742-test-only");
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = name, RawStr = 60, RawDex = 60, RawInt = 60 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1700, 1700, 0), Map.Trammel);
            Connect();
        }

        public void Connect()
        {
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Drop()
        {
            Pm.NetState = null;
            if (Ns != null)
            {
                Ns.Mobile = null;
                Ns.Dispose();
                Ns = null;
            }
        }

        public void Dispose()
        {
            TutorialVisionScene.End(Pm, false);
            TutorialCoreTrials.Cancel(Pm);
            ClusterFFanfare.FlushAll();
            Drop();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    /// <summary>A trainee at the stones, with the lessons before the vision done and the vision lesson started.</summary>
    private static Player AtTheStones(string name = "Watcher")
    {
        var p = new Player(name);
        TutorialTestStart.Begin(p.Pm);
        var record = ClusterFTutorial.RecordOf(p.Pm)!;
        foreach (var station in TutorialStations.Core.TakeWhile(s => s.Key != "core.loop"))
        {
            record.StationsDone.Add(station.Key);
        }

        p.Pm.MoveToWorld(TutorialCorePlacement.Spot("core.loop"), TutorialWorld.MapOf(p.Pm));
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.loop")!);
        Assert.True(TutorialCoreTrials.IsDoing(p.Pm, "core.loop"));
        return p;
    }

    private static Point3D Spot(TutorialVisionScene.Scene s, Point2D p) => TutorialVisionChamber.At(s.Slot, p);

    private static TutorialVisionProp[] RoomPieces(Map map, int slot) =>
        [..map.GetItemsInRange<TutorialVisionProp>(TutorialVisionChamber.At(slot, new Point2D(4, 4)), 6)];

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheRoomIsWholeKeepsTheSceneTilesClearAndStandsWhereNobodyWalks()
    {
        var pieces = TutorialVisionChamberLayout.Pieces;
        Assert.All(pieces, r => Assert.InRange(r[1], 0, TutorialVisionChamber.Size - 1));
        Assert.All(pieces, r => Assert.InRange(r[2], 0, TutorialVisionChamber.Size - 1));
        int At(int x, int y, Func<int, bool> kind) => pieces.Count(r => r[1] == x && r[2] == y && kind(r[0]));
        bool Wall(int id) => id is >= 0x0241 and <= 0x0244;
        var n = TutorialVisionChamber.Size - 1;

        Assert.Equal(1, pieces.Count(r => r[0] == 0x0244 && r[1] == 0 && r[2] == 0)); // post NW
        Assert.Equal(1, pieces.Count(r => r[0] == 0x0243 && r[1] == n && r[2] == n)); // corner SE
        for (var x = 1; x <= n; x++)
        {
            Assert.Equal(1, pieces.Count(r => r[0] == 0x0241 && r[1] == x && r[2] == 0)); // the north row, to the NE tile
        }

        for (var y = 1; y <= n; y++)
        {
            Assert.Equal(1, pieces.Count(r => r[0] == 0x0242 && r[1] == 0 && r[2] == y)); // the west column, to the SW tile
        }

        for (var i = 1; i < n; i++)
        {
            Assert.Equal(1, pieces.Count(r => r[0] == 0x0241 && r[1] == i && r[2] == n)); // the south row
            Assert.Equal(1, pieces.Count(r => r[0] == 0x0242 && r[1] == n && r[2] == i)); // the east column
        }

        Assert.Equal(4 * n, pieces.Count(r => Wall(r[0]))); // nothing else of the set, no doubled corner

        bool Floor(int id) => id is >= 0x0519 and <= 0x0524 or >= 0x053B and <= 0x0553;
        var used = new[] { TutorialVisionChamber.Onlooker, TutorialVisionChamber.Echo, TutorialVisionChamber.Hooded }
            .Concat(TutorialVisionChamber.StrangerWalk);
        foreach (var p in used)
        {
            Assert.Equal(0, At(p.X, p.Y, id => !Floor(id))); // only floor where the scene stands its figures
        }

        Assert.Equal(1, At(4, 4, id => id == TutorialVisionChamber.PedestalId));

        // the strip: west of the isle, north of Blackthorn's Fold dungeon, on the void of site A
        var strip = TutorialVisionChamber.Strip;
        Assert.True(strip.X + strip.Width <= TutorialWorld.Area.X);
        // north of Blackthorn's Fold dungeon (BlackthornFold.Dungeon starts at y 3804 on main; the isle-only release
        // has no Blackthorn on the Fold, so the number is written here, not read)
        Assert.True(strip.Y + strip.Height <= 3804);
        Assert.True(strip.X >= 6160 && strip.Y >= 3744); // inside site A (FoldMirrorRegion.Area)
        // the next room's nearest piece, east or west, is out of the onlooker's view (a client sees 18 tiles)
        var o = TutorialVisionChamber.Onlooker;
        Assert.True(TutorialVisionChamber.Spacing - o.X > 18);
        Assert.True(TutorialVisionChamber.Spacing - (TutorialVisionChamber.Size - 1) + o.X > 18);
        Assert.True(TutorialVisionChamber.Corner(TutorialVisionChamber.Slots - 1).X + TutorialVisionChamber.Size <= TutorialWorld.Area.X);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheVisionTakesThePlayerHiddenAndFrozenIntoTheShrine()
    {
        using var p = AtTheStones();
        var origin = p.Pm.Location;
        var map = p.Pm.Map!;
        Assert.False(p.Pm.Hidden);

        TutorialCoreTrials.WatchVision(p.Pm);
        var scene = TutorialVisionScene.Current(p.Pm)!;

        Assert.Equal(Spot(scene, TutorialVisionChamber.Onlooker), p.Pm.Location);
        Assert.Equal(map, p.Pm.Map);
        Assert.True(p.Pm.Hidden);
        Assert.True(p.Pm.Frozen);
        Assert.Equal(Direction.Up, p.Pm.Direction & Direction.Mask); // toward the pedestal
        Assert.Equal(TutorialVisionScene.Black, TutorialVisionScene.LightOf(p.Pm));
        Assert.Equal(origin, scene.Origin);
        Assert.Equal(TutorialVisionChamberLayout.Pieces.Length, scene.Chamber.Count);
        Assert.Equal(TutorialVisionChamberLayout.Pieces.Length, RoomPieces(map, scene.Slot).Length);
        Assert.Equal(Spot(scene, TutorialVisionChamber.Pedestal), scene.Pedestal!.Location);
        Assert.Equal(2, scene.Chamber.Count(i => i.ItemID == TutorialVisionChamber.BrazierId && i.Light == LightType.Circle300));

        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialVisionScene.Beats.Lights + 0.5));
        Assert.Equal(TutorialVisionScene.Lit, TutorialVisionScene.LightOf(p.Pm));
        var gem = scene.Gem!;
        Assert.Equal(scene.Pedestal.X, gem.X);
        Assert.Equal(scene.Pedestal.Y, gem.Y);
        Assert.True(gem.Z > scene.Pedestal.Z); // on top of it
        Assert.Equal(Spot(scene, TutorialVisionChamber.StrangerWalk[0]), scene.Stranger!.Location);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheStrangerWalksToTheGemStrikesAndTheGemShatters()
    {
        using var p = AtTheStones();
        TutorialCoreTrials.WatchVision(p.Pm);
        var scene = TutorialVisionScene.Current(p.Pm)!;

        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialVisionScene.Beats.Walk + 0.5));
        var stranger = scene.Stranger!;
        Assert.Equal(Spot(scene, TutorialVisionChamber.StrangerWalk[1]), stranger.Location);
        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialVisionScene.Beats.Face - TutorialVisionScene.Beats.Walk));
        Assert.Equal(Spot(scene, TutorialVisionChamber.StrangerWalk[2]), stranger.Location); // beside the pedestal
        Assert.Equal(Direction.Down, stranger.Direction & Direction.Mask); // facing the Gem, and the onlooker

        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialVisionScene.Beats.Strike - TutorialVisionScene.Beats.Face));
        Assert.Equal("The sword strikes the Gem. The shards fly.", scene.Subtitle);
        Assert.NotNull(scene.Gem);

        var pedestal = scene.Pedestal!;
        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialVisionScene.Beats.Shards - TutorialVisionScene.Beats.Strike));
        Assert.Null(scene.Gem); // broken
        Assert.Equal(TutorialVisionScene.Flash, TutorialVisionScene.LightOf(p.Pm)); // the flash
        var shards = scene.Placed.OfType<Item>().Where(i => i.ItemID == TutorialVisionScene.ShardsId).ToList();
        Assert.Contains(shards, i => i.X == pedestal.X && i.Y == pedestal.Y && i.Z > pedestal.Z); // on the pedestal
        Assert.Equal(TutorialVisionChamber.Shards.Length + 1, shards.Count);
        Assert.Contains(scene.Placed.OfType<Item>(), i => i.ItemID == TutorialVisionScene.SwordId && i.Location == Spot(scene, TutorialVisionChamber.BrokenSword));

        ShardTestClock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(TutorialVisionScene.Lit, TutorialVisionScene.LightOf(p.Pm)); // the braziers' light again
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void AtTheEndThePlayerIsBackOnTheirTileFreeAndHiddenAsBefore()
    {
        foreach (var wasHidden in new[] { false, true })
        {
            using var p = AtTheStones(wasHidden ? "Sneak" : "Watcher");
            var aside = TutorialWorld.Ground(p.Pm.Map!, new Point2D(p.Pm.X + 1, p.Pm.Y + 1)); // not the stones' own tile
            p.Pm.MoveToWorld(aside, p.Pm.Map);
            p.Pm.Hidden = wasHidden;
            var origin = p.Pm.Location;
            var map = p.Pm.Map!;
            TutorialCoreTrials.WatchVision(p.Pm);
            var slot = TutorialVisionScene.Current(p.Pm)!.Slot;

            ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialVisionScene.Length + 1));

            Assert.False(TutorialVisionScene.IsPlaying(p.Pm));
            Assert.Contains("core.loop", ClusterFTutorial.RecordOf(p.Pm)!.StationsDone);
            Assert.Equal(origin, p.Pm.Location);
            Assert.Equal(map, p.Pm.Map);
            Assert.False(p.Pm.Frozen);
            Assert.Equal(wasHidden, p.Pm.Hidden);
            Assert.Null(TutorialVisionScene.LightOf(p.Pm));
            Assert.Empty(RoomPieces(map, slot));
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void ADisconnectMidSceneSendsThePlayerStraightBackAndTakesTheRoomAway()
    {
        using var p = AtTheStones();
        var origin = p.Pm.Location;
        var map = p.Pm.Map!;
        TutorialCoreTrials.WatchVision(p.Pm);
        var slot = TutorialVisionScene.Current(p.Pm)!.Slot;
        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialVisionScene.Beats.Shards + 1));
        Assert.True(TutorialVisionChamber.Contains(p.Pm.Location, map));

        p.Drop();
        ShardTestClock.Advance(TimeSpan.FromSeconds(1.5));

        Assert.False(TutorialVisionScene.IsPlaying(p.Pm));
        Assert.Equal(origin, p.Pm.Location);
        Assert.False(p.Pm.Frozen);
        Assert.False(p.Pm.Hidden);
        Assert.Empty(RoomPieces(map, slot));
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void ARestartMidSceneIsRecoveredOntoTheSavedSpot()
    {
        using var p = AtTheStones();
        var origin = p.Pm.Location;
        var map = p.Pm.Map!;
        TutorialCoreTrials.WatchVision(p.Pm);
        var scene = TutorialVisionScene.Current(p.Pm)!;
        ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialVisionScene.Beats.Shards + 1));
        var everything = scene.Placed.Concat(scene.Chamber).ToArray();
        Assert.True(TutorialVisionChamber.Contains(p.Pm.Location, map));

        var buffer = new byte[262144];
        var writer = new BufferWriter(buffer, true);
        TutorialVisionStore.WriteTo(writer);
        writer.Flush();

        // The process ends mid-scene: the save holds the player in the room, hidden and frozen, and the room itself.
        TutorialVisionScene.AbandonForRestart(p.Pm);
        TutorialCoreTrials.Cancel(p.Pm);
        var strays = -1;
        try
        {
            TutorialVisionStore.ReadFrom(new BufferReader(buffer));
            Assert.True(TutorialVisionStore.IsPending(p.Pm.Serial));
            TutorialVisionScene.Initialize(); // after World.Load
            strays = everything.Count(e => !e.Deleted);
        }
        finally
        {
            foreach (var e in everything.Where(e => !e.Deleted).ToList()) { e.Delete(); }
        }

        Assert.Equal(0, strays);
        Assert.Equal(origin, p.Pm.Location);
        Assert.False(p.Pm.Frozen);
        Assert.False(p.Pm.Hidden);
        Assert.False(TutorialVisionStore.IsPending(p.Pm.Serial));
    }

    [Fact]
    public void AVersionZeroSaveStillReadsAndRecoversToTheStones()
    {
        // two scenes, so a version 0 record read as version 1 would swallow the second one's bytes
        using var p = AtTheStones();
        using var q = AtTheStones("Second");
        var buffer = new byte[4096];
        var writer = new BufferWriter(buffer, true);
        writer.WriteEncodedInt(0);  // version 0
        writer.WriteEncodedInt(2);  // two scenes
        foreach (var who in new[] { p, q })
        {
            writer.Write(who.Pm.Serial);
            writer.WriteEncodedInt(who.Pm.Map!.MapIndex);
            writer.WriteEncodedInt(0);  // nothing placed
        }

        writer.Flush();
        foreach (var who in new[] { p, q })
        {
            who.Pm.Frozen = true;
            who.Pm.MoveToWorld(new Point3D(who.Pm.X - 10, who.Pm.Y, who.Pm.Z), who.Pm.Map);
        }

        try
        {
            TutorialVisionStore.ReadFrom(new BufferReader(buffer));
            TutorialVisionScene.Initialize();
        }
        catch (Exception)
        {
            // a misread may throw; the asserts below say what went wrong
        }

        foreach (var who in new[] { p, q })
        {
            Assert.False(who.Pm.Frozen);
            Assert.True(who.Pm.InRange(TutorialVisionScene.Stones(who.Pm.Map!), 4));
        }
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void ALoginInTheChamberWithNoSceneGoesToTheStones()
    {
        using var p = AtTheStones();
        var map = p.Pm.Map!;
        p.Pm.MoveToWorld(TutorialVisionChamber.At(3, TutorialVisionChamber.Onlooker), map);
        p.Pm.Hidden = true;
        p.Pm.Frozen = true;

        TutorialVisionScene.OnLogin(p.Pm);

        Assert.False(TutorialVisionChamber.Contains(p.Pm.Location, map));
        Assert.True(p.Pm.InRange(TutorialVisionScene.Stones(map), 4));
        Assert.False(p.Pm.Hidden);
        Assert.False(p.Pm.Frozen);
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void TwoScenesOnOneMapTakeTwoSlotsOutOfEachOthersView()
    {
        using var a = AtTheStones("First");
        using var b = AtTheStones("Second");
        b.Pm.MoveToWorld(a.Pm.Location, a.Pm.Map); // the same map, whatever copy the first one has
        TutorialCoreTrials.WatchVision(a.Pm);
        Assert.True(TutorialVisionScene.Play(b.Pm));
        var sa = TutorialVisionScene.Current(a.Pm)!;
        var sb = TutorialVisionScene.Current(b.Pm)!;

        Assert.NotEqual(sa.Slot, sb.Slot);
        Assert.False(a.Pm.InRange(b.Pm.Location, 18));
    }
}
