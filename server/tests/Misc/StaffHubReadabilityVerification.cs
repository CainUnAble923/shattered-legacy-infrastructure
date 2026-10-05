// StaffHubReadabilityVerification.cs
//
// cc-P52 Part F (bug-list D67, Chase 2026-10-04 screenshots and requests). The Staff Hub (cc-P51) was hard to read and
// gained two Travel controls:
//   F1. The content panel was an alpha region (the paperdoll and world showed through the text). It is now the solid
//       black tile 2624 over the same rectangle, with no alpha region on any tab.
//   F2. Command rows read "Re-run: Skips . Shard: Any". Now "Re-run: Skips. Shard: Any." and no label has " .".
//   F3. Player tab values were cropped at 176 pixels ("Guild standing: Miners' Compact: Apprentice..."). The rows are
//       one wrapped HTML block that carries every value whole; it scrolls rather than crop if it ever outgrows its room.
//   F4. Travel: the same x, y on another facet, z as pinned's [Go x y picks it (map.GetAverageZ, Handlers.cs:537);
//       refused in one line, with no move, where the point is outside that facet; the current facet has no button.
//   F5. Travel: Green Acres, Felucca 5445, 1153, 0, from pinned's go-locations list (felucca.json:221); Go lands there.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class StaffHubReadabilityVerification : IDisposable
{
    private readonly ITestOutputHelper _out;
    private readonly List<Account> _accounts = [];

    public StaffHubReadabilityVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        // The test host runs no Configure of ours: register the hub and the grants it lists (each is idempotent).
        ClusterFStaffHub.Configure();
        ClusterFCompactAdminTools.Configure();
    }

    public void Dispose()
    {
        foreach (var account in _accounts)
        {
            for (var i = 0; i < account.Length; i++)
            {
                if (account[i] is { } m)
                {
                    if (m.NetState is { } ns)
                    {
                        m.NetState = null;
                        ns.Mobile = null;
                        ns.Dispose();
                    }

                    ClusterFAccountPersistence.Get(account)?.ClearGuildData();
                    m.Delete();
                }
            }

            Accounts.Remove(account);
        }
    }

    private PlayerMobile NewCharacter(AccessLevel level = AccessLevel.Player, string name = null)
    {
        var account = new Account($"p52f{Guid.NewGuid():N}"[..16], "p52-test-only");
        _accounts.Add(account);
        var pm = new PlayerMobile
        {
            Player = true, Race = Race.Human, AccessLevel = level, Name = name ?? $"P52 {Guid.NewGuid():N}"[..12]
        };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(3460, 2604, 18), Map.Trammel);

        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        ns.Account = account;
        return pm;
    }

    private static StaffHubGump Hub(Mobile staff, StaffHubState state) => new(staff, state);

    private static IEnumerable<int> Buttons(Gump g) => g.Entries.OfType<GumpButton>().Select(b => b.ButtonID);

    private static IEnumerable<string> LabelTexts(Gump g) =>
        g.Entries.OfType<GumpLabel>().Select(l => l.Text).Concat(g.Entries.OfType<GumpLabelCropped>().Select(l => l.Text));

    private IEnumerable<(string Name, StaffHubGump Gump)> EveryTab(Mobile staff, Mobile player)
    {
        foreach (var cat in ClusterFStaffHub.Categories)
        {
            yield return ($"Commands/{cat}", Hub(staff, new StaffHubState { Category = cat }));
        }

        yield return ("Player/none", Hub(staff, new StaffHubState { Tab = StaffHubTab.Player }));
        yield return ("Player/selected", Hub(staff, new StaffHubState { Tab = StaffHubTab.Player, Selected = player }));
        yield return ("Travel/places", Hub(staff, new StaffHubState { Tab = StaffHubTab.Travel }));
        yield return ("Travel/halls", Hub(staff, new StaffHubState { Tab = StaffHubTab.Travel, ShowGuildHalls = true }));
    }

    // ---------------------------------------------------------------- F1

    [Fact]
    public void TheContentPanelIsSolidOnEveryTab()
    {
        var admin = NewCharacter(AccessLevel.Administrator);
        var player = NewCharacter();

        foreach (var (name, g) in EveryTab(admin, player))
        {
            Assert.Empty(g.Entries.OfType<GumpAlphaRegion>());
            var panel = g.Entries.OfType<GumpImageTiled>().FirstOrDefault(t => t.GumpID == StaffHubGump.PanelTile);
            Assert.NotNull(panel);
            Assert.Equal((16, 74, 528, 324), (panel.X, panel.Y, panel.Width, panel.Height));
            _out.WriteLine($"{name}: panel tile {panel.GumpID} at {panel.X},{panel.Y} {panel.Width}x{panel.Height}, no alpha region");
        }
    }

    // ---------------------------------------------------------------- F2

    [Fact]
    public void CommandRowsReadAsTwoSentences()
    {
        var admin = NewCharacter(AccessLevel.Administrator);
        var rows = 0;

        foreach (var cat in ClusterFStaffHub.Categories)
        {
            var g = Hub(admin, new StaffHubState { Category = cat });
            var labels = LabelTexts(g).ToList();
            Assert.DoesNotContain(labels, t => t.Contains(" ."));

            foreach (var meta in labels.Where(t => t.StartsWith("Re-run:")))
            {
                rows++;
                Assert.Matches(@"^Re-run: \w+\. Shard: \w+\.$", meta);
            }
        }

        var any = ClusterFStaffHub.AllCommands().FirstOrDefault();
        Assert.NotNull(any);
        _out.WriteLine($"{rows} command rows checked; for example: {ClusterFStaffHub.CommandMeta(any)}");
        Assert.True(rows > 0);
        Assert.Equal($"Re-run: {any.Declaration.Rerun}. Shard: {any.Declaration.Shard}.", ClusterFStaffHub.CommandMeta(any));
    }

    // ---------------------------------------------------------------- F3

    [Fact]
    public void PlayerValuesAreWholeAndWrapped()
    {
        var gm = NewCharacter(AccessLevel.GameMaster);
        var pm = NewCharacter(name: "Long Values Fixture");
        var acct = (Account)pm.Account;
        var guild = ClusterFAccountPersistence.GetOrCreateGuild(pm);
        guild.JoinedGuilds.Add("mining");
        guild.JoinedGuilds.Add("smithing");
        guild.JoinedGuilds.Add("foresters");
        guild.GuildReputation["mining"] = 1_500;
        foreach (var key in new[]
                 {
                     "legacy.jacobs_pickaxe", "legacy.hammer_of_hephaestus", "legacy.reinforced_hammer_of_hephaestus",
                     "legacy.compact_ore_satchel", "legacy.foresters_logbook"
                 })
        {
            ClusterFRestorationRegistry.Unlock(acct, key, "test");
        }

        var g = Hub(gm, new StaffHubState { Tab = StaffHubTab.Player, Selected = pm });
        var rows = ClusterFStaffHub.PlayerRows(pm);
        var block = Assert.Single(g.Entries.OfType<GumpHtml>(), h => h.Text.Contains("Guild standing"));

        ClusterFStaffHub.PlayerRowsHtml(rows, StaffHubGump.PlayerRowsW - 20, out var lines);
        _out.WriteLine($"rows block at {block.X},{block.Y} {block.Width}x{block.Height}, ~{lines} lines, scrollbar {block.Scrollbar}");

        var cropped = g.Entries.OfType<GumpLabelCropped>().Select(l => l.Text).ToList();
        foreach (var r in rows)
        {
            _out.WriteLine($"  {r.Label}: {r.Value} ({r.Value.Length} chars)");
            Assert.Contains(ClusterFStaffHub.Html(r.Value), block.Text);
            Assert.DoesNotContain(r.Value, cropped);
        }

        // The two values Chase saw cut off are longer than the old 176-pixel column, and get a second line.
        var standing = rows.Single(r => r.Label == "Guild standing");
        var caps = rows.Single(r => r.Label == "Skill / stat caps");
        Assert.True(standing.Value.Length * ClusterFStaffHub.CharPixels > 176, standing.Value);
        Assert.True(caps.Value.Length * ClusterFStaffHub.CharPixels > 176, caps.Value);
        Assert.Contains(" and 2 more", standing.Value);

        // Either it fits, or it scrolls; it never crops. The block clears the placeholder line and the Actions column.
        Assert.Equal(lines * StaffHubGump.LineHeight > block.Height, block.Scrollbar);
        Assert.True(block.Y + block.Height <= 374);
        Assert.True(block.X + block.Width <= 340);
    }

    // ---------------------------------------------------------------- F4

    [Fact]
    public void ChangeFacetKeepsXAndYAndRefusesOutsideTheFacet()
    {
        var gm = NewCharacter(AccessLevel.GameMaster);
        var facets = ClusterFStaffHub.ChangeFacets;
        Assert.Equal(new[] { "Felucca", "Trammel", "Ilshenar", "Malas", "Tokuno", "TerMur" }, facets.Select(f => f.Name));

        gm.MoveToWorld(new Point3D(1500, 1610, 0), Map.Trammel);
        var g = Hub(gm, new StaffHubState { Tab = StaffHubTab.Travel });
        var here = Array.IndexOf(facets, Map.Trammel);
        Assert.DoesNotContain(StaffHubGump.BtnFacetBase + here, Buttons(g));
        for (var i = 0; i < facets.Length; i++)
        {
            if (i != here)
            {
                Assert.Contains(StaffHubGump.BtnFacetBase + i, Buttons(g));
            }
        }

        // Trammel to Felucca: same x and y, z as [Go x y.
        g.Respond(gm, StaffHubGump.BtnFacetBase + Array.IndexOf(facets, Map.Felucca));
        _out.WriteLine($"to Felucca: {gm.Location} on {gm.Map}");
        Assert.Same(Map.Felucca, gm.Map);
        Assert.Equal(new Point3D(1500, 1610, Map.Felucca.GetAverageZ(1500, 1610)), gm.Location);
        Assert.Equal("Felucca 1500, 1610", ClusterFStaffHubSpots.RecentOf(gm)[0].Name);

        // Outside the target: Ilshenar is 2304 x 1600, so 3000, 3000 is off it. One line, no move.
        gm.MoveToWorld(new Point3D(3000, 3000, 0), Map.Trammel);
        Assert.False(ClusterFStaffHub.ChangeFacet(gm, Map.Ilshenar, out var error));
        _out.WriteLine($"to Ilshenar from 3000, 3000: {error}");
        Assert.Contains("outside Ilshenar", error);
        Assert.DoesNotContain("\n", error);
        Hub(gm, new StaffHubState { Tab = StaffHubTab.Travel })
            .Respond(gm, StaffHubGump.BtnFacetBase + Array.IndexOf(facets, Map.Ilshenar));
        Assert.Same(Map.Trammel, gm.Map);
        Assert.Equal(new Point3D(3000, 3000, 0), gm.Location);

        // The same facet, and a stale press for it, change nothing.
        Assert.False(ClusterFStaffHub.ChangeFacet(gm, Map.Trammel, out error));
        Assert.Contains("already on Trammel", error);

        // Every facet's bounds, at its far corner and one past it.
        gm.MoveToWorld(new Point3D(100, 100, 0), Map.Trammel);
        foreach (var f in facets.Where(f => f != Map.Trammel))
        {
            Assert.True(f.Width > 100 && f.Height > 100);
            Assert.True(ClusterFStaffHub.ChangeFacet(gm, f, out error), error);
            Assert.Same(f, gm.Map);
            Assert.Equal((100, 100), (gm.X, gm.Y));
            gm.MoveToWorld(new Point3D(100, 100, 0), Map.Trammel);
        }
    }

    // ---------------------------------------------------------------- F5

    [Fact]
    public void GreenAcresIsAShardPlace()
    {
        var places = ClusterFStaffHub.ShardPlaces();
        var green = Assert.Single(places, p => p.Name == "Green Acres");
        _out.WriteLine($"{green.Name}: {green.Facet} {green.Location}");
        Assert.Same(Map.Felucca, green.Map);
        Assert.Equal(new Point3D(5445, 1153, 0), green.Location);
        Assert.True(green.Available);

        var gm = NewCharacter(AccessLevel.GameMaster);
        var g = Hub(gm, new StaffHubState { Tab = StaffHubTab.Travel });
        var row = g.Places.ToList().FindIndex(p => p.Name == "Green Acres");
        Assert.True(row >= 0);
        Assert.Contains(StaffHubGump.BtnPlaceBase + row, Buttons(g));
        g.Respond(gm, StaffHubGump.BtnPlaceBase + row);
        Assert.Same(Map.Felucca, gm.Map);
        Assert.Equal(new Point3D(5445, 1153, 0), gm.Location);
    }
}
