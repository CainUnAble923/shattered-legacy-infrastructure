// SkillGainRowKeysVerification.cs
//
// cc-P66 Part B. Hook G's rows (customizations ClusterFSkillGain.TryRow) are keyed on the literal windows pinned's uses
// pass: Hiding (0, 100), Musicianship (0, 120) and so on. An upstream bump that changed one of those numbers would leave a
// row matching nothing, or matching a different use, with every other fact still green. So this fact reads the build tree
// (the test runs inside it, docker/uo/build.sh) and asserts each keyed line, word for word, and how many times its key
// appears in all of Projects/UOContent: a new use with the same key would join the row silently, so it stops the build
// too. Counts measured on pinned d4531cd94 with grep -rF, customizations included (they are mirrored into the same tree).

using System;
using System.IO;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class SkillGainRowKeysVerification
{
    private readonly ITestOutputHelper _out;

    public SkillGainRowKeysVerification(ITestOutputHelper output) => _out = output;

    private static string UOContent()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Projects", "UOContent")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir != null, $"no Projects/UOContent above {AppContext.BaseDirectory}");
        return Path.Combine(dir!.FullName, "Projects", "UOContent");
    }

    // (file under Projects/UOContent, the exact text, times in that file, times in all of Projects/UOContent)
    public static TheoryData<string, string, int, int> Keys() => new()
    {
        // Hiding (0, 100); (-100, 0) in a friend's house stays stock.
        { "Skills/Hiding.cs", "m.CheckSkill(SkillName.Hiding, 0.0 - bonus, 100.0 - bonus)", 1, 1 },
        { "Skills/Hiding.cs", "bonus = 100.0;", 1, 1 },
        // Stealth (-20 + 2 AR, 60 + 2 AR), AR below 42.
        { "Skills/Stealth.cs", "-20.0 + armorRating * 2,", 1, 1 },
        { "Skills/Stealth.cs", "(Core.AOS ? 60.0 : 80.0) + armorRating * 2", 1, 1 },
        { "Skills/Stealth.cs", "if (armorRating >= (Core.AOS ? 42 : 26))", 1, 1 },
        // Detect Hidden's two (0, 100) scan rolls (not rows: the found-something call is) and the faction trap's (80, 100).
        { "Skills/DetectHidden.cs", "src.CheckSkill(SkillName.DetectHidden, 0.0, 100.0)", 2, 2 },
        { "Skills/DetectHidden.cs", "ClusterFSkillGain.DetectHiddenFound(src);", 1, 1 },
        { "Skills/Snooping.cs", "from.CheckTargetSkill(SkillName.Snooping, cont, 0.0, 100.0)", 1, 1 },
        // Poisoning: the potion's window. Greater (60, 100); Deadly, Darkglow, Parasitic (95, 100).
        { "Skills/Poisoning.cs", "m_From.CheckTargetSkill(SkillName.Poisoning, m_Target, m_MinSkill, m_MaxSkill)", 1, 1 },
        { "Items/Skill Items/Magical/Potions/Poison Potions/GreaterPoisonPotion.cs", "MinPoisoningSkill => 60.0;", 1, 1 },
        { "Items/Skill Items/Magical/Potions/Poison Potions/DeadlyPoisonPotion.cs", "MinPoisoningSkill => 95.0;", 1, 3 },
        { "Items/Skill Items/Magical/Potions/Poison Potions/DarkglowPotion.cs", "MinPoisoningSkill => 95.0;", 1, 3 },
        { "Items/Skill Items/Magical/Potions/Poison Potions/ParasiticPotion.cs", "MinPoisoningSkill => 95.0;", 1, 3 },
        { "Items/Skill Items/Magical/Potions/Poison Potions/GreaterPoisonPotion.cs", "MaxPoisoningSkill => 100.0;", 1, 4 },
        // Healing and Veterinary (0, 120), the bandage's primary skill.
        { "Items/Skill Items/Misc/Bandage.cs", "Healer.CheckSkill(primarySkill, 0.0, 120.0);", 1, 1 },
        { "Items/Skill Items/Misc/Bandage.cs", "Healer.CheckSkill(secondarySkill, 0.0, 120.0);", 1, 1 },
        { "Skills/Begging.cs", "_from.CheckTargetSkill(SkillName.Begging, _target, 0.0, 100.0)", 1, 1 },
        { "Skills/ForensicEval.cs", "from.CheckTargetSkill(SkillName.Forensics, target, 40.0, 100.0)", 1, 1 },
        { "Skills/ForensicEval.cs", "from.CheckTargetSkill(SkillName.Forensics, c, 0.0, 100.0)", 1, 1 },
        // Animal Lore (0, 120) on a creature; past 120 the creature's tame window (Taming's, no owners).
        { "Skills/AnimalLore.cs", "from.CheckTargetSkill(SkillName.AnimalLore, c, 0.0, 120.0)", 1, 1 },
        { "Skills/AnimalTaming.cs", "m_Tamer.CheckTargetSkill(SkillName.AnimalLore, m_Creature, 0.0, 120.0);", 2, 2 },
        { "Skills/AnimalTaming.cs", "var minSkill = m_Creature.MinTameSkill + m_Creature.Owners.Count * 6.0;", 1, 1 },
        { "Skills/AnimalTaming.cs", "minSkill += 24.9;", 1, 1 },
        {
            "Skills/AnimalTaming.cs",
            "m_Tamer.CheckTargetSkill(SkillName.AnimalTaming, m_Creature, minSkill - 25.0, minSkill + 25.0)", 1, 1
        },
        { "Items/Skill Items/Musical Instruments/BaseInstrument.cs", "m.CheckSkill(SkillName.Musicianship, 0.0, 120.0);", 1, 1 },
        { "Skills/Tracking/Tracking.cs", "from.CheckSkill(SkillName.Tracking, 21.1, 100.0);", 1, 1 },
        { "Skills/TasteID.cs", "from.CheckTargetSkill(SkillName.TasteID, food, 0, 100)", 1, 1 },
        { "Items/Skill Items/Camping/Kindling.cs", "from.CheckSkill(SkillName.Camping, 0.0, 100.0)", 1, 1 },
        // Resisting Spells (0, 120): five live sites; the sixth, FireHorn.cs:175, runs only before AOS.
        { "Spells/Base/SpellHelper.cs", "target.CheckSkill(SkillName.MagicResist, 0.0, 120.0);", 1, 1 },
        { "Spells/Necromancy/PainSpike.cs", "m.CheckSkill(SkillName.MagicResist, 0.0, 120.0);", 1, 4 },
        { "Spells/Necromancy/MindRot.cs", "m.CheckSkill(SkillName.MagicResist, 0.0, 120.0);", 1, 4 },
        { "Spells/Necromancy/CorpseSkin.cs", "m.CheckSkill(SkillName.MagicResist, 0.0, 120.0);", 1, 4 },
        { "Spells/Necromancy/BloodOathSpell.cs", "m.CheckSkill(SkillName.MagicResist, 0.0, 120.0);", 1, 4 },
        { "Items/Skill Items/Misc/FireHorn.cs", "if (!Core.AOS && m.CheckSkill(SkillName.MagicResist, 0.0, 120.0))", 1, 1 },
        { "Spells/Base/SpellHelper.cs", "CheckSkill(SkillName.MagicResist, 0.0, 120.0)", 1, 6 },
        // Bushido: Momentum Strike (70, 120).
        { "Spells/Bushido/MomentumStrike.cs", "m.CheckSkill(MoveSkill, RequiredSkill, 120.0);", 1, 1 },
        { "Spells/Bushido/MomentumStrike.cs", "public override double RequiredSkill => 70.0;", 1, 2 },
        // Stealing: weight x 10 - 22.5 to + 27.5, at most 10 stones (cc-P57's patch), so the top is (77.5, 127.5).
        { "Skills/Stealing.cs", "_thief.CheckTargetSkill(SkillName.Stealing, toSteal, iw - 22.5, iw + 27.5)", 1, 1 },
        { "Skills/Stealing.cs", "pileWeight - 22.5,", 2, 2 },
        { "Skills/Stealing.cs", "pileWeight + 27.5", 2, 2 },
        { "Skills/Stealing.cs", "MaxWeightToSteal = ServerConfiguration.GetSetting(\"stealing.maxWeightToSteal\", 10);", 1, 1 },
        // The cap-following uses the worn-bonus rule reaches (any player call whose maximum is the skill's cap).
        { "Items/Weapons/BaseWeapon.cs", "attacker.CheckSkill(SkillName.Tactics, 0.0, attacker.Skills.Tactics.Cap);", 2, 2 },
        { "Items/Weapons/BaseWeapon.cs", "attacker.CheckSkill(SkillName.Anatomy, 0.0, attacker.Skills.Anatomy.Cap);", 2, 2 },
        { "Spells/Base/Spell.cs", "Caster.CheckSkill(DamageSkill, 0.0, Caster.Skills[DamageSkill].Cap);", 1, 1 },
        { "Skills/Meditation.cs", "m.CheckSkill(SkillName.Meditation, 0.0, m.Skills[SkillName.Meditation].Cap);", 1, 1 },
    };

    private static int Count(string text, string key)
    {
        var n = 0;

        for (var i = text.IndexOf(key, StringComparison.Ordinal); i >= 0; i = text.IndexOf(key, i + key.Length, StringComparison.Ordinal))
        {
            n++;
        }

        return n;
    }

    [Theory]
    [MemberData(nameof(Keys))]
    public void EachKeyedLineIsStillPinnedsWordForWord(string file, string key, int inFile, int inTree)
    {
        var root = UOContent();
        var path = Path.Combine(root, file);
        Assert.True(File.Exists(path), $"{file} is gone");

        var here = Count(File.ReadAllText(path), key);
        var everywhere = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Sum(f => Count(File.ReadAllText(f), key));
        _out.WriteLine($"{file}: {here} in the file, {everywhere} in UOContent: {key}");

        Assert.Equal(inFile, here);
        Assert.Equal(inTree, everywhere);
    }
}
