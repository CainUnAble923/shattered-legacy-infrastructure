// cc-P199 ResourcesTools batch: sixteen ServUO Items/Resource files (DragonBlood not ported: it is pinned DragonsBlood),
// seven Items/Tools files and two talismans,
// ported to customizations/Items/Resource, Items/Tools and Items/Equipment/Talismans.
//
// Facts:
//   1. The fifteen resource types construct with ServUO's art, label, hue, stacking and weight (IGem is an interface).
//   2. The tools and talismans construct with ServUO's art, label, hue, loot type and values.
//   3. Every ported type survives a serialization round trip, with the saved fields set away from their defaults
//      (key uses, the tub's reward flag and hue, the mallet's resource and uses, talisman charges and attributes,
//      stack amounts).
//   4. Blackrock's label follows its art: large 1153837, small 1150016, anything else 1153836.
//   5. Scissors cut abyssal cloth into the same number of bandages, keeping hue 2075.
//   6. The runic mallet and chisel: 1111796-1111803 for Dull Copper..Valorite, 1045128 for iron, masonry craft system.
//   7. A master skeleton key opens a 100-skill lock with no skill, 200 ms after the target, and spends one of 10 uses.
//   8. A skeleton key lowers the skill gate by 10: 45 skill opens a 50-skill lock (and spends a use), 30 skill does not
//      (and spends nothing); a key on its last use is gone after opening a lock.
//   9. A skeleton key cannot pick a lock of level 0 (CannotPick), master or not.
//  10. The pet bonding potion bonds your own pet and is used up; it refuses someone else's pet and a summoned one and
//      stays in the pack.
//  11. The metallic dye tub dyes plate in its hue and refuses leather.
//  12. The mana phasing orb, worn: a use enters mana phase, spends a charge and starts the 30 s recharge; a second use
//      during the recharge changes nothing; taking it off ends the phase.
//  13. The Talisman of Goblin Slaying has no slayer yet (pinned lacks TalismanSlayerName.Goblin; see its header).
//
// Host notes: timers through ShardTestClock (Arm before anything that schedules, Advance to fire); every fact deletes
// what it made. Weights asserted here are DefaultWeight overrides, so they do not depend on the host's empty tile data.

using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class P199ResourcesToolsVerification
{
    private static readonly Point3D Spot = new(1410, 1730, 0);

    public P199ResourcesToolsVerification()
    {
        ShardTestHost.EnsureSkillChecks();
        Mobile.SkillCheckTargetHandler ??= SkillCheck.Mobile_SkillCheckTarget;
    }

    private sealed class Player : IDisposable
    {
        public readonly PlayerMobile Pm;
        public readonly List<IEntity> Made = new();

        public Player()
        {
            ShardTestClock.Arm();
            Pm = new PlayerMobile { Player = true, Race = Race.Human, RawStr = 100, RawDex = 100, RawInt = 100 };
            Pm.AddItem(new Backpack());
            Pm.MoveToWorld(Spot, Map.Trammel);
        }

        public T Pack<T>(T item) where T : Item
        {
            Pm.Backpack.DropItem(item);
            Made.Add(item);
            return item;
        }

        public BaseCreature Pet(Mobile master)
        {
            var bc = new Skeleton();
            bc.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Pm.Map);
            bc.Controlled = true;
            bc.ControlMaster = master;
            Made.Add(bc);
            return bc;
        }

        public void Dispose()
        {
            foreach (var e in Made)
            {
                e.Delete();
            }

            foreach (var item in new List<Item>(Pm.Backpack.Items))
            {
                item.Delete();
            }

            Pm.Delete();
        }
    }

    private static void Check(Item item, int itemID, int label, int hue, bool stackable, double weight = -1)
    {
        try
        {
            Assert.Equal(itemID, item.ItemID);
            Assert.Equal(label, item.LabelNumber);
            Assert.Equal(hue, item.Hue);
            Assert.Equal(stackable, item.Stackable);
            Assert.Equal(1, item.Amount);

            if (weight >= 0)
            {
                Assert.Equal(weight, item.Weight);
            }
        }
        finally
        {
            item.Delete();
        }
    }

    // ServUO and pinned both label an item with no override 1020000 + art below 0x4000 and 1078872 + art above.
    private static int TileLabel(int itemID) => itemID < 0x4000 ? 1020000 + itemID : 1078872 + itemID;

    [Fact]
    public void TheResourcesConstructAsServUO()
    {
        Check(new AbyssalCloth(), 0x1767, 1113350, 2075, true);
        Check(new BloodOfTheDarkFather(), 0x9D7F, 1157343, 2741, true);
        Check(new EssencePersistence(), 0x571C, 1113343, 37, true);
        Check(new EtherealSand(), 0xA3E8, 1125984, 0, true, 0.1);
        Check(new FeyWings(), 0x5726, 1113332, 0, true, 0.1);
        Check(new LiveRock(), 0xA3E9, 1125985, 0, true);
        Check(new OceanSapphire(), 0xA414, 1159162, 1917, true);
        Check(new PrimalLichDust(), 0x2DB5, 1031701, 0, true);
        Check(new VoidEssence(), 0x4007, 1112327, 0, true);
        Check(new WoodPulp(), 0x103D, 1113136, 0, true);
        Check(new WorkableGlass(), 19328, 1154170, 0, true, 1.0);

        var sand = new EtherealSand(3, 5);
        var meteorite = new PolishedMeteorite();
        var fish = new SmallFish();
        var rock = new Blackrock();

        try
        {
            Assert.InRange(sand.Amount, 3, 5);

            Assert.InRange(meteorite.ItemID, 41422, 41433);
            Assert.Equal(1158693, meteorite.LabelNumber);
            Assert.Equal(3.0, meteorite.Weight);
            Assert.False(meteorite.Stackable);

            Assert.InRange(fish.ItemID, 0x0DD6, 0x0DD7);
            Assert.Equal(TileLabel(fish.ItemID), fish.LabelNumber);
            Assert.False(fish.Stackable);

            Assert.Contains(rock.ItemID, new[] { 0x136C, 0x1EA7 });
            Assert.Equal(1954, rock.Hue);
            Assert.False(rock.Stackable);

            Assert.True(typeof(IGem).IsInterface);
            Assert.Empty(typeof(IGem).GetMembers());
        }
        finally
        {
            sand.Delete();
            meteorite.Delete();
            fish.Delete();
            rock.Delete();
        }
    }

    [Fact]
    public void TheToolsAndTalismansConstructAsServUO()
    {
        var pole = new BegFishingPole();
        var key = new Skeletonkey();
        var master = new MasterSkeletonKey();
        var tub = new MetallicDyeTub();
        var mallet = new RunicMalletAndChisel(CraftResource.Verite);
        var maker = new SteamPoweredBeverageMaker();
        var potion = new PetBondingPotion();
        var goblin = new TalismanofGoblinSlaying();
        ShardTestClock.Arm();
        var orb = new ManaPhasingOrb();

        try
        {
            Assert.Equal(0x0DC0, pole.ItemID);
            Assert.Equal(8.0, pole.Weight);

            Assert.Equal(16650, key.ItemID);
            Assert.Equal(1095522, key.LabelNumber);
            Assert.False(key.Stackable);
            Assert.Equal(10, key.Uses);
            Assert.Equal(10, key.SkillBonus);
            Assert.True(key.IsSkeletonKey);

            Assert.Equal(16650, master.ItemID);
            Assert.Equal(1095523, master.LabelNumber);
            Assert.Equal(10, master.Uses);
            Assert.Equal(100, master.SkillBonus);

            Assert.Equal(0xFAB, tub.ItemID);
            Assert.Equal(1150067, tub.LabelNumber);
            Assert.Equal(LootType.Blessed, tub.LootType);
            Assert.True(tub.MetallicHues);
            Assert.False(tub.AllowDyables);
            Assert.Equal(1080393, tub.TargetMessage);
            Assert.Equal(1080394, tub.FailMessage);
            Assert.False(tub.IsRewardItem);

            Assert.Equal(4787, mallet.ItemID);
            Assert.Equal(CraftResources.GetHue(CraftResource.Verite), mallet.Hue);
            Assert.Equal(2.0, mallet.Weight);

            Assert.Equal(0x9A96, maker.ItemID);
            Assert.Equal(1123598, maker.LabelNumber);
            Assert.Equal(LootType.Blessed, maker.LootType);

            Assert.Equal(0x0F04, potion.ItemID);
            Assert.Equal(1152921, potion.LabelNumber);
            Assert.Equal(2629, potion.Hue);
            Assert.Equal(1.0, potion.Weight);
            Assert.Equal(LootType.Blessed, potion.LootType);

            Assert.Equal(0x2F58, goblin.ItemID);
            Assert.Equal(1095011, goblin.LabelNumber);
            Assert.True(goblin.ForceShowName);
            Assert.Equal(1200, goblin.MaxChargeTime);

            Assert.Equal(4246, orb.ItemID);
            Assert.Equal(1116230, orb.LabelNumber);
            Assert.Equal(1165, orb.Hue);
            Assert.Equal(50, orb.Charges);
            Assert.Equal(50, orb.MaxCharges);
            Assert.Equal(30, orb.MaxChargeTime);
            Assert.Equal(6, orb.Attributes.LowerManaCost);
            Assert.Equal(1, orb.Attributes.RegenHits + orb.Attributes.RegenMana + orb.Attributes.RegenStam);
            Assert.Equal(5, orb.Attributes.DefendChance + orb.Attributes.AttackChance);
            Assert.True(
                orb.Attributes.LowerRegCost == 10 && orb.Attributes.WeaponDamage == 0 && orb.Attributes.SpellDamage == 0 ||
                orb.Attributes.LowerRegCost == 0 && orb.Attributes.WeaponDamage == 15 && orb.Attributes.SpellDamage == 0 ||
                orb.Attributes.LowerRegCost == 0 && orb.Attributes.WeaponDamage == 0 && orb.Attributes.SpellDamage == 5
            );
        }
        finally
        {
            pole.Delete();
            key.Delete();
            master.Delete();
            tub.Delete();
            mallet.Delete();
            maker.Delete();
            potion.Delete();
            goblin.Delete();
            orb.Delete();
        }
    }

    [Fact]
    public void EveryPortedTypeSurvivesARoundTrip()
    {
        ShardTestClock.Arm();

        void Stack<T>(T item, Func<Serial, T> make) where T : Item
        {
            item.Amount = 7;
            var hue = item.Hue;
            var id = item.ItemID;
            var copy = ConsumablesVerification.RoundTrip(item, make);

            try
            {
                Assert.Equal(7, copy.Amount);
                Assert.Equal(hue, copy.Hue);
                Assert.Equal(id, copy.ItemID);
            }
            finally
            {
                copy.Delete();
            }
        }

        void Single<T>(T item, Func<Serial, T> make) where T : Item
        {
            var hue = item.Hue;
            var id = item.ItemID;
            var copy = ConsumablesVerification.RoundTrip(item, make);

            try
            {
                Assert.Equal(hue, copy.Hue);
                Assert.Equal(id, copy.ItemID);
            }
            finally
            {
                copy.Delete();
            }
        }

        Stack(new AbyssalCloth(), s => new AbyssalCloth(s));
        Stack(new BloodOfTheDarkFather(), s => new BloodOfTheDarkFather(s));
        Stack(new EssencePersistence(), s => new EssencePersistence(s));
        Stack(new EtherealSand(), s => new EtherealSand(s));
        Stack(new FeyWings(), s => new FeyWings(s));
        Stack(new LiveRock(), s => new LiveRock(s));
        Stack(new OceanSapphire(), s => new OceanSapphire(s));
        Stack(new PrimalLichDust(), s => new PrimalLichDust(s));
        Stack(new VoidEssence(), s => new VoidEssence(s));
        Stack(new WoodPulp(), s => new WoodPulp(s));
        Stack(new WorkableGlass(), s => new WorkableGlass(s));
        Single(new Blackrock(), s => new Blackrock(s));
        Single(new PolishedMeteorite(), s => new PolishedMeteorite(s));
        Single(new SmallFish(), s => new SmallFish(s));
        Single(new BegFishingPole(), s => new BegFishingPole(s));
        Single(new SteamPoweredBeverageMaker(), s => new SteamPoweredBeverageMaker(s));
        Single(new PetBondingPotion(), s => new PetBondingPotion(s));

        var key = new Skeletonkey { Uses = 7 };
        var keyCopy = ConsumablesVerification.RoundTrip(key, s => new Skeletonkey(s));
        var master = new MasterSkeletonKey { Uses = 4 };
        var masterCopy = ConsumablesVerification.RoundTrip(master, s => new MasterSkeletonKey(s));
        var tub = new MetallicDyeTub { IsRewardItem = true, DyedHue = 2510 };
        var tubCopy = ConsumablesVerification.RoundTrip(tub, s => new MetallicDyeTub(s));
        var mallet = new RunicMalletAndChisel(CraftResource.Valorite, 33);
        var malletCopy = ConsumablesVerification.RoundTrip(mallet, s => new RunicMalletAndChisel(s));
        var goblin = new TalismanofGoblinSlaying();
        var goblinCopy = ConsumablesVerification.RoundTrip(goblin, s => new TalismanofGoblinSlaying(s));
        var orb = new ManaPhasingOrb { Charges = 12 };
        var lowerReg = orb.Attributes.LowerRegCost;
        var orbCopy = ConsumablesVerification.RoundTrip(orb, s => new ManaPhasingOrb(s));

        try
        {
            Assert.Equal(7, keyCopy.Uses);
            Assert.False(keyCopy.Stackable);
            Assert.Equal(4, masterCopy.Uses);
            Assert.True(tubCopy.IsRewardItem);
            Assert.Equal(2510, tubCopy.DyedHue);
            Assert.Equal(LootType.Blessed, tubCopy.LootType);
            Assert.Equal(CraftResource.Valorite, malletCopy.Resource);
            Assert.Equal(33, malletCopy.UsesRemaining);
            Assert.Equal(1111803, malletCopy.LabelNumber);
            Assert.Equal(1200, goblinCopy.MaxChargeTime);
            Assert.Equal(12, orbCopy.Charges);
            Assert.Equal(50, orbCopy.MaxCharges);
            Assert.Equal(30, orbCopy.MaxChargeTime);
            Assert.Equal(6, orbCopy.Attributes.LowerManaCost);
            Assert.Equal(lowerReg, orbCopy.Attributes.LowerRegCost);
        }
        finally
        {
            keyCopy.Delete();
            masterCopy.Delete();
            tubCopy.Delete();
            malletCopy.Delete();
            goblinCopy.Delete();
            orbCopy.Delete();
        }
    }

    [Fact]
    public void BlackrocksLabelFollowsItsArt()
    {
        var rock = new Blackrock();

        try
        {
            rock.ItemID = 0x136C;
            Assert.Equal(1153837, rock.LabelNumber);
            rock.ItemID = 0x1EA7;
            Assert.Equal(1150016, rock.LabelNumber);
            rock.ItemID = 0x1363;
            Assert.Equal(1153836, rock.LabelNumber);
        }
        finally
        {
            rock.Delete();
        }
    }

    [Fact]
    public void ScissorsCutAbyssalClothIntoHuedBandages()
    {
        using var p = new Player();
        var cloth = p.Pack(new AbyssalCloth(5));
        var scissors = p.Pack(new Scissors());

        Assert.True(cloth.Scissor(p.Pm, scissors));

        var bandage = p.Pm.Backpack.FindItemByType<Bandage>();
        Assert.NotNull(bandage);
        Assert.Equal(5, bandage.Amount);
        Assert.Equal(2075, bandage.Hue);
        Assert.True(cloth.Deleted);
    }

    [Fact]
    public void TheRunicMalletIsLabelledByMetalAndCraftsMasonry()
    {
        ShardTestHost.EnsureCraftSystems();

        var resources = new[]
        {
            CraftResource.DullCopper, CraftResource.ShadowIron, CraftResource.Copper, CraftResource.Bronze,
            CraftResource.Gold, CraftResource.Agapite, CraftResource.Verite, CraftResource.Valorite
        };

        for (var i = 0; i < resources.Length; i++)
        {
            var mallet = new RunicMalletAndChisel(resources[i]);

            try
            {
                Assert.Equal(1111796 + i, mallet.LabelNumber);
            }
            finally
            {
                mallet.Delete();
            }
        }

        var iron = new RunicMalletAndChisel(CraftResource.Iron, 10);

        try
        {
            Assert.Equal(1045128, iron.LabelNumber);
            Assert.Equal(10, iron.UsesRemaining);
            Assert.NotNull(iron.CraftSystem);
            Assert.Same(DefMasonry.CraftSystem, iron.CraftSystem);
        }
        finally
        {
            iron.Delete();
        }
    }

    private static WoodenChest LockedChest(Player p, int required, int lockLevel, int maxLockLevel) =>
        p.Pack(new WoodenChest { Locked = true, RequiredSkill = required, LockLevel = lockLevel, MaxLockLevel = maxLockLevel });

    private static void Pick(Player p, Skeletonkey key, Item target)
    {
        key.OnDoubleClick(p.Pm);
        Assert.NotNull(p.Pm.Target);
        p.Pm.Target.Invoke(p.Pm, target);
        ShardTestClock.Advance(TimeSpan.FromMilliseconds(250));
    }

    [Fact]
    public void AMasterSkeletonKeyOpensAnyPickableLockAndSpendsAUse()
    {
        using var p = new Player();
        p.Pm.Skills.Lockpicking.Base = 0;
        var key = p.Pack(new MasterSkeletonKey());
        var chest = LockedChest(p, 100, 80, 120);

        key.OnDoubleClick(p.Pm);
        p.Pm.Target.Invoke(p.Pm, chest);

        // ServUO resolves 200 ms after the target, not at once.
        Assert.True(chest.Locked);
        ShardTestClock.Advance(TimeSpan.FromMilliseconds(250));

        Assert.False(chest.Locked);
        Assert.Same(p.Pm, chest.Picker);
        Assert.Equal(9, key.Uses);
        Assert.False(key.Deleted);
    }

    [Fact]
    public void ASkeletonKeyLowersTheSkillGateByTen()
    {
        using var p = new Player();
        var key = p.Pack(new Skeletonkey());

        p.Pm.Skills.Lockpicking.Base = 30;
        var hard = LockedChest(p, 50, 10, 45);
        Pick(p, key, hard);
        Assert.True(hard.Locked);
        Assert.Equal(10, key.Uses);

        p.Pm.Skills.Lockpicking.Base = 45;
        Pick(p, key, hard);
        Assert.False(hard.Locked);
        Assert.Equal(9, key.Uses);

        key.Uses = 1;
        var last = LockedChest(p, 50, 10, 45);
        Pick(p, key, last);
        Assert.False(last.Locked);
        Assert.True(key.Deleted);
    }

    [Fact]
    public void ASkeletonKeyCannotPickAnUnpickableLock()
    {
        using var p = new Player();
        p.Pm.Skills.Lockpicking.Base = 100;
        var master = p.Pack(new MasterSkeletonKey());
        var chest = LockedChest(p, 10, ILockpickable.CannotPick, 45);

        Pick(p, master, chest);

        Assert.True(chest.Locked);
        Assert.Equal(10, master.Uses);
    }

    [Fact]
    public void ThePetBondingPotionBondsOnlyYourOwnRealPet()
    {
        using var p = new Player();
        var stranger = new PlayerMobile { Player = true };
        stranger.MoveToWorld(new Point3D(Spot.X, Spot.Y + 1, Spot.Z), Map.Trammel);
        p.Made.Add(stranger);

        var theirs = p.Pet(stranger);
        var summoned = p.Pet(p.Pm);
        summoned.Summoned = true;
        var mine = p.Pet(p.Pm);
        var potion = p.Pack(new PetBondingPotion());

        foreach (var refused in new[] { theirs, summoned })
        {
            potion.OnDoubleClick(p.Pm);
            Assert.IsType<BondingTarget>(p.Pm.Target);
            p.Pm.Target.Invoke(p.Pm, refused);
            Assert.False(refused.IsBonded);
            Assert.False(potion.Deleted);
        }

        potion.OnDoubleClick(p.Pm);
        p.Pm.Target.Invoke(p.Pm, mine);

        Assert.True(mine.IsBonded);
        Assert.True(potion.Deleted);
    }

    [Fact]
    public void TheMetallicDyeTubDyesPlateAndRefusesLeather()
    {
        using var p = new Player();
        var tub = p.Pack(new MetallicDyeTub { DyedHue = 2520 });
        var plate = p.Pack(new PlateChest());
        var leather = p.Pack(new LeatherChest());

        tub.OnDoubleClick(p.Pm);
        p.Pm.Target.Invoke(p.Pm, leather);
        Assert.Equal(0, leather.Hue);

        tub.OnDoubleClick(p.Pm);
        p.Pm.Target.Invoke(p.Pm, plate);
        Assert.Equal(2520, plate.Hue);
    }

    [Fact]
    public void TheManaPhasingOrbEntersPhaseWhenWornAndLeavesWhenRemoved()
    {
        using var p = new Player();
        var orb = new ManaPhasingOrb();
        p.Made.Add(orb);

        try
        {
            // Not worn: nothing happens.
            p.Pack(orb);
            orb.OnDoubleClick(p.Pm);
            Assert.False(ManaPhasingOrb.IsInManaPhase(p.Pm));
            Assert.Equal(50, orb.Charges);

            p.Pm.AddItem(orb);
            Assert.Same(orb, p.Pm.Talisman);

            orb.OnDoubleClick(p.Pm);
            Assert.True(ManaPhasingOrb.IsInManaPhase(p.Pm));
            Assert.Equal(49, orb.Charges);
            Assert.Equal(30, orb.ChargeTime);

            orb.OnDoubleClick(p.Pm);
            Assert.Equal(49, orb.Charges);
            Assert.True(ManaPhasingOrb.IsInManaPhase(p.Pm));

            p.Pm.RemoveItem(orb);
            Assert.False(ManaPhasingOrb.IsInManaPhase(p.Pm));
        }
        finally
        {
            ManaPhasingOrb._ManaPhaseTable?.Remove(p.Pm);
        }
    }

    [Fact]
    public void TheGoblinSlayingTalismanHasNoSlayerUntilPinnedHasGoblin()
    {
        var goblin = new TalismanofGoblinSlaying();

        try
        {
            Assert.Equal(TalismanSlayerName.None, goblin.Slayer);
            Assert.False(Enum.IsDefined(typeof(TalismanSlayerName), "Goblin"));
        }
        finally
        {
            goblin.Delete();
        }
    }
}
