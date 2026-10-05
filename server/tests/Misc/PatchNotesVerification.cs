// PatchNotesVerification.cs
//
// cc-P22, F-15: the date version and patch notes from CHANGELOG.md, in the login bulletin, [version, the wiki page
// and status.json (the last in ShardStatusPublisherVerification). Notes in shard-migration
// notes/cc-P22-small-features-1.md.
//
// Facts:
//   1. A two-version sample changelog parses into the right versions, bulletin lines, wiki markup and anchors; the
//      format comment at the top (which holds an example version) is not a version.
//   2. An account sees a version's notes once; a new version shows again. cc-P64: All patch notes opens the Patch
//      History gump and no longer the wiki (no open-URL packet, 0xA5); the address itself is still built right.
//   3. An empty changelog posts nothing and does not crash; nor does a VERSION that is not a date version.
//   4. [version says the version and shows the notes (cc-P64: in the Patch History gump).

using System;
using System.Text;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Gumps;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PatchNotesVerification : IDisposable
{
    // The real CHANGELOG.md's header, example and all, then two versions.
    private const string Sample = """
        # Shattered Legacy changelog

        <!--
        How this file works.

        ## 2026.10.01

        ### Added
        - An example inside the comment, which is not a version.
        -->

        ## 2026.10.02.2

        ### Fixed
        - The lookout no longer faces the wall.

        ## 2026.10.02

        ### Added
        - A Thieves' Den lookout sits by the fighting pit.
        - Banks hold 1,000 items.

        ### Changed
        - Jacob's Pickaxe restoration costs less at the first two tiers.

        ### Fixed
        - Guild upgrades take ingots from your bank too.
        """;

    private readonly ITestOutputHelper _out;

    public PatchNotesVerification(ITestOutputHelper output)
    {
        _out = output;

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
    }

    public void Dispose() => ShardVersion.Reset();

    private static bool _startupHooksRun;

    private static bool SentText(NetState ns, int from, string text)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        return span.IndexOf(Encoding.ASCII.GetBytes(text)) >= 0 ||
               span.IndexOf(Encoding.BigEndianUnicode.GetBytes(text)) >= 0;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ATwoVersionChangelogParsesIntoBulletinWikiAndAnchors()
    {
        var versions = PatchNotes.Parse(Sample);

        Assert.Equal(new[] { "2026.10.02.2", "2026.10.02" }, versions.ConvertAll(v => v.Version));
        Assert.Equal(new[] { "The lookout no longer faces the wall." }, versions[0].Fixed);
        Assert.Empty(versions[0].Added);
        Assert.Equal(2, versions[1].Added.Count);
        Assert.Single(versions[1].Changed);
        Assert.Single(versions[1].Fixed);

        Assert.Equal(
            new[]
            {
                "Added: A Thieves' Den lookout sits by the fighting pit.",
                "Added: Banks hold 1,000 items.",
                "Changed: Jacob's Pickaxe restoration costs less at the first two tiers.",
                "Fixed: Guild upgrades take ingots from your bank too."
            },
            PatchNotes.BulletinLines(versions[1])
        );

        // DokuWiki's sectionID of "Version 2026.10.02.2": lowercased, space to "_", dots dropped.
        Assert.Equal("version_202610022", PatchNotes.Anchor("2026.10.02.2"));
        Assert.Equal("version_20261002", PatchNotes.Anchor("2026.10.02"));
        Assert.Equal("https://wiki.shatteredlegacyuo.com/uo:patch_notes#version_20261002", PatchNotes.NotesUrl("2026.10.02"));

        var wiki = PatchNotes.RenderDokuWiki(versions);
        _out.WriteLine(wiki);
        Assert.StartsWith("====== Patch notes ======\n", wiki);
        var newer = wiki.IndexOf("===== Version 2026.10.02.2 =====\n", StringComparison.Ordinal);
        var older = wiki.IndexOf("===== Version 2026.10.02 =====\n", StringComparison.Ordinal);
        Assert.True(newer > 0 && older > newer, "versions missing or out of order");
        Assert.Contains("**Added**\n\n  * A Thieves' Den lookout sits by the fighting pit.\n  * Banks hold 1,000 items.\n", wiki);
        Assert.DoesNotContain("2026.10.01", wiki);
        Assert.All(wiki, c => Assert.True(c < 0x80, $"non-ASCII {(int)c}"));

        // The newest section is the version when VERSION is empty, and VERSION wins when it names one.
        Assert.Empty(ShardVersion.Load(Sample, ""));
        Assert.Equal("2026.10.02.2", ShardVersion.Current);
        Assert.Equal("2026.10.02", Assert.Single(ShardVersion.Load(Sample, "2026.10.02\n")).Split(' ')[2]);
        Assert.Equal("2026.10.02", ShardVersion.CurrentNotes.Version);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AnAccountSeesAVersionsNotesOnceAndAllPatchNotesOpensTheHistory()
    {
        ShardVersion.Load(Sample, "2026.10.02");

        var account = new Account($"p22v{Guid.NewGuid():N}"[..16], "p22-test-only");
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        account[0] = pm;
        pm.MoveToWorld(new Point3D(1250, 1250, 0), Map.Trammel);
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;

        try
        {
            var notes = Assert.IsType<PatchNoteVersion>(ShardVersion.UnseenNotesFor(account));
            Assert.Equal("2026.10.02", notes.Version);

            var gump = new BulletinGump(pm, ClusterFBulletinSystem.UnreadFor(account), notes);
            var from = ns.SendBuffer.GetReadSpan().Length;
            gump.OnResponse(ns, new RelayInfo(BulletinGump.AllNotesButton, ReadOnlySpan<int>.Empty,
                ReadOnlySpan<ushort>.Empty, ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

            Assert.False(SentText(ns, from, PatchNotes.NotesUrl("2026.10.02")), "the wiki was opened (cc-P64: not until launch)");
            Assert.True(pm.HasGump<PatchHistoryGump>());
            Assert.Null(ShardVersion.UnseenNotesFor(account)); // seen: not again

            // The next version shows again.
            ShardVersion.Load(Sample, "2026.10.02.2");
            Assert.Equal("2026.10.02.2", ShardVersion.UnseenNotesFor(account)?.Version);
        }
        finally
        {
            pm.NetState = null;
            ns.Mobile = null;
            pm.Delete();
            Accounts.Remove(account);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AnEmptyChangelogPostsNothingAndDoesNotCrash()
    {
        var account = new Account($"p22e{Guid.NewGuid():N}"[..16], "p22-test-only");
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        account[0] = pm;

        try
        {
            Assert.Empty(PatchNotes.Parse(""));
            Assert.Empty(PatchNotes.Parse(null));
            Assert.Empty(PatchNotes.Parse("# Shattered Legacy changelog\n\n<!--\n## 2026.10.01\n### Added\n- x\n-->\n"));

            Assert.Empty(ShardVersion.Load("", ""));
            Assert.Equal("", ShardVersion.Current);
            Assert.Null(ShardVersion.CurrentNotes);
            Assert.Null(ShardVersion.UnseenNotesFor(account));
            ClusterFBulletinSystem.OnLogin(pm); // nothing to show; must not throw

            Assert.Contains("not a date version", Assert.Single(ShardVersion.Load("", "v1.2")));
            Assert.Equal("", ShardVersion.Current);

            Assert.Contains("====== Patch notes ======", PatchNotes.RenderDokuWiki(ShardVersion.All));

            ShardVersion.Version_OnCommand(new CommandEventArgs(pm, "version", "", []));
        }
        finally
        {
            pm.Delete();
            Accounts.Remove(account);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheVersionCommandSaysTheVersionAndShowsTheNotes()
    {
        ShardVersion.Load(Sample, "");

        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.MoveToWorld(new Point3D(1251, 1250, 0), Map.Trammel);
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;

        try
        {
            var from = ns.SendBuffer.GetReadSpan().Length;
            ShardVersion.Version_OnCommand(new CommandEventArgs(pm, "version", "", []));

            Assert.True(SentText(ns, from, "Shattered Legacy 2026.10.02.2"), "the version was not said");
            Assert.True(pm.HasGump<PatchHistoryGump>());
        }
        finally
        {
            pm.NetState = null;
            ns.Mobile = null;
            pm.Delete();
        }
    }
}
