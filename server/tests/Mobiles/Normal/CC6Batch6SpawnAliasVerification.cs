// CC6 batch 6: the name-mapping spike (Q-055). Nothing is ported here.
//
// Pinned ModernUO's own spawn data (Distribution/Data/Spawns, a converted Nerun's Distro, commit 0d1fffd9f) names
// types under spellings no assembly declares. Seven Citadel entries ask for "eliteninjawarrior"; the stock type is
// EliteNinja (Mobiles/Monsters/SE/EliteNinja.cs), and ServUO's own Citadel XML (Spawns/malas.xml) names EliteNinja at
// the same rooms. Nothing we add to server/customizations changes what the JSON asks for, so the only additive route
// is the one AssemblyHandler already provides: TypeCache registers every [TypeAlias] string in the same lookup that
// FindTypeByName reads (AssemblyHandler.cs:251-263), and every stock creature is a partial class (the serialization
// generator requires it), so a second part carrying the attribute merges at compile time with no patch.
//
// server/customizations/Mobiles/Aliases/EliteNinjaSpawnAlias.cs is that second part, for exactly one name. This
// file proves what the spike was built to prove - the name resolves, and a spawner entry carrying it fills - and
// pins a negative control beside it, so the mechanism is shown to discriminate rather than merely to pass. Whether
// the route is rolled out across the row is Q-055, Chase's decision; notes/cc6-batch6-name-mapping.md has both sides.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by docker/uo/apply-patches.sh and run
// as a gate by docker/uo/build.sh (see batch 1's file for the host facts). The test host registers the six maps with
// no tile data (Server.Tests/Fixtures/TestMapDefinitions.cs), so the spawner below keeps SpawnBounds at default:
// GetSpawnPosition then returns the spawner's own location without touching the map (BaseSpawner.cs:571-577).

using System;
using System.Reflection;
using Server;
using Server.Engines.Spawners;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class CC6Batch6SpawnAliasVerification
{
    private readonly ITestOutputHelper _out;

    public CC6Batch6SpawnAliasVerification(ITestOutputHelper output) => _out = output;

    // The Citadel's spelling, exactly as shared/malas/Citadel.json carries it (7 entries, 2026-09-21).
    private const string CitadelName = "eliteninjawarrior";

    // A Citadel name the spike does NOT alias (4 entries in the same file). ServUO's DragonsFlameGrandMage, which
    // pinned lacks; it stays an orphan until it is ported, and this file must keep saying so.
    private const string ControlName = "magedragonsflamemage";

    // The first Citadel spawner that names it: shared/malas/Citadel.json, [82, 1875, 0], Malas, maxCount 4.
    private static readonly Point3D CitadelRoom = new(82, 1875, 0);

    [Fact]
    public void TheCitadelNameResolvesToTheStockEliteNinjaThroughTheAlias()
    {
        // The alias sits on the stock type itself: one attribute, one alias, and the attribute is read with
        // inherit: false exactly as TypeCache reads it (AssemblyHandler.cs:255).
        var alias = typeof(EliteNinja).GetCustomAttribute<TypeAliasAttribute>(false);
        Assert.NotNull(alias);
        Assert.Equal(new[] { "Server.Mobiles.EliteNinjaWarrior" }, alias.Aliases);

        // The spawner path: BaseSpawner.Spawn(entry) calls FindTypeByName(entry.SpawnedName) with the defaults
        // (fullName: false, ignoreCase: true), which is what the JSON's lower-case spelling depends on.
        Assert.Same(typeof(EliteNinja), AssemblyHandler.FindTypeByName(CitadelName));
        Assert.Same(typeof(EliteNinja), AssemblyHandler.FindTypeByName("EliteNinjaWarrior"));
        Assert.Same(typeof(EliteNinja), AssemblyHandler.FindTypeByName("Server.Mobiles.EliteNinjaWarrior", fullName: true));

        // The stock name is untouched, and the type's own FullName - the string a save writes - is still its own.
        Assert.Same(typeof(EliteNinja), AssemblyHandler.FindTypeByName("EliteNinja"));
        Assert.Equal("Server.Mobiles.EliteNinja", typeof(EliteNinja).FullName);

        _out.WriteLine($"{CitadelName} -> {AssemblyHandler.FindTypeByName(CitadelName)?.FullName}");
    }

    [Fact]
    public void ASpawnerEntryCarryingTheCitadelNameFills()
    {
        var spawner = new Spawner();
        spawner.MoveToWorld(CitadelRoom, Map.Malas);
        spawner.SpawnBounds = default; // no tile data in the host: spawn at the spawner's own location
        var entry = spawner.AddEntry(CitadelName, 100, 4, dotimer: false);

        try
        {
            Assert.True(spawner.Spawn(entry, out var flags), $"Spawn returned false with flags {flags}");
            Assert.Equal(EntryFlags.None, flags);

            var spawned = Assert.Single(spawner.Spawned.Keys);
            var ninja = Assert.IsType<EliteNinja>(spawned);
            Assert.Same(Map.Malas, ninja.Map);
            Assert.Equal(CitadelRoom, ninja.Location);
            Assert.Same(entry, spawner.Spawned[ninja]);
            Assert.Contains(ninja, entry.Spawned);

            _out.WriteLine($"spawner at {CitadelRoom} on Malas filled with {ninja.GetType().Name} '{ninja.Name}' " +
                           $"body={(int)ninja.Body} hits={ninja.HitsMax}");
        }
        finally
        {
            spawner.Delete(); // BaseSpawner.OnDelete removes what it spawned
        }
    }

    [Fact]
    public void ANameWithNoAliasStillFlagsInvalidType()
    {
        // The negative control. If this ever passes for the wrong reason - the name ported, or aliased - the
        // batch-1 orphan count in CC6Batch1CreatureVerification moves too, and both files say so.
        Assert.Null(AssemblyHandler.FindTypeByName(ControlName));

        var spawner = new Spawner();
        spawner.MoveToWorld(CitadelRoom, Map.Malas);
        spawner.SpawnBounds = default;
        var entry = spawner.AddEntry(ControlName, 100, 2, dotimer: false);

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
