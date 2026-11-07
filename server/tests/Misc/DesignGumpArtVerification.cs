// DesignGumpArtVerification.cs
//
// cc-P813: Chase's Claude Design gump art and the Hall of Finishers banner, on the server. Notes in shard-migration
// notes/cc-P813-gump-art-import.md. The players' copy (gump and art PNGs, registry.csv, records.json) is checked against
// this by player-package\tests\Build-UoOverrides.Tests.ps1. Every ID here is written out, not read from DesignGumpArt,
// so a constant moved to another gump fails here.
//
// Facts:
//   1. The pet choice scroll is drawn on its background 23211; Dog is button 1 on tile 23212 (pressed 23213), Cat is
//      button 2 on tile 23214 (pressed 23215), and their words are labels (html would take the tile's clicks).
//      None of the old stock arrows (4005) is left.
//   2. "Didn't Read the Sign" pops up on its panel 23216, with the title, the System's flavor and the earned line;
//      any other achievement keeps the standard popup and never shows 23216.
//   3. The League shop is drawn on the store window 23217: Titles and Hall are tab buttons (23219, pressed 23220),
//      the shop's own tab is drawn active (23220) and is no button, an open row's Buy is 23221 (pressed 23222) with
//      the word "Buy" as a label, every row has the rule 23218 under it, and no stock scroll piece (0x1F40) is left.
//   4. The Finishers banner is art 46710, not movable, never decays, and after ShatteredLegacyArt.Apply carries OSI's
//      banner 0x1586's record: "banner", ArticleA | PartialHue, weight 255, height 0.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.LeagueRewards;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class DesignGumpArtVerification
{
    public DesignGumpArtVerification()
    {
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        if (!_achievements)
        {
            ClusterFAchievementSystem.Configure();
            _achievements = true;
        }
    }

    private static bool _achievements;

    private static PlayerMobile NewCharacter(Account account)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(3460, 2604, 18), Map.Trammel);
        return pm;
    }

    private static Account NewAccount() => new($"p813{Guid.NewGuid():N}"[..16], "p813-test-only");

    private static void Cleanup(Account account)
    {
        for (var i = 0; i < account.Length; i++)
        {
            account[i]?.Delete();
        }

        account.Delete();
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ThePetScrollIsDrawnOnItsDesignWithTheTilesAsButtons()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var scroll = new PetChoiceScroll();
            pm.Backpack!.DropItem(scroll);
            var g = new PetChoiceGump(scroll);

            var images = g.Entries.OfType<GumpImage>().ToList();
            Assert.Contains(images, i => i.GumpID == 23211 && i.X == 0 && i.Y == 0);

            var buttons = g.Entries.OfType<GumpButton>().ToList();
            Assert.Equal(2, buttons.Count);
            var dog = buttons.Single(b => b.ButtonID == 1);
            var cat = buttons.Single(b => b.ButtonID == 2);
            Assert.Equal((23212, 23213), (dog.NormalID, dog.PressedID));
            Assert.Equal((23214, 23215), (cat.NormalID, cat.PressedID));
            Assert.DoesNotContain(buttons, b => b.NormalID == 4005);

            var labels = g.Entries.OfType<GumpLabel>().ToList();
            var dogLabel = labels.Single(l => l.Text == "Dog");
            var catLabel = labels.Single(l => l.Text == "Cat");
            // Each word lies on its own tile (143 x 96).
            Assert.InRange(dogLabel.X, dog.X, dog.X + 143);
            Assert.InRange(dogLabel.Y, dog.Y, dog.Y + 96);
            Assert.InRange(catLabel.X, cat.X, cat.X + 143);
            Assert.InRange(catLabel.Y, cat.Y, cat.Y + 96);

            // No html over a tile (it would take the tile's clicks).
            foreach (var h in g.Entries.OfType<GumpHtml>())
            {
                foreach (var b in buttons)
                {
                    var overlaps = h.X < b.X + 143 && b.X < h.X + h.Width && h.Y < b.Y + 96 && b.Y < h.Y + h.Height;
                    Assert.False(overlaps, $"html at {h.X},{h.Y} {h.Width}x{h.Height} covers button {b.ButtonID}");
                }
            }

            var html = string.Join(" ", g.Entries.OfType<GumpHtml>().Select(h => h.Text));
            Assert.Contains("A companion for the road", html);
            Assert.Contains("the scroll is used up either way", html);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheMoongateSkipPopupIsDrawnOnItsPanelAndOthersAreNot()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var skip = ClusterFAchievementSystem.Definitions["tutorial.gateSkip"];
            Assert.Equal("Didn't Read the Sign", skip.Title);
            var popup = new AchievementEarnedGump(pm, skip);
            Assert.Contains(popup.Entries.OfType<GumpImage>(), i => i.GumpID == 23216 && i.X == 0 && i.Y == 0);
            var html = string.Join(" ", popup.Entries.OfType<GumpHtml>().Select(h => h.Text));
            Assert.Contains("ACHIEVEMENT UNLOCKED", html);
            Assert.Contains(skip.Title, html);
            Assert.Contains(skip.FlavorText, html);
            Assert.Contains(skip.EarnedLine, html);
            Assert.Contains($"+{skip.AP} AP", html);
            Assert.Empty(popup.Entries.OfType<GumpBackground>());

            var other = ClusterFAchievementSystem.Definitions["league.registered_citizen"];
            var standard = new AchievementEarnedGump(pm, other);
            Assert.DoesNotContain(standard.Entries.OfType<GumpImage>(), i => i.GumpID == 23216);
            Assert.NotEmpty(standard.Entries.OfType<GumpBackground>());
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheLeagueShopIsDrawnOnTheStoreWindow()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);
        try
        {
            var data = LeagueShop.DataOf(pm)!;
            data.AchievementPoints = 250;
            data.Renown = 100;

            var shop = new LeagueShopGump(pm);
            var images = shop.Entries.OfType<GumpImage>().ToList();
            var buttons = shop.Entries.OfType<GumpButton>().ToList();
            var labels = shop.Entries.OfType<GumpLabel>().ToList();

            Assert.Contains(images, i => i.GumpID == 23217 && i.X == 0 && i.Y == 0);
            Assert.DoesNotContain(images, i => i.GumpID == 0x1F40);

            var titles = buttons.Single(b => b.ButtonID == LeagueShopGump.BtnTitles);
            var hall = buttons.Single(b => b.ButtonID == LeagueShopGump.BtnHall);
            Assert.Equal((23219, 23220), (titles.NormalID, titles.PressedID));
            Assert.Equal((23219, 23220), (hall.NormalID, hall.PressedID));
            Assert.Contains(images, i => i.GumpID == 23220);
            Assert.Contains(labels, l => l.Text == "Shop");
            Assert.Contains(labels, l => l.Text == "Titles");
            Assert.Contains(labels, l => l.Text == "Hall");

            var rows = buttons.Where(b => b.ButtonID >= LeagueShopGump.BtnRowBase).ToList();
            Assert.NotEmpty(rows);
            Assert.All(rows, b => Assert.Equal((23221, 23222), (b.NormalID, b.PressedID)));
            Assert.Equal(rows.Count, labels.Count(l => l.Text == "Buy"));

            // One rule under each row shown, on every page.
            var rules = shop.Entries.OfType<GumpImageTiled>().Count(t => t.GumpID == 23218);
            Assert.Equal(LeagueShop.Stock.Length, rules);

            // The paging buttons are tabs too, and every piece of the window stays inside its 560 x 420.
            Assert.All(buttons.Where(b => b.Type == GumpButtonType.Page), b => Assert.Equal(23219, b.NormalID));
            Assert.All(buttons, b => Assert.InRange(b.X, 0, 560 - 72));
            Assert.All(buttons, b => Assert.InRange(b.Y, 0, 420 - 28));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheFinishersBannerIsDecorWithOsisBannerRecord()
    {
        ShatteredLegacyArt.Apply();
        var banner = new FinishersBanner();
        try
        {
            Assert.Equal(46710, banner.ItemID);
            Assert.False(banner.Movable);
            Assert.False(banner.Decays);

            var tile = TileData.ItemTable[46710];
            Assert.Equal("banner", tile.Name);
            Assert.Equal(TileFlag.ArticleA | TileFlag.PartialHue, tile.Flags);
            Assert.Equal(255, tile.Weight);
            Assert.Equal(0, tile.Height);
        }
        finally
        {
            banner.Delete();
        }
    }
}
