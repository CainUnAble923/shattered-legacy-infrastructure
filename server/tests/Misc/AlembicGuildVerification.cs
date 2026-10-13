// AlembicGuildVerification.cs
//
// cc-P77 (F-43, F-9 table note): the Order of the Alembic, guild 18. It owns Alchemy and Taste ID, taken from the
// Healers' Covenant. Notes in shard-migration notes/cc-P77-alembic-guild.md.
//
// Facts (the brief's tests):
//   1. Joining the Order hands over its starter kit: Alchemy's creation items (4 bottles, a mortar and pestle, a robe;
//      Taste ID has none). The Healers' kit no longer has them (GuildStarterPathVerification fact 1 holds that too).
//   2. Every skill is still in exactly one guild (GuildStarterPathVerification), and Alchemy and Taste ID are the
//      Order's; the Healers keep Healing and Anatomy.
//   3. The Order's ranks read through GuildRankIndex: six ranks on the generic steps, its own names (placeholders),
//      the Apprentice task lifting the index to 1, and League promotion counting it like any guild.
//   4. The guildmaster's post is the tile measured walkable offline (pinned Map.CanFit port, notes/cc-P77-tools/walk.py),
//      clear of the hall props, Sadrah and the Alchemist spawn; the hall seeder would place it there.
// And: the hall props are an addon at their design tiles, and the Order has a row in the Guild Directory, which is
// also the League Registrar's Guild Referrals.
//
// The test host has no map files (D27), so no fact here can ask the map whether a tile is standable; fact 4 pins the
// offline measurement, as NewHavenSeederFloorVerification does.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.MLQuests;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class AlembicGuildVerification
{
    private readonly ITestOutputHelper _out;

    // Measured 2026-10-12 with the hall props placed: fits at z35, all eight neighbors reachable from both doors.
    private static readonly Point3D MeasuredPost = new(3463, 2565, 35);

    // What else stands in the shop: Sadrah the courier (ClusterFNewHavenSeeder) and pinned's Alchemist spawn.
    private static readonly Point2D Sadrah = new(3462, 2567);
    private static readonly Point2D AlchemistSpawn = new(3461, 2566);

    public AlembicGuildVerification(ITestOutputHelper output)
    {
        _out = output;
        EnsureStartupHooks();

        if (!MLQuestSystem.Enabled)
        {
            MLQuestSystem.Configure();
        }

        ClusterFGuildSystem.EnsureRegistered();
    }

    private static bool _startupHooksRun;

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

    private static Account NewAccount() => new($"p77{Guid.NewGuid():N}"[..16], "p77-test-only");

    private static PlayerMobile NewCharacter(Account account)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human };
        pm.AddItem(new Backpack());
        pm.RawStr = pm.RawDex = pm.RawInt = 50;
        account[0] = pm;
        pm.MoveToWorld(new Point3D(1200, 1200, 0), Map.Trammel);
        return pm;
    }

    private static List<Item> Owned(PlayerMobile pm)
    {
        var all = new List<Item>();
        foreach (var worn in pm.Items)
        {
            if (worn is not Backpack)
            {
                all.Add(worn);
            }
        }

        if (pm.Backpack != null)
        {
            foreach (var item in pm.Backpack.FindItemsByType<Item>())
            {
                all.Add(item);
            }
        }

        return all;
    }

    private static void Cleanup(Account account)
    {
        if (account[0] is PlayerMobile pm)
        {
            MLQuestSystem.HandleDeletion(pm);
            pm.Delete();
        }

        Accounts.Remove(account);
    }

    private static GuildDef Order => Assert.IsType<GuildDef>(ClusterFGuildSystem.GetDef(ClusterFAlembicGuild.Key));

    // ---------------------------------------------------------------- 1

    [Fact]
    public void JoiningTheOrderHandsOverTheAlchemyKitAndTheHealersNoLonger()
    {
        var account = NewAccount();
        var pm = NewCharacter(account);

        try
        {
            Assert.Equal("Order of the Alembic", Order.Name);
            Assert.True(ClusterFGuildSystem.CanJoin(pm, Order, out var reason), reason);

            ClusterFGuildSystem.Join(pm, Order);
            var owned = Owned(pm);
            _out.WriteLine("after joining the Order: " + string.Join(", ", owned.Select(i => $"{i.GetType().Name}x{i.Amount}")));

            Assert.True(ClusterFGuildSystem.IsJoined(pm, ClusterFAlembicGuild.Key));
            Assert.Contains(owned, i => i is Bottle { Amount: 4 });
            Assert.Single(owned, i => i is MortarPestle);
            Assert.Single(owned, i => i is Robe);
            Assert.Equal(3, owned.Count);

            // Joining is free: no skill, no gold, Initiate.
            Assert.Equal("Initiate", ClusterFGuildSystem.GetRankName(ClusterFAlembicGuild.Key, ClusterFAccountPersistence.GetOrCreateGuild(pm)));

            // The Healers' kit, on the same character: bandages, scissors, Anatomy's robe; no bottle, no mortar.
            var before = owned.Count;
            ClusterFGuildSystem.Join(pm, Assert.IsType<GuildDef>(ClusterFGuildSystem.GetDef("healers")));
            var healers = Owned(pm).Except(owned).ToList();
            _out.WriteLine("then the Healers: " + string.Join(", ", healers.Select(i => $"{i.GetType().Name}x{i.Amount}")));

            Assert.DoesNotContain(healers, i => i is Bottle or MortarPestle);
            Assert.Contains(healers, i => i is Bandage { Amount: 50 });
            Assert.Contains(healers, i => i is Scissors);
            Assert.True(Owned(pm).Count > before);
        }
        finally
        {
            Cleanup(account);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AlchemyAndTasteIdAreTheOrdersAndTheHealersKeepHealingAndAnatomy()
    {
        Assert.Equal(new[] { SkillName.Alchemy, SkillName.TasteID }, GuildSkillTable.For(ClusterFAlembicGuild.Key));
        Assert.Equal(new[] { SkillName.Healing, SkillName.Anatomy }, GuildSkillTable.For("healers"));

        foreach (var skill in new[] { SkillName.Alchemy, SkillName.TasteID })
        {
            var owners = GuildSkillTable.Skills.Where(g => g.Value.Contains(skill)).Select(g => g.Key).ToList();
            Assert.Equal(ClusterFAlembicGuild.Key, Assert.Single(owners));
        }

        // Its Apprentice task is Alchemy 50 or 10 empty bottles, like the other craft guilds' tasks.
        Assert.Equal(SkillName.Alchemy, Order.TaskSkill);
        Assert.Equal(typeof(Bottle), Order.TaskItemType);
        _out.WriteLine($"teaches: {GuildSkillTable.Describe(ClusterFAlembicGuild.Key)}");
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void TheOrdersRanksReadThroughGuildRankIndex()
    {
        int[] thresholds = [0, 1_000, 5_000, 15_000, 50_000, 100_000];

        Assert.Equal(thresholds, ClusterFGuildSystem.RankThresholds(ClusterFAlembicGuild.Key));
        Assert.Equal(6, ClusterFAlembicGuild.RankNames.Length);

        for (var i = 0; i < thresholds.Length; i++)
        {
            Assert.Equal(i, ClusterFGuildSystem.GuildRankIndex(ClusterFAlembicGuild.Key, thresholds[i]));
            Assert.Equal(ClusterFAlembicGuild.RankNames[i], ClusterFGuildSystem.GetRankName(ClusterFAlembicGuild.Key, thresholds[i]));
            if (i > 0)
            {
                Assert.Equal(i - 1, ClusterFGuildSystem.GuildRankIndex(ClusterFAlembicGuild.Key, thresholds[i] - 1));
            }

            _out.WriteLine($"{thresholds[i],7:N0} index {i} {ClusterFGuildSystem.GetRankName(ClusterFAlembicGuild.Key, thresholds[i])}");
        }

        // The Apprentice task lifts the index to 1, and League promotion counts the Order like any joined guild.
        var data = new CharacterGuildData();
        data.JoinedGuilds.Add(ClusterFAlembicGuild.Key);
        Assert.Equal(0, ClusterFGuildSystem.GuildRankIndex(ClusterFAlembicGuild.Key, data));
        Assert.Equal(0, ClusterFLeagueRanks.CountGuildsAtOrAbove(data, ClusterFGuildSystem.RankApprentice));

        data.ApprenticeGuilds.Add(ClusterFAlembicGuild.Key);
        Assert.Equal(ClusterFGuildSystem.RankApprentice, ClusterFGuildSystem.GuildRankIndex(ClusterFAlembicGuild.Key, data));
        Assert.Equal(1, ClusterFLeagueRanks.CountGuildsAtOrAbove(data, ClusterFGuildSystem.RankApprentice));

        data.AddReputation(ClusterFAlembicGuild.Key, 15_000);
        Assert.Equal(ClusterFGuildSystem.RankMaster, ClusterFGuildSystem.GuildRankIndex(ClusterFAlembicGuild.Key, data));
        Assert.Equal(1, ClusterFLeagueRanks.CountGuildsAtOrAbove(data, ClusterFGuildSystem.RankMaster));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void TheGuildmastersPostIsTheMeasuredWalkableTileClearOfThePropsAndTheShopsNpcs()
    {
        var loc = Assert.Single(GuildLocations.For(ClusterFAlembicGuild.Key));

        Assert.Equal(typeof(AlembicGuildmaster), loc.NpcType);
        Assert.Same(Order, ClusterFGuildSystem.GetDefForGuildmaster(typeof(AlembicGuildmaster)));
        Assert.Equal(GuildLocation.TrammelIndex, loc.MapIndex);
        Assert.Equal(MeasuredPost, loc.Point);
        Assert.True(loc.Seeded, "the hall seeder places it");

        // Not on the shop's other NPCs, and no prop on the post or the eight tiles around it.
        var post = new Point2D(loc.Point.X, loc.Point.Y);
        Assert.NotEqual(Sadrah, post);
        Assert.NotEqual(AlchemistSpawn, post);

        foreach (var c in ClusterFAlembicGuild.Hall)
        {
            var near = Math.Max(Math.Abs(c.X - loc.Point.X), Math.Abs(c.Y - loc.Point.Y)) <= 1;
            Assert.False(near, $"{c.Name} at {c.X},{c.Y} stands on or beside the guildmaster");
        }

        // The hall seeder, as a dry run: it would place this guildmaster on this tile and changes nothing.
        var report = ClusterFGuildHallSeeder.Seed(dryRun: true, repair: false);
        var line = Assert.Single(report, l => l.StartsWith($"{ClusterFAlembicGuild.Key} (AlembicGuildmaster)"));
        _out.WriteLine(line);
        Assert.Contains($"would place at {MeasuredPost}", line);

        // The NPC itself: invulnerable, the Order's key, and its hall page.
        var gm = new AlembicGuildmaster();
        try
        {
            Assert.Equal(ClusterFAlembicGuild.Key, gm.GuildKey);
            Assert.True(gm.IsInvulnerable);
        }
        finally
        {
            gm.Delete();
        }
    }

    // ---------------------------------------------------------------- and

    [Fact]
    public void TheHallPropsAreOneAddonAtTheirDesignTiles()
    {
        var addon = new AlembicHallAddon();

        try
        {
            Assert.Equal(ClusterFAlembicGuild.Hall.Length, addon.Components.Count);
            var origin = ClusterFAlembicGuild.Hall[0].Location;
            addon.MoveToWorld(origin, Map.Trammel);

            var placed = addon.Components.Select(c => (c.ItemID, c.X, c.Y, c.Z)).OrderBy(t => t).ToList();
            var design = ClusterFAlembicGuild.Hall.Select(c => (c.ItemID, c.X, c.Y, c.Z)).OrderBy(t => t).ToList();
            Assert.Equal(design, placed);

            // Every prop is inside the shop's walls (x3457-3464, y2563-2570), along the north wall.
            Assert.All(ClusterFAlembicGuild.Hall, c => Assert.InRange(c.X, 3457, 3464));
            Assert.All(ClusterFAlembicGuild.Hall, c => Assert.Equal(2563, c.Y));
        }
        finally
        {
            addon.Delete();
        }
    }

    [Fact]
    public void TheOrderHasADirectoryRowAndANamedCurrency()
    {
        var rows = GuildProgressGump.Guilds();
        Assert.Contains(rows, d => d.Key == ClusterFAlembicGuild.Key);
        Assert.Equal(ClusterFGuildSystem.AllGuilds.Count, rows.Count);

        Assert.Equal(ClusterFAlembicGuild.CurrencyName, MuleUpgrades.CurrencyName(ClusterFAlembicGuild.Key));
    }
}
