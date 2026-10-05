// HammerHeldBonusVerification.cs
//
// cc-P52 Parts A and B (bug-list D59, D60; Chase 2026-10-03 and 10-04).
//
// Part A: the Hammer of Hephaestus gave its +5 Blacksmithy from the backpack (OnAdded applied the SkillMod when the
// parent was any container whose root was the player), and nothing re-applied it after a world load, so a hammer held
// at login gave nothing. Same on the Reinforced hammer (+10 base and the familiarity mod). The bonus now applies only
// while the hammer is held (Parent is the Mobile), is re-applied after a load, and when a restore or passive regen
// brings an exhausted hammer back while held. Each hammer holds at most one mod of each kind.
//
// Part B: the hammers' single-click (context) menu gains an entry, cliloc 1112530 "Knowledge", that opens the Metal
// Familiarity panel anywhere, for the character carrying the hammer.
//
// Facts, each for both tiers:
//   A1. In the pack: no bonus. Held: the bonus, once.
//   A2. Held then to the pack: none. Pack then held: the bonus, once (repeated moves never stack).
//   A3. A held hammer after serialize and deserialize has the bonus (after the load), one mod; one in the pack has none.
//   A4. A Guildmaster restore and passive regen out of exhaustion add the bonus while held, not in the pack.
//   A5. Reinforced only: the familiarity mod follows the same rule (held only, one mod, back after a load). Its familiarity
//       is one metal at the cap (2,500 since cc-P55 Part A, 250 before).
//   B1. The menu entry is there for the owner (held or in the pack) and not for another character; selecting it sends
//       the familiarity gump.

using System;
using System.Linq;
using System.Reflection;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class HammerHeldBonusVerification
{
    private readonly ITestOutputHelper _out;

    public HammerHeldBonusVerification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D Spot = new(1420, 1750, 0);

    private const string T1Base = "HammerOfHephaestusBase";
    private const string T2Base = "ReinforcedHammerOfHephaestusBase";
    private const string T2Familiarity = "ReinforcedHammerOfHephaestus";

    private static PlayerMobile Smith()
    {
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.RawStr = pm.RawDex = pm.RawInt = 100;
        pm.MoveToWorld(Spot, Map.Trammel);
        pm.Skills.Blacksmith.Cap = 120.0;
        pm.Skills.Blacksmith.Base = 100.0;
        return pm;
    }

    private static BaseTool NewHammer(bool reinforced) =>
        reinforced ? new ReinforcedHammerOfHephaestus() : new HammerOfHephaestus();

    private static string BaseName(bool reinforced) => reinforced ? T2Base : T1Base;

    private static int Mods(Mobile m, string name) => m.SkillMods?.Count(x => x.Name == name) ?? 0;

    private static double Bonus(Mobile m) => Math.Round(m.Skills.Blacksmith.Value - m.Skills.Blacksmith.Base, 1);

    private static double Expected(bool reinforced) => reinforced ? 10.0 : 5.0;

    private void Report(string step, Mobile m, bool reinforced) =>
        _out.WriteLine($"{(reinforced ? "T2" : "T1")} {step}: base mods {Mods(m, BaseName(reinforced))}, " +
                       $"familiarity mods {Mods(m, T2Familiarity)}, bonus +{Bonus(m)}");

    private static void Hold(PlayerMobile pm, Item hammer) => Assert.True(pm.EquipItem(hammer));

    // Through pinned's private ApplyPassiveRegen and the regen timestamp, which read the wall clock.
    private static void ExhaustAndRegen(BaseTool hammer, Mobile from, bool regen)
    {
        hammer.UsesRemaining = 0;
        hammer.OnDoubleClick(from); // TriggerExhaustion: no uses left
        Assert.True(IsExhausted(hammer));

        if (!regen)
        {
            return;
        }

        var type = hammer.GetType();
        type.GetField("_lastRegenAtTicks", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(hammer, DateTime.UtcNow.AddMinutes(-31).Ticks);
        type.GetMethod("ApplyPassiveRegen", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(hammer, null);
        Assert.False(IsExhausted(hammer));
    }

    private static bool IsExhausted(BaseTool hammer) => hammer switch
    {
        HammerOfHephaestus h => h.Exhausted,
        ReinforcedHammerOfHephaestus r => r.Exhausted,
        _ => throw new ArgumentException(hammer.GetType().Name)
    };

    private static void Restore(BaseTool hammer)
    {
        switch (hammer)
        {
            case HammerOfHephaestus h:
                h.GuildmasterRestore();
                break;
            case ReinforcedHammerOfHephaestus r:
                r.GuildmasterRestore();
                break;
        }
    }

    // ---------------------------------------------------------------- A1, A2

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheBonusAppliesOnlyWhileHeldAndNeverStacks(bool reinforced)
    {
        var pm = Smith();
        var hammer = NewHammer(reinforced);
        var name = BaseName(reinforced);

        try
        {
            pm.Backpack.DropItem(hammer);
            Report("in pack", pm, reinforced);
            Assert.Equal(0, Mods(pm, name));
            Assert.Equal(0.0, Bonus(pm));

            Hold(pm, hammer);
            Report("held", pm, reinforced);
            Assert.Same(pm, hammer.Parent);
            Assert.Equal(1, Mods(pm, name));
            Assert.Equal(Expected(reinforced), Bonus(pm));

            pm.Backpack.DropItem(hammer);
            Report("held then pack", pm, reinforced);
            Assert.Equal(0, Mods(pm, name));
            Assert.Equal(0.0, Bonus(pm));

            for (var i = 0; i < 3; i++)
            {
                Hold(pm, hammer);
                pm.Backpack.DropItem(hammer);
            }

            Hold(pm, hammer);
            Report("pack then held, after three round trips", pm, reinforced);
            Assert.Equal(1, Mods(pm, name));
            Assert.Equal(Expected(reinforced), Bonus(pm));
        }
        finally
        {
            hammer.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- A3

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void AHeldHammerHasTheBonusAfterALoad(bool reinforced, bool held)
    {
        ShardTestClock.Arm();
        var pm = Smith();
        var original = NewHammer(reinforced);
        var name = BaseName(reinforced);
        BaseTool copy = null;

        try
        {
            if (held)
            {
                Hold(pm, original);
            }
            else
            {
                pm.Backpack.DropItem(original);
            }

            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            original.Serialize(writer);
            writer.Flush();

            // A restart: the mods were never saved, and the item comes back from its bytes.
            original.Delete();
            Assert.Equal(0, Mods(pm, name));

            copy = reinforced
                ? new ReinforcedHammerOfHephaestus(original.Serial)
                : new HammerOfHephaestus(original.Serial);
            copy.Deserialize(new BufferReader(buffer));
            Assert.Same(held ? pm : pm.Backpack, copy.Parent);

            ShardTestClock.Advance(TimeSpan.FromMilliseconds(100));
            Report(held ? "held, after load" : "in pack, after load", pm, reinforced);

            Assert.Equal(held ? 1 : 0, Mods(pm, name));
            Assert.Equal(held ? Expected(reinforced) : 0.0, Bonus(pm));
        }
        finally
        {
            copy?.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- A4

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    public void RestoreAndRegenAddTheBonusOnlyWhileHeld(bool reinforced, bool held, bool regen)
    {
        var pm = Smith();
        var hammer = NewHammer(reinforced);
        var name = BaseName(reinforced);

        try
        {
            if (held)
            {
                Hold(pm, hammer);
            }
            else
            {
                pm.Backpack.DropItem(hammer);
            }

            ExhaustAndRegen(hammer, pm, false);
            Report("exhausted", pm, reinforced);
            Assert.Equal(0, Mods(pm, name));

            if (regen)
            {
                ExhaustAndRegen(hammer, pm, true);
            }
            else
            {
                Restore(hammer);
                Restore(hammer); // a second restore must not add a second mod
            }

            Report($"{(held ? "held" : "in pack")}, after {(regen ? "regen" : "two restores")}", pm, reinforced);
            Assert.Equal(held ? 1 : 0, Mods(pm, name));
            Assert.Equal(held ? Expected(reinforced) : 0.0, Bonus(pm));
        }
        finally
        {
            hammer.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- A5

    [Fact]
    public void TheReinforcedFamiliarityModFollowsTheSameRule()
    {
        ShardTestClock.Arm();
        var pm = Smith();
        var hammer = new ReinforcedHammerOfHephaestus();
        ReinforcedHammerOfHephaestus copy = null;

        try
        {
            // 2,500 Iron of 42,500: +0.1 (GetSkillBonus rounds to a tenth). cc-P55 Part A: the cap was 250 (250 of 4,250).
            hammer.LoadFamiliaritySnapshot(new() { [(int)CraftResource.Iron] = ReinforcedHammerOfHephaestus.FamCap }, ReinforcedHammerOfHephaestus.FamCap);
            pm.Backpack.DropItem(hammer);
            Report("in pack with familiarity", pm, true);
            Assert.Equal(0, Mods(pm, T2Familiarity));

            Hold(pm, hammer);
            hammer.GuildmasterRestore(); // re-applies: still one of each
            Report("held with familiarity", pm, true);
            Assert.Equal(1, Mods(pm, T2Familiarity));
            Assert.Equal(10.1, Bonus(pm));

            pm.Backpack.DropItem(hammer);
            Assert.Equal(0, Mods(pm, T2Familiarity));

            Hold(pm, hammer);
            var buffer = new byte[65536];
            var writer = new BufferWriter(buffer, true);
            hammer.Serialize(writer);
            writer.Flush();
            hammer.Delete();
            Assert.Equal(0, Mods(pm, T2Familiarity));

            copy = new ReinforcedHammerOfHephaestus(hammer.Serial);
            copy.Deserialize(new BufferReader(buffer));
            ShardTestClock.Advance(TimeSpan.FromMilliseconds(100));
            Report("held with familiarity, after load", pm, true);
            Assert.Equal(1, Mods(pm, T2Familiarity));
            Assert.Equal(1, Mods(pm, T2Base));
            Assert.Equal(10.1, Bonus(pm));
        }
        finally
        {
            copy?.Delete();
            hammer.Delete();
            pm.Delete();
        }
    }

    // ---------------------------------------------------------------- B1

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheMenuEntryOpensTheFamiliarityPanelForTheOwner(bool reinforced)
    {
        var pm = Smith();
        var other = Smith();
        var hammer = NewHammer(reinforced);
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;

        try
        {
            foreach (var held in new[] { false, true })
            {
                if (held)
                {
                    Hold(pm, hammer);
                }
                else
                {
                    pm.Backpack.DropItem(hammer);
                }

                var mine = Entries(hammer, pm);
                var theirs = Entries(hammer, other);
                _out.WriteLine($"{(reinforced ? "T2" : "T1")} {(held ? "held" : "in pack")}: owner sees " +
                               $"[{string.Join(", ", mine.Select(e => e.Number))}], another character " +
                               $"[{string.Join(", ", theirs.Select(e => e.Number))}]");

                var entry = Assert.Single(mine.OfType<HammerFamiliarityEntry>());
                Assert.Equal(1112530, entry.Number);
                Assert.Empty(theirs.OfType<HammerFamiliarityEntry>());

                pm.CloseGump<HammerFamiliarityGump>();
                Assert.Null(pm.FindGump<HammerFamiliarityGump>());
                entry.OnClick(pm, hammer);
                Assert.NotNull(pm.FindGump<HammerFamiliarityGump>());
            }
        }
        finally
        {
            pm.NetState = null;
            ns.Mobile = null;
            hammer.Delete();
            pm.Delete();
            other.Delete();
        }
    }

    private static ContextMenuEntry[] Entries(Item item, Mobile from)
    {
        var list = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            item.GetContextMenuEntries(from, ref list);
            var result = new ContextMenuEntry[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                result[i] = list[i];
            }

            return result;
        }
        finally
        {
            list.Dispose();
        }
    }
}
