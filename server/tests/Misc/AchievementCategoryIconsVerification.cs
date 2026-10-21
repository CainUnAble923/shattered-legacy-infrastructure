// AchievementCategoryIconsVerification.cs
//
// cc-P241: each achievement category's icon (Praxiiz's game-icons.net spell icons, CC BY 3.0, gumps 22459-22468 in our
// player package) beside the category's heading in AchievementsGump. Notes in shard-migration
// notes/cc-P241-achievement-category-icons.md.
//
// Facts:
//   1. Every AchievementCategory has its own icon, a gump in the range this set registered.
//   2. A player holding achievements in every category sees every category's heading on the All tab: the icon, and its
//      name in a label beside it (so the heading reads on a client without the icon), the icon inside its own row.
//      Every category's own tab opens on its heading.
//   3. A player who has earned no joke achievement sees no Mischief heading: the hidden set stays hidden.
// The registry rows, PNGs and licenses are checked by player-package\tests\Build-UoOverrides.Tests.ps1.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class AchievementCategoryIconsVerification
{
    private const int FirstIcon = 22459;
    private const int LastIcon = 22468;

    private readonly ITestOutputHelper _out;

    public AchievementCategoryIconsVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p241{Guid.NewGuid():N}"[..16], "p241-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Name = "Icon Fixture" };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1480, 1760, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            ClusterFAccountPersistence.Get(Account)?.ClearGuildData();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static List<AchievementsGump> EveryPage(PlayerMobile pm, AchievementCategory? cat)
    {
        var pages = new List<AchievementsGump>();
        for (var i = 0; i < 80; i++)
        {
            var g = new AchievementsGump(pm, cat, false, i);
            var counter = g.Entries.OfType<GumpLabel>().Select(l => Regex.Match(l.Text, @"^(\d+)/(\d+)$"))
                .FirstOrDefault(m => m.Success);
            pages.Add(g);
            if (counter == null || counter.Groups[1].Value == counter.Groups[2].Value)
            {
                break;
            }
        }

        return pages;
    }

    private static IEnumerable<GumpImage> Icons(AchievementsGump g) =>
        g.Entries.OfType<GumpImage>().Where(i => i.GumpID is >= FirstIcon and <= LastIcon);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EveryCategoryHasItsOwnIconInTheRegisteredRange()
    {
        var cats = Enum.GetValues<AchievementCategory>();
        foreach (var cat in cats)
        {
            Assert.True(AchievementsGump.CategoryIconGumpIds.TryGetValue(cat, out var id), $"{cat} has no icon");
            Assert.InRange(id, FirstIcon, LastIcon);
        }

        Assert.Equal(cats.Length, AchievementsGump.CategoryIconGumpIds.Values.Distinct().Count());
        Assert.Equal(LastIcon - FirstIcon + 1, cats.Length); // one registered gump per category, none spare
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void APlayerWithAchievementsInEveryCategorySeesEveryHeadingWithItsIconAndName()
    {
        using var p = new Player();
        var cats = Enum.GetValues<AchievementCategory>();
        foreach (var cat in cats)
        {
            var def = ClusterFAchievementSystem.Definitions.Values
                .Where(d => d.Category == cat && !d.Retired && d.PrerequisiteKey == null)
                .OrderBy(d => d.Key)
                .First();
            Assert.True(ClusterFAchievementSystem.TryGrant(p.Account, def.Key), $"granted {def.Key}");
        }

        var seen = new HashSet<AchievementCategory>();
        var pages = EveryPage(p.Pm, null);
        foreach (var g in pages)
        {
            foreach (var icon in Icons(g))
            {
                var cat = AchievementsGump.CategoryIconGumpIds.Single(kv => kv.Value == icon.GumpID).Key;
                seen.Add(cat);

                // The name beside it, in a label of its own: the heading reads without the art.
                var name = Assert.Single(g.Entries.OfType<GumpLabel>(),
                    l => l.Y >= icon.Y && l.Y < icon.Y + AchievementsGump.CategoryIconSize && l.X > icon.X);
                Assert.Equal(AchievementsGump.CategoryName(cat), name.Text);
                Assert.True(name.X >= icon.X + AchievementsGump.CategoryIconSize, $"{cat}: the name starts under the icon");

                // The icon ends inside its own row: nothing else starts above its bottom.
                var bottom = icon.Y + AchievementsGump.CategoryIconSize;
                Assert.True(bottom <= AchievementsGump.ContentBottom, $"{cat}: the icon runs past the list");
                Assert.DoesNotContain(g.Entries.OfType<GumpHtml>(), h => h.Y > icon.Y - 2 && h.Y < bottom);
                Assert.DoesNotContain(Icons(g), i => i != icon && i.Y > icon.Y - 2 && i.Y < bottom);
            }
        }

        _out.WriteLine($"{seen.Count} headings over {pages.Count} pages of All");
        Assert.Equal(cats.OrderBy(c => c), seen.OrderBy(c => c));

        // Each category's own list opens on its heading, and holds no other category's (its Earned and Not Yet sections
        // each start with it).
        foreach (var cat in cats)
        {
            var first = new AchievementsGump(p.Pm, cat);
            var icons = Icons(first).OrderBy(i => i.Y).ToList();
            Assert.NotEmpty(icons);
            Assert.All(icons, i => Assert.Equal(AchievementsGump.CategoryIconGumpIds[cat], i.GumpID));
            var firstRow = first.Entries.OfType<GumpHtml>().Where(h => h.Y >= AchievementsGump.ListY).Min(h => h.Y);
            Assert.True(icons[0].Y < firstRow, $"{cat}: the heading comes before the first row");
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void NoMischiefHeadingUntilAJokeIsEarned()
    {
        using var p = new Player();
        var mischief = AchievementsGump.CategoryIconGumpIds[AchievementCategory.Mischief];
        foreach (var g in EveryPage(p.Pm, null))
        {
            Assert.DoesNotContain(Icons(g), i => i.GumpID == mischief);
            Assert.DoesNotContain(g.Entries.OfType<GumpLabel>(), l => l.Text == "Mischief");
        }
    }
}
