// LootBoxFanfareVerification.cs
//
// cc-P79 (F-40 loot boxes, F-41 fanfare, phase 1). Notes in shard-migration notes/cc-P79-loot-boxes-fanfare.md.
//
// Facts (the brief's tests):
//   1. First award versus repeat: the first earn gives a box; the same loop again gives nothing; a later loop gives no
//      box, a repeat line with the count, and the achievement page's loops line.
//   2. Roll at earn: the contents are fixed when the box is made; opening hands over exactly those items, re-rolling
//      nothing, whatever the character's skills have become.
//   3. Safe-zone refusal: outside a guarded region and any house it refuses with a System line; inside a guarded region
//      it opens; a guarded region with its guards off refuses.
//   4. Houses: the owner, a co-owner and a friend may open; anyone else in the house may not.
//   5. Account bound: no trade, no drop on the ground, onto another player or into another account's pack, no lift by
//      another account; into the owner's own bag is fine.
//   6. Cosmetics are bound; other contents trade.
//   7. An emptied box deletes itself, and the reveal gump lists what it held.
//   8. Fanfare merge: two fanfares in the window play as one with both lines, at the higher level.
//   9. Mute: a muted player's own fanfare plays no effects; the lines still arrive.
//  10. A capstone always broadcasts, even when the player is muted.
// And: the data files round-trip and a bad one is refused whole; the account record v18 keeps the loot records.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Items;
using Server.LootBoxes;
using Server.Misc;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Regions;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LootBoxFanfareVerification
{
    private readonly ITestOutputHelper _out;

    public LootBoxFanfareVerification(ITestOutputHelper output)
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

        LootTable.Current = LootTable.Defaults;
        FanfareLines.Current = FanfareLines.Defaults;
        ClusterFLootBoxes.LoopProvider = _ => 1;
        ClusterFFanfare.FlushAll();
    }

    private static bool _startupHooksRun;

    private sealed class Player : IDisposable
    {
        public readonly Account Account;
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player(Point3D at, Map? map = null)
        {
            Account = new Account($"p79{Guid.NewGuid():N}"[..16], "p79-test-only");
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(at, map ?? Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Dispose()
        {
            ClusterFFanfare.FlushAll();
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static readonly Point3D Wild = new(1400, 1400, 0);

    // A house with a floor, as TestAccountWipeVerification's: the host has no multi data.
    private sealed class TestHouse : SmallOldHouse
    {
        private static readonly MultiComponentList _floor = BuildFloor();

        public TestHouse(Mobile owner) : base(owner, 0x64)
        {
        }

        public override MultiComponentList Components => _floor;
        public override int GetAosMaxSecures() => 425;
        public override int GetAosMaxLockdowns() => 212;

        private static MultiComponentList BuildFloor()
        {
            var tiles = new List<MultiTileEntry>();
            for (short x = -3; x <= 3; x++)
            {
                for (short y = -3; y <= 3; y++)
                {
                    tiles.Add(new MultiTileEntry(0x4B8, x, y, 0, (TileFlag)1));
                }
            }

            return new MultiComponentList(tiles);
        }
    }

    private static LootBox BoxFor(Player p, int tier = 3)
    {
        var box = ClusterFLootBoxes.MakeBox(p.Pm, BoxTheme.Miner, tier, null, "a test", null);
        p.Pm.Backpack.DropItem(box);
        return box;
    }

    private static List<T> InPack<T>(PlayerMobile pm) where T : Item
    {
        var list = new List<T>();
        foreach (var item in pm.Backpack.FindItemsByType<T>())
        {
            list.Add(item);
        }

        return list;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TheFirstEarnGivesABoxTheSameLoopNothingAndALaterLoopARepeatLineWithTheCount()
    {
        using var p = new Player(Wild);

        Assert.Equal(ClusterFLootBoxes.AwardResult.Box,
            ClusterFLootBoxes.Award(p.Pm, "ach:test.first", BoxTheme.Explorer, 2, null, "a test", out var box));
        Assert.NotNull(box);
        Assert.Single(InPack<LootBox>(p.Pm));
        ClusterFFanfare.FlushAll();

        Assert.Equal(ClusterFLootBoxes.AwardResult.AlreadyThisLoop,
            ClusterFLootBoxes.Award(p.Pm, "ach:test.first", BoxTheme.Explorer, 2, null, "a test", out _));

        ClusterFLootBoxes.LoopProvider = _ => 3;
        try
        {
            Assert.Equal(ClusterFLootBoxes.AwardResult.Repeat,
                ClusterFLootBoxes.Award(p.Pm, "ach:test.first", BoxTheme.Explorer, 2, null, "a test", out box));
            Assert.Null(box);
            Assert.Single(InPack<LootBox>(p.Pm)); // still the one box

            var played = ClusterFFanfare.Flush(p.Pm)!;
            _out.WriteLine($"repeat: {string.Join(" | ", played.Lines)}");
            var line = Assert.Single(played.Lines);
            Assert.Contains(line, FanfareLines.Defaults[FanfareLines.RepeatAchievement].Select(l =>
                FanfareLines.Fill(l, p.Pm, new FanfareVars(Loop: 3, Count: 2))));

            Assert.Equal(2, ClusterFLootBoxes.RecordOf(p.Pm)!.CountOf("ach:test.first"));
            Assert.Equal("Earned on loops 1, 3 (2 times)", ClusterFLootBoxes.LoopsLine(p.Account.Username, "test.first"));
        }
        finally
        {
            ClusterFLootBoxes.LoopProvider = _ => 1;
        }
    }

    // ---------------------------------------------------------------- 2, 7

    [Fact]
    public void ContentsAreRolledAtEarnAndOpeningHandsOverExactlyThoseThenTheBoxIsGone()
    {
        var region = new GuardedRegion("p79 guarded", Map.Trammel, 50, new Rectangle2D(1490, 1490, 20, 20));
        region.Register();
        using var p = new Player(new Point3D(1500, 1500, 0));

        try
        {
            p.Pm.Skills[SkillName.Mining].Base = 30;
            var box = BoxFor(p, 5);
            var held = box.Contents.ToList();
            _out.WriteLine($"{box.DefaultName}: {box.Gold} gold, {box.Renown} Renown, " + string.Join(", ", held.Select(LootBoxRoller.Describe)));
            // The general pool fills every slot past theme and currency; the Miner's theme slot gives ore, gems and maybe a third.
            var poolSlots = LootTable.Current.SlotsFor(5) - 2;
            Assert.InRange(held.Count, poolSlots + 2, poolSlots + 3);
            Assert.All(held, i => Assert.Equal(Map.Internal, i.Map));

            // Skills change after the earn: nothing is rolled again.
            p.Pm.Skills[SkillName.Mining].Base = 200;
            var shown = box.Open(p.Pm);
            ClusterFFanfare.FlushAll();

            Assert.True(box.Deleted);
            foreach (var item in held)
            {
                Assert.False(item.Deleted);
                Assert.True(item.IsChildOf(p.Pm.Backpack) || item.IsChildOf(p.Pm.BankBox), $"{item.GetType().Name} was not handed over");
            }

            Assert.Equal(held.Count + 2, shown.Count); // the items, the gold and the Renown
            var gump = new LootBoxRevealGump(box.DefaultName, shown);
            Assert.Equal(shown, gump.Shown);
        }
        finally
        {
            region.Unregister();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void OutsideSafetyItRefusesInAGuardedTownItOpensAndWithGuardsOffItRefuses()
    {
        using var p = new Player(Wild);
        var box = BoxFor(p);

        box.OnDoubleClick(p.Pm);
        Assert.False(box.Deleted);
        Assert.False(LootBox.IsSafeToOpen(p.Pm));

        var region = new GuardedRegion("p79 town", Map.Trammel, 50, new Rectangle2D(1390, 1390, 20, 20));
        region.Register();
        try
        {
            p.Pm.MoveToWorld(new Point3D(1401, 1401, 0), Map.Trammel);
            Assert.True(LootBox.IsSafeToOpen(p.Pm));

            region.GuardsDisabled = true;
            Assert.False(LootBox.IsSafeToOpen(p.Pm));
            region.GuardsDisabled = false;

            box.OnDoubleClick(p.Pm);
            ClusterFFanfare.FlushAll();
            Assert.True(box.Deleted);
        }
        finally
        {
            region.Unregister();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void InAHouseTheOwnerACoOwnerAndAFriendMayOpenAndNobodyElse()
    {
        var at = new Point3D(1600, 1600, 0);
        using var owner = new Player(at);
        using var co = new Player(at);
        using var friend = new Player(at);
        using var stranger = new Player(at);

        var house = new TestHouse(owner.Pm);
        house.MoveToWorld(at, Map.Trammel);
        house.CoOwners ??= new List<Mobile>();
        house.Friends ??= new List<Mobile>();
        house.CoOwners.Add(co.Pm);
        house.Friends.Add(friend.Pm);

        try
        {
            Assert.Same(house, BaseHouse.FindHouseAt(owner.Pm));
            Assert.True(LootBox.IsSafeToOpen(owner.Pm));
            Assert.True(LootBox.IsSafeToOpen(co.Pm));
            Assert.True(LootBox.IsSafeToOpen(friend.Pm));
            Assert.False(LootBox.IsSafeToOpen(stranger.Pm));

            var box = BoxFor(friend);
            box.OnDoubleClick(friend.Pm);
            ClusterFFanfare.FlushAll();
            Assert.True(box.Deleted);

            var refused = BoxFor(stranger);
            refused.OnDoubleClick(stranger.Pm);
            Assert.False(refused.Deleted);
        }
        finally
        {
            house.Delete();
        }
    }

    // ---------------------------------------------------------------- 5, 6

    [Fact]
    public void ABoxAndItsCosmeticsAreAccountBoundAndOtherContentsTrade()
    {
        using var p = new Player(Wild);
        using var other = new Player(Wild);
        var box = BoxFor(p);

        Assert.False(box.AllowSecureTrade(p.Pm, other.Pm, other.Pm, true));
        Assert.False(box.OnDroppedToWorld(p.Pm, Wild));
        Assert.False(box.OnDroppedToMobile(p.Pm, other.Pm));
        Assert.False(box.OnDroppedInto(p.Pm, other.Pm.Backpack, Point3D.Zero));

        var bag = new Bag();
        p.Pm.Backpack.DropItem(bag);
        Assert.True(box.OnDroppedInto(p.Pm, bag, Point3D.Zero));

        var reject = LRReason.Inspecific;
        Assert.False(box.CheckLift(other.Pm, box, ref reject));

        // A cosmetic is bound the same way; gold and gear are not.
        var sash = new BoundCosmeticSash(p.Account.Username, "Box", 0x0489);
        p.Pm.Backpack.DropItem(sash);
        Assert.False(sash.AllowSecureTrade(p.Pm, other.Pm, other.Pm, true));
        Assert.False(sash.OnDroppedToWorld(p.Pm, Wild));
        Assert.False(sash.OnDroppedInto(p.Pm, other.Pm.Backpack, Point3D.Zero));

        var gear = LootBoxRoller.MagicGear(50, 4)!;
        Assert.True(gear.AllowSecureTrade(p.Pm, other.Pm, other.Pm, true));
        Assert.True(new Gold(10).AllowSecureTrade(p.Pm, other.Pm, other.Pm, true));
        gear.Delete();
    }

    // ---------------------------------------------------------------- 8, 9, 10

    [Fact]
    public void FanfaresMergeMuteSilencesTheEffectsAndACapstoneAlwaysBroadcasts()
    {
        using var p = new Player(Wild);

        ClusterFFanfare.Play(p.Pm, FanfareLevel.Minor, FanfareLines.MinorAchievement, tier: 1);
        ClusterFFanfare.Play(p.Pm, FanfareLevel.Major, FanfareLines.BoxAwarded, tier: 4);
        Assert.True(ClusterFFanfare.HasPending(p.Pm));
        var merged = ClusterFFanfare.Flush(p.Pm)!;
        Assert.Equal(FanfareLevel.Major, merged.Level);
        Assert.Equal(4, merged.Tier);
        Assert.Equal(2, merged.Lines.Count);
        Assert.True(merged.Effects);
        Assert.Null(ClusterFFanfare.Flush(p.Pm)); // played once

        ClusterFFanfare.SetMuted(p.Pm, true);
        Assert.True(ClusterFFanfare.IsMuted(p.Pm));
        ClusterFFanfare.Play(p.Pm, FanfareLevel.Major, FanfareLines.MajorAchievement, tier: 4);
        var muted = ClusterFFanfare.Flush(p.Pm)!;
        Assert.False(muted.Effects);
        Assert.Single(muted.Lines);

        ClusterFLootBoxes.OnLoopCompleted(p.Pm, 2);
        var capstone = ClusterFFanfare.Flush(p.Pm)!;
        _out.WriteLine($"capstone broadcast: {capstone.Broadcast}");
        Assert.Equal(FanfareLevel.Capstone, capstone.Level);
        Assert.NotNull(capstone.Broadcast);
        Assert.Contains("Tester", capstone.Broadcast);
        Assert.Contains("2", capstone.Broadcast);
        Assert.False(capstone.Effects); // muted: no personal effects, but the broadcast went out
    }

    // ---------------------------------------------------------------- data and saves

    [Fact]
    public void TheDataFilesRoundTripAndABadOneIsRefusedWhole()
    {
        var json = LootTable.ToJson(LootTable.Defaults);
        var errors = new List<string>();
        var parsed = LootTable.Parse(json, "test", errors);
        Assert.Empty(errors);
        Assert.Equal(LootTable.Defaults.Slots, parsed!.Slots);

        var bad = LootTable.Apply(json.Replace("\"Slots\": [\n    2,", "\"Slots\": [\n    1,"), "bad");
        _out.WriteLine(string.Join(" / ", bad));
        Assert.Contains("REFUSED", bad[0]);
        Assert.Same(LootTable.Defaults, LootTable.Current);

        var lines = FanfareLines.Apply("{ \"minorAchievement\": [ \"Hello [nobody]\" ] }", "bad lines");
        Assert.Contains("REFUSED", lines[0]);
        Assert.Same(FanfareLines.Defaults, FanfareLines.Current);
    }

    [Fact]
    public void TheLootRecordsSurviveASave()
    {
        using var p = new Player(Wild);
        var record = ClusterFLootBoxes.RecordOf(p.Pm)!;
        record.Record("boss:Harrower", 1);
        record.Record("boss:Harrower", 4);
        record.FanfareMuted = true;

        var data = ClusterFAccountPersistence.GetOrCreate(p.Account);
        var w = new BufferWriter(true);
        data.Serialize(w);
        var copy = new ClusterFAccountData(new BufferReader(w.Buffer));

        var read = copy.GetLootRecord(p.Pm.Serial)!;
        Assert.Equal(new[] { 1, 4 }, read.Earned["boss:Harrower"]);
        Assert.True(read.FanfareMuted);
    }
}
