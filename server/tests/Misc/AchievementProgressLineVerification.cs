// AchievementProgressLineVerification.cs
//
// cc-P61 Part B (bug-list D84, Chase screenshot 2026-10-05 09:41): "Oh Look, It's Dead" was Earned and still showed
// "Progress: 9/1". An achievement's counter keeps counting after it is earned, and the row printed it. An earned row now
// has no progress line (its Earned line says what was done); a row not yet earned keeps it.
// Notes: shard-migration notes/cc-P61-batch-7.md, Part B.
//
// Facts:
//   B1. Every achievement with a counter (every trigger kind that has one), earned and its counter at nine times its
//       goal: its row in the [achievements gump has its Earned line and no "Progress:" line.
//   B2. The same achievements not yet earned, counter at a third of the goal: each visible row shows
//       "Progress: n/goal" with the counter's own number.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class AchievementProgressLineVerification
{
    private readonly ITestOutputHelper _out;

    public AchievementProgressLineVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }
    }

    private static readonly MethodInfo SetCounter =
        typeof(ClusterFAchievementSystem).GetMethod("SetCounter", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static List<AchievementDef> Counted() =>
        ClusterFAchievementSystem.Definitions.Values
            .Where(d => d.ProgressCounter != null && d.ProgressThreshold > 0 && !d.Retired)
            .OrderBy(d => d.Key)
            .ToList();

    private static string Plain(string html) => Regex.Replace(html.Replace("<BR>", "\n"), "<[^>]+>", "");

    /// <summary>Every row's text on every page of every tab (the League tab is not under All).</summary>
    private static List<string> AllRows(PlayerMobile pm)
    {
        var rows = new List<string>();
        foreach (var cat in AchievementsGump.CategoryTabs.Select(t => t.Cat).Append(AchievementCategory.League))
        {
            rows.AddRange(TabRows(pm, cat));
        }

        return rows;
    }

    private static List<string> TabRows(PlayerMobile pm, AchievementCategory? cat)
    {
        var rows = new List<string>();
        for (var i = 0; i < 60; i++)
        {
            var g = new AchievementsGump(pm, cat, false, i);
            rows.AddRange(g.Entries.OfType<GumpHtml>().Select(h => Plain(h.Text)));
            var counter = g.Entries.OfType<GumpLabel>().Select(l => Regex.Match(l.Text, @"^(\d+)/(\d+)$"))
                .FirstOrDefault(m => m.Success);
            if (counter == null || counter.Groups[1].Value == counter.Groups[2].Value)
            {
                break;
            }
        }

        return rows;
    }

    // A row's first line is its title, then " [Secret]" or " [AP / R]".
    private static string RowOf(List<string> rows, AchievementDef def) =>
        rows.FirstOrDefault(r => r.Split('\n')[0].StartsWith(def.Title + " [", StringComparison.Ordinal));

    private static (Account, PlayerMobile) Player()
    {
        var account = new Account($"p61b{Guid.NewGuid():N}"[..16], "p61-test-only");
        var pm = new PlayerMobile { Player = true, Name = "Progress Fixture" };
        pm.AddItem(new Backpack());
        account[0] = pm;
        pm.MoveToWorld(new Point3D(1480, 1760, 0), Map.Trammel);
        return (account, pm);
    }

    private static void Done(Account account, PlayerMobile pm)
    {
        ClusterFAccountPersistence.Get(account)?.ClearGuildData();
        pm.Delete();
        Accounts.Remove(account);
    }

    // ---------------------------------------------------------------- B1

    [Fact]
    public void AnEarnedAchievementShowsNoProgressLine()
    {
        var (account, pm) = Player();
        try
        {
            var counted = Counted();
            // Earn them, prerequisites first (a few passes settle every chain).
            for (var pass = 0; pass < 6; pass++)
            {
                foreach (var def in counted)
                {
                    if (def.PrerequisiteKey != null)
                    {
                        ClusterFAchievementSystem.TryGrant(account, def.PrerequisiteKey);
                    }

                    ClusterFAchievementSystem.TryGrant(account, def.Key);
                }
            }

            foreach (var def in counted)
            {
                Assert.True(ClusterFAchievementSystem.HasEarned(account, def.Key), $"{def.Key} could not be earned");
                SetCounter.Invoke(null, [account.Username, def.ProgressCounter, def.ProgressThreshold * 9]);
            }

            var rows = AllRows(pm);
            var kinds = new HashSet<TriggerKind>();
            foreach (var def in counted)
            {
                var row = RowOf(rows, def);
                Assert.True(row != null, $"{def.Title} has no row");
                Assert.Contains(def.EarnedLine, row);
                Assert.DoesNotContain("Progress:", row);
                kinds.Add(def.Trigger.Kind);
            }

            var dead = counted.Single(d => d.Title == "Oh Look, It's Dead");
            _out.WriteLine($"{counted.Count} counted achievements ({kinds.Count} trigger kinds: {string.Join(", ", kinds)}), earned, no progress line");
            _out.WriteLine($"\"{dead.Title}\" at {ClusterFAchievementSystem.GetCounter(account.Username, dead.ProgressCounter)}/{dead.ProgressThreshold}: {RowOf(rows, dead).Replace("\n", " | ")}");
        }
        finally
        {
            Done(account, pm);
        }
    }

    // ---------------------------------------------------------------- B2

    [Fact]
    public void ANotYetEarnedAchievementStillShowsItsProgress()
    {
        var (account, pm) = Player();
        try
        {
            var counted = Counted();
            foreach (var def in counted)
            {
                SetCounter.Invoke(null, [account.Username, def.ProgressCounter, def.ProgressThreshold / 3]);
            }

            var rows = AllRows(pm);
            var shown = 0;
            foreach (var def in counted.Where(d => !d.Hidden))
            {
                var row = RowOf(rows, def);
                Assert.True(row != null, $"{def.Title} has no row");
                var n = ClusterFAchievementSystem.GetCounter(account.Username, def.ProgressCounter);
                Assert.Contains($"Progress: {n:N0}/{def.ProgressThreshold:N0}", row);
                shown++;
            }

            _out.WriteLine($"{shown} visible counted achievements not yet earned show their progress");
            Assert.True(shown > 0);
        }
        finally
        {
            Done(account, pm);
        }
    }
}
