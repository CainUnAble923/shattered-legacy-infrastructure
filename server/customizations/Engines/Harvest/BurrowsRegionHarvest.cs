// cc-P36 Part D.1 (cc-P24 3.1): region-only harvest. Inside the New Haven dungeon (OldHavenBurrowsRegion) a miner digs
// Dawnstone and a lumberjack cuts Rootwood, each with Iron or a regular log as the fallback; everywhere else nothing changes.
//
// The seam is HarvestSystem.MutateResource (pinned Engines/Harvest/Core/HarvestSystem.cs:332-352 at d4531cd9), virtual and
// overridden nowhere else in pinned or ours. Its return value is what HarvestSystem.FinishHarvesting's skill gate reads
// (:147, Skills[def.Skill].Base >= resource.ReqSkill, then CheckSkill(min, max)), so the region's resource carries its own
// 45 / 5 / 85 band. Inside the region the bank's own vein is ignored: the region stands in a vein of its own, shaped as
// stock's colored veins are (ChanceToFallback 0.5, the fallback iron or plain log; pinned Mining.cs:193-200,
// Lumberjacking.cs:80-88), and stock's MutateResource rolls it, so the fallback roll, the elf bonus and the skill test are
// pinned's own code. Bank depletion still uses the global banks (the world map haze reads them).
//
// Both classes are partial already (patches/Mining.cs; apply-patches.sh's sed on Lumberjacking.cs) and neither overrides
// MutateResource. No patch.

using Server.Items;
using Server.Regions;

namespace Server.Engines.Harvest;

public partial class Mining
{
    /// <summary>Dawnstone: required 45, window 5 to 85 (decisions file item 9). Ore and granite, as every colored ore (F1).</summary>
    public static readonly HarvestResource Dawnstone =
        new(45.0, 5.0, 85.0, "You dig some dawnstone ore and put it into your backpack.", typeof(DawnstoneOre), typeof(DawnstoneGranite));

    private HarvestVein _burrowsVein;

    /// <summary>The vein the Burrows stand in: Dawnstone, falling back to this system's iron (Resources[0]).</summary>
    public HarvestVein BurrowsVein => _burrowsVein ??= new HarvestVein(0, 0.5, Dawnstone, OreAndStone.Resources[0]);

    public override HarvestResource MutateResource(
        Mobile from, Item tool, HarvestDefinition def, Map map, Point3D loc,
        HarvestVein vein, HarvestResource primary, HarvestResource fallback
    )
    {
        if (def == OreAndStone && OldHavenBurrowsRegion.IsInside(map, loc))
        {
            var burrows = BurrowsVein;
            return base.MutateResource(from, tool, def, map, loc, burrows, burrows.PrimaryResource, burrows.FallbackResource);
        }

        return base.MutateResource(from, tool, def, map, loc, vein, primary, fallback);
    }
}

public partial class Lumberjacking
{
    /// <summary>Rootwood: required 45, window 5 to 85 (decisions file item 9).</summary>
    public static readonly HarvestResource Rootwood =
        new(45.0, 5.0, 85.0, "You put some rootwood logs into your backpack.", typeof(RootwoodLog));

    private HarvestVein _burrowsVein;

    /// <summary>The vein the Burrows stand in: Rootwood, falling back to this system's plain log (Resources[0]).</summary>
    public HarvestVein BurrowsVein => _burrowsVein ??= new HarvestVein(0, 0.5, Rootwood, Definitions[0].Resources[0]);

    public override HarvestResource MutateResource(
        Mobile from, Item tool, HarvestDefinition def, Map map, Point3D loc,
        HarvestVein vein, HarvestResource primary, HarvestResource fallback
    )
    {
        if (def == Definitions[0] && OldHavenBurrowsRegion.IsInside(map, loc))
        {
            var burrows = BurrowsVein;
            return base.MutateResource(from, tool, def, map, loc, burrows, burrows.PrimaryResource, burrows.FallbackResource);
        }

        return base.MutateResource(from, tool, def, map, loc, vein, primary, fallback);
    }
}
