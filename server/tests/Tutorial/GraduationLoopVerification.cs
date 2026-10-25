using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Multis;
using Server.Spells;
using Server.Spells.Sixth;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class GraduationLoopVerification : IDisposable
{
    private readonly Account _account;
    private readonly PlayerMobile _pm;
    private readonly Func<PlayerMobile, int> _loopProvider = ClusterFLootBoxes.LoopProvider;
    private readonly Action<PlayerMobile> _rewards = ClusterFTutorial.GraduationRewards;
    private readonly bool _gate = TutorialIslandGate.Enabled;
    private readonly bool _skip = ClusterFTutorial.SkipCountsAsGraduation;
    private static readonly Point3D Haven = new(3503, 2574, 14);
    private static readonly Point3D Britain = new(1496, 1628, 10);

    public GraduationLoopVerification()
    {
        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }
        _account = new Account($"p371{Guid.NewGuid():N}"[..16], "test-only");
        _pm = new PlayerMobile { Player = true, Race = Race.Human };
        _pm.AddItem(new Backpack());
        _account[0] = _pm;
        _pm.MoveToWorld(Haven, Map.Trammel);
        ClusterFTutorial.GraduationRewards = _ => { };
        ClusterFLootBoxes.LoopProvider = ClusterFLoops.LoopOf;
        TutorialIslandGate.Enabled = true;
        ClusterFTutorial.SkipCountsAsGraduation = true;
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }
    }

    private void Graduate()
    {
        Assert.True(ClusterFTutorial.TryEnter(_pm));
        foreach (var station in TutorialStations.Core)
        {
            ClusterFTutorial.CompleteStation(_pm, station.Key);
        }
        Assert.Null(ClusterFTutorial.Graduate(_pm));
    }

    private int BoxCount()
    {
        var count = 0;
        foreach (var box in _pm.Backpack.FindItemsByType<LootBox>())
        {
            count++;
        }
        return count;
    }

    [Fact]
    public void GraduationRecordsFirstLoopKeepsYoungAndAwardsLoopBoxOnce()
    {
        _account.Young = _pm.Young = true;
        _pm.Skills[SkillName.Swords].Base = 45;
        Graduate();
        Assert.Equal(1, ClusterFLoops.LoopOf(_pm));
        Assert.Equal(1, ClusterFLootBoxes.CurrentLoop(_pm));
        Assert.True(_account.Young);
        Assert.True(_pm.Young);
        Assert.Equal(45, _pm.Skills[SkillName.Swords].Base);
        Assert.Equal(1, BoxCount());
        Assert.NotNull(ClusterFTutorial.Graduate(_pm));
        ClusterFLoops.Graduate(_pm);
        Assert.Equal(1, BoxCount());
        _pm.MoveToWorld(Britain, Map.Trammel);
        Assert.Equal(Britain, _pm.Location);

        var data = ClusterFAccountPersistence.GetOrCreate(_account);
        var writer = new BufferWriter(true);
        data.Serialize(writer);
        var copy = new ClusterFAccountData(new BufferReader(writer.Buffer));
        Assert.Equal(data.FlagValues, copy.FlagValues);
        var alt = new PlayerMobile();
        try
        {
            _account[1] = alt;
            Assert.Equal(0, ClusterFLoops.LoopOf(alt));
            ClusterFTutorial.OnLogin(alt);
            Assert.Equal(1, ClusterFLoops.LoopOf(alt));
        }
        finally { alt.Delete(); }
    }

    // These are the shared movement entry points used by gates, recall, teleporters and help. The spell checks
    // additionally refuse before resources or pets move; boat transport is preflighted as a whole.
    [Theory]
    [InlineData("gate")]
    [InlineData("recall")]
    [InlineData("teleporter")]
    [InlineData("young help")]
    [InlineData("GM help")]
    [InlineData("boat passenger")]
    [InlineData("facet")]
    public void LoopZeroCannotExitThroughSharedMovementEntryPoints(string route)
    {
        UseExit(route);
        Assert.Equal(Haven, _pm.Location);
        Assert.Equal(Map.Trammel, _pm.Map);
        Graduate();
        UseExit(route);
        Assert.Equal(Britain, _pm.Location);
        Assert.Equal(route == "facet" ? Map.Felucca : Map.Trammel, _pm.Map);
    }

    private void UseExit(string route)
    {
        if (route == "gate")
        {
            var gate = new Moongate(Britain, Map.Trammel);
            try { gate.UseGate(_pm); }
            finally { gate.Delete(); }
        }
        else if (route == "teleporter")
        {
            var teleporter = new Teleporter(Britain, Map.Trammel);
            try { teleporter.DoTeleport(_pm); }
            finally { teleporter.Delete(); }
        }
        else if (route == "boat passenger")
        {
            _pm.SetLocation(Britain, false);
        }
        else if (route == "GM help")
        {
            _pm.Location = Britain;
        }
        else
        {
            _pm.MoveToWorld(Britain, route == "facet" ? Map.Felucca : Map.Trammel);
        }
    }

    [Theory]
    [InlineData(TravelCheckType.RecallTo)]
    [InlineData(TravelCheckType.GateTo)]
    [InlineData(TravelCheckType.TeleportTo)]
    public void TravelChecksRefuseExitsButAllowTravelWithinIsland(TravelCheckType route)
    {
        Assert.False(SpellHelper.CheckTravel(_pm, Map.Trammel, Britain, route, out var message));
        Assert.Contains("Rook", message.String); // cc-P609: the refusal names Rook and Graduate
        Assert.True(SpellHelper.CheckTravel(_pm, Map.Trammel, Haven, route, out _));
    }

    [Fact]
    public void MarkCannotPrepareAnExitBeforeGraduation()
    {
        Assert.False(_pm.CheckSpellCast(new MarkSpell(_pm)));
        Graduate();
        Assert.True(_pm.CheckSpellCast(new MarkSpell(_pm)));
    }

    // A real BaseBoat enumeration and Teleport, with only the footprint faked (no client multi files required).
    private sealed class TestBoat : SmallBoat
    {
        public override bool Contains(int x, int y) => x == X && y == Y;
        public override MovingEntitiesEnumerable GetMovingEntities(bool includeBoat = false) =>
            new(this, includeBoat, new Rectangle2D(X, Y, 1, 1));
    }

    [Fact]
    public void BoatStaysWithItsLoopZeroPassengerThenCarriesAGraduate()
    {
        _pm.Location = new Point3D(3813, 2700, 0);
        var boat = new TestBoat();
        boat.MoveToWorld(_pm.Location, Map.Trammel);
        var before = boat.Location;
        try
        {
            Assert.False(TutorialIslandGate.CanMoveBoat(boat, 1, 0, 0));
            boat.Teleport(1, 0, 0);
            Assert.Equal(before, boat.Location);
            Assert.Equal(before, _pm.Location);
            Graduate();
            boat.Teleport(1, 0, 0);
            Assert.Equal(before.X + 1, boat.X);
            Assert.Equal(boat.Location, _pm.Location);
        }
        finally { boat.Delete(); }
    }

    [Fact]
    public void InternalizationAndIslandLocalMovesRemainAvailable()
    {
        var nearby = new Point3D(Haven.X + 1, Haven.Y, Haven.Z);
        _pm.MoveToWorld(nearby, Map.Trammel);
        Assert.Equal(nearby, _pm.Location);
        _pm.Internalize();
        Assert.Equal(Map.Internal, _pm.Map);
        _pm.MoveToWorld(Haven, Map.Trammel);
        Assert.Equal(Haven, _pm.Location);
        Assert.Equal(0, ClusterFLoops.LoopOf(_pm));
    }

    [Fact]
    public void IslandIncludesItsTutorialDungeonAndThatDungeonCannotBypassTheExitGate()
    {
        var landing = new Point3D(5193, 1138, 0);
        var teleporter = new Teleporter(landing, Map.Trammel);
        try
        {
            teleporter.DoTeleport(_pm);
            Assert.Equal(landing, _pm.Location);
            _pm.MoveToWorld(Britain, Map.Trammel);
            Assert.Equal(landing, _pm.Location);
            _pm.MoveToWorld(Haven, Map.Trammel);
            Assert.Equal(Haven, _pm.Location);
            Graduate();
            _pm.MoveToWorld(landing, Map.Trammel);
            _pm.MoveToWorld(Britain, Map.Trammel);
            Assert.Equal(Britain, _pm.Location);
        }
        finally { teleporter.Delete(); }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SkipFollowsSettingAndRemedialNeverLoopsTwice(bool counts)
    {
        ClusterFTutorial.SkipCountsAsGraduation = counts;
        ClusterFTutorial.TryEnter(_pm);
        Assert.True(ClusterFTutorial.Skip(_pm));
        Assert.Equal(counts ? 1 : 0, ClusterFLoops.LoopOf(_pm));
        _pm.MoveToWorld(Britain, Map.Trammel);
        Assert.Equal(counts ? Britain : Haven, _pm.Location);
        Assert.True(ClusterFTutorial.StartRemedial(_pm));
        foreach (var station in TutorialStations.Core)
        {
            ClusterFTutorial.CompleteStation(_pm, station.Key);
        }
        Assert.Null(ClusterFTutorial.Graduate(_pm));
        Assert.Equal(1, ClusterFLoops.LoopOf(_pm));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StaffAndDisabledGateCanLeave(bool staff)
    {
        TutorialIslandGate.Enabled = staff;
        _pm.AccessLevel = staff ? AccessLevel.Counselor : AccessLevel.Player;
        _pm.MoveToWorld(Britain, Map.Trammel);
        Assert.Equal(Britain, _pm.Location);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1, 100)]
    [InlineData(2, 105)]
    [InlineData(20, 195)]
    [InlineData(21, 200)]
    [InlineData(int.MaxValue, 200)]
    public void LadderMatchesTheDecision(int loop, int cap) => Assert.Equal(cap, ClusterFLoops.MaxLevelFor(loop));

    public void Dispose()
    {
        ClusterFFanfare.FlushAll();
        _pm.Delete();
        Accounts.Remove(_account);
        ClusterFLootBoxes.LoopProvider = _loopProvider;
        ClusterFTutorial.GraduationRewards = _rewards;
        TutorialIslandGate.Enabled = _gate;
        ClusterFTutorial.SkipCountsAsGraduation = _skip;
    }
}
