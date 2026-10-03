// SmithCommissionRewardsVerification.cs
//
// cc-P46 Part D (Chase, 2026-10-03). cc-P42 Part G2 raised bulk orders to x3 Seals and x2 standing; commissions
// (SmithCommissionSystem) now get the same, applied after their floors, so each pays exactly three times the Seals and
// twice the standing it did and keeps its place among commissions and bulk orders. The "before" figures below are
// computed from the formulas and pinned's gold table (shard-migration notes/cc-P46-smith-orders-2.md, Part D;
// D:\UO\cc-p46-work\d_calc.py), as P42's fact 5 did for bulk orders.
//
// Facts:
//   1. A small commission, five representative kinds, 40 rewards each: Seals are a multiple of 3 whose third lies in the
//      old range, and standing is exactly twice the old.
//   2. A large commission: standing is exactly twice the old set formula, and Seals a multiple of 3 whose third is at
//      least the old floor of 5.
//   3. Completing a commission pays the entry's Seals and standing into the character's guild data.

using System;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SmithCommissionRewardsVerification
{
    private readonly ITestOutputHelper _out;

    public SmithCommissionRewardsVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ShardTestHost.EnsureCraftSystems();
        ShardTestHost.EnsureSkillChecks(); // Complete rolls Mobile.CheckSkill
    }

    private static (Account, PlayerMobile) Smith(double skill)
    {
        var account = new Account($"p46d{Guid.NewGuid():N}"[..16], "p46-test-only");
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.Skills.Blacksmith.Cap = 300.0;
        pm.Skills.Blacksmith.Base = skill;
        account[0] = pm;
        pm.MoveToWorld(new Point3D(1580, 1580, 0), Map.Trammel);
        ClusterFAccountPersistence.GetOrCreateGuild(pm).JoinedGuilds.Add("smithing");
        return (account, pm);
    }

    private static void Done(Account account, PlayerMobile pm)
    {
        ClusterFAccountPersistence.Get(account)?.ClearGuildData();
        pm.Delete();
        Accounts.Remove(account);
    }

    // ---------------------------------------------------------------- 1

    [Theory]
    [InlineData(CraftResource.Iron, false, 1, 2, 40)]
    [InlineData(CraftResource.Iron, true, 2, 3, 80)]
    [InlineData(CraftResource.Valorite, true, 27, 33, 400)]
    [InlineData(CraftResource.Platinum, true, 40, 50, 440)]
    [InlineData(CraftResource.Celestial, true, 135, 167, 720)]
    public void ASmallCommissionPaysThreeTimesTheSealsAndTwiceTheStanding(
        CraftResource mat, bool exceptional, int oldLow, int oldHigh, int oldStanding)
    {
        var seen = new System.Collections.Generic.List<int>();
        for (var i = 0; i < 40; i++)
        {
            var (seals, standing) = SmithCommissionSystem.ComputeReward(mat, exceptional);
            seen.Add(seals);
            Assert.Equal(0, seals % 3);
            Assert.InRange(seals / 3, oldLow, oldHigh);
            Assert.Equal(oldStanding * 2, standing);
        }

        _out.WriteLine($"{mat} {(exceptional ? "exceptional" : "regular")}: Seals {seen.Min()}-{seen.Max()} " +
                       $"(was {oldLow}-{oldHigh}), standing {oldStanding * 2} (was {oldStanding})");
    }

    // ---------------------------------------------------------------- 2

    [Theory]
    [InlineData(30.0)]
    [InlineData(100.0)]
    [InlineData(130.0)]
    public void ALargeCommissionPaysThreeTimesTheSealsAndTwiceTheStanding(double skill)
    {
        var (account, pm) = Smith(skill);
        try
        {
            for (var i = 0; i < 20; i++)
            {
                var c = SmithCommissionSystem.GenerateLarge(pm);
                Assert.NotNull(c);
                var set = SmithCommissionSetPool.GetSet(c.SetKey);
                var (_, pieceStanding) = SmithCommissionSystem.ComputeBaseReward(c.Material, c.RequireExceptional);
                var oldStanding = Math.Max(50, (int)(pieceStanding * set.ItemKeys.Length * 1.5 * set.RewardMultiplier));

                if (i == 0)
                {
                    _out.WriteLine($"{skill}: {c.SetKey} {c.Material} exc {c.RequireExceptional}: {c.SealReward} Seals, " +
                                   $"{c.StandingReward} standing (was {c.SealReward / 3}, {oldStanding})");
                }

                Assert.Equal(oldStanding * 2, c.StandingReward);
                Assert.Equal(0, c.SealReward % 3);
                Assert.True(c.SealReward / 3 >= 5);
                ClusterFAccountPersistence.GetOrCreateGuild(pm).SmithLargeCommissions.Clear();
            }
        }
        finally
        {
            Done(account, pm);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void CompletingACommissionPaysItsReward()
    {
        var (account, pm) = Smith(100.0);
        try
        {
            var c = SmithCommissionSystem.Generate(pm);
            Assert.NotNull(c);
            var guild = ClusterFAccountPersistence.GetOrCreateGuild(pm);
            var seals = guild.GetCurrency("smithing");
            var standing = guild.GetReputation("smithing");

            SmithCommissionSystem.Complete(pm, c, new Longsword());

            _out.WriteLine($"{c.FullLabel}: paid {guild.GetCurrency("smithing") - seals} Seals, " +
                           $"{guild.GetReputation("smithing") - standing} standing");
            Assert.Equal(seals + c.SealReward, guild.GetCurrency("smithing"));
            Assert.Equal(standing + c.StandingReward, guild.GetReputation("smithing"));
            Assert.Equal(0, c.SealReward % 3);
            Assert.DoesNotContain(c, guild.SmithCommissions);
        }
        finally
        {
            Done(account, pm);
        }
    }
}
