// SmithSealCatalogVerification.cs
//
// cc-P23, D43: the Smith Seal catalog sold Blacksmithing power scrolls at 305 to 320, and
// ClusterFSkillCaps.Apply undid every one at the buyer's next login. The tab is gone until F-12 turns
// power scrolls into level scrolls (notes/f12-levels-loops-caps.md), and SmithSealScrollCensus counts
// what was sold so Chase can decide on refunds. Notes in notes/cc-P23-live-defect-sweep.md.
//
// Facts (the brief's numbering):
//   1. The catalog offers no Blacksmithing power scroll, and no button id is left dangling: pressing
//      every id from 0 to 300 in every category, with seals to spare, never hands one over or throws.
//   2. The census reports the right numbers in a world seeded with a known number of scrolls.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;
using Spot = Server.SmithSealScrollCensus.Where;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SmithSealCatalogVerification
{
    private readonly ITestOutputHelper _out;

    public SmithSealCatalogVerification(ITestOutputHelper output)
    {
        _out = output;
        EnsureStartupHooks();
    }

    // ---------------------------------------------------------------- helpers

    private static bool _startupHooksRun;

    // As GuildStarterPathVerification: new Account(...) needs Accounts.Configure and a password algorithm.
    private static void EnsureStartupHooks()
    {
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

    private static Account NewAccount() => new($"p23{Guid.NewGuid():N}"[..16], "p23-test-only");

    private static PlayerMobile NewCharacter(Account account, int slot)
    {
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        account[slot] = pm;
        pm.MoveToWorld(new Point3D(1300, 1300, 0), Map.Trammel);
        return pm;
    }

    private static void Cleanup(Account account)
    {
        for (var i = 0; i < account.Length; i++)
        {
            if (account[i] is PlayerMobile pm)
            {
                var ns = pm.NetState;
                pm.NetState = null;

                if (ns != null)
                {
                    ns.Mobile = null;
                    ns.Dispose();
                }

                ClusterFAccountPersistence.Get(account)?.ClearGuildData();
                pm.Delete();
            }
        }

        Accounts.Remove(account);
    }

    private static void Press(Gump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheCatalogOffersNoPowerScrollAndLeavesNoButtonDangling()
    {
        var account = NewAccount();
        var pm = NewCharacter(account, 0);
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;

        var guild = ClusterFAccountPersistence.GetOrCreate(account).GetOrCreateGuildData(pm.Serial);
        var before = SmithSealScrollCensus.Take().Scrolls.Count;

        try
        {
            // Every category the sidebar renders, by its own button ids.
            var first = new SmithSealCatalogGump(pm);
            var categories = first.Entries.OfType<GumpButton>()
                .Where(b => b.Type == GumpButtonType.Reply && b.ButtonID is > 0 and < 100)
                .Select(b => b.ButtonID)
                .ToList();
            _out.WriteLine($"category buttons rendered: {string.Join(", ", categories)}");

            foreach (var category in categories)
            {
                var gump = new SmithSealCatalogGump(pm, (SmithSealCatalogGump.Cat)(category - 1));
                var labels = gump.Entries.OfType<GumpLabel>().Select(l => l.Text).ToList();
                Assert.DoesNotContain(labels, t => t.Contains("Power Scroll", StringComparison.OrdinalIgnoreCase));
            }

            // Every button id 0 to 300 pressed on every category the enum names, rendered or not, with
            // seals to spare. Nothing throws, and no power scroll is ever handed over.
            foreach (var cat in Enum.GetValues<SmithSealCatalogGump.Cat>())
            {
                for (var id = 0; id <= 300; id++)
                {
                    guild.AddCurrency("smithing", 100_000);
                    Press(new SmithSealCatalogGump(pm, cat), ns, id);
                }
            }

            // And the category ids past the sidebar's own, on the first category.
            for (var id = categories.Max() + 1; id < 100; id++)
            {
                Press(first, ns, id);
            }

            // Counted world-wide: a full backpack would drop a purchase at the buyer's feet.
            var handed = SmithSealScrollCensus.Take().Scrolls.Count - before;
            _out.WriteLine($"Blacksmithing power scrolls handed over: {handed}");
            Assert.Equal(0, handed);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheCensusCountsASeededWorld()
    {
        var before = SmithSealScrollCensus.Take();

        var a = NewAccount();
        var b = NewAccount();
        var c = NewAccount();
        var holder = NewCharacter(a, 0);
        var second = NewCharacter(a, 1);
        var other = NewCharacter(b, 0);
        NewCharacter(c, 0); // no seals at all

        var loose = new List<Item>();

        try
        {
            // Two in a backpack, one of them in a pouch; one in a bank; one in a chest on the ground;
            // one loose on Trammel; one on Map.Internal. None on another mobile.
            holder.Backpack.DropItem(new PowerScroll(SkillName.Blacksmith, 305.0));
            var pouch = new Pouch();
            holder.Backpack.DropItem(pouch);
            pouch.DropItem(new PowerScroll(SkillName.Blacksmith, 320.0));
            holder.BankBox.DropItem(new PowerScroll(SkillName.Blacksmith, 310.0));

            var chest = new WoodenChest();
            chest.MoveToWorld(new Point3D(1310, 1300, 0), Map.Trammel);
            chest.DropItem(new PowerScroll(SkillName.Blacksmith, 315.0));
            loose.Add(chest);

            var onGround = new PowerScroll(SkillName.Blacksmith, 305.0);
            onGround.MoveToWorld(new Point3D(1311, 1300, 0), Map.Trammel);
            loose.Add(onGround);

            var stashed = new PowerScroll(SkillName.Blacksmith, 310.0); // never moved: Map.Internal
            loose.Add(stashed);

            // A stock 110 Blacksmithing scroll (counted, not as a catalog one), and a Tailoring 105 (not counted).
            other.Backpack.DropItem(new PowerScroll(SkillName.Blacksmith, 110.0));
            other.Backpack.DropItem(new PowerScroll(SkillName.Tailoring, 105.0));

            // Seals: two characters on account a, one on b, none on c.
            ClusterFAccountPersistence.GetOrCreate(a).GetOrCreateGuildData(holder.Serial).AddCurrency("smithing", 750);
            ClusterFAccountPersistence.GetOrCreate(a).GetOrCreateGuildData(second.Serial).AddCurrency("smithing", 50);
            ClusterFAccountPersistence.GetOrCreate(b).GetOrCreateGuildData(other.Serial).AddCurrency("smithing", 1);
            ClusterFAccountPersistence.GetOrCreate(c).GetOrCreateGuildData(c[0].Serial).AddCurrency("tailoring", 99);

            // One character used a 305 since the last Apply: Blacksmithing cap above the rest.
            for (var i = 0; i < holder.Skills.Length; i++)
            {
                holder.Skills[i].Cap = 300.0;
            }

            holder.Skills.Blacksmith.Cap = 305.0;

            var after = SmithSealScrollCensus.Take();

            foreach (var line in SmithSealScrollCensus.Describe(after))
            {
                _out.WriteLine(line);
            }

            Assert.Equal(7, after.Scrolls.Count - before.Scrolls.Count);
            Assert.Equal(6, after.CatalogScrolls - before.CatalogScrolls);
            Assert.Equal(1, after.StockScrolls - before.StockScrolls);
            Assert.Equal(3, after.Count(Spot.PlayerBackpack) - before.Count(Spot.PlayerBackpack));
            Assert.Equal(2, after.CatalogCount(Spot.PlayerBackpack) - before.CatalogCount(Spot.PlayerBackpack));
            Assert.Equal(1, after.Count(Spot.PlayerBank) - before.Count(Spot.PlayerBank));
            Assert.Equal(1, after.Count(Spot.WorldContainer) - before.Count(Spot.WorldContainer));
            Assert.Equal(1, after.Count(Spot.LooseOnMap) - before.Count(Spot.LooseOnMap));
            Assert.Equal(1, after.Count(Spot.Internal) - before.Count(Spot.Internal));
            Assert.Equal(0, after.Count(Spot.OtherMobile) - before.Count(Spot.OtherMobile));

            Assert.Equal(2, after.AccountsWithSeals - before.AccountsWithSeals);
            Assert.Equal(3, after.CharactersWithSeals - before.CharactersWithSeals);
            Assert.Equal(801, after.TotalSeals - before.TotalSeals);

            Assert.Equal(1, after.RaisedCaps.Count - before.RaisedCaps.Count);
            Assert.Contains(after.RaisedCaps, l => l.Contains(holder.Serial.ToString()));
        }
        finally
        {
            foreach (var item in loose)
            {
                item.Delete();
            }

            Cleanup(a);
            Cleanup(b);
            Cleanup(c);
        }
    }
}
