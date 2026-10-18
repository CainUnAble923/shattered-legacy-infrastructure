// cc-P127: monster stealing (ServUO Services/Monster Stealing -> customizations/Services/Monster Stealing/, reached from
// pinned Skills/Stealing.cs through Stealing-monster-stealing.patch).
//
// Facts:
//   1. ServUO's chance table (kept beside ours for the register, cc-P142): 2 at 100, 5 from 100.1, 8 from 110.1, 10 at
//      120, plus 1 to 5 by the creature's fame level; and the fame line and the gain ceiling.
//   2. A Felucca dungeon is OSI's place; the same dungeon on Trammel is a place of ours (cc-P142: Journeyman), open land
//      none (the ladder itself is MonsterStealingShardRulesVerification).
//   3. A Den member at 120 in a Felucca dungeon steals a special item from a famous creature within a few hundred tries,
//      a creature gives once (cc-P142: once it has given; a miss leaves it open), a tame creature gives nothing, and
//      below 60 nothing happens.
//   4. The shard's rule: a monster steal rolls Stealing on (100, 100 + 2.5 x fame level), so a thief at 150 gains past
//      pinned's 127.5 ceiling against a 30,000-fame creature.
//   5. The balms: one effect at a time, +10 to the stat while it lasts, gone when it ends; Stone Skin's resistances.
//   6. The seed of life and the mana draught heal 25-40 once in ten minutes; the tool box hands out 5-12 lockpicks a use
//      and takes one use back a day; the Gem of Salvation waits out its six hours.
//   7. A balm's effect type and the tool box's uses survive a serialization round trip.
//
// Host notes: no regions.json, so a DungeonRegion is registered where a fact needs one (the Shame facts' note). Hit and
// mana points are read on the tick they change (the port recipe's rule).

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.CreatureStealing;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class MonsterStealingVerification
{
    private readonly ITestOutputHelper _out;

    public MonsterStealingVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
    }

    private static readonly Point3D FelSpot = new(4410, 1210, 0);
    private static readonly Rectangle3D Den = new(new Point3D(4400, 1200, -128), new Point3D(4440, 1240, 127));

    private sealed class Thief : IDisposable
    {
        public readonly Account Account = new($"p127{Guid.NewGuid():N}"[..16], "p127-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<Mobile> Marks = new();
        private readonly Region _den;

        public Thief(double stealing, Map map, bool den = true, bool member = true)
        {
            ShardTestClock.Arm();
            if (den)
            {
                _den = new DungeonRegion("P127 den", map, 50, Den);
                _den.Register();
            }

            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Pm.Skills[SkillName.Stealing].Cap = 200.0;
            Pm.Skills[SkillName.Stealing].Base = stealing;
            Account[0] = Pm;
            Pm.MoveToWorld(FelSpot, map);

            if (member)
            {
                // cc-P142: monster stealing needs the Thieves' Den; an Initiate works OSI's places.
                var data = ClusterFAccountPersistence.GetOrCreateGuild(Pm);
                data.JoinedGuilds.Add(ThievesDenStealRules.GuildKey);
                data.GuildReputation[ThievesDenStealRules.GuildKey] = 0;
            }

            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public BaseCreature Mark(int fame)
        {
            var bc = new Skeleton { Fame = fame };
            bc.MoveToWorld(new Point3D(FelSpot.X + 1, FelSpot.Y, 0), Pm.Map);
            Marks.Add(bc);
            return bc;
        }

        public T Pack<T>(T item) where T : Item
        {
            Pm.Backpack.DropItem(item);
            return item;
        }

        public void Dispose()
        {
            foreach (var item in Pm.Backpack.Items.ToList())
            {
                item.Delete();
            }

            Marks.ForEach(m => m.Delete());
            BaseThieveConsumable.EndEffect(Pm);
            Pm.NetState = null;
            Ns.Mobile = null;
            Pm.Delete();
            Ns.Dispose();
            _den?.Unregister();
        }
    }

    private static readonly Type[] PlainPrizes = StealingHandler.SpecialItemList.Take(StealingHandler.SpecialItemList.Length - 2).ToArray();

    [Fact]
    public void TheChanceTableIsServUOsBesideOurs()
    {
        var low = new Skeleton { Fame = 1 };
        var high = new Skeleton { Fame = 30000 };
        var mid = new Skeleton { Fame = 18752 }; // level 25 (ServUO's line gives 24.9995 at 18,750 and truncates)

        try
        {
            Assert.Equal(0, StealingHandler.FameLevel(low));
            Assert.Equal(40, StealingHandler.FameLevel(high));
            Assert.Equal(25, StealingHandler.FameLevel(mid));

            Assert.Equal(2, StealingHandler.GetServUOStealingChance(null, low, 100.0));
            Assert.Equal(5, StealingHandler.GetServUOStealingChance(null, low, 100.1));
            Assert.Equal(5, StealingHandler.GetServUOStealingChance(null, low, 110.0));
            Assert.Equal(8, StealingHandler.GetServUOStealingChance(null, low, 110.1));
            Assert.Equal(8, StealingHandler.GetServUOStealingChance(null, low, 119.9));
            Assert.Equal(10, StealingHandler.GetServUOStealingChance(null, low, 120.0));
            Assert.Equal(15, StealingHandler.GetServUOStealingChance(null, high, 120.0));
            Assert.Equal(11, StealingHandler.GetServUOStealingChance(null, mid, 120.0));
            Assert.Equal(0, StealingHandler.GetServUOStealingChance(null, low, 99.9));

            // Ours (cc-P142) is the live one: the same fame bonus on a line to 200.
            Assert.Equal(16.0, StealingHandler.GetStealingChance(null, high, 120.0), 3);
            Assert.Equal(1, StealingHandler.FameBonus(mid));

            Assert.Equal(200.0, StealingHandler.GainCeiling(high));
            Assert.Equal(100.0, StealingHandler.GainCeiling(low));
            Assert.Equal(162.5, StealingHandler.GainCeiling(mid));
        }
        finally
        {
            low.Delete();
            high.Delete();
            mid.Delete();
        }
    }

    [Fact]
    public void AFeluccaDungeonIsOSIsPlaceAndTrammelsIsOurs()
    {
        using (var fel = new Thief(120.0, Map.Felucca))
        {
            Assert.True(fel.Pm.Region.IsPartOf<DungeonRegion>(), "the fact's den is registered on Felucca");
            Assert.Equal(StealPlace.FeluccaDungeon, ThievesDenStealRules.PlaceOf(fel.Pm));
            Assert.True(ThievesDenStealRules.CheckPlaceAndRank(fel.Pm, fel.Mark(1), out _, out _), "an Initiate works OSI's places");
        }

        using (var tram = new Thief(120.0, Map.Trammel))
        {
            Assert.True(tram.Pm.Region.IsPartOf<DungeonRegion>());
            Assert.Equal(StealPlace.FacetDungeon, ThievesDenStealRules.PlaceOf(tram.Pm));
            Assert.False(ThievesDenStealRules.CheckPlaceAndRank(tram.Pm, tram.Mark(1), out _, out var line), "the same dungeon on Trammel needs a Journeyman");
            Assert.NotNull(line);
        }

        using (var open = new Thief(120.0, Map.Felucca, den: false))
        {
            Assert.False(open.Pm.Region.IsPartOf<DungeonRegion>(), "the dens are unregistered on dispose");
            Assert.Equal(StealPlace.None, ThievesDenStealRules.PlaceOf(open.Pm));
            Assert.False(ThievesDenStealRules.CheckPlaceAndRank(open.Pm, open.Mark(1), out _, out var line), "Felucca, no dungeon");
            Assert.Null(line);
        }
    }

    [Fact]
    public void AThiefAt120StealsASpecialItemOncePerCreature()
    {
        using var t = new Thief(120.0, Map.Felucca);

        Item prize = null;
        var tries = 0;
        BaseCreature last = null;

        while (prize == null && tries < 400)
        {
            tries++;
            last = t.Mark(30000);
            StealingHandler.HandleSteal(last, t.Pm);
            Assert.Equal(StealingHandler.LastOutcome == StealingHandler.Outcome.StoleSpecial, StealingHandler.HasBeenStolen(last)); // cc-P142: a success marks, a miss leaves it open
            prize = t.Pm.Backpack.Items.FirstOrDefault(i => PlainPrizes.Contains(i.GetType()));
        }

        Assert.NotNull(prize);
        _out.WriteLine($"{prize.GetType().Name} on try {tries} (16% a try at 120 against fame 30,000; four in five successes an OSI item)");

        // Once per creature: no second prize from the same one.
        var before = t.Pm.Backpack.Items.Count;
        for (var i = 0; i < 50; i++)
        {
            StealingHandler.HandleSteal(last, t.Pm);
        }

        Assert.Equal(before, t.Pm.Backpack.Items.Count);

        // A tame creature gives nothing and is not marked.
        var pet = t.Mark(30000);
        pet.Controlled = true;
        StealingHandler.HandleSteal(pet, t.Pm);
        Assert.False(StealingHandler.HasBeenStolen(pet));

        // Below 60 nothing happens either (cc-P142: the lower tier starts at 60, where OSI starts at 100).
        t.Pm.Skills[SkillName.Stealing].Base = 59.9;
        var novice = t.Mark(30000);
        StealingHandler.HandleSteal(novice, t.Pm);
        Assert.Equal(StealingHandler.Outcome.TooLittleSkill, StealingHandler.LastOutcome);
        Assert.False(StealingHandler.HasBeenStolen(novice));
    }

    [Fact]
    public void AMonsterStealTrainsStealingPastPinnedsCeiling()
    {
        using var t = new Thief(150.0, Map.Felucca);
        var skill = t.Pm.Skills[SkillName.Stealing];
        var start = skill.Base;
        Assert.True(start > 127.5, "pinned's best pack steal gains to 127.5");

        var was = StealingHandler.MonsterGain;
        StealingHandler.MonsterGain = true;

        try
        {
            for (var i = 0; i < 150 && skill.Base <= start; i++)
            {
                StealingHandler.HandleSteal(t.Mark(30000), t.Pm);
            }

            Assert.True(skill.Base > start, $"Stealing stayed at {skill.Base} after 150 monster steals");
            _out.WriteLine($"Stealing {start} -> {skill.Base}");

            // With the shard's rule off, 80 more steals gain nothing: ServUO's handler rolls no skill check.
            StealingHandler.MonsterGain = false;
            var held = skill.Base;

            for (var i = 0; i < 80; i++)
            {
                StealingHandler.HandleSteal(t.Mark(30000), t.Pm);
            }

            Assert.Equal(held, skill.Base);
        }
        finally
        {
            StealingHandler.MonsterGain = was;
        }
    }

    [Fact]
    public void OneBalmAtATimeAndItWearsOff()
    {
        using var t = new Thief(100.0, Map.Felucca);
        t.Pm.RawStr = 50;
        t.Pm.RawInt = 40;

        var strength = t.Pack(new BalmOfStrength());
        var wisdom = t.Pack(new BalmOfWisdom());

        strength.OnDoubleClick(t.Pm);

        Assert.True(strength.Deleted, "the balm is used up");
        Assert.Equal(60, t.Pm.Str);
        Assert.True(BaseThieveConsumable.IsUnderThieveConsumableEffect(t.Pm, ThieveConsumableEffect.BalmOfStrengthEffect));

        wisdom.OnDoubleClick(t.Pm);
        Assert.False(wisdom.Deleted, "a second balm is refused while the first lasts");
        Assert.Equal(40, t.Pm.Int);

        BaseThieveConsumable.EndEffect(t.Pm);
        Assert.Equal(50, t.Pm.Str);
        Assert.Equal(ThieveConsumableEffect.None, BaseThieveConsumable.CheckThieveConsumable(t.Pm));

        wisdom.OnDoubleClick(t.Pm);
        Assert.True(wisdom.Deleted);
        Assert.Equal(50, t.Pm.Int);
        BaseThieveConsumable.EndEffect(t.Pm);
        Assert.Equal(40, t.Pm.Int);
    }

    [Fact]
    public void StoneSkinTradesFireAndColdForPhysical()
    {
        using var t = new Thief(100.0, Map.Felucca);
        var phys = t.Pm.PhysicalResistance;
        var fire = t.Pm.FireResistance;
        var cold = t.Pm.ColdResistance;

        var lotion = t.Pack(new StoneSkinLotion());
        lotion.OnDoubleClick(t.Pm);

        Assert.True(lotion.Deleted);
        Assert.Equal(phys + 30, t.Pm.PhysicalResistance);
        Assert.Equal(fire - 5, t.Pm.FireResistance);
        Assert.Equal(cold - 5, t.Pm.ColdResistance);

        BaseThieveConsumable.EndEffect(t.Pm);
        Assert.Equal(phys, t.Pm.PhysicalResistance);
        Assert.Equal(fire, t.Pm.FireResistance);
        Assert.Equal(cold, t.Pm.ColdResistance);
    }

    [Fact]
    public void TheSeedAndTheDraughtHealOnceInTenMinutes()
    {
        using var t = new Thief(100.0, Map.Felucca);
        t.Pm.RawStr = 100;
        t.Pm.RawInt = 100;

        var seeds = t.Pack(new SeedOfLife(2));
        t.Pm.Hits = t.Pm.HitsMax - 50;
        var hits = t.Pm.Hits;
        seeds.OnDoubleClick(t.Pm);
        var healed = t.Pm.Hits - hits;
        Assert.InRange(healed, 25, 40);
        Assert.Equal(1, seeds.Amount);

        t.Pm.Hits = t.Pm.HitsMax - 50;
        seeds.OnDoubleClick(t.Pm);
        Assert.Equal(1, seeds.Amount); // ten minutes between uses
        Assert.False(SeedOfLife.CheckUse(t.Pm));

        var draught = t.Pack(new ManaDraught());
        t.Pm.Mana = t.Pm.ManaMax - 50;
        var mana = t.Pm.Mana;
        draught.OnDoubleClick(t.Pm);
        Assert.InRange(t.Pm.Mana - mana, 25, 40);
        Assert.True(draught.Deleted);
        Assert.False(ManaDraught.CheckUse(t.Pm));
        _out.WriteLine($"seed healed {healed}, draught restored {t.Pm.Mana - mana}");
    }

    [Fact]
    public void TheToolBoxHandsOutLockpicksAndRechargesDaily()
    {
        using var t = new Thief(100.0, Map.Felucca);
        var box = t.Pack(new SmugglersToolBox());
        Assert.Equal(10, box.UsesRemaining);
        box.NextRecharge = Core.Now + TimeSpan.FromHours(1); // the daily top-up is not due yet

        box.OnDoubleClick(t.Pm);

        var picks = t.Pm.Backpack.Items.OfType<Lockpick>().Single();
        Assert.InRange(picks.Amount, 5, 12);
        Assert.Equal(9, box.UsesRemaining);

        box.NextRecharge = Core.Now - TimeSpan.FromHours(1);
        box.Recharge();
        Assert.Equal(10, box.UsesRemaining);
        Assert.True(box.NextRecharge > Core.Now + TimeSpan.FromHours(23));

        box.Recharge();
        Assert.Equal(10, box.UsesRemaining); // once a day
    }

    [Fact]
    public void TheGemWaitsOutItsSixHours()
    {
        using var t = new Thief(100.0, Map.Felucca);
        var gem = t.Pack(new GemOfSalvation());
        Assert.Equal(LootType.Blessed, gem.LootType);

        // Dead: a ghost body, as the recipe says (Kill() would delete an account-less player).
        t.Pm.Body = 0x192;
        Assert.False(t.Pm.Alive);

        GemOfSalvation.SetNextUse(t.Pm, Core.Now + TimeSpan.FromHours(2));
        GemOfSalvation.Offer(t.Pm, gem);
        Assert.False(gem.Deleted);
        Assert.False(t.Pm.HasGump<GemResurrectGump>());

        GemOfSalvation.SetNextUse(t.Pm, DateTime.MinValue);
        GemOfSalvation.Offer(t.Pm, gem);
        Assert.True(t.Pm.HasGump<GemResurrectGump>(), "the offer is a gump");
        Assert.False(gem.Deleted, "the gem goes only when the player accepts");
    }

    [Fact]
    public void ABalmAndTheToolBoxSurviveASerializationRoundTrip()
    {
        var balm = new BalmOfSwiftness();
        var box = new SmugglersToolBox { UsesRemaining = 4 };
        var balmCopy = new BalmOfSwiftness(balm.Serial);
        var boxCopy = new SmugglersToolBox(box.Serial);

        try
        {
            var buffer = new byte[16384];
            var writer = new BufferWriter(buffer, true);
            balm.Serialize(writer);
            writer.Flush();
            balmCopy.Deserialize(new BufferReader(buffer));

            Assert.Equal(ThieveConsumableEffect.BalmOfSwiftnessEffect, balmCopy.EffectType);
            Assert.Equal(TimeSpan.FromMinutes(30), balmCopy.EffectDuration);

            var buffer2 = new byte[16384];
            var writer2 = new BufferWriter(buffer2, true);
            box.Serialize(writer2);
            writer2.Flush();
            boxCopy.Deserialize(new BufferReader(buffer2));

            Assert.Equal(4, boxCopy.UsesRemaining);
            Assert.Equal(box.NextRecharge, boxCopy.NextRecharge);
        }
        finally
        {
            balm.Delete();
            box.Delete();
            balmCopy.Delete();
            boxCopy.Delete();
        }
    }
}
