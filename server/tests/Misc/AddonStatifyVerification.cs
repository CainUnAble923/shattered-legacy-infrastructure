// AddonStatifyVerification.cs
//
// cc-P861: [VBuild statify (server/customizations/StaffBuildTools/AddonStatify.cs, ClusterFVisualBuilder.cs). Notes in
// shard-migration notes/cc-P861-names-and-statify.md.
//
// Facts:
//   1. Statify puts a plain Static where each part stood, with its art, hue, name and light; the addon and its parts go
//      to the internal map; the statics are the session's; it is one undo step.
//   2. Undo takes the statics away and brings the addon and every part back where they stood.
//   3. It refuses, and changes nothing, for: something that is not an addon, an addon a seasonal decor tracks, an addon
//      type a seeder keeps, and an addon already off the map.
//   4. A statify step that falls off the undo history deletes its parked addon for good, and so does forget.
//   5. The builder's store (version 1) saves a statify across a restart, so undo still brings the addon back; a
//      version 0 save still reads.
//   6. [VBuild statify asks for a target and freezes the addon of the part targeted.
//   7. (cc-P877, P876 item 2) It refuses the Fountain of Youth, fixed or roaming, and leaves it in the world and in the
//      fountain system's list; every addon type a seeder re-places is on the refusal list.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.BattleChess;
using Server.Commands;
using Server.Engines.FountainOfYouth;
using Server.Items;
using Server.Mobiles;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class AddonStatifyVerification : IDisposable
{
    private static readonly Point3D At = new(5740, 1200, 0);
    private static readonly Map M = Map.Felucca;
    private readonly List<IEntity> _made = [];
    private readonly List<Account> _accounts = [];

    public AddonStatifyVerification()
    {
        ShardTestHost.EnsureAccounts();
        ClusterFVisualBuilder.ResetAll();
    }

    public void Dispose()
    {
        foreach (var e in _made)
        {
            e?.Delete();
        }

        foreach (var account in _accounts)
        {
            for (var i = 0; i < account.Length; i++)
            {
                account[i]?.Delete();
            }

            Accounts.Remove(account);
        }

        ClusterFVisualBuilder.ResetAll();
    }

    private T Track<T>(T e) where T : IEntity
    {
        _made.Add(e);
        return e;
    }

    private PlayerMobile Staff()
    {
        var account = new Account($"p861{Guid.NewGuid():N}"[..16], "p861-test-only");
        _accounts.Add(account);
        var pm = new PlayerMobile { Player = true, Race = Race.Human, AccessLevel = AccessLevel.GameMaster, Name = "Freezer" };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(At.X - 2, At.Y - 2, 0), M);
        return pm;
    }

    private TestAddon Placed(Point3D at)
    {
        var a = Track(new TestAddon());
        a.MoveToWorld(at, M);
        return a;
    }

    // ---- 1 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ItLeavesAPlainStaticForEachPartAndParksTheAddon()
    {
        var gm = Staff();
        var addon = Placed(At);
        var parts = addon.Components.Select(c => (c.ItemID, c.Hue, c.Name, c.Light, c.Location)).ToList();

        var made = AddonStatify.Statify(gm, addon, out var refused);

        Assert.Null(refused);
        Assert.NotNull(made);
        Track(addon);
        made!.ForEach(s => Track(s));
        Assert.Equal(parts.Count, made.Count);
        foreach (var (itemId, hue, name, light, loc) in parts)
        {
            var s = Assert.Single(made, x => x.Location == loc && x.ItemID == itemId);
            Assert.Equal(typeof(Static), s.GetType());
            Assert.Same(M, s.Map);
            Assert.Equal(hue, s.Hue);
            Assert.Equal(name, s.Name);
            Assert.Equal(light, s.Light);
        }

        Assert.False(addon.Deleted);
        Assert.Same(Map.Internal, addon.Map);
        Assert.All(addon.Components, c => Assert.Same(Map.Internal, c.Map));

        var session = ClusterFVisualBuilder.Get(gm);
        Assert.Single(session.Steps);
        Assert.Equal("statify", session.Steps[0].Kind);
        Assert.All(made, s => Assert.Contains(s.Serial, session.Placed));
        Assert.All(made, s => Assert.True(ClusterFVisualBuilder.CanModify(gm, s)));     // erase, hue and nudge work on them
    }

    // ---- 2 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void UndoBringsTheAddonBackAndTakesTheStaticsAway()
    {
        var gm = Staff();
        var addon = Placed(new Point3D(At.X, At.Y + 6, 0));
        var where = addon.Components.Select(c => (c.Serial, c.Location)).ToList();
        var made = AddonStatify.Statify(gm, addon, out _)!;

        var n = ClusterFVisualBuilder.Undo(gm);

        Assert.Equal(made.Count + 1, n);
        Assert.All(made, s => Assert.True(s.Deleted));
        Assert.Same(M, addon.Map);
        Assert.Equal(new Point3D(At.X, At.Y + 6, 0), addon.Location);
        foreach (var (serial, loc) in where)
        {
            var c = Assert.Single(addon.Components, x => x.Serial == serial);
            Assert.Same(M, c.Map);
            Assert.Equal(loc, c.Location);
        }

        Assert.Empty(ClusterFVisualBuilder.Get(gm).Steps);
    }

    // ---- 3 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ItRefusesWhatItMustNotFreezeAndChangesNothing()
    {
        var gm = Staff();

        var plain = Track(new Static(0x0519));
        plain.MoveToWorld(new Point3D(At.X + 8, At.Y, 0), M);
        Assert.Null(AddonStatify.AddonOf(plain));
        Assert.Null(AddonStatify.Statify(gm, AddonStatify.AddonOf(plain), out var why1));
        Assert.Contains("not an addon", why1);

        var seasonal = Placed(new Point3D(At.X + 8, At.Y + 4, 0));
        ClusterFSeasonalState.Placed("p861-test").Add(seasonal.Serial);
        try
        {
            Assert.Null(AddonStatify.Statify(gm, seasonal, out var why2));
            Assert.Contains("seasonal decor", why2);
            Assert.Same(M, seasonal.Map);
        }
        finally
        {
            ClusterFSeasonalState.Placed("p861-test").Remove(seasonal.Serial);
        }

        var tent = Track(new MinersCompactTentAddon());
        tent.MoveToWorld(new Point3D(At.X + 14, At.Y, 0), M);
        Assert.Null(AddonStatify.Statify(gm, tent, out var why3));
        Assert.Contains("seeder", why3);
        Assert.Same(M, tent.Map);

        var gone = Placed(new Point3D(At.X + 8, At.Y + 8, 0));
        gone.Internalize();
        Assert.Null(AddonStatify.Statify(gm, gone, out var why4));
        Assert.Contains("not an addon in the world", why4);

        Assert.Empty(ClusterFVisualBuilder.Get(gm).Steps);
        Assert.Contains(typeof(AlembicHallAddon), AddonStatify.SeededTypes);
        Assert.Contains(typeof(LeagueHallAddon), AddonStatify.SeededTypes);
    }

    // ---- 4 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void AParkedAddonGoesWhenItsStepFallsOffTheHistoryOrTheSessionIsForgotten()
    {
        ClusterFVisualBuilder.Configure();
        var gm = Staff();
        var first = Placed(new Point3D(At.X, At.Y + 12, 0));
        AddonStatify.Statify(gm, first, out _)!.ForEach(s => Track(s));

        for (var i = 0; i < ClusterFVisualBuilder.MaxUndo; i++)
        {
            Track(ClusterFVisualBuilder.Paint(gm, 0x0519, 0, M, new Point3D(At.X + 20 + i % 10, At.Y + 12 + i / 10, 0), out _));
        }

        Assert.True(first.Deleted);
        Assert.All(first.Components, c => Assert.True(c.Deleted));

        var second = Placed(new Point3D(At.X, At.Y + 18, 0));
        AddonStatify.Statify(gm, second, out _)!.ForEach(s => Track(s));
        Assert.False(second.Deleted);
        Assert.True(CommandSystem.Handle(gm, "[VBuild forget"));
        Assert.True(second.Deleted);
    }

    // ---- 5 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheStoreSavesAStatifyAcrossARestartAndReadsAnOldSave()
    {
        var gm = Staff();
        var addon = Placed(new Point3D(At.X, At.Y + 24, 0));
        AddonStatify.Statify(gm, addon, out _)!.ForEach(s => Track(s));

        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        ClusterFVisualBuilderStore.WriteTo(writer);
        writer.Flush();
        Assert.Equal(1, ClusterFVisualBuilderStore.CurrentVersion);

        ClusterFVisualBuilder.ResetAll();                                       // a restart
        ClusterFVisualBuilderStore.ReadFrom(new BufferReader(buffer));

        var step = Assert.Single(ClusterFVisualBuilder.Get(gm).Steps);
        var stash = Assert.Single(step.Stashed);
        Assert.Equal(addon.Serial, stash.Serial);
        Assert.Equal(M.MapIndex, stash.MapIndex);

        ClusterFVisualBuilder.Undo(gm);
        Assert.Same(M, addon.Map);
        Assert.Equal(new Point3D(At.X, At.Y + 24, 0), addon.Location);

        // A version 0 save (before statify): one session, one paint step, no stash list.
        var old = new byte[256];
        var w = new BufferWriter(old, true);
        w.WriteEncodedInt(0);
        w.WriteEncodedInt(1);
        w.Write(gm.Serial);
        w.WriteEncodedInt(0x0519);
        w.WriteEncodedInt(0);
        w.WriteEncodedInt(0);       // placed
        w.WriteEncodedInt(1);       // steps
        w.Write("paint");
        w.WriteEncodedInt(0);
        w.WriteEncodedInt(0);
        w.WriteEncodedInt(0);
        w.WriteEncodedInt(0);
        w.Flush();
        ClusterFVisualBuilderStore.ReadFrom(new BufferReader(old));
        var oldStep = Assert.Single(ClusterFVisualBuilder.Get(gm).Steps);
        Assert.Equal("paint", oldStep.Kind);
        Assert.Empty(oldStep.Stashed);
    }

    // ---- 6 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheCommandAsksForATargetAndFreezesThePartsAddon()
    {
        ClusterFVisualBuilder.Configure();
        var gm = Staff();
        var addon = Placed(new Point3D(At.X, At.Y + 30, 0));

        Assert.True(CommandSystem.Handle(gm, "[VBuild statify"));
        Assert.NotNull(gm.Target);
        gm.Target.Invoke(gm, addon.Components[1]);

        Assert.Same(Map.Internal, addon.Map);
        var step = Assert.Single(ClusterFVisualBuilder.Get(gm).Steps);
        Assert.Equal(addon.Components.Count, step.Placed.Count);
        foreach (var serial in step.Placed)
        {
            Track(World.FindItem(serial)!);
        }
    }

    // Three parts: one plain, one hued and named, one that gives light.
    // ---- 7 ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ItRefusesTheFountainOfYouthAndLeavesItInTheWorld(bool roaming)
    {
        var gm = Staff();
        var fountain = Track(new FountainOfYouthAddon(roaming));
        var at = new Point3D(At.X + 30, At.Y + (roaming ? 6 : 0), 0);
        fountain.MoveToWorld(at, M);
        var part = fountain.Components[0];

        Assert.Same(fountain, AddonStatify.AddonOf(part));
        Assert.Null(AddonStatify.Statify(gm, AddonStatify.AddonOf(part), out var why));
        Assert.Contains("seeder", why);
        Assert.Contains(nameof(FountainOfYouthAddon), why);

        Assert.False(fountain.Deleted);
        Assert.Same(M, fountain.Map);
        Assert.Equal(at, fountain.Location);
        Assert.All(fountain.Components, c => Assert.Same(M, c.Map));
        Assert.Contains(fountain, FountainOfYouthSystem.Fountains);
        Assert.Empty(ClusterFVisualBuilder.Get(gm).Steps);
        var statics = 0;                                                     // no frozen copy left beside it
        foreach (var item in M.GetItemsInRange(at, 3))
        {
            if (item is Static)
            {
                statics++;
            }
        }

        Assert.Equal(0, statics);
    }

    [Fact]
    public void EveryAddonASeederRePlacesIsOnTheRefusalList()
    {
        Type[] seeded =
        [
            typeof(FountainOfYouthAddon), typeof(BattleChessBoard), typeof(FortuneTellerWagon),
            typeof(TutorialFarmAddon), typeof(TutorialIsleFarmAddon), typeof(WakeningIsleShipwreckAddon),
            typeof(WakeningIsleLighthouseAddon), typeof(WakeningIsleMoongateFrameAddon), typeof(MinersCompactTentAddon),
            typeof(SurveyArchivistTentAddon), typeof(RegistrarOfficeAddon), typeof(RegistrarOfficeOutsideAddon),
            typeof(RegistrarQuartersAddon), typeof(AlembicHallAddon), typeof(LeagueHallAddon)
        ];

        Assert.All(seeded, t => Assert.Contains(t, AddonStatify.SeededTypes));
    }

    private sealed class TestAddon : BaseAddon
    {
        public TestAddon()
        {
            AddComponent(new AddonComponent(0x0B8F), 0, 0, 0);
            AddComponent(new AddonComponent(0x0A99) { Hue = 0x482, Name = "a guild banner" }, 1, 0, 0);
            AddComponent(new AddonComponent(0x0A0C) { Light = LightType.Circle300 }, 0, 1, 13);
        }

        public TestAddon(Serial serial) : base(serial)
        {
        }
    }
}
