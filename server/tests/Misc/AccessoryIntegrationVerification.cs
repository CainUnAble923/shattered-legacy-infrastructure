// cc-P610 (P533 W3, P486 F01-F09, P482 #2): extra jewelry stays removable and obeys the equipment systems. Notes:
// shard-migration notes/cc-P610-fix-drafts-from-p533.md.
//
// Facts:
//   1. A bracelet left in a slot that locked again shows in a fresh accessories gump and its Remove takes it back (F01).
//   2. Losing the League rank takes the extra bracelet's skill bonus off at once and keeps the bracelet; raising the
//      unlock rank in the settings does the same on the wearer's next step (F02).
//   3. An extra slot runs the jewel's own equip checks: a staff ring is refused (it deletes itself for a player, as on
//      the paperdoll) and Barreraak's ring is refused while mounted (F03).
//   4. Set and absorption jewelry built on BaseJewel (Bracelet of Primal Consumption, Etoile Bleue) is a bracelet and a
//      ring, in the paperdoll slot and in an extra one (F04).
//   5. A levelable bracelet in an active extra slot gives its bought bonus once and earns XP; in a locked slot neither
//      (F05, P482 #2). Its custom imbue counts there (Bountiful), a covered toe ring's does not, and Fortune's Favor knows
//      when it went on (F06).
//   6. An imbue that changes an active extra bracelet's skill bonus takes effect at once (F07).
//   7. Taking the shoes off as the last step that fills every jewelry slot earns Fully Accessorized (F08).
//   8. Repeated refreshes and re-equips of the same jewel add its skill and stat bonus once (F09).

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class AccessoryIntegrationVerification : IDisposable
{
    private static readonly Point3D Spot = new(1490, 1760, 0);
    private const string LoopKey = "clusterf.accessories.loopSlotsEnabled";
    private const string BraceletKey = "clusterf.accessories.braceletLeagueRank";

    private readonly List<IEntity> _things = [];
    private readonly List<Account> _accounts = [];
    private readonly bool _loopKept;
    private readonly int _braceletKept;

    public AccessoryIntegrationVerification()
    {
        ShardTestHost.EnsureAccounts();
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }

        ClusterFAccessories.Register();
        _loopKept = ServerConfiguration.GetSetting(LoopKey, false);
        _braceletKept = ServerConfiguration.GetSetting(BraceletKey, 9);
    }

    public void Dispose()
    {
        ClusterFAccessories.LoopOverride = null;
        ServerConfiguration.SetSetting(LoopKey, _loopKept);
        ServerConfiguration.SetSetting(BraceletKey, _braceletKept);
        foreach (var e in _things)
        {
            e.Delete();
        }

        foreach (var a in _accounts)
        {
            ClusterFAchievementSystem.ResetForAccount(a.Username);
        }
    }

    private T Track<T>(T e) where T : IEntity
    {
        _things.Add(e);
        return e;
    }

    private PlayerMobile Player()
    {
        var account = new Account($"p610{Guid.NewGuid():N}"[..16], "p610-test-only");
        _accounts.Add(account);
        var pm = Track(new PlayerMobile { Player = true, Race = Race.Human, Name = "P610 tester" });
        pm.RawStr = 100;
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private T InPack<T>(PlayerMobile pm, T item) where T : Item
    {
        Track(item);
        pm.Backpack.DropItem(item);
        return item;
    }

    /// <summary>Two bracelet slots: the League rank the setting asks for.</summary>
    private static void TwoBracelets(PlayerMobile pm) => ClusterFLeagueRanks.SetRank(pm, AccessorySettings.BraceletLeagueRank);

    private (GoldBracelet First, GoldBracelet Extra) BothBracelets(PlayerMobile pm, Action<GoldBracelet>? dressExtra = null)
    {
        TwoBracelets(pm);
        var first = InPack(pm, new GoldBracelet());
        var extra = InPack(pm, new GoldBracelet());
        dressExtra?.Invoke(extra);
        Assert.Null(ClusterFAccessories.Equip(pm, first));
        Assert.Same(pm, first.Parent);
        Assert.Null(ClusterFAccessories.Equip(pm, extra));
        Assert.IsType<AccessoryContainer>(extra.Parent);
        return (first, extra);
    }

    private static RelayInfo Press(int button) => new(button, default, default, default, default);

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ABraceletInASlotThatLockedAgainShowsInAFreshGumpAndComesOff()
    {
        var pm = Player();
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        try
        {
            var (_, extra) = BothBracelets(pm);
            ClusterFLeagueRanks.SetRank(pm, 1); // the slot locks again

            pm.SendGump(new AccessoriesGump(pm, pm));
            var gump = pm.FindGump<AccessoriesGump>();
            Assert.NotNull(gump);
            gump.OnResponse(ns, Press(101)); // 100: the paperdoll bracelet, 101: the one in the locked slot

            Assert.Same(pm.Backpack, extra.Parent);
            Assert.False(ClusterFAccessories.IsApplied(extra));
        }
        finally
        {
            pm.NetState = null;
            ns.Mobile = null;
            ns.Dispose();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void LosingTheRankTakesTheBonusOffAtOnceAndASettingTakesItOffAtTheNextStep()
    {
        var pm = Player();
        pm.Skills.Magery.Base = 50.0; // above a human's Jack of All Trades 20, so a bonus shows
        var magery = pm.Skills.Magery.Base;
        var (_, extra) = BothBracelets(pm, b => b.SkillBonuses.SetValues(0, SkillName.Magery, 10.0));
        Assert.Equal(magery + 10, pm.Skills.Magery.Value, 1);

        ClusterFLeagueRanks.SetRank(pm, 1);
        Assert.Equal(magery, pm.Skills.Magery.Value, 1);
        Assert.False(ClusterFAccessories.IsApplied(extra));
        Assert.IsType<AccessoryContainer>(extra.Parent); // kept

        TwoBracelets(pm);
        Assert.Equal(magery + 10, pm.Skills.Magery.Value, 1);

        ServerConfiguration.SetSetting(BraceletKey, AccessorySettings.BraceletLeagueRank + 5); // a settings reload
        ClusterFAccessories.OnMovement(new MovementEventArgs(pm, Direction.East)); // the next step (EventSink.Movement)
        Assert.Equal(magery, pm.Skills.Magery.Value, 1);
        Assert.False(ClusterFAccessories.IsApplied(extra));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AnExtraSlotRunsTheJewelsOwnEquipChecks()
    {
        var pm = Player();
        ServerConfiguration.SetSetting(LoopKey, true);
        ClusterFAccessories.LoopOverride = _ => 10;
        Assert.Null(ClusterFAccessories.Equip(pm, InPack(pm, new GoldRing()))); // the paperdoll ring is taken

        var staff = InPack(pm, new StaffRing());
        ClusterFAccessories.Equip(pm, staff);
        Assert.True(staff.Deleted || staff.Parent is not AccessoryContainer, "a player cannot wear a staff ring in an extra slot");
        Assert.DoesNotContain(ClusterFAccessories.InContainer(pm, AccessoryKind.Ring), i => i is StaffRing);

        var horse = Track(new Horse());
        horse.MoveToWorld(Spot, Map.Trammel);
        horse.Rider = pm;
        Assert.True(pm.Mounted);
        var barreraak = InPack(pm, new BarreraaksRing());
        Assert.NotNull(ClusterFAccessories.Equip(pm, barreraak));
        Assert.Same(pm.Backpack, barreraak.Parent);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void SetAndAbsorptionJewelryIsABraceletAndARing()
    {
        var pm = Player();
        TwoBracelets(pm);
        var primal = InPack(pm, new BraceletOfPrimalConsumption());
        var etoile = InPack(pm, new EtoileBleue());
        Assert.Equal(AccessoryKind.Bracelet, ClusterFAccessories.KindOf(primal));
        Assert.Equal(AccessoryKind.Ring, ClusterFAccessories.KindOf(etoile));

        Assert.Null(ClusterFAccessories.Equip(pm, primal)); // the paperdoll bracelet slot
        Assert.Same(pm, primal.Parent);
        Assert.Null(ClusterFAccessories.Equip(pm, etoile)); // the paperdoll ring slot
        Assert.Same(pm, etoile.Parent);

        var second = InPack(pm, new BraceletOfPrimalConsumption());
        Assert.Null(ClusterFAccessories.Equip(pm, second)); // the extra bracelet slot
        Assert.IsType<AccessoryContainer>(second.Parent);
        // The carrier keeps what it carries: its absorption and its own luck, in the extra slot as on the paperdoll.
        Assert.Equal(6, ((IAbsorptionItem)second).AbsorptionAttributes.EaterDamage);
        Assert.True(AosAttributes.GetValue(pm, AosAttribute.Luck) >= 400, "both bracelets' 200 luck count");
    }

    // ---------------------------------------------------------------- 5

    private GoldBracelet LevelableBracelet()
    {
        var b = new GoldBracelet { PlayerConstructed = true };
        Track(b);
        Assert.Null(ClusterFLevelGear.MakeLevelable(b, null));
        LevelGearStore.Of(b)!.Points = 100;
        Assert.Null(ClusterFLevelGear.Buy(b, LevelGearAttribute.ByKey("defenseChance")!));
        return b;
    }

    [Fact]
    public void ALevelableBraceletInAnExtraSlotGivesItsBoughtBonusOnceAndEarnsXp()
    {
        var pm = Player();
        TwoBracelets(pm);
        Assert.Null(ClusterFAccessories.Equip(pm, InPack(pm, new GoldBracelet())));
        var levelable = LevelableBracelet();
        pm.Backpack.DropItem(levelable);
        var dci = AosAttributes.GetValue(pm, AosAttribute.DefendChance);

        Assert.Null(ClusterFAccessories.Equip(pm, levelable));
        Assert.IsType<AccessoryContainer>(levelable.Parent);
        Assert.Equal(dci + 1, AosAttributes.GetValue(pm, AosAttribute.DefendChance));

        var xp = LevelGearStore.Of(levelable)!.Xp;
        ClusterFLevelGear.AwardWorn(pm, 50);
        Assert.True(LevelGearStore.Of(levelable)!.Xp > xp, "an active extra bracelet earns XP");

        ClusterFLeagueRanks.SetRank(pm, 1); // the slot locks
        Assert.Equal(dci, AosAttributes.GetValue(pm, AosAttribute.DefendChance));
        xp = LevelGearStore.Of(levelable)!.Xp;
        ClusterFLevelGear.AwardWorn(pm, 50);
        Assert.Equal(xp, LevelGearStore.Of(levelable)!.Xp);
    }

    [Fact]
    public void ACustomImbueCountsInAnActiveExtraSlotAndFortunesFavorKnowsWhenItWentOn()
    {
        var pm = Player();
        TwoBracelets(pm);
        Assert.Null(ClusterFAccessories.Equip(pm, InPack(pm, new GoldBracelet())));
        var bountiful = InPack(pm, new GoldBracelet());
        ImbueCustomStore.Set(bountiful, "Bountiful", 10);
        Assert.Null(ClusterFAccessories.Equip(pm, bountiful));
        Assert.Equal(10, ClusterFImbueEffects.Worn(pm, "Bountiful"));

        ClusterFLeagueRanks.SetRank(pm, 1);
        Assert.Equal(0, ClusterFImbueEffects.Worn(pm, "Bountiful"));
        ClusterFAccessories.Unequip(pm, bountiful);

        TwoBracelets(pm);
        var lucky = InPack(pm, new GoldBracelet());
        ImbueCustomStore.Set(lucky, "Fortune's Favor", 5);
        Assert.Null(ClusterFAccessories.Equip(pm, lucky));
        Assert.Equal(5, ClusterFImbueEffects.FortunePercent(pm));
        Assert.True(ClusterFImbueEffects.LuckGearJustEquipped(pm), "the extra slot's Fortune's Favor counts as just put on");
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void AnImbueOnAnActiveExtraBraceletTakesEffectAtOnce()
    {
        var pm = Player();
        pm.Skills.Magery.Base = 50.0; // above a human's Jack of All Trades 20, so a bonus shows
        var magery = pm.Skills.Magery.Base;
        var (_, extra) = BothBracelets(pm, b => b.SkillBonuses.SetValues(0, SkillName.Magery, 10.0));
        Assert.Equal(magery + 10, pm.Skills.Magery.Value, 1);

        // What the imbuing gump does after it writes the property (ArtificersImbueGump): it tells the accessory slots.
        extra.SkillBonuses.SetValues(0, SkillName.Magery, 15.0);
        typeof(ClusterFAccessories).GetMethod("OnPropertiesChanged", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, [extra]);
        Assert.Equal(magery + 15, pm.Skills.Magery.Value, 1);
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TakingTheShoesOffAsTheLastStepEarnsFullyAccessorized()
    {
        var pm = Player();
        Assert.True(ClusterFAchievementSystem.TryGrant(pm.Account, ClusterFAchievementSystem.PlateBottomsOnlyKey));
        var shoes = InPack(pm, new Shoes());
        Assert.True(pm.EquipItem(shoes) || pm.FindItemOnLayer(Layer.Shoes) == shoes || AddShoes(pm, shoes));

        Assert.Null(ClusterFAccessories.Equip(pm, InPack(pm, new GoldRing())));
        Assert.Null(ClusterFAccessories.Equip(pm, InPack(pm, new GoldBracelet())));
        var earrings = InPack(pm, new GoldEarrings());
        pm.AddItem(earrings);
        var necklace = InPack(pm, new GoldNecklace());
        pm.AddItem(necklace);
        Assert.Null(ClusterFAccessories.Equip(pm, InPack(pm, new ToeRing())));
        Assert.False(ClusterFAchievementSystem.HasEarned(pm.Account, ClusterFAccessoryAchievementsKey()), "shod: the toe ring does not count yet");

        pm.Backpack.DropItem(shoes); // the shoes come off
        Assert.True(ClusterFAccessories.EveryJewelrySlotFilled(pm));
        Assert.True(ClusterFAchievementSystem.HasEarned(pm.Account, ClusterFAccessoryAchievementsKey()));
    }

    private static string ClusterFAccessoryAchievementsKey() => ClusterFAchievementSystem.FullyAccessorizedKey;

    private static bool AddShoes(PlayerMobile pm, Shoes shoes)
    {
        shoes.Layer = Layer.Shoes; // the host has no tile data to read the layer from
        pm.AddItem(shoes);
        return pm.FindItemOnLayer(Layer.Shoes) == shoes;
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void RepeatedRefreshesAndReEquipsAddTheBonusesOnce()
    {
        var pm = Player();
        pm.Skills.Magery.Base = 50.0; // above a human's Jack of All Trades 20, so a bonus shows
        var magery = pm.Skills.Magery.Base;
        var str = pm.Str;
        var (_, extra) = BothBracelets(pm, b =>
        {
            b.SkillBonuses.SetValues(0, SkillName.Magery, 10.0);
            b.Attributes.BonusStr = 5;
        });

        for (var i = 0; i < 3; i++)
        {
            ClusterFAccessories.Refresh(pm);
            Assert.Null(ClusterFAccessories.Unequip(pm, extra));
            Assert.Equal(magery, pm.Skills.Magery.Value, 1); // off with the jewel, every time
            Assert.Equal(str, pm.Str);
            Assert.Null(ClusterFAccessories.Equip(pm, extra));
        }

        Assert.Equal(magery + 10, pm.Skills.Magery.Value, 1);
        Assert.Equal(str + 5, pm.Str);
    }
}
