// AchievementEarnedLineVerification.cs
//
// cc-P55 Parts F and G (bug-list D73, D75; Chase 2026-10-05).
//
//   F (D73): every achievement keeps its flavor text and gains a plain "(Earned: ...)" line, written from the
//      achievement's own trigger (kind and threshold) that the grant code now reads, so the number shown is the number
//      that awards it. Shown in the earn popup and the achievements list.
//   G (D75): the six skill achievements keyed above the 200 cap (Paragon 250, Grandmaster 300, Triple Grandmaster,
//      Mining, Smith and Magery Legend 300) are retired: never awarded, left out of the lists and the progress count;
//      a holder keeps them, listed under "Retired".
// Notes: shard-migration notes/cc-P55-bug-batch-5.md, Parts F and G.
//
// Facts:
//   F1. All 74 registered achievements have an Earned line written from their trigger.
//   F2. For a sample of every counted and skill trigger kind, one short of the threshold awards nothing, the threshold
//       awards it, and the Earned line carries that threshold.
//   F3. The earn popup and an earned list row both show the Earned line next to the flavor text.
//   G1. The retired set is exactly the skill achievements keyed above 200, six of them.
//   G2. None of the six is awarded at any skill: every skill at 300 Base, a +100 item bonus on top, or a staff grant.
//   G3. A holder keeps a retired achievement through a save and load; the list shows it under Retired.
//   G4. The progress count leaves the retired ones out of both numbers.

using System;
using System.Collections.Generic;
using System.Linq;
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
public class AchievementEarnedLineVerification
{
    private readonly ITestOutputHelper _out;

    private static readonly string[] RetiredKeys =
    [
        "skills.paragon", "skills.grandmaster", "skills.triple_grandmaster",
        "skills.mining_legend", "skills.smith_legend", "skills.magery_legend"
    ];

    public AchievementEarnedLineVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestHost.EnsureAccounts();
        ClusterFGuildSystem.EnsureRegistered();

        // The server's Configure defines the achievements (the test host calls none); LeagueLadderVerification may have.
        if (ClusterFAchievementSystem.Definitions.Count == 0)
        {
            ClusterFAchievementSystem.Configure();
        }
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p55a{Guid.NewGuid():N}"[..16], "p55-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;

        public Player()
        {
            Pm = new PlayerMobile { Player = true };
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(new Point3D(1480, 1750, 0), Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public bool Has(string key) => ClusterFAchievementSystem.HasEarned(Account, key);

        public void Dispose()
        {
            Pm.NetState = null;
            Ns.Mobile = null;
            ClusterFAchievementSystem.ResetForAccount(Account.Username);
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static AchievementDef Def(string key) => ClusterFAchievementSystem.Definitions[key];

    // ---------------------------------------------------------------- F1

    [Fact]
    public void EveryRegisteredAchievementHasAnEarnedLine()
    {
        var defs = ClusterFAchievementSystem.Definitions.Values.ToList();
        Assert.Equal(74, defs.Count);

        foreach (var def in defs.OrderBy(d => d.Key))
        {
            _out.WriteLine($"{def.Key}: {def.EarnedLine}");
            Assert.StartsWith("(Earned: ", def.EarnedLine);
            Assert.EndsWith(")", def.EarnedLine);
            Assert.NotEqual($"(Earned: {def.Trigger.Kind})", def.EarnedLine); // no kind without words
            Assert.False(string.IsNullOrWhiteSpace(def.FlavorText), $"{def.Key} lost its flavor text");
            if (def.ProgressCounter != null)
            {
                Assert.Equal(def.Trigger.Threshold, def.ProgressThreshold);
            }
        }
    }

    // ---------------------------------------------------------------- F2

    // One sample per counted or skill kind: (key, set the player one short, set the player at the threshold, number shown).
    public static IEnumerable<object[]> Samples() =>
    [
        ["combat.century", "100"],
        ["combat.oldhaven_mage", "25"],
        ["mining.ore_1k", "1,000"],
        ["mining.prospector", "5"],
        ["crafting.ingots_100", "100"],
        ["collection.gold_10k", "10,000"],
        ["league.veteran", "10"],
        ["skills.journeyman", "100"],
        ["skills.triple_journeyman", "100"],
        ["skills.mining_gm", "100"]
    ];

    [Theory]
    [MemberData(nameof(Samples))]
    public void ATriggerAwardsAtItsThresholdAndTheLineSaysIt(string key, string shown)
    {
        using var p = new Player();
        var def = Def(key);
        Assert.Contains(shown, def.EarnedLine);

        if (def.PrerequisiteKey != null)
        {
            ClusterFAchievementSystem.TryGrant(p.Account, def.PrerequisiteKey);
        }

        var n = def.Trigger.Threshold;
        var level = def.Trigger.Level;

        void Drive(bool atThreshold)
        {
            switch (def.Trigger.Kind)
            {
                case TriggerKind.Kills:
                    var victim = new Mobile();
                    for (var i = ClusterFAchievementSystem.GetCounter(p.Account.Username, "kills"); i < (atThreshold ? n : n - 1); i++)
                    {
                        ClusterFAchievementSystem.NotifyKill(p.Pm, victim);
                    }
                    victim.Delete();
                    break;
                case TriggerKind.OldHavenMageKills:
                    var mage = new OldHavenMage();
                    for (var i = ClusterFAchievementSystem.GetCounter(p.Account.Username, "oldhaven_mage_kills"); i < (atThreshold ? n : n - 1); i++)
                    {
                        ClusterFAchievementSystem.NotifyKill(p.Pm, mage);
                    }
                    mage.Delete();
                    break;
                case TriggerKind.ColoredOreMined:
                    ClusterFAchievementSystem.NotifyMining(p.Pm, atThreshold ? 1 : n - 1, false);
                    break;
                case TriggerKind.OreTypesDiscovered:
                    ClusterFAchievementSystem.NotifyOreDiscoveryCount(p.Pm, atThreshold ? n : n - 1);
                    break;
                case TriggerKind.IngotsSmelted:
                    ClusterFAchievementSystem.NotifyIngotsSmelted(p.Pm, atThreshold ? 1 : n - 1);
                    break;
                case TriggerKind.BankGold:
                    p.Pm.BankBox.Items.OfType<Gold>().ToList().ForEach(g => g.Delete());
                    p.Pm.BankBox.DropItem(new Gold(atThreshold ? n : n - 1));
                    ClusterFAchievementSystem.OnLogin(p.Pm);
                    break;
                case TriggerKind.Logins:
                    for (var i = ClusterFAchievementSystem.GetCounter(p.Account.Username, "login_count"); i < (atThreshold ? n : n - 1); i++)
                    {
                        ClusterFAchievementSystem.OnLogin(p.Pm);
                    }
                    break;
                case TriggerKind.AnySkill:
                    p.Pm.Skills.Tactics.Base = atThreshold ? level : level - 0.1;
                    ClusterFAchievementSystem.OnLogin(p.Pm);
                    break;
                case TriggerKind.SkillsAtLevel:
                    var skills = new[] { SkillName.Tactics, SkillName.Anatomy, SkillName.Healing, SkillName.Parry, SkillName.Focus };
                    for (var i = 0; i < (atThreshold ? n : n - 1); i++)
                    {
                        p.Pm.Skills[skills[i]].Base = level;
                    }
                    ClusterFAchievementSystem.OnLogin(p.Pm);
                    break;
                case TriggerKind.SpecificSkill:
                    p.Pm.Skills[def.Trigger.OnSkill!.Value].Base = atThreshold ? level : level - 0.1;
                    ClusterFAchievementSystem.OnLogin(p.Pm);
                    break;
                default:
                    throw new ArgumentException(def.Trigger.Kind.ToString());
            }
        }

        Drive(false);
        var shortOf = p.Has(key);
        Drive(true);
        _out.WriteLine($"{key} {def.EarnedLine}: one short {shortOf}, at the threshold {p.Has(key)}");
        Assert.False(shortOf);
        Assert.True(p.Has(key));
    }

    // ---------------------------------------------------------------- F3

    [Fact]
    public void ThePopupAndTheListShowTheEarnedLine()
    {
        using var p = new Player();
        var def = Def("league.registered_citizen");
        ClusterFAchievementSystem.TryGrant(p.Account, def.Key);

        var popup = new AchievementEarnedGump(p.Pm, def);
        var popupHtml = string.Join(" ", popup.Entries.OfType<GumpHtml>().Select(h => h.Text));
        Assert.Contains(def.FlavorText, popupHtml);
        Assert.Contains(def.EarnedLine, popupHtml);

        var list = new AchievementsGump(p.Pm, AchievementCategory.League);
        var row = list.Entries.OfType<GumpHtml>().Select(h => h.Text).Single(t => t.Contains(def.Title));
        _out.WriteLine($"popup: {popupHtml}");
        _out.WriteLine($"row: {row}");
        Assert.Contains(def.FlavorText, row);
        Assert.Contains("(Earned: registered with the League)", row);
    }

    // ---------------------------------------------------------------- G1

    [Fact]
    public void TheRetiredSetIsEverySkillAchievementAboveTheCap()
    {
        var defs = ClusterFAchievementSystem.Definitions.Values;
        var retired = defs.Where(d => d.Retired).Select(d => d.Key).OrderBy(k => k).ToList();
        var aboveCap = defs.Where(d => d.Trigger.IsSkillLevel && d.Trigger.Level > ClusterFSkillCaps.DefaultIndividualSkillCap)
            .Select(d => d.Key).OrderBy(k => k).ToList();

        _out.WriteLine($"retired: {string.Join(", ", retired)}");
        Assert.Equal(RetiredKeys.OrderBy(k => k), retired);
        Assert.Equal(aboveCap, retired);
        Assert.All(defs.Where(d => !d.Retired && d.Trigger.IsSkillLevel), d => Assert.True(d.Trigger.Level <= 200, d.Key));
    }

    // ---------------------------------------------------------------- G2

    [Fact]
    public void NoneOfTheSixIsAwardedAtAnySkill()
    {
        using var p = new Player();
        for (var i = 0; i < p.Pm.Skills.Length; i++)
        {
            p.Pm.Skills[i].Cap = 300.0;
            p.Pm.Skills[i].Base = 300.0;
        }

        p.Pm.AddSkillMod(new DefaultSkillMod(SkillName.Mining, "p55-item", true, 100.0));
        ClusterFAchievementSystem.OnLogin(p.Pm);
        ClusterFAchievementSystem.NotifySkillValue(p.Pm, 400.0);

        foreach (var key in RetiredKeys)
        {
            Assert.False(ClusterFAchievementSystem.TryGrant(p.Account, key), $"{key}: a staff grant");
            Assert.False(p.Has(key), key);
        }

        // The 200 rungs below them are still awarded.
        foreach (var key in new[] { "skills.master", "skills.triple_master", "skills.mining_elite", "skills.smith_elite", "skills.magery_elite" })
        {
            Assert.True(p.Has(key), key);
        }

        _out.WriteLine($"Mining Value {p.Pm.Skills.Mining.Value}: none of the six; the five 200 rungs earned");
    }

    // ---------------------------------------------------------------- G3, G4

    [Fact]
    public void AHolderKeepsARetiredAchievementAndTheCountLeavesItOut()
    {
        using var p = new Player();
        var (earned, counters) = ClusterFAchievementSystem.GetForSave();
        var savedEarned = earned.ToDictionary(kv => kv.Key, kv => new HashSet<string>(kv.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        var savedCounters = counters.ToDictionary(kv => kv.Key, kv => new Dictionary<string, int>(kv.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);

        try
        {
            // Earned before retirement: as a save holds it.
            var mine = new Dictionary<string, HashSet<string>>(savedEarned, StringComparer.OrdinalIgnoreCase)
            {
                [p.Account.Username] = new(StringComparer.OrdinalIgnoreCase) { "skills.grandmaster", "skills.master", "exploration.citizen" }
            };
            ClusterFAchievementSystem.LoadFromSave(mine, savedCounters);

            var store = new ClusterFAchievementPersistence(World.NewItem);
            var buffer = new byte[1 << 20];
            var writer = new BufferWriter(buffer, true);
            store.Serialize(writer);
            writer.Flush();
            ClusterFAchievementSystem.ResetForAccount(p.Account.Username);
            Assert.False(p.Has("skills.grandmaster"));

            var loaded = new ClusterFAchievementPersistence(store.Serial);
            loaded.Deserialize(new BufferReader(buffer));
            Assert.True(p.Has("skills.grandmaster"));

            var (count, total) = ClusterFAchievementSystem.ProgressCount(p.Account.Username);
            _out.WriteLine($"holder: earned {count} of {total} (retired held, not counted)");
            Assert.Equal(2, count);
            Assert.Equal(74 - RetiredKeys.Length, total);

            // Every page of the Skills tab (five rows a page; the Retired section is last).
            var pages = Enumerable.Range(0, 20).Select(i => new AchievementsGump(p.Pm, AchievementCategory.Skills, false, i)).ToList();
            var labels = pages.SelectMany(g => g.Entries.OfType<GumpLabel>()).Select(l => l.Text).ToList();
            var html = pages.SelectMany(g => g.Entries.OfType<GumpHtml>()).Select(h => h.Text).ToList();
            Assert.Contains(AchievementsGump.RetiredHeader, labels);
            Assert.Contains(html, h => h.Contains(Def("skills.grandmaster").Title));
            Assert.Contains($"Renown: 0   {count}/{total}", labels);

            // Someone who does not hold it never sees it.
            using var other = new Player();
            var otherPages = Enumerable.Range(0, 20).Select(i => new AchievementsGump(other.Pm, AchievementCategory.Skills, false, i)).ToList();
            Assert.DoesNotContain(AchievementsGump.RetiredHeader, otherPages.SelectMany(g => g.Entries.OfType<GumpLabel>()).Select(l => l.Text));
            Assert.DoesNotContain(otherPages.SelectMany(g => g.Entries.OfType<GumpHtml>()), h => RetiredKeys.Any(k => h.Text.Contains(Def(k).Title)));
            loaded.Delete();
            store.Delete();
        }
        finally
        {
            ClusterFAchievementSystem.LoadFromSave(savedEarned, savedCounters);
        }
    }
}
