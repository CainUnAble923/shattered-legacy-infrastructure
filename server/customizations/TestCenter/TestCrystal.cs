// cc-P190 (Chase, 2026-10-19 14:58: "a crystal/item that can be placed in players banks for now that when turned on
// increases skill gain XXX amount for testing"): the Test Crystal, a per-character skill gain booster for the test shard.
// Notes: shard-migration notes/cc-P190-cont-test-crystal.md.
//
// What it does: its owner's gain chance is multiplied by the crystal's strength (Off, 2x, 5x, 10x, 25x) at the one place
// pinned reads GainFactor, CheckSkill's `gc *= skill.Info.GainFactor` (SkillCheck.cs:133 at d4531cd9), through
// server/patches/SkillCheck-test-crystal.patch. Same spot, same meaning as the old global testCenter.skillGainMultiplier
// (TestCenterSkillGain.cs, now retired), but for one character. Nobody else is touched; with every crystal Off, gains are
// exactly the shard's own (ClusterFSkillGain's attempts and amount sit around pinned's roll, unchanged).
// Below 10.0 pinned gains on every use regardless of the chance (SkillCheck.cs:120-123), so the crystal changes nothing
// there, as the global multiplier did not.
//
// Test Center only: on a shard with testCenter.enable off it multiplies nothing, and it deletes itself when loaded or
// used there, so a crystal that reached live by any route can never matter.
//
// Owner-bound and blessed. The setting is the crystal's, saved with it; one crystal per character counts (the registry
// below holds the owner's newest). [TCFill puts one in the bank if the character has none; [GiveTestCrystal gives one to a
// target (staff, Test Center only).

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Commands;
using Server.Gumps;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class TestCrystal : Item
{
    /// <summary>The settings offered: 1 is Off.</summary>
    public static readonly int[] Strengths = [1, 2, 5, 10, 25];

    private const int HueOff = 0x0481; // pale
    private const int HueOn = 0x0035;  // gold

    private static readonly Dictionary<Mobile, TestCrystal> _byOwner = new();

    [SerializableField(0)]
    private Mobile? _owner;

    [SerializableField(1)]
    private int _strength = 1;

    [Constructible]
    public TestCrystal() : this(null)
    {
    }

    public TestCrystal(Mobile? owner) : base(0x1ECD)
    {
        LootType = LootType.Blessed;
        Weight = 0.1;
        Hue = HueOff;
        _owner = owner;
        Register();
    }

    public override string DefaultName => "a Test Crystal";

    /// <summary>The owner's gain chance multiplier: the strength of their crystal, or 1.</summary>
    public static double GainScalar(Mobile from) =>
        TestCenter.Enabled && from is PlayerMobile && _byOwner.TryGetValue(from, out var crystal) && !crystal.Deleted
            ? crystal._strength
            : 1.0;

    /// <summary>The owner's crystal, or null.</summary>
    public static TestCrystal? For(Mobile m) =>
        m != null && _byOwner.TryGetValue(m, out var crystal) && !crystal.Deleted ? crystal : null;

    /// <summary>Puts a crystal (Off) in <paramref name="m"/>'s bank unless they have one. Test Center only.</summary>
    public static TestCrystal? EnsureFor(Mobile m)
    {
        if (!TestCenter.Enabled || m is not PlayerMobile || m.BankBox == null || For(m) != null)
        {
            return null;
        }

        var crystal = new TestCrystal(m);
        m.BankBox.DropItem(crystal);
        return crystal;
    }

    /// <summary>Sets the strength (one of <see cref="Strengths"/>; anything else is Off).</summary>
    public void SetStrength(int strength)
    {
        Strength = Array.IndexOf(Strengths, strength) >= 0 ? strength : 1;
        Hue = Strength > 1 ? HueOn : HueOff;
        InvalidateProperties();
    }

    private void Register()
    {
        if (_owner != null)
        {
            _byOwner[_owner] = this;
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (!TestCenter.Enabled)
        {
            Timer.DelayCall(Delete); // never on a live shard; not during the load itself
            return;
        }

        Register();
    }

    public override void OnDelete()
    {
        base.OnDelete();

        if (_owner != null && _byOwner.TryGetValue(_owner, out var crystal) && crystal == this)
        {
            _byOwner.Remove(_owner);
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(_strength > 1 ? $"Skill gain chance: {_strength}x" : "Off");
        list.Add($"Owner: {_owner?.Name ?? "nobody"}");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!TestCenter.Enabled)
        {
            from.SendMessage("The crystal crumbles: it only works on a Test Center shard.");
            Delete();
            return;
        }

        if (from != _owner && from.AccessLevel == AccessLevel.Player)
        {
            from.SendMessage("This crystal is bound to someone else.");
            return;
        }

        if (RootParent != from)
        {
            from.SendMessage("The crystal must be in your bank or your pack.");
            return;
        }

        from.SendGump(new TestCrystalGump(this));
    }

    // ---------------------------------------------------------------- staff command

    public static void Initialize()
    {
        if (TestCenter.Enabled)
        {
            CommandSystem.Register("GiveTestCrystal", AccessLevel.GameMaster, GiveTestCrystal_OnCommand);
        }
    }

    [Usage("GiveTestCrystal")]
    [Description("Test Center only. Puts a Test Crystal (Off) in the targeted player's bank, bound to them, unless they have one.")]
    [ShardCommand(CommandCategory.DevTool)]
    private static void GiveTestCrystal_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target the player to give a Test Crystal to.");
        e.Mobile.Target = new GiveTarget();
    }

    private sealed class GiveTarget : Target
    {
        public GiveTarget() : base(-1, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not PlayerMobile pm)
            {
                from.SendMessage("That is not a player.");
                return;
            }

            if (For(pm) is { } existing)
            {
                from.SendMessage($"{pm.Name} already has a Test Crystal ({(existing.Strength > 1 ? $"{existing.Strength}x" : "Off")}).");
                return;
            }

            if (EnsureFor(pm) == null)
            {
                from.SendMessage($"{pm.Name} has no bank box.");
                return;
            }

            from.SendMessage($"A Test Crystal is in {pm.Name}'s bank.");
            pm.SendMessage("A Test Crystal has been placed in your bank. Double-click it to set your skill gain for testing.");
        }
    }

    // ---------------------------------------------------------------- the gump

    private sealed class TestCrystalGump : Gump
    {
        private readonly TestCrystal _crystal;

        public TestCrystalGump(TestCrystal crystal) : base(200, 200)
        {
            _crystal = crystal;

            AddPage(0);
            AddBackground(0, 0, 300, 250, 9270);
            AddAlphaRegion(8, 8, 284, 234);

            AddLabel(100, 16, 1153, "Test Crystal");
            AddImageTiled(10, 36, 280, 2, 9304);

            var now = crystal.Strength > 1 ? $"{crystal.Strength}x skill gain chance" : "Off";
            AddLabel(20, 44, 1152, $"Now: {now}");

            for (var i = 0; i < Strengths.Length; i++)
            {
                var s = Strengths[i];
                var y = 70 + i * 24;
                AddButton(20, y, s == crystal.Strength ? 4006 : 4005, 4007, i + 1);
                AddLabel(55, y + 2, s == crystal.Strength ? 68 : 1152, s > 1 ? $"{s}x" : "Off");
            }

            AddHtml(20, 194, 260, 44, $"<BASEFONT COLOR=#CCCCCC>Only affects its owner, {crystal.Owner?.Name ?? "nobody"}. Test Center only.</BASEFONT>");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var from = sender.Mobile;
            var index = info.ButtonID - 1;

            if (from == null || index < 0 || index >= Strengths.Length)
            {
                return;
            }

            if (_crystal.Deleted || !TestCenter.Enabled || (from != _crystal.Owner && from.AccessLevel == AccessLevel.Player))
            {
                return;
            }

            _crystal.SetStrength(Strengths[index]);
            from.SendMessage(_crystal.Strength > 1 ? $"Test Crystal: {_crystal.Strength}x skill gain chance." : "Test Crystal: Off.");
            from.SendGump(new TestCrystalGump(_crystal));
        }
    }
}
