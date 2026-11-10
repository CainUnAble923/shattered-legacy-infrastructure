// HornOfPlentyVerification.cs
//
// cc-P859: the Horn of Plenty, Mister Gobbles and the Roasting Pig on a Spit (Nuggzy's port). Notes in shard-migration
// notes/cc-P859-horn-of-plenty.md.
//
// Facts: each roll of the horn gives what it names and spends a charge; an empty horn and a horn outside the pack give
// nothing; the horn refills one charge a day in November only, up to ten; the pig deed asks south or east and builds the
// right pieces; the pig feeds its house's owner once a day and nobody else, and redeeding keeps its clock; the
// Achievement store sells the horn in November only; Clean Up Britannia takes the horn at 2,500; every new type saves.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Engines.Points;
using Server.Engines.RewardStores;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HornOfPlentyVerification : IDisposable
{
    private static readonly Point3D Yard = new(5480, 3870, 0);
    private readonly List<IEntity> _made = [];

    public void Dispose()
    {
        HornOfPlenty.Clock = () => Core.Now;
        HornOfPlenty.Roll = Utility.RandomDouble;
        RoastingPigOnASpitAddon.Roll = Utility.RandomDouble;

        foreach (var e in _made)
        {
            e?.Delete();
        }
    }

    private T Track<T>(T e) where T : IEntity
    {
        _made.Add(e);
        return e;
    }

    // The host has no multi data: a small house whose floor is the 7 x 7 it stands on (as PetPerchVerification).
    private sealed class FloorHouse : SmallOldHouse
    {
        private static readonly MultiComponentList Floor = MakeFloor();

        public FloorHouse(Mobile owner) : base(owner, 0x64)
        {
        }

        private static MultiComponentList MakeFloor()
        {
            var list = new List<MultiTileEntry>();
            for (short x = -3; x <= 3; x++)
            {
                for (short y = -3; y <= 3; y++)
                {
                    list.Add(new MultiTileEntry(0x495, x, y, 0, (TileFlag)1));
                }
            }

            return new MultiComponentList(list);
        }

        public override MultiComponentList Components => Floor;
        public override bool IsAosRules => false;
    }

    private PlayerMobile Player(string name = "Harvester", Point3D? at = null)
    {
        var pm = Track(new PlayerMobile { Player = true, Race = Race.Human, Name = name });
        pm.AddItem(new Backpack());
        pm.MoveToWorld(at ?? Yard, Map.Trammel);
        return pm;
    }

    private HornOfPlenty HornIn(PlayerMobile pm)
    {
        var horn = Track(new HornOfPlenty());
        pm.Backpack.DropItem(horn);
        return horn;
    }

    private static List<Item> Pack(Mobile m, HornOfPlenty horn) => m.Backpack.Items.Where(i => i != horn).ToList();

    private List<BaseCreature> TurkeysAt(Point3D p)
    {
        var found = new List<BaseCreature>();
        foreach (var m in Map.Trammel.GetMobilesAt(p))
        {
            if (m is Turkey t)
            {
                found.Add(Track(t));
            }
        }

        return found;
    }

    [Fact]
    public void EachRollGivesWhatItNamesAndSpendsACharge()
    {
        var pm = Player();
        var horn = HornIn(pm);
        Assert.Equal(10, horn.Charges);
        Assert.Equal(1153503, horn.LabelNumber);
        Assert.Equal(0x4BD9, horn.ItemID);

        // The pig deed (under 9%).
        horn.Use(pm, 0.05);
        Assert.Single(Pack(pm, horn).OfType<RoastingPigOnASpitDeed>());
        Assert.Equal(9, horn.Charges);

        // A turkey (9% to 25%): wild and tamable at the user's feet; 1 in 10 is Mister Gobbles.
        HornOfPlenty.Roll = () => 0.5;
        horn.Use(pm, 0.20);
        var turkey = Assert.Single(TurkeysAt(pm.Location));
        Assert.IsNotType<MisterGobbles>(turkey);
        Assert.True(turkey.Tamable);
        Assert.False(turkey.Controlled);
        turkey.Delete();
        Assert.Equal(8, horn.Charges);

        HornOfPlenty.Roll = () => 0.05;
        horn.Use(pm, 0.20);
        var gobbles = Assert.IsType<MisterGobbles>(Assert.Single(TurkeysAt(pm.Location)));
        Assert.Equal("Mister Gobbles", gobbles.Name);
        Assert.True(gobbles.Tamable);
        Assert.Equal(7, horn.Charges);

        // A nap (25% to 33%): frozen in place.
        horn.Use(pm, 0.30);
        Assert.True(pm.Frozen);
        pm.Frozen = false;
        Assert.Equal(6, horn.Charges);

        // The foods (33% and up), one of each quarter of the band.
        horn.Use(pm, 0.40);
        horn.Use(pm, 0.55);
        horn.Use(pm, 0.75);
        horn.Use(pm, 0.95);
        var pack = Pack(pm, horn);
        Assert.Single(pack.OfType<SweetPotatoPie>());
        Assert.Single(pack.OfType<MashedSweetPotatoes>());
        Assert.Single(pack.OfType<BasketOfRolls>());
        Assert.Single(pack.OfType<TurkeyPlatter>());
        Assert.Equal(2, horn.Charges);

        // Nothing it gives is a skill or stat item: the only things in the pack are the deed and the four foods.
        Assert.Equal(5, pack.Count);
    }

    [Fact]
    public void AnEmptyHornOrOneOutsideThePackGivesNothing()
    {
        HornOfPlenty.Clock = () => new DateTime(2026, 10, 15, 12, 0, 0, DateTimeKind.Utc);

        var pm = Player();
        var horn = HornIn(pm);
        horn.Charges = 0;
        horn.OnDoubleClick(pm);
        Assert.Empty(Pack(pm, horn));
        Assert.Equal(0, horn.Charges);

        var ground = Track(new HornOfPlenty());
        ground.MoveToWorld(pm.Location, pm.Map);
        ground.OnDoubleClick(pm);
        Assert.Equal(10, ground.Charges);
        Assert.Empty(Pack(pm, horn));
    }

    [Fact]
    public void TheHornRefillsOneChargeADayInNovemberOnlyUpToTen()
    {
        var horn = Track(new HornOfPlenty { Charges = 8 });
        var now = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
        HornOfPlenty.Clock = () => now;

        Assert.False(horn.CheckRecharge()); // October
        Assert.Equal(8, horn.Charges);

        now = new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(horn.CheckRecharge());
        Assert.Equal(9, horn.Charges);
        Assert.False(horn.CheckRecharge()); // the same day
        Assert.Equal(9, horn.Charges);

        now = now.AddDays(1);
        Assert.True(horn.CheckRecharge());
        Assert.Equal(10, horn.Charges);

        now = now.AddDays(1);
        Assert.False(horn.CheckRecharge()); // full
        Assert.Equal(10, horn.Charges);

        horn.Charges = 3;
        now = new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(horn.CheckRecharge()); // December
        Assert.Equal(3, horn.Charges);
    }

    [Fact]
    public void ThePigDeedAsksSouthOrEastAndBuildsThatPig()
    {
        var deed = Track(new RoastingPigOnASpitDeed());
        Assert.Equal(1154557, deed.LabelNumber);
        Assert.Equal(LootType.Blessed, deed.LootType);

        var options = new RewardOptionList();
        deed.GetOptions(options);
        Assert.Equal(2, options.Count);

        var south = Track(new RoastingPigOnASpitAddon(false));
        Assert.Equal([0x9995, 0x9994], south.Components.Select(c => c.ItemID).ToArray());
        Assert.Equal(new Point3D(-1, 1, 0), south.Components[1].Offset);
        Assert.Equal(1123329, south.Components[0].LabelNumber);

        var east = Track(new RoastingPigOnASpitAddon(true));
        Assert.Equal([0x9989, 0x9988], east.Components.Select(c => c.ItemID).ToArray());
        Assert.Equal(new Point3D(1, -1, 0), east.Components[1].Offset);
    }

    [Fact]
    public void ThePigFeedsItsOwnerOnceADayAndNobodyElse()
    {
        var owner = Player("Owner");
        var house = Track(new FloorHouse(owner));
        house.MoveToWorld(Yard, Map.Trammel);

        var pig = Track(new RoastingPigOnASpitAddon(true));
        pig.MoveToWorld(new Point3D(Yard.X + 1, Yard.Y, 0), Map.Trammel);
        var spit = pig.Components[0];

        RoastingPigOnASpitAddon.Roll = () => 0.1;
        spit.OnDoubleClick(owner);
        Assert.Single(owner.Backpack.Items.OfType<PulledPorkPlatter>());
        Assert.True(pig.NextEatTime > Core.Now.AddHours(23));

        spit.OnDoubleClick(owner); // not ready yet
        Assert.Single(owner.Backpack.Items.OfType<Food>());

        pig.NextEatTime = Core.Now.AddMinutes(-1);
        RoastingPigOnASpitAddon.Roll = () => 0.9;
        spit.OnDoubleClick(owner);
        Assert.Single(owner.Backpack.Items.OfType<PulledPorkSandwich>());

        // A stranger in the house, and the owner out of reach, get nothing.
        pig.NextEatTime = Core.Now.AddMinutes(-1);
        var stranger = Player("Stranger");
        spit.OnDoubleClick(stranger);
        Assert.Empty(stranger.Backpack.Items.OfType<Food>());

        owner.MoveToWorld(new Point3D(Yard.X + 10, Yard.Y, 0), Map.Trammel);
        spit.OnDoubleClick(owner);
        Assert.Equal(2, owner.Backpack.Items.OfType<Food>().Count());

        // Redeeding keeps the pig's clock.
        var later = Core.Now.AddHours(5);
        pig.NextEatTime = later;
        var back = Track((RoastingPigOnASpitDeed)pig.Deed);
        Assert.Equal(later, back.NextEatTime);
        Assert.Equal(later, ((RoastingPigOnASpitAddon)Track(back.Addon)).NextEatTime);

        var platter = new PulledPorkPlatter();
        var sandwich = new PulledPorkSandwich();
        Assert.Equal((1123351, 5, 0x999F), (platter.LabelNumber, platter.FillFactor, platter.ItemID));
        Assert.Equal((1123352, 3, 0x99A0), (sandwich.LabelNumber, sandwich.FillFactor, sandwich.ItemID));
        platter.Delete();
        sandwich.Delete();
    }

    [Fact]
    public void TheAchievementStoreSellsTheHornInNovemberOnly()
    {
        var row = new AchievementStoreDef().Find("ap.hornofplenty");
        Assert.NotNull(row);
        Assert.NotNull(row!.Condition);
        Assert.Equal("Sold in November only", row.ConditionText);

        HornOfPlenty.Clock = () => new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(row.Condition!(null!));

        HornOfPlenty.Clock = () => new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        Assert.True(row.Condition!(null!));

        var made = row.Create!(null!);
        var horn = Assert.IsType<HornOfPlenty>(Assert.Single(made));
        Track(horn);
        Assert.Equal(10, horn.Charges);
    }

    [Fact]
    public void CleanUpBritanniaTakesTheHornAt2500()
    {
        var horn = Track(new HornOfPlenty());
        Assert.Equal(2500.0, CleanUpBritanniaData.GetPoints(horn));
    }

    [Fact]
    public void EveryNewTypeSavesAndLoads()
    {
        var when = new DateTime(2026, 11, 20, 8, 0, 0, DateTimeKind.Utc);

        var horn = Track(new HornOfPlenty { Charges = 4, NextRecharge = when });
        var hornCopy = RoundTrip(horn);
        Assert.Equal(4, hornCopy.Charges);
        Assert.Equal(when, hornCopy.NextRecharge);

        var pig = Track(new RoastingPigOnASpitAddon(true, when));
        Assert.Equal(when, RoundTrip(pig).NextEatTime);

        var deed = Track(new RoastingPigOnASpitDeed { NextEatTime = when });
        Assert.Equal(when, RoundTrip(deed).NextEatTime);

        Assert.Equal(0x999F, RoundTrip(Track(new PulledPorkPlatter())).ItemID);
        Assert.Equal(0x99A0, RoundTrip(Track(new PulledPorkSandwich())).ItemID);

        var gobbles = Track(new MisterGobbles());
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        gobbles.Serialize(writer);
        writer.Flush();
        var copy = (MisterGobbles)Activator.CreateInstance(typeof(MisterGobbles), gobbles.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        Assert.Equal("Mister Gobbles", copy.Name);
        Assert.True(copy.Tamable);
    }

    private static T RoundTrip<T>(T original) where T : Item
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();
        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }
}
