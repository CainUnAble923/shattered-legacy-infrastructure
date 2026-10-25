// cc-P610 (P533 U2, P494 #1, #2, #4, #5): Unravel Container destroys exactly what its confirmation page showed, and
// never what the player protected. Notes: shard-migration notes/cc-P610-fix-drafts-from-p533.md. The shard recovery
// formula (P494 #3, P533 U2 part 4) waits on Chase's call (P533 C4), so the yields here are today's.
//
// Facts:
//   1. A magic item put in the bag after the page opened survives the approval; a swapped-in item (same count) survives;
//      an approved item moved out of the bag is not destroyed where it went (#1).
//   2. A personal blessing (BlessedFor) is refused by Full Disenchant and skipped by Unravel Container (#2).
//   3. An outer container locked after the page opened (or before) protects the bag inside it; a protection added after
//      the page opened protects that item; pressing the approval again does nothing more (#4, #5).
//   4. The yield is exact: the mixed bag's three magic items give 8 shards, with an essence for each property when the
//      25% roll passes and none when it fails; with a full backpack the essences go where delivery has always put them
//      (the satchel, else at the player's feet) (#5).

using System;
using System.Linq;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Xunit;
using A = ShatteredLegacy.Tests.ImbueSelfRepairGateVerification;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class UnravelApprovalVerification
{
    private const int BtnDisenchantTarget = 6;
    private const int BtnFullDisenchant = 7;
    private const int BtnApplyDisenchant = 8;
    private const int BtnUnravelContainerTarget = 12;
    private const int BtnApplyUnravelContainer = 13;

    public UnravelApprovalVerification()
    {
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ClusterFGuildSystem.EnsureRegistered();
        ImbuingTable.Current = ImbuingTable.Defaults;
        ClusterFImbueEffects.RollOverride = null;
    }

    private sealed class ConstantRandom : System.Random
    {
        public double Value;

        protected override double Sample() => Value;
        public override double NextDouble() => Value;
        public override int Next() => (int)(Value * int.MaxValue);
        public override int Next(int maxValue) => (int)(Value * maxValue);
        public override int Next(int minValue, int maxValue) => minValue + (int)(Value * (maxValue - minValue));
        public override long NextInt64(long maxValue) => (long)(Value * maxValue);
    }

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

    /// <summary>Unravel Container, target the container: the confirmation page, not yet approved.</summary>
    private static ArtificersImbueGump Preview(A.Artificer a, Container cont)
    {
        A.Press(new ArtificersImbueGump(a.Pm), a.Ns, BtnUnravelContainerTarget);
        while (a.Pm.CloseGump<ArtificersImbueGump>())
        {
        }

        a.Pm.Target.Invoke(a.Pm, cont);
        var confirm = a.Pm.FindGump<ArtificersImbueGump>();
        Assert.NotNull(confirm);
        return confirm;
    }

    private static void Approve(A.Artificer a, ArtificersImbueGump confirm) => A.Press(confirm, a.Ns, BtnApplyUnravelContainer);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void AnItemAddedAfterThePageOpenedSurvivesTheApproval()
    {
        using var a = new A.Artificer(0);
        var bag = new Bag();
        a.Pm.Backpack.DropItem(bag);
        var shown = MagicSword();
        bag.DropItem(shown);

        var confirm = Preview(a, bag); // "1 item(s) will be DESTROYED"
        var late = MagicRing();
        bag.DropItem(late);
        Approve(a, confirm);

        Assert.True(shown.Deleted);
        Assert.False(late.Deleted, "an item the page never showed is not destroyed");
        Assert.Same(bag, late.Parent);
    }

    [Fact]
    public void ASwappedItemSurvivesAndTheApprovedOneIsNotDestroyedWhereItWent()
    {
        using var a = new A.Artificer(0);
        var bag = new Bag();
        a.Pm.Backpack.DropItem(bag);
        var shown = MagicSword();
        bag.DropItem(shown);

        var confirm = Preview(a, bag);
        a.Pm.Backpack.DropItem(shown); // moved out of the bag
        var swapped = MagicSword();
        bag.DropItem(swapped); // the same count in the bag
        Approve(a, confirm);

        Assert.False(swapped.Deleted, "the swapped-in item was never approved");
        Assert.False(shown.Deleted, "the approved item left the bag, so it stays where it went");
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void APersonalBlessingIsRefusedAloneAndSkippedInABag()
    {
        using var a = new A.Artificer(0);
        var single = MagicSword();
        single.BlessedFor = a.Pm;
        a.Pm.Backpack.DropItem(single);

        A.Press(new ArtificersImbueGump(a.Pm), a.Ns, BtnDisenchantTarget);
        while (a.Pm.CloseGump<ArtificersImbueGump>())
        {
        }

        a.Pm.Target.Invoke(a.Pm, single);
        var view = a.Pm.FindGump<ArtificersImbueGump>();
        var sure = A.Next(a, view, BtnFullDisenchant);
        A.Next(a, sure, BtnApplyDisenchant);
        Assert.False(single.Deleted, "Full Disenchant refuses a personally blessed item");

        var bag = new Bag();
        a.Pm.Backpack.DropItem(bag);
        var inBag = MagicRing();
        inBag.BlessedFor = a.Pm;
        bag.DropItem(inBag);
        var plainMagic = MagicSword();
        bag.DropItem(plainMagic);
        Approve(a, Preview(a, bag));
        Assert.False(inBag.Deleted, "Unravel Container skips a personally blessed item");
        Assert.True(plainMagic.Deleted);
    }

    // ---------------------------------------------------------------- 3

    [Theory]
    [InlineData(true)]  // locked after the page opened
    [InlineData(false)] // locked before
    public void AnOuterChestLockedProtectsTheBagInsideIt(bool afterPreview)
    {
        using var a = new A.Artificer(0);
        var chest = new WoodenChest();
        a.Pm.Backpack.DropItem(chest);
        var bag = new Bag();
        chest.DropItem(bag);
        var sword = MagicSword();
        bag.DropItem(sword);

        if (!afterPreview)
        {
            chest.Locked = true;
        }

        var confirm = Preview(a, bag);
        chest.Locked = true;
        Approve(a, confirm);
        Assert.False(sword.Deleted, "nothing inside a locked chest is unravelled");
    }

    [Fact]
    public void AProtectionAddedAfterThePageOpenedHoldsAndASecondPressDoesNothingMore()
    {
        using var a = new A.Artificer(0);
        var bag = new Bag();
        a.Pm.Backpack.DropItem(bag);
        var kept = MagicSword();
        var taken = MagicSword();
        bag.DropItem(kept);
        bag.DropItem(taken);

        var confirm = Preview(a, bag);
        kept.Insured = true; // insured after the page opened
        var shards = a.ShardsLeft;
        Approve(a, confirm);
        var once = a.ShardsLeft - shards;
        Assert.False(kept.Deleted);
        Assert.True(taken.Deleted);

        Approve(a, confirm); // the same answer again
        Assert.Equal(once, a.ShardsLeft - shards);
        Assert.False(kept.Deleted);
    }

    // ---------------------------------------------------------------- 4

    private static int Essences(A.Artificer a) =>
        World.Items.Values.Count(i => i is PropertyEssence { Deleted: false } e && (e.RootParent == a.Pm || e.Parent == null && e.InRange(a.Pm.Location, 1)));

    [Theory]
    [InlineData(0.0, 3, false)] // every 25% roll passes: one essence per property
    [InlineData(0.9, 0, false)] // every roll fails
    [InlineData(0.0, 3, true)]  // a full backpack: delivered where delivery has always put them
    public void TheMixedBagsYieldIsExactlyEightShardsAndItsEssences(double roll, int essences, bool fullPack)
    {
        using var a = new A.Artificer(0);
        var bag = new Bag();
        a.Pm.Backpack.DropItem(bag);
        Item[] magic = [MagicSword(), MagicSword(), MagicRing()];
        foreach (var i in magic.Concat(new Item[] { new Dagger(), new GoldRing() }))
        {
            bag.DropItem(i);
        }

        if (fullPack)
        {
            var pack = a.Pm.Backpack;
            while (pack.TotalItems < pack.MaxItems)
            {
                pack.DropItem(new Item(0x1F14) { Stackable = false });
            }
        }

        var shards = a.ShardsLeft;
        var before = Essences(a);
        var confirm = Preview(a, bag);
        BuiltInRng.Generator = new ConstantRandom { Value = roll };
        try
        {
            Approve(a, confirm);
        }
        finally
        {
            BuiltInRng.Reset();
        }

        Assert.All(magic, i => Assert.True(i.Deleted));
        Assert.Equal(8, a.ShardsLeft - shards); // 2 x Hit Life Leech (3 each) + Luck (2): today's formula, P494's fixture
        Assert.Equal(essences, Essences(a) - before);
    }
}
