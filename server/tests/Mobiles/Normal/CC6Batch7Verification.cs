// CC6 batch 7: the eight remaining spawn-name aliases (Q-055, answered yes), the leaf-creature row and one leaf item.
//
// Part B ports ServUO's DragonsFlameGrandMage (as MageDragonsFlameMage, on our DragonsFlameMage) with its DragonFlameKey,
// and the seven Clan ratmen (ClanCA..ClanSSW, as the spawn data spells them: Q-056). Part C adds TreasureLevel1h to
// our TreasureChestMod.cs for the Sea Market's four spawners. The facts for B and C are the second half of this file.
//
// Part A. Pinned ModernUO's own spawn data (Distribution/Data/Spawns, a converted Nerun's Distro, commit 0d1fffd9f)
// names eight more of pinned's own types under spellings no assembly declares: "DryadA"/"Dryada" (two Twisted Weald
// spawners) for stock MLDryad, and "abbein", "alethanian", "aneen", "jothan", "mallew", "taellia", "vicaie" (Heartwood,
// Trammel and Felucca) for pinned's ML-quest NPCs ElderAbbein, ElderAlethanian, LorekeeperAneen, ElderJothan,
// ElderMallew, ElderTaellia, ElderVicaie. Batch 6 built the route for one name (Mobiles/Aliases/EliteNinjaSpawnAlias.cs,
// its test in CC6Batch6SpawnAliasVerification.cs); this batch rolls it out to the eight, one file each under
// Mobiles/Aliases/, and this file carries one fact per alias: the spawn spelling resolves to the stock type, the
// attribute is on that type and carries exactly that alias, and a spawner placed where the data places it fills with
// an instance of that type. The mappings were re-verified before the files were written (notes/cc6-batch7.md §2): the
// seven ServUO NPCs are BaseVendors with no quest and no stock, matching pinned's by name, title and sex; pinned's
// Data/MLQuests.cfg binds no quest to any of the seven either, which is upstream's gap and is logged, not asserted.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh (see batch 1's file for the host facts). The test host registers the six maps with
// no tile data (Server.Tests/Fixtures/TestMapDefinitions.cs), so the spawners below keep SpawnBounds at default:
// GetSpawnPosition then returns the spawner's own location without touching the map (BaseSpawner.cs:571-577).

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CC6Batch7Verification
{
    private readonly ITestOutputHelper _out;

    public CC6Batch7Verification(ITestOutputHelper output) => _out = output;

    // (spawn spelling exactly as the JSON carries it, the stock type, the alias string on it, the type's own
    // DefaultName, where the data places it). Locations are the first spawner naming each in pinned's files.
    private static readonly (string SpawnName, Type Type, string Alias, string Name, Point3D Where, Map Map)[] Aliases =
    {
        ("DryadA", typeof(MLDryad), "Server.Mobiles.DryadA", "a dryad", new Point3D(2171, 1181, -44), Map.Ilshenar),
        ("abbein", typeof(ElderAbbein), "Server.Engines.MLQuests.Definitions.Abbein", "Elder Abbein", new Point3D(7048, 382, 0), Map.Trammel),
        ("alethanian", typeof(ElderAlethanian), "Server.Engines.MLQuests.Definitions.Alethanian", "Elder Alethanian", new Point3D(7048, 382, 0), Map.Trammel),
        ("aneen", typeof(LorekeeperAneen), "Server.Engines.MLQuests.Definitions.Aneen", "Lorekeeper Aneen", new Point3D(7053, 338, 0), Map.Trammel),
        ("jothan", typeof(ElderJothan), "Server.Engines.MLQuests.Definitions.Jothan", "Elder Jothan", new Point3D(7048, 382, 0), Map.Trammel),
        ("mallew", typeof(ElderMallew), "Server.Engines.MLQuests.Definitions.Mallew", "Elder Mallew", new Point3D(7048, 382, 0), Map.Trammel),
        ("taellia", typeof(ElderTaellia), "Server.Engines.MLQuests.Definitions.Taellia", "Elder Taellia", new Point3D(7048, 382, 0), Map.Trammel),
        ("vicaie", typeof(ElderVicaie), "Server.Engines.MLQuests.Definitions.Vicaie", "Elder Vicaie", new Point3D(7048, 382, 0), Map.Trammel)
    };

    // A Heartwood name nothing aliases: ServUO's Athialon has no pinned counterpart (batch 6 §2c), so it stays an
    // orphan until it is ported, and this file must keep saying so. It replaces batch 6's control, which Part B ports.
    private const string ControlName = "athialon";

    [Fact]
    public void EachOfTheEightSpawnNamesResolvesToItsStockTypeThroughItsOwnAlias()
    {
        Assert.Equal(8, Aliases.Length);

        foreach (var (spawnName, type, alias, name, _, _) in Aliases)
        {
            // The alias sits on the stock type itself: one attribute, one alias, read with inherit: false exactly as
            // TypeCache reads it (AssemblyHandler.cs:255).
            var attr = type.GetCustomAttribute<TypeAliasAttribute>(false);
            Assert.NotNull(attr);
            Assert.Equal(new[] { alias }, attr.Aliases);

            // The spawner path: BaseSpawner.Spawn calls FindTypeByName(entry.SpawnedName) with the defaults
            // (fullName: false, ignoreCase: true), which the JSON's lower-case spellings depend on.
            Assert.Same(type, AssemblyHandler.FindTypeByName(spawnName));
            Assert.Same(type, AssemblyHandler.FindTypeByName(spawnName.ToUpperInvariant()));
            Assert.Same(type, AssemblyHandler.FindTypeByName(alias, fullName: true));

            // The stock name is untouched, and the type's own FullName - the string a save writes - is its own.
            Assert.Same(type, AssemblyHandler.FindTypeByName(type.Name));
            Assert.Equal(type.Namespace + "." + type.Name, type.FullName);

            // What a player sees is the stock type's own name.
            var m = (Mobile)Activator.CreateInstance(type)!;
            Assert.Equal(name, m.Name);
            _out.WriteLine($"{spawnName} -> {type.FullName} '{m.Name}' {m.Title} body={(int)m.Body}");
            m.Delete();
        }

        // The Weald's second spelling is the same key to a case-insensitive lookup.
        Assert.Same(typeof(MLDryad), AssemblyHandler.FindTypeByName("Dryada"));
    }

    [Fact]
    public void ASpawnerPlacedWhereTheDataPlacesEachNameFillsWithTheStockType()
    {
        foreach (var (spawnName, type, _, _, where, map) in Aliases)
        {
            var spawner = new Spawner();
            spawner.MoveToWorld(where, map);
            spawner.SpawnBounds = default; // no tile data in the host: spawn at the spawner's own location
            var entry = spawner.AddEntry(spawnName, 100, 1, dotimer: false);

            try
            {
                Assert.True(spawner.Spawn(entry, out var flags), $"{spawnName}: Spawn returned false with flags {flags}");
                Assert.Equal(EntryFlags.None, flags);

                var spawned = Assert.Single(spawner.Spawned.Keys);
                var mobile = Assert.IsAssignableFrom<Mobile>(spawned);
                Assert.Same(type, mobile.GetType());
                Assert.Same(map, mobile.Map);
                Assert.Equal(where, mobile.Location);
                Assert.Same(entry, spawner.Spawned[spawned]);
                Assert.Contains(spawned, entry.Spawned);

                _out.WriteLine($"spawner at {where} on {map} filled with {type.Name} '{mobile.Name}'");
            }
            finally
            {
                spawner.Delete(); // BaseSpawner.OnDelete removes what it spawned
            }
        }
    }

    [Fact]
    public void TheSevenHeartwoodEldersAreQuestGiversByShapeAndPinnedBindsThemNoQuest()
    {
        // The re-verification batch 6 asked for. ServUO's Abbein..Vicaie are BaseVendors with an empty InitSBInfo and
        // no quest list (Mobiles/NPCs/*.cs); pinned's are BaseCreature (IQuestGiver) with AI_Vendor, invulnerable,
        // and Data/MLQuests.cfg names none of the seven as a quester, so neither tree gives them a quest. The mapping
        // holds on everything a player sees; the missing quests are upstream's own gap (27 of Heartwood.cs's 48 NPC
        // types are bound, 21 are not) and are logged here, not asserted, so a pinned bump that binds them stays green.
        foreach (var (_, type, _, _, _, _) in Aliases.Skip(1))
        {
            var bc = (BaseCreature)Activator.CreateInstance(type)!;
            Assert.IsAssignableFrom<IQuestGiver>(bc);
            Assert.Equal(AIType.AI_Vendor, bc.AI);
            Assert.True(bc.IsInvulnerable);
            Assert.Same(Race.Elf, bc.Race);
            Assert.Equal(type.Name.StartsWith("Elder") ? "the wise" : "the keeper of tradition", bc.Title);

            var bound = MLQuestSystem.QuestGivers.TryGetValue(type, out var quests) ? quests.Count : 0;
            _out.WriteLine($"{type.Name}: {bound} quest(s) bound by Data/MLQuests.cfg");
            bc.Delete();
        }
    }

    [Fact]
    public void ANameWithNoAliasStillFlagsInvalidType()
    {
        // The negative control. If this ever passes for the wrong reason - the name ported, or aliased - the orphan
        // count every CC6 batch test pins moves too, and this file says so.
        Assert.Null(AssemblyHandler.FindTypeByName(ControlName));

        var spawner = new Spawner();
        spawner.MoveToWorld(new Point3D(7048, 382, 0), Map.Trammel);
        spawner.SpawnBounds = default;
        var entry = spawner.AddEntry(ControlName, 100, 1, dotimer: false);

        try
        {
            Assert.False(spawner.Spawn(entry, out var flags));
            Assert.Equal(EntryFlags.InvalidType, flags);
            Assert.Empty(spawner.Spawned);
        }
        finally
        {
            spawner.Delete();
        }
    }

    // ---- Part B, the leaf-creature row, and Part C, the leaf item ----------------------------------------------

    // (type, spawn spelling exactly as the JSON carries it, ServUO Name, ServUO CorpseName, Body, Hue, Fame, Karma).
    // The types are named by the spawn-data spelling (Q-056); the names a player sees are ServUO's, except the
    // tinkerer's, which is OSI's (D-76).
    private static readonly (Type Type, string SpawnName, string Name, string Corpse, int Body, int Hue, int Fame, int Karma)[] Leaf =
    {
        (typeof(MageDragonsFlameMage), "magedragonsflamemage", "Black Order Grand Mage", "a black order grand mage corpse", -1, -1, 25000, -25000),
        (typeof(ClanChitterAssistant), "ClanChitterAssistant", "Clan Chitter Assistant", "a clan chitter assistant corpse", 0x8E, 0, 6500, -6500),
        (typeof(ClanChitterTinkerer), "ClanChitterTinkerer", "Clan Chitter Tinkerer", "a clan chitter tinkerer corpse", 0x8E, 0, 6500, -6500),
        (typeof(ClanRibbonCourtier), "ClanRibbonCourtier", "Clan Ribbon Courtier", "a clan ribbon courtier corpse", 42, 2207, 1500, -1500),
        (typeof(ClanRibbonSupplicant), "ClanRibbonSupplicant", "Clan Ribbon Supplicant", "a clan ribbon supplicant corpse", 42, 2952, 1500, -1500),
        (typeof(ClanScratchHenchrat), "ClanScratchHenchrat", "Clan Scratch Henchrat", "a clan scratch henchrat corpse", 42, 0, 1500, -1500),
        (typeof(ClanScratchScrounger), "ClanScratchScrounger", "Clan Scratch Scrounger", "a clan scratch scrounger corpse", 0x8E, 0, 6500, -6500),
        (typeof(ClanScratchSavageWolf), "ClanScratchSavageWolf", "Clan Scratch Savage Wolf", "a clan scratch savage wolf corpse", 98, 0x2C, 3400, -3400)
    };

    [Fact]
    public void TheLeafRowConstructsWithItsServUOValues()
    {
        Assert.Equal(8, Leaf.Length);

        foreach (var (type, spawnName, name, corpse, body, hue, fame, karma) in Leaf)
        {
            var bc = (BaseCreature)Activator.CreateInstance(type)!;

            _out.WriteLine($"{type.Name}: name='{bc.Name}' title='{bc.Title}' body={(int)bc.Body} hue={bc.Hue} " +
                           $"fame={bc.Fame} karma={bc.Karma} hits={bc.HitsMax} ai={bc.AI} speed={bc.ActiveSpeed}/{bc.PassiveSpeed}");

            Assert.Equal(name, bc.Name);
            Assert.Equal(corpse, bc.CorpseName);
            Assert.Equal(fame, bc.Fame);
            Assert.Equal(karma, bc.Karma);
            Assert.True(bc.HitsMax > 0, $"{type.Name} has no hits");

            if (body >= 0)
            {
                Assert.Equal(body, (int)bc.Body);
                Assert.Equal(hue, bc.Hue);
            }
            else
            {
                Assert.True(bc.Body.IsHuman, $"{type.Name} has body {(int)bc.Body}, not a human body"); // Race.Human, from the parent
            }

            // Q-008: every ported creature takes ModernUO's Medium fallback; none of these overrides it.
            Assert.Equal(0.25, bc.ActiveSpeed);
            Assert.Equal(0.5, bc.PassiveSpeed);

            // The spawner path, by the JSON's own spelling (lower case for the grand mage).
            Assert.Same(type, AssemblyHandler.FindTypeByName(spawnName));

            // Each carries its own generator attribute (inherit: false) and its own (Serial) constructor.
            Assert.Single(type.GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
            Assert.Same(type, type.GetConstructor(new[] { typeof(Serial) })!.DeclaringType);

            bc.Delete();
        }
    }

    [Fact]
    public void EveryStockSpawnEntryNamingOneOfTheSeventeenNowResolves()
    {
        var spawnsDir = Path.Combine(Core.BaseDirectory, "Data", "Spawns");
        Assert.True(Directory.Exists(spawnsDir), $"no spawn data at {spawnsDir}");

        var files = Directory.GetFiles(spawnsDir, "*.json", SearchOption.AllDirectories);
        Assert.True(files.Length > 100, $"expected pinned ModernUO's 109 spawn files, found {files.Length}");

        var entries = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(file));

            foreach (var spawner in doc.RootElement.EnumerateArray())
            {
                if (!spawner.TryGetProperty("entries", out var list))
                {
                    continue;
                }

                foreach (var entry in list.EnumerateArray())
                {
                    var name = entry.GetProperty("name").GetString()!;
                    entries[name] = entries.GetValueOrDefault(name) + 1;
                }
            }
        }

        var unresolved = entries.Keys.Where(n => AssemblyHandler.FindTypeByName(n) == null).OrderBy(n => n).ToList();
        _out.WriteLine($"{entries.Count} distinct entry names in {files.Length} files; {unresolved.Count} still resolve to no type: " +
                       string.Join(", ", unresolved));

        var referenced = 0;
        var names = Aliases.Select(a => a.SpawnName).Concat(Leaf.Select(l => l.SpawnName)).Append("treasurelevel1h").ToList();
        Assert.Equal(17, names.Count);

        foreach (var name in names)
        {
            Assert.True(entries.TryGetValue(name, out var count) && count > 0,
                $"{name} is not named by any stock spawn entry; it was chosen because it is");
            Assert.DoesNotContain(name, unresolved, StringComparer.OrdinalIgnoreCase);
            referenced += count;
            _out.WriteLine($"{name}: {count} spawn entries");
        }

        // Batch 6 left exactly 40 names unresolved (tools/spawn-orphans.txt at 21aef83). Part A's eight aliases took
        // it to 32, Part B's eight creatures to 24, Part C's chest to 23; nothing else in this batch adds or removes a
        // spawnable type (DragonFlameKey is an item no spawner names), so the count must be exactly 23.
        // CC6 batch 8 (2026-09-22), Parts A and C: Navrey Night-Eyes ported (NavreyNightEyes, one name), so it is now 22.
        // Part B: SlasherOfVeils and StygianDragon ported (two names), so it is now 20.
        // Part D: Medusa ported (one name), so it is now 19.
        Assert.Equal(19, unresolved.Count);
        // Counted 2026-09-21 from the same files: 16 entries name the eight aliased (2 each), 11 name the leaf row
        // (the grand mage 4, the seven rats 1 each), 4 name the chest.
        Assert.Equal(31, referenced);
        Assert.DoesNotContain(ControlName, entries.Keys.Where(n => AssemblyHandler.FindTypeByName(n) != null), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheGrandMageIsAGeneratedSubclassOfOurMageAndDropsTheKeyOnEveryDeath()
    {
        ShardTestClock.Arm(); // PeerlessKey schedules its 10 s slice on construction

        var mage = new MageDragonsFlameMage();
        Assert.IsAssignableFrom<DragonsFlameMage>(mage);
        Assert.Equal("of the Dragon's Flame Sect", mage.Title);
        Assert.Equal(800, mage.HitsMax);
        Assert.True(mage.AlwaysMurderer);
        Assert.False(mage.ShowFameTitle);
        Assert.Equal(AIType.AI_Mage, mage.AI);
        Assert.Contains(mage.Items, i => i is Kasa && i.Hue == 0x51D); // the parent's outfit

        // Batch 4 §2's subclass rule, read off the generator: the child carries its own attribute and its own (Serial)
        // constructor; so does the parent.
        Assert.Single(typeof(MageDragonsFlameMage).GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
        Assert.Single(typeof(DragonsFlameMage).GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
        Assert.Same(typeof(MageDragonsFlameMage), typeof(MageDragonsFlameMage).GetConstructor(new[] { typeof(Serial) })!.DeclaringType);

        // ServUO OnDeath: the key every time, the badge half the time on top of the parent's 30%. Through a real kill
        // and a real corpse (the fixture calls Corpse.Initialize).
        var keys = 0;
        for (var i = 0; i < 10; i++)
        {
            var m = new MageDragonsFlameMage();
            m.MoveToWorld(new Point3D(139, 1868, 0), Map.Malas);
            m.Kill();

            var corpse = Assert.IsAssignableFrom<Corpse>(m.Corpse);
            var key = corpse.FindItemByType<DragonFlameKey>();
            Assert.NotNull(key);
            keys++;
            Assert.Equal(LootType.Blessed, key.LootType);
            Assert.Equal(42, key.Hue);
            Assert.Equal(0x2002, key.ItemID);
            Assert.Equal(1074343, key.LabelNumber);
            corpse.Delete();
        }

        Assert.Equal(10, keys);
        Assert.Single(typeof(DragonFlameKey).GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
        Assert.Same(typeof(DragonFlameKey), typeof(DragonFlameKey).GetConstructor(new[] { typeof(Serial) })!.DeclaringType);
        Assert.Same(typeof(DragonFlameKey), AssemblyHandler.FindTypeByName("DragonFlameKey"));

        mage.Delete();
    }

    [Fact]
    public void TheRatmenKeepTheirServUOShape()
    {
        foreach (var archer in new BaseCreature[] { new ClanChitterAssistant(), new ClanChitterTinkerer(), new ClanScratchScrounger() })
        {
            Assert.Equal(AIType.AI_Archer, archer.AI);
            Assert.Contains(archer.Items, i => i is Bow);
            var arrows = archer.Backpack?.FindItemByType<Arrow>();
            Assert.NotNull(arrows);
            Assert.InRange(arrows.Amount, 50, 70);
            Assert.True(archer.CanRummageCorpses);
            Assert.Equal(8, archer.Hides);
            Assert.Equal(HideType.Spined, archer.HideType);
            archer.Delete();
        }

        var tinkerer = new ClanChitterTinkerer();
        Assert.InRange(tinkerer.HitsMax, 2025, 2068);
        Assert.Equal("Clan Chitter Tinkerer", tinkerer.Name); // D-76, not ServUO's "Clan Scratch Tinkerer"

        var scrounger = new ClanScratchScrounger();
        Assert.Equal(2, scrounger.TreasureMapLevel);
        Assert.Equal(135, scrounger.HitsMax);
        // ServUO's: an AI_Archer holding a bow with no Archery skill set at all (ClanSS.cs sets Anatomy, MagicResist,
        // Tactics, Wrestling). Copied, bug-list §3's kind; the two Chitter archers have Archery 80.1-90.
        Assert.Equal(0.0, scrounger.Skills.Archery.Base);
        Assert.True(tinkerer.Skills.Archery.Base >= 80.1);

        foreach (var melee in new BaseCreature[] { new ClanRibbonCourtier(), new ClanRibbonSupplicant(), new ClanScratchHenchrat() })
        {
            Assert.Equal(AIType.AI_Melee, melee.AI);
            Assert.Equal(42, (int)melee.Body);
            Assert.True(melee.CanRummageCorpses);
            Assert.Equal(HideType.Spined, melee.HideType);
            Assert.DoesNotContain(melee.Items, i => i is BaseWeapon);
            melee.Delete();
        }

        var courtier = new ClanRibbonCourtier();
        Assert.InRange(courtier.HitsMax, 2054, 2100);
        Assert.True(courtier.Skills.MagicResist.Value >= 113.5);

        var supplicant = new ClanRibbonSupplicant();
        Assert.Equal(127, supplicant.HitsMax);
        Assert.InRange(supplicant.ColdResistance, 80, 85);

        var henchrat = new ClanScratchHenchrat();
        Assert.Equal(2065, henchrat.HitsMax);

        var wolf = new ClanScratchSavageWolf();
        Assert.Same(WeaponAbility.ParalyzingBlow, wolf.GetWeaponAbility());
        Assert.Equal(1, wolf.Meat);
        Assert.Equal(FoodType.Meat, wolf.FavoriteFood);
        Assert.Equal(PackInstinct.Canine, wolf.PackInstinct);
        Assert.Equal(20, wolf.PhysicalDamage);
        Assert.Equal(80, wolf.ColdDamage);
        Assert.Equal(229, wolf.BaseSoundID);
        Assert.True(wolf.Skills.Swords.Value >= 99.0);
        Assert.Equal(65, wolf.HitsMax);

        foreach (var m in new Mobile[] { tinkerer, scrounger, courtier, supplicant, henchrat, wolf })
        {
            m.Delete();
        }
    }

    [Fact]
    public void TheHybridChestCarriesItsServUOValues()
    {
        Assert.Same(typeof(TreasureLevel1h), AssemblyHandler.FindTypeByName("treasurelevel1h"));

        for (var i = 0; i < 20; i++)
        {
            var chest = new TreasureLevel1h();
            Assert.IsAssignableFrom<BaseTreasureChestMod>(chest);
            Assert.Contains(chest.ItemID, new[] { 0xE3C, 0xE3E, 0x9A9 });
            Assert.Equal(0x49, chest.DefaultGumpID);
            Assert.True(chest.Locked);
            Assert.False(chest.Movable);
            Assert.Equal(56, chest.RequiredSkill);
            Assert.Equal(56, chest.MaxLockLevel);
            Assert.InRange(chest.LockLevel, 46, 55);
            Assert.Equal(TrapType.MagicTrap, chest.TrapType);
            Assert.InRange(chest.TrapPower, 1, 25);

            var gold = chest.FindItemByType<Gold>();
            Assert.NotNull(gold);
            Assert.InRange(gold.Amount, 10, 40);
            Assert.Equal(5, chest.FindItemByType<Bolt>()?.Amount);

            var footwear = chest.Items.Single(it => it is Shoes or Sandals);
            Assert.InRange(footwear.Hue, 1, 2);
            Assert.Single(chest.Items, it => it is BeverageBottle or Jug);

            if (i == 0)
            {
                _out.WriteLine($"TreasureLevel1h: itemID=0x{chest.ItemID:X} lock={chest.LockLevel}/{chest.MaxLockLevel} " +
                               $"trap={chest.TrapPower} contents=[{string.Join(", ", chest.Items.Select(it => it.GetType().Name))}]");
            }

            chest.Delete();
        }
    }
}
