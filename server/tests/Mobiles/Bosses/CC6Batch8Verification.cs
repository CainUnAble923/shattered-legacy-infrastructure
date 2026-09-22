// CC6 batch 8: the Stygian Abyss boss tier (Q-052 answered yes, 2026-09-22).
//
// Part A ports the creature half of B14 Instanced Peerless WITHOUT the altar: ServUO's BasePeerless (: BaseCreature)
// and BaseSABoss (abstract : BasePeerless), plus Putrefaction, the one of BasePeerless.PackResources' six ML resources
// pinned lacks. Part C ports Navrey Night-Eyes (a plain BaseCreature, named by the spawn data: Q-056 provisional),
// EyeOfNavrey, Tangle1 and UntranslatedAncientTome. Part B (SlasherOfVeils, StygianDragon and their leaves) and
// Part D (Medusa) follow in this file when they land.
//
// The fact that matters most here is not that the bosses construct. It is that BasePeerless serializes and
// deserializes correctly with no altar - that a boss saved and reloaded comes back whole - because the base's save
// format is set the first time it is written and no later migration can re-parent it (AGENTS.md, the final-parent
// rule). So the round trips below go through a real BufferWriter/BufferReader, the shape the armour-set tests use.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh (see batch 1's file for the host facts). The test host registers the six maps with
// no tile data, so spawners below keep SpawnBounds at default and Kill() corpses land on the creature's own tile.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Engines.Spawners;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CC6Batch8Verification
{
    private readonly ITestOutputHelper _out;

    public CC6Batch8Verification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D Here = new(1000, 1000, 0);

    private static PlayerMobile NewPlayer(Point3D loc, Map map = null)
    {
        // Mobile.Player is a flag CharacterCreation sets, not something the PlayerMobile constructor does (batch 3).
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(loc, map ?? Map.TerMur);
        pm.Hits = pm.HitsMax;
        return pm;
    }

    // Serialize `original` through the generated Serialize, read it back into a fresh instance built with the
    // generated (Serial) constructor, and hand the copy back. The armour-set tests' shape.
    private static T RoundTrip<T>(T original) where T : Mobile
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true, new ConcurrentQueue<Type>());
        original.Serialize(writer);
        writer.Flush();

        var copy = (T)Activator.CreateInstance(typeof(T), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    // ---- Part A: the base -----------------------------------------------------------------------------------------

    // ServUO BasePeerless.OnDeath, the GiveMLSpecial branch: 10% HumanFeyLeggings, 2.5% CrimsonCincture, 5% one of 32.
    private static readonly Type[] MLSpecial =
    {
        typeof(HumanFeyLeggings), typeof(CrimsonCincture),
        typeof(AssassinChest), typeof(AssassinArms), typeof(AssassinLegs), typeof(AssassinGloves),
        typeof(DeathChest), typeof(DeathArms), typeof(DeathLegs), typeof(DeathBoneHelm), typeof(DeathGloves),
        typeof(MyrmidonArms), typeof(MyrmidonLegs), typeof(MyrmidonGorget), typeof(MyrmidonChest),
        typeof(LeafweaveGloves), typeof(LeafweaveLegs), typeof(LeafweavePauldrons),
        typeof(PaladinGloves), typeof(PaladinGorget), typeof(PaladinArms), typeof(PaladinLegs), typeof(PaladinHelm), typeof(PaladinChest),
        typeof(HunterArms), typeof(HunterGloves), typeof(HunterLegs), typeof(HunterChest),
        typeof(GreymistArms), typeof(GreymistGloves), typeof(GreymistLegs),
        typeof(MalekisHonor), typeof(Feathernock), typeof(Swiftflight)
    };

    private static readonly Type[] MLResources =
    {
        typeof(Blight), typeof(Scourge), typeof(Taint), typeof(Putrefaction), typeof(Corruption), typeof(Muculent)
    };

    [Fact]
    public void TheTwoBasesHaveTheirFinalParentsTheirOwnGeneratorAndNoAltar()
    {
        // BasePeerless : BaseCreature, concrete (ServUO's is), with its own attribute and its own (Serial) constructor.
        Assert.Same(typeof(BaseCreature), typeof(BasePeerless).BaseType);
        Assert.False(typeof(BasePeerless).IsAbstract);
        Assert.Single(typeof(BasePeerless).GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
        Assert.Same(typeof(BasePeerless), typeof(BasePeerless).GetConstructor(new[] { typeof(Serial) })!.DeclaringType);

        // BaseSABoss : BasePeerless, abstract, same three things of its own (batch 4 section 2's subclass rule).
        Assert.Same(typeof(BasePeerless), typeof(BaseSABoss).BaseType);
        Assert.True(typeof(BaseSABoss).IsAbstract);
        Assert.Single(typeof(BaseSABoss).GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
        var serialCtor = typeof(BaseSABoss).GetConstructor(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Serial) }, null);
        Assert.NotNull(serialCtor);
        Assert.Same(typeof(BaseSABoss), serialCtor.DeclaringType);

        // No altar anywhere: no Altar member on the base, no PeerlessAltar type in any loaded assembly (by declaration or
        // alias), and neither base declares a serialized field - version 0 carries nothing (D-79).
        Assert.Null(typeof(BasePeerless).GetProperty("Altar"));
        Assert.Null(AssemblyHandler.FindTypeByName("PeerlessAltar"));
        foreach (var t in new[] { typeof(BasePeerless), typeof(BaseSABoss) })
        {
            var serialized = t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(f => f.GetCustomAttribute<SerializableFieldAttribute>() != null)
                .Select(f => f.Name)
                .ToList();
            _out.WriteLine($"{t.Name}: serialized fields = [{string.Join(", ", serialized)}]");
            Assert.Empty(serialized);
        }

        // The altar-less arms.
        var bp = new BasePeerless(AIType.AI_Melee);
        Assert.True(bp.AllHelpersDead);
        Assert.True(bp.Unprovokable);
        Assert.True(bp.GiveMLSpecial);
        Assert.Equal(Core.TOL, bp.DropPrimer);
        Assert.False(bp.HasFireRing);
        Assert.False(bp.CanSpawnHelpers);
        Assert.Equal(0, bp.CurrentWave);
        Assert.False(bp.CanSpawnWave());
        Assert.Equal(0.25, bp.ActiveSpeed); // Q-008
        bp.Delete();

        // The carrier check AGENTS.md asks for before a base is given its parent, run in the gate too: neither base
        // exposes a set-item member.
        foreach (var name in new[] { "SetID", "Pieces", "SetAttributes", "SetSkillBonuses", "AbsorptionAttributes", "LastEquipped" })
        {
            Assert.Null(typeof(BaseSABoss).GetProperty(name));
        }
    }

    [Fact]
    public void ABasePeerlessSurvivesASerializationRoundTripWithNoAltarAndWithServUOsUnsavedWaveCount()
    {
        var original = new BasePeerless(AIType.AI_Mage) { Name = "a peerless probe", Hue = 1175 };
        original.SetHits(1234);
        original.SetStr(321);
        original.Body = 400;
        original.CurrentWave = 3; // ServUO never serializes m_CurrentWave; a reloaded boss has 0 waves left
        original.MoveToWorld(Here, Map.TerMur);

        var copy = RoundTrip(original);

        _out.WriteLine($"round trip: name='{copy.Name}' hue={copy.Hue} body={(int)copy.Body} hitsMax={copy.HitsMax} " +
                       $"str={copy.RawStr} ai={copy.AI} at {copy.Location} on {copy.Map} wave={copy.CurrentWave}");

        Assert.Equal("a peerless probe", copy.Name);
        Assert.Equal(1175, copy.Hue);
        Assert.Equal(400, (int)copy.Body);
        Assert.Equal(1234, copy.HitsMax);
        Assert.Equal(321, copy.RawStr);
        Assert.Equal(AIType.AI_Mage, copy.AI);
        Assert.Equal(Here, copy.Location);
        Assert.Same(Map.TerMur, copy.Map);
        Assert.Equal(0, copy.CurrentWave);
        Assert.True(copy.AllHelpersDead);

        copy.Delete();
        original.Delete();
    }

    [Fact]
    public void BasePeerlessReachesEveryMLSpecialDropAndPacksTheSixResources()
    {
        // PackResources: six ML resources, one of which (Putrefaction) is ours. 60 draws, stacked in the backpack.
        var packer = new BasePeerless(AIType.AI_Melee);
        packer.PackResources(60);
        var stacks = packer.Backpack!.Items.ToList();
        Assert.Equal(60, stacks.Sum(i => i.Amount));
        Assert.All(stacks, i => Assert.Contains(i.GetType(), MLResources));
        Assert.Contains(stacks, i => i is Putrefaction);
        _out.WriteLine("PackResources(60): " + string.Join(", ", stacks.Select(i => $"{i.GetType().Name}x{i.Amount}")));

        packer.PackTalismans(3);
        Assert.InRange(packer.Backpack.Items.Count(i => i is BaseTalisman), 0, 2); // Utility.Random(3)
        packer.Delete();

        var putrefaction = new Putrefaction(5);
        Assert.Equal(0x3186, putrefaction.ItemID);
        Assert.Equal(883, putrefaction.Hue);
        Assert.True(putrefaction.Stackable);
        Assert.Equal(5, putrefaction.Amount);
        Assert.True(((ICommodity)putrefaction).IsDeedable);
        putrefaction.Delete();

        // The GiveMLSpecial branch through real kills: every drop is one of the 35 types, and the 10% leggings show up
        // in 100 kills (P(miss) = 0.9^100). The base has no GenerateLoot, so a corpse carries nothing else.
        var tally = new Dictionary<string, int>();
        for (var i = 0; i < 100; i++)
        {
            var bc = new BasePeerless(AIType.AI_Melee);
            bc.MoveToWorld(Here, Map.TerMur);
            bc.Kill();

            var corpse = Assert.IsAssignableFrom<Corpse>(bc.Corpse);
            foreach (var item in corpse.Items)
            {
                Assert.Contains(item.GetType(), MLSpecial);
                tally[item.GetType().Name] = tally.GetValueOrDefault(item.GetType().Name) + 1;
            }

            corpse.Delete();
        }

        _out.WriteLine("100 kills: " + string.Join(", ", tally.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}={kv.Value}")));
        Assert.True(tally.GetValueOrDefault("HumanFeyLeggings") > 0, "no HumanFeyLeggings in 100 kills");
    }

    // ---- Part C: Navrey Night-Eyes ---------------------------------------------------------------------------------

    [Fact]
    public void NavreyConstructsWithServUOValuesAndIsNamedByTheSpawnData()
    {
        var n = new NavreyNightEyes();

        _out.WriteLine($"NavreyNightEyes: name='{n.Name}' body={(int)n.Body} sound={n.BaseSoundID} hits={n.HitsMax} " +
                       $"fame={n.Fame} karma={n.Karma} ai={n.AI} speed={n.ActiveSpeed}/{n.PassiveSpeed} " +
                       $"scrolls=[{string.Join(", ", n.Backpack?.Items.Select(i => i.GetType().Name) ?? Array.Empty<string>())}]");

        Assert.Equal("Navrey Night-Eyes", n.Name);
        Assert.Equal("a navrey corpse", n.CorpseName);
        Assert.Equal(735, (int)n.Body);
        Assert.Equal(389, n.BaseSoundID);
        Assert.InRange(n.HitsMax, 30000, 35000);
        Assert.Equal(24000, n.Fame);
        Assert.Equal(-24000, n.Karma);
        Assert.Equal(90, n.VirtualArmor);
        Assert.Equal(AIType.AI_Mage, n.AI);
        Assert.True(n.AlwaysMurderer);
        Assert.Same(Poison.DeadlyParasitic, n.PoisonImmune);
        Assert.Same(Poison.Lethal, n.HitPoison);
        Assert.Equal(1, n.Meat);
        Assert.Equal(100, n.PoisonResistance);
        Assert.Equal(50, n.PhysicalDamage);
        Assert.Equal(0.25, n.ActiveSpeed); // Q-008
        Assert.False(n.UsedPillars);

        // 1-3 Mysticism scrolls, each one of the sixteen; ServUO's RandomScroll(0, Length) can index one past the array
        // and pack nothing, so the count is 0-3 (copied, section 5).
        var scrolls = n.Backpack?.Items.OfType<SpellScroll>().ToList() ?? new List<SpellScroll>();
        Assert.InRange(scrolls.Count, 0, 3);
        Assert.All(scrolls, s => Assert.Contains(s.GetType(), NavreyNightEyes.MysticismScrollTypes));
        Assert.Equal(16, NavreyNightEyes.MysticismScrollTypes.Length);

        // Q-056 provisional: the type takes the spawn-data spelling and no alias; ServUO's spelling resolves to nothing.
        Assert.Same(typeof(NavreyNightEyes), AssemblyHandler.FindTypeByName("NavreyNightEyes"));
        Assert.Same(typeof(NavreyNightEyes), AssemblyHandler.FindTypeByName("navreynighteyes"));
        Assert.Null(AssemblyHandler.FindTypeByName("Navrey"));
        Assert.Null(typeof(NavreyNightEyes).GetCustomAttribute<TypeAliasAttribute>(false));

        Assert.Single(typeof(NavreyNightEyes).GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
        Assert.Same(typeof(NavreyNightEyes), typeof(NavreyNightEyes).GetConstructor(new[] { typeof(Serial) })!.DeclaringType);

        // Her round trip: UsedPillars is a GM property ServUO never serialized, so it does not survive (copied).
        n.UsedPillars = true;
        n.MoveToWorld(Here, Map.TerMur);
        var copy = RoundTrip(n);
        Assert.Equal("Navrey Night-Eyes", copy.Name);
        Assert.Equal(735, (int)copy.Body);
        Assert.Equal(n.HitsMax, copy.HitsMax);
        Assert.False(copy.UsedPillars);
        copy.Delete();
        n.Delete();
    }

    [Fact]
    public void NavreysLeavesCarryTheirServUOValues()
    {
        var tangle = new Tangle1();
        Assert.IsAssignableFrom<HalfApron>(tangle);
        Assert.Equal(506, tangle.Hue);
        Assert.Equal(1114784, tangle.LabelNumber);
        Assert.Equal(10, tangle.Attributes.BonusInt);
        Assert.Equal(5, tangle.Attributes.DefendChance);
        Assert.Equal(2, tangle.Attributes.RegenMana);
        tangle.Delete();

        var eye = new EyeOfNavrey();
        Assert.Equal(0x318D, eye.ItemID);
        Assert.Equal(68, eye.Hue);
        Assert.Equal(LootType.Blessed, eye.LootType);
        Assert.Equal(1095154, eye.LabelNumber);
        Assert.Equal(1.0, eye.Weight);
        eye.Delete();

        var tome = new UntranslatedAncientTome(3);
        Assert.Equal(0x0FF2, tome.ItemID);
        Assert.Equal(2405, tome.Hue);
        Assert.True(tome.Stackable);
        Assert.Equal(3, tome.Amount);
        Assert.Equal(1112992, tome.LabelNumber);
        tome.Delete();

        foreach (var t in new[] { typeof(Tangle1), typeof(EyeOfNavrey), typeof(UntranslatedAncientTome), typeof(Putrefaction) })
        {
            Assert.Single(t.GetCustomAttributes(typeof(SerializationGeneratorAttribute), false));
            Assert.Same(t, t.GetConstructor(new[] { typeof(Serial) })!.DeclaringType);
            Assert.Same(t, AssemblyHandler.FindTypeByName(t.Name));
        }
    }

    [Fact]
    public void NavreyDropsHerLootThroughRealKillsAndNeverTheEye()
    {
        // ServUO OnDeath: tome 50%, transcendence scroll 10%, tattered scroll 10%, lucky coin 10%, an artifact 2.5% to
        // a random nearby player (none here). The Green With Envy eye needs S5's engine (D-84) and never appears.
        var tally = new Dictionary<string, int>();
        for (var i = 0; i < 40; i++)
        {
            var n = new NavreyNightEyes();
            n.MoveToWorld(Here, Map.TerMur);
            n.Kill();

            var corpse = Assert.IsAssignableFrom<Corpse>(n.Corpse);
            Assert.Null(corpse.FindItemByType<EyeOfNavrey>());

            foreach (var type in new[] { typeof(UntranslatedAncientTome), typeof(ScrollofTranscendence), typeof(TatteredAncientScroll), typeof(LuckyCoin) })
            {
                if (corpse.Items.Any(it => it.GetType() == type))
                {
                    tally[type.Name] = tally.GetValueOrDefault(type.Name) + 1;
                }
            }

            corpse.Delete();
        }

        _out.WriteLine("40 kills: " + string.Join(", ", tally.Select(kv => $"{kv.Key}={kv.Value}")));
        Assert.True(tally.GetValueOrDefault("UntranslatedAncientTome") > 0, "no tome in 40 kills (P = 0.5^40)");

        // The artifact path, driven directly: a player in the world gets it in the pack; nobody, and it is deleted.
        var pm = NewPlayer(Here);
        var tangle = new Tangle1();
        NavreyNightEyes.DistributeArtifact(pm, tangle);
        Assert.Same(pm.Backpack, tangle.Parent);
        tangle.Delete();

        var orphan = new NightEyes();
        NavreyNightEyes.DistributeArtifact(null, orphan);
        Assert.True(orphan.Deleted);
        pm.Delete();
    }

    [Fact]
    public void ASpawnerWhereTheDataPlacesNavreyFillsWithHer()
    {
        // post-uoml/termur/Underworld.json, "Spawner (603)" at [1053, 861, -32], one NavreyNightEyes.
        var spawner = new Spawner();
        spawner.MoveToWorld(new Point3D(1053, 861, -32), Map.TerMur);
        spawner.SpawnBounds = default;
        var entry = spawner.AddEntry("NavreyNightEyes", 100, 1, dotimer: false);

        try
        {
            Assert.True(spawner.Spawn(entry, out var flags), $"Spawn returned false with flags {flags}");
            Assert.Equal(EntryFlags.None, flags);
            var spawned = Assert.Single(spawner.Spawned.Keys);
            var navrey = Assert.IsType<NavreyNightEyes>(spawned);
            Assert.Same(Map.TerMur, navrey.Map);
            Assert.Equal("Navrey Night-Eyes", navrey.Name);
            _out.WriteLine($"spawner at {spawner.Location} filled with {navrey.GetType().Name} '{navrey.Name}'");
        }
        finally
        {
            spawner.Delete();
        }
    }

    // ---- The orphan count --------------------------------------------------------------------------------------

    // The spawn names this batch closes, exactly as the JSON carries them. Part C: NavreyNightEyes (1 entry).
    private static readonly string[] Closed = { "NavreyNightEyes" };

    [Fact]
    public void EveryStockSpawnEntryNamingThisBatchResolvesAndTheOrphanCountMovedInStep()
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
        foreach (var name in Closed)
        {
            Assert.True(entries.TryGetValue(name, out var count) && count > 0, $"{name} is not named by any stock spawn entry");
            Assert.DoesNotContain(name, unresolved, StringComparer.OrdinalIgnoreCase);
            referenced += count;
            _out.WriteLine($"{name}: {count} spawn entries");
        }

        // Batch 7 left exactly 23 names unresolved (tools/spawn-orphans.txt at 1595753). Part C's Navrey takes it to
        // 22; nothing else in Parts A and C adds a spawnable name (BasePeerless is named by no entry, BaseSABoss is
        // abstract, the four items are named by no entry).
        Assert.Equal(22, unresolved.Count);
        Assert.Equal(1, referenced);
    }
}
