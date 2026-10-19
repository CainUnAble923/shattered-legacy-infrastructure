// SmithOrdersTeachHighSkillVerification.cs
//
// cc-P170 (Chase, 2026-10-18 13:36, test shard): "im gettin iron plate tunic with 144 smithing when asking for BODs and
// training turned on". At 144 Blacksmithy no stock metal teaches (every BOD item's maximum is below it), but Blaze does
// (requires 137.5, gain ceiling Frost's 150, ClusterFMetalTiers). With "Orders that still teach me" on, the order's metal
// was still rolled stock first (half the weight is iron, SmallSmithBOD.m_BlacksmithMaterialChances), and the teaching
// metal was only tried inside the 30% post-Valorite gate (15% for a large deed); when that gate missed, nothing taught and
// the fallback handed out the hardest item in the stock metal: an iron plate tunic.
//
// Facts:
//   1. A Society member at 130, 144 (with and without a held Hammer of Hephaestus, and with a +10 bonus) and 160, setting
//      on: every order teaches in its own metal, 100 rolls on each path (SmallSmithBOD.CreateRandomFor(m, true),
//      BlacksmithGuildmaster.TryCreateSmallBOD, TryCreateBOD, TryCreateLargeBOD). At 144 the metal is Blaze.
//   2. Setting off: the stock pick, which at 144 includes orders that teach nothing (iron among them).
//   3. Regular smiths (the one-argument CreateRandomFor and new LargeSmithBOD()) are stock at 144 (PT-11).
//   4. Large at 144, on: ring, chain or plate, in Blaze, every piece teaching.

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
public class SmithOrdersTeachHighSkillVerification
{
    private const int Rolls = 100;

    private readonly ITestOutputHelper _out;

    public SmithOrdersTeachHighSkillVerification(ITestOutputHelper output)
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

        if (DefBlacksmithy.CraftSystem == null)
        {
            DefBlacksmithy.Initialize();
        }

        BlacksmithyCraftRegistrations.Register(); // idempotent
    }

    private static bool _startupHooksRun;

    // bonus: "hammer" holds a Hammer of Hephaestus (+5 while held), "+10" adds a +10 skill mod, "" nothing.
    private static (Account, PlayerMobile) Smith(double skill, string bonus = "", bool teaching = true)
    {
        var account = new Account($"p170{Guid.NewGuid():N}"[..16], "p170-test-only");
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.Skills.Blacksmith.Cap = 300.0;
        pm.Skills.Blacksmith.Base = skill;
        account[0] = pm;
        pm.MoveToWorld(new Point3D(1560, 1560, 0), Map.Trammel);
        ClusterFAccountPersistence.GetOrCreate(account).GetOrCreateGuildData(pm.Serial).JoinedGuilds.Add("smithing");
        ClusterFSmithTeaching.SetWantsTeaching(pm, teaching);

        if (bonus == "hammer")
        {
            Assert.True(pm.EquipItem(new HammerOfHephaestus()));
            Assert.Equal(skill + 5.0, pm.Skills.Blacksmith.Value, 3);
        }
        else if (bonus == "+10")
        {
            pm.AddSkillMod(new DefaultSkillMod(SkillName.Blacksmith, "p170", true, 10.0));
            Assert.Equal(skill + 10.0, pm.Skills.Blacksmith.Value, 3);
        }

        return (account, pm);
    }

    private static void Done(Account account, PlayerMobile pm)
    {
        ClusterFAccountPersistence.Get(account)?.ClearGuildData();
        pm.Delete();
        Accounts.Remove(account);
    }

    private static string Name(Item deed) => deed switch
    {
        SmallSmithBOD s => $"{s.Material} {s.Type.Name}",
        LargeSmithBOD l => $"{l.Material} large {l.Entries[0].Details.Type.Name}...",
        _               => deed?.GetType().Name ?? "none"
    };

    private static bool OrderTeaches(Mobile m, Item deed) => deed switch
    {
        SmallSmithBOD s => ClusterFSmithTeaching.Teaches(m, s.Type, ClusterFMetalTiers.GainCeiling(s.Material)),
        LargeSmithBOD l => l.Entries.All(e =>
            ClusterFSmithTeaching.Teaches(m, e.Details.Type, ClusterFMetalTiers.GainCeiling(l.Material))),
        _ => false
    };

    private static string Tally(IEnumerable<string> names) =>
        string.Join(", ", names.GroupBy(n => n).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} x{g.Count()}"));

    public static IEnumerable<object[]> Paths() =>
    [
        ["CreateRandomFor(m, true)", (Func<PlayerMobile, Item>)(pm => SmallSmithBOD.CreateRandomFor(pm, true))],
        ["TryCreateSmallBOD", (Func<PlayerMobile, Item>)BlacksmithGuildmaster.TryCreateSmallBOD],
        ["TryCreateBOD", (Func<PlayerMobile, Item>)BlacksmithGuildmaster.TryCreateBOD],
        ["TryCreateLargeBOD", (Func<PlayerMobile, Item>)BlacksmithGuildmaster.TryCreateLargeBOD],
    ];

    public static IEnumerable<object[]> Cases() =>
        from skill in new[] { 130.0, 144.0, 160.0 }
        from bonus in skill == 144.0 ? new[] { "", "hammer", "+10" } : new[] { "" }
        from path in Paths()
        select new[] { skill, bonus, path[0], path[1] };

    // ---------------------------------------------------------------- 1

    [Theory]
    [MemberData(nameof(Cases))]
    public void EverySocietyOrderTeachesAtHighSkill(double skill, string bonus, string path, Func<PlayerMobile, Item> make)
    {
        var (account, pm) = Smith(skill, bonus);
        try
        {
            var names = new List<string>();
            var wrong = new List<string>();
            for (var i = 0; i < Rolls; i++)
            {
                var deed = make(pm);
                Assert.NotNull(deed);
                names.Add(Name(deed));
                if (!OrderTeaches(pm, deed))
                {
                    wrong.Add(Name(deed));
                }

                deed.Delete();
            }

            _out.WriteLine($"{path} at {skill}{bonus}: {Tally(names)}");
            Assert.True(wrong.Count == 0, $"{wrong.Count} of {Rolls} teach nothing: {Tally(wrong)}");

            if (skill == 144.0)
            {
                Assert.All(names, n => Assert.StartsWith("Blaze ", n));
            }
        }
        finally
        {
            Done(account, pm);
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void SettingOffIsTheStockPick()
    {
        var (account, pm) = Smith(144.0, teaching: false);
        try
        {
            var names = new List<string>();
            var nonTeaching = 0;
            var iron = 0;
            for (var i = 0; i < Rolls; i++)
            {
                var deed = BlacksmithGuildmaster.TryCreateSmallBOD(pm) as SmallSmithBOD;
                Assert.NotNull(deed);
                names.Add(Name(deed));
                nonTeaching += OrderTeaches(pm, deed) ? 0 : 1;
                iron += deed.Material == BulkMaterialType.None ? 1 : 0;
                deed.Delete();
            }

            _out.WriteLine($"off at 144: {Tally(names)}");
            Assert.True(nonTeaching > 0);
            Assert.True(iron > 0);
        }
        finally
        {
            Done(account, pm);
        }
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void RegularSmithOrdersStayStock()
    {
        var (account, pm) = Smith(144.0);
        try
        {
            var names = new List<string>();
            var nonTeaching = 0;
            for (var i = 0; i < Rolls; i++)
            {
                var small = SmallSmithBOD.CreateRandomFor(pm);
                Assert.NotNull(small);
                names.Add(Name(small));
                nonTeaching += OrderTeaches(pm, small) ? 0 : 1;
                small.Delete();

                var large = new LargeSmithBOD();
                nonTeaching += OrderTeaches(pm, large) ? 0 : 1;
                large.Delete();
            }

            _out.WriteLine($"regular smith at 144: {Tally(names)}; {nonTeaching} of {2 * Rolls} teach nothing");
            Assert.True(nonTeaching > Rolls);
        }
        finally
        {
            Done(account, pm);
        }
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void LargeOrderAt144IsArmorInBlaze()
    {
        var (account, pm) = Smith(144.0);
        try
        {
            var armorSets = new[] { 0, 1, 2 }.Select(s => LargeSmithBOD.SetTypes(s)[0]).ToArray();
            var names = new List<string>();
            for (var i = 0; i < Rolls; i++)
            {
                var deed = LargeSmithBOD.CreateRandomFor(pm);
                Assert.NotNull(deed);
                names.Add(Name(deed));
                Assert.Equal(BulkMaterialType.Blaze, deed.Material);
                Assert.Contains(deed.Entries[0].Details.Type, armorSets);
                Assert.True(OrderTeaches(pm, deed), Name(deed));
                deed.Delete();
            }

            _out.WriteLine($"large at 144: {Tally(names)}");
        }
        finally
        {
            Done(account, pm);
        }
    }
}
