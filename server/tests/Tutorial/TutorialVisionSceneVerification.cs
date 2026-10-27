// cc-P665 (P481 F5): the loop vision as a staged scene. Notes: shard-migration notes/cc-P665-vision-scene.md.
//
// Facts, each seen failing first (notes: the red runs):
//   1. The scene plays its beats for one player: frozen, the scene's light for them alone, the subtitles, the figures, its
//      sounds on their client and on nobody else's; at the end it finishes the lesson, released, light restored, all gone.
//   2. The echo copies the player's look only: body, hue, hair and what they wear; no skills, no master, no AI.
//   3. A disconnect mid-scene: back at the stones, unfrozen, light restored, figures gone, and the stone plays it again.
//   4. Death mid-scene ends it the same way.
//   5. A restart mid-scene: the saved state reads back, and recovery puts the player at the stones, unfrozen, with the
//      figures and props gone.
//   6. The real scene changes no skill, level, loop or Young status (P80's check, run against the scene itself).
//   7. The panels are off by default, and the scene runs to its end without them; on, they show on their beats.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
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
public class TutorialVisionSceneVerification
{
    private static bool _startupHooksRun;

    public TutorialVisionSceneVerification()
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

        public Player(string name = "Newbie")
        {
            Account = new Account($"p665{Guid.NewGuid():N}"[..16], "p665-test-only");
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = name, RawStr = 60, RawDex = 60, RawInt = 60 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1700, 1700, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public int Sent => Ns!.SendBuffer.GetReadSpan().Length;

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
    private static Player AtTheStones()
    {
        var p = new Player();
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

    private static bool Done(Player p) => ClusterFTutorial.RecordOf(p.Pm)!.StationsDone.Contains("core.loop");

    private static int GlobalLight(Mobile m)
    {
        m.ComputeLightLevels(out var global, out _);
        return global;
    }

    /// <summary>Whether a 0x54 sound packet for <paramref name="soundId"/> was sent on <paramref name="ns"/> after byte <paramref name="from"/>.</summary>
    private static bool Heard(NetState ns, int from, int soundId)
    {
        var wire = ns.SendBuffer.GetReadSpan()[from..];
        for (var i = 0; i + 12 <= wire.Length; i++)
        {
            if (wire[i] == 0x54 && wire[i + 1] == 0x01 && (wire[i + 2] << 8 | wire[i + 3]) == soundId)
            {
                return true;
            }
        }

        return false;
    }

    private static TutorialVisionFigure[] FiguresNear(Player p) =>
        [..p.Pm.Map!.GetMobilesInRange<TutorialVisionFigure>(p.Pm.Location, 8)];

    private static TutorialVisionProp[] PropsNear(Player p) =>
        [..p.Pm.Map!.GetItemsInRange<TutorialVisionProp>(p.Pm.Location, 8)];

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheScenePlaysItsBeatsForOnePlayerAndFinishesTheLesson()
    {
        using var p = AtTheStones();
        using var bystander = new Player("Bystander");
        bystander.Pm.MoveToWorld(new Point3D(p.Pm.X + 2, p.Pm.Y, p.Pm.Z), p.Pm.Map);
        var heardFrom = p.Sent;
        var bystanderFrom = bystander.Sent;

        TutorialCoreTrials.WatchVision(p.Pm);

        Assert.True(TutorialVisionScene.IsPlaying(p.Pm));
        Assert.True(p.Pm.Frozen);
        Assert.Equal(TutorialVisionScene.Black, GlobalLight(p.Pm));
        Assert.Equal(LightCycle.DayLevel, GlobalLight(bystander.Pm));
        Assert.True(p.Pm.HasGump<TutorialVisionSubtitleGump>());
        Assert.Equal("A vision.", p.Pm.FindGump<TutorialVisionSubtitleGump>()!.Text);

        ShardTestClock.Advance(TimeSpan.FromSeconds(4)); // the Gem and the Stranger
        Assert.Equal(TutorialVisionScene.Dim, GlobalLight(p.Pm));
        Assert.Contains(FiguresNear(p), f => f.Name == "the Stranger");
        Assert.Contains(PropsNear(p), i => i.ItemID == TutorialVisionScene.GemId);
        Assert.False(Done(p));

        ShardTestClock.Advance(TimeSpan.FromSeconds(19)); // 23 s: the echo has spoken
        Assert.Contains(FiguresNear(p), f => f.Name == p.Pm.Name);
        Assert.Equal(TutorialVisionScene.EchoLine, p.Pm.FindGump<TutorialVisionSubtitleGump>()!.Text);
        Assert.True(p.Pm.Frozen);

        ShardTestClock.Advance(TimeSpan.FromSeconds(23)); // 46 s: over

        Assert.True(Done(p));
        Assert.False(TutorialVisionScene.IsPlaying(p.Pm));
        Assert.False(p.Pm.Frozen);
        Assert.Equal(LightCycle.DayLevel, GlobalLight(p.Pm));
        Assert.False(p.Pm.HasGump<TutorialVisionSubtitleGump>());
        Assert.Empty(FiguresNear(p));
        Assert.Empty(PropsNear(p));

        foreach (var sound in new[] { 0x1FE, 0x20F, 0x29, 0x307, 0x211, 0x220, 0x44D, 0x3C4, 0x212 })
        {
            Assert.True(Heard(p.Ns!, heardFrom, sound), $"the player did not hear 0x{sound:X}");
            Assert.False(Heard(bystander.Ns!, bystanderFrom, sound), $"the bystander heard 0x{sound:X}");
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheEchoCopiesThePlayersLookAndNothingElse()
    {
        using var p = new Player();
        p.Pm.Hue = 0x83F0;
        p.Pm.HairItemID = 0x203B;
        p.Pm.HairHue = 0x44E;
        p.Pm.Skills[SkillName.Swords].Base = 45.0;
        p.Pm.Skills[SkillName.Ninjitsu].Base = 80.0;
        var shirt = new Shirt { Hue = 0x21 };
        p.Pm.AddItem(shirt);

        var echo = TutorialVisionFigure.EchoOf(p.Pm);
        Item? worn = null;
        try
        {
            Assert.Equal(p.Pm.Body, echo.Body);
            Assert.Equal(p.Pm.Hue, echo.Hue);
            Assert.Equal(p.Pm.Name, echo.Name);
            Assert.Equal(0x203B, echo.HairItemID);
            Assert.Equal(0x44E, echo.HairHue);
            var copy = echo.FindItemOnLayer(Layer.Shirt);
            worn = copy;
            Assert.NotNull(copy);
            Assert.IsType<TutorialVisionProp>(copy);
            Assert.Equal(shirt.ItemID, copy.ItemID);
            Assert.Equal(0x21, copy.Hue);
            Assert.NotSame(shirt, copy);
            Assert.Null(echo.FindItemOnLayer(Layer.Backpack));

            Assert.False(echo is BaseCreature);
            Assert.Equal(0.0, echo.Skills[SkillName.Swords].Base);
            Assert.Equal(0.0, echo.Skills[SkillName.Ninjitsu].Base);
            Assert.True(echo.Blessed);
        }
        finally
        {
            echo.Delete();
        }

        // Deleting the figure deletes its copied clothes, never the player's own.
        Assert.True(echo.Deleted);
        Assert.True(worn!.Deleted);
        Assert.False(shirt.Deleted);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void ADisconnectMidSceneReturnsThePlayerToTheStonesAndTheVisionCanBePlayedAgain()
    {
        using var p = AtTheStones();
        var map = p.Pm.Map!;
        TutorialCoreTrials.WatchVision(p.Pm);
        ShardTestClock.Advance(TimeSpan.FromSeconds(13)); // shards on the floor, the Stranger still there
        Assert.NotEmpty(FiguresNear(p));
        Assert.NotEmpty(PropsNear(p));

        p.Drop();
        ShardTestClock.Advance(TimeSpan.FromSeconds(1.5));

        Assert.False(TutorialVisionScene.IsPlaying(p.Pm));
        Assert.False(p.Pm.Frozen);
        Assert.Null(TutorialVisionScene.LightOf(p.Pm));
        Assert.Equal(LightCycle.DayLevel, GlobalLight(p.Pm));
        Assert.Empty(FiguresNear(p));
        Assert.Empty(PropsNear(p));
        Assert.True(p.Pm.InRange(TutorialVisionScene.Stones(map), 4));
        Assert.False(Done(p));

        // Back on: the lesson starts again at the stone, and the vision plays from its first beat.
        p.Ns = PacketTestUtilities.CreateTestNetState();
        p.Ns.Account = p.Account;
        p.Pm.NetState = p.Ns;
        p.Ns.Mobile = p.Pm;
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.loop")!);
        TutorialCoreTrials.WatchVision(p.Pm);
        Assert.True(TutorialVisionScene.IsPlaying(p.Pm));
        ShardTestClock.Advance(TimeSpan.FromSeconds(46));
        Assert.True(Done(p));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void DeathMidSceneEndsItTheSameWay()
    {
        using var p = AtTheStones();
        TutorialCoreTrials.WatchVision(p.Pm);
        ShardTestClock.Advance(TimeSpan.FromSeconds(22));
        Assert.NotEmpty(FiguresNear(p));

        p.Pm.Kill();
        ShardTestClock.Advance(TimeSpan.FromSeconds(1.5));

        Assert.False(TutorialVisionScene.IsPlaying(p.Pm));
        Assert.False(p.Pm.Frozen);
        Assert.Null(TutorialVisionScene.LightOf(p.Pm));
        Assert.Empty(FiguresNear(p));
        Assert.Empty(PropsNear(p));
        Assert.True(p.Pm.InRange(TutorialVisionScene.Stones(p.Pm.Map!), 4));
        Assert.False(Done(p));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void ARestartMidSceneIsRecoveredFromTheSave()
    {
        using var p = AtTheStones();
        var map = p.Pm.Map!;
        var strays = -1;
        TutorialCoreTrials.WatchVision(p.Pm);
        ShardTestClock.Advance(TimeSpan.FromSeconds(13));
        var placed = TutorialVisionScene.Current(p.Pm)!.Placed.ToArray();
        Assert.NotEmpty(placed);

        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        TutorialVisionStore.WriteTo(writer);
        writer.Flush();

        // The process ends: nothing runs the scene's end, and the world holds what the save held.
        TutorialVisionScene.AbandonForRestart(p.Pm);
        TutorialCoreTrials.Cancel(p.Pm);
        Assert.True(p.Pm.Frozen);
        p.Pm.MoveToWorld(new Point3D(p.Pm.X - 10, p.Pm.Y, p.Pm.Z), map);

        try
        {
            TutorialVisionStore.ReadFrom(new BufferReader(buffer));
            Assert.True(TutorialVisionStore.IsPending(p.Pm.Serial));
            TutorialVisionScene.Initialize(); // after World.Load
            strays = placed.Count(e => !e.Deleted);
        }
        finally
        {
            // A failed recovery must not leave its figures for the next fact at the same stones.
            foreach (var e in placed.Where(e => !e.Deleted).ToList()) { e.Delete(); }
        }


        Assert.False(TutorialVisionStore.IsPending(p.Pm.Serial));
        Assert.False(p.Pm.Frozen);
        Assert.Null(TutorialVisionScene.LightOf(p.Pm));
        Assert.True(p.Pm.InRange(TutorialVisionScene.Stones(map), 4));
        Assert.Equal(0, strays);
        Assert.False(p.Pm.HasGump<TutorialVisionSubtitleGump>());

        // And it can be played again.
        ClusterFTutorial.RunStation(p.Pm, TutorialStations.Find("core.loop")!);
        TutorialCoreTrials.WatchVision(p.Pm);
        Assert.True(TutorialVisionScene.IsPlaying(p.Pm));
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheSceneChangesNoSkillLevelLoopOrYoungStatus()
    {
        using var p = AtTheStones();
        p.Account.Young = true;
        p.Pm.Young = true;
        p.Pm.Skills[SkillName.Swords].Base = 45.0;
        p.Pm.Skills[SkillName.Mining].Base = 30.0;
        var skillsBefore = Enum.GetValues<SkillName>().Select(s => p.Pm.Skills[s].Base).ToArray();
        var levelBefore = ClusterFLootBoxes.LevelOf(p.Pm);
        var loopBefore = ClusterFLoops.LoopOf(p.Pm);

        TutorialCoreTrials.WatchVision(p.Pm);
        ShardTestClock.Advance(TimeSpan.FromSeconds(46));

        Assert.True(Done(p));
        Assert.Equal(skillsBefore, Enum.GetValues<SkillName>().Select(s => p.Pm.Skills[s].Base).ToArray());
        Assert.Equal(levelBefore, ClusterFLootBoxes.LevelOf(p.Pm));
        Assert.Equal(loopBefore, ClusterFLoops.LoopOf(p.Pm));
        Assert.True(p.Account.Young);
        Assert.True(p.Pm.Young);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void ThePanelsAreOffByDefaultAndShowOnTheirBeatsWhenOn()
    {
        Assert.False(TutorialVisionScene.ShowPanels);
        Assert.Equal(23209, TutorialVisionScene.PanelBrokenSword);
        Assert.Equal(23210, TutorialVisionScene.PanelCrest);

        using (var off = AtTheStones())
        {
            TutorialCoreTrials.WatchVision(off.Pm);
            ShardTestClock.Advance(TimeSpan.FromSeconds(17.5));
            Assert.False(off.Pm.HasGump<TutorialVisionPanelGump>());
            ShardTestClock.Advance(TimeSpan.FromSeconds(29));
            Assert.True(Done(off));
        }

        TutorialVisionScene.ShowPanels = true;
        try
        {
            using var on = AtTheStones();
            TutorialCoreTrials.WatchVision(on.Pm);
            ShardTestClock.Advance(TimeSpan.FromSeconds(17.5));
            Assert.Equal(TutorialVisionScene.PanelBrokenSword, on.Pm.FindGump<TutorialVisionPanelGump>()!.GumpId);
            ShardTestClock.Advance(TimeSpan.FromSeconds(13)); // 30.5 s
            Assert.Equal(TutorialVisionScene.PanelCrest, on.Pm.FindGump<TutorialVisionPanelGump>()!.GumpId);
            ShardTestClock.Advance(TimeSpan.FromSeconds(16));
            Assert.True(Done(on));
            Assert.False(on.Pm.HasGump<TutorialVisionPanelGump>());
        }
        finally
        {
            TutorialVisionScene.ShowPanels = false;
        }
    }
}
