// PostValoriteBODMaterialVerification.cs
//
// cc-P46 Part A (bug-list D55, found in cc-P42). Pinned's ore rule covers DullCopper..Valorite only (SmallBOD.cs:142-150,
// LargeBOD.cs:120-129), and SmallBOD.GetMaterial (SmallBOD.cs:98-115) mapped none of our post-Valorite ores, while
// BulkMaterialType.cs (server/patches) appends Platinum = 12 .. Celestial = 19. So any item, an iron one included, filled
// a Celestial deed. SmallBOD-post-valorite-material.patch and LargeBOD-post-valorite-material.patch add the same rule for
// Platinum..Celestial beside pinned's, with pinned's messages; LargeBODItemFill (ours) checks it too. OSI's rule: a deed
// of an ore takes items (or small deeds) of that ore only. Notes: shard-migration notes/cc-P46-smith-orders-2.md, Part A.
//
// Facts, for each ore DullCopper..Celestial (so DullCopper..Valorite show pinned's rule unchanged):
//   1. Small deed, the deed's own combine cursor: an item of the ore fills; an iron item and an item of the next ore
//      down are refused with "The item is not made from the requested ore." (1045168) and kept.
//   2. Small into large: a completed small deed of the ore combines (1045165); an iron one and one of the next ore down
//      are refused with "Both orders must use the same ore type." (1045162) and kept.
//   3. Item into large (cc-P42 Part H), by the cursor and by "combine with contained items": only the item of the ore
//      fills; iron and the next ore down are refused with 1045168 and stay.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.BulkOrders;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PostValoriteBODMaterialVerification
{
    private readonly ITestOutputHelper _out;

    public PostValoriteBODMaterialVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
    }

    // Iron, then every coloured ore in order: (deed material, the ore an item is made of).
    private static readonly (BulkMaterialType Bulk, CraftResource Ore)[] Ladder =
    {
        (BulkMaterialType.None, CraftResource.Iron),
        (BulkMaterialType.DullCopper, CraftResource.DullCopper),
        (BulkMaterialType.ShadowIron, CraftResource.ShadowIron),
        (BulkMaterialType.Copper, CraftResource.Copper),
        (BulkMaterialType.Bronze, CraftResource.Bronze),
        (BulkMaterialType.Gold, CraftResource.Gold),
        (BulkMaterialType.Agapite, CraftResource.Agapite),
        (BulkMaterialType.Verite, CraftResource.Verite),
        (BulkMaterialType.Valorite, CraftResource.Valorite),
        (BulkMaterialType.Platinum, CraftResource.Platinum),
        (BulkMaterialType.Toxic, CraftResource.Toxic),
        (BulkMaterialType.Blaze, CraftResource.Blaze),
        (BulkMaterialType.Frost, CraftResource.Frost),
        (BulkMaterialType.Obsidian, CraftResource.Obsidian),
        (BulkMaterialType.Mythril, CraftResource.Mythril),
        (BulkMaterialType.Adamantium, CraftResource.Adamantium),
        (BulkMaterialType.Celestial, CraftResource.Celestial),
    };

    public static IEnumerable<object[]> Ores() => Enumerable.Range(1, Ladder.Length - 1).Select(i => new object[] { i });

    private sealed class Smith : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Smith()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.MoveToWorld(new Point3D(1250, 1250, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            // A connection with no account has a 4 KiB send ring (cc-P30); a player is past login.
            Ns.Account = new Server.Accounting.Account($"p46a{Guid.NewGuid():N}"[..16], "p46-test-only");
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public Container Pack => Pm.Backpack;

        public int Mark => Ns.SendBuffer.GetReadSpan().Length;

        // Every 0xC1 localized message number sent since `from`.
        public List<int> Clilocs(int from)
        {
            var span = Ns.SendBuffer.GetReadSpan();
            var found = new List<int>();
            for (var i = from; i + 18 <= span.Length; i++)
            {
                if (span[i] != 0xC1)
                {
                    continue;
                }

                var len = (span[i + 1] << 8) | span[i + 2];
                if (len < 48 || i + len > span.Length)
                {
                    continue;
                }

                found.Add((span[i + 14] << 24) | (span[i + 15] << 16) | (span[i + 16] << 8) | span[i + 17]);
                i += len - 1;
            }

            return found;
        }

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Server.Accounting.Accounts.Remove((Server.Accounting.Account)Ns.Account);
        }
    }

    private static T Put<T>(Container c, T item) where T : Item
    {
        c.DropItem(item);
        return item;
    }

    private static PlateGorget Gorget(CraftResource ore) => new() { Resource = ore };

    // A property, not a static field: xUnit calls Ores() while it discovers the facts, before UOContentFixture has run,
    // and a static field would read the BOD data (SmallBulkEntry.GetEntries) then, configuring the server too early;
    // the fixture's own assembly load then fails for every fact in the collection (seen in cc-P46 build 2).
    private static SmallBulkEntry GorgetEntry => SmallBulkEntry.BlacksmithArmor.First(e => e.Type == typeof(PlateGorget));

    private static SmallSmithBOD SmallDeed(BulkMaterialType mat, int amountCur) =>
        new(amountCur, 10, typeof(PlateGorget), GorgetEntry.Number, GorgetEntry.Graphic, false, mat);

    private static LargeSmithBOD LargeDeed(BulkMaterialType mat)
    {
        var deed = new LargeSmithBOD(10, false, mat, null);
        deed.Entries = LargeBulkEntry.ConvertEntries(deed, LargeBulkEntry.LargePlate);
        return deed;
    }

    private static LargeBulkEntry GorgetLine(LargeBOD deed) => deed.Entries.Single(e => e.Details.Type == typeof(PlateGorget));

    // The deed's own combine cursor (BaseBOD.BeginCombine -> BODTarget), aimed at `item`: the path a player takes.
    private static List<int> Cursor(Smith s, BaseBOD deed, Item item)
    {
        var mark = s.Mark;
        deed.BeginCombine(s.Pm);
        var target = Assert.IsType<BODTarget>(s.Pm.Target);
        target.Invoke(s.Pm, item);
        s.Pm.Target = null;
        return s.Clilocs(mark);
    }

    // ---------------------------------------------------------------- 1

    [Theory]
    [MemberData(nameof(Ores))]
    public void ASmallDeedTakesOnlyItsOwnOre(int rung)
    {
        var (bulk, ore) = Ladder[rung];
        var below = Ladder[rung - 1].Ore;
        using var s = new Smith();
        var deed = Put(s.Pack, SmallDeed(bulk, 0));

        foreach (var wrong in new[] { CraftResource.Iron, below }.Distinct())
        {
            var item = Put(s.Pack, Gorget(wrong));
            var said = Cursor(s, deed, item);
            _out.WriteLine($"{bulk} small deed, {wrong} gorget: {string.Join(", ", said)}");
            Assert.False(item.Deleted, $"a {wrong} gorget filled a {bulk} deed");
            Assert.Contains(1045168, said);
            Assert.Equal(0, deed.AmountCur);
        }

        var right = Put(s.Pack, Gorget(ore));
        var ok = Cursor(s, deed, right);
        Assert.True(right.Deleted);
        Assert.Equal(1, deed.AmountCur);
        Assert.Contains(1045170, ok);
    }

    // ---------------------------------------------------------------- 2

    [Theory]
    [MemberData(nameof(Ores))]
    public void ALargeDeedTakesOnlySmallDeedsOfItsOwnOre(int rung)
    {
        var (bulk, _) = Ladder[rung];
        var below = Ladder[rung - 1].Bulk;
        using var s = new Smith();
        var deed = Put(s.Pack, LargeDeed(bulk));

        foreach (var wrong in new[] { BulkMaterialType.None, below }.Distinct())
        {
            var small = Put(s.Pack, SmallDeed(wrong, 10));
            var said = Cursor(s, deed, small);
            _out.WriteLine($"{bulk} large deed, {wrong} small deed: {string.Join(", ", said)}");
            Assert.False(small.Deleted, $"a {wrong} small deed combined into a {bulk} large deed");
            Assert.Contains(1045162, said);
            Assert.Equal(0, GorgetLine(deed).Amount);
        }

        var right = Put(s.Pack, SmallDeed(bulk, 10));
        var ok = Cursor(s, deed, right);
        Assert.True(right.Deleted);
        Assert.Equal(10, GorgetLine(deed).Amount);
        Assert.Contains(1045165, ok);
    }

    // ---------------------------------------------------------------- 3

    [Theory]
    [MemberData(nameof(Ores))]
    public void ALargeDeedTakesOnlyItemsOfItsOwnOre(int rung)
    {
        var (bulk, ore) = Ladder[rung];
        var below = Ladder[rung - 1].Ore;
        using var s = new Smith();
        var deed = Put(s.Pack, LargeDeed(bulk));

        // By the cursor.
        foreach (var wrong in new[] { CraftResource.Iron, below }.Distinct())
        {
            var item = Put(s.Pack, Gorget(wrong));
            var said = Cursor(s, deed, item);
            Assert.False(item.Deleted, $"a {wrong} gorget filled a {bulk} large deed");
            Assert.Contains(1045168, said);
        }

        Assert.Equal(0, GorgetLine(deed).Amount);
        var right = Put(s.Pack, Gorget(ore));
        Assert.Contains(1045170, Cursor(s, deed, right));
        Assert.True(right.Deleted);
        Assert.Equal(1, GorgetLine(deed).Amount);

        // By "combine with contained items" (cc-P22 F-17, cc-P42 Part H): a bag of one right and the wrong ones.
        var bag = Put(s.Pack, new Bag());
        var good = Put(bag, Gorget(ore));
        var bad = new[] { CraftResource.Iron, below }.Distinct().Select(o => Put(bag, Gorget(o))).ToList();
        var combined = BODCombineContained.Combine(s.Pm, deed, bag);
        _out.WriteLine($"{bulk} large deed, contained: {combined} combined");
        Assert.Equal(1, combined);
        Assert.True(good.Deleted);
        Assert.All(bad, b => Assert.False(b.Deleted));
        Assert.Equal(2, GorgetLine(deed).Amount);
    }
}
