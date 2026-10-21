// MuleVerification.cs
//
// cc-P69, F-33: the Compact Mule's core (tiers, bloodlines, breeding, the League errand). Notes in shard-migration
// notes/cc-P69-mule-core.md.
//
// Facts:
//   1. The tier table is Chase's (HP, pack, resists, module slots), each tier hued with its metal's own hue.
//   2. A mule costs 1 follower slot; changing the cost moves the owner's count with it; a slot hook can set 0.
//   3. Compute places stats by quality inside the tier's range, keeps the resist shape, adds traits, caps resists, and
//      the module hook and the pack-on-death check work.
//   4. A tier-up keeps quality, traits, lineage, name, bond and pack; tier 5 is the top.
//   5. The upgrade path: tier 1 -> 2 is the old Exchange price; short pays nothing; paid, it tiers up.
//   6. A mule round-trips every new field; a version 0 mule (before cc-P69) loads as tier 3 with its HP quality from its
//      hits, no traits, generation 0, and the 2-slot cost is set right.
//   7. Inheritance stays in bounds: quality 0-100, at most 3 traits, grade III only from tier 4+ parents, Celestial
//      Tempered never from a pair that is not both tier 5; the Lore factor is linear from 120 to 200.
//   8. Breeding makes the foal deed at once, genes rolled; the gestation is saved and the deed delivers when due.
//   9. The foal deed summons a bonded tier 1 mule carrying its genes.
//  10. The League errand: gate, deliver (short takes nothing), collect, once only, replace only when the mule is gone.
//  11. The League record's version 0 bytes still load (CharacterLeagueData v1).

using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class MuleVerification
{
    private readonly ITestOutputHelper _out;

    public MuleVerification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D Spot = new(3460, 2604, 18);

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(typeof(T), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    private static Account NewAccount() => new($"p69m{Guid.NewGuid():N}"[..16], "p69-test-only");

    private static PlayerMobile NewCharacter(Account account, int slot = 0)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        account[slot] = pm;
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private static void Cleanup(Account account)
    {
        for (var i = 0; i < account.Length; i++)
        {
            account[i]?.Delete();
        }

        Accounts.Remove(account);
    }

    private static PackMule MuleWith(int tier, int quality, params (MuleTrait, int)[] traits)
    {
        var g = new MuleGenes
        {
            IsMale = true,
            QualityHits = quality, QualityStr = quality, QualityStam = quality, QualityResist = quality, QualityPack = quality
        };
        g.Traits.AddRange(traits);
        return new PackMule(tier, g);
    }

    [Fact]
    public void TheTierTableIsChasesAndEachTierWearsItsMetal()
    {
        var expected = new[]
        {
            (1, "Compact", 150, 190, 1600, 150, 25, 45, 1, CraftResource.DullCopper),
            (2, "Bronzeback", 200, 250, 2400, 225, 35, 55, 2, CraftResource.Bronze),
            (3, "Shadowhide", 250, 310, 3200, 300, 45, 65, 3, CraftResource.ShadowIron),
            (4, "Valorite-Barded", 320, 400, 4000, 400, 55, 70, 4, CraftResource.Valorite),
            (5, "Celestial", 420, 500, 5000, 500, 60, 75, 5, CraftResource.Celestial)
        };

        foreach (var (tier, name, hMin, hMax, w, items, rMin, rMax, slots, metal) in expected)
        {
            var t = MuleData.Tier(tier);
            Assert.Equal((name, hMin, hMax, w, items, rMin, rMax, slots, metal),
                (t.Name, t.HitsMin, t.HitsMax, t.PackWeight, t.PackItems, t.ResistMin, t.ResistMax, t.ModuleSlots, t.Metal));
            Assert.Equal(CraftResources.GetHue(metal), t.Hue);
            Assert.NotEqual(0, t.Hue);

            var mule = new PackMule(tier);
            Assert.Equal(t.Hue, mule.Hue);
            Assert.Equal($"a {name} mule", mule.Name);
            Assert.Equal(slots, mule.ModuleSlots);
            Assert.Empty(mule.Modules);
            mule.Delete();
        }
    }

    [Fact]
    public void AMuleCostsOneSlotAndTheOwnersCountFollowsTheCost()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            var mule = PackMule.SummonFor(pm, 1, MuleGenes.Fresh(), "here");
            Assert.NotNull(mule);
            Assert.Equal(1, mule.ControlSlots);
            Assert.Equal(1, pm.Followers);
            Assert.True(mule.IsBonded);
            Assert.Same(pm, mule.ControlMaster);

            // An old 2-slot mule: setting the cost right moves the owner's count with it.
            mule.SetSlotCost(2);
            Assert.Equal(3 - 1, pm.Followers);
            mule.ApplyNumbers(false);
            Assert.Equal(1, mule.ControlSlots);
            Assert.Equal(1, pm.Followers);

            // The one place the cost is computed: a hook (Trailwise Tack III later) sets 0.
            Action<PackMule, MuleNumbers> zero = (_, n) => n.SlotCost = 0;
            PackMule.ModifyNumbers += zero;
            try
            {
                mule.ApplyNumbers(false);
                Assert.Equal(0, mule.ControlSlots);
                Assert.Equal(0, pm.Followers);
            }
            finally
            {
                PackMule.ModifyNumbers -= zero;
            }

            mule.ApplyNumbers(false);
            Assert.Equal(1, pm.Followers);

            // No free slot: nothing summoned.
            pm.Followers = pm.FollowersMax;
            Assert.Null(PackMule.SummonFor(pm, 1, MuleGenes.Fresh(), "here"));
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void ComputePlacesStatsByQualityKeepsTheResistShapeAndAddsTraits()
    {
        for (var tier = 1; tier <= 5; tier++)
        {
            var t = MuleData.Tier(tier);
            var low = MuleWith(tier, 0);
            var high = MuleWith(tier, 100);

            Assert.Equal(t.HitsMin, low.HitsMax);
            Assert.Equal(t.HitsMax, high.HitsMax);
            Assert.Equal(t.PackWeight, high.PackWeightCap);
            Assert.Equal(t.PackItems, high.PackItemsCap);
            Assert.Equal(t.PackWeight, high.Backpack!.MaxWeight);
            Assert.Equal(t.PackItems, high.Backpack.MaxItems);

            foreach (var m in new[] { low, high })
            {
                var n = m.Numbers;
                foreach (var r in new[] { n.Physical, n.Fire, n.Cold, n.Poison, n.Energy })
                {
                    Assert.InRange(r, t.ResistMin, t.ResistMax);
                }

                Assert.True(n.Physical >= n.Cold && n.Cold >= n.Fire && n.Cold == n.Poison && n.Fire == n.Energy);
                Assert.Equal(n.Physical, m.PhysicalResistance);
            }

            Assert.Equal(t.ResistMax, high.Numbers.Physical);
            low.Delete();
            high.Delete();
        }

        var plain = MuleWith(3, 50);
        var traited = MuleWith(3, 50, (MuleTrait.Burdened, 2), (MuleTrait.Stoneback, 3), (MuleTrait.Surefooted, 1));
        Assert.Equal(plain.PackWeightCap * 110 / 100, traited.PackWeightCap);
        Assert.Equal(plain.Numbers.Physical + 9, traited.Numbers.Physical);
        Assert.Equal(plain.Numbers.Stam * 110 / 100, traited.Numbers.Stam);

        var tempered = MuleWith(5, 100, (MuleTrait.CelestialTempered, 3), (MuleTrait.Stoneback, 3));
        Assert.True(tempered.Numbers.Physical <= MuleData.ResistCap);

        var deep = MuleWith(3, 50, (MuleTrait.Deepdelver, 3));
        Assert.Equal(0.85, deep.Numbers.DungeonDamageScale, 3);

        // The pack drops to the corpse, until something switches the one check.
        Assert.Equal(DeathMoveResult.MoveToCorpse, plain.GetInventoryMoveResultFor(new Gold(1)));
        Action<PackMule, MuleNumbers> keep = (_, n) => n.KeepPackOnDeath = true;
        PackMule.ModifyNumbers += keep;
        try
        {
            Assert.Equal(DeathMoveResult.MoveToBackpack, plain.GetInventoryMoveResultFor(new Gold(1)));
        }
        finally
        {
            PackMule.ModifyNumbers -= keep;
        }

        plain.Delete();
        traited.Delete();
        tempered.Delete();
        deep.Delete();
    }

    [Fact]
    public void ATierUpKeepsTheBloodlineAndTheTopIsTierFive()
    {
        var mule = MuleWith(1, 70, (MuleTrait.Emberblood, 2));
        mule.Name = "Bessie";
        mule.FamilyName = "Ironhoof";
        var gold = new Gold(500);
        mule.Backpack!.DropItem(gold);

        for (var tier = 2; tier <= 5; tier++)
        {
            Assert.True(mule.TierUp());
            Assert.Equal(tier, mule.Tier);
            Assert.Equal(70, mule.QualityHits);
            Assert.Equal(2, mule.GradeOf(MuleTrait.Emberblood));
            Assert.Equal("Bessie", mule.Name);
            Assert.Equal("Ironhoof", mule.FamilyName);
            Assert.Same(mule.Backpack, gold.Parent);
            Assert.Equal(MuleData.Tier(tier).Hue, mule.Hue);
        }

        Assert.False(mule.TierUp());
        Assert.Equal(1, mule.LineTier5Generations);
        mule.Delete();
    }

    [Fact]
    public void TheUpgradePathStartsAtTheOldExchangePriceAndPaysAllOrNothing()
    {
        Assert.Equal(new[] { 1, 2, 3, 4 }, MuleUpgrades.Steps.Select(s => s.FromTier));
        var first = MuleUpgrades.StepFrom(1);
        Assert.Equal(25_000, first.Gold);
        Assert.Equal(new[] { ("mining", 25), ("smithing", 25), ("rangers", 25), ("foresters", 25) }, first.Scrip);
        Assert.True(MuleUpgrades.Steps.Zip(MuleUpgrades.Steps.Skip(1)).All(p => p.Second.Gold > p.First.Gold));

        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            var mule = PackMule.SummonFor(pm, 1, MuleGenes.Fresh(), "here");
            var guild = ClusterFAccountPersistence.GetOrCreate(account).GetOrCreateGuildData(pm.Serial);
            guild.GuildCurrency["mining"] = 25;
            guild.GuildCurrency["smithing"] = 25;
            guild.GuildCurrency["rangers"] = 25;
            guild.GuildCurrency["foresters"] = 24; // one short
            pm.Backpack!.DropItem(new Gold(25_000));

            Assert.Contains(MuleUpgrades.MulesOf(pm), m => m == mule);
            Assert.False(MuleUpgrades.TryUpgrade(pm, guild, mule));
            Assert.Equal(1, mule.Tier);
            Assert.Equal(25, guild.GuildCurrency["mining"]);
            Assert.Equal(25_000, CompactGoldHelper.GetTotalGold(pm));

            guild.GuildCurrency["foresters"] = 25;
            Assert.True(MuleUpgrades.TryUpgrade(pm, guild, mule));
            Assert.Equal(2, mule.Tier);
            Assert.Equal(0, guild.GuildCurrency["mining"]);
            Assert.Equal(0, guild.GuildCurrency["foresters"]);
            Assert.Equal(0, CompactGoldHelper.GetTotalGold(pm));

            // Tier 2 -> 3 also needs a Miners' Compact rank.
            Assert.Contains(MuleUpgrades.Shortfalls(pm, guild, MuleUpgrades.StepFrom(2)), s => s.StartsWith("Miners' Compact rank"));
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void AMuleRoundTripsAndAVersionZeroMuleLoadsAsTierThree()
    {
        var mule = MuleWith(4, 37, (MuleTrait.NightmareKin, 2), (MuleTrait.Deepdelver, 1));
        mule.FamilyName = "Slatefoot";
        var copy = RoundTrip(mule);
        Assert.Equal(4, copy.Tier);
        Assert.Equal(37, copy.QualityPack);
        Assert.Equal(2, copy.GradeOf(MuleTrait.NightmareKin));
        Assert.Equal(1, copy.GradeOf(MuleTrait.Deepdelver));
        Assert.Equal("Slatefoot", copy.FamilyName);
        Assert.Empty(copy.Modules);

        // Version 0 bytes: BaseMount's own part (written through a non-virtual call), then PackMule's v0 header and its
        // two fields, as the generator wrote them before cc-P69.
        var old = MuleWith(3, 0);
        old.HitsMaxSeed = 280; // inside tier 3's 250-310: quality 50
        var due = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);

        var baseSerialize = new DynamicMethod("BaseMountSerialize", null, new[] { typeof(PackMule), typeof(IGenericWriter) }, typeof(PackMule), true);
        var il = baseSerialize.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Call, typeof(BaseMount).GetMethod("Serialize", new[] { typeof(IGenericWriter) })!);
        il.Emit(OpCodes.Ret);
        var callBase = (Action<PackMule, IGenericWriter>)baseSerialize.CreateDelegate(typeof(Action<PackMule, IGenericWriter>));

        // How the generator writes a version header: read it off a v1 save of the same mule (male, so the field after a
        // one-byte header is 1; after a four-byte one, zeros).
        var probe = new byte[65536];
        var pw = new BufferWriter(probe, true);
        callBase(old, pw);
        var baseLength = (int)pw.Position;
        var full = new byte[65536];
        var fw = new BufferWriter(full, true);
        old.Serialize(fw);
        var plainIntHeader = full[baseLength + 1] == 0 && full[baseLength + 2] == 0 && full[baseLength + 3] == 0;
        _out.WriteLine($"base part {baseLength} bytes; version header is {(plainIntHeader ? "a plain int" : "an encoded int")}");

        var bytes = new byte[65536];
        var w = new BufferWriter(bytes, true);
        callBase(old, w);
        if (plainIntHeader)
        {
            w.Write(0);
        }
        else
        {
            w.WriteEncodedInt(0);
        }

        w.Write(true);
        w.Write(due);
        w.Flush();

        var migrated = new PackMule(old.Serial);
        migrated.Deserialize(new BufferReader(bytes));
        _out.WriteLine($"v0 -> tier {migrated.Tier}, hits quality {migrated.QualityHits}, male {migrated.IsMale}, next breed {migrated.NextBreedTime:o}");

        Assert.Equal(3, migrated.Tier);
        Assert.True(migrated.IsMale);
        Assert.Equal(due, migrated.NextBreedTime);
        Assert.Equal(50, migrated.QualityHits);
        Assert.InRange(migrated.QualityStr, 0, 100);
        Assert.Equal("none", migrated.TraitsText);
        Assert.Equal(0, migrated.Generation);
        Assert.Equal(0u, migrated.Parent1);
        Assert.Empty(migrated.Modules);

        mule.Delete();
        old.Delete();
    }

    [Fact]
    public void InheritanceStaysInBoundsAndParentTiersCapTheGrades()
    {
        Assert.Equal(0.0, MuleBreeding.LoreFactor(100));
        Assert.Equal(0.0, MuleBreeding.LoreFactor(120));
        Assert.Equal(0.5, MuleBreeding.LoreFactor(160));
        Assert.Equal(1.0, MuleBreeding.LoreFactor(200));

        var allIII = new[] { (MuleTrait.Burdened, 3), (MuleTrait.Stoneback, 3), (MuleTrait.Emberblood, 3) };
        var otherIII = new[] { (MuleTrait.Surefooted, 3), (MuleTrait.Stoneback, 3), (MuleTrait.CelestialTempered, 3) };

        foreach (var (tierA, tierB) in new[] { (1, 1), (3, 4), (4, 4), (5, 4), (5, 5) })
        {
            var a = MuleWith(tierA, 100, allIII);
            var b = MuleWith(tierB, 0, otherIII);
            b.IsMale = false;
            var celestialSeen = false;

            for (var i = 0; i < 1500; i++)
            {
                var foal = MuleGenes.Breed(a, b, i % 2 == 0 ? 200 : 0);

                foreach (var q in new[] { foal.QualityHits, foal.QualityStr, foal.QualityStam, foal.QualityResist, foal.QualityPack })
                {
                    Assert.InRange(q, 0, 100);
                }

                Assert.True(foal.Traits.Count <= 3);
                Assert.Equal(foal.Traits.Count, foal.Traits.Select(t => t.Id).Distinct().Count());
                var cap = Math.Min(tierA, tierB) >= 4 ? 3 : 2;
                Assert.All(foal.Traits, t => Assert.InRange(t.Grade, 1, cap));

                var hasCelestial = foal.Traits.Any(t => t.Id == MuleTrait.CelestialTempered);
                Assert.False(hasCelestial && !(tierA == 5 && tierB == 5), $"Celestial Tempered from tiers {tierA} and {tierB}");
                celestialSeen |= hasCelestial;

                Assert.Equal(1, foal.Generation);
                Assert.Equal((uint)a.Serial, foal.Parent1);
                Assert.False(string.IsNullOrEmpty(foal.FamilyName));
            }

            _out.WriteLine($"tiers {tierA}/{tierB}: Celestial Tempered seen {celestialSeen}");
            if (tierA == 5 && tierB == 5)
            {
                Assert.True(celestialSeen); // one parent carries it at III: it passes at 35%+ when both are tier 5
            }

            a.Delete();
            b.Delete();
        }

        // Family: the higher-tier parent's name runs on; a tier 5 pair carries the Legendary count.
        var sire = MuleWith(5, 50);
        var dam = MuleWith(5, 50);
        dam.IsMale = false;
        sire.FamilyName = "Oreheart";
        var kit = MuleGenes.Breed(sire, dam, 0);
        Assert.Equal("Oreheart", kit.FamilyName);
        Assert.Equal(1, kit.InheritedTier5);
        sire.Delete();
        dam.Delete();
    }

    [Fact]
    public void BreedingMakesTheFoalDeedAtOnceAndTheGestationSurvivesARestart()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            var sire = PackMule.SummonFor(pm, 1, MuleGenes.Fresh(), "here");
            var dam = PackMule.SummonFor(pm, 1, MuleGenes.Fresh(), "here");
            sire.IsMale = true;
            dam.IsMale = false;

            Assert.Null(PackMuleBreedingSystem.CanBreed(sire, dam, pm));
            Assert.NotNull(PackMuleBreedingSystem.CanBreed(sire, sire, pm));

            var deed = PackMuleBreedingSystem.StartBreeding(sire, dam, pm);
            Assert.NotNull(PackMuleBreedingSystem.CanBreed(sire, dam, pm)); // the cooldown
            Assert.True(deed.Pending);
            Assert.True(deed.Rolled);
            Assert.Same(Map.Internal, deed.Map);
            Assert.InRange(deed.DeliverAt, DateTime.UtcNow.AddHours(23.9), DateTime.UtcNow.AddHours(24.1));

            // A restart: the deed's state comes back, and its delivery is armed again from it.
            var copy = RoundTrip(deed);
            Assert.True(copy.Pending);
            Assert.Same(pm, copy.Recipient);
            Assert.Equal(deed.DeliverAt, copy.DeliverAt);
            Assert.Equal(deed.Genes.QualityPack, copy.Genes.QualityPack);
            Assert.Equal(deed.Genes.TraitsText(), copy.Genes.TraitsText());

            Assert.False(deed.TryDeliver(DateTime.UtcNow));
            Assert.True(deed.TryDeliver(deed.DeliverAt));
            Assert.Same(pm.BankBox, deed.Parent);
            Assert.False(deed.Pending);
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void TheFoalDeedSummonsABondedTierOneMuleWithItsGenes()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            var genes = MuleGenes.Fresh();
            genes.IsMale = false;
            genes.QualityPack = 88;
            genes.Traits.Add((MuleTrait.Surefooted, 2));
            genes.FamilyName = "Deepvein";
            genes.Generation = 4;
            var deed = new BredPackMuleDeed(genes);
            pm.Backpack!.DropItem(deed);

            deed.OnDoubleClick(pm);

            var mule = pm.AllFollowers.OfType<PackMule>().Single();
            Assert.True(deed.Deleted);
            Assert.Equal(1, mule.Tier);
            Assert.True(mule.IsBonded);
            Assert.False(mule.IsMale);
            Assert.Equal(88, mule.QualityPack);
            Assert.Equal(2, mule.GradeOf(MuleTrait.Surefooted));
            Assert.Equal("Deepvein", mule.FamilyName);
            Assert.Equal(4, mule.Generation);
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void TheLeagueErrandGatesDeliversCollectsOnceAndReplacesOnlyALostMule()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            Assert.False(MuleErrand.Accept(pm, out _)); // not registered
            ClusterFLeagueSystem.JoinLeague(pm);
            ClusterFLeagueRanks.SetRank(pm, 1);
            Assert.False(MuleErrand.Accept(pm, out var gate)); // Iron: below the gate
            _out.WriteLine(gate);

            ClusterFLeagueRanks.SetRank(pm, MuleErrand.RankGate);
            Assert.True(MuleErrand.Accept(pm, out _));
            Assert.Equal(MuleErrand.Taken, MuleErrand.Stage(pm));
            Assert.False(MuleErrand.Accept(pm, out _));

            // Short: nothing is taken.
            var ore = new IronOre(MuleErrand.OreCount);
            var ingots = new IronIngot(MuleErrand.IngotCount - 1);
            pm.Backpack!.DropItem(ore);
            pm.Backpack.DropItem(ingots);
            Assert.False(MuleErrand.Deliver(pm, out _));
            Assert.Equal(MuleErrand.Taken, MuleErrand.Stage(pm));
            Assert.Equal(MuleErrand.OreCount, ore.Amount);

            pm.Backpack.DropItem(new IronIngot(1));
            Assert.True(MuleErrand.Deliver(pm, out _));
            Assert.Equal(MuleErrand.Delivered, MuleErrand.Stage(pm));
            Assert.True(ore.Deleted);

            var mule = MuleErrand.Collect(pm, out _);
            Assert.NotNull(mule);
            Assert.Equal(1, mule.Tier);
            Assert.True(mule.IsBonded);
            Assert.True(MuleErrand.Record(pm).MuleGranted);
            Assert.Equal(MuleErrand.Done, MuleErrand.Stage(pm));
            Assert.Null(MuleErrand.Collect(pm, out _));
            Assert.False(MuleErrand.Accept(pm, out _));

            // While the character has a mule (here, following) there is nothing to replace.
            Assert.True(MuleErrand.OwnsAMule(pm));
            Assert.False(MuleErrand.CanReplace(pm));

            mule.Delete();
            Assert.True(MuleErrand.CanReplace(pm));
            Assert.Null(MuleErrand.Replace(pm, out _)); // no fee in hand

            var guild = ClusterFAccountPersistence.GetOrCreate(account).GetOrCreateGuildData(pm.Serial);
            guild.GuildCurrency["mining"] = MuleErrand.ReplaceVouchers;
            pm.Backpack.DropItem(new Gold(MuleErrand.ReplaceGold));
            var again = MuleErrand.Replace(pm, out _);
            Assert.NotNull(again);
            Assert.Equal(0, guild.GuildCurrency["mining"]);
            Assert.False(MuleErrand.CanReplace(pm));
        }
        finally
        {
            Cleanup(account);
        }
    }

    [Fact]
    public void TheLeagueRecordsVersionZeroBytesStillLoad()
    {
        var bytes = new byte[64];
        var w = new BufferWriter(bytes, true);
        w.Write(0);     // version 0
        w.Write(4);     // rank
        w.Write(1);     // one promotion job
        w.Write(3);
        w.Write(0);     // no trials
        w.Flush();

        var v0 = new CharacterLeagueData(new BufferReader(bytes));
        Assert.Equal(4, v0.Rank);
        Assert.Contains(3, v0.PromotionDone);
        Assert.Equal(0, v0.MuleErrandStage);
        Assert.False(v0.MuleGranted);

        var record = new CharacterLeagueData { Rank = 2, MuleErrandStage = MuleErrand.Delivered, MuleGranted = true };
        var buffer = new byte[64];
        var bw = new BufferWriter(buffer, true);
        record.Serialize(bw);
        bw.Flush();
        var back = new CharacterLeagueData(new BufferReader(buffer));
        Assert.Equal(MuleErrand.Delivered, back.MuleErrandStage);
        Assert.True(back.MuleGranted);
        Assert.Equal(2, CharacterLeagueData.CurrentVersion); // cc-P280: v2 adds the League Register and the backpack tier
    }
}
