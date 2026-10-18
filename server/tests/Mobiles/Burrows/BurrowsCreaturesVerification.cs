// BurrowsCreaturesVerification.cs
//
// cc-P140, F-45: the Rootdeep Burrows' creatures (Parts B and C). Notes in shard-migration notes/cc-P140-rootdeep-burrows.md.
//
// Facts:
//   1. Every new type constructs with its body and names, the server types its body (ShardBodyTypes: every cc-P140 body
//      is Monster or Animal, never Empty), it spawns into a world, and killed it leaves a corpse carrying the body.
//   2. Every new type round-trips the generated serializer with its body and name.
//   3. The tutorial creatures engage only a tutorial participant (IsEnemy, CanBeHarmful, Damage), keep NoKillAwards, and
//      still drop their fixed loot when the participant kills them.
//   4. The Overdraft's drop is exactly its list: a coin pouch holding 100 gold, one Dawnstone ingot, 10 bandages, one
//      trinket from BurrowsLoot.OverdraftTrinkets, and nothing else.
//   5. Each family drops its part: Burrower Claws (Warren), Rootheart Sap (Tangle), Dawnstone Shards (Dawnseam), Gloom
//      Glands and Gloomsilk (Gloom); the bosses drop jewelry and their boss part; the hog and the grub carve Burrowhide.
//   6. The Hoarding Rat calls exactly two Penny Rats the first time it is hurt and none after; the Burrow Mother's brood
//      stops at her caps and dies with her; a Gloomspinner's web paralyzes; a Crop Crow flees when struck; a Safe Deposit
//      Box is a ChestMimic that engages only participants and drops its gold.
//   The Vault's doorway, crawlway and the mini champ are cc-P141's or a later port and are not asserted here.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run as a
// gate by docker/uo/build.sh.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class BurrowsCreaturesVerification
{
    private static readonly Point3D Here = new(1050, 1050, 0);
    private static bool _startupHooksRun;

    private readonly ITestOutputHelper _out;

    public BurrowsCreaturesVerification(ITestOutputHelper output)
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

        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }

        ClusterFTutorial.Enabled = true;
        ClusterFLootBoxes.LoopProvider = _ => 1;
        ShardBodyTypes.Apply();
    }

    // (type, name, corpse name, body, server type)
    private static readonly (Type Type, string Name, string Corpse, int Body, BodyType Kind)[] Lineup =
    {
        (typeof(Gourdling), "a gourdling", "a gourdling corpse", 2049, BodyType.Monster),
        (typeof(CropCrow), "a crop crow", "a crow corpse", 2071, BodyType.Animal),
        (typeof(BurrowScampSlinger), "a burrow scamp slinger", "a burrow scamp corpse", 2048, BodyType.Monster),
        (typeof(CacklerScrapper), "a cackler scrapper", "a cackler corpse", 2050, BodyType.Monster),
        (typeof(CacklerSlasher), "a cackler slasher", "a cackler corpse", 2051, BodyType.Monster),
        (typeof(CacklerVeteran), "a cackler veteran", "a cackler corpse", 2052, BodyType.Monster),
        (typeof(CacklerPackleader), "a cackler packleader", "a cackler corpse", 2053, BodyType.Monster),
        (typeof(BurrowHog), "a burrow hog", "a hog corpse", 2054, BodyType.Animal),
        (typeof(LedgerBat), "a ledger bat", "a bat corpse", 2055, BodyType.Monster),
        (typeof(PennyRat), "a penny rat", "a rat corpse", 0xD7, BodyType.Monster), // pinned bodyTable.cfg types 215 Monster
        (typeof(HoardingRat), "a hoarding rat", "a hoarding rat corpse", 2056, BodyType.Monster),
        (typeof(TheOverdraft), "The Overdraft", "the Overdraft's wreckage", 2057, BodyType.Monster),
        (typeof(BurrowGrub), "a burrow grub", "a burrow grub corpse", 2058, BodyType.Monster),
        (typeof(BurrowScamp), "a burrow scamp", "a burrow scamp corpse", 2059, BodyType.Animal),
        (typeof(RedcapScamp), "a redcap scamp", "a redcap scamp corpse", 2060, BodyType.Animal),
        (typeof(TheBurrowMother), "The Burrow Mother", "the Burrow Mother's corpse", 2061, BodyType.Monster),
        (typeof(Rootling), "a rootling", "a rootling corpse", 779, BodyType.Monster),
        (typeof(Bramblehulk), "a bramblehulk", "a bramblehulk corpse", 2064, BodyType.Monster),
        (typeof(Dawnshard), "a dawnshard", "a dawnshard corpse", 2065, BodyType.Monster),
        (typeof(Seamguard), "a seamguard", "a seamguard corpse", 2066, BodyType.Monster),
        (typeof(Gloomling), "a gloomling", "a gloomling corpse", 2067, BodyType.Monster),
        (typeof(Gloomspinner), "a gloomspinner", "a gloomspinner corpse", 2068, BodyType.Monster),
        (typeof(Gloomweaver), "a gloomweaver", "a gloomweaver corpse", 2069, BodyType.Monster),
        (typeof(TheGloomQueen), "The Gloom Queen", "the Gloom Queen's corpse", 2070, BodyType.Monster)
    };

    private static readonly Type[] TutorialTypes =
    {
        typeof(Gourdling), typeof(CropCrow), typeof(BurrowScampSlinger), typeof(CacklerScrapper), typeof(CacklerSlasher),
        typeof(CacklerVeteran), typeof(CacklerPackleader), typeof(LedgerBat), typeof(PennyRat), typeof(HoardingRat),
        typeof(TheOverdraft)
    };

    private sealed class Player : IDisposable
    {
        public readonly Account Account;
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player()
        {
            Account = new Account($"p140{Guid.NewGuid():N}"[..16], "p140-test-only");
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Newbie" };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1050, 1052, 0), Map.Trammel);
            Pm.Hits = Pm.HitsMax;
            Ns = PacketTestUtilities.CreateTestNetState();
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public TutorialRecord Record => ClusterFTutorial.RecordOf(Pm)!;

        public void Dispose()
        {
            ClusterFFanfare.FlushAll();
            Pm.NetState = null;
            Ns.Mobile = null;
            for (var i = 0; i < Account.Length; i++)
            {
                Account[i]?.Delete();
            }

            Accounts.Remove(Account);
        }
    }

    private static BaseCreature Make(Type t) => (BaseCreature)Activator.CreateInstance(t)!;

    // GetMobilesInRange<T> is a pooled enumerator, not an IEnumerable: walk it.
    private static List<T> Nearby<T>(Mobile around, int range) where T : Mobile
    {
        var list = new List<T>();
        foreach (var m in around.GetMobilesInRange<T>(range))
        {
            list.Add(m);
        }

        return list;
    }

    private static T RoundTrip<T>(T original) where T : ISerializable
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    private static Corpse Kill(BaseCreature bc, Mobile killer = null)
    {
        bc.MoveToWorld(Here, Map.TerMur);
        if (killer != null)
        {
            bc.Damage(bc.Hits + 50, killer);
        }
        else
        {
            bc.Kill();
        }

        return Assert.IsAssignableFrom<Corpse>(bc.Corpse);
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void EveryTypeConstructsOnATypedBodySpawnsAndDiesToACorpseCarryingTheBody()
    {
        Assert.Equal(24, Lineup.Length);

        foreach (var (type, name, corpse, body, kind) in Lineup)
        {
            var bc = Make(type);
            _out.WriteLine($"{type.Name}: name='{bc.Name}' body={(int)bc.Body} type={bc.Body.Type} hits={bc.HitsMax} ai={bc.AI} fame={bc.Fame}");

            Assert.Equal(name, bc.Name);
            Assert.Equal(corpse, bc.CorpseName);
            if (type == typeof(Sporecap))
            {
                Assert.Contains((int)bc.Body, new[] { Sporecap.RedBody, Sporecap.BlueBody });
            }
            else
            {
                Assert.Equal(body, (int)bc.Body);
            }

            Assert.Equal(kind, bc.Body.Type);
            Assert.NotEqual(BodyType.Empty, bc.Body.Type);

            bc.MoveToWorld(Here, Map.TerMur);
            Assert.True(bc.Alive);
            Assert.Same(Map.TerMur, bc.Map);

            var c = Kill(bc);
            Assert.Equal(0x2006, c.ItemID);
            Assert.Equal((int)bc.Body, c.Amount);
            c.Delete();
        }

        // The Sporecap wears one of two bodies.
        var sporecap = new Sporecap();
        Assert.Equal("a sporecap", sporecap.Name);
        Assert.Contains((int)sporecap.Body, new[] { Sporecap.RedBody, Sporecap.BlueBody });
        Assert.Equal(BodyType.Monster, sporecap.Body.Type);
        sporecap.Delete();

        // Every cc-P140 body is in ShardBodyTypes with the type the lineup says.
        foreach (var body in new[] { 2049, 2050, 2051, 2052, 2053, 2054, 2055, 2056, 2057, 2058, 2059, 2060, 2061, 2062, 2063, 2064, 2065, 2066, 2067, 2068, 2069, 2070, 2071 })
        {
            Assert.Contains(ShardBodyTypes.Entries, e => e.Body == body);
            Assert.NotEqual(BodyType.Empty, new Body(body).Type);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void EveryTypeRoundTripsTheGeneratedSerializer()
    {
        foreach (var (type, name, _c, _b, _k) in Lineup.Append((typeof(Sporecap), "a sporecap", "", 0, BodyType.Monster)))
        {
            var bc = Make(type);
            var copy = RoundTrip(bc);
            Assert.Equal((int)bc.Body, (int)copy.Body);
            Assert.Equal(name, copy.Name);
            Assert.Equal(bc.Hue, copy.Hue);
            copy.Delete();
            bc.Delete();
        }

        foreach (var part in new Item[] { new BurrowerClaws(3), new RootheartSap(2), new DawnstoneShards(), new GloomGlands(4), new BroodMothersMandible(), new GloomQueensFang() })
        {
            var copy = RoundTrip(part);
            Assert.Equal(part.Amount, copy.Amount);
            Assert.Equal(part.Name, copy.Name);
            Assert.Equal(part.Hue, copy.Hue);
            Assert.True(copy.Stackable);
            copy.Delete();
            part.Delete();
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TutorialCreaturesEngageOnlyAParticipantKeepNoKillAwardsAndStillDropTheirFixedLoot()
    {
        using var newbie = new Player();
        using var veteran = new Player();
        ClusterFTutorial.OnLogin(newbie.Pm);
        veteran.Record.State = TutorialState.Graduated;
        Assert.True(ClusterFTutorial.IsParticipant(newbie.Pm));
        Assert.False(ClusterFTutorial.IsParticipant(veteran.Pm));

        foreach (var type in TutorialTypes)
        {
            var bc = Make(type);
            Assert.IsAssignableFrom<TutorialCreature>(bc);
            bc.MoveToWorld(Here, Map.Trammel);

            Assert.True(bc.IsEnemy(newbie.Pm), type.Name);
            Assert.False(bc.IsEnemy(veteran.Pm), type.Name);
            Assert.False(bc.CanBeHarmful(veteran.Pm, false, false), type.Name);
            Assert.True(bc.NoKillAwards, type.Name);
            Assert.True(bc.IgnoreYoungProtection, type.Name);

            var hits = bc.Hits;
            bc.Damage(3, veteran.Pm);
            Assert.Equal(hits, bc.Hits);

            // Killed by the participant: the fixed drop is on the corpse (every one carries gold).
            bc.Damage(bc.Hits + 50, newbie.Pm);
            var corpse = Assert.IsAssignableFrom<Corpse>(bc.Corpse);
            Assert.True(corpse.Items.Any(i => i is Gold or Pouch), $"{type.Name} dropped no gold: {string.Join(", ", corpse.Items.Select(i => i.GetType().Name))}");
            _out.WriteLine($"{type.Name}: {string.Join(", ", corpse.Items.Select(i => $"{i.GetType().Name} x{i.Amount}"))}");
            corpse.Delete();
        }

        // The Safe Deposit Box is a ChestMimic with the same rule.
        var box = new SafeDepositBox();
        box.MoveToWorld(Here, Map.Trammel);
        Assert.IsAssignableFrom<ChestMimic>(box);
        Assert.Equal(ChestMimic.MinLevel, box.ChestLevel);
        Assert.True(box.IsEnemy(newbie.Pm));
        Assert.False(box.IsEnemy(veteran.Pm));
        Assert.False(box.CanBeHarmful(veteran.Pm, false, false));
        Assert.True(box.NoKillAwards);
        Assert.Empty(box.Backpack?.Items ?? new List<Item>());
        var boxHits = box.Hits;
        box.Damage(3, veteran.Pm);
        Assert.Equal(boxHits, box.Hits);
        box.Damage(box.Hits + 50, newbie.Pm);
        var boxCorpse = Assert.IsAssignableFrom<Corpse>(box.Corpse);
        Assert.Contains(boxCorpse.Items, i => i is Gold { Amount: 25 });
        boxCorpse.Delete();
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheOverdraftsDropIsExactlyItsList()
    {
        using var newbie = new Player();
        ClusterFTutorial.OnLogin(newbie.Pm);

        for (var i = 0; i < 20; i++)
        {
            var boss = new TheOverdraft();
            Assert.Equal(TheOverdraft.StrongboxHue, boss.Hue);
            Assert.Equal(0.4, boss.ActiveSpeed);
            boss.MoveToWorld(Here, Map.Trammel);
            boss.Damage(boss.Hits + 500, newbie.Pm);
            var corpse = Assert.IsAssignableFrom<Corpse>(boss.Corpse);

            var items = corpse.Items.ToList();
            _out.WriteLine(string.Join(", ", items.Select(it => $"{it.GetType().Name} x{it.Amount}")));
            Assert.Equal(4, items.Count);

            var pouch = Assert.Single(items.OfType<Pouch>());
            Assert.Equal("a coin pouch from the vault", pouch.Name);
            var gold = Assert.Single(pouch.Items);
            Assert.IsType<Gold>(gold);
            Assert.Equal(TheOverdraft.PouchGold, gold.Amount);

            var ingot = Assert.Single(items.OfType<DawnstoneIngot>());
            Assert.Equal(1, ingot.Amount);
            var bandage = Assert.Single(items.OfType<Bandage>());
            Assert.Equal(TheOverdraft.Bandages, bandage.Amount);

            var trinket = Assert.Single(items, it => it is not Pouch and not DawnstoneIngot and not Bandage);
            Assert.Contains(trinket.GetType(), BurrowsLoot.OverdraftTrinkets);
            corpse.Delete();
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void EachFamilyDropsItsPartTheBossesTheirJewelryAndPartAndBurrowhideCarves()
    {
        void Drops<TPart>(BaseCreature bc, int atLeast = 1) where TPart : Item
        {
            var c = Kill(bc);
            var part = c.FindItemByType<TPart>();
            Assert.True(part != null && part.Amount >= atLeast, $"{bc.GetType().Name} dropped no {typeof(TPart).Name}: {string.Join(", ", c.Items.Select(i => i.GetType().Name))}");
            c.Delete();
        }

        Drops<BurrowerClaws>(new BurrowGrub());
        Drops<BurrowerClaws>(new BurrowScamp());
        Drops<BurrowerClaws>(new RedcapScamp());
        Drops<BurrowerClaws>(new TheBurrowMother(), 3);
        Drops<RootheartSap>(new Rootling());
        Drops<RootheartSap>(new Sporecap());
        Drops<RootheartSap>(new Bramblehulk(), 2);
        Drops<RootwoodLog>(new Rootling(), 2);
        Drops<DawnstoneShards>(new Dawnshard());
        Drops<DawnstoneShards>(new Seamguard(), 2);
        Drops<DawnstoneOre>(new Dawnshard(), 2);
        Drops<GloomGlands>(new Gloomling());
        Drops<GloomGlands>(new Gloomspinner());
        Drops<GloomGlands>(new Gloomweaver());
        Drops<GloomGlands>(new TheGloomQueen(), 3);
        Drops<GloomsilkCloth>(new Gloomling());
        Drops<GloomsilkCloth>(new Gloomspinner(), 2);
        Drops<GloomsilkCloth>(new TheGloomQueen(), 5);

        // The bosses: jewelry and the boss part.
        var mother = Kill(new TheBurrowMother());
        Assert.NotNull(mother.FindItemByType<BroodMothersMandible>());
        Assert.Contains(mother.Items, i => i is BaseJewel);
        mother.Delete();
        var queen = Kill(new TheGloomQueen());
        Assert.NotNull(queen.FindItemByType<GloomQueensFang>());
        Assert.Contains(queen.Items, i => i is BaseJewel);
        queen.Delete();

        // Burrowhide: the hog (hides and ribs) and the grub (hides) carve it; a second carve finds nothing.
        var pm = new PlayerMobile { Player = true, Race = Race.Elf, Name = "Carver" };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Here, Map.TerMur);
        var knife = new SkinningKnife();
        pm.AddToBackpack(knife);
        try
        {
            var hog = new BurrowHog();
            var hogCorpse = Kill(hog);
            hog.OnCarve(pm, hogCorpse, knife);
            Assert.True(hogCorpse.Carved);
            var hides = hogCorpse.FindItemByType<BurrowhideHides>();
            Assert.NotNull(hides);
            Assert.Equal(BurrowHog.BurrowhideHidesCount, hides.Amount);
            Assert.NotNull(hogCorpse.FindItemByType<RawRibs>());
            hog.OnCarve(pm, hogCorpse, knife);
            Assert.Equal(1, hogCorpse.Items.Count(i => i is BurrowhideHides));
            hogCorpse.Delete();

            var grub = new BurrowGrub();
            var grubCorpse = Kill(grub);
            grub.OnCarve(pm, grubCorpse, knife);
            Assert.Equal(BurrowGrub.BurrowhideHidesCount, grubCorpse.FindItemByType<BurrowhideHides>()?.Amount);
            Assert.Null(grubCorpse.FindItemByType<RawRibs>());
            grubCorpse.Delete();
        }
        finally
        {
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheHoardingRatCallsTwoPennyRatsOnceAndTheBurrowMothersBroodStopsAtHerCapsAndDiesWithHer()
    {
        using var newbie = new Player();
        ClusterFTutorial.OnLogin(newbie.Pm);

        var rat = new HoardingRat();
        var spot = new Point3D(1200, 1200, 0);
        rat.MoveToWorld(spot, Map.Trammel);
        Assert.False(rat.CalledPack);
        var before = Nearby<PennyRat>(rat, 6).Count;
        rat.Damage(5, newbie.Pm);
        Assert.True(rat.CalledPack);
        Assert.Equal(before + HoardingRat.PackSize, Nearby<PennyRat>(rat, 6).Count);
        rat.Damage(5, newbie.Pm);
        Assert.Equal(before + HoardingRat.PackSize, Nearby<PennyRat>(rat, 6).Count);
        foreach (var m in Nearby<PennyRat>(rat, 6))
        {
            Assert.Same(newbie.Pm, m.Combatant);
            m.Delete();
        }

        rat.Delete();

        var mother = new TheBurrowMother();
        mother.MoveToWorld(new Point3D(1300, 1300, 0), Map.Trammel);
        for (var i = 0; i < TheBurrowMother.BroodAtOnce; i++)
        {
            Assert.True(mother.SpawnBrood());
        }

        Assert.False(mother.SpawnBrood()); // three alive: no more until one dies
        Assert.Equal(TheBurrowMother.BroodAtOnce, mother.BroodAlive);
        var grubs = Nearby<BurrowGrub>(mother, 10).ToArray();
        Assert.Equal(TheBurrowMother.BroodAtOnce, grubs.Length);
        grubs[0].Kill();
        Assert.True(mother.SpawnBrood());
        Assert.Equal(TheBurrowMother.BroodAtOnce + 1, mother.BroodSpawned);

        mother.Kill();
        Assert.DoesNotContain(Nearby<BurrowGrub>(mother, 10), g => g.Alive && !g.Deleted);
        foreach (var g in grubs)
        {
            g.Corpse?.Delete();
            if (!g.Deleted)
            {
                g.Delete();
            }
        }

        mother.Corpse?.Delete();
    }

    [Fact]
    public void AGloomspinnersWebParalyzesAndACropCrowFleesWhenStruck()
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Webbed" };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Here, Map.TerMur);
        pm.Hits = pm.HitsMax;
        var spinner = new Gloomspinner();
        spinner.MoveToWorld(Here, Map.TerMur);
        try
        {
            Assert.False(pm.Paralyzed);
            Assert.True(BurrowsWebs.TryWeb(spinner, pm));
            Assert.True(pm.Paralyzed);
            Assert.False(BurrowsWebs.TryWeb(spinner, pm)); // already held
            pm.Paralyzed = false;

            var crow = new CropCrow();
            crow.MoveToWorld(Here, Map.TerMur);
            Assert.True(crow.CanFly);
            Assert.Equal(CropCrow.BlackHue, crow.Hue);
            Assert.False(crow.CheckFlee());
            crow.OnGotMeleeAttack(pm, 1);
            Assert.True(crow.CheckFlee());
            crow.Delete();

            var slasher = new CacklerSlasher();
            Assert.Same(WeaponAbility.BleedAttack, slasher.GetWeaponAbility());
            slasher.Delete();
            var slinger = new BurrowScampSlinger();
            Assert.Equal(BurrowScampSlinger.RustHue, slinger.Hue);
            Assert.IsType<BurrowScampTestBow>(slinger.FindItemOnLayer(Layer.TwoHanded));
            Assert.Equal(AIType.AI_Archer, slinger.AI);
            slinger.Delete();
        }
        finally
        {
            spinner.Delete();
            pm.Delete();
        }
    }
}
