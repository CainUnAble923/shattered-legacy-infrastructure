// cc-P629 (P565 design, stage 1): places the League investigation markers at the catalog's sites. Explicit and
// idempotent, with a dry run; nothing places them automatically. A site that fails LeagueInvestigationCatalog.CheckSite
// on the world it runs against gets no marker, and the reasons are reported. Moves and deletes nothing.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Engines.LeagueInvestigations;
using Server.Items;

namespace Server;

public static class LeagueInvestigationSeeder
{
    public static void Configure()
    {
        CommandSystem.Register("LeagueInvestigationSeed", AccessLevel.Administrator, OnSeedCommand);
    }

    [Usage("LeagueInvestigationSeed [dryrun]")]
    [Description("Places a League investigation marker at each investigation site that passes its checks and has none. Reports the sites that fail and why. Moves and deletes nothing.")]
    [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst, DryRun = "dryrun", Summary = "Places the League investigation markers at the checked sites where missing.")]
    private static void OnSeedCommand(CommandEventArgs e)
    {
        var dryRun = e.Length > 0 && e.GetString(0).Equals("dryrun", StringComparison.OrdinalIgnoreCase);
        var result = Seed(dryRun);
        e.Mobile.SendMessage(result.Message);
        foreach (var line in result.Refused)
        {
            e.Mobile.SendMessage(0x22, line);
        }
    }

    public sealed record SeedResult(int Placed, int Present, List<string> Refused, string Message);

    public static SeedResult Seed(bool dryRun)
    {
        int placed = 0, present = 0;
        var refused = new List<string>();

        foreach (var site in LeagueInvestigationCatalog.Sites)
        {
            if (FindMarker(site) != null)
            {
                present++;
                continue;
            }

            var problems = LeagueInvestigationCatalog.CheckSite(site);
            if (problems.Count > 0)
            {
                refused.AddRange(problems);
                continue;
            }

            if (!dryRun)
            {
                new LeagueInvestigationMarker(site.Key).MoveToWorld(site.Center, site.Map);
            }

            placed++;
        }

        var verb = dryRun ? "would place" : "placed";
        return new SeedResult(placed, present, refused,
            $"League investigations{(dryRun ? " (dry run)" : "")}: {verb} {placed} markers; already there {present}; refused {refused.Count} problems.");
    }

    /// <summary>The site's marker: one of ours with its key within 2 tiles of the centre, or null.</summary>
    public static LeagueInvestigationMarker? FindMarker(LeagueInvestigationSite site)
    {
        foreach (var item in site.Map.GetItemsInRange<LeagueInvestigationMarker>(site.Center, 2))
        {
            if (!item.Deleted && item.SiteKey == site.Key)
            {
                return item;
            }
        }

        return null;
    }
}
