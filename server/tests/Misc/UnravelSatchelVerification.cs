// UnravelSatchelVerification.cs
//
// cc-P137 sections 5 and 6, notes in shard-migration notes/cc-P137-usable-ankhs-and-playtest-fixes.md.
//
// Section 5 (bug, Chase 2026-10-16): a full disenchant of an item with no magic paid one Essence Shard
// (ArtificersImbueGump.HandleFullDisenchant, "bare minimum for unenchanted items"), so vendor daggers farmed the
// Artificers' currency. OSI refuses to unravel an item with no magic. Facts:
//   1. Targeting a plain item to disenchant refuses with "There is no magic in this to unravel." before any page:
//      no shard, no skill gain, the item kept.
//   2. A view left open after the last property was extracted cannot pay either: its full-disenchant button and an
//      apply pressed on it refuse the same way; no shard, no skill gain, the item kept. A real disenchant still pays
//      and gains.
//
// Section 6 (F-50, ours, Chase 2026-10-16): what Unravel produces goes straight into the Artificers' Essence Satchel
// when one is in the backpack, top level or nested. Facts:
//   3. Extracting a property puts its essence in a nested satchel.
//   4. A full disenchant puts its essences in the satchel.
//   5. Picking up magical residue, enchanted essence or a relic fragment from the ground or a corpse puts it in the
//      satchel; moving one from the bank or within the pack does not, and crafting an essence leaves it in the pack.
//   6. A full satchel sends the item to the backpack as stock with a message; nothing is lost.

using System;
using System.Linq;
using System.Text;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Random;
using Xunit;
using A = ShatteredLegacy.Tests.ImbueSelfRepairGateVerification;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class UnravelSatchelVerification
{
    private const int BtnDisenchantTarget = 6;
    private const int BtnFullDisenchant = 7;
    private const int BtnApplyDisenchant = 8;
    private const int BtnApplyExtract = 9;
    private const int BtnExtractBase = 200;
    private const string NoMagic = "There is no magic in this to unravel.";

    public UnravelSatchelVerification()
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

    private static bool SentText(NetState ns, int from, string text)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        var be = Encoding.BigEndianUnicode.GetBytes(text);
        var ascii = Encoding.ASCII.GetBytes(text);
        return span.IndexOf(be) >= 0 || span.IndexOf(ascii) >= 0;
    }

    private static int Mark(NetState ns) => ns.SendBuffer.GetReadSpan().Length;

    private static ArtificersImbueGump TargetForDisenchant(A.Artificer a, Item item)
    {
        A.Press(new ArtificersImbueGump(a.Pm), a.Ns, BtnDisenchantTarget);
        while (a.Pm.CloseGump<ArtificersImbueGump>())
        {
        }

        a.Pm.Target.Invoke(a.Pm, item);
        return a.Pm.FindGump<ArtificersImbueGump>();
    }

    private static Longsword MagicSword()
    {
        var sword = new Longsword();
        sword.WeaponAttributes.HitLeechHits = 30;
        return sword;
    }

    private static void Learner(A.Artificer a)
    {
        a.Pm.Skills[SkillName.Imbuing].Base = 50.0;
        a.Pm.Skills[SkillName.Imbuing].Cap = 100.0;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void APlainItemIsRefusedBeforeAnyPageAndPaysNothing()
    {
        using var a = new A.Artificer(0);
        Learner(a);
        var dagger = new Dagger();
        a.Pm.Backpack.DropItem(dagger);
        var shards = a.ShardsLeft;
        var mark = Mark(a.Ns);

        BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };
        try
        {
            var page = TargetForDisenchant(a, dagger);

            Assert.True(SentText(a.Ns, mark, NoMagic));
            Assert.DoesNotContain(page?.Entries.OfType<GumpLabel>().Select(l => l.Text) ?? [],
                t => t.Contains("Full Disenchant"));
            Assert.Equal(shards, a.ShardsLeft);
            Assert.False(dagger.Deleted);
            Assert.Equal(50.0, a.Pm.Skills[SkillName.Imbuing].Base);
        }
        finally
        {
            BuiltInRng.Reset();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AViewLeftOpenAfterTheLastExtractCannotPayButARealDisenchantStillDoes()
    {
        using var a = new A.Artificer(0);
        Learner(a);
        var sword = MagicSword();
        a.Pm.Backpack.DropItem(sword);

        BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };
        try
        {
            var index = ImbueCatalogue.ForItem(sword).FindIndex(d => d.Name == "Hit Life Leech");
            var view = TargetForDisenchant(a, sword);
            var confirm = A.Next(a, view, BtnExtractBase + index);
            var stale = A.Next(a, confirm, BtnApplyExtract);       // the view again, now with no magic
            Assert.NotNull(stale);
            Assert.Empty(ImbueCatalogue.GetActiveProperties(sword));

            var shards = a.ShardsLeft;
            var skill = a.Pm.Skills[SkillName.Imbuing].Base;
            var mark = Mark(a.Ns);

            A.Next(a, stale, BtnFullDisenchant);
            Assert.True(SentText(a.Ns, mark, NoMagic), "the view's button");
            A.Press(stale, a.Ns, BtnApplyDisenchant);                // an apply pressed on the stale gump

            Assert.Equal(shards, a.ShardsLeft);
            Assert.False(sword.Deleted);
            Assert.Equal(skill, a.Pm.Skills[SkillName.Imbuing].Base);

            // A real one still pays and trains.
            var magic = MagicSword();
            a.Pm.Backpack.DropItem(magic);
            var real = TargetForDisenchant(a, magic);
            var sure = A.Next(a, real, BtnFullDisenchant);
            A.Next(a, sure, BtnApplyDisenchant);
            Assert.True(magic.Deleted);
            Assert.True(a.ShardsLeft > shards);
            Assert.True(a.Pm.Skills[SkillName.Imbuing].Base > skill, "a real disenchant gains");
        }
        finally
        {
            BuiltInRng.Reset();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AnExtractedEssenceGoesIntoANestedSatchel()
    {
        using var a = new A.Artificer(0);
        var bag = new Bag();
        a.Pm.Backpack.DropItem(bag);
        var satchel = new ArtificersSatchel();
        bag.DropItem(satchel);
        var sword = MagicSword();
        a.Pm.Backpack.DropItem(sword);

        var index = ImbueCatalogue.ForItem(sword).FindIndex(d => d.Name == "Hit Life Leech");
        var confirm = A.Next(a, TargetForDisenchant(a, sword), BtnExtractBase + index);
        A.Next(a, confirm, BtnApplyExtract);

        Assert.Contains(satchel.Items.OfType<PropertyEssence>(), e => e.PropertyKey == "Hit Life Leech");
        Assert.DoesNotContain(a.Pm.Backpack.Items, i => i is PropertyEssence);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ADisenchantsEssencesGoIntoTheSatchel()
    {
        using var a = new A.Artificer(0);
        var satchel = new ArtificersSatchel();
        a.Pm.Backpack.DropItem(satchel);
        var sword = MagicSword();
        a.Pm.Backpack.DropItem(sword);

        BuiltInRng.Generator = new ConstantRandom { Value = 0.0 };   // every 25% essence roll succeeds
        try
        {
            var sure = A.Next(a, TargetForDisenchant(a, sword), BtnFullDisenchant);
            A.Next(a, sure, BtnApplyDisenchant);
        }
        finally
        {
            BuiltInRng.Reset();
        }

        Assert.True(sword.Deleted);
        Assert.NotEmpty(satchel.Items.OfType<PropertyEssence>());
        Assert.DoesNotContain(a.Pm.Backpack.Items, i => i is PropertyEssence);
    }

    // ---------------------------------------------------------------- 5

    private static bool PickUp(PlayerMobile pm, Item item)
    {
        pm.NextActionTime = Core.TickCount - 1;
        pm.Lift(item, item.Amount, out var rejected, out _);
        Assert.False(rejected, $"lift {item.GetType().Name}");
        return pm.Drop(pm.Backpack, new Point3D(-1, -1, 0));
    }

    [Fact]
    public void LootPickedUpGoesIntoTheSatchelButBankPackAndCraftingDoNot()
    {
        using var a = new A.Artificer(0);
        var pack = new ClusterFBackpack();
        a.Pm.Backpack.Delete();
        a.Pm.AddItem(pack);
        var satchel = new ArtificersSatchel();
        pack.DropItem(satchel);

        // From the ground.
        var residue = new MagicalResidue(5);
        residue.MoveToWorld(a.Pm.Location, a.Pm.Map);
        Assert.True(PickUp(a.Pm, residue));

        // From a corpse's container on the ground.
        var corpseBox = new WoodenBox();
        corpseBox.MoveToWorld(a.Pm.Location, a.Pm.Map);
        var fragment = new RelicFragment(2);
        corpseBox.DropItem(fragment);
        Assert.True(PickUp(a.Pm, fragment));

        var essence = new EnchantedEssence(3);
        essence.MoveToWorld(a.Pm.Location, a.Pm.Map);
        Assert.True(PickUp(a.Pm, essence));

        Assert.Equal(5, satchel.GetAmount(typeof(MagicalResidue)));
        Assert.Equal(2, satchel.GetAmount(typeof(RelicFragment)));
        Assert.Equal(3, satchel.GetAmount(typeof(EnchantedEssence)));

        // From the bank: stays in the pack.
        var banked = new MagicalResidue(4);
        a.Pm.BankBox.DropItem(banked);
        a.Pm.BankBox.Open(); // a closed bank refuses the lift
        Assert.True(PickUp(a.Pm, banked));
        Assert.Equal(pack, banked.Parent);

        // Within the pack (from a bag in it): stays in the pack.
        var bag = new Bag();
        pack.DropItem(bag);
        var inBag = new RelicFragment(1);
        bag.DropItem(inBag);
        Assert.True(PickUp(a.Pm, inBag));
        Assert.Equal(pack, inBag.Parent);

        // Crafting an essence (AddToBackpack): stays in the pack.
        var crafted = new PropertyEssence("Hit Life Leech");
        a.Pm.AddToBackpack(crafted);
        Assert.Equal(pack, crafted.Parent);

        corpseBox.Delete();
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void AFullSatchelSendsTheItemToTheBackpackWithAMessage()
    {
        using var a = new A.Artificer(0);
        var pack = new ClusterFBackpack();
        a.Pm.Backpack.Delete();
        a.Pm.AddItem(pack);
        var satchel = new ArtificersSatchel();
        pack.DropItem(satchel);
        // Full by its own item count. (Filled to its 125 items or 500 stones it would also overfill this test pack.)
        satchel.MaxItems = 1;
        satchel.AddItem(new PropertyEssence("Hit Life Leech"));

        var mark = Mark(a.Ns);
        var residue = new MagicalResidue(1);
        residue.MoveToWorld(a.Pm.Location, a.Pm.Map);
        Assert.True(PickUp(a.Pm, residue));

        Assert.Equal(pack, residue.Parent);
        Assert.True(SentText(a.Ns, mark, ArtificersSatchel.FullMessage));

        // The unravel path the same way.
        var sword = MagicSword();
        pack.DropItem(sword);
        var index = ImbueCatalogue.ForItem(sword).FindIndex(d => d.Name == "Hit Life Leech");
        var confirm = A.Next(a, TargetForDisenchant(a, sword), BtnExtractBase + index);
        A.Next(a, confirm, BtnApplyExtract);
        Assert.Contains(pack.Items.OfType<PropertyEssence>(), e => e.PropertyKey == "Hit Life Leech");
    }
}
