// SmithOrdersTeachVerification.cs
//
// cc-P42 Part G (Chase, 2026-10-03).
//
// G1. The Society of Smiths' orders (small and large bulk orders, small and large commissions) ask for items that can
// still raise the smith's Blacksmithy. The engine's rule (ClusterFSmithTeaching): crafting rolls CheckSkill(skill, item
// min, item max) (CraftItem.cs:874-894), which gains only for min <= skill < max (SkillCheck.cs:56-74). When nothing on
// offer teaches, the order falls back to the hardest items the smith can make. Stock generation accepted anything with a
// success chance above 0, so a Grandmaster was asked for ringmail.
//
// G2. Turn-in rewards: Seals x3 and standing x2, no gold, exceptional and large still worth more, Seal floor kept.
//
// Facts:
//   1. At 30, 70, 100 and 120 Blacksmithy (and 130, past every BOD item's maximum), every small guild order's item
//      teaches, or, when no BOD item teaches at all, is the hardest the smith can make.
//   2. At 70.1, 100, 120 and 130, every large guild order's set teaches piece for piece, or is the set the fallback
//      names (the most teaching pieces, then the highest lowest maximum): plate at 100, 120 and 130.
//   3. At 30, 70, 100, 120 and 130, every commission (small: its item; large: its set) teaches, or is a fallback.
//   4. Regular smiths are unchanged: the one-argument SmallSmithBOD.CreateRandomFor still gives a Grandmaster items
//      that teach nothing.
//   5. The turn-in numbers: Seals are exactly three times the old count and standing twice the old, for the five
//      representative orders in the notes.

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Accounting.Security;
using Server.Engines.BulkOrders;
using Server.Engines.Craft;
using Server.Items;
using Server.Misc;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SmithOrdersTeachVerification
{
    private const int Draws = 60;

    private readonly ITestOutputHelper _out;

    public SmithOrdersTeachVerification(ITestOutputHelper output)
    {
        _out = output;
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

        // The craft system is built at server start; a test run that reaches this class first has none
        // (as CraftRegistrationsVerification and CraftXVerification do it).
        if (DefBlacksmithy.CraftSystem == null)
        {
            DefBlacksmithy.Initialize();
        }

        BlacksmithyCraftRegistrations.Register(); // idempotent
    }

    private static bool _startupHooksRun;

    private static (Account, PlayerMobile) Smith(double skill)
    {
        var account = new Account($"p42g{Guid.NewGuid():N}"[..16], "p42-test-only");
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.Skills.Blacksmith.Cap = 300.0;
        pm.Skills.Blacksmith.Base = skill;
        account[0] = pm;
        pm.MoveToWorld(new Point3D(1560, 1560, 0), Map.Trammel);
        ClusterFAccountPersistence.GetOrCreate(account).GetOrCreateGuildData(pm.Serial).JoinedGuilds.Add("smithing");
        return (account, pm);
    }

    private static void Done(Account account, PlayerMobile pm)
    {
        ClusterFAccountPersistence.Get(account)?.ClearGuildData();
        pm.Delete();
        Accounts.Remove(account);
    }

    private static Type[] Armor => SmallBulkEntry.BlacksmithArmor.Select(e => e.Type).ToArray();
    private static Type[] Weapons => SmallBulkEntry.BlacksmithWeapons.Select(e => e.Type).ToArray();

    private static bool AnyTeaches(Mobile m, IEnumerable<Type> types, bool exceptional) =>
        types.Any(t => ClusterFSmithTeaching.CanMake(m, t, exceptional) && ClusterFSmithTeaching.Teaches(m, t));

    private static double Hardest(Mobile m, IEnumerable<Type> types, bool exceptional) =>
        types.Where(t => ClusterFSmithTeaching.CanMake(m, t, exceptional)).Max(ClusterFSmithTeaching.MaxSkill);

    private static string Tally(IEnumerable<string> names) =>
        string.Join(", ", names.GroupBy(n => n).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} x{g.Count()}"));

    // ---------------------------------------------------------------- 1

    [Theory]
    [InlineData(30.0)]
    [InlineData(70.0)]
    [InlineData(100.0)]
    [InlineData(120.0)]
    [InlineData(130.0)]
    public void EverySmallGuildOrderTeachesOrIsTheHardest(double skill)
    {
        var (account, pm) = Smith(skill);
        try
        {
            var names = new List<string>();
            for (var i = 0; i < Draws; i++)
            {
                var bod = SmallSmithBOD.CreateRandomFor(pm, true);
                Assert.NotNull(bod);
                names.Add(bod.Type.Name + (bod.RequireExceptional ? " (exc)" : ""));

                if (!ClusterFSmithTeaching.Teaches(pm, bod.Type))
                {
                    // A fallback: nothing on either list teaches, and this is the hardest the smith can make.
                    Assert.False(AnyTeaches(pm, Armor.Concat(Weapons), false), $"{bod.Type.Name} at {skill} teaches nothing but something else does");
                    var list = Armor.Contains(bod.Type) ? Armor : Weapons;
                    Assert.Equal(Hardest(pm, list, bod.RequireExceptional), ClusterFSmithTeaching.MaxSkill(bod.Type));
                }

                bod.Delete();
            }

            _out.WriteLine($"small at {skill}: {Tally(names)}");
        }
        finally
        {
            Done(account, pm);
        }
    }

    // ---------------------------------------------------------------- 2

    [Theory]
    [InlineData(70.1, false)]
    [InlineData(100.0, true)]
    [InlineData(120.0, true)]
    [InlineData(130.0, true)]
    public void EveryLargeGuildOrderTeachesOrIsTheClosestSet(double skill, bool plateOnly)
    {
        var (account, pm) = Smith(skill);
        try
        {
            var names = new List<string>();
            for (var i = 0; i < Draws; i++)
            {
                var bod = LargeSmithBOD.CreateRandomFor(pm);
                Assert.NotNull(bod);
                var types = bod.Entries.Select(e => e.Details.Type).ToArray();
                names.Add(string.Join("+", types.Select(t => t.Name).Take(2)) + "..." + (bod.RequireExceptional ? " (exc)" : ""));

                if (plateOnly)
                {
                    Assert.Equal(LargeSmithBOD.SetTypes(1), types);
                }

                if (!types.All(t => ClusterFSmithTeaching.Teaches(pm, t)))
                {
                    var anyFull = Enumerable.Range(0, 8).Any(s => LargeSmithBOD.SetTypes(s).All(t =>
                        ClusterFSmithTeaching.CanMake(pm, t, bod.RequireExceptional) && ClusterFSmithTeaching.Teaches(pm, t)));
                    Assert.False(anyFull, $"a set teaches fully at {skill} but {types[0].Name}'s set was given");
                }

                bod.Delete();
            }

            _out.WriteLine($"large at {skill}: {Tally(names)}");
        }
        finally
        {
            Done(account, pm);
        }
    }

    // ---------------------------------------------------------------- 3

    [Theory]
    [InlineData(30.0)]
    [InlineData(70.0)]
    [InlineData(100.0)]
    [InlineData(120.0)]
    [InlineData(130.0)]
    public void EveryCommissionTeachesOrIsAFallback(double skill)
    {
        var (account, pm) = Smith(skill);
        try
        {
            var guild = ClusterFAccountPersistence.GetOrCreate(account).GetOrCreateGuildData(pm.Serial);
            var allKeys = typeof(SmithCommissionPool).GetField("AllKeys",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetValue(null) as string[];
            Assert.NotNull(allKeys);
            var allTypes = allKeys.Select(SmithCommissionPool.GetItemType).ToArray();

            var small = new List<string>();
            var large = new List<string>();
            for (var i = 0; i < Draws; i++)
            {
                guild.SmithCommissions.Clear();
                var c = SmithCommissionSystem.Generate(pm);
                Assert.NotNull(c);
                var type = SmithCommissionPool.GetItemType(c.ItemKey);
                small.Add(type.Name);
                if (!ClusterFSmithTeaching.Teaches(pm, type))
                {
                    Assert.False(AnyTeaches(pm, allTypes, c.RequireExceptional), $"{type.Name} at {skill}");
                    Assert.Equal(Hardest(pm, allTypes, c.RequireExceptional), ClusterFSmithTeaching.MaxSkill(type));
                }

                guild.SmithLargeCommissions.Clear();
                var lc = SmithCommissionSystem.GenerateLarge(pm);
                Assert.NotNull(lc);
                var set = SmithCommissionSetPool.GetSet(lc.SetKey);
                var pieces = set.ItemKeys.Select(SmithCommissionPool.GetItemType).ToArray();
                large.Add(set.Key);
                if (!pieces.All(t => ClusterFSmithTeaching.Teaches(pm, t)))
                {
                    var sets = typeof(SmithCommissionSetPool).GetField("_sets",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                        .GetValue(null) as SmithCommissionSetPool.CommissionSet[];
                    var anyFull = sets!.Any(s => s.ItemKeys.Select(SmithCommissionPool.GetItemType).All(t =>
                        ClusterFSmithTeaching.CanMake(pm, t, lc.RequireExceptional) && ClusterFSmithTeaching.Teaches(pm, t)));
                    Assert.False(anyFull, $"{set.Key} at {skill}");
                }
            }

            _out.WriteLine($"commissions at {skill}: {Tally(small)}");
            _out.WriteLine($"large commissions at {skill}: {Tally(large)}");
        }
        finally
        {
            Done(account, pm);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void RegularSmithsStillGiveStockOrders()
    {
        var (account, pm) = Smith(120.0);
        try
        {
            var nonTeaching = 0;
            for (var i = 0; i < 200; i++)
            {
                var bod = SmallSmithBOD.CreateRandomFor(pm);
                if (bod != null && !ClusterFSmithTeaching.Teaches(pm, bod.Type))
                {
                    nonTeaching++;
                }

                bod?.Delete();
            }

            _out.WriteLine($"regular smith at 120: {nonTeaching} of 200 teach nothing");
            Assert.True(nonTeaching > 0);
        }
        finally
        {
            Done(account, pm);
        }
    }

    // ---------------------------------------------------------------- 5

    private static LargeSmithBOD LargePlate(BulkMaterialType mat)
    {
        var deed = new LargeSmithBOD(20, true, mat, null);
        deed.Entries = LargeBulkEntry.ConvertEntries(deed, LargeBulkEntry.LargePlate);
        return deed;
    }

    public static IEnumerable<object[]> Representative() =>
    [
        // name, deed factory, old Seals low and high (divisor 400, floor 1), old standing
        ["Iron small regular 10", (Func<Item>)(() => new SmallSmithBOD(0, 10, typeof(RingmailChest), 1025008, 0x13EC, false, BulkMaterialType.None)), 1, 1, 25],
        ["Iron small exceptional 20", (Func<Item>)(() => new SmallSmithBOD(0, 20, typeof(RingmailChest), 1025008, 0x13EC, true, BulkMaterialType.None)), 1, 1, 60],
        ["Valorite small exceptional 20", (Func<Item>)(() => new SmallSmithBOD(0, 20, typeof(PlateChest), 1025141, 0x1415, true, BulkMaterialType.Valorite)), 27, 33, 180],
        ["Large Valorite exceptional 20 (plate)", (Func<Item>)(() => LargePlate(BulkMaterialType.Valorite)), 450, 556, 310],
        ["Large Celestial exceptional 20 (plate)", (Func<Item>)(() => LargePlate(BulkMaterialType.Celestial)), 2250, 2778, 470],
    ];

    [Theory]
    [MemberData(nameof(Representative))]
    public void TurnInPaysThreeTimesTheSealsAndTwiceTheStanding(string name, Func<Item> make, int oldLow, int oldHigh, int oldStanding)
    {
        var seen = new SortedSet<int>();
        for (var i = 0; i < 40; i++)
        {
            var deed = make();
            var (seals, standing, _) = BlacksmithGuildmaster.ComputeGuildReward(deed);
            deed.Delete();

            Assert.Equal(oldStanding * 2, standing);
            Assert.Equal(0, seals % 3);
            Assert.InRange(seals, oldLow * 3, oldHigh * 3);
            seen.Add(seals);
        }

        _out.WriteLine($"{name}: Seals seen {string.Join(", ", seen)}; standing {oldStanding * 2} (was {oldStanding})");
    }
}
