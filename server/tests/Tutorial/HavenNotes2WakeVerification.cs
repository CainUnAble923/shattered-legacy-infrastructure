// cc-P802 item 7 (Chase 11-07, a fresh character waking on the Wakening Isle: "Still too much going on in system messages.
// it should all be gumps."): the wake is one window that holds the wake text, how to get up and the login notes, then the
// first step. Notes: shard-migration notes/cc-P802-haven-notes-2.md.

#nullable enable

using System;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.LootBoxes;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HavenNotes2WakeVerification : IDisposable
{
    private readonly Func<PlayerMobile, int> _loopProvider = ClusterFLootBoxes.LoopProvider;

    public HavenNotes2WakeVerification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        ClusterFTutorial.Enabled = true;
        FoldMaps.Configure();
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
        TutorialWake.EnsureHooks();
        ClusterFLootBoxes.LoopProvider = ClusterFLoops.LoopOf;
        TutorialWorld.IsleReady = () => true;
        TutorialTestStart.ReleaseIdleCopies();
    }

    public void Dispose()
    {
        ClusterFLootBoxes.LoopProvider = _loopProvider;
        TutorialTestStart.ReleaseIdleCopies();
    }

    [Fact]
    public void P802_TheWakeIsOneWindowWithTheLoginNotesThenTheFirstStep()
    {
        var account = new Account($"p802w{Guid.NewGuid():N}"[..16], "p802-test-only");
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Ashore", RawStr = 60, RawDex = 60, RawInt = 60 };
        pm.Hits = pm.HitsMax;
        pm.AddItem(new Server.Items.Backpack());
        account[0] = pm;
        pm.MoveToWorld(HavenSpots.Start, HavenSpots.Map);
        var ns = PacketTestUtilities.CreateTestNetState();
        ns.Account = account;
        pm.NetState = ns;
        ns.Mobile = pm;
        try
        {
            TutorialStart.Record(pm, TutorialStart.Choice.Isle);
            TutorialTestStart.Begin(pm);
            Assert.True(TutorialWake.IsLying(pm));

            var gump = pm.FindGump<TutorialWelcomeGump>();
            Assert.NotNull(gump);
            Assert.Contains(TutorialWake.LyingLine, gump.Text);
            Assert.Contains(TutorialWelcome.StandHint, gump.Text);

            // The login's notes come into the window, not the System window, while they lie there.
            var young = ClusterFYoungPlayer.LoginMessage(TimeSpan.FromDays(13));
            Assert.True(TutorialWelcome.Note(pm, young));
            Assert.True(TutorialWelcome.Note(pm, ClusterFTutorial.ContinuesLine));
            Assert.True(TutorialWelcome.Note(pm, young)); // once only
            gump = pm.FindGump<TutorialWelcomeGump>()!;
            Assert.Equal(new[] { young, ClusterFTutorial.ContinuesLine }, gump.Notes);

            // Up: the window turns to the first step.
            TutorialWake.Stand(pm, true);
            ShardTestClock.Advance(TimeSpan.FromSeconds(TutorialWake.DawnLight.Length + 1));
            gump = pm.FindGump<TutorialWelcomeGump>()!;
            Assert.Equal(TutorialWelcome.FirstStep, gump.Text);
            Assert.Equal(2, gump.Notes.Count); // still there to read until closed
        }
        finally
        {
            TutorialCoreTrials.Cancel(pm);
            TutorialWake.Stand(pm, true);
            TutorialWelcome.Forget(pm);
            ClusterFFanfare.FlushAll();
            pm.NetState = null;
            ns.Mobile = null;
            ns.Dispose();
            pm.Delete();
            Accounts.Remove(account);
        }
    }
}
