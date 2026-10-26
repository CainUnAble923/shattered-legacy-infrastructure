// ItemKnowledgeVerification.cs
//
// cc-P627, item identification stage 1: customizations ClusterFItemKnowledge and the patches Item-property-list-policy,
// LootPack-unidentified-magic, ItemIdentification-item-knowledge and ArmsLore-item-knowledge.
// Design: shard-migration notes/cc-P607-item-identification-design.md; notes: notes/cc-P627-build-item-identification.md.
//
// Every fact reads the item's real cached property list (Item.PropertyList, the buffer the client is sent), never
// GetProperties directly, so the seam in Item.InitializePropertyList is what is tested.
//
//   1. The leak test: a concealed magic weapon's list carries its plain art name, "Unidentified", the uninspected line
//      and what anyone may know, and none of its magic, resists, damage types, numbers, condition or custom name.
//   2. The tracks are independent and shared: Arms Lore measures resists and damage types while the magic stays hidden;
//      Appraisal names, then values, the magic while resists stay hidden; both complete gives the ordinary tooltip.
//   3. Appraisal's stages: below 30 it only senses magic; one roll per use against each reachable stage; a failure
//      changes nothing; a one second pause between items; the difficulty and chance formulas.
//   4. The real skill handlers reach the policy: pinned ItemIdentification's item target and ArmsLore's success.
//   5. Monster loot stamps new magic weapons and armor only while the policy is on.
//   6. What is visible today stays visible: an unstamped (crafted or existing) item, and every item with the policy off.
//   7. The registry survives a save and load, with the on switch.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Random;
using Server.SkillHandlers;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ItemKnowledgeVerification : IDisposable
{
    private static readonly Point3D Spot = new(1700, 1700, 0);

    private readonly ITestOutputHelper _out;
    private readonly List<IEntity> _made = new();
    private readonly List<(Mobile Who, string Text)> _heard = new();
    private readonly Func<Item, IPropertyList, bool> _policy = Item.PropertyListPolicy;
    private readonly bool _opl = ObjectPropertyList.Enabled;

    public ItemKnowledgeVerification(ITestOutputHelper output)
    {
        _out = output;
        ShardTestClock.Arm();
        ShardTestHost.EnsureSkillChecks();
        ClusterFItemKnowledge.ResetForTests();
        Item.PropertyListPolicy = ClusterFItemKnowledge.Render; // as Configure installs it
        ObjectPropertyList.Enabled = true; // so InvalidateProperties rebuilds the cached list, as on the shard
        ClusterFItemKnowledge.Heard = (m, t) => _heard.Add((m, t));
    }

    public void Dispose()
    {
        ClusterFItemKnowledge.Heard = null;
        ClusterFItemKnowledge.ResetForTests();
        Item.PropertyListPolicy = _policy;
        ObjectPropertyList.Enabled = _opl;
        BuiltInRng.Reset();
        foreach (var e in _made)
        {
            e.Delete();
        }
    }

    private sealed class ConstantRandom : System.Random
    {
        public double Value;

        protected override double Sample() => Value;
        public override double NextDouble() => Value;
        public override int Next() => (int)(Value * int.MaxValue);
        public override int Next(int maxValue) => (int)(Value * maxValue);
        public override int Next(int minValue, int maxValue) => minValue + (int)(Value * (maxValue - minValue));
        public override long NextInt64(long maxValue) => (long)(Value * maxValue);
    }

    private static void Roll(double value) => BuiltInRng.Generator = new ConstantRandom { Value = value };

    // The list the client is sent: Item.PropertyList, built through InitializePropertyList.
    private static List<(int Number, string Argument)> Sent(Item item)
    {
        var buffer = item.PropertyList.Buffer;
        var entries = new List<(int, string)>();
        var pos = 15;
        while (pos + 6 <= buffer.Length)
        {
            var number = BinaryPrimitives.ReadInt32BigEndian(buffer.AsSpan(pos));
            if (number == 0)
            {
                break;
            }

            var length = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(pos + 4));
            entries.Add((number, Encoding.Unicode.GetString(buffer, pos + 6, length)));
            pos += 6 + length;
        }

        return entries;
    }

    private static string Show(List<(int Number, string Argument)> l) =>
        string.Join(" | ", l.Select(e => e.Argument.Length == 0 ? $"{e.Number}" : $"{e.Number}:{e.Argument}"));

    private static bool HasText(List<(int Number, string Argument)> l, string text) => l.Any(e => e.Argument == text);

    // A magic longsword: a custom name, hit life leech 20, swing speed 10, a silver slayer, 20% fire damage.
    private Longsword Sword()
    {
        var sword = new Longsword { Name = "the Blade of Secrets" };
        sword.WeaponAttributes.HitLeechHits = 20;
        sword.Attributes.WeaponSpeed = 10;
        sword.Slayer = SlayerName.Silver;
        sword.AosElementDamages.Fire = 20;
        sword.MoveToWorld(Spot, Map.Trammel);
        _made.Add(sword);
        return sword;
    }

    private PlayerMobile Player(double appraisal = 0.0, double armsLore = 0.0)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Elf, Name = "Sage" };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        pm.RawStr = pm.RawDex = pm.RawInt = 0; // Value is exactly Base
        pm.Skills.ItemID.Cap = 200.0;
        pm.Skills.ItemID.Base = appraisal;
        pm.Skills.ArmsLore.Cap = 200.0;
        pm.Skills.ArmsLore.Base = armsLore;
        _made.Add(pm);
        return pm;
    }

    private static readonly int[] Secrets =
    [
        1060422, // hit life leech
        1060486, // swing speed increase
        1060403, // physical damage
        1060405, // fire damage
        1061168, // weapon damage
        1061167, // weapon speed
        1060639  // durability
    ];

    private static int SilverTitle => SlayerGroup.GetEntryByName(SlayerName.Silver).Title;

    // ---------------------------------------------------------------- 1

    [Fact]
    public void AConcealedWeaponSendsNoneOfItsSecrets()
    {
        var sword = Sword();

        // A line no catalog entry names (as an imbued custom property or a level bonus adds through Item.ExtraProperties)
        // must stay hidden too: an unknown line is concealed until someone reviews it.
        void Extra(Item item, IPropertyList list)
        {
            if (item == sword)
            {
                list.Add(1060658, "Hidden power	42"); // ~1_val~: ~2_val~
            }
        }

        Item.ExtraProperties += Extra;
        sword.InvalidateProperties(); // its list was built when it was placed
        try
        {
            CheckConcealed(sword);
        }
        finally
        {
            Item.ExtraProperties -= Extra;
        }
    }

    private void CheckConcealed(Longsword sword)
    {
        var stock = Sent(sword);
        _out.WriteLine($"stock: {Show(stock)}");
        Assert.Contains(stock, e => e.Number == 1060422 && e.Argument == "20"); // the stock list does carry it
        Assert.Contains(stock, e => e.Number == 1060658);

        ClusterFItemKnowledge.Enabled = true;
        ClusterFItemKnowledge.Conceal(sword);
        var sent = Sent(sword);
        _out.WriteLine($"concealed: {Show(sent)}");

        Assert.Equal((ClusterFItemKnowledge.SafeLabel(sword), ""), sent[0]);
        Assert.Equal(ClusterFItemKnowledge.SafeLabel(sword), sword.PropertyList.Header);
        Assert.True(HasText(sent, ClusterFItemKnowledge.UnidentifiedLine));
        Assert.True(HasText(sent, ClusterFItemKnowledge.UninspectedLine));
        Assert.Contains(sent, e => e.Number == 1061824); // one-handed: anyone can see how it is held
        Assert.Contains(sent, e => e.Number == 1061172); // skill required: swordsmanship

        foreach (var secret in Secrets.Append(SilverTitle))
        {
            Assert.DoesNotContain(sent, e => e.Number == secret);
        }

        Assert.DoesNotContain(sent, e => e.Argument.Contains("Secrets") || e.Argument.Contains("Hidden power"));
        Assert.DoesNotContain(sent, e => e.Argument.Contains("20") || e.Argument.Contains("10") || e.Argument.Contains("42"));
        Assert.NotEqual(stock.Count, sent.Count);
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void TheTwoTracksAreIndependentAndCompleteGivesTheOrdinaryTooltip()
    {
        Roll(0.0); // every check that may pass, passes
        var sword = Sword();
        var stock = Sent(sword);
        ClusterFItemKnowledge.Enabled = true;
        ClusterFItemKnowledge.Conceal(sword);

        // Arms Lore first: resists, damage types, numbers and condition, and still no magic.
        ClusterFItemKnowledge.OnArmsLore(Player(armsLore: 65.0), sword);
        var arms = Sent(sword);
        _out.WriteLine($"measured: {Show(arms)}");
        Assert.Contains(arms, e => e.Number == 1060405 && e.Argument == "20");
        Assert.Contains(arms, e => e.Number == 1060639);
        Assert.True(HasText(arms, ClusterFItemKnowledge.UnidentifiedLine));
        Assert.DoesNotContain(arms, e => e.Number is 1060422 or 1060486 || e.Number == SilverTitle);

        // Appraisal at 59: the names, not the values; the resists stay as measured. (A silver slayer counts at full
        // intensity, so this sword's difficulty is high: names at D - 60, values at D.)
        var sage = Player(appraisal: 59.0);
        Assert.True(ClusterFItemKnowledge.TryAppraise(sage, sword));
        Assert.Equal(MagicKnowledge.Named, ClusterFItemKnowledge.Of(sword).Magic);
        var named = Sent(sword);
        _out.WriteLine($"named: {Show(named)}");
        var traits = named.Single(e => e.Argument.StartsWith("Appraised traits: ")).Argument;
        Assert.Contains("hit life leech", traits);
        Assert.Contains("swing speed increase", traits);
        Assert.Contains("slayer", traits);
        Assert.True(HasText(named, ClusterFItemKnowledge.MagicValuesUnknownLine));
        Assert.DoesNotContain(named, e => e.Number is 1060422 or 1060486);
        Assert.Contains(named, e => e.Number == 1060405 && e.Argument == "20");

        // An expert's Appraisal: the values. Both tracks complete: the ordinary full tooltip, for everyone.
        var expert = Player(appraisal: 170.0);
        Assert.True(ClusterFItemKnowledge.TryAppraise(expert, sword));
        Assert.True(ClusterFItemKnowledge.Of(sword).Complete);
        Assert.True(sword.Identified);
        Assert.Equal(Show(stock), Show(Sent(sword)));
    }

    [Fact]
    public void AppraisalAloneNeverMeasuresResists()
    {
        Roll(0.0);
        var sword = Sword();
        ClusterFItemKnowledge.Enabled = true;
        ClusterFItemKnowledge.Conceal(sword);

        Assert.True(ClusterFItemKnowledge.TryAppraise(Player(appraisal: 120.0), sword));
        var sent = Sent(sword);
        _out.WriteLine($"values only: {Show(sent)}");
        Assert.Contains(sent, e => e.Number == 1060422 && e.Argument == "20");
        Assert.Contains(sent, e => e.Number == SilverTitle);
        Assert.True(HasText(sent, ClusterFItemKnowledge.UninspectedLine));
        Assert.DoesNotContain(sent, e => e.Number is 1060403 or 1060405 or 1061168 or 1060639);

        // Arms Lore at 40 assesses: the names of what it found, no numbers.
        ClusterFItemKnowledge.OnArmsLore(Player(armsLore: 40.0), sword);
        var assessed = Sent(sword).Single(e => e.Argument.StartsWith("Inspected: ")).Argument;
        Assert.Contains("fire damage", assessed);
        Assert.Contains("durability", assessed);
        Assert.DoesNotContain(Sent(sword), e => e.Number is 1060405 or 1060639);
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AppraisalStagesOneRollAndThePause()
    {
        var sword = Sword();
        ClusterFItemKnowledge.Enabled = true;
        ClusterFItemKnowledge.Conceal(sword);

        var novice = Player(appraisal: 20.0);
        Assert.True(ClusterFItemKnowledge.TryAppraise(novice, sword));
        Assert.Contains(_heard, h => h.Who == novice && h.Text == "You sense magic, but cannot identify it.");
        Assert.Equal(MagicKnowledge.Unidentified, ClusterFItemKnowledge.Of(sword).Magic);

        // One roll, read against the stage's chance: a roll at the chance fails and changes nothing; trying again at
        // once is refused for a second; a roll under the chance names the magic.
        var named = ClusterFItemKnowledge.StageDifficulty(ClusterFItemKnowledge.Difficulty(sword), MagicKnowledge.Named);
        var chance = ClusterFItemKnowledge.Chance(59.0, named);
        _out.WriteLine($"names difficulty {named}, chance at 59: {chance}");
        Assert.True(chance > 0.0);
        Roll(chance);
        var sage = Player(appraisal: 59.0);
        ClusterFItemKnowledge.TryAppraise(sage, sword);
        Assert.Equal(MagicKnowledge.Unidentified, ClusterFItemKnowledge.Of(sword).Magic);
        Roll(0.0);
        ClusterFItemKnowledge.TryAppraise(sage, sword);
        Assert.Equal(MagicKnowledge.Unidentified, ClusterFItemKnowledge.Of(sword).Magic);
        Assert.Contains(_heard, h => h.Who == sage && h.Text.StartsWith("You must wait"));
        ShardTestClock.AdvanceSeconds(1.1);
        ClusterFItemKnowledge.TryAppraise(sage, sword);
        Assert.Equal(MagicKnowledge.Named, ClusterFItemKnowledge.Of(sword).Magic);

        // The formulas: D = clamp(15 x properties + 0.75 x strongest intensity, 10, 175); names at D - 60; +/-25 window.
        var d = ClusterFItemKnowledge.Difficulty(sword);
        _out.WriteLine($"difficulty {d}, properties {RequisitionProperties.PropertyCount(sword)}");
        Assert.InRange(d, 10, 175);
        Assert.Equal(Math.Max(0, d - 60), ClusterFItemKnowledge.StageDifficulty(d, MagicKnowledge.Named));
        Assert.Equal(d, ClusterFItemKnowledge.StageDifficulty(d, MagicKnowledge.Values));
        Assert.Equal(1.0, ClusterFItemKnowledge.Chance(60.0, 30));
        Assert.Equal(0.5, ClusterFItemKnowledge.Chance(60.0, 60));
        Assert.Equal(0.0, ClusterFItemKnowledge.Chance(50.0, 75));

        var plain = new Longsword();
        plain.MoveToWorld(Spot, Map.Trammel);
        _made.Add(plain);
        Assert.Equal(10, ClusterFItemKnowledge.Difficulty(plain));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void ThePinnedSkillHandlersReachThePolicy()
    {
        Roll(0.0);
        ItemIdentification.Initialize();
        ArmsLore.Initialize();
        var sword = Sword();
        ClusterFItemKnowledge.Enabled = true;
        ClusterFItemKnowledge.Conceal(sword);

        var pm = Player(appraisal: 170.0, armsLore: 100.0);
        sword.Delete();
        sword = Sword();
        pm.Backpack.DropItem(sword);
        ClusterFItemKnowledge.Conceal(sword);

        Assert.True(pm.UseSkill(SkillName.ItemID));
        pm.Target.Invoke(pm, sword);
        Assert.Equal(MagicKnowledge.Values, ClusterFItemKnowledge.Of(sword).Magic);

        pm.NextSkillTime = Core.TickCount;
        Assert.True(pm.UseSkill(SkillName.ArmsLore));
        pm.Target.Invoke(pm, sword);
        Assert.Equal(ArmsKnowledge.Measured, ClusterFItemKnowledge.Of(sword).Arms);
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void MonsterLootStampsNewMagicGearOnlyWhileOn()
    {
        List<Item> Gear(bool on)
        {
            ClusterFItemKnowledge.Enabled = on;
            var found = new List<Item>();
            for (var i = 0; i < 40 && found.Count < 3; i++)
            {
                var orc = new Orc();
                orc.MoveToWorld(Spot, Map.Trammel);
                _made.Add(orc);
                if (orc.Backpack == null)
                {
                    orc.AddItem(new Backpack());
                }

                foreach (var old in orc.Backpack.Items.ToList())
                {
                    old.Delete(); // only what this pack generates below
                }

                LootPack.SuperBoss.Generate(orc, orc.Backpack, false, 0);
                found.AddRange(orc.Backpack.Items.Where(it => it is BaseWeapon or BaseArmor &&
                                                             RequisitionProperties.PropertyCount(it) > 0));
            }

            return found;
        }

        var stamped = Gear(true);
        _out.WriteLine($"on: {stamped.Count} magic weapons and armor");
        Assert.NotEmpty(stamped);
        Assert.All(stamped, it => Assert.True(ClusterFItemKnowledge.IsConcealed(it), $"{it.GetType().Name} was not concealed"));
        Assert.All(stamped, it => Assert.True(HasText(Sent(it), ClusterFItemKnowledge.UnidentifiedLine)));

        var unstamped = Gear(false);
        Assert.NotEmpty(unstamped);
        Assert.All(unstamped, it => Assert.Null(ClusterFItemKnowledge.Of(it)));
    }

    // ---------------------------------------------------------------- 6

    [Fact]
    public void WhatIsVisibleTodayStaysVisible()
    {
        var crafted = Sword(); // never stamped: as made by a smith, or found before the policy
        var stock = Show(Sent(crafted));
        var found = Sword();
        var foundStock = Show(Sent(found));
        ClusterFItemKnowledge.Enabled = true;
        crafted.InvalidateProperties();
        Assert.Equal(stock, Show(Sent(crafted)));
        Assert.Null(ClusterFItemKnowledge.Of(crafted));

        // A concealed item with the policy switched off shows its ordinary list; switched on again it is concealed again.
        ClusterFItemKnowledge.Conceal(found);
        Assert.True(HasText(Sent(found), ClusterFItemKnowledge.UnidentifiedLine));
        ClusterFItemKnowledge.Enabled = false;
        Assert.Equal(foundStock, Show(Sent(found)));
        Assert.False(ClusterFItemKnowledge.TryAppraise(Player(appraisal: 100.0), found)); // the stock route runs
        ClusterFItemKnowledge.Enabled = true;
        Assert.True(HasText(Sent(found), ClusterFItemKnowledge.UnidentifiedLine));
    }

    // ---------------------------------------------------------------- 7

    [Fact]
    public void TheRegistrySurvivesASaveAndLoad()
    {
        var sword = Sword();
        var other = Sword();
        ClusterFItemKnowledge.Enabled = true;
        ClusterFItemKnowledge.Conceal(sword);
        ClusterFItemKnowledge.Conceal(other);
        ClusterFItemKnowledge.Of(sword).Magic = MagicKnowledge.Named;
        ClusterFItemKnowledge.Of(sword).Arms = ArmsKnowledge.Measured;
        other.Delete(); // a deleted item's row is not saved

        var buffer = new byte[4096];
        var writer = new BufferWriter(buffer, true);
        ItemKnowledgeStore.Instance.Serialize(writer);
        writer.Flush();

        ClusterFItemKnowledge.ResetForTests();
        Assert.Null(ClusterFItemKnowledge.Of(sword));
        ItemKnowledgeStore.Instance.Deserialize(new BufferReader(buffer));

        Assert.True(ClusterFItemKnowledge.Enabled);
        Assert.Equal(MagicKnowledge.Named, ClusterFItemKnowledge.Of(sword).Magic);
        Assert.Equal(ArmsKnowledge.Measured, ClusterFItemKnowledge.Of(sword).Arms);
        Assert.Null(ClusterFItemKnowledge.Of(other));
    }
}
