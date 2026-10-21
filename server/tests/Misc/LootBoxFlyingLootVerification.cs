// LootBoxFlyingLootVerification.cs
//
// cc-P242: the loot that flies out of the box (Clint Bellanger's Flying Loot, CC-BY 3.0, our art 15536-15589). Notes in
// shard-migration notes/cc-P242-loot-box-flying-loot.md.
//
// Facts:
//   1. Every kind of item maps to its animation or to the stated fallback (none): a named item of every weapon family,
//      armor, a shield, jewelry, a gem, a potion and the box cosmetic each to its own registered effect ID, and gold,
//      bandages, resources, reagents, scrolls, placeholders, components and a box in a box to none. Over hundreds of
//      real rolls (every theme, every tier), every item the roller returns is one or the other, and gear, potions, gems
//      and cosmetics always have an animation.
//   2. The best item wins: gear over a cosmetic over a gem over a potion; the first rolled wins a tie; nothing with an
//      animation sends nothing.
//   3. One effect per open, for the best item, and the box's contents handed over exactly as without it; none when
//      opening is refused (outside a guarded town, or in a house where the opener is a stranger); a tier 4 box shows
//      it to everyone, a smaller one to the opener alone.
//   4. clusterf.lootBoxes.flyingLoot = false sends nothing, and so does a muted fanfare; the box still opens and hands
//      everything over.
// The art (registry rows, PNGs, the records against ClusterFFlyingLoot.Records) is checked by
// player-package\tests\Build-UoOverrides.Tests.ps1.

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
public class LootBoxFlyingLootVerification
{
    private const int FirstArt = 15536;
    private const int LastArt = 15589;

    private readonly ITestOutputHelper _out;

    public LootBoxFlyingLootVerification(ITestOutputHelper output)
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

        public Player(Point3D at)
        {
            Account = new Account($"p242{Guid.NewGuid():N}"[..16], "p242-test-only");
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Tester" };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(at, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public void Dispose()
        {
            ClusterFFanfare.FlushAll();
            if (ClusterFFanfare.IsMuted(Pm))
            {
                ClusterFFanfare.SetMuted(Pm, false);
            }

            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    // A house with a floor, as LootBoxFanfareVerification's: the host has no multi data.
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

    private static readonly Point3D Wild = new(1400, 1400, 0);

    /// <summary>A box holding exactly these items (no roll), bound to the player and in the pack.</summary>
    private static LootBox BoxWith(Player p, int tier, params Item[] items)
    {
        var box = new LootBox(BoxTheme.Miner, tier, p.Account.Username, "a test");
        box.Fill("Test", 0, 100, 0, items.Select(i => (i, false)).ToList());
        p.Pm.Backpack.DropItem(box);
        return box;
    }

    private sealed class Recorder : IDisposable
    {
        public readonly List<(PlayerMobile Pm, int Id, bool Major)> Sent = new();

        public Recorder() => ClusterFFlyingLoot.Sent += OnSent;

        private void OnSent(PlayerMobile pm, int id, bool major) => Sent.Add((pm, id, major));

        public void Dispose() => ClusterFFlyingLoot.Sent -= OnSent;
    }

    private static int Expected(FlyingLootKind kind) => ClusterFFlyingLoot.Records.Single(r => r.Kind == kind).BaseId;

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EveryKindOfItemMapsToARegisteredAnimationOrTheStatedFallback()
    {
        // Every record: its own six registered frames inside the set's range.
        Assert.Equal(Enum.GetValues<FlyingLootKind>().Length - 1, ClusterFFlyingLoot.Records.Length);
        Assert.Equal(ClusterFFlyingLoot.Records.Length, ClusterFFlyingLoot.Records.Select(r => r.BaseId).Distinct().Count());
        foreach (var r in ClusterFFlyingLoot.Records)
        {
            Assert.NotEqual(FlyingLootKind.None, r.Kind);
            Assert.InRange(r.BaseId, FirstArt, LastArt - ClusterFFlyingLoot.FrameCount + 1);
            Assert.Equal(0, (r.BaseId - FirstArt) % ClusterFFlyingLoot.FrameCount);
            Assert.True(r.Name.Length <= 19 && r.Name.All(c => c < 128), $"{r.Name}: 19 ASCII characters at most");
            Assert.Equal(r.BaseId, ClusterFFlyingLoot.EffectId(r.Kind));
        }

        Assert.Equal(0, ClusterFFlyingLoot.EffectId(FlyingLootKind.None));

        var named = new (Item Item, FlyingLootKind Kind)[]
        {
            (new Longsword(), FlyingLootKind.Blade), (new Axe(), FlyingLootKind.Blade), (new Dagger(), FlyingLootKind.Blade),
            (new Bow(), FlyingLootKind.Bow), (new Crossbow(), FlyingLootKind.Bow),
            (new QuarterStaff(), FlyingLootKind.Staff), (new Mace(), FlyingLootKind.Staff),
            (new Spear(), FlyingLootKind.Staff), (new Halberd(), FlyingLootKind.Staff),
            (new PlateChest(), FlyingLootKind.Armor), (new LeatherGloves(), FlyingLootKind.Armor),
            (new MetalShield(), FlyingLootKind.Shield), (new Buckler(), FlyingLootKind.Shield),
            (new GoldRing(), FlyingLootKind.Jewelry), (new GoldBracelet(), FlyingLootKind.Jewelry),
            (new Ruby(), FlyingLootKind.Gem), (new StarSapphire(), FlyingLootKind.Gem),
            (new GreaterHealPotion(), FlyingLootKind.Potion), (new RefreshPotion(), FlyingLootKind.Potion),
            (new BoundCosmeticSash("someone", "Box", 0x0489), FlyingLootKind.Clothing),
            (new Gold(100), FlyingLootKind.None), (new Bandage(10), FlyingLootKind.None),
            (new IronIngot(10), FlyingLootKind.None), (new Board(10), FlyingLootKind.None),
            (new Leather(10), FlyingLootKind.None), (new BlackPearl(10), FlyingLootKind.None),
            (new BlankScroll(10), FlyingLootKind.None), (new TreasureMap(1, Map.Trammel), FlyingLootKind.None),
            (new LootBoxPlaceholder("Companion Treat"), FlyingLootKind.None),
            (new EpicComponent(), FlyingLootKind.None), (new LoopScrollFragment(), FlyingLootKind.None),
            (new LootBox(), FlyingLootKind.None)
        };

        try
        {
            foreach (var (item, kind) in named)
            {
                Assert.True(kind == ClusterFFlyingLoot.KindOf(item), $"{item.GetType().Name}: {ClusterFFlyingLoot.KindOf(item)}, not {kind}");
            }
        }
        finally
        {
            foreach (var (item, _) in named)
            {
                item.Delete();
            }
        }

        // What the roller really returns: every theme and tier, many times.
        using var p = new Player(Wild);
        var seen = new Dictionary<FlyingLootKind, int>();
        var items = 0;
        foreach (var theme in Enum.GetValues<BoxTheme>())
        {
            for (var tier = 1; tier <= 6; tier++)
            {
                for (var n = 0; n < 8; n++)
                {
                    var rolled = LootBoxRoller.Roll(p.Pm, theme, tier, null, "a test");
                    foreach (var (item, _) in rolled.Items)
                    {
                        items++;
                        var kind = ClusterFFlyingLoot.KindOf(item);
                        seen[kind] = seen.GetValueOrDefault(kind) + 1;
                        if (item is BaseWeapon or BaseArmor or BaseJewel or BasePotion or BaseClothing
                            || Array.IndexOf(Loot.GemTypes, item.GetType()) >= 0)
                        {
                            Assert.True(kind != FlyingLootKind.None, $"{item.GetType().Name} has no animation");
                        }

                        Assert.True(kind == FlyingLootKind.None || ClusterFFlyingLoot.EffectId(kind) is >= FirstArt and <= LastArt,
                            $"{item.GetType().Name} maps outside the set");
                        item.Delete();
                    }
                }
            }
        }

        _out.WriteLine($"{items} rolled items: " + string.Join(", ", seen.OrderBy(k => k.Key).Select(k => $"{k.Key} {k.Value}")));
        Assert.True(seen.GetValueOrDefault(FlyingLootKind.None) > 0, "the fallback was exercised");
        Assert.True(seen.Keys.Count(k => k != FlyingLootKind.None) >= 5, "several animated kinds were rolled");
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheBestItemWinsAndNothingAnimatedSendsNothing()
    {
        var potion = new GreaterHealPotion();
        var ruby = new Ruby();
        var sash = new BoundCosmeticSash("someone", "Box", 0x0489);
        var bow = new Bow();
        var plate = new PlateChest();
        var bandage = new Bandage(10);
        try
        {
            Assert.Equal(FlyingLootKind.Bow, ClusterFFlyingLoot.Best([bandage, potion, ruby, sash, bow, plate]));
            Assert.Equal(FlyingLootKind.Armor, ClusterFFlyingLoot.Best([plate, bow])); // a tie: the first rolled
            Assert.Equal(FlyingLootKind.Clothing, ClusterFFlyingLoot.Best([potion, ruby, sash]));
            Assert.Equal(FlyingLootKind.Gem, ClusterFFlyingLoot.Best([potion, bandage, ruby]));
            Assert.Equal(FlyingLootKind.Potion, ClusterFFlyingLoot.Best([bandage, potion]));
            Assert.Equal(FlyingLootKind.None, ClusterFFlyingLoot.Best([bandage]));
            Assert.Equal(FlyingLootKind.None, ClusterFFlyingLoot.Best([]));
        }
        finally
        {
            foreach (var i in new Item[] { potion, ruby, sash, bow, plate, bandage })
            {
                i.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void OneEffectPerOpenForTheBestItemAndNoneWhenOpeningIsRefused()
    {
        var region = new GuardedRegion("p242 town", Map.Trammel, 50, new Rectangle2D(1490, 1490, 20, 20));
        region.Register();
        using var rec = new Recorder();
        using var p = new Player(Wild);
        try
        {
            // Refused in the wild: nothing sent, nothing handed over.
            var sword = new Longsword();
            var box = BoxWith(p, 2, new Bandage(10), sword, new Ruby());
            box.OnDoubleClick(p.Pm);
            Assert.False(box.Deleted);
            Assert.Empty(rec.Sent);

            // In a guarded town it opens: one effect, the sword's, to the opener alone (tier 2), contents handed over.
            p.Pm.MoveToWorld(new Point3D(1500, 1500, 0), Map.Trammel);
            box.OnDoubleClick(p.Pm);
            ClusterFFanfare.FlushAll();
            Assert.True(box.Deleted);
            var sent = Assert.Single(rec.Sent);
            Assert.Same(p.Pm, sent.Pm);
            Assert.Equal(Expected(FlyingLootKind.Blade), sent.Id);
            Assert.False(sent.Major);
            Assert.True(sword.IsChildOf(p.Pm.Backpack));

            // A tier 4 box shows it to everyone nearby; nothing animated in it sends nothing.
            rec.Sent.Clear();
            BoxWith(p, 4, new GreaterHealPotion()).OnDoubleClick(p.Pm);
            BoxWith(p, 1, new Bandage(10), new IronIngot(10)).OnDoubleClick(p.Pm);
            ClusterFFanfare.FlushAll();
            var major = Assert.Single(rec.Sent);
            Assert.Equal(Expected(FlyingLootKind.Potion), major.Id);
            Assert.True(major.Major);
        }
        finally
        {
            region.Unregister();
        }

        // A house: a stranger is refused and sends nothing; the owner opens and sends one.
        rec.Sent.Clear();
        var at = new Point3D(1600, 1600, 0);
        using var owner = new Player(at);
        using var stranger = new Player(at);
        var house = new TestHouse(owner.Pm);
        house.MoveToWorld(at, Map.Trammel);
        try
        {
            var refused = BoxWith(stranger, 3, new GoldRing());
            refused.OnDoubleClick(stranger.Pm);
            Assert.False(refused.Deleted);
            Assert.Empty(rec.Sent);

            BoxWith(owner, 3, new GoldRing()).OnDoubleClick(owner.Pm);
            ClusterFFanfare.FlushAll();
            var ring = Assert.Single(rec.Sent);
            Assert.Same(owner.Pm, ring.Pm);
            Assert.Equal(Expected(FlyingLootKind.Jewelry), ring.Id);
        }
        finally
        {
            house.Delete();
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheSettingOffOrAMutedFanfareSendsNothingAndTheBoxStillOpens()
    {
        var region = new GuardedRegion("p242 town 2", Map.Trammel, 50, new Rectangle2D(1490, 1490, 20, 20));
        region.Register();
        using var rec = new Recorder();
        using var p = new Player(new Point3D(1500, 1500, 0));
        var kept = ServerConfiguration.GetOrUpdateSetting(ClusterFFlyingLoot.SettingKey, true);
        try
        {
            ServerConfiguration.SetSetting(ClusterFFlyingLoot.SettingKey, false);
            var shield = new MetalShield();
            var box = BoxWith(p, 5, shield);
            box.OnDoubleClick(p.Pm);
            ClusterFFanfare.FlushAll();
            Assert.True(box.Deleted);
            Assert.True(shield.IsChildOf(p.Pm.Backpack));
            Assert.Empty(rec.Sent);

            ServerConfiguration.SetSetting(ClusterFFlyingLoot.SettingKey, true);
            ClusterFFanfare.SetMuted(p.Pm, true);
            var muted = BoxWith(p, 5, new MetalShield());
            muted.OnDoubleClick(p.Pm);
            ClusterFFanfare.FlushAll();
            Assert.True(muted.Deleted);
            Assert.Empty(rec.Sent);

            ClusterFFanfare.SetMuted(p.Pm, false);
            BoxWith(p, 5, new MetalShield()).OnDoubleClick(p.Pm);
            ClusterFFanfare.FlushAll();
            Assert.Equal(Expected(FlyingLootKind.Shield), Assert.Single(rec.Sent).Id);
        }
        finally
        {
            ServerConfiguration.SetSetting(ClusterFFlyingLoot.SettingKey, kept);
            region.Unregister();
        }
    }
}
