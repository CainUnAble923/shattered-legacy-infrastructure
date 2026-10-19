// UnravelContainerVerification.cs
//
// cc-P176, notes in shard-migration notes/cc-P176-cont-artificers-unravel-container.md.
//
// OSI's Unravel Container on the Artificers' Imbuing Table (ArtificersImbueGump; ServUO pub57 ImbuingGump.cs:246-365):
// target a container in the backpack, confirm, and every magic item at its top level is unravelled through the same
// path as one Full Disenchant (ArtificersImbueGump.UnravelOne). Facts:
//   1. A bag with 3 magic and 2 plain items unravels the 3 and keeps the 2; the Imbuing skill is checked 3 times.
//   2. A blessed magic item is skipped (no check, it stays); so are an insured one, a quest item, and a magic item in a
//      bag inside the bag (top level only).
//   3. The summary counts: "Unraveled: 3/5 items", "Unraveled 3 item(s), skipped 2." and the shards gained.
//   4. One code path: a blessed magic item is refused by a single Full Disenchant too (OSI 1080421), with no check.
//   5. A container outside the backpack, and the backpack itself, are refused (OSI 1062334) and nothing goes.

using System;
using System.Linq;
using System.Text;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Xunit;
using A = ShatteredLegacy.Tests.ImbueSelfRepairGateVerification;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class UnravelContainerVerification
{
    private const int BtnDisenchantTarget = 6;
    private const int BtnFullDisenchant = 7;
    private const int BtnApplyDisenchant = 8;
    private const int BtnUnravelContainerTarget = 12;
    private const int BtnApplyUnravelContainer = 13;

    public UnravelContainerVerification()
    {
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ClusterFGuildSystem.EnsureRegistered();
        ImbuingTable.Current = ImbuingTable.Defaults;
        ClusterFImbueEffects.RollOverride = null;
    }

    private static bool SentText(NetState ns, int from, string text)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        var be = Encoding.BigEndianUnicode.GetBytes(text);
        var ascii = Encoding.ASCII.GetBytes(text);
        return span.IndexOf(be) >= 0 || span.IndexOf(ascii) >= 0;
    }

    private static int Mark(NetState ns) => ns.SendBuffer.GetReadSpan().Length;

    private static Longsword MagicSword()
    {
        var sword = new Longsword();
        sword.WeaponAttributes.HitLeechHits = 30;
        return sword;
    }

    private static GoldRing MagicRing()
    {
        var ring = new GoldRing();
        ring.Attributes.Luck = 40;
        return ring;
    }

    /// <summary>Counts the Imbuing skill checks made while it runs, then puts the handler back.</summary>
    private sealed class ImbuingChecks : IDisposable
    {
        private readonly SkillCheckLocationHandler _was = Mobile.SkillCheckLocationHandler;
        public int Count;

        public ImbuingChecks() =>
            Mobile.SkillCheckLocationHandler = (from, skill, min, max) =>
            {
                if (skill == SkillName.Imbuing)
                {
                    Count++;
                }

                return _was?.Invoke(from, skill, min, max) == true;
            };

        public void Dispose() => Mobile.SkillCheckLocationHandler = _was;
    }

    /// <summary>Unravel Container on the table, target the container, and press the confirm page's button.</summary>
    private static void UnravelContainer(A.Artificer a, Container cont)
    {
        A.Press(new ArtificersImbueGump(a.Pm), a.Ns, BtnUnravelContainerTarget);
        Assert.NotNull(a.Pm.Target);

        while (a.Pm.CloseGump<ArtificersImbueGump>())
        {
        }

        a.Pm.Target.Invoke(a.Pm, cont);
        var confirm = a.Pm.FindGump<ArtificersImbueGump>();
        Assert.NotNull(confirm);
        A.Next(a, confirm, BtnApplyUnravelContainer);
    }

    // ---------------------------------------------------------------- 1, 3

    [Fact]
    public void ThreeMagicAndTwoPlainUnravelsTheThreeChecksThreeTimesAndCounts()
    {
        using var a = new A.Artificer(0);
        var bag = new Bag();
        a.Pm.Backpack.DropItem(bag);
        Item[] magic = [MagicSword(), MagicSword(), MagicRing()];
        Item[] plain = [new Dagger(), new GoldRing()];
        foreach (var i in magic.Concat(plain))
        {
            bag.DropItem(i);
        }

        var shards = a.ShardsLeft;
        var mark = Mark(a.Ns);

        using (var checks = new ImbuingChecks())
        {
            UnravelContainer(a, bag);
            Assert.Equal(3, checks.Count);
        }

        Assert.All(magic, i => Assert.True(i.Deleted));
        Assert.All(plain, i => Assert.False(i.Deleted));
        Assert.All(plain, i => Assert.Same(bag, i.Parent));
        Assert.True(a.ShardsLeft > shards);

        Assert.True(SentText(a.Ns, mark, "Unraveled: 3/5 items"));
        Assert.True(SentText(a.Ns, mark, "Unraveled 3 item(s), skipped 2."));
        Assert.True(SentText(a.Ns, mark, $"Ingredients: {a.ShardsLeft - shards} Essence Shard(s)"));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void BlessedInsuredQuestAndNestedMagicItemsAreSkipped()
    {
        using var a = new A.Artificer(0);
        var bag = new Bag();
        a.Pm.Backpack.DropItem(bag);

        var blessed = MagicSword();
        blessed.LootType = LootType.Blessed;
        var insured = MagicSword();
        insured.Insured = true;
        var quest = MagicRing();
        quest.QuestItem = true;
        var inner = new Pouch();
        var nested = MagicSword();
        inner.DropItem(nested);
        var taken = MagicSword();

        foreach (var i in new Item[] { blessed, insured, quest, inner, taken })
        {
            bag.DropItem(i);
        }

        var mark = Mark(a.Ns);
        using (var checks = new ImbuingChecks())
        {
            UnravelContainer(a, bag);
            Assert.Equal(1, checks.Count);
        }

        Assert.True(taken.Deleted);
        Assert.All(new Item[] { blessed, insured, quest, inner }, i => Assert.Same(bag, i.Parent));
        Assert.False(nested.Deleted);
        Assert.Same(inner, nested.Parent);
        Assert.True(SentText(a.Ns, mark, "Unraveled: 1/5 items"));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ASingleFullDisenchantRefusesABlessedItemToo()
    {
        using var a = new A.Artificer(0);
        var sword = MagicSword();
        sword.LootType = LootType.Blessed;
        a.Pm.Backpack.DropItem(sword);
        var shards = a.ShardsLeft;
        var mark = Mark(a.Ns);

        using (var checks = new ImbuingChecks())
        {
            A.Press(new ArtificersImbueGump(a.Pm), a.Ns, BtnDisenchantTarget);
            while (a.Pm.CloseGump<ArtificersImbueGump>())
            {
            }

            a.Pm.Target.Invoke(a.Pm, sword);
            var view = a.Pm.FindGump<ArtificersImbueGump>();
            var sure = A.Next(a, view, BtnFullDisenchant);
            A.Next(a, sure, BtnApplyDisenchant);
            Assert.Equal(0, checks.Count);
        }

        Assert.False(sword.Deleted);
        Assert.Equal(shards, a.ShardsLeft);
        Assert.True(SentText(a.Ns, mark, "You cannot unravel the magic of a blessed item."));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void AContainerOutsideTheBackpackOrTheBackpackItselfIsRefused()
    {
        using var a = new A.Artificer(0);
        var ground = new Bag();
        ground.MoveToWorld(a.Pm.Location, a.Pm.Map);
        var outside = MagicSword();
        ground.DropItem(outside);
        var loose = MagicSword();
        a.Pm.Backpack.DropItem(loose);

        foreach (var cont in new Container[] { ground, a.Pm.Backpack })
        {
            var mark = Mark(a.Ns);
            A.Press(new ArtificersImbueGump(a.Pm), a.Ns, BtnUnravelContainerTarget);
            Assert.NotNull(a.Pm.Target);
            a.Pm.Target.Invoke(a.Pm, cont);
            Assert.True(SentText(a.Ns, mark, "This item must be in your backpack to be used."), cont.GetType().Name);
        }

        Assert.False(outside.Deleted);
        Assert.False(loose.Deleted);
        ground.Delete();
    }
}
