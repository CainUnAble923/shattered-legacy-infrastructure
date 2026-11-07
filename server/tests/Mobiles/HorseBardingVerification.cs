// HorseBardingVerification.cs
//
// cc-P809: crafted horse barding. Notes in shard-migration notes/cc-P809-pet-perch-horse-barding.md.
//
// Facts: the numbers are Chase's (half the dragon); the craft row; who may put it on which horse and the look it gives; a
// monster's hit on the rider loses the barding's share through AOS.Damage (the patch), a player's does not; it wears out and
// gives the horse its look back; its store saves and loads, drops gone horses, and refuses an unknown version; the
// settings file is refused whole when wrong.

using System;
using System.Collections.Generic;
using System.IO;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HorseBardingVerification : IDisposable
{
    private static readonly Point3D Spot = new(5460, 3840, 0);
    private readonly List<IEntity> _made = [];

    public HorseBardingVerification()
    {
        HorseBardingSettings.Current = new HorseBardingSettings();
        HorseBardingStore.Rows.Clear();
    }

    public void Dispose()
    {
        foreach (var e in _made)
        {
            e?.Delete();
        }

        HorseBardingStore.Rows.Clear();
        HorseBardingSettings.Current = new HorseBardingSettings();
    }

    private T Track<T>(T e) where T : IEntity
    {
        _made.Add(e);
        return e;
    }

    private PlayerMobile Player(string name = "Rider")
    {
        var pm = Track(new PlayerMobile { Player = true, Race = Race.Human, Name = name });
        pm.AddItem(new Backpack());
        pm.RawStr = 100;
        pm.Hits = pm.HitsMax;
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private Horse TamedHorse(Mobile owner)
    {
        var horse = Track(new Horse());
        horse.MoveToWorld(Spot, Map.Trammel);
        horse.SetControlMaster(owner);
        return horse;
    }

    private CraftedHorseBarding Barding(Mobile owner, bool exceptional = false, CraftResource resource = CraftResource.Valorite)
    {
        var barding = Track(new CraftedHorseBarding { Exceptional = exceptional, Resource = resource });
        owner.Backpack.DropItem(barding);
        return barding;
    }

    [Fact]
    public void TheNumbersAreHalfTheDragonsAsChaseChose()
    {
        var s = new HorseBardingSettings();
        Assert.Equal(5, s.AbsorbPercent);
        Assert.Equal(10, s.AbsorbPercentExceptional);
        Assert.Equal(500, s.Durability);
        Assert.Equal(1250, s.DurabilityExceptional);
        Assert.Equal(400, s.IronIngots);
        Assert.Equal(60.0, s.MinSkill);
        Assert.Equal(284, s.BardedBody);
        Assert.Equal(0x3E92, s.BardedMountItemID);
    }

    [Fact]
    public void ABlacksmithMakesItFrom400IronAtSixty()
    {
        if (DefBlacksmithy.CraftSystem == null)
        {
            DefBlacksmithy.Initialize();
        }

        HorseBarding.Register();
        HorseBarding.Register(); // idempotent

        var row = DefBlacksmithy.CraftSystem.CraftItems.SearchFor(typeof(CraftedHorseBarding));
        Assert.NotNull(row);
        Assert.Equal(typeof(IronIngot), row.Resources[0].ItemType);
        Assert.Equal(400, row.Resources[0].Amount);
        Assert.Equal(60.0, row.Skills[0].MinSkill);

        var count = 0;
        foreach (CraftItem item in DefBlacksmithy.CraftSystem.CraftItems)
        {
            if (item.ItemType == typeof(CraftedHorseBarding))
            {
                count++;
            }
        }

        Assert.Equal(1, count);
    }

    [Fact]
    public void OnlyItsOwnerPutsItOnTheirOwnPlainHorseWhichTakesTheArmoredLook()
    {
        var owner = Player();
        var stranger = Player("Stranger");

        var wild = Track(new Horse());
        wild.MoveToWorld(Spot, Map.Trammel);
        var b = Barding(owner);
        b.OnTarget(owner, wild);
        Assert.False(b.Deleted);
        Assert.False(HorseBarding.HasBarding(wild));

        var theirs = TamedHorse(stranger);
        b.OnTarget(owner, theirs);
        Assert.False(b.Deleted);
        Assert.False(HorseBarding.HasBarding(theirs));

        var tutorial = Track(new TutorialStableHorse(owner));
        Assert.False(HorseBarding.IsBardable(tutorial));

        var horse = TamedHorse(owner);
        int body = horse.Body, item = horse.ItemID, hue = horse.Hue;
        b.OnTarget(owner, horse);
        Assert.True(b.Deleted);
        Assert.True(HorseBarding.HasBarding(horse));
        Assert.Equal(284, (int)horse.Body);
        Assert.Equal(0x3E92, horse.ItemID);
        Assert.Equal(CraftResources.GetHue(CraftResource.Valorite), horse.Hue);

        var r = HorseBarding.Get(horse);
        Assert.Equal(500, r.HP);
        Assert.Equal(body, r.OldBody);
        Assert.Equal(item, r.OldItemID);
        Assert.Equal(hue, r.OldHue);

        var second = Barding(owner);
        second.OnTarget(owner, horse); // already barded
        Assert.False(second.Deleted);
    }

    [Fact]
    public void AMonstersHitOnTheRiderLosesFivePercentOrTenIfExceptional()
    {
        var owner = Player();
        var monster = Track(new Mongbat());
        monster.MoveToWorld(Spot, Map.Trammel);

        var horse = TamedHorse(owner);
        horse.Rider = owner;
        Assert.True(owner.Mounted);

        var plain = AOS.Damage(owner, monster, 100, false, 0, 0, 0, 0, 0, 0, 100);
        owner.Hits = owner.HitsMax;

        horse.Rider = null;
        Barding(owner).OnTarget(owner, horse);
        horse.Rider = owner;

        var barded = AOS.Damage(owner, monster, 100, false, 0, 0, 0, 0, 0, 0, 100);
        owner.Hits = owner.HitsMax;
        Assert.Equal(plain - AOS.Scale(plain, 5), barded);
        Assert.Equal(500 - AOS.Scale(plain, 5), HorseBarding.Get(horse).HP);

        // A player's hit passes by it, as it does the dragon's.
        var player = Player("Duelist");
        var pvp = AOS.Damage(owner, player, 100, false, 0, 0, 0, 0, 0, 0, 100);
        owner.Hits = owner.HitsMax;
        Assert.Equal(plain, pvp);

        // Exceptional: ten percent.
        var owner2 = Player("Rider2");
        var horse2 = TamedHorse(owner2);
        Barding(owner2, exceptional: true).OnTarget(owner2, horse2);
        Assert.Equal(1250, HorseBarding.Get(horse2).HP);
        horse2.Rider = owner2;
        var ex = AOS.Damage(owner2, monster, 100, false, 0, 0, 0, 0, 0, 0, 100);
        Assert.Equal(plain - AOS.Scale(plain, 10), ex);
    }

    [Fact]
    public void ItWearsOutAndTheHorseGetsItsOwnLookBack()
    {
        var owner = Player();
        var monster = Track(new Mongbat());
        monster.MoveToWorld(Spot, Map.Trammel);

        var horse = TamedHorse(owner);
        int body = horse.Body, item = horse.ItemID, hue = horse.Hue;
        Barding(owner).OnTarget(owner, horse);
        horse.Rider = owner;

        HorseBarding.Get(horse).HP = 1;
        AOS.Damage(owner, monster, 100, false, 0, 0, 0, 0, 0, 0, 100);

        Assert.False(HorseBarding.HasBarding(horse));
        Assert.Equal(body, (int)horse.Body);
        Assert.Equal(item, horse.ItemID);
        Assert.Equal(hue, horse.Hue);
    }

    [Fact]
    public void TheStoreSavesAndLoadsDropsGoneHorsesAndRefusesAnUnknownVersion()
    {
        var owner = Player();
        var kept = TamedHorse(owner);
        var gone = TamedHorse(owner);
        Barding(owner, exceptional: true).OnTarget(owner, kept);
        Barding(owner).OnTarget(owner, gone);
        HorseBarding.Get(kept).HP = 777;
        HorseBarding.Get(kept).CraftedBy = "Smith";
        gone.Delete();

        var store = new HorseBardingStore();
        var buffer = new byte[4096];
        var writer = new BufferWriter(buffer, true);
        store.Serialize(writer);
        writer.Flush();

        HorseBardingStore.Rows.Clear();
        store.Deserialize(new BufferReader(buffer));

        Assert.Single(HorseBardingStore.Rows);
        var r = HorseBarding.Get(kept);
        Assert.True(r.Exceptional);
        Assert.Equal(777, r.HP);
        Assert.Equal("Smith", r.CraftedBy);
        Assert.Equal(CraftResource.Valorite, r.Resource);

        var bad = new byte[16];
        var w2 = new BufferWriter(bad, true);
        w2.WriteEncodedInt(HorseBardingStore.CurrentVersion + 1);
        w2.Flush();
        Assert.Throws<InvalidDataException>(() => store.Deserialize(new BufferReader(bad)));
    }

    [Fact]
    public void TheBardingItemSavesAndLoads()
    {
        var b = Track(new CraftedHorseBarding { Exceptional = true, Resource = CraftResource.Agapite, CraftedBy = "Smith" });
        var buffer = new byte[4096];
        var writer = new BufferWriter(buffer, true);
        b.Serialize(writer);
        writer.Flush();
        var copy = (CraftedHorseBarding)Activator.CreateInstance(typeof(CraftedHorseBarding), b.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        Assert.True(copy.Exceptional);
        Assert.Equal(CraftResource.Agapite, copy.Resource);
        Assert.Equal("Smith", copy.CraftedBy);
    }

    [Fact]
    public void ABadSettingsFileIsRefusedWhole()
    {
        var errors = new List<string>();
        Assert.Null(PetSettingsFile.Parse<HorseBardingSettings>("{\"AbsorbPercent\": 150}", HorseBardingSettings.Validate, errors));
        Assert.NotEmpty(errors);

        errors.Clear();
        Assert.Null(PetSettingsFile.Parse<HorseBardingSettings>("not json", HorseBardingSettings.Validate, errors));

        errors.Clear();
        var ok = PetSettingsFile.Parse<HorseBardingSettings>(PetSettingsFile.ToJson(new HorseBardingSettings { Durability = 900 }),
            HorseBardingSettings.Validate, errors);
        Assert.Equal(900, ok.Durability);
    }
}
