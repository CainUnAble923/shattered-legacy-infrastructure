// MuleModuleVerification.cs
//
// cc-P70, F-33: the Compact Mule's modules. Notes in shard-migration notes/cc-P70-mule-modules.md.
//
// Facts:
//   1. The data: 17 modules, one per guild, three tiers (a setting), each with a craft row and unique recipe ids.
//   2. Attach and detach: owner only, a free slot, one per guild, the module's tier at most the mule's; refused, it stays
//      with the player; removed, it keeps its tier.
//   3. Crafting: every tier is a recipe in its craft system; a crafted module takes its id and tier; tier II needs, and
//      on success takes, a tier I of the same kind.
//   4. Schematics: membership, rank, Mining Vouchers and the guild's currency (Custodians: Clean Up points) gate the
//      purchase; buying teaches the recipe.
//   5. Hauling: Ore Hopper and Timber Rack lighten their goods; Saddlebags add items; the Load Harness adds weight; the
//      Survey Saddle Roll's map case weighs nothing.
//   6. Survival: Trailwise Tack (stamina, warning list, Homing, 0 slots), Field Medic (heal, cure), Expedition Barding,
//      Arcane Ward (resist, absorb, stable), Soul Tether (pack kept, skills kept).
//   7. Utility: the Smelter's forge and anvil follow the mule; Survey (reveal, tracking, the mule digs); Shadow Cloak;
//      Salvage Bin (trash, pickup); Minstrel's Bells (calm, bard ease).
//   8. Merchants and Artificers: the Ledger sells cargo and gates the bank; the Socket's properties; Steady Hoof Wraps is
//      stubbed and says so.
//   9. Satchels: from tier 3, Unload sends the contents to the mule within reach.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class MuleModuleVerification
{
    private readonly ITestOutputHelper _out;

    public MuleModuleVerification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D Spot = new(3460, 2604, 18);

    private sealed class Owner : IDisposable
    {
        public readonly Account Account = new($"p70m{Guid.NewGuid():N}"[..16], "p70-test-only");
        public readonly PlayerMobile Pm;
        public readonly PackMule Mule;

        public Owner(int muleTier = 3)
        {
            MuleModules.Configure(); // the test host runs no Configure
            Pm = new PlayerMobile { Player = true, Race = Race.Human };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);
            Mule = PackMule.SummonFor(Pm, muleTier, MuleGenes.Fresh(), "here");
        }

        public CharacterGuildData Guild => ClusterFAccountPersistence.GetOrCreate(Account).GetOrCreateGuildData(Pm.Serial);

        public MuleModule Fit(MuleModuleId id, int tier)
        {
            var module = new MuleModule(id, tier);
            Assert.True(MuleModules.TryAttach(Pm, Mule, module), $"{id} {tier} attaches");
            return module;
        }

        public void Dispose()
        {
            Mule?.Delete();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    [Fact]
    public void TheDataHasSeventeenModulesOnePerGuildEachWithACraftAndRecipes()
    {
        Assert.Equal(17, MuleModuleData.Modules.Length);
        Assert.Equal(17, MuleModuleData.Modules.Select(m => m.GuildKey).Distinct().Count());
        Assert.Equal(3, MuleModuleData.MaxTier);
        Assert.All(MuleModuleData.Modules, m => Assert.Equal(3, m.Effects.Length));
        Assert.All(MuleModuleData.Modules, m => Assert.NotNull(MuleModuleCrafts.Row(m.Id)));

        var ids = MuleModuleData.Modules.SelectMany(m => Enumerable.Range(1, 3).Select(t => MuleModuleCrafts.RecipeId(m.Id, t))).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.All(ids, id => Assert.True(id > 9000));

        for (var tier = 1; tier <= 3; tier++)
        {
            Assert.Equal(MuleData.Tier(tier).Hue, MuleModuleData.HueFor(tier));
            Assert.Equal(MuleData.Tier(tier).Hue, new MuleModule(MuleModuleId.OreHopper, tier).Hue);
        }

        Assert.Equal(MuleModuleState.Stubbed, MuleModuleData.Info(MuleModuleId.SteadyHoofWraps).State);
    }

    [Fact]
    public void AttachIsOwnerOnlyNeedsASlotOnePerGuildAndAHighEnoughMule()
    {
        using var o = new Owner(muleTier: 2);
        var stranger = new PlayerMobile { Player = true };

        try
        {
            var hopper = new MuleModule(MuleModuleId.OreHopper, 1);
            o.Pm.Backpack!.DropItem(hopper);
            Assert.NotNull(MuleModules.RefuseAttach(stranger, o.Mule, hopper));

            var tooHigh = new MuleModule(MuleModuleId.TimberRack, 3);
            o.Pm.Backpack.DropItem(tooHigh);
            Assert.False(MuleModules.TryAttach(o.Pm, o.Mule, tooHigh));
            Assert.False(tooHigh.Deleted);
            Assert.True(tooHigh.IsChildOf(o.Pm.Backpack)); // refused: never deleted, stays with the player

            Assert.True(MuleModules.TryAttach(o.Pm, o.Mule, hopper));
            Assert.Same(Map.Internal, hopper.Map);
            Assert.Equal(1, MuleModules.TierOf(o.Mule, MuleModuleId.OreHopper));

            Assert.False(MuleModules.TryAttach(o.Pm, o.Mule, new MuleModule(MuleModuleId.OreHopper, 1))); // one per guild
            Assert.True(MuleModules.TryAttach(o.Pm, o.Mule, new MuleModule(MuleModuleId.TimberRack, 2)));
            Assert.False(MuleModules.TryAttach(o.Pm, o.Mule, new MuleModule(MuleModuleId.ReinforcedSaddlebags, 1))); // 2 slots at tier 2

            Assert.True(MuleModules.Detach(o.Pm, o.Mule, hopper));
            Assert.True(hopper.IsChildOf(o.Pm.Backpack));
            Assert.Equal(1, hopper.Tier);
            Assert.Equal(0, MuleModules.TierOf(o.Mule, MuleModuleId.OreHopper));
        }
        finally
        {
            stranger.Delete();
        }
    }

    [Fact]
    public void EveryTierIsARecipeInItsCraftSystemAndTierTwoTakesATierOne()
    {
        MuleModuleCrafts.Register();

        foreach (var row in MuleModuleCrafts.Rows)
        {
            var system = row.System();
            Assert.NotNull(system);

            for (var tier = 1; tier <= 3; tier++)
            {
                var entry = MuleModuleCrafts.Map.Single(kv => kv.Value == (row.Id, tier));
                Assert.Equal(typeof(MuleModule), entry.Key.ItemType);
                Assert.NotNull(entry.Key.Recipe);
                Assert.Equal(MuleModuleCrafts.RecipeId(row.Id, tier), entry.Key.Recipe.ID);
            }
        }

        var craft = MuleModuleCrafts.Map.Single(kv => kv.Value == (MuleModuleId.FieldMedicPack, 2)).Key;
        var made = new MuleModule();
        made.OnCraft(1, false, null, null, null, null, craft, 0);
        Assert.Equal(MuleModuleId.FieldMedicPack, made.ModuleId);
        Assert.Equal(2, made.Tier);

        using var o = new Owner();
        TextDefinition message = null;
        Assert.False(MuleModuleCrafts.CheckPreviousTier(o.Pm, craft, false, ref message));
        _out.WriteLine(message?.ToString());

        var one = new MuleModule(MuleModuleId.FieldMedicPack, 1);
        o.Pm.Backpack!.DropItem(one);
        Assert.True(MuleModuleCrafts.CheckPreviousTier(o.Pm, craft, false, ref message));
        Assert.False(one.Deleted); // a check takes nothing
        Assert.True(MuleModuleCrafts.CheckPreviousTier(o.Pm, craft, true, ref message));
        Assert.True(one.Deleted); // a successful craft takes it

        var firstTier = MuleModuleCrafts.Map.Single(kv => kv.Value == (MuleModuleId.FieldMedicPack, 1)).Key;
        Assert.True(MuleModuleCrafts.CheckPreviousTier(o.Pm, firstTier, true, ref message));
        made.Delete();
    }

    [Fact]
    public void SchematicsAreGatedByMembershipRankAndCurrencyAndTeachTheRecipe()
    {
        using var o = new Owner();
        var g = o.Guild;

        Assert.NotNull(MuleSchematics.RefuseBuy(o.Pm, g, MuleModuleId.ClockworkLoadHarness, 1)); // not a member
        g.JoinedGuilds.Add("tinkers");
        g.GuildReputation["tinkers"] = 0;
        Assert.Contains("rank", MuleSchematics.RefuseBuy(o.Pm, g, MuleModuleId.ClockworkLoadHarness, 1));
        g.GuildReputation["tinkers"] = 1_000; // Apprentice
        Assert.Contains("Mining Vouchers", MuleSchematics.RefuseBuy(o.Pm, g, MuleModuleId.ClockworkLoadHarness, 1));
        g.GuildCurrency["mining"] = 5;
        Assert.NotNull(MuleSchematics.RefuseBuy(o.Pm, g, MuleModuleId.ClockworkLoadHarness, 1)); // no tinkers scrip
        g.GuildCurrency["tinkers"] = 20;
        Assert.Null(MuleSchematics.RefuseBuy(o.Pm, g, MuleModuleId.ClockworkLoadHarness, 1));
        Assert.NotNull(MuleSchematics.RefuseBuy(o.Pm, g, MuleModuleId.ClockworkLoadHarness, 2)); // Journeyman for II

        Assert.True(MuleSchematics.Buy(o.Pm, g, MuleModuleId.ClockworkLoadHarness, 1));
        Assert.True(o.Pm.HasRecipe(MuleModuleCrafts.RecipeId(MuleModuleId.ClockworkLoadHarness, 1)));
        Assert.Equal(0, g.GuildCurrency["mining"]);
        Assert.Equal(0, g.GuildCurrency["tinkers"]);
        Assert.Contains("already", MuleSchematics.RefuseBuy(o.Pm, g, MuleModuleId.ClockworkLoadHarness, 1));

        // The Custodians pay in Clean Up points.
        g.JoinedGuilds.Add("custodians");
        g.GuildReputation["custodians"] = 1_000_000;
        g.GuildCurrency["mining"] = 5;
        Assert.Contains("Clean Up points", MuleSchematics.RefuseBuy(o.Pm, g, MuleModuleId.SalvageBin, 1));
    }

    [Fact]
    public void HaulingModulesLightenTheirGoodsAndAddRoom()
    {
        using var o = new Owner(muleTier: 3);
        var pack = (PackMuleBackpack)o.Mule.Backpack!;
        pack.DropItem(new IronIngot(100));
        pack.DropItem(new Log(100));
        var raw = pack.TotalWeight;
        _out.WriteLine($"raw {raw}");

        o.Fit(MuleModuleId.OreHopper, 2);
        Assert.Equal(raw - 5, pack.TotalWeight); // 100 ingots weigh 10 stones; at 50%, 5 are saved
        o.Fit(MuleModuleId.TimberRack, 1);
        Assert.Equal(raw - 5 - 50, pack.TotalWeight); // 100 logs weigh 200 stones; at 25%, 50 are saved

        var items = o.Mule.PackItemsCap;
        o.Fit(MuleModuleId.ReinforcedSaddlebags, 1);
        Assert.Equal(items + 50, o.Mule.PackItemsCap);

        var weight = o.Mule.PackWeightCap;
        o.Mule.StaffSetTier(5);
        weight = o.Mule.PackWeightCap;
        o.Fit(MuleModuleId.ClockworkLoadHarness, 2);
        Assert.Equal(weight + weight * 20 / 100, o.Mule.PackWeightCap);

        o.Fit(MuleModuleId.SurveySaddleRoll, 1);
        var before = pack.TotalWeight;
        pack.DropItem(new SOS());
        Assert.Equal(before, pack.TotalWeight); // the map case: SOS bottles weigh nothing
    }

    [Fact]
    public void SurvivalModulesWork()
    {
        using var o = new Owner(muleTier: 3);
        var plainStam = o.Mule.StamMax;

        // Trailwise Tack: stamina, Homing, then 0 follower slots at III.
        o.Fit(MuleModuleId.TrailwiseTack, 3);
        Assert.Equal(plainStam + plainStam * 20 / 100, o.Mule.Numbers.Stam);
        Assert.True(o.Mule.Numbers.StamRegen > 0);
        Assert.Equal(0, o.Mule.ControlSlots);
        Assert.Equal(0, o.Pm.Followers);

        o.Pm.MoveToWorld(new Point3D(Spot.X + 30, Spot.Y, Spot.Z), Map.Trammel);
        var rt = MuleModules.State(o.Mule);
        var now = DateTime.UtcNow;
        Assert.False(MuleModules.Homing(o.Mule, o.Pm, rt, now));
        Assert.False(MuleModules.Homing(o.Mule, o.Pm, rt, now.AddSeconds(1)));
        Assert.True(MuleModules.Homing(o.Mule, o.Pm, rt, now.AddSeconds(4)));
        Assert.Equal(o.Pm.Location, o.Mule.Location);

        // Field Medic: heals out of combat.
        o.Fit(MuleModuleId.FieldMedicPack, 2);
        o.Mule.Hits = o.Mule.HitsMax / 2;
        var hurt = o.Mule.Hits;
        MuleModules.Tick(o.Mule, DateTime.UtcNow.AddMinutes(1));
        Assert.True(o.Mule.Hits > hurt);

        // Expedition Barding: 30% less damage at III.
        o.Fit(MuleModuleId.ExpeditionBarding, 3);
        Assert.Equal(70, MuleModules.ScaleIncoming(o.Mule, 100));

        // Soul Tether II: the pack is kept on death; III: no skill loss (restored after the caller's decrement).
        Assert.Equal(DeathMoveResult.MoveToCorpse, o.Mule.GetInventoryMoveResultFor(new Gold(1)));
        o.Mule.StaffSetTier(5);
        o.Fit(MuleModuleId.SoulTether, 3);
        Assert.Equal(DeathMoveResult.MoveToBackpack, o.Mule.GetInventoryMoveResultFor(new Gold(1)));
        var wrestling = o.Mule.Skills.Wrestling.Base;
        o.Mule.OnBeforeResurrect();
        var saved = o.Mule.SkillsBeforeResurrect;
        Assert.NotNull(saved);
        o.Mule.Skills.Wrestling.Base -= 0.1; // as PetResurrectGump does after ResurrectPet returns
        o.Mule.RestoreSkills(saved);
        Assert.Equal(wrestling, o.Mule.Skills.Wrestling.Base);
    }

    [Fact]
    public void TheArcaneWardResistsAbsorbsAndSendsTheMuleHome()
    {
        using var o = new Owner(muleTier: 3);
        var resist = o.Mule.Numbers.MagicResist;
        o.Fit(MuleModuleId.ArcaneWardHarness, 3);
        Assert.Equal(resist + 15, o.Mule.Numbers.MagicResist);

        var now = DateTime.UtcNow;
        var damage = 40;
        Assert.True(MuleModules.WardAbsorb(o.Mule, ref damage, now));
        Assert.Equal(0, damage);
        damage = 40;
        Assert.False(MuleModules.WardAbsorb(o.Mule, ref damage, now.AddSeconds(10))); // recharging
        Assert.Equal(40, damage);
        Assert.True(MuleModules.WardAbsorb(o.Mule, ref damage, now.AddSeconds(61)));

        o.Mule.Backpack!.DropItem(new Gold(77));
        o.Mule.Hits = o.Mule.HitsMax / 10;
        Assert.True(MuleModules.WardRecallTick(o.Mule, o.Pm));
        Assert.True(o.Mule.IsStabled);
        Assert.Contains(o.Mule, o.Pm.Stabled);
        Assert.Equal(77, o.Mule.Backpack.GetAmount(typeof(Gold)));
    }

    [Fact]
    public void UtilityModulesWork()
    {
        using var o = new Owner(muleTier: 3);

        // Field Smelter Rig II and III: a forge, then an anvil, beside the mule.
        o.Fit(MuleModuleId.FieldSmelterRig, 3);
        var rt = MuleModules.State(o.Mule);
        Assert.NotNull(rt.Forge);
        Assert.NotNull(rt.Anvil);
        Assert.Equal(o.Mule.Location, rt.Forge.Location);
        Assert.True(Attribute.IsDefined(typeof(MuleForgeSpot), typeof(ForgeAttribute)));
        Assert.True(Attribute.IsDefined(typeof(MuleAnvilSpot), typeof(AnvilAttribute)));

        // Survey Saddle Roll: the reveal, tracking a decoded map, and the mule digging it.
        Assert.Equal(0, MuleModules.ExploreBonus(o.Pm));
        o.Fit(MuleModuleId.SurveySaddleRoll, 3);
        Assert.Equal(1, MuleModules.ExploreBonus(o.Pm));

        var tmap = new TreasureMap(1, Map.Trammel) { Decoder = o.Pm, ChestMap = Map.Trammel, ChestLocation = new Point2D(Spot.X + 2, Spot.Y) };
        o.Mule.Backpack!.DropItem(tmap);
        Assert.Same(tmap, MuleModules.Track(o.Mule, o.Pm));
        var chest = MuleModules.Dig(o.Mule, o.Pm);
        Assert.NotNull(chest);
        Assert.True(tmap.Completed);
        Assert.Equal(new Point2D(Spot.X + 2, Spot.Y), new Point2D(chest.X, chest.Y));
        foreach (var g in chest.Guardians ?? new System.Collections.Generic.List<Mobile>())
        {
            g.Delete();
        }

        chest.Delete();

        // Shadow Cloak I: hides when the owner hides.
        o.Fit(MuleModuleId.ShadowCloak, 2);
        o.Pm.Hidden = true;
        MuleModules.Tick(o.Mule, DateTime.UtcNow);
        Assert.True(o.Mule.Hidden);
        o.Pm.Hidden = false;
        o.Mule.Hidden = false;
    }

    [Fact]
    public void TheSalvageBinTrashesAndPicksUpAndTheBellsCalmAndEase()
    {
        using var o = new Owner(muleTier: 3);
        var bin = o.Fit(MuleModuleId.SalvageBin, 2);

        var shirt = new Shirt();
        o.Pm.Backpack!.DropItem(shirt);
        var eligible = ClusterFCustodianSystem.IsEligible(shirt, false);
        _out.WriteLine($"a shirt is eligible for Clean Up: {eligible}");
        Assert.Equal(eligible, MuleModules.Trash(o.Pm, o.Mule, shirt));

        bin.ToggleFilter("reagents");
        var reagent = new BlackPearl(5);
        reagent.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        var gold = new Gold(5);
        gold.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y + 1, Spot.Z), Map.Trammel);
        Assert.Equal(1, MuleModules.PickupTick(o.Mule, o.Pm, MuleModules.State(o.Mule), DateTime.UtcNow.AddMinutes(1)));
        Assert.True(reagent.IsChildOf(o.Mule.Backpack));
        Assert.Null(gold.Parent); // not in the filter
        gold.Delete();

        o.Fit(MuleModuleId.MinstrelsBells, 3);
        var rat = new Rat();
        rat.MoveToWorld(Spot, Map.Trammel);
        MuleModules.OnMuleDamaged(o.Mule, rat, false, roll: 0.0);
        Assert.True(rat.BardPacified);
        rat.Delete();

        Assert.Equal(10, MuleModules.BardSkillEase(o.Pm, SkillName.Peacemaking));
        Assert.Equal(0, MuleModules.BardSkillEase(o.Pm, SkillName.Mining));
    }

    [Fact]
    public void TheLedgerSellsCargoAndGatesTheBankTheSocketHoldsPropertiesAndTheWrapsAreStubbed()
    {
        using var o = new Owner(muleTier: 3);
        o.Fit(MuleModuleId.TradersLedger, 3);

        var smith = new Blacksmith();
        smith.MoveToWorld(new Point3D(Spot.X + 2, Spot.Y, Spot.Z), Map.Trammel);

        try
        {
            o.Mule.Backpack!.DropItem(new IronIngot(10));
            var rows = MuleSell.Sellable(o.Mule, smith);
            _out.WriteLine($"the blacksmith buys {rows.Count} kind(s) from the mule");
            Assert.NotEmpty(rows);
            var goldBefore = o.Pm.Backpack!.GetAmount(typeof(Gold));
            Assert.Equal(1, MuleSell.Sell(o.Pm, o.Mule, smith, rows.Select(r => r.Item)));
            Assert.True(o.Pm.Backpack.GetAmount(typeof(Gold)) > goldBefore);
        }
        finally
        {
            smith.Delete();
        }

        Assert.Null(MuleModules.RefuseBank(o.Pm, o.Mule));
        o.Pm.Criminal = true;
        Assert.Contains("criminal", MuleModules.RefuseBank(o.Pm, o.Mule));
        o.Pm.Criminal = false;

        var socket = o.Fit(MuleModuleId.ImbuedBardingSocket, 2);
        var fire = o.Mule.Numbers.Fire;
        Assert.True(socket.AddSocket("fire", 10));
        Assert.True(socket.AddSocket("hits", 20));
        Assert.False(socket.AddSocket("cold", 5)); // tier II: two sockets
        Assert.False(new MuleModule(MuleModuleId.ImbuedBardingSocket, 3).AddSocket("fire", 99)); // over the cap
        Assert.Equal(Math.Min(MuleData.ResistCap, fire + 10), o.Mule.Numbers.Fire);

        var wraps = new MuleModule(MuleModuleId.SteadyHoofWraps, 1);
        Assert.NotNull(MuleModuleData.Info(MuleModuleId.SteadyHoofWraps).StubNote);
        wraps.Delete();
    }

    [Fact]
    public void FromTierThreeASatchelUnloadsToTheMuleWithinReach()
    {
        using var o = new Owner(muleTier: 1);
        var low = new ReinforcedOreSatchel();
        var high = new SurveyorsSatchel();
        o.Pm.Backpack!.DropItem(low);
        o.Pm.Backpack.DropItem(high);

        Assert.Null(MuleSatchels.ReachableMule(o.Pm, low)); // tier 2: no Unload
        Assert.Same(o.Mule, MuleSatchels.ReachableMule(o.Pm, high));

        var ore = new IronOre(20);
        high.DropItem(ore);
        Assert.Equal(1, MuleSatchels.Unload(o.Pm, high));
        Assert.True(ore.IsChildOf(o.Mule.Backpack));

        o.Mule.MoveToWorld(new Point3D(Spot.X + 30, Spot.Y, Spot.Z), Map.Trammel);
        Assert.Null(MuleSatchels.ReachableMule(o.Pm, high)); // tier 3: within 12 tiles
        Assert.Same(o.Mule, MuleSatchels.ReachableMule(o.Pm, new MasterExpeditionSatchel())); // tier 5: anywhere on the map
    }
}
