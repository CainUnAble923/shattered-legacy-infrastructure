// ServUO: Services/Harvest/Mining.cs:116 (cc-P195): a small piece of blackrock, one dig in a thousand at 100 Mining,
// beside the six ML gems. The Craftsman path to the ritual needs blackrock (the Sacrificial Dagger and the Summoning
// Altar deed take ten each) and this is ServUO's one dig source, so the row is appended to pinned's ore bonus table
// at world load (ClusterFMiningExtension extends the same definition for the shard's ores). Pinned draws a bonus by
// walking the table with one roll over 100 (HarvestDefinition.cs:166-184), so the new 0.1 sits where ServUO's does.

using Server.Engines.Harvest;
using Server.Items;

namespace Server.Engines.Exodus;

public static class ExodusMiningBonus
{
    public const double Chance = 0.1;
    public const double ReqSkill = 100.0;
    public const int Message = 1077180; // You dig up a small piece of blackrock!

    public static void Configure()
    {
        EventSink.WorldLoad += () => Register();
    }

    /// <summary>Appends the blackrock row to the ore definition's bonus table. Idempotent; the test host calls it directly.</summary>
    public static bool Register()
    {
        var def = Mining.System?.OreAndStone;

        if (def == null)
        {
            return false;
        }

        return Apply(def);
    }

    public static bool Apply(HarvestDefinition def)
    {
        var table = def.BonusResources;

        if (table == null)
        {
            return false;
        }

        foreach (var row in table)
        {
            if (row.Type == typeof(SmallPieceofBlackrock))
            {
                return false;
            }
        }

        var extended = new BonusHarvestResource[table.Length + 1];
        table.CopyTo(extended, 0);
        extended[table.Length] = new BonusHarvestResource(ReqSkill, Chance, Message, typeof(SmallPieceofBlackrock));
        def.BonusResources = extended;

        return true;
    }
}
