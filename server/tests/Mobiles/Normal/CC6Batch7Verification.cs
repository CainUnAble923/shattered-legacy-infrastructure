// CC6 batch 7: the eight remaining spawn-name aliases (Q-055, answered yes), the leaf-creature row and one leaf item.
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
using System.Linq;
using System.Reflection;
using Server;
using Server.Engines.MLQuests;
using Server.Engines.MLQuests.Definitions;
using Server.Engines.Spawners;
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
}
