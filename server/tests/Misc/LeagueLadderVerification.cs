// LeagueLadderVerification.cs
//
// cc-P48 Parts B, D and E (F-24, League batch 1): the 17-rank metal ladder, League rank per character, lifetime Renown,
// promotion and its four checks, the placeholder table, and the bank bonus at milestone ranks. Notes in shard-migration
// notes/cc-P48-league-batch-1.md. The save format half of Part B is in AccountDataSerializerVerification (13-15).
//
// Facts (B = ladder and storage, D = promotion, E = milestone bonuses):
//   B1. The generated ladder has 17 ranks, Iron Citizen first and Celestial Citizen last, in metal order, each hued with
//       its metal's hue.
//   B2. A Dawnstone metal after Celestial adds no rank (LadderExcluded); a metal not excluded would add one.
//   B3. Two characters on one account hold different ranks; registering on either makes both Iron.
//   B4. An account saved before the bump (version 15) with "league.joined" loads with its characters at Iron, and one
//       without it at 0.
//   B5. Spending Renown leaves lifetime Renown unchanged; achievements and League jobs feed it; Commission Renown never.
//   D1. Each of the four checks blocks promotion on its own; all four passing promotes by exactly one rank.
//   D2. Promote re-checks: pressing a stale page's Promote twice promotes once, and a check that stopped passing after
//       the page was drawn blocks it.
//   D3. Waivers pass as "not yet required"; clearing one chapter's waiver blocks that rank until TrialsDone holds the
//       chapter, and leaves the other chapters waived; a character already past the rank is never asked for it.
//   D4. Rank never drops on a Renown spend or on the reset's loop parts (guilds, exploration, Renown).
//   D5. The table: the defaults written as JSON read back the same; a file with a bad row is refused whole, every error
//       is reported and the table in force is kept; a good edited file is put in force.
//   D6. The Registrar shows the rank in its status line, and the Rank page shows the four checks and Promote.
//   E1. Bank limit at 0, 1 and 6 milestones; a promotion that is not a milestone leaves the bank as it was; walking Iron
//       to Celestial takes 16 promotions and the bank steps at exactly six of them.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class LeagueLadderVerification
{
    private readonly ITestOutputHelper _out;

    public LeagueLadderVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();

        // The server's Configure defines the achievements (the test host calls no Configure). Once per process.
        if (!_achievements)
        {
            ClusterFAchievementSystem.Configure();
            _achievements = true;
        }
    }

    private static bool _achievements;

    // ---------------------------------------------------------------- helpers

    // Puts the League settings and table back as the server starts them, whatever a fact changed.
    private sealed class DefaultSettings : IDisposable
    {
        public DefaultSettings() => Reset();

        public void Dispose() => Reset();

        private static void Reset()
        {
            LeagueLadderTable.Current = LeagueLadderTable.Defaults;
            ClusterFLeagueRanks.PromotionJobsWaived = true;
            for (var ch = 1; ch <= ClusterFLeagueRanks.ChapterCount; ch++)
            {
                ClusterFLeagueRanks.SetTrialWaived(ch, true);
            }
        }
    }

    private static readonly string[] GuildKeys = ["warriors", "healers", "tinkers", "bards", "merchants"];

    private static Account NewAccount() => new($"p48l{Guid.NewGuid():N}"[..16], "p48-test-only");

    private static PlayerMobile NewCharacter(Account account, int slot)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        account[slot] = pm;
        pm.MoveToWorld(new Point3D(3460, 2604, 18), Map.Trammel);
        return pm;
    }

    private static NetState Online(PlayerMobile pm)
    {
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        ns.Account = pm.Account; // a connection with no account has a 4 KiB send ring (cc-P30)
        return ns;
    }

    private static void Cleanup(Account account)
    {
        for (var i = 0; i < account.Length; i++)
        {
            if (account[i] is PlayerMobile pm)
            {
                if (pm.NetState != null)
                {
                    var ns = pm.NetState;
                    pm.NetState = null;
                    ns.Mobile = null;
                    ns.Dispose();
                }

                pm.Delete();
            }
        }

        Accounts.Remove(account);
    }

    private static ClusterFAccountData Data(Account account) => ClusterFAccountPersistence.GetOrCreate(account);

    private static void Register(PlayerMobile pm) => Data((Account)pm.Account).SetFlag(ClusterFLeagueSystem.FlagJoined);

    // This character joined to `count` guilds at the given standing, Apprentice task done.
    private static void GiveGuilds(PlayerMobile pm, int count, int standing = 0)
    {
        var g = ClusterFAccountPersistence.GetOrCreateGuild(pm);
        for (var i = 0; i < count; i++)
        {
            g.JoinedGuilds.Add(GuildKeys[i]);
            g.ApprenticeGuilds.Add(GuildKeys[i]);
            g.GuildReputation[GuildKeys[i]] = standing;
        }
    }

    // Everything Dull Copper (rank 2) asks for, on a registered Iron character.
    private static PlayerMobile ReadyForDullCopper(Account account, int slot = 0)
    {
        var pm = NewCharacter(account, slot);
        Register(pm);
        Data(account).LifetimeRenown = LeagueLadderTable.Current.For(2).Renown;
        GiveGuilds(pm, 1);
        return pm;
    }

    private static void Press(BaseGump gump, NetState ns, int buttonId) =>
        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));

    private static string Blocking(LeaguePromotionReport r) =>
        string.Join(",", r.Checks.Where(c => c.Blocks).Select(c => c.Label));

    // ---------------------------------------------------------------- B1

    [Fact]
    public void TheGeneratedLadderHas17RanksIronFirstCelestialLast()
    {
        var ladder = ClusterFLeagueRanks.Ladder;
        foreach (var r in ladder)
        {
            _out.WriteLine($"{r.Index,2} {r.Name,-22} hue 0x{r.Hue:X4} {(LeagueLadderTable.Defaults.For(r.Index).IsMilestone ? $"milestone, chapter {LeagueLadderTable.Defaults.For(r.Index).Chapter}" : "")}");
        }

        Assert.Equal(17, ladder.Count);
        Assert.Equal("Iron Citizen", ladder[0].Name);
        Assert.Equal("Celestial Citizen", ladder[^1].Name);
        Assert.Equal(17, ClusterFLeagueRanks.TopRank);

        // In metal order, every one a metal, hued and named from the resource table.
        var metals = Enum.GetValues<CraftResource>()
            .Where(r => CraftResources.GetType(r) == CraftResourceType.Metal && !ClusterFLeagueRanks.LadderExcluded.Contains(r.ToString()))
            .ToList();
        Assert.Equal(metals.Count, ladder.Count);
        for (var i = 0; i < ladder.Count; i++)
        {
            Assert.Equal(i + 1, ladder[i].Index);
            Assert.Equal(metals[i].ToString(), ladder[i].MetalKey);
            Assert.Equal(CraftResources.GetHue(metals[i]), ladder[i].Hue);
            Assert.Equal($"{CraftResources.GetName(metals[i])} Citizen", ladder[i].Name);
        }

        // The six milestones by default, by the metal names Chase gave.
        var milestones = ladder.Where(r => LeagueLadderTable.Defaults.For(r.Index).IsMilestone).Select(r => r.MetalKey).ToArray();
        Assert.Equal(["DullCopper", "Bronze", "Verite", "Blaze", "Mythril", "Celestial"], milestones);
        Assert.Equal([1, 2, 3, 4, 5, 6],
            ladder.Where(r => LeagueLadderTable.Defaults.For(r.Index).IsMilestone).Select(r => LeagueLadderTable.Defaults.For(r.Index).Chapter));
    }

    // ---------------------------------------------------------------- B2

    [Fact]
    public void ADawnstoneMetalAddsNoRankAndAMetalNotExcludedWould()
    {
        var metals = ClusterFLeagueRanks.MetalsFromEnum();
        var withDawnstone = new List<ClusterFLeagueRanks.Metal>(metals.Where(m => m.Key != "Dawnstone"))
        {
            new("Dawnstone", "Dawnstone", 0x0A3D),
        };

        var ladder = ClusterFLeagueRanks.Generate(withDawnstone, ClusterFLeagueRanks.LadderExcluded);
        _out.WriteLine($"metals with Dawnstone: {withDawnstone.Count}; ranks: {ladder.Count}; top {ladder[^1].Name}");
        Assert.Equal(17, ladder.Count);
        Assert.Equal("Celestial Citizen", ladder[^1].Name);
        Assert.DoesNotContain(ladder, r => r.MetalKey == "Dawnstone");

        // The rule is the exclusion set, not a fixed count: a new metal that is not excluded adds a rank at the top.
        var withNew = new List<ClusterFLeagueRanks.Metal>(withDawnstone) { new("Starmetal", "Starmetal", 0x0481) };
        var longer = ClusterFLeagueRanks.Generate(withNew, ClusterFLeagueRanks.LadderExcluded);
        Assert.Equal(18, longer.Count);
        Assert.Equal("Starmetal Citizen", longer[^1].Name);

        // A row-less new rank asks at least what the rank below asks, and has no trial.
        Assert.Contains("Dawnstone", ClusterFLeagueRanks.LadderExcluded);
    }

    // ---------------------------------------------------------------- B3

    [Fact]
    public void TwoCharactersOnOneAccountHoldDifferentRanks()
    {
        using var settings = new DefaultSettings();
        var account = NewAccount();
        var first = NewCharacter(account, 0);
        var second = NewCharacter(account, 1);

        try
        {
            Assert.Equal(0, ClusterFLeagueRanks.GetRank(first));
            Assert.Equal(0, ClusterFLeagueRanks.GetRank(second));

            // Registering on the second character makes both Iron: registration is per account.
            ClusterFLeagueSystem.JoinLeague(second);
            Assert.Equal(1, ClusterFLeagueRanks.GetRank(first));
            Assert.Equal(1, ClusterFLeagueRanks.GetRank(second));

            Data(account).LifetimeRenown = LeagueLadderTable.Current.For(3).Renown;
            GiveGuilds(first, 1);
            Assert.True(ClusterFLeagueRanks.TryPromote(first, 1, out var m1), m1);
            Assert.True(ClusterFLeagueRanks.TryPromote(first, 2, out var m2), m2);

            _out.WriteLine($"first {ClusterFLeagueRanks.GetRank(first)}, second {ClusterFLeagueRanks.GetRank(second)}");
            Assert.Equal(3, ClusterFLeagueRanks.GetRank(first));
            Assert.Equal(1, ClusterFLeagueRanks.GetRank(second));
            Assert.Equal("Shadow Iron Citizen", first.LeagueRankTitle);
            Assert.Equal("Iron Citizen", second.LeagueRankTitle);

            // The second has no guild rank of its own: the account's Renown alone does not promote it.
            Assert.False(ClusterFLeagueRanks.TryPromote(second, 1, out var m3));
            _out.WriteLine(m3);
            Assert.Equal(1, ClusterFLeagueRanks.GetRank(second));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- B4

    [Fact]
    public void AnAccountSavedBeforeTheBumpLoadsWithItsCharactersAtIron()
    {
        var joined = NewAccount();
        var unjoined = NewAccount();
        var a = NewCharacter(joined, 0);
        var b = NewCharacter(joined, 1);
        var c = NewCharacter(unjoined, 0);

        var field = typeof(ClusterFAccountPersistence).GetField("_data", BindingFlags.NonPublic | BindingFlags.Static)!;
        var all = (Dictionary<string, ClusterFAccountData>)field.GetValue(null)!;

        try
        {
            // Version 15 bytes from the frozen pre-cc-P48 writer.
            var before = new ClusterFAccountData { Renown = 777 };
            before.SetFlag(ClusterFLeagueSystem.FlagJoined);
            var writer = new BufferWriter(true);
            AccountDataSerializerVerification.SerializeVersion15(before, writer);
            Assert.Equal(15, BitConverter.ToInt32(writer.Buffer, 0));

            var reader = new BufferReader(writer.Buffer);
            all[joined.Username] = new ClusterFAccountData(reader);
            Assert.Equal(writer.Position, reader.Position);

            var plain = new BufferWriter(true);
            AccountDataSerializerVerification.SerializeVersion15(new ClusterFAccountData { Renown = 5 }, plain);
            all[unjoined.Username] = new ClusterFAccountData(new BufferReader(plain.Buffer));

            _out.WriteLine($"joined: {ClusterFLeagueRanks.GetRank(a)}, {ClusterFLeagueRanks.GetRank(b)}; unjoined {ClusterFLeagueRanks.GetRank(c)}");
            Assert.Equal(1, ClusterFLeagueRanks.GetRank(a));
            Assert.Equal(1, ClusterFLeagueRanks.GetRank(b));
            Assert.Equal(0, ClusterFLeagueRanks.GetRank(c));
            Assert.Equal(777, Data(joined).LifetimeRenown);
            Assert.Equal(0, Data(joined).LeagueDataCount);
        }
        finally
        {
            all.Remove(joined.Username);
            all.Remove(unjoined.Username);
            Cleanup(joined);
            Cleanup(unjoined);
        }
    }

    // ---------------------------------------------------------------- B5

    [Fact]
    public void SpendingRenownLeavesLifetimeRenownUnchanged()
    {
        var account = NewAccount();
        var pm = NewCharacter(account, 0);

        try
        {
            var data = Data(account);
            Assert.Equal(0, data.LifetimeRenown);

            // The one earning path today: an achievement (registering grants league.registered_citizen).
            ClusterFLeagueSystem.JoinLeague(pm);
            _out.WriteLine($"after registering: Renown {data.Renown}, lifetime {data.LifetimeRenown}");
            Assert.True(data.Renown > 0, "registering paid no Renown");
            Assert.Equal(data.Renown, data.LifetimeRenown);

            var lifetime = data.LifetimeRenown;
            Assert.True(data.SpendRenown(data.Renown));
            Assert.Equal(0, data.Renown);
            Assert.Equal(lifetime, data.LifetimeRenown);
            Assert.False(data.SpendRenown(1));
            Assert.Equal(lifetime, data.LifetimeRenown);

            // Batch 2's seam counts; batch 4's never does.
            data.GrantRenown(50, RenownSource.LeagueJob);
            Assert.Equal(lifetime + 50, data.LifetimeRenown);
            data.GrantRenown(1_000, RenownSource.Commission);
            Assert.Equal(1_050, data.Renown);
            Assert.Equal(lifetime + 50, data.LifetimeRenown);
            Assert.False(ClusterFAccountData.CountsTowardLeagueRank(RenownSource.Commission));

            // Nothing negative sneaks in.
            data.GrantRenown(-10, RenownSource.Achievement);
            Assert.Equal(lifetime + 50, data.LifetimeRenown);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- D1

    [Fact]
    public void EachCheckBlocksAloneAndAllFourPromoteByExactlyOne()
    {
        using var settings = new DefaultSettings();
        ClusterFLeagueRanks.PromotionJobsWaived = false;
        ClusterFLeagueRanks.SetTrialWaived(1, false);

        var account = NewAccount();
        var pm = ReadyForDullCopper(account);

        try
        {
            var league = Data(account).GetOrCreateLeagueData(pm.Serial);
            var guild = ClusterFAccountPersistence.GetOrCreateGuild(pm);
            league.PromotionDone.Add(2);
            league.TrialsDone.Add(1);

            // Enough for several ranks at once, to show promotion still moves only one.
            Data(account).LifetimeRenown = LeagueLadderTable.Current.For(4).Renown;
            league.PromotionDone.Add(3);

            var all = ClusterFLeagueRanks.Evaluate(pm);
            Assert.True(all.CanPromote, Blocking(all));

            void BlocksAlone(string label, Action breakIt, Action mendIt)
            {
                breakIt();
                var r = ClusterFLeagueRanks.Evaluate(pm);
                _out.WriteLine($"{label}: blocking [{Blocking(r)}]");
                Assert.False(r.CanPromote);
                Assert.Equal(label, Blocking(r));
                Assert.False(ClusterFLeagueRanks.TryPromote(pm, 1, out _));
                Assert.Equal(1, ClusterFLeagueRanks.GetRank(pm));
                mendIt();
                Assert.True(ClusterFLeagueRanks.Evaluate(pm).CanPromote);
            }

            var renown = Data(account).LifetimeRenown;
            BlocksAlone("Lifetime Renown",
                () => Data(account).LifetimeRenown = LeagueLadderTable.Current.For(2).Renown - 1,
                () => Data(account).LifetimeRenown = renown);
            BlocksAlone("Guild ranks",
                () => guild.ApprenticeGuilds.Clear(),
                () => guild.ApprenticeGuilds.Add(GuildKeys[0]));
            BlocksAlone("Promotion job",
                () => league.PromotionDone.Remove(2),
                () => league.PromotionDone.Add(2));
            BlocksAlone("Story trial, chapter 1",
                () => league.TrialsDone.Remove(1),
                () => league.TrialsDone.Add(1));

            // A guild rank held but not as a member does not count.
            guild.JoinedGuilds.Clear();
            Assert.Equal("Guild ranks", Blocking(ClusterFLeagueRanks.Evaluate(pm)));
            guild.JoinedGuilds.Add(GuildKeys[0]);

            Assert.True(ClusterFLeagueRanks.TryPromote(pm, 1, out var message), message);
            _out.WriteLine(message);
            Assert.Equal(2, ClusterFLeagueRanks.GetRank(pm));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- D2

    [Fact]
    public void PromoteChecksAgainSoAStalePageCannotPromote()
    {
        using var settings = new DefaultSettings();
        var account = NewAccount();
        var pm = ReadyForDullCopper(account);
        var ns = Online(pm);

        try
        {
            // Enough Renown and guild rank for Shadow Iron too: a second press must still not promote twice.
            Data(account).LifetimeRenown = LeagueLadderTable.Current.For(3).Renown;

            var page = new LeagueRegistrarGump(pm, LeagueRegistrarGump.View.Rank);
            Assert.Contains(page.Entries.OfType<GumpButton>(), b => b.ButtonID == LeagueRegistrarGump.BtnPromote);

            Press(page, ns, LeagueRegistrarGump.BtnPromote);
            Assert.Equal(2, ClusterFLeagueRanks.GetRank(pm));
            Press(page, ns, LeagueRegistrarGump.BtnPromote);
            _out.WriteLine($"after two presses of one page: rank {ClusterFLeagueRanks.GetRank(pm)}");
            Assert.Equal(2, ClusterFLeagueRanks.GetRank(pm));

            // A fresh page drawn while passing; then the guild rank goes before the press.
            var fresh = new LeagueRegistrarGump(pm, LeagueRegistrarGump.View.Rank);
            Assert.Contains(fresh.Entries.OfType<GumpButton>(), b => b.ButtonID == LeagueRegistrarGump.BtnPromote);
            ClusterFAccountPersistence.GetOrCreateGuild(pm).JoinedGuilds.Clear();
            Press(fresh, ns, LeagueRegistrarGump.BtnPromote);
            Assert.Equal(2, ClusterFLeagueRanks.GetRank(pm));

            // The Promote id pressed on a page that is not the Rank page does nothing.
            GiveGuilds(pm, 1);
            Press(new LeagueRegistrarGump(pm), ns, LeagueRegistrarGump.BtnPromote);
            Assert.Equal(2, ClusterFLeagueRanks.GetRank(pm));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- D3

    [Fact]
    public void AClearedWaiverBlocksUntilTheChapterIsDoneAndOnlyItsOwnRank()
    {
        using var settings = new DefaultSettings();
        var account = NewAccount();
        var pm = ReadyForDullCopper(account);

        try
        {
            // Waived (the default): passes, shown as not yet required.
            var waived = ClusterFLeagueRanks.Evaluate(pm);
            Assert.True(waived.CanPromote);
            Assert.Equal(LeagueCheckState.Waived, waived.Trial.State);
            Assert.Equal(LeagueCheckState.Waived, waived.Job.State);
            Assert.Equal("not yet required", waived.Trial.Detail);

            // Chapter 1 built: Dull Copper now needs it.
            ClusterFLeagueRanks.SetTrialWaived(1, false);
            var blocked = ClusterFLeagueRanks.Evaluate(pm);
            Assert.Equal("Story trial, chapter 1", Blocking(blocked));
            Assert.False(ClusterFLeagueRanks.TryPromote(pm, 1, out _));

            // Other chapters keep their own waiver.
            for (var ch = 2; ch <= ClusterFLeagueRanks.ChapterCount; ch++)
            {
                Assert.True(ClusterFLeagueRanks.IsTrialWaived(ch));
            }

            Data(account).GetOrCreateLeagueData(pm.Serial).TrialsDone.Add(1);
            Assert.Equal(LeagueCheckState.Passed, ClusterFLeagueRanks.Evaluate(pm).Trial.State);
            Assert.True(ClusterFLeagueRanks.TryPromote(pm, 1, out _));
            Assert.Equal(2, ClusterFLeagueRanks.GetRank(pm));

            // Never required after the fact: a Bronze citizen who never played chapters 1 or 2 keeps Bronze and rises
            // to Gold (no trial) with chapter 2 now built.
            var other = NewCharacter(account, 1);
            ClusterFLeagueRanks.SetRank(other, 5);
            ClusterFLeagueRanks.SetTrialWaived(2, false);
            Data(account).LifetimeRenown = LeagueLadderTable.Current.For(6).Renown;
            GiveGuilds(other, 2);
            var gold = ClusterFLeagueRanks.Evaluate(other);
            Assert.Equal(6, gold.To);
            Assert.Equal(LeagueCheckState.NotRequired, gold.Trial.State);
            Assert.True(ClusterFLeagueRanks.TryPromote(other, 5, out _));
            Assert.Equal(6, ClusterFLeagueRanks.GetRank(other));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- D4

    [Fact]
    public void RankNeverDropsOnARenownSpendOrTheResetsLoopParts()
    {
        using var settings = new DefaultSettings();
        var account = NewAccount();
        var pm = NewCharacter(account, 0);

        try
        {
            ClusterFLeagueRanks.SetRank(pm, 5);
            var data = Data(account);
            data.Renown = 3_000;
            data.LifetimeRenown = 3_000;

            Assert.True(data.SpendRenown(3_000));
            Assert.Equal(5, ClusterFLeagueRanks.GetRank(pm));
            Assert.Equal(3_000, data.LifetimeRenown);

            // The loop parts of the reset (and Renown): guilds, exploration, skills, stats, quests, achievements,
            // Renown, discoveries, bulletins. Everything but "League, flags".
            var opts = new ResetOptions(true) { Flags = false };
            ClusterFDevTools.ExecuteReset(pm, pm, account, opts);
            _out.WriteLine($"after the reset without League, flags: rank {ClusterFLeagueRanks.GetRank(pm)}, lifetime {data.LifetimeRenown}");
            Assert.Equal(5, ClusterFLeagueRanks.GetRank(pm));

            // "League, flags" is registration itself: it clears rank with it (the staff tool's own choice).
            ClusterFDevTools.ExecuteReset(pm, pm, account, new ResetOptions(false) { Flags = true });
            Assert.Equal(0, ClusterFLeagueRanks.GetRank(pm));
            Assert.Equal(0, data.LeagueDataCount);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- D5

    [Fact]
    public void TheTableReadsBackRefusesABadFileWholeAndTakesAGoodOne()
    {
        using var settings = new DefaultSettings();

        // The defaults, written as the server writes a missing file, read back the same for every rank.
        var json = LeagueLadderTable.ToJson(LeagueLadderTable.Defaults);
        _out.WriteLine(json);
        var errors = new List<string>();
        var parsed = LeagueLadderTable.Parse(json, "test", errors);
        Assert.Empty(errors);
        Assert.NotNull(parsed);
        for (var r = 1; r <= ClusterFLeagueRanks.TopRank; r++)
        {
            Assert.Equal(LeagueLadderTable.Defaults.For(r), parsed.For(r));
        }

        // A bad file: every kind of bad row at once. Refused whole, each error named, the table in force kept.
        var bad = json
            .Replace("\"metal\": \"Copper\", \"renown\": 1000", "\"metal\": \"Copper\", \"renown\": \"lots\"")
            .Replace("\"metal\": \"Gold\"", "\"metal\": \"Gol\"")
            .Replace("\"metal\": \"Agapite\", \"renown\": 10000, \"guildRank\": \"Apprentice\", \"guilds\": 2, \"chapter\": 0",
                     "\"metal\": \"Agapite\", \"renown\": 10000, \"guildRank\": \"Apprentice\", \"guilds\": 2, \"chapter\": 3")
            .Replace("\"metal\": \"Valorite\", \"renown\": 19000", "\"metal\": \"Valorite\", \"renown\": 100")
            .Replace("\"guildRank\": \"Master\", \"guilds\": 5", "\"guildRank\": \"Boss\", \"guilds\": 5");
        Assert.NotEqual(json, bad);

        var lines = LeagueLadderTable.Apply(bad, "bad file");
        foreach (var line in lines)
        {
            _out.WriteLine(line);
        }

        Assert.Same(LeagueLadderTable.Defaults, LeagueLadderTable.Current);
        Assert.Contains("REFUSED", lines[0]);
        Assert.Contains(lines, l => l.Contains("(Copper)") && l.Contains("renown"));
        Assert.Contains(lines, l => l.Contains("'Gol' is not a ladder metal"));
        Assert.Contains(lines, l => l.Contains("no row for Gold"));
        Assert.Contains(lines, l => l.Contains("chapter 3 is already on"));
        Assert.Contains(lines, l => l.Contains("Valorite") && l.Contains("less than the rank below"));
        Assert.Contains(lines, l => l.Contains("(Celestial)") && l.Contains("guildRank"));

        // Not JSON at all: refused, kept.
        Assert.Contains("REFUSED", LeagueLadderTable.Apply("{ ranks: [", "broken")[0]);
        Assert.Same(LeagueLadderTable.Defaults, LeagueLadderTable.Current);

        // A good edit is put in force.
        var good = json.Replace("\"metal\": \"DullCopper\", \"renown\": 100", "\"metal\": \"DullCopper\", \"renown\": 50");
        var ok = LeagueLadderTable.Apply(good, "good file");
        _out.WriteLine(ok[0]);
        Assert.Equal("good file", LeagueLadderTable.Current.Source);
        Assert.Equal(50, LeagueLadderTable.Current.For(2).Renown);
    }

    // ---------------------------------------------------------------- D6

    [Fact]
    public void TheRegistrarShowsTheRankAndTheFourChecks()
    {
        using var settings = new DefaultSettings();
        var account = NewAccount();
        var pm = ReadyForDullCopper(account);

        try
        {
            var main = new LeagueRegistrarGump(pm);
            var labels = main.Entries.OfType<GumpLabel>().Select(l => l.Text).ToList();
            Assert.Contains("Iron Citizen", labels);
            Assert.Contains(main.Entries.OfType<GumpButton>(), b => b.ButtonID == LeagueRegistrarGump.BtnRank);

            var rank = new LeagueRegistrarGump(pm, LeagueRegistrarGump.View.Rank);
            var text = rank.Entries.OfType<GumpLabel>().Select(l => l.Text).ToList();
            foreach (var t in text)
            {
                _out.WriteLine(t);
            }

            Assert.Contains("Iron Citizen", text);
            Assert.Contains("Dull Copper Citizen", text);
            Assert.Contains("Lifetime Renown", text);
            Assert.Contains("Guild ranks", text);
            Assert.Contains("Promotion job", text);
            Assert.Contains("Story trial, chapter 1", text);
            Assert.Equal(2, text.Count(t => t == "not yet required"));
            Assert.Contains("Promote to Dull Copper Citizen", text);

            // The rank label carries the metal's hue (Dull Copper 0x973).
            var next = rank.Entries.OfType<GumpLabel>().Single(l => l.Text == "Dull Copper Citizen");
            Assert.Equal(CraftResources.GetHue(CraftResource.DullCopper), next.Hue);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- E1

    [Fact]
    public void TheBankStepsOnlyAtMilestones()
    {
        using var settings = new DefaultSettings();
        var account = NewAccount();
        var pm = NewCharacter(account, 0);
        ClusterFBankLimit.OnLogin(pm);
        var bank = pm.BankBox;

        try
        {
            Assert.Equal(1_000, bank.MaxItems);
            Assert.Equal(0, ClusterFLeagueRanks.LeagueMilestonesReached(pm));

            Register(pm);
            Assert.Equal(1, ClusterFLeagueRanks.GetRank(pm));
            Assert.Equal(0, ClusterFLeagueRanks.LeagueMilestonesReached(pm));
            Assert.Equal(1_000, ClusterFBankLimit.GetMaxItems(pm));

            // Walk Iron to Celestial with every check met, one press at a time.
            Data(account).LifetimeRenown = LeagueLadderTable.Current.For(ClusterFLeagueRanks.TopRank).Renown;
            GiveGuilds(pm, 5, ClusterFGuildSystem.RankThresholds(GuildKeys[0])[ClusterFGuildSystem.RankMaster]);

            var steps = new List<string>();
            var promotions = 0;
            while (ClusterFLeagueRanks.GetRank(pm) < ClusterFLeagueRanks.TopRank)
            {
                var from = ClusterFLeagueRanks.GetRank(pm);
                var before = bank.MaxItems;
                Assert.True(ClusterFLeagueRanks.TryPromote(pm, from, out var message), message);
                promotions++;
                Assert.Equal(from + 1, ClusterFLeagueRanks.GetRank(pm));

                var milestone = LeagueLadderTable.Current.For(from + 1).IsMilestone;
                Assert.Equal(before + (milestone ? ClusterFBankLimit.ItemsPerLeagueMilestone : 0), bank.MaxItems);
                if (milestone)
                {
                    steps.Add($"{ClusterFLeagueRanks.RankName(from + 1)} -> {bank.MaxItems}");
                }

                if (from + 1 == 2)
                {
                    Assert.Equal(1, ClusterFLeagueRanks.LeagueMilestonesReached(pm));
                    Assert.Equal(1_100, bank.MaxItems);
                }

                if (from + 1 == 3)
                {
                    Assert.Equal(1_100, bank.MaxItems); // Shadow Iron is not a milestone
                }
            }

            _out.WriteLine($"{promotions} promotions; bank steps: {string.Join("; ", steps)}");
            Assert.Equal(16, promotions);
            Assert.Equal(6, steps.Count);
            Assert.Equal(6, ClusterFLeagueRanks.LeagueMilestonesReached(pm));
            Assert.Equal(1_600, bank.MaxItems);
            Assert.Equal(1_600, ClusterFBankLimit.GetMaxItems(pm));
            Assert.Equal(6, pm.LeagueMilestones);
        }
        finally
        {
            Cleanup(account);
        }
    }
}
