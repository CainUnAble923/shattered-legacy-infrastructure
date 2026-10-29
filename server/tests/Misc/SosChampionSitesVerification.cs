// SosChampionSitesVerification.cs
//
// cc-P685 (SoS swap S12): Felucca's 17 champion spawns on the Siege of Sosaria land. server/patches/
// GenChamps-sos-sites.patch moves three Lost Lands altars (LL06, LL07, LL11) onto new ground and gives LL08 a new eject
// point (P696's proposal, checked by shard-migration notes/cc-P685-tools/champs.py); the other 13 are pinned's. The map
// side (each platform on its floor, the spawn area walkable and clear of guard zones and S14's closures, every eject
// point on a floor) is shard-migration tools/tests/test_sos_champions.py; this is the shard's side.
//
// A fact per altar: a real [GenChamps places exactly one ChampionSpawn of the site's class at its controller on
// Felucca, with its eject point (on Felucca), its boss type or random type, and nothing at a moved site's old spot.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Commands;
using Server.Engines.CannedEvil;
using Server.Mobiles;
using Xunit;

namespace ShatteredLegacy.Tests;

public sealed class SosChampionSitesFixture : IDisposable
{
    public List<ChampionSpawn> Spawns { get; }

    public SosChampionSitesFixture()
    {
        ChampionGenerator.Configure(); // Register is idempotent (Commands.cs DoRegister).
        var dev = new PlayerMobile { AccessLevel = AccessLevel.Developer };
        dev.MoveToWorld(new Point3D(1300, 1300, 0), Map.Trammel);
        try
        {
            Assert.True(CommandSystem.Handle(dev, $"{CommandSystem.Prefix}GenChamps"));
        }
        finally
        {
            dev.Delete();
        }

        Spawns = World.Items.Values.OfType<ChampionSpawn>().Where(s => !s.Deleted).ToList();
    }

    public void Dispose()
    {
        foreach (var s in Spawns)
        {
            s.Delete();
        }
    }
}

[Collection("Sequential UOContent Tests")]
public class SosChampionSitesVerification : IClassFixture<SosChampionSitesFixture>
{
    private readonly SosChampionSitesFixture _fx;

    public SosChampionSitesVerification(SosChampionSitesFixture fx) => _fx = fx;

    // id, Lost Lands (or dungeon), boss type (null: random), controller, eject, the stock controller when it moved
    public static readonly TheoryData<string, bool, string?, int[], int[], int[]?> Sites = new()
    {
        { "LL01", true, null, [5511, 2360, 42], [5439, 2323, 26], null },
        { "LL02", true, null, [6038, 2401, 47], [5988, 2340, 24], null },
        { "LL03", true, null, [5549, 2640, 16], [5645, 2696, -8], null },
        { "LL04", true, null, [5636, 2916, 37], [5721, 2949, 28], null },
        { "LL05", true, null, [6035, 2943, 50], [6098, 2997, 17], null },
        { "LL06", true, null, [5432, 3220, 19], [5423, 3183, -3], [5265, 3171, 105] },
        { "LL07", true, null, [5324, 3428, 15], [5296, 3428, -5], [5282, 3368, 50] },
        { "LL08", true, null, [5207, 3637, 20], [5209, 3609, 0], null },
        { "LL09", true, null, [5954, 3475, 25], [6013, 3529, 0], null },
        { "LL10", true, null, [5982, 3882, 20], [5929, 3820, -1], null },
        { "LL11", true, null, [5768, 4024, 20], [5768, 3996, 0], [5724, 3991, 41] },
        { "LL12", true, "ForestLord", [5559, 3757, 21], [5513, 3878, 3], null },
        { "D1", false, "UnholyTerror", [5179, 709, 20], [4111, 432, 5], null },
        { "D2", false, "VerminHorde", [5557, 827, 65], [5580, 632, 30], null },
        { "D3", false, "ColdBlood", [5259, 837, 64], [1176, 2637, 0], null },
        { "D4", false, "Abyss", [5815, 1352, 5], [2923, 3406, 8], null },
        { "D5", false, "Arachnid", [5190, 1607, 20], [5482, 3161, -54], null }
    };

    private static Point3D P(int[] a) => new(a[0], a[1], a[2]);

    [Theory]
    [MemberData(nameof(Sites))]
    public void TheAltarStandsWhereTheSiteSays(string id, bool lostLands, string? type, int[] at, int[] eject, int[]? oldAt)
    {
        Assert.Equal(17, _fx.Spawns.Count);
        var here = _fx.Spawns.Where(s => s.Map == Map.Felucca && s.Location == P(at)).ToList();
        var spawn = Assert.Single(here);
        Assert.Equal(lostLands ? typeof(LLChampionSpawn) : typeof(DungeonChampionSpawn), spawn.GetType());
        Assert.Equal(P(eject), spawn.EjectLocation);
        Assert.Same(Map.Felucca, spawn.EjectMap);
        if (type == null)
        {
            Assert.True(spawn.RandomizeType, id);
        }
        else
        {
            Assert.False(spawn.RandomizeType, id);
            Assert.Equal(Enum.Parse<ChampionSpawnType>(type), spawn.Type);
        }

        if (oldAt != null)
        {
            Assert.DoesNotContain(_fx.Spawns, s => s.Location == P(oldAt));
        }
    }
}
