// CreatureLevelsVerification.cs
//
// cc-P239 (F-14): creature levels from OSI's Bard difficulty, con colors against the player, and Item Identification's
// new job, Appraisal. Notes: shard-migration notes/cc-P239-creature-levels-appraisal.md.
//
// Facts:
//   1. The report covers every creature type (pinned and ours), and ranks a rat below a dragon below an ancient wyrm.
//   2. Below pinned's 160 cap the uncapped difficulty is pinned's GetBaseDifficulty exactly, for every type (no drift).
//   3. An override beats the computed level: the type table, a champion, a peerless boss, an invulnerable NPC.
//   4. Con color bands sit at fixed level gaps.
//   5. The con line is its own message: the name keeps its notoriety hue on a red and a blue mobile.
//   6. The provisional player level is the best combat skill, behind a setting, and F-12 can replace its source.
//   7. Appraisal shows each step's detail at 0, 30, 60 and 90 and nothing from the next step; the stat card opens at 90.
//   8. Through the skill's own target: a creature is appraised, a player is stock, and identification still works on items.
//   9. The skill is called Appraisal, and it gains past 100 on creatures near its level, not on items.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server;
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
public class CreatureLevelsVerification
{
    private static readonly Point3D Spot = new(3503, 2574, 14);
    private readonly ITestOutputHelper _out;

    public CreatureLevelsVerification(ITestOutputHelper output) => _out = output;

    private static PlayerMobile NewPlayer(params (SkillName skill, double value)[] skills)
    {
        // A real character is a Player (CharacterCreation sets it); the con line and the gain hook ask for it.
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        foreach (var (skill, value) in skills)
        {
            pm.Skills[skill].Cap = 200.0;
            pm.Skills[skill].Base = value;
        }

        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private static T Place<T>(T bc) where T : BaseCreature
    {
        bc.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        return bc;
    }

    private static PlayerMobile Online(PlayerMobile pm, out NetState ns)
    {
        ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        if (Server.Accounting.Security.AccountSecurity.CurrentAlgorithm == Server.Accounting.Security.PasswordProtectionAlgorithm.None)
        {
            Server.Accounting.Security.AccountSecurity.CurrentAlgorithm = Server.Accounting.Security.PasswordProtectionAlgorithm.PBKDF2;
        }

        ns.Account = new Server.Accounting.Account($"p239-{pm.Serial.Value:X8}", "p239-test-only");
        return pm;
    }

    private static int Mark(NetState ns) => ns.SendBuffer.GetReadSpan().Length;

    // Every ASCII (0x1C) and Unicode (0xAE) text message sent on `ns` since `from`: speaker, hue, text.
    private static List<(uint serial, int hue, string text)> Texts(NetState ns, int from)
    {
        var span = ns.SendBuffer.GetReadSpan();
        var found = new List<(uint, int, string)>();
        for (var i = from; i + 3 <= span.Length;)
        {
            int id = span[i];
            var len = (span[i + 1] << 8) | span[i + 2];
            if ((id == 0x1C || id == 0xAE) && len >= 44 && i + len <= span.Length)
            {
                var serial = (uint)((span[i + 3] << 24) | (span[i + 4] << 16) | (span[i + 5] << 8) | span[i + 6]);
                var hue = (span[i + 10] << 8) | span[i + 11];
                string text;
                if (id == 0x1C)
                {
                    text = Encoding.ASCII.GetString(span.Slice(i + 44, len - 44)).TrimEnd('\0');
                }
                else
                {
                    text = Encoding.BigEndianUnicode.GetString(span.Slice(i + 48, len - 48)).TrimEnd('\0');
                }

                found.Add((serial, hue, text));
                i += len;
                continue;
            }

            i++;
        }

        return found;
    }

    private static void Cleanup(params Mobile[] ms)
    {
        foreach (var m in ms)
        {
            if (m is PlayerMobile pm)
            {
                pm.NetState = null;
            }

            m?.Delete();
        }
    }

    // 1.
    [Fact]
    public void TheReportCoversEveryCreatureTypeAndRanksThemSensibly()
    {
        var types = (AssemblyHandler.Assemblies ?? new[] { typeof(BaseCreature).Assembly })
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(BaseCreature).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
            .Distinct()
            .ToHashSet();

        var rows = ClusterFCreatureLevels.BuildReport(out var failures);
        _out.WriteLine($"{types.Count} types, {rows.Count} rows, {failures.Count} failures: {string.Join("; ", failures)}");

        Assert.True(types.Count > 900, $"the probe found {types.Count} types");
        Assert.Empty(failures);
        Assert.Equal(types.Count, rows.Count);
        Assert.True(types.SetEquals(rows.Select(r => r.Type)), "every creature type has exactly one row");
        Assert.Contains(rows, r => r.Type == typeof(TheGloomQueen)); // ours (cc-P140)

        int LevelOf(Type t) => rows.Single(r => r.Type == t).Level;
        _out.WriteLine($"rat {LevelOf(typeof(Rat))}, dragon {LevelOf(typeof(Dragon))}, ancient wyrm {LevelOf(typeof(AncientWyrm))}");
        Assert.True(LevelOf(typeof(Rat)) < LevelOf(typeof(Dragon)), "a rat below a dragon");
        Assert.True(LevelOf(typeof(Dragon)) < LevelOf(typeof(AncientWyrm)), "a dragon below an ancient wyrm");
        Assert.All(rows, r => Assert.InRange(r.Level, ClusterFCreatureLevels.MinLevel, ClusterFCreatureLevels.MaxLevel));
    }

    // 2. Some creatures roll their HitPoison on every read (a speckled scorpion, a giant serpent), so pinned's own value
    //    moves between two calls on one creature. Each creature is read 12 times both ways: every uncapped value is one
    //    pinned also gives, below the cap.
    [Fact]
    public void BelowPinnedsCapTheUncappedDifficultyIsPinnedsExactly()
    {
        var below = 0;
        var moving = new List<string>();
        foreach (var t in ClusterFCreatureLevels.CreatureTypes())
        {
            var bc = (BaseCreature)Activator.CreateInstance(t);
            try
            {
                var pinned = new HashSet<double>();
                var ours = new HashSet<double>();
                // Both reads roll (a Giant Serpent moves between 74.38 and 76.38), so pinned is read far more often
                // than ours: twelve reads of each sometimes missed a value pinned can give, and the test failed (cc-P355).
                for (var i = 0; i < 12; i++)
                {
                    ours.Add(ClusterFCreatureLevels.UncappedDifficulty(bc));
                }

                for (var i = 0; i < 12 || i < 400 && !ours.IsSubsetOf(pinned); i++)
                {
                    pinned.Add(BaseInstrument.GetBaseDifficulty(bc));
                }

                if (pinned.Max() >= 160.0)
                {
                    Assert.True(ours.Max() >= 160.0, $"{t.Name}: uncapped {ours.Max()} under pinned's cap {pinned.Max()}");
                    continue;
                }

                below++;
                if (pinned.Count > 1)
                {
                    moving.Add($"{t.Name} ({string.Join(", ", pinned.OrderBy(d => d))})");
                }

                Assert.True(ours.IsSubsetOf(pinned), $"{t.Name}: {string.Join(", ", ours)} against pinned {string.Join(", ", pinned)}");
            }
            finally
            {
                bc.Delete();
            }
        }

        _out.WriteLine($"{below} types below the cap; their value moves between reads for {moving.Count}: {string.Join("; ", moving)}");
        Assert.True(below > 800, $"{below} types below the cap");
    }

    // 3.
    [Fact]
    public void AnOverrideBeatsTheComputedLevel()
    {
        var rat = new Rat();
        var champion = new Barracoon();
        var peerless = new CrimsonDragon(); // ours, a BasePeerless
        var healer = new Healer(); // invulnerable always (pinned Healer.cs); a vendor's depends on vendor.isInvulnerable
        try
        {
            var computed = ClusterFCreatureLevels.ComputedLevel(rat);
            ClusterFCreatureLevels.TypeOverrides[typeof(Rat)] = 150;
            var level = ClusterFCreatureLevels.Level(rat, out var why);
            ClusterFCreatureLevels.TypeOverrides.Remove(typeof(Rat));
            _out.WriteLine($"rat computed {computed}, overridden {level} ({why})");
            Assert.NotEqual(150, computed);
            Assert.Equal(150, level);
            Assert.Equal(computed, ClusterFCreatureLevels.Level(rat));

            Assert.Equal(200, ClusterFCreatureLevels.Level(champion, out why));
            _out.WriteLine($"Barracoon: {why}");
            Assert.Equal(190, ClusterFCreatureLevels.Level(peerless, out why));
            Assert.NotEqual(190, ClusterFCreatureLevels.ComputedLevel(peerless));
            _out.WriteLine($"CrimsonDragon: computed {ClusterFCreatureLevels.ComputedLevel(peerless)}, {why}");

            Assert.True(healer.IsInvulnerable);
            Assert.Equal(1, ClusterFCreatureLevels.Level(healer, out why));
            Assert.NotEqual(1, ClusterFCreatureLevels.ComputedLevel(healer));
            _out.WriteLine($"Healer: computed {ClusterFCreatureLevels.ComputedLevel(healer)}, {why}");
        }
        finally
        {
            ClusterFCreatureLevels.TypeOverrides.Remove(typeof(Rat));
            Cleanup(rat, champion, peerless, healer);
        }
    }

    // 4.
    [Theory]
    [InlineData(30, ConColor.Grey)]
    [InlineData(31, ConColor.Green)]
    [InlineData(40, ConColor.Green)]
    [InlineData(41, ConColor.Blue)]
    [InlineData(47, ConColor.Blue)]
    [InlineData(48, ConColor.Yellow)]
    [InlineData(50, ConColor.Yellow)]
    [InlineData(52, ConColor.Yellow)]
    [InlineData(53, ConColor.Orange)]
    [InlineData(59, ConColor.Orange)]
    [InlineData(60, ConColor.Red)]
    [InlineData(69, ConColor.Red)]
    [InlineData(70, ConColor.Skull)]
    [InlineData(200, ConColor.Skull)]
    public void ConColorBandsSitAtFixedLevelGaps(int creature, ConColor expected)
    {
        Assert.Equal(expected, ClusterFCreatureLevels.Con(50, creature));
        // The same gap anywhere on the ladder.
        Assert.Equal(expected, ClusterFCreatureLevels.Con(150, creature + 100));
    }

    [Fact]
    public void EachBandHasItsOwnHueAndWord()
    {
        var bands = Enum.GetValues<ConColor>();
        Assert.Equal(7, bands.Length);
        Assert.Equal(7, bands.Select(ClusterFCreatureLevels.Hue).Distinct().Count());
        Assert.Equal(7, bands.Select(ClusterFCreatureLevels.Word).Distinct().Count());
        Assert.All(bands, b => Assert.False(string.IsNullOrWhiteSpace(ClusterFCreatureLevels.Word(b))));
    }

    // 5.
    [Fact]
    public void TheConLineIsItsOwnMessageAndTheNameKeepsItsNotorietyHue()
    {
        // Pinned's real notoriety, for this fact only: Initialize also sets the harmful and beneficial handlers, which
        // other facts run without (a thrown explosion potion in JokeAchievementsVerification), so all of it is put back.
        var oldHandler = Notoriety.Handler;
        var oldHues = (int[])Notoriety.Hues.Clone();
        var oldHarmful = Mobile.AllowHarmfulHandler;
        var oldBeneficial = Mobile.AllowBeneficialHandler;
        NotorietyHandlers.Initialize();
        var pm = Online(NewPlayer((SkillName.Swords, 50.0)), out var ns);
        var red = Place(new EvilMage());
        var blue = Place(new Banker());
        try
        {
            foreach (var (m, expected) in new (BaseCreature, int)[] { (red, Notoriety.Murderer), (blue, Notoriety.Innocent) })
            {
                var noto = Notoriety.Compute(pm, m);
                Assert.Equal(expected, noto);
                var before = Mark(ns);
                m.OnSingleClick(pm);
                var said = Texts(ns, before).Where(t => t.serial == m.Serial.Value).ToList();
                _out.WriteLine($"{m.GetType().Name} ({noto}): {string.Join(" | ", said.Select(s => $"0x{s.hue:X} '{s.text}'"))}");

                var name = said.Single(s => s.text.Contains(m.Name));
                Assert.Equal(Notoriety.GetHue(noto), name.hue);

                var con = ClusterFCreatureLevels.Con(pm, m);
                var conLine = said.Single(s => s.text == ClusterFCreatureLevels.Word(con));
                Assert.Equal(ClusterFCreatureLevels.Hue(con), conLine.hue);
                Assert.True(said.IndexOf(name) < said.IndexOf(conLine), "the name first, then the con line");
            }

            // Switched off, the name alone.
            ClusterFCreatureLevels.ConLineEnabled = false;
            var mark = Mark(ns);
            red.OnSingleClick(pm);
            Assert.Single(Texts(ns, mark), t => t.serial == red.Serial.Value);
        }
        finally
        {
            ClusterFCreatureLevels.ConLineEnabled = true;
            Cleanup(pm, red, blue);
            Notoriety.Handler = oldHandler;
            Array.Copy(oldHues, Notoriety.Hues, oldHues.Length);
            Mobile.AllowHarmfulHandler = oldHarmful;
            Mobile.AllowBeneficialHandler = oldBeneficial;
        }
    }

    // 6.
    [Fact]
    public void TheProvisionalPlayerLevelIsTheBestCombatSkillBehindASettingAndItsSourceCanBeReplaced()
    {
        var fighter = NewPlayer((SkillName.Swords, 80.0), (SkillName.Magery, 95.4), (SkillName.Cooking, 120.0));
        var cook = NewPlayer((SkillName.Cooking, 120.0));
        var old = ClusterFCreatureLevels.PlayerLevels;
        try
        {
            Assert.Equal(95, ClusterFCreatureLevels.PlayerLevel(fighter));
            Assert.Equal(1, ClusterFCreatureLevels.PlayerLevel(cook));

            ClusterFCreatureLevels.ProvisionalPlayerLevelEnabled = false;
            ClusterFCreatureLevels.FixedPlayerLevel = 50;
            Assert.Equal(50, ClusterFCreatureLevels.PlayerLevel(fighter));
            ClusterFCreatureLevels.ProvisionalPlayerLevelEnabled = true;

            // F-12's build replaces the source; the con color reads it with no change to its callers.
            ClusterFCreatureLevels.PlayerLevels = new FixedLevel(120);
            var drake = new Drake();
            try
            {
                Assert.Equal(120, ClusterFCreatureLevels.PlayerLevel(fighter));
                Assert.Equal(ClusterFCreatureLevels.Con(120, ClusterFCreatureLevels.Level(drake)), ClusterFCreatureLevels.Con(fighter, drake));
            }
            finally
            {
                drake.Delete();
            }
        }
        finally
        {
            ClusterFCreatureLevels.PlayerLevels = old;
            ClusterFCreatureLevels.ProvisionalPlayerLevelEnabled = true;
            Cleanup(fighter, cook);
        }
    }

    private sealed class FixedLevel : IPlayerLevelSource
    {
        private readonly int _level;
        public FixedLevel(int level) => _level = level;
        public int LevelOf(Mobile player) => _level;
    }

    // 7.
    [Theory]
    [InlineData(0.0)]
    [InlineData(30.0)]
    [InlineData(60.0)]
    [InlineData(90.0)]
    public void AppraisalShowsEachStepsDetailAndNothingFromTheNext(double skill)
    {
        var pm = Online(NewPlayer((SkillName.ItemID, skill), (SkillName.Swords, 50.0)), out _);
        var troll = Place(new Troll());
        try
        {
            var lines = ClusterFAppraisal.Describe(pm, troll, skill);
            var all = string.Join(" | ", lines);
            _out.WriteLine($"{skill}: {all}");
            var level = ClusterFCreatureLevels.Level(troll);

            Assert.NotEmpty(lines); // the vague line, always
            Assert.Equal(skill >= 30.0, all.Contains($"Level {level}"));
            Assert.Equal(skill >= 60.0, all.Contains($"Hits {troll.Hits}/{troll.HitsMax}"));
            Assert.Equal(skill >= 60.0, all.Contains($"Str {troll.Str}"));
            Assert.Equal(skill >= 60.0, all.Contains("Resists"));

            ClusterFAppraisal.TryAppraise(pm, troll);
            Assert.Equal(skill >= 90.0, pm.HasGump<AppraisalCardGump>());
        }
        finally
        {
            Cleanup(pm, troll);
        }
    }

    [Fact]
    public void TheStatCardShowsTheWholeCard()
    {
        var pm = Online(NewPlayer((SkillName.ItemID, 95.0)), out var ns);
        var dragon = Place(new Dragon());
        try
        {
            Assert.True(ClusterFAppraisal.TryAppraise(pm, dragon));
            var card = pm.GetGumps().Find<AppraisalCardGump>();
            Assert.NotNull(card);
            Assert.Same(dragon, card.Creature);
            var text = string.Join(" | ", card.Lines);
            _out.WriteLine(text);
            foreach (var want in new[] { $"Level {ClusterFCreatureLevels.Level(dragon)}", $"Hits {dragon.HitsMax}", "Str", "Physical", "Fire", "Breathes fire", "Magery" })
            {
                Assert.True(text.Contains(want), $"the card shows '{want}': {text}");
            }
        }
        finally
        {
            Cleanup(pm, dragon);
        }
    }

    // 8.
    [Fact]
    public void ThroughTheSkillsTargetACreatureIsAppraisedAPlayerIsStockAndItemsAreStillIdentified()
    {
        ClusterFAppraisal.Initialize();
        var pm = Online(NewPlayer((SkillName.ItemID, 100.0)), out var ns);
        var other = NewPlayer();
        var troll = Place(new Troll());
        var sword = new Longsword { Identified = false };
        pm.Backpack.DropItem(sword);
        try
        {
            ItemIdentification.OnUse(pm);
            var before = Mark(ns);
            pm.Target.Invoke(pm, troll);
            var said = Texts(ns, before);
            _out.WriteLine("troll: " + string.Join(" | ", said.Select(s => s.text)));
            Assert.Contains(said, s => s.text.Contains($"Level {ClusterFCreatureLevels.Level(troll)}"));

            ItemIdentification.OnUse(pm);
            before = Mark(ns);
            pm.Target.Invoke(pm, other);
            said = Texts(ns, before);
            _out.WriteLine("player: " + string.Join(" | ", said.Select(s => s.text)));
            Assert.DoesNotContain(said, s => s.text.StartsWith("Level ", StringComparison.Ordinal));

            ItemIdentification.OnUse(pm);
            pm.Target.Invoke(pm, sword);
            Assert.True(sword.Identified, "item identification still identifies an item");
        }
        finally
        {
            Cleanup(pm, other, troll);
        }
    }

    // 9.
    [Fact]
    public void TheSkillIsCalledAppraisalAndGainsPastOneHundredOnCreaturesNearItsLevel()
    {
        ClusterFAppraisal.Initialize();
        Assert.Equal("Appraisal", SkillInfo.Table[(int)SkillName.ItemID].Name);

        var pm = NewPlayer((SkillName.ItemID, 120.0));
        var near = new Dragon();      // about level 117
        var far = new Rat();
        var sword = new Longsword();
        try
        {
            var skill = pm.Skills[SkillName.ItemID];
            var onNear = ClusterFSkillGain.PastStockCeilingChance(pm, skill, ClusterFSkillGain.CheckKind.Target, near, 0.0, 100.0);
            var onFar = ClusterFSkillGain.PastStockCeilingChance(pm, skill, ClusterFSkillGain.CheckKind.Target, far, 0.0, 100.0);
            var onItem = ClusterFSkillGain.PastStockCeilingChance(pm, skill, ClusterFSkillGain.CheckKind.Target, sword, 0.0, 100.0);
            _out.WriteLine($"dragon {ClusterFCreatureLevels.Level(near)}: {onNear}; rat: {onFar}; sword: {onItem}");
            Assert.False(double.IsNaN(onNear));
            Assert.True(onNear > 0.0);
            Assert.True(double.IsNaN(onFar) || onFar <= 0.0, "a rat teaches a 120 appraiser nothing");
            Assert.True(double.IsNaN(onItem), "items stay stock");
        }
        finally
        {
            Cleanup(pm, near, far);
            sword.Delete();
        }
    }
}
