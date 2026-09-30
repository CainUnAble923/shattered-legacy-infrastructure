// ResetStoneVerification.cs
//
// cc-P18, F-7: guild data per character, and the player's account reset (the Reset Stone in New Haven
// and [ResetMyAccount). Notes in shard-migration notes/cc-P18-reset-stone-young-craftx.md.
//
// Facts (the brief's numbering; 2 and 3, old saves and the round trip, are in
// AccountDataSerializerVerification):
//   1. Two characters on one account: joining a guild on one does not join the other; reputation and
//      scrip are separate, earned through the real Apprentice task and a real work order turn-in.
//   4. The reset with everything selected leaves the account's guild data, exploration, work orders,
//      commissions, discoveries and flags empty on every character.
//   5. [ResetMyAccount, typed by a player with another account's name after it, opens the reset for
//      the player's own account and character only; [ClusterFReset is refused to a player; the stone
//      opens the reset for whoever uses it.
//   6. The seeder run twice places one stone; a dry run places none.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.MLQuests;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ResetStoneVerification
{
    private readonly ITestOutputHelper _out;

    private static readonly Point3D Spot = new(1210, 1210, 0);

    public ResetStoneVerification(ITestOutputHelper output)
    {
        _out = output;
        EnsureStartupHooks();

        // The server calls these Configure methods; the test host calls none.
        if (!MLQuestSystem.Enabled)
        {
            MLQuestSystem.Configure();
        }

        ClusterFGuildSystem.EnsureRegistered();
        ClusterFDevTools.Configure(); // registers [ClusterFReset and [ResetMyAccount; a second call is a no-op
    }

    // ---------------------------------------------------------------- helpers

    private static bool _startupHooksRun;

    // As GuildStarterPathVerification: new Account(...) needs Accounts.Configure and a password algorithm.
    private static void EnsureStartupHooks()
    {
        if (!_startupHooksRun)
        {
            Accounts.Configure();
            WelcomeTimer.Initialize();
            _startupHooksRun = true;
        }

        if (AccountSecurity.CurrentAlgorithm == PasswordProtectionAlgorithm.None)
        {
            AccountSecurity.CurrentAlgorithm = PasswordProtectionAlgorithm.PBKDF2;
        }
    }

    private static Account NewAccount() => new($"p18{Guid.NewGuid():N}"[..16], "p18-test-only");

    private static PlayerMobile NewCharacter(Account account, int slot)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.RawStr = pm.RawDex = pm.RawInt = 50;
        account[slot] = pm;
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private static GuildDef Guild(string key) => Assert.IsType<GuildDef>(ClusterFGuildSystem.GetDef(key));

    private static void Cleanup(params Account[] accounts)
    {
        foreach (var account in accounts)
        {
            for (var i = 0; i < account.Length; i++)
            {
                if (account[i] is PlayerMobile pm)
                {
                    if (pm.NetState != null)
                    {
                        pm.NetState.Mobile = null;
                        pm.NetState = null;
                    }

                    MLQuestSystem.HandleDeletion(pm);
                    pm.Delete();
                }
            }

            Accounts.Remove(account);
        }
    }

    private static NetState Online(PlayerMobile pm)
    {
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        return ns;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void TwoCharactersOnOneAccountHaveTheirOwnGuildsReputationAndScrip()
    {
        var account = NewAccount();
        var first = NewCharacter(account, 0);
        var second = NewCharacter(account, 1);
        var thieves = Guild("thieves");

        try
        {
            ClusterFGuildSystem.Join(first, thieves);

            Assert.True(ClusterFGuildSystem.IsJoined(first, "thieves"));
            Assert.False(ClusterFGuildSystem.IsJoined(second, "thieves"));
            Assert.True(ClusterFGuildSystem.CanJoin(second, thieves, out var reason), reason);

            // The Apprentice task pays reputation and scrip, to the character that did it.
            first.Skills[SkillName.Stealing].Base = 20.0;
            Assert.True(ClusterFGuildSystem.CompleteApprenticeTask(first, thieves, out reason), reason);

            var one = ClusterFAccountPersistence.GetOrCreateGuild(first);
            var two = ClusterFAccountPersistence.GetOrCreateGuild(second);
            Assert.Equal(thieves.JoinReputation, one.GetReputation("thieves"));
            Assert.Equal(thieves.JoinScrip, one.GetCurrency("thieves"));
            Assert.Equal(0, two.GetReputation("thieves"));
            Assert.Equal(0, two.GetCurrency("thieves"));
            Assert.True(ClusterFGuildSystem.IsApprentice(one, "thieves"));
            Assert.False(ClusterFGuildSystem.IsApprentice(two, "thieves"));

            // The second character cannot do the first's Apprentice task: it is not in the guild.
            second.Skills[SkillName.Stealing].Base = 20.0;
            Assert.False(ClusterFGuildSystem.CompleteApprenticeTask(second, thieves, out reason));
            Assert.Equal("Join the guild first.", reason);

            // The second joins on its own: a new member, rank Initiate, nothing of the first's.
            ClusterFGuildSystem.Join(second, thieves);
            Assert.True(ClusterFGuildSystem.IsJoined(second, "thieves"));
            Assert.Equal("Initiate", ClusterFGuildSystem.GetRankName("thieves", two));
            Assert.Equal("Apprentice", ClusterFGuildSystem.GetRankName("thieves", one));

            // Scrip spent by one is not spent by the other.
            two.AddCurrency("thieves", 5);
            Assert.True(one.SpendCurrency("thieves", thieves.JoinScrip));
            Assert.Equal(0, one.GetCurrency("thieves"));
            Assert.Equal(5, two.GetCurrency("thieves"));

            var data = ClusterFAccountPersistence.GetOrCreate(account);
            Assert.Equal(2, data.GuildDataCount);
            _out.WriteLine($"first: {string.Join(",", one.JoinedGuilds)} rep {one.GetReputation("thieves")}; " +
                           $"second: {string.Join(",", two.JoinedGuilds)} scrip {two.GetCurrency("thieves")}");
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheResetWithEverythingSelectedEmptiesEveryCharacter()
    {
        var account = NewAccount();
        var first = NewCharacter(account, 0);
        var second = NewCharacter(account, 1);

        try
        {
            var data = ClusterFAccountPersistence.GetOrCreate(account);

            foreach (var pm in new[] { first, second })
            {
                ClusterFGuildSystem.Join(pm, Guild("warriors"));
                var g = ClusterFAccountPersistence.GetOrCreateGuild(pm);
                g.ApprenticeGuilds.Add("warriors");
                g.AddReputation("mining", 50);
                g.AddCurrency("mining", 9);
                g.ActiveWorkOrders.Add(new WorkOrderEntry("mining.dullcopper.50", "mining"));
                g.CompletedWorkOrders.Add(new WorkOrderEntry("smithing.plate.10", "smithing"));
                g.SmithCommissions.Add(new SmithCommissionEntry(
                    "c1", "platechest", CraftResource.Iron, false, "Sir Test", "", 1, 2));
                g.SmithLargeCommissions.Add(new SmithLargeCommissionEntry(
                    "L1", "ringmail", CraftResource.Iron, false, "Dame Test", "", 3, 4));
                g.AcceptArtificerOrder("artificer.slayer.silver", 0);

                data.GetOrCreateExploration(pm.Serial, 1, 40, 30)[5] = true;
            }

            data.Renown = 10;
            data.AchievementPoints = 20;
            data.LastSeenBulletinId = 30;
            data.RestorationRegistry["legacy.jacobs_pickaxe"] = new RestorationEntry("legacy.jacobs_pickaxe", "quest");
            data.OreDiscoveries["Valorite"] = new OreDiscoveryEntry("Valorite");
            data.WoodDiscoveries["Ironwood"] = new WoodDiscoveryEntry("Ironwood");
            data.ImbuingDiscoveries["Hit Chance Increase"] = 3;
            data.EncounteredCreatures.Add("Dragon");
            data.SetFlag("league.joined");
            data.SetFlagValue("league.registered_at", "2026-09-30");
            first.Skills[SkillName.Mining].Base = 40.0;

            Assert.Equal(2, data.GuildDataCount);
            Assert.Equal(2, data.GuildStarterRecordCount);
            Assert.Equal(2, data.ExploredCharacterCount);

            ClusterFDevTools.ExecuteReset(first, first, account, new ResetOptions(true));

            // Every character: no guild data, no starter record, no exploration.
            Assert.Equal(0, data.GuildDataCount);
            Assert.Null(data.GetGuildData(first.Serial));
            Assert.Null(data.GetGuildData(second.Serial));
            Assert.Equal(0, data.GuildStarterRecordCount);
            Assert.Equal(0, data.ExploredCharacterCount);
            Assert.False(ClusterFGuildSystem.IsJoined(first, "warriors"));
            Assert.False(ClusterFGuildSystem.IsJoined(second, "warriors"));

            // The account: discoveries, flags, renown, AP, bulletins.
            Assert.Empty(data.OreDiscoveries);
            Assert.Empty(data.WoodDiscoveries);
            Assert.Empty(data.ImbuingDiscoveries);
            Assert.Empty(data.EncounteredCreatures);
            Assert.Empty(data.Flags);
            Assert.Empty(data.FlagValues);
            Assert.Empty(data.RestorationRegistry);
            Assert.Equal(0, data.Renown);
            Assert.Equal(0, data.AchievementPoints);
            Assert.Equal(0, data.LastSeenBulletinId);

            // This character's skills; the other's are its own and untouched.
            Assert.Equal(0.0, first.Skills[SkillName.Mining].Base);

            // Both can start again: join, and the tools come again.
            Assert.True(ClusterFGuildSystem.CanJoin(second, Guild("warriors"), out _));
            Assert.False(ClusterFGuildStarter.HasTools(first, "warriors"));

            // The confirm step says, line by line, what the reset clears and whom it clears for.
            var lines = new ResetOptions(true).Describe(first.Name);
            foreach (var line in lines)
            {
                _out.WriteLine(line);
            }

            Assert.Equal(10, lines.Count);
            Assert.Equal(2, lines.Count(l => l.EndsWith("EVERY character", StringComparison.Ordinal)));
            Assert.Contains(lines, l => l.StartsWith("Guilds", StringComparison.Ordinal) && l.Contains("work orders"));
            Assert.Contains(lines, l => l.StartsWith("Exploration", StringComparison.Ordinal));
            Assert.Contains(lines, l => l.StartsWith("Ore, wood and imbuing discoveries", StringComparison.Ordinal));
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void ResetMyAccountAndTheStoneReachOnlyTheCallersOwnAccount()
    {
        var mine = NewAccount();
        var theirs = NewAccount();
        var me = NewCharacter(mine, 0);
        var them = NewCharacter(theirs, 0);
        var stone = new ResetStone();

        try
        {
            Assert.Equal(AccessLevel.Player, me.AccessLevel);
            Online(me);

            // Another account's name after the command changes nothing: the command takes no argument.
            Assert.True(CommandSystem.Handle(me, $"{CommandSystem.Prefix}ResetMyAccount {theirs.Username}"));
            var gump = me.FindGump<DevResetGump>();
            Assert.NotNull(gump);
            _out.WriteLine($"[ResetMyAccount {theirs.Username} by {mine.Username}: gump for {gump.Account.Username}");
            Assert.Same(mine, gump.Account);
            Assert.Same(me, gump.Character);
            me.CloseGump<DevResetGump>();

            // The admin command is not a player's.
            CommandSystem.Handle(me, $"{CommandSystem.Prefix}ClusterFReset {theirs.Username}");
            Assert.Null(me.FindGump<DevResetGump>());

            // The stone: whoever uses it gets their own account, and only within reach.
            stone.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
            stone.OnDoubleClick(me);
            gump = me.FindGump<DevResetGump>();
            Assert.NotNull(gump);
            Assert.Same(mine, gump.Account);
            me.CloseGump<DevResetGump>();

            Online(them);
            stone.OnDoubleClick(them);
            Assert.Same(theirs, them.FindGump<DevResetGump>()?.Account);

            me.MoveToWorld(new Point3D(Spot.X + 10, Spot.Y, Spot.Z), Map.Trammel);
            stone.OnDoubleClick(me);
            Assert.Null(me.FindGump<DevResetGump>());

            // Not movable and does not decay.
            Assert.False(stone.Movable);
            Assert.False(stone.Decays);
        }
        finally
        {
            stone.Delete();
            Cleanup(mine, theirs);
        }
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void TheSeederRunTwicePlacesOneStone()
    {
        foreach (var old in World.Items.Values.OfType<ResetStone>().ToList())
        {
            old.Delete();
        }

        try
        {
            var dry = ClusterFResetStoneSeeder.Seed(true);
            _out.WriteLine(dry.Message);
            Assert.False(dry.Placed);
            Assert.Empty(World.Items.Values.OfType<ResetStone>());

            var first = ClusterFResetStoneSeeder.Seed(false);
            var second = ClusterFResetStoneSeeder.Seed(false);
            _out.WriteLine(first.Message);
            _out.WriteLine(second.Message);

            Assert.True(first.Placed);
            Assert.False(second.Placed);

            var stones = World.Items.Values.OfType<ResetStone>().Where(s => !s.Deleted).ToList();
            Assert.Single(stones);
            Assert.Equal(ClusterFResetStoneSeeder.Location, stones[0].Location);
            Assert.Equal(Map.Trammel, stones[0].Map);
            Assert.Same(stones[0], second.Existing);
            Assert.Equal(ResetStone.StoneGraphic, stones[0].ItemID);
        }
        finally
        {
            foreach (var s in World.Items.Values.OfType<ResetStone>().ToList())
            {
                s.Delete();
            }
        }
    }
}
