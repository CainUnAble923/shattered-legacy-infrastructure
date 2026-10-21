// cc-P199 batch AddonsEodon: ServUO Items/Functional automaton parts (customizations/Items/Functional/Automaton/), the
// chicken coop, the house craftables, the strange contraption, the serpent's nest (with its RareSerpentEgg,
// customizations/Services/InstancedPeerless/Medusa/) and the serpent's jawbone.
//
// Facts:
//   1. The automaton parts are ServUO's: actuator, black moonstone, inoperative head, rebuilt heads, statue, broken head.
//   2. The kotl and blackrock automatons are ServUO's: name, body, slots, perception 16, ability chance and abilities,
//      no friends, blackrock's hue and repair resource.
//   3. A metal automaton takes the metal's hue, armour resists on top of 45-50 and elemental damage out of physical;
//      the blackrock automaton ignores a metal.
//   4. Only a Grandmaster tinker commands an automaton (control chance 0 below 100 Tinkering, 1 at 100).
//   5. A rebuilt head activates for a Grandmaster tinker after three seconds into a bonded automaton of its metal; below
//      100 Tinkering nothing happens.
//   6. An owned automaton that dies is stabled away and leaves a broken head; 74 ingots do not repair it, 75 do and
//      bring it back alive at full hits. The broken head cannot be dropped or traded.
//   7. A statue wakes an automaton for a player within 5 tiles (never for staff), goes invisible, and is deleted when its
//      automaton dies.
//   8. The serpent's nest: too far is refused; hatching makes four snakes of ServUO's four kinds and collapses it; the
//      rare serpent egg is a 12-hour blessed key.
//   9. The serpent's jawbone: not locked down it does nothing; locked down it lists 17 places and sends a player to the
//      first, hidden and out of war mode; a murderer is refused.
//  10. The strange contraption: 20 pieces; the mill's entry eats the three ingredients for +500 karma, then refuses for
//      an hour; without them nothing is taken.
//  11. The chicken coop, locked down in a house: only chickens are stabled, three at most; claim brings them back.
//  12. The craftable house item: each kind's art, flipping, the craft entry's name choosing the kind and the
//      ingots/granite the metal; the house-only drop rules.
//  13. The craftable doors: labels, facings, the craft entry's name choosing the door, a flip; the old door deed turns
//      into its door in the same pack (Replace), and the old addon deed into its house item 30 seconds after a load.
//  14. Every saved field survives a serialization round trip (heads, statue, jawbone, coop, house item, doors, deeds,
//      addon, automaton); a statue whose automaton is gone deletes itself after load.
//
// Host notes: timers through ShardTestClock (Arm before anything that schedules, Advance to fire); every fact deletes
// what it made. The host never runs BaseHouse.Configure, so Item.LockedDownFlag is 0 and IsLockedDown cannot be set;
// facts 9 and 11 set it to Configure's value (1) inside the fact and put it back (LockFlag below). The house is the
// synthetic-floor SmallOldHouse of RaisedGardenVerification (the host has no multi data).

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.Craft;
using Server.Engines.Quests.Collector;
using Server.Engines.Quests.Hag;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class P199AddonsEodonVerification
{
    private static readonly Point3D Spot = new(1400, 1720, 0);

    public P199AddonsEodonVerification()
    {
        ShardTestHost.EnsureSkillChecks();
    }

    private static PlayerMobile NewPlayer(Point3D? at = null, Map map = null)
    {
        ShardTestClock.Arm();
        var pm = new PlayerMobile { Player = true, Race = Race.Human, RawStr = 100, RawDex = 100, RawInt = 100 };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(at ?? Spot, map ?? Map.Trammel);
        pm.Hits = pm.HitsMax;
        return pm;
    }

    private static void DeletePlayer(PlayerMobile pm)
    {
        foreach (var item in new List<Item>(pm.Backpack.Items))
        {
            item.Delete();
        }

        pm.Delete();
    }

    private static RelayInfo Button(int id) => new(id, default, default, default, default);

    // Item.LockedDownFlag / SecureFlag at BaseHouse.Configure's values (BaseHouse.cs:1304-1305) for one fact.
    private sealed class LockFlag : IDisposable
    {
        private readonly int _locked = Item.LockedDownFlag;
        private readonly int _secure = Item.SecureFlag;

        public LockFlag()
        {
            Item.LockedDownFlag = 1;
            Item.SecureFlag = 2;
        }

        public void Dispose()
        {
            Item.LockedDownFlag = _locked;
            Item.SecureFlag = _secure;
        }
    }

    // A house with a floor, as RaisedGardenVerification's: the host has no multi data.
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

    private static KotlAutomaton OwnedAutomaton(PlayerMobile pm, KotlAutomaton automaton = null)
    {
        automaton ??= new KotlAutomaton();
        automaton.MoveToWorld(new Point3D(pm.X + 1, pm.Y, pm.Z), pm.Map);
        Assert.True(automaton.SetControlMaster(pm));
        automaton.IsBonded = true;
        return automaton;
    }

    private static void DeleteAutomaton(KotlAutomaton automaton)
    {
        if (automaton == null)
        {
            return;
        }

        automaton.Corpse?.Delete();
        automaton.Delete();
    }

    // ---- 1 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheAutomatonPartsAreServUOs()
    {
        var actuator = new AutomatonActuator();
        var moonstone = new BlackrockMoonstone();
        var inoperative = new InoperativeAutomatonHead();
        var head = new KotlAutomatonHead();
        var blackHead = new BlackrockAutomatonHead();
        var statue = new AutomatonStatue();
        var automaton = new KotlAutomaton();
        var broken = new BrokenAutomatonHead(automaton);
        KotlAutomaton built = null;

        try
        {
            Assert.Equal(0x9CE9, actuator.ItemID);
            Assert.Equal(1156997, actuator.LabelNumber);

            Assert.Equal(0x9CAA, moonstone.ItemID);
            Assert.Equal(1175, moonstone.Hue);
            Assert.Equal(1156993, moonstone.LabelNumber);

            Assert.Equal(0x9DB1, inoperative.ItemID);
            Assert.Equal(1157002, inoperative.LabelNumber);

            Assert.Equal(0x9DB1, head.ItemID);
            Assert.Equal(LootType.Blessed, head.LootType);
            Assert.Equal(CraftResource.Iron, head.Resource);
            Assert.Equal(CraftResources.GetHue(CraftResource.Iron), head.Hue);

            Assert.Equal(0x9DB1, blackHead.ItemID);
            Assert.Equal(1175, blackHead.Hue);
            Assert.Equal(1157220, blackHead.LabelNumber);
            built = blackHead.GetAutomaton(null);
            Assert.IsType<BlackrockAutomaton>(built);

            Assert.Contains(statue.ItemID, new[] { 0x9DB3, 0x9DB4 });
            Assert.Equal(1124395, statue.LabelNumber);
            Assert.False(statue.Movable);
            Assert.True(statue.HandlesOnMovement);
            Assert.Null(statue.Automaton);

            Assert.Equal(0x9DB1, broken.ItemID);
            Assert.Equal(LootType.Blessed, broken.LootType);
            Assert.Equal(1.0, broken.DefaultWeight);
            Assert.True(broken.Nontransferable);
            Assert.Same(automaton, broken.Automaton);
            Assert.Equal(typeof(IronIngot), broken.RepairResource);
            Assert.Equal(75, BrokenAutomatonHead.RepairAmount);
        }
        finally
        {
            actuator.Delete();
            moonstone.Delete();
            inoperative.Delete();
            head.Delete();
            blackHead.Delete();
            statue.Delete();
            broken.Delete();
            automaton.Delete();
            built?.Delete();
        }
    }

    // ---- 2 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheKotlAndBlackrockAutomatonsAreServUOs()
    {
        var kotl = new KotlAutomaton();
        var black = new BlackrockAutomaton();

        try
        {
            Assert.Equal("kotl automaton", kotl.Name);
            Assert.Equal(1406, kotl.Body.BodyID);
            Assert.Equal(541, kotl.BaseSoundID);
            Assert.Equal(4, kotl.ControlSlots);
            Assert.Equal(14000, kotl.Fame);
            Assert.Equal(-14000, kotl.Karma);
            Assert.Equal(16, kotl.RangePerception);
            Assert.InRange(kotl.RawStr, 793, 875);
            Assert.InRange(kotl.HitsMax, 774, 876);
            Assert.Equal(CraftResource.Iron, kotl.Resource);
            Assert.Equal(typeof(IronIngot), kotl.RepairResource);
            Assert.Equal(0.33, kotl.WeaponAbilityChance);
            Assert.False(kotl.AllowNewPetFriend);
            Assert.True(kotl.DeleteOnRelease);
            Assert.True(kotl.BardImmune);
            Assert.True(kotl.BleedImmune);
            Assert.Same(Poison.Lethal, kotl.PoisonImmune);
            Assert.Equal(FoodType.None, kotl.FavoriteFood);
            Assert.False(kotl.IsScaredOfScaryThings);

            var kotlAbilities = new HashSet<WeaponAbility>();
            var blackAbilities = new HashSet<WeaponAbility>();

            for (var i = 0; i < 300; i++)
            {
                kotlAbilities.Add(kotl.GetWeaponAbility());
                blackAbilities.Add(black.GetWeaponAbility());
            }

            Assert.Equal(new HashSet<WeaponAbility> { WeaponAbility.ParalyzingBlow, WeaponAbility.Disarm }, kotlAbilities);
            Assert.Equal(
                new HashSet<WeaponAbility> { WeaponAbility.ParalyzingBlow, WeaponAbility.Disarm, WeaponAbility.ArmorPierce },
                blackAbilities
            );

            Assert.Equal("blackrock automaton", black.Name);
            Assert.Equal(0x8497, black.Hue);
            Assert.InRange(black.RawStr, 800, 900);
            Assert.InRange(black.PhysicalResistanceSeed, 55, 60);
            Assert.Equal(typeof(CrystallineBlackrock), black.RepairResource);
            Assert.Equal(0.45, black.WeaponAbilityChance);
            Assert.Equal(4, black.ControlSlots);
        }
        finally
        {
            kotl.Delete();
            black.Delete();
        }
    }

    // ---- 3 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void AMetalAutomatonTakesItsMetalsHueResistsAndDamage()
    {
        var kotl = new KotlAutomaton();
        var black = new BlackrockAutomaton();

        try
        {
            var attrs = CraftResources.GetInfo(CraftResource.Valorite).AttributeInfo;
            kotl.Resource = CraftResource.Valorite;

            Assert.Equal(0x8000 | CraftResources.GetHue(CraftResource.Valorite), kotl.Hue);
            Assert.InRange(kotl.PhysicalResistanceSeed, 45 + attrs.ArmorPhysicalResist, 50 + attrs.ArmorPhysicalResist);
            Assert.InRange(kotl.FireResistSeed, 45 + attrs.ArmorFireResist, 50 + attrs.ArmorFireResist);
            Assert.InRange(kotl.EnergyResistSeed, 45 + attrs.ArmorEnergyResist, 50 + attrs.ArmorEnergyResist);
            Assert.Equal(attrs.WeaponFireDamage, kotl.FireDamage);
            Assert.Equal(attrs.WeaponEnergyDamage, kotl.EnergyDamage);
            Assert.Equal(
                100 - attrs.WeaponFireDamage - attrs.WeaponColdDamage - attrs.WeaponPoisonDamage - attrs.WeaponEnergyDamage,
                kotl.PhysicalDamage
            );
            Assert.Equal(typeof(ValoriteIngot), kotl.RepairResource);

            black.Resource = CraftResource.Valorite;
            Assert.Equal(0x8497, black.Hue);
            Assert.Equal(100, black.PhysicalDamage);
        }
        finally
        {
            kotl.Delete();
            black.Delete();
        }
    }

    // ---- 4 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void OnlyAGrandmasterTinkerCommandsAnAutomaton()
    {
        var pm = NewPlayer();
        var kotl = new KotlAutomaton();

        try
        {
            pm.Skills.Tinkering.Base = 99.9;
            Assert.Equal(0, kotl.GetControlChance(pm));

            pm.Skills.Tinkering.Base = 100.0;
            Assert.Equal(1.0, kotl.GetControlChance(pm));
        }
        finally
        {
            kotl.Delete();
            DeletePlayer(pm);
        }
    }

    // ---- 5 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARebuiltHeadActivatesForAGrandmasterTinkerAfterThreeSeconds()
    {
        var pm = NewPlayer();
        var head = new KotlAutomatonHead { Resource = CraftResource.Valorite };
        pm.Backpack.DropItem(head);
        KotlAutomaton built = null;

        try
        {
            pm.Skills.Tinkering.Base = 99.0;
            head.OnDoubleClick(pm);
            ShardTestClock.Advance(TimeSpan.FromSeconds(4));
            Assert.False(head.Deleted);
            Assert.DoesNotContain(World.Mobiles.Values.OfType<KotlAutomaton>(), a => a.ControlMaster == pm);

            pm.Skills.Tinkering.Base = 100.0;
            head.OnDoubleClick(pm);
            head.OnDoubleClick(pm); // "already being activated": no second automaton
            ShardTestClock.Advance(TimeSpan.FromSeconds(2));
            Assert.False(head.Deleted);

            ShardTestClock.Advance(TimeSpan.FromSeconds(2));
            Assert.True(head.Deleted);

            var mine = World.Mobiles.Values.OfType<KotlAutomaton>().Where(a => a.ControlMaster == pm).ToList();
            Assert.Single(mine);
            built = mine[0];
            Assert.True(built.Controlled);
            Assert.True(built.IsBonded);
            Assert.Equal(CraftResource.Valorite, built.Resource);
            Assert.Equal(0x8000 | CraftResources.GetHue(CraftResource.Valorite), built.Hue);
            // Placed on the player's tile (ServUO KotlAutomatonHead.cs:58); its AI may take a step before this reads it.
            Assert.True(built.InRange(pm.Location, 2), $"automaton at {built.Location}, player at {pm.Location}");
        }
        finally
        {
            built?.Delete();
            head.Delete();
            DeletePlayer(pm);
        }
    }

    // ---- 6 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void AnOwnedAutomatonLeavesABrokenHeadAndIsRepairedWith75Ingots()
    {
        var pm = NewPlayer();
        pm.Skills.Tinkering.Base = 100.0;
        var automaton = OwnedAutomaton(pm);

        try
        {
            automaton.Kill();

            var broken = pm.Backpack.FindItemByType<BrokenAutomatonHead>();
            Assert.NotNull(broken);
            Assert.Same(automaton, broken.Automaton);
            Assert.True(automaton.IsStabled);
            Assert.Equal(Map.Internal, automaton.Map);
            Assert.Equal(BaseCreature.MaxLoyalty, automaton.Loyalty);

            Assert.False(broken.DropToWorld(pm, pm.Location));
            Assert.False(broken.AllowSecureTrade(pm, pm, pm, true));

            var short74 = new IronIngot(74);
            pm.Backpack.DropItem(short74);
            Assert.False(broken.TryRepair(pm));
            Assert.Equal(74, pm.Backpack.GetAmount(typeof(IronIngot)));
            Assert.False(broken.Deleted);

            pm.Backpack.DropItem(new IronIngot(1));
            Assert.True(broken.TryRepair(pm));
            Assert.Equal(0, pm.Backpack.GetAmount(typeof(IronIngot)));
            Assert.True(broken.Deleted);
            Assert.False(automaton.IsDeadPet);
            Assert.Equal(automaton.HitsMax, automaton.Hits);
            Assert.Equal(pm.Map, automaton.Map);
            Assert.Equal(pm.Location, automaton.Location);
        }
        finally
        {
            DeleteAutomaton(automaton);
            DeletePlayer(pm);
        }
    }

    // ---- 7 ------------------------------------------------------------------------------------------------------

    [Fact]
    public void AStatueWakesAnAutomatonForAPlayerAndGoesWithIt()
    {
        var pm = NewPlayer();
        var statue = new AutomatonStatue();
        statue.MoveToWorld(new Point3D(Spot.X + 3, Spot.Y, Spot.Z), Map.Trammel);
        KotlAutomaton woke = null;

        try
        {
            pm.AccessLevel = AccessLevel.GameMaster;
            for (var i = 0; i < 100; i++)
            {
                statue.OnMovement(pm, pm.Location);
            }

            Assert.Null(statue.Automaton);

            pm.AccessLevel = AccessLevel.Player;
            for (var i = 0; i < 100 && statue.Automaton == null; i++)
            {
                statue.OnMovement(pm, pm.Location);
            }

            woke = statue.Automaton;
            Assert.NotNull(woke);
            Assert.False(statue.Visible);
            Assert.False(statue.HandlesOnMovement);
            Assert.Equal(statue.Location, woke.Location);
            Assert.False(woke.Controlled);
            Assert.Same(statue, AutomatonStatue.Statues[woke]);

            ShardTestClock.Advance(TimeSpan.FromMilliseconds(100));
            Assert.Same(pm, woke.Combatant);

            AutomatonStatue.OnDeath(woke);
            Assert.True(statue.Deleted);
        }
        finally
        {
            if (woke != null)
            {
                AutomatonStatue.Statues?.Remove(woke);
                woke.Delete();
            }

            statue.Delete();
            DeletePlayer(pm);
        }
    }

    // ---- 8 ------------------------------------------------------------------------------------------------------

    private static bool IsNestSnake(Mobile m) => m is LavaSnake or Snake or CoralSnake or GiantSerpent;

    [Fact]
    public void TheSerpentNestHatchesFourSnakesAndTheRareEggIsATwelveHourKey()
    {
        var pm = NewPlayer();
        var nest = new SerpentNest();
        var far = new SerpentNest();
        var egg = new RareSerpentEgg();
        nest.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        far.MoveToWorld(new Point3D(Spot.X + 5, Spot.Y, Spot.Z), Map.Trammel);
        var before = World.Mobiles.Values.Where(IsNestSnake).ToHashSet();
        var hatched = new List<Mobile>();

        try
        {
            Assert.Equal(0x2233, nest.ItemID);
            Assert.Equal(0x456, nest.Hue);
            Assert.False(nest.Movable);
            Assert.Equal(1112582, nest.LabelNumber);

            far.OnDoubleClick(pm);
            Assert.False(far.Deleted);
            Assert.Empty(pm.Backpack.Items);

            var nestAt = nest.Location;
            nest.HatchEggs(pm);
            Assert.True(nest.Deleted);
            hatched.AddRange(World.Mobiles.Values.Where(m => IsNestSnake(m) && !before.Contains(m)));
            Assert.Equal(4, hatched.Count);
            Assert.All(hatched, s => Assert.True(Math.Abs(s.X - nestAt.X) <= 1 && Math.Abs(s.Y - nestAt.Y) <= 1));

            Assert.Equal(0x41BF, egg.ItemID);
            Assert.Equal(1112575, egg.LabelNumber);
            Assert.Equal(LootType.Blessed, egg.LootType);
            Assert.Equal(43200, egg.Lifespan);
            Assert.Equal(43200, egg.TimeLeft);
            Assert.Contains(egg.Hue, new[] { 0x21, 0x4AC, 0x41C });
        }
        finally
        {
            foreach (var s in hatched)
            {
                s.Delete();
            }

            nest.Delete();
            far.Delete();
            egg.Delete();
            DeletePlayer(pm);
        }
    }

    // ---- 9 ------------------------------------------------------------------------------------------------------

    private static BaseGump JawboneGump(PlayerMobile pm)
    {
        foreach (var g in pm.GetGumps())
        {
            if (g.GetType().DeclaringType == typeof(SerpentsJawbone))
            {
                return g;
            }
        }

        return null;
    }

    [Fact]
    public void TheJawboneSendsAPlayerToIlshenarAndRefusesAMurderer()
    {
        using var flags = new LockFlag();
        var pm = NewPlayer();
        var jawbone = new SerpentsJawbone();
        jawbone.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;

        try
        {
            Assert.Equal(0x9F74, jawbone.ItemID);
            Assert.Equal(1157654, jawbone.LabelNumber);
            Assert.Equal(17, SerpentsJawbone.Locations.Count);
            Assert.Equal(1157135, SerpentsJawbone.Locations.Keys.First());

            jawbone.OnDoubleClick(pm);
            Assert.Null(JawboneGump(pm));

            jawbone.IsLockedDown = true;
            Assert.True(jawbone.IsLockedDown);
            jawbone.OnDoubleClick(pm);
            var gump = JawboneGump(pm);
            Assert.NotNull(gump);

            // A test NetState cannot cross facets (port recipe, trap 2): the reply is sent with it detached.
            ns.Mobile = null;
            pm.NetState = null;

            pm.Kills = 5;
            gump.OnResponse(null, Button(1157135));
            Assert.Equal(Map.Trammel, pm.Map);

            pm.Kills = 0;
            pm.Warmode = true;
            gump.OnResponse(null, Button(1157135));
            Assert.Equal(Map.Ilshenar, pm.Map);
            Assert.Equal(new Point3D(1156, 1143, -24), pm.Location);
            Assert.True(pm.Hidden);
            Assert.False(pm.Warmode);
        }
        finally
        {
            ns.Mobile = null;
            pm.NetState = null;
            ns.Dispose();
            jawbone.Delete();
            DeletePlayer(pm);
        }
    }

    // ---- 10 -----------------------------------------------------------------------------------------------------

    private static void ClickDevice(StrangeContraptionComponent mill, PlayerMobile pm)
    {
        var list = PooledRefList<ContextMenuEntry>.Create();

        try
        {
            mill.GetContextMenuEntries(pm, ref list);

            ContextMenuEntry device = null;
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Number == 3006190)
                {
                    device = list[i];
                }
            }

            Assert.NotNull(device);
            device.OnClick(pm, mill);
        }
        finally
        {
            list.Dispose();
        }
    }

    [Fact]
    public void TheStrangeContraptionEatsThreeIngredientsForKarmaOncePerHour()
    {
        var pm = NewPlayer();
        var addon = new StrangeContraptionAddon();
        addon.MoveToWorld(new Point3D(Spot.X - 2, Spot.Y, Spot.Z), Map.Trammel);

        try
        {
            Assert.Equal(20, addon.Components.Count);
            var mill = addon.Components.OfType<StrangeContraptionComponent>().Single(c => c.ItemID == 6434);
            Assert.Equal("a strange contraption", mill.Name);
            Assert.True(pm.InRange(mill.Location, 2));

            pm.Karma = 0;
            var core = new PlagueBeastMutationCore();
            var obsidian = new Obsidian();
            pm.Backpack.DropItem(core);
            pm.Backpack.DropItem(obsidian);
            ClickDevice(mill, pm);
            Assert.Equal(0, pm.Karma);
            Assert.False(core.Deleted || obsidian.Deleted);

            var brew = new MoonfireBrew();
            pm.Backpack.DropItem(brew);
            ClickDevice(mill, pm);
            Assert.Equal(500, pm.Karma);
            Assert.True(core.Deleted && obsidian.Deleted && brew.Deleted);
            Assert.True(StrangeContraptionAddon.Table.ContainsKey(pm));

            var core2 = new PlagueBeastMutationCore();
            var obsidian2 = new Obsidian();
            var brew2 = new MoonfireBrew();
            pm.Backpack.DropItem(core2);
            pm.Backpack.DropItem(obsidian2);
            pm.Backpack.DropItem(brew2);
            ClickDevice(mill, pm);
            Assert.Equal(500, pm.Karma);
            Assert.False(core2.Deleted || obsidian2.Deleted || brew2.Deleted);
        }
        finally
        {
            StrangeContraptionAddon.Table.Remove(pm);
            addon.Delete();
            DeletePlayer(pm);
        }
    }

    // ---- 11 -----------------------------------------------------------------------------------------------------

    private static BaseCreature Tamed<T>(PlayerMobile pm) where T : BaseCreature, new()
    {
        var bc = new T();
        bc.MoveToWorld(new Point3D(pm.X, pm.Y + 1, pm.Z), pm.Map);
        Assert.True(bc.SetControlMaster(pm));
        return bc;
    }

    [Fact]
    public void TheChickenCoopStablesThreeChickensAndGivesThemBack()
    {
        using var flags = new LockFlag();
        var at = new Point3D(1400, 1700, 0);
        var pm = NewPlayer(new Point3D(at.X + 1, at.Y, at.Z), Map.Felucca);
        pm.FollowersMax = 20;
        var house = new TestHouse(pm);
        house.MoveToWorld(at, Map.Felucca);
        var coop = new ChickenCoop();
        coop.MoveToWorld(at, Map.Felucca);
        var pets = new List<BaseCreature>();

        try
        {
            Assert.Equal(0x4513, coop.ItemID);
            Assert.Equal(1112570, coop.LabelNumber);
            Assert.Equal(SecureLevel.CoOwners, coop.Level);
            Assert.Equal(20.0, coop.DefaultWeight);
            Assert.Same(house, BaseHouse.FindHouseAt(coop));
            Assert.True(coop.CheckAccess(pm));

            // Not locked down: no target.
            coop.BeginStable(pm);
            Assert.Null(pm.Target);

            coop.IsLockedDown = true;

            var horse = Tamed<Horse>(pm);
            pets.Add(horse);
            coop.BeginStable(pm);
            Assert.NotNull(pm.Target);
            pm.Target.Invoke(pm, horse);
            Assert.False(coop.Stored.ContainsKey(pm));
            Assert.True(horse.Controlled);

            var chicken = Tamed<Chicken>(pm);
            var lizard = Tamed<ChickenLizard>(pm);
            var battle = Tamed<BattleChickenLizard>(pm);
            var fourth = Tamed<Chicken>(pm);
            pets.AddRange(new[] { chicken, lizard, battle, fourth });

            coop.BeginStable(pm);
            pm.Target.Invoke(pm, chicken);
            coop.EndStable(pm, lizard);
            coop.EndStable(pm, battle);
            coop.EndStable(pm, fourth); // no more room

            Assert.Equal(new[] { chicken, lizard, battle }, coop.Stored[pm]);
            Assert.All(coop.Stored[pm], c =>
                {
                    Assert.True(c.IsStabled);
                    Assert.False(c.Controlled);
                    Assert.Equal(Map.Internal, c.Map);
                }
            );
            Assert.True(fourth.Controlled);
            Assert.False(fourth.IsStabled);

            coop.Claim(pm);
            Assert.Empty(coop.Stored[pm]);
            foreach (var c in new[] { chicken, lizard, battle })
            {
                Assert.False(c.IsStabled);
                Assert.True(c.Controlled);
                Assert.Same(pm, c.ControlMaster);
                Assert.Equal(Map.Felucca, c.Map);
            }
        }
        finally
        {
            foreach (var p in pets)
            {
                p.Delete();
            }

            coop.Delete();
            house.Delete();
            DeletePlayer(pm);
        }
    }

    // ---- 12 -----------------------------------------------------------------------------------------------------

    [Fact]
    public void TheCraftableHouseItemTakesItsKindAndMetalFromTheCraft()
    {
        var made = new List<Item>();

        try
        {
            foreach (var type in Enum.GetValues<CraftableItemType>())
            {
                var item = new CraftableHouseItem(type);
                made.Add(item);
                Assert.Equal(CraftableHouseItem.IDs[(int)type][1], item.ItemID);
                Assert.Equal(type, item.ItemType);
            }

            var wall = (CraftableHouseItem)made[(int)CraftableItemType.RoughWindowless];
            Assert.True(wall.CanFlip);
            foreach (var next in new[] { 465, 466, 463, 464 })
            {
                wall.OnFlip(null);
                Assert.Equal(next, wall.ItemID);
            }

            var paver = (CraftableHouseItem)made[(int)CraftableItemType.LightPaver];
            Assert.False(paver.CanFlip);
            paver.OnFlip(null);
            Assert.Equal(1305, paver.ItemID);

            // ServUO DefMasonry.cs:238 (RoughArch, cliloc 1155799) and :318 ("Medium Paver"); DefTinkering.cs:480.
            var crafted = new CraftableHouseItem();
            made.Add(crafted);
            var arch = new CraftItem(typeof(CraftableHouseItem), 1155792, 1155799);
            Assert.Equal(1, crafted.OnCraft(1, false, null, null, typeof(ValoriteGranite), null, arch, 0));
            Assert.Equal(CraftableItemType.RoughArch, crafted.ItemType);
            Assert.Equal(469, crafted.ItemID);
            Assert.Equal(CraftResource.Valorite, crafted.Resource);
            Assert.Equal(CraftResources.GetHue(CraftResource.Valorite), crafted.Hue);

            var paverCraft = new CraftItem(typeof(CraftableHouseItem), 1155877, "Medium Paver");
            crafted.OnCraft(1, false, null, null, typeof(Granite), null, paverCraft, 0);
            Assert.Equal(CraftableItemType.MediumPaver, crafted.ItemType);
            Assert.Equal(1309, crafted.ItemID);
            Assert.Equal(CraftResource.Iron, crafted.Resource);

            var hanger = new CraftItem(typeof(CraftableHouseItem), 1044050, 1155852);
            crafted.OnCraft(1, false, null, null, typeof(IronIngot), null, hanger, 0);
            Assert.Equal(CraftableItemType.FlourishedMetalSignHanger, crafted.ItemType);

            // House only: not into a movable container, not onto a pack animal's pack.
            var bag = new Bag();
            made.Add(bag);
            Assert.True(bag.Movable);
            Assert.False(wall.OnDroppedInto(null, bag, Point3D.Zero));
        }
        finally
        {
            foreach (var item in made)
            {
                item.Delete();
            }
        }
    }

    // ---- 13 -----------------------------------------------------------------------------------------------------

    [Fact]
    public void TheCraftableDoorsAndTheOldDeedsAreServUOs()
    {
        var pm = NewPlayer();
        var made = new List<Item>();

        try
        {
            var metal = new CraftableMetalHouseDoor(DoorType.LeftMetalDoor_S_In, DoorFacing.WestCCW);
            var stone = new CraftableStoneHouseDoor(DoorType.StoneDoor_E_Out, DoorFacing.SouthCW);
            made.Add(metal);
            made.Add(stone);

            Assert.Equal(1156080, metal.LabelNumber);
            Assert.Equal(0x675 + 2 * (int)DoorFacing.WestCCW, metal.ItemID);
            Assert.True(metal.Movable);
            Assert.Equal(1156079, stone.LabelNumber);
            Assert.Equal(0x324 + 2 * (int)DoorFacing.SouthCW, stone.ItemID);
            Assert.True(stone.Movable);

            Assert.Equal(DoorFacing.EastCW, CraftableMetalHouseDoor.GetDoorFacing(DoorType.StoneDoor_S_In));
            Assert.Equal(DoorFacing.NorthCCW, CraftableMetalHouseDoor.GetDoorFacing(DoorType.RightMetalDoor_E_Out));
            Assert.Equal(DoorType.RightMetalDoor_E_Out, CraftableMetalHouseDoor.Flip(DoorType.LeftMetalDoor_S_Out));

            // ServUO DefTinkering.cs:507 (1156083 RightMetalDoor_E_Out) and DefMasonry.cs:277 (1156349 StoneDoor_E_In).
            var made1 = (CraftableMetalHouseDoor)CraftableMetalHouseDoor.Create(
                pm, new CraftItem(typeof(CraftableMetalHouseDoor), 1044050, 1156083), null
            );
            var made2 = (CraftableStoneHouseDoor)CraftableStoneHouseDoor.Create(
                pm, new CraftItem(typeof(CraftableStoneHouseDoor), 1155792, 1156349), null
            );
            made.Add(made1);
            made.Add(made2);
            Assert.Equal(DoorType.RightMetalDoor_E_Out, made1.Type);
            Assert.Equal(DoorFacing.NorthCCW, made1.Facing);
            Assert.Equal(DoorType.StoneDoor_E_In, made2.Type);
            Assert.Equal(DoorFacing.NorthCW, made2.Facing);

            metal.OnCraft(1, false, pm, null, typeof(ValoriteIngot), null, null, 0);
            Assert.Equal(CraftResource.None, metal.Resource); // ServUO: no craft item, no resource
            metal.OnCraft(1, false, pm, null, typeof(ValoriteIngot), null, new CraftItem(typeof(CraftableMetalHouseDoor), 1044050, 1156080), 0);
            Assert.Equal(CraftResource.Valorite, metal.Resource);
            Assert.Equal(CraftResources.GetHue(CraftResource.Valorite), metal.Hue);

            metal.OnFlip(pm);
            Assert.Equal(DoorType.LeftMetalDoor_S_Out, metal.Type);
            Assert.Equal(DoorFacing.WestCW, metal.Facing);
            Assert.Equal(0x675 + 2 * (int)DoorFacing.WestCW, metal.ClosedId);
            Assert.Equal(0x676 + 2 * (int)DoorFacing.WestCW, metal.OpenedId);
            Assert.Equal(BaseDoor.GetOffset(DoorFacing.WestCW), metal.Offset);

            // The old deed in a pack turns into its door in the same pack.
            var deed = new CraftableHouseDoorDeed(DoorType.RightMetalDoor_S_In) { Resource = CraftResource.Valorite };
            Assert.Equal(0x14F0, deed.ItemID);
            pm.Backpack.DropItem(deed);
            deed.Replace();
            Assert.True(deed.Deleted);
            var door = pm.Backpack.FindItemByType<CraftableMetalHouseDoor>();
            Assert.NotNull(door);
            Assert.Equal(DoorType.RightMetalDoor_S_In, door.Type);
            Assert.Equal(CraftResource.Valorite, door.Resource);

            var stoneDeed = new CraftableHouseDoorDeed(DoorType.StoneDoor_S_Out);
            pm.Backpack.DropItem(stoneDeed);
            stoneDeed.Replace();
            var stoneDoor = pm.Backpack.FindItemByType<CraftableStoneHouseDoor>();
            Assert.NotNull(stoneDoor);
            Assert.Equal(DoorType.StoneDoor_S_Out, stoneDoor.Type);

            // The old addon deed, 30 seconds after a load, turns into the house item of its kind in the same pack.
            var addonDeed = new CraftableHouseAddonDeed { ItemType = CraftableItemType.RoughSteps };
            pm.Backpack.DropItem(addonDeed);
            var copy = ConsumablesVerification.RoundTrip(addonDeed, s => new CraftableHouseAddonDeed(s));
            Assert.Equal(CraftableItemType.RoughSteps, copy.ItemType);
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(100));
            ShardTestClock.Advance(TimeSpan.FromSeconds(31));
            Assert.True(copy.Deleted);
            var piece = pm.Backpack.FindItemByType<CraftableHouseItem>();
            Assert.NotNull(piece);
            Assert.Equal(CraftableItemType.RoughSteps, piece.ItemType);
            Assert.Equal(1929, piece.ItemID);
        }
        finally
        {
            foreach (var item in made)
            {
                item.Delete();
            }

            DeletePlayer(pm);
        }
    }

    // ---- 14 -----------------------------------------------------------------------------------------------------

    [Fact]
    public void EverySavedFieldSurvivesARoundTrip()
    {
        var pm = NewPlayer();
        var automaton = new KotlAutomaton();
        var gone = new KotlAutomaton();
        var chicken = Tamed<Chicken>(pm);
        var copies = new List<Item>();
        KotlAutomaton automatonCopy = null;

        try
        {
            var head = new KotlAutomatonHead { Resource = CraftResource.Valorite };
            var headCopy = ConsumablesVerification.RoundTrip(head, s => new KotlAutomatonHead(s));
            copies.Add(headCopy);
            Assert.Equal(CraftResource.Valorite, headCopy.Resource);

            var broken = new BrokenAutomatonHead(automaton);
            var brokenCopy = ConsumablesVerification.RoundTrip(broken, s => new BrokenAutomatonHead(s));
            copies.Add(brokenCopy);
            Assert.Same(automaton, brokenCopy.Automaton);

            var statue = new AutomatonStatue { Automaton = automaton };
            var statueCopy = ConsumablesVerification.RoundTrip(statue, s => new AutomatonStatue(s));
            copies.Add(statueCopy);
            Assert.Same(automaton, statueCopy.Automaton);

            var lost = new AutomatonStatue { Automaton = gone };
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            lost.Serialize(writer);
            writer.Flush();
            var lostSerial = lost.Serial;
            lost.Delete();
            gone.Delete();
            var lostCopy = new AutomatonStatue(lostSerial);
            copies.Add(lostCopy);
            lostCopy.Deserialize(new BufferReader(buffer));
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(100));
            Assert.True(lostCopy.Deleted); // ServUO: had an automaton that no longer reads back

            var jawbone = new SerpentsJawbone { Level = SecureLevel.Owner };
            var jawboneCopy = ConsumablesVerification.RoundTrip(jawbone, s => new SerpentsJawbone(s));
            copies.Add(jawboneCopy);
            Assert.Equal(SecureLevel.Owner, jawboneCopy.Level);

            // By hand, not RoundTrip: deleting the original coop would delete the chicken it holds (ServUO's Delete).
            var coop = new ChickenCoop { Level = SecureLevel.Friends };
            coop.Stored[pm] = new List<BaseCreature> { chicken };
            var coopBuffer = new byte[65536];
            var coopWriter = new BufferWriter(coopBuffer, true);
            coop.Serialize(coopWriter);
            coopWriter.Flush();
            var coopSerial = coop.Serial;
            coop.Stored.Clear();
            coop.Delete();
            var coopCopy = new ChickenCoop(coopSerial);
            copies.Add(coopCopy);
            coopCopy.Deserialize(new BufferReader(coopBuffer));
            Assert.Equal(SecureLevel.Friends, coopCopy.Level);
            Assert.Equal(new[] { chicken }, coopCopy.Stored[pm]);
            Assert.True(chicken.IsStabled);

            var piece = new CraftableHouseItem(CraftableItemType.RoughArch) { Resource = CraftResource.Valorite };
            var pieceCopy = ConsumablesVerification.RoundTrip(piece, s => new CraftableHouseItem(s));
            copies.Add(pieceCopy);
            Assert.Equal(CraftableItemType.RoughArch, pieceCopy.ItemType);
            Assert.Equal(CraftResource.Valorite, pieceCopy.Resource);

            var metal = new CraftableMetalHouseDoor(DoorType.RightMetalDoor_E_In, DoorFacing.NorthCW)
                { Resource = CraftResource.Agapite };
            var metalCopy = ConsumablesVerification.RoundTrip(metal, s => new CraftableMetalHouseDoor(s));
            copies.Add(metalCopy);
            Assert.Equal(DoorType.RightMetalDoor_E_In, metalCopy.Type);
            Assert.Equal(CraftResource.Agapite, metalCopy.Resource);
            Assert.Equal(DoorFacing.NorthCW, metalCopy.Facing);

            var stone = new CraftableStoneHouseDoor(DoorType.StoneDoor_E_In, DoorFacing.NorthCW)
                { Resource = CraftResource.Verite };
            var stoneCopy = ConsumablesVerification.RoundTrip(stone, s => new CraftableStoneHouseDoor(s));
            copies.Add(stoneCopy);
            Assert.Equal(DoorType.StoneDoor_E_In, stoneCopy.Type);
            Assert.Equal(CraftResource.Verite, stoneCopy.Resource);

            var deed = new CraftableHouseDoorDeed(DoorType.LeftMetalDoor_E_Out) { Resource = CraftResource.Gold };
            var deedCopy = ConsumablesVerification.RoundTrip(deed, s => new CraftableHouseDoorDeed(s));
            copies.Add(deedCopy);
            Assert.Equal(DoorType.LeftMetalDoor_E_Out, deedCopy.Type);
            Assert.Equal(CraftResource.Gold, deedCopy.Resource);

            var addonDeed = new CraftableHouseAddonDeed { ItemType = CraftableItemType.DarkPaver };
            var addonDeedCopy = ConsumablesVerification.RoundTrip(addonDeed, s => new CraftableHouseAddonDeed(s));
            copies.Add(addonDeedCopy);
            Assert.Equal(CraftableItemType.DarkPaver, addonDeedCopy.ItemType);

            var addon = new CraftableHouseAddon { ItemType = CraftableItemType.GateEast };
            var addonCopy = ConsumablesVerification.RoundTrip(addon, s => new CraftableHouseAddon(s));
            copies.Add(addonCopy);
            Assert.Equal(CraftableItemType.GateEast, addonCopy.ItemType);

            // The copies are deleted before any conversion timer (30 s) can run; the conversions skip a deleted item.
            foreach (var c in copies)
            {
                c.Delete();
            }

            automaton.Resource = CraftResource.Valorite;
            var hue = automaton.Hue;
            var physical = automaton.PhysicalResistanceSeed;
            var mobBuffer = new byte[65536];
            var mobWriter = new BufferWriter(mobBuffer, true);
            automaton.Serialize(mobWriter);
            mobWriter.Flush();
            var mobSerial = automaton.Serial;
            automaton.Delete();
            automatonCopy = new KotlAutomaton(mobSerial);
            automatonCopy.Deserialize(new BufferReader(mobBuffer));
            Assert.Equal(CraftResource.Valorite, automatonCopy.Resource);
            Assert.Equal(hue, automatonCopy.Hue);
            Assert.Equal(physical, automatonCopy.PhysicalResistanceSeed); // the load does not re-roll the metal's resists
        }
        finally
        {
            foreach (var c in copies)
            {
                c.Delete();
            }

            automaton.Delete();
            gone.Delete();
            chicken.Delete();
            automatonCopy?.Delete();
            DeletePlayer(pm);
        }
    }
}
