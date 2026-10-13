// ImbuingReworkVerification.cs
//
// cc-P82: the Artificers' Order Imbuing rework (design: shard-migration docs/artificers-imbuing-design.md; notes:
// shard-migration notes/cc-P82-artificers-imbuing.md). Imbues go through the Imbuing Table's own buttons, as a client
// would send them (ImbueSelfRepairGateVerification's helpers).
//
// Facts:
//   1. Knowledge is account-wide, in the recipe store's set: a grant on the account is known to every character on it,
//      and stays as imbue:<property>:<tier> keys. Free properties are Basic for a fresh account; nothing else is known.
//   2. Tiers: Basic, Refined and Full are 1/3, 2/3 and all of the OSI max (the config's fractions); Overcharged is the
//      config's maximum; imbuing is clamped to the known tier (Refined known: Full refused, Refined imbued).
//   3. The default formula books are the design's tier table exactly: a property learned at rank L gets tier t from the
//      rank L + t - 1 book (free: Basic needs no book), never past Full, and no book teaches Overcharged.
//   4. Formula books: the shop lists a member's books up to their rank, sells for Essence Shards only (no gold), and
//      reading one teaches the account and consumes it; a second copy of a read book is refused.
//   5. The Master Formulary needs the trial (not listed, not readable without it), then gives 6 slots; the config's
//      budget mode keeps 5 slots and raises the intensity budget to 600% instead.
//   6. The grant API: any property or family at any tier, including the grant-only Splintering, Reactive Paralyze and
//      Soul Charge; Fortune's Favor has no Overcharged.
//   7. Hidden menu: a fresh account's table lists only what it knows and one hint line per category with anything locked;
//      a learned property then shows, and only its known tiers are offered.
//   8. Unravel stays free: a non-member with nothing learned extracts a property.
//   9. +Skill: jewelry takes combat skills, one per OSI group (a second of the group is refused, another group's is
//      not); crafter gear takes crafting and gathering skills and no combat skill; the tiers are 5, 10, 15 and 25.
//  10. Item skill bonuses never count toward skill gain: Skills.Total sums Base only, a gain adds to Base, and a skill
//      at its cap gains nothing with a bonus on.
//  11. Custom properties: one per item; Bountiful, Prospector, Butcher's Eye, Mastercraft, Spell Leech, Shatter, Time
//      Slip and Bond do what the design says; Fortune's Favor raises a box's quality tier and never its slot count.
//  12. Sites: away from a soul forge Overcharged is refused; at an Artificers' hall forge it is imbued with the forge
//      bonus. The mule socket at II caps at Refined with no bonus, at III reaches Full with the forge bonus.
//  13. The hidden achievements: a box earned with Fortune's Favor gear put on under a minute before earns Dressed for the
//      Occasion (its own box does not count again); 25 earn Creature of Habit; gear on for longer than a minute does
//      not count.
//  14. The staff gump grants and revokes a tier and toggles the Master Formulary.
//  15. The config: the defaults round-trip; a file with Overcharged Fortune's Favor or an unknown property is refused.
//  16. Every item kind's imbue list fits the gump's button ranges (under 100 rows).

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Misc;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;
using A = ShatteredLegacy.Tests.ImbueSelfRepairGateVerification;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ImbuingReworkVerification
{
    private const int BtnDisenchantTarget = 6;
    private const int BtnApplyExtract = 9;
    private const int BtnExtractBase = 200;

    private readonly ITestOutputHelper _out;

    public ImbuingReworkVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureSkillChecks();
        ClusterFGuildSystem.EnsureRegistered();
        ImbuingTable.Current = ImbuingTable.Defaults;
        ClusterFImbueEffects.RollOverride = null;
    }

    private static ImbuingTable Copy()
    {
        var errors = new List<string>();
        var t = ImbuingTable.Parse(ImbuingTable.ToJson(ImbuingTable.Defaults), "test", errors);
        Assert.Empty(errors);
        return t;
    }

    private static ImbuePropertyDef Def(string name) => ImbueCatalogue.All.First(d => d.Name == name);

    // FindItemsByType is a pooled enumerator (no LINQ): copied into a list.
    private static List<T> InPack<T>(Container c) where T : Item
    {
        var list = new List<T>();
        foreach (var item in c.FindItemsByType<T>())
        {
            list.Add(item);
        }

        return list;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void KnowledgeIsAccountWideInTheRecipeStore()
    {
        using var a = new A.Artificer(0);
        var alt = new PlayerMobile { Player = true };
        a.Account[1] = alt;
        try
        {
            Assert.Equal(ImbueTier.Basic, ClusterFImbueKnowledge.KnownTier(a.Pm, "Luck"));
            Assert.Equal(ImbueTier.None, ClusterFImbueKnowledge.KnownTier(a.Pm, "Hit Life Leech"));

            Assert.True(ClusterFImbueKnowledge.Grant(a.Pm, "Hit Life Leech", ImbueTier.Refined));
            Assert.Equal(ImbueTier.Refined, ClusterFImbueKnowledge.KnownTier(alt, "Hit Life Leech"));
            Assert.Contains("imbue:Hit Life Leech:Refined", a.Data.LearnedRecipes);
            Assert.False(ClusterFImbueKnowledge.Grant(a.Data, "Hit Life Leech", ImbueTier.Basic)); // never lowers
            Assert.Equal(ImbueTier.Refined, ClusterFImbueKnowledge.KnownTier(a.Data, "Hit Life Leech"));
        }
        finally
        {
            alt.Delete();
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TiersAreThirdsOfTheOsiMaxAndImbuingIsClampedToTheKnownTier()
    {
        var hci = Def("Hit Chance Increase");
        Assert.Equal(15, ImbueTiers.MaxFor(hci, ImbueTier.Basic));
        Assert.Equal(30, ImbueTiers.MaxFor(hci, ImbueTier.Refined));
        Assert.Equal(45, ImbueTiers.MaxFor(hci, ImbueTier.Full));
        Assert.Equal(ImbuingTable.Current.OverchargedMax["Hit Chance Increase"], ImbueTiers.MaxFor(hci, ImbueTier.Overcharged));

        using var a = new A.Artificer(0);
        var sword = new Longsword();
        a.Pm.Backpack.DropItem(sword);
        ClusterFImbueKnowledge.Grant(a.Data, "Hit Chance Increase", ImbueTier.Refined);

        A.Imbue(a, sword, "Hit Chance Increase", 3);
        Assert.Equal(0, sword.Attributes.AttackChance);
        A.Imbue(a, sword, "Hit Chance Increase", 2);
        Assert.Equal(30, sword.Attributes.AttackChance);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheDefaultBooksAreTheDesignTierTable()
    {
        var t = ImbuingTable.Defaults;
        var taught = new Dictionary<(string, ImbueTier), int>();
        foreach (var book in t.Books)
        {
            foreach (var g in book.Grants)
            {
                var tier = Enum.Parse<ImbueTier>(g.Tier);
                Assert.True(tier <= ImbueTier.Full, $"{book.Name} teaches {g.Property} at {tier}");
                Assert.True(taught.TryAdd((g.Property, tier), book.Rank), $"{g.Property} {tier} taught twice");
            }
        }

        foreach (var (key, at) in t.Ladder)
        {
            if (!ImbuingTable.TryRankOf(at, out var learned))
            {
                Assert.DoesNotContain(taught.Keys, k => k.Item1 == key); // the 140+ path only
                continue;
            }

            for (var tier = ImbueTier.Basic; tier <= ImbueTier.Full; tier++)
            {
                var rank = learned + (int)tier - 1;
                if (rank == 0)
                {
                    Assert.False(taught.ContainsKey((key, tier)), $"{key} {tier} is free");
                }
                else if (rank <= 5)
                {
                    Assert.True(taught.TryGetValue((key, tier), out var r) && r == rank, $"{key} {tier}: rank {rank} book");
                }
                else
                {
                    Assert.False(taught.ContainsKey((key, tier)), $"{key} {tier} is the 140+ path's");
                }
            }
        }

        // The design table's rows, spot-checked by name.
        Assert.Equal(1, taught[("Luck", ImbueTier.Refined)]);
        Assert.Equal(2, taught[("Luck", ImbueTier.Full)]);
        Assert.Equal(4, taught[("Swing Speed Increase", ImbueTier.Basic)]);
        Assert.Equal(5, taught[("Swing Speed Increase", ImbueTier.Refined)]);
        Assert.False(taught.ContainsKey(("Swing Speed Increase", ImbueTier.Full)));
        Assert.Equal(5, taught[("Hit Chance Increase", ImbueTier.Full)]);
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void BooksAreSoldByRankForShardsOnlyAndTeachTheAccount()
    {
        using var a = new A.Artificer(1000); // Journeyman: rank index 1
        var rows = ImbueFormulaShop.Rows(a.Pm);
        Assert.Equal(new[] { "apprentice-formulary" }, rows.Select(r => r.Id).ToArray());

        var gold = a.GoldLeft;
        Assert.True(ImbueFormulaShop.Buy(a.Pm, "apprentice-formulary", out var said), said);
        Assert.Equal(A.Shards - 250, a.ShardsLeft);
        Assert.Equal(gold, a.GoldLeft);

        var book = a.Pm.Backpack.FindItemByType<ImbueFormulaBook>();
        Assert.NotNull(book);
        book.OnDoubleClick(a.Pm);
        Assert.True(book.Deleted);
        Assert.Equal(ImbueTier.Basic, ClusterFImbueKnowledge.KnownTier(a.Data, "Hit Life Leech"));
        Assert.Equal(ImbueTier.Refined, ClusterFImbueKnowledge.KnownTier(a.Data, "Luck"));
        Assert.Equal(ImbueTier.Basic, ClusterFImbueKnowledge.KnownTier(a.Data, ImbueFamilies.JewelrySkill));
        Assert.Empty(ImbueFormulaShop.Rows(a.Pm));

        var second = new ImbueFormulaBook("apprentice-formulary");
        a.Pm.Backpack.DropItem(second);
        second.OnDoubleClick(a.Pm);
        Assert.False(second.Deleted);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void TheMasterFormularyNeedsTheTrialThenGivesASlotOrTheBudget()
    {
        var waived = ClusterFGuildPromotion.TrialsWaivedUntilBuilt;
        ClusterFGuildPromotion.TrialsWaivedUntilBuilt = false;
        try
        {
            using var a = new A.Artificer(40000);
            Assert.DoesNotContain(ImbueFormulaShop.Rows(a.Pm), r => r.Id == "master-formulary");
            var early = new ImbueFormulaBook("master-formulary");
            a.Pm.Backpack.DropItem(early);
            early.OnDoubleClick(a.Pm);
            Assert.False(early.Deleted);
            Assert.Equal(5, ClusterFImbueKnowledge.MaxSlots(a.Data));

            a.Guild.TrialsPassed.Add("artificers");
            Assert.Contains(ImbueFormulaShop.Rows(a.Pm), r => r.Id == "master-formulary");
            early.OnDoubleClick(a.Pm);
            Assert.True(early.Deleted);
            Assert.Equal(6, ClusterFImbueKnowledge.MaxSlots(a.Data));
            Assert.Equal(ImbueTier.Full, ClusterFImbueKnowledge.KnownTier(a.Data, "Hit Chance Increase"));
            Assert.Equal(ImbueTier.Refined, ClusterFImbueKnowledge.KnownTier(a.Data, "Faster Casting"));

            var budget = Copy();
            budget.MasterMode = "budget";
            ImbuingTable.Current = budget;
            Assert.Equal(5, ClusterFImbueKnowledge.MaxSlots(a.Data));
            Assert.Equal(600, ClusterFImbueKnowledge.IntensityBudget(a.Data));
        }
        finally
        {
            ClusterFGuildPromotion.TrialsWaivedUntilBuilt = waived;
            ImbuingTable.Current = ImbuingTable.Defaults;
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheGrantApiTakesEveryPropertyAndTheGrantOnlyOnes()
    {
        using var a = new A.Artificer(0);
        foreach (var key in new[] { "Splintering", "Reactive Paralyze", "Soul Charge", "Echo of the Loop", "Swing Speed Increase" })
        {
            Assert.True(ClusterFImbueKnowledge.Grant(a.Data, key, ImbueTier.Overcharged), key);
            Assert.Equal(ImbueTier.Overcharged, ClusterFImbueKnowledge.KnownTier(a.Data, key));
        }

        Assert.False(ClusterFImbueKnowledge.Grant(a.Data, "Fortune's Favor", ImbueTier.Overcharged));
        Assert.False(ClusterFImbueKnowledge.Grant(a.Data, ImbueFamilies.SuperSlayer, ImbueTier.Overcharged)); // on or off: Basic is all
        Assert.True(ClusterFImbueKnowledge.Grant(a.Data, ImbueFamilies.SuperSlayer, ImbueTier.Basic));
        Assert.True(ClusterFImbueKnowledge.Grant(a.Data, "Fortune's Favor", ImbueTier.Full));
        Assert.Equal(0, ImbueTiers.MaxFor(Def("Fortune's Favor"), ImbueTier.Overcharged));
        Assert.Throws<ArgumentException>(() => ClusterFImbueKnowledge.Grant(a.Data, "No Such Property", ImbueTier.Basic));
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheMenuHidesWhatIsUnknownAndHintsOncePerCategory()
    {
        using var a = new A.Artificer(0);
        var sword = new Longsword();
        a.Pm.Backpack.DropItem(sword);

        var labels = A.AllLabels(a, sword);
        _out.WriteLine(string.Join(" | ", labels));
        Assert.Contains("Self Repair", labels);
        Assert.DoesNotContain("Hit Life Leech", labels);
        var hints = labels.Where(l => l.Contains(" locked. Learn more: ")).ToList();
        Assert.Contains(hints, h => h.StartsWith("Weapon:"));
        Assert.Equal(hints.Count, hints.Select(h => h[..h.IndexOf(':')]).Distinct().Count());

        ClusterFImbueKnowledge.Grant(a.Data, "Hit Life Leech", ImbueTier.Basic);
        Assert.Contains("Hit Life Leech", A.AllLabels(a, sword));

        var index = ImbueCatalogue.ForItem(sword).FindIndex(d => d.Name == "Hit Life Leech");
        var select = A.Next(a, A.View(a, sword), A.BtnPropBase + index);
        var tiers = select.Entries.OfType<GumpLabel>().Select(l => l.Text).ToList();
        Assert.Contains(tiers, t => t.StartsWith("Basic:"));
        Assert.DoesNotContain(tiers, t => t.StartsWith("Refined"));
    }

    // ---------------------------------------------------------------- 8

    [Fact]
    public void UnravelIsFreeForAnyone()
    {
        using var a = new A.Artificer(0);
        a.Guild.JoinedGuilds.Remove("artificers");
        var sword = new Longsword();
        sword.WeaponAttributes.HitLeechHits = 30;
        a.Pm.Backpack.DropItem(sword);

        var index = ImbueCatalogue.ForItem(sword).FindIndex(d => d.Name == "Hit Life Leech");
        var table = new ArtificersImbueGump(a.Pm);
        A.Press(table, a.Ns, BtnDisenchantTarget);
        while (a.Pm.CloseGump<ArtificersImbueGump>())
        {
        }

        a.Pm.Target.Invoke(a.Pm, sword);
        var view = a.Pm.FindGump<ArtificersImbueGump>();
        var confirm = A.Next(a, view, BtnExtractBase + index);
        A.Next(a, confirm, BtnApplyExtract);

        Assert.Equal(0, sword.WeaponAttributes.HitLeechHits);
        Assert.Contains(InPack<PropertyEssence>(a.Pm.Backpack), e => e.PropertyKey == "Hit Life Leech");
    }

    // ---------------------------------------------------------------- 9

    [Fact]
    public void SkillBonusesFollowTheGroupsAndTheGearKinds()
    {
        using var a = new A.Artificer(0);
        ClusterFImbueKnowledge.Grant(a.Data, ImbueFamilies.JewelrySkill, ImbueTier.Full);
        ClusterFImbueKnowledge.Grant(a.Data, ImbueFamilies.CrafterSkill, ImbueTier.Full);
        var ring = new GoldRing();
        var apron = new HalfApron();
        a.Pm.Backpack.DropItem(ring);
        a.Pm.Backpack.DropItem(apron);

        var swords = Def("Skill: Swordsmanship");
        Assert.Equal(new[] { 5, 10, 15, 25 }, new[] { ImbueTier.Basic, ImbueTier.Refined, ImbueTier.Full, ImbueTier.Overcharged }
            .Select(t => ImbueTiers.MaxFor(swords, t)).ToArray());

        A.Imbue(a, ring, "Skill: Swordsmanship", 3);
        Assert.Equal(15, (int)ring.SkillBonuses.GetBonus(0));
        Assert.Equal(SkillName.Swords, ring.SkillBonuses.GetSkill(0));

        A.Imbue(a, ring, "Skill: Fencing", 3); // the same group: refused
        Assert.Equal(SkillName.Swords, ring.SkillBonuses.GetSkill(0));
        A.Imbue(a, ring, "Skill: Tactics", 2); // another group
        Assert.Equal(10, (int)ring.SkillBonuses.GetBonus(1));

        var apronRows = ImbueCatalogue.ForItem(apron).Select(d => d.Name).ToList();
        Assert.Contains("Skill: Blacksmithy", apronRows);
        Assert.Contains("Skill: Mining", apronRows);
        Assert.DoesNotContain("Skill: Swordsmanship", apronRows);
        Assert.DoesNotContain("Skill: Blacksmithy", ImbueCatalogue.ForItem(ring).Select(d => d.Name));
        Assert.DoesNotContain("Skill: Blacksmithy", ImbueCatalogue.ForItem(new Shirt()).Select(d => d.Name));

        A.Imbue(a, apron, "Skill: Blacksmithy", 3);
        Assert.Equal(SkillName.Blacksmith, apron.SkillBonuses.GetSkill(0));
        Assert.Equal(15, (int)apron.SkillBonuses.GetBonus(0));
    }

    // ---------------------------------------------------------------- 10

    [Fact]
    public void ItemSkillBonusesNeverCountTowardGain()
    {
        using var a = new A.Artificer(0);
        var skill = a.Pm.Skills[SkillName.Swords];
        skill.Base = 50.0;
        var ring = new GoldRing();
        ring.SkillBonuses.SetValues(0, SkillName.Swords, 15);
        a.Pm.EquipItem(ring);
        Assert.Equal(65.0, skill.Value);

        var baseSum = Enumerable.Range(0, a.Pm.Skills.Length).Sum(i => a.Pm.Skills[i].BaseFixedPoint);
        Assert.Equal(baseSum, a.Pm.Skills.Total);

        SkillCheck.Gain(a.Pm, skill);
        Assert.Equal(50.1, skill.Base, 3);
        Assert.Equal(baseSum + 1, a.Pm.Skills.Total);

        // At its cap a skill gains nothing, whatever is worn (the cap check reads Base).
        skill.Cap = 50.1;
        SkillCheck.Gain(a.Pm, skill);
        Assert.Equal(50.1, skill.Base, 3);
    }

    // ---------------------------------------------------------------- 11

    [Fact]
    public void CustomPropertiesDoWhatTheDesignSays()
    {
        using var a = new A.Artificer(0);
        ClusterFImbueEffects.RollOverride = 0.0; // every chance happens
        try
        {
            ClusterFImbueKnowledge.Grant(a.Data, "Bountiful", ImbueTier.Full);
            ClusterFImbueKnowledge.Grant(a.Data, "Prospector", ImbueTier.Full);
            var ring = new GoldRing();
            a.Pm.Backpack.DropItem(ring);

            // One per item, through the table: Bountiful goes on (its ingredient taken), Prospector is then refused.
            a.Pm.Backpack.DropItem(new SpiderCarapace(5));
            A.Imbue(a, ring, "Bountiful", 3);
            Assert.Equal(15, ImbueCustomStore.Get(ring, "Bountiful"));
            Assert.Null(a.Pm.Backpack.FindItemByType<SpiderCarapace>());
            a.Pm.Backpack.DropItem(new SlithTongue(5));
            A.Imbue(a, ring, "Prospector", 3);
            Assert.Equal(0, ImbueCustomStore.Get(ring, "Prospector"));

            a.Pm.EquipItem(ring);
            var ore = new IronOre(10);
            ClusterFImbueEffects.OnGathered(a.Pm, ore, "mining");
            Assert.Equal(12, ore.Amount); // 15% of 10 is 1.5: 1, and the half by chance

            // The rest, set straight on worn gear.
            var bracelet = new GoldBracelet();
            ImbueCustomStore.Set(bracelet, "Mastercraft", 5);
            a.Pm.EquipItem(bracelet);
            Assert.Equal(0.05, ClusterFImbueEffects.MastercraftBonus(a.Pm), 6);

            var necklace = new GoldNecklace();
            ImbueCustomStore.Set(necklace, "Spell Leech", 5);
            a.Pm.EquipItem(necklace);
            a.Pm.Hits = 10;
            var foe = new Orc();
            foe.MoveToWorld(a.Pm.Location, a.Pm.Map);
            ClusterFImbueEffects.OnSpellDamage(a.Pm, foe, 40);
            Assert.Equal(12, a.Pm.Hits);

            var before = new[] { ResistanceType.Physical, ResistanceType.Fire, ResistanceType.Cold, ResistanceType.Poison, ResistanceType.Energy }
                .ToDictionary(t => t, foe.GetResistance);
            ClusterFImbueEffects.Shatter(foe);
            var highest = before.OrderByDescending(p => p.Value).First().Key;
            Assert.Equal(before[highest] - 10, foe.GetResistance(highest));

            var sword = new Longsword();
            ImbueCustomStore.Set(sword, "Time Slip", 5);
            a.Pm.AddItem(sword); // straight onto its layer (the weapon's equip checks are not the fact here)
            Assert.Equal(a.Pm, sword.Parent);
            a.Pm.Mana = 20;
            _out.WriteLine($"Time Slip worn {ClusterFImbueEffects.Worn(a.Pm, "Time Slip")}, mana {a.Pm.Mana}/{a.Pm.ManaMax}");
            ClusterFImbueEffects.OnWeaponHit(a.Pm, foe, WeaponAbility.ArmorIgnore, 50);
            Assert.Equal(50, a.Pm.Mana);

            var cloak = new Cloak();
            ImbueCustomStore.Set(cloak, "Bond", 10);
            a.Pm.EquipItem(cloak);
            var pet = new Horse { Controlled = true, ControlMaster = a.Pm };
            var damage = 50;
            ClusterFImbueEffects.AlterMeleeDamage(pet, foe, ref damage);
            Assert.Equal(55, damage);
            Assert.Equal(110, ClusterFImbueEffects.BondXp(pet, 100));
            pet.Delete();
            foe.Delete();
        }
        finally
        {
            ClusterFImbueEffects.RollOverride = null;
        }
    }

    [Fact]
    public void FortunesFavorRaisesTheQualityTierAndNeverTheSlotCount()
    {
        using var a = new A.Artificer(0);
        ClusterFImbueEffects.RollOverride = 0.0;
        try
        {
            var plain = LootBoxRoller.Roll(a.Pm, BoxTheme.Explorer, 3, null);
            Assert.Equal(3, plain.QualityTier);

            var ring = new GoldRing();
            ImbueCustomStore.Set(ring, "Fortune's Favor", 5);
            a.Pm.EquipItem(ring);
            Assert.Equal(5, ClusterFImbueEffects.FortunePercent(a.Pm));

            var lucky = LootBoxRoller.Roll(a.Pm, BoxTheme.Explorer, 3, null);
            Assert.Equal(4, lucky.QualityTier);
            Assert.Equal(plain.Pools.Count, lucky.Pools.Count);
            Assert.Equal(plain.Gold, lucky.Gold);
            Assert.Equal(LootTable.Current.SlotsFor(3) - 2, lucky.Pools.Count);

            foreach (var (item, _) in plain.Items.Concat(lucky.Items))
            {
                item.Delete();
            }
        }
        finally
        {
            ClusterFImbueEffects.RollOverride = null;
        }
    }

    // ---------------------------------------------------------------- 12

    [Fact]
    public void OverchargedNeedsASoulForge()
    {
        using var a = new A.Artificer(0);
        ClusterFImbueKnowledge.Grant(a.Data, "Luck", ImbueTier.Overcharged);
        var ring = new GoldRing();
        a.Pm.Backpack.DropItem(ring);

        Assert.Equal("Anywhere", ImbueSites.Resolve(a.Pm).Key);
        A.Imbue(a, ring, "Luck", 4);
        Assert.Equal(0, ring.Attributes.Luck);

        var forge = new ArtificersSoulForge();
        forge.MoveToWorld(new Point3D(a.Pm.X + 1, a.Pm.Y, a.Pm.Z), a.Pm.Map);
        try
        {
            var site = ImbueSites.Resolve(a.Pm);
            Assert.Equal(("Forge", ImbueTier.Overcharged, 2), (site.Key, site.Cap, site.BonusPercent));
            A.Imbue(a, ring, "Luck", 4);
            Assert.Equal(150, ring.Attributes.Luck);
        }
        finally
        {
            forge.Delete();
        }
    }

    [Fact]
    public void TheMuleSocketCapsAtRefinedThenFull()
    {
        var t = Copy();
        t.SiteCaps["Anywhere"] = "None"; // so the mule is the best place here
        ImbuingTable.Current = t;
        MuleModules.Configure();
        var account = new Account($"p82m{Guid.NewGuid():N}"[..16], "p82-test-only");
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(3460, 2604, 18), Map.Trammel);
        var mule = PackMule.SummonFor(pm, 3, MuleGenes.Fresh(), "here");
        try
        {
            Assert.Equal(ImbueTier.None, ImbueSites.Resolve(pm).Cap);

            var two = new MuleModule(MuleModuleId.ImbuedBardingSocket, 2);
            Assert.True(MuleModules.TryAttach(pm, mule, two));
            var site = ImbueSites.Resolve(pm);
            Assert.Equal(("MuleII", ImbueTier.Refined, 0), (site.Key, site.Cap, site.BonusPercent));

            Assert.True(MuleModules.Detach(pm, mule, two));
            Assert.True(MuleModules.TryAttach(pm, mule, new MuleModule(MuleModuleId.ImbuedBardingSocket, 3)));
            site = ImbueSites.Resolve(pm);
            Assert.Equal(("MuleIII", ImbueTier.Full, 2), (site.Key, site.Cap, site.BonusPercent));
        }
        finally
        {
            ImbuingTable.Current = ImbuingTable.Defaults;
            mule?.Delete();
            pm.Delete();
            Accounts.Remove(account);
        }
    }

    // ---------------------------------------------------------------- 13

    [Fact]
    public void LuckGearJustPutOnEarnsTheHiddenAchievements()
    {
        using var a = new A.Artificer(0);
        var ring = new GoldRing();
        ImbueCustomStore.Set(ring, "Fortune's Favor", 3);
        a.Pm.EquipItem(ring);

        Assert.Equal(ClusterFLootBoxes.AwardResult.Box,
            ClusterFLootBoxes.Award(a.Pm, "test:p82-1", BoxTheme.Explorer, 1, null, "a test", out _));
        Assert.True(ClusterFAchievementSystem.HasEarned(a.Account, ClusterFAchievementSystem.DressedKey));
        Assert.Equal(1, ClusterFAchievementSystem.GetCounter(a.Account.Username, ClusterFAchievementSystem.LuckyGearCounter));
        Assert.True(ClusterFAchievementSystem.Definitions[ClusterFAchievementSystem.DressedKey].Hidden);

        for (var i = 2; i <= 24; i++)
        {
            ClusterFLootBoxes.Award(a.Pm, $"test:p82-{i}", BoxTheme.Explorer, 1, null, "a test", out _);
        }

        Assert.False(ClusterFAchievementSystem.HasEarned(a.Account, ClusterFAchievementSystem.HabitKey));
        ClusterFLootBoxes.Award(a.Pm, "test:p82-25", BoxTheme.Explorer, 1, null, "a test", out _);
        Assert.True(ClusterFAchievementSystem.HasEarned(a.Account, ClusterFAchievementSystem.HabitKey));

        ClusterFImbueEffects.SetEquippedAt(ring, Core.Now - TimeSpan.FromSeconds(61));
        ClusterFLootBoxes.Award(a.Pm, "test:p82-26", BoxTheme.Explorer, 1, null, "a test", out _);
        Assert.Equal(25, ClusterFAchievementSystem.GetCounter(a.Account.Username, ClusterFAchievementSystem.LuckyGearCounter));

        foreach (var box in InPack<LootBox>(a.Pm.Backpack))
        {
            box.Delete();
        }

        foreach (var box in InPack<LootBox>(a.Pm.BankBox))
        {
            box.Delete();
        }
    }

    // ---------------------------------------------------------------- 14

    [Fact]
    public void TheStaffGumpGrantsRevokesAndToggles()
    {
        using var a = new A.Artificer(0);
        var staff = new PlayerMobile { Player = true, AccessLevel = AccessLevel.GameMaster };
        var ns = Server.Tests.Network.PacketTestUtilities.CreateTestNetState();
        staff.NetState = ns;
        ns.Mobile = staff;
        try
        {
            var keys = ImbueFamilies.AllKeys();
            var i = keys.IndexOf("Hit Life Leech");
            var gump = new ImbueKnowledgeGump(a.Account, 0);
            A.Press(gump, ns, 1000 + i * 2);
            Assert.Equal(ImbueTier.Basic, ClusterFImbueKnowledge.KnownTier(a.Data, "Hit Life Leech"));
            A.Press(gump, ns, 1000 + i * 2);
            Assert.Equal(ImbueTier.Refined, ClusterFImbueKnowledge.KnownTier(a.Data, "Hit Life Leech"));
            A.Press(gump, ns, 1001 + i * 2);
            Assert.Equal(ImbueTier.Basic, ClusterFImbueKnowledge.KnownTier(a.Data, "Hit Life Leech"));
            A.Press(gump, ns, 10);
            Assert.True(ClusterFImbueKnowledge.HasMasterFormulary(a.Data));
        }
        finally
        {
            staff.NetState = null;
            ns.Mobile = null;
            staff.Delete();
        }
    }

    // ---------------------------------------------------------------- 15

    [Fact]
    public void TheConfigRoundTripsAndABadFileIsRefusedWhole()
    {
        var t = Copy();
        Assert.Equal(ImbuingTable.Defaults.Books.Count, t.Books.Count);

        var json = ImbuingTable.ToJson(ImbuingTable.Defaults);
        var errors = new List<string>();
        Assert.Null(ImbuingTable.Parse(json.Replace("\"overcharged\": 0", "\"overcharged\": 7"), "bad", errors));
        Assert.Contains(errors, e => e.Contains("Fortune's Favor"));

        errors.Clear();
        Assert.Null(ImbuingTable.Parse(json.Replace("\"Echo of the Loop\": \"140\"", "\"Echo of the Toast\": \"140\""), "bad", errors));
        Assert.Contains(errors, e => e.Contains("Echo of the Toast"));
        _out.WriteLine(string.Join("\n", errors));
    }

    // ---------------------------------------------------------------- 16

    [Fact]
    public void EveryItemKindsListFitsTheButtonRanges()
    {
        foreach (Item item in new Item[] { new Longsword(), new Bow(), new PlateChest(), new GoldRing(), new HalfApron(), new Shirt() })
        {
            var count = ImbueCatalogue.ForItem(item).Count;
            _out.WriteLine($"{item.GetType().Name}: {count} rows");
            Assert.InRange(count, 1, 99);
            item.Delete();
        }

        Assert.InRange(ImbueCatalogue.All.Count, 1, 699);
    }
}
