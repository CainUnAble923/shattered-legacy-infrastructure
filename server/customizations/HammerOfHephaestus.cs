using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Server.Accounting;
using Server.Engines.BulkOrders;
using Server.Engines.Craft;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

// ─────────────────────────────────────────────────────────────────────────────
// Hammer of Hephaestus — Society of Smiths legacy tool, parallel to Jacob's
// Pickaxe for the Miners' Compact.
//
// Design identity:
//   "Jacob's Pickaxe records the world. Hammer of Hephaestus remembers the metal."
//
// Tier progression — Metal Familiarity bonuses (one per tier):
//   T1 — D: Use cost reduction   — up to 20% chance to refund a hammer use per forge
//   T2 — C: Skill bonus          — cross-metal familiarity drives a real SkillMod (+2.5 BS max)
//   T3 — B: Resource efficiency  — familiar metals waste fewer ingots (STUB — implement with T3)
//   T4 — A: Exceptional quality  — familiar metals get bonus exceptional chance  (STUB — implement with T4)
//
// Familiarity is serialized (persistent across restarts) and transferred on upgrade.
// T3+T3 → T4 combination: call MergeFamiliarity on the T4 instance with both T3 snapshots.
//
// Serialization (manual — no source generator, to support Dictionary<int,int>):
//   v0 (legacy generator format): bool _exhausted + long _lastRegenAtTicks (no familiarity)
//   v1 (current):                 bool + long + Dictionary<int,int> familiarity
//
// Note on +5/+10 Blacksmithy base bonus: SmithHammer extends BaseTool which has no
// SkillBonuses property. The base bonus is display-only until Phase 4E adds SkillMod
// on equip/remove. The T2 familiarity SkillMod IS real and stacks on top of it.
// ─────────────────────────────────────────────────────────────────────────────

// ── Shared helpers ────────────────────────────────────────────────────────────

internal static class HammerMetal
{
    // All 17 supported metals in display order
    public static readonly (int Resource, string Name)[] All =
    [
        ((int)CraftResource.Iron,       "Iron"),
        ((int)CraftResource.DullCopper, "Dull Copper"),
        ((int)CraftResource.ShadowIron, "Shadow Iron"),
        ((int)CraftResource.Copper,     "Copper"),
        ((int)CraftResource.Bronze,     "Bronze"),
        ((int)CraftResource.Gold,       "Gold"),
        ((int)CraftResource.Agapite,    "Agapite"),
        ((int)CraftResource.Verite,     "Verite"),
        ((int)CraftResource.Valorite,   "Valorite"),
        ((int)CraftResource.Platinum,   "Platinum"),
        ((int)CraftResource.Toxic,      "Toxic"),
        ((int)CraftResource.Blaze,      "Blaze"),
        ((int)CraftResource.Frost,      "Frost"),
        ((int)CraftResource.Obsidian,   "Obsidian"),
        ((int)CraftResource.Mythril,    "Mythril"),
        ((int)CraftResource.Adamantium, "Adamantium"),
        ((int)CraftResource.Celestial,  "Celestial"),
    ];

    public static int Count => All.Length; // 17

    public static int InferResource(Type? t)
    {
        if (t == null) return -1;
        if (t == typeof(IronIngot))       return (int)CraftResource.Iron;
        if (t == typeof(DullCopperIngot)) return (int)CraftResource.DullCopper;
        if (t == typeof(ShadowIronIngot)) return (int)CraftResource.ShadowIron;
        if (t == typeof(CopperIngot))     return (int)CraftResource.Copper;
        if (t == typeof(BronzeIngot))     return (int)CraftResource.Bronze;
        if (t == typeof(GoldIngot))       return (int)CraftResource.Gold;
        if (t == typeof(AgapiteIngot))    return (int)CraftResource.Agapite;
        if (t == typeof(VeriteIngot))     return (int)CraftResource.Verite;
        if (t == typeof(ValoriteIngot))   return (int)CraftResource.Valorite;
        if (t == typeof(PlatinumIngot))   return (int)CraftResource.Platinum;
        if (t == typeof(ToxicIngot))      return (int)CraftResource.Toxic;
        if (t == typeof(BlazeIngot))      return (int)CraftResource.Blaze;
        if (t == typeof(FrostIngot))      return (int)CraftResource.Frost;
        if (t == typeof(ObsidianIngot))   return (int)CraftResource.Obsidian;
        if (t == typeof(MythrilIngot))    return (int)CraftResource.Mythril;
        if (t == typeof(AdamantiumIngot)) return (int)CraftResource.Adamantium;
        if (t == typeof(CelestialIngot))  return (int)CraftResource.Celestial;
        return -1;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Tier 1 — Hammer of Hephaestus
// ─────────────────────────────────────────────────────────────────────────────

[Flippable(0x13E3, 0x13E4)]
public partial class HammerOfHephaestus : SmithHammer
{
    private const int    FunctionalHue    = 0x0504;
    private const int    ExhaustedHue     = 0x0415;
    private const int    MaxUses          = 150;
    private const int    RegenMinutes     = 30;
    private const int    FamCap           = 100;
    private const string RegKey           = "legacy.hammer_of_hephaestus";

    // T1 Bonus — D: use cost reduction
    // Chance to refund a hammer use = (familiarity / FamCap) * MaxRefundChance
    private const double MaxRefundChance  = 0.20; // 20% at cap

    // ── Non-serialized ────────────────────────────────────────────────────────

    private SkillMod? _baseSkillMod; // NOT serialized — transient, fixed +5 Blacksmithy

    // ── State ─────────────────────────────────────────────────────────────────

    private bool                  _exhausted;
    private long                  _lastRegenAtTicks;
    private Dictionary<int, int>  _metalFamiliarity = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    [Constructible]
    public HammerOfHephaestus() : base(MaxUses)
    {
        LootType          = LootType.Blessed;
        Hue               = FunctionalHue;
        _lastRegenAtTicks = DateTime.UtcNow.Ticks;
    }

    // Required by ModernUO world-load deserializer
    public HammerOfHephaestus(Serial serial) : base(serial) { }

    // ── Properties ────────────────────────────────────────────────────────────

    public override int  LabelNumber      => 1077740; // Hammer of Hephaestus
    public override bool BreakOnDepletion => false;
    public          bool Exhausted        => _exhausted;

    // ── Serialization (manual — supports Dictionary<int,int>) ────────────────

    public override void Serialize(IGenericWriter writer)
    {
        base.Serialize(writer);
        writer.Write(1); // version

        writer.Write(_exhausted);
        writer.Write(_lastRegenAtTicks);

        writer.Write(_metalFamiliarity.Count);
        foreach (var (k, v) in _metalFamiliarity)
        {
            writer.Write(k);
            writer.Write(v);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        base.Deserialize(reader);
        var version = reader.ReadInt();

        switch (version)
        {
            case 0:
                // Legacy generator format: bool + long (no familiarity)
                _exhausted        = reader.ReadBool();
                _lastRegenAtTicks = reader.ReadLong();
                _metalFamiliarity = new Dictionary<int, int>();
                break;

            case 1:
                _exhausted        = reader.ReadBool();
                _lastRegenAtTicks = reader.ReadLong();
                var count         = reader.ReadInt();
                _metalFamiliarity = new Dictionary<int, int>(count);
                for (var i = 0; i < count; i++)
                    _metalFamiliarity[reader.ReadInt()] = reader.ReadInt();
                break;
        }

        // Guard: uninitialized regen timestamp
        if (_lastRegenAtTicks == 0)
            _lastRegenAtTicks = DateTime.UtcNow.Ticks;
    }

    // ── Passive regen ─────────────────────────────────────────────────────────

    private void ApplyPassiveRegen()
    {
        if (UsesRemaining >= MaxUses && !_exhausted) return;

        var now     = DateTime.UtcNow;
        var elapsed = now - new DateTime(_lastRegenAtTicks, DateTimeKind.Utc);
        var earned  = (int)(elapsed.TotalMinutes / RegenMinutes);

        if (earned <= 0) return;

        var wasExhausted = _exhausted;
        UsesRemaining    = Math.Min(MaxUses, UsesRemaining + earned);
        _lastRegenAtTicks += TimeSpan.FromMinutes((long)earned * RegenMinutes).Ticks;

        if (wasExhausted && UsesRemaining > 0)
        {
            _exhausted = false;
            Hue        = FunctionalHue;
            ((IUsesRemaining)this).ShowUsesRemaining = true;
            InvalidateProperties();
        }
    }

    // ── Exhaustion ────────────────────────────────────────────────────────────

    private void TriggerExhaustion(Mobile from)
    {
        if (_exhausted) return;
        _exhausted = true;
        Hue        = ExhaustedHue;
        ((IUsesRemaining)this).ShowUsesRemaining = false;
        InvalidateProperties();
        RemoveBaseSkillMod();
        from.SendMessage(0x22,
            "The Hammer of Hephaestus is spent. It will slowly regenerate on its own. " +
            "Speak with the Blacksmith Guildmaster to restore it immediately.");
    }

    // ── Base SkillMod (Phase 4E) ──────────────────────────────────────────────

    private Mobile? GetOwnerMobile() => RootParent as Mobile ?? Parent as Mobile;

    private void ApplyBaseSkillMod(Mobile m)
    {
        RemoveBaseSkillMod();
        if (!_exhausted)
        {
            _baseSkillMod = new DefaultSkillMod(SkillName.Blacksmith, "HammerOfHephaestusBase", true, 5.0);
            m.AddSkillMod(_baseSkillMod);
        }
    }

    private void RemoveBaseSkillMod()
    {
        if (_baseSkillMod != null)
        {
            _baseSkillMod.Remove();
            _baseSkillMod = null;
        }
    }

    // ── Metal Familiarity + T1 D-bonus ───────────────────────────────────────

    /// <summary>
    /// Called from CraftItem.cs after each successful Blacksmithy craft.
    /// Records familiarity and applies the T1 use-refund bonus.
    /// </summary>
    public void RecordFamiliarity(Type resourceType, Mobile from)
    {
        var key = HammerMetal.InferResource(resourceType);
        if (key < 0) return;

        _metalFamiliarity.TryGetValue(key, out var cur);
        if (cur < FamCap)
        {
            _metalFamiliarity[key] = cur + 1;
            InvalidateProperties();
        }

        // T1 Bonus D — use cost reduction
        var chance = (Math.Min(cur + 1, FamCap) / (double)FamCap) * MaxRefundChance;
        if (Utility.RandomDouble() < chance)
        {
            UsesRemaining = Math.Min(MaxUses, UsesRemaining + 1);
            from?.SendMessage(0x44,
                "Your familiarity with this metal spared the hammer a charge.");
        }
    }

    // ── Familiarity snapshots (for upgrade / T4 combine) ─────────────────────

    public Dictionary<int, int> GetFamiliaritySnapshot() =>
        new Dictionary<int, int>(_metalFamiliarity);

    public void LoadFamiliaritySnapshot(Dictionary<int, int> snapshot, int targetCap)
    {
        _metalFamiliarity = new Dictionary<int, int>();
        foreach (var (k, v) in snapshot)
            _metalFamiliarity[k] = Math.Min(targetCap, v);
        InvalidateProperties();
    }

    /// <summary>
    /// Merge another hammer's familiarity into this one (for T4 two-T3-combine path).
    /// Each metal is summed then capped at targetCap.
    /// </summary>
    public void MergeFamiliarity(Dictionary<int, int> other, int targetCap)
    {
        foreach (var (k, v) in other)
        {
            _metalFamiliarity.TryGetValue(k, out var existing);
            _metalFamiliarity[k] = Math.Min(targetCap, existing + v);
        }
        InvalidateProperties();
    }

    // ── Overrides ─────────────────────────────────────────────────────────────

    public override bool OnEquip(Mobile from)
    {
        ApplyPassiveRegen();
        if (_exhausted || UsesRemaining <= 0)
        {
            from.SendMessage(0x22,
                "The Hammer of Hephaestus is exhausted and cannot be equipped. " +
                "Wait for it to regenerate, or speak with the Blacksmith Guildmaster.");
            return false;
        }
        return base.OnEquip(from);
    }

    public override void OnDoubleClick(Mobile from)
    {
        ApplyPassiveRegen();

        if (UsesRemaining <= 0 && !_exhausted)
            TriggerExhaustion(from);

        if (_exhausted)
        {
            var last     = new DateTime(_lastRegenAtTicks, DateTimeKind.Utc);
            var minsLeft = (int)Math.Ceiling(RegenMinutes - (DateTime.UtcNow - last).TotalMinutes);
            from.SendMessage(0x22,
                $"The Hammer of Hephaestus is exhausted. " +
                $"Next charge in ~{Math.Max(0, minsLeft)} minute{(minsLeft == 1 ? "" : "s")}.");
            from.SendGump(new HammerFamiliarityGump(from, this));
            return;
        }

        // Not near a forge — show familiarity panel instead of the craft gump.
        // Double-clicking near a forge opens the craft menu as normal.
        DefBlacksmithy.CheckAnvilAndForge(from, 2, out _, out var nearForge);
        if (!nearForge)
        {
            from.SendMessage(0x59, "You must be near a forge to smith. Showing Metal Familiarity.");
            from.SendGump(new HammerFamiliarityGump(from, this));
            return;
        }

        base.OnDoubleClick(from);
    }

    public override void OnSingleClick(Mobile from)
    {
        base.OnSingleClick(from);
        ApplyPassiveRegen();

        if (_exhausted)
            LabelTo(from, "[Exhausted — regenerating slowly]");
        else
            LabelTo(from, $"[{UsesRemaining}/{MaxUses} uses — T1]");
    }

    public override void GetProperties(IPropertyList list)
    {
        ApplyPassiveRegen();
        base.GetProperties(list);

        list.Add("Society of Smiths Legacy Tool — T1");

        if (_exhausted)
        {
            list.Add("(Exhausted — regenerating slowly)");
        }
        else
        {
            list.Add("+5 Blacksmithy while equipped");

            var totalFam = _metalFamiliarity.Values.Sum();
            if (totalFam > 0)
            {
                var topMetal = _metalFamiliarity
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => HammerMetal.All.FirstOrDefault(m => m.Resource == kv.Key).Name)
                    .FirstOrDefault() ?? "Unknown";
                list.Add($"Metal Familiarity: {totalFam} strikes — most familiar: {topMetal}");
            }
            else
            {
                list.Add("Metal Familiarity: 0 forge strikes this session");
            }

            list.Add("Bonus: Use refund chance (single-click for details)");
        }
    }

    // ── Registry ──────────────────────────────────────────────────────────────

    public override void OnAdded(IEntity parent)
    {
        base.OnAdded(parent);

        PlayerMobile? pm = parent switch
        {
            PlayerMobile directPm                                    => directPm,
            Container c when c.RootParent is PlayerMobile containerPm => containerPm,
            _                                                        => null
        };

        if (pm?.Account is IAccount acct)
        {
            ClusterFRestorationRegistry.Unlock(acct, RegKey, "item_acquisition");

            if (!_exhausted)
            {
                var entry = ClusterFRestorationRegistry.GetEntry(acct, RegKey);
                if (entry != null) entry.HasActiveCopy = true;
            }
        }

        // Apply base SkillMod when entering a player's possession
        if (pm != null && !_exhausted)
            ApplyBaseSkillMod(pm);
    }

    public override void OnRemoved(IEntity parent)
    {
        RemoveBaseSkillMod();
        base.OnRemoved(parent);
    }

    public override void OnDelete()
    {
        RemoveBaseSkillMod();

        PlayerMobile? owner = Parent switch
        {
            PlayerMobile pm => pm,
            Container    c  => c.RootParent as PlayerMobile,
            _               => null
        };

        if (owner?.Account is IAccount acct)
            ClusterFRestorationRegistry.ClearActiveCopy(acct, RegKey);

        base.OnDelete();
    }

    // ── Guild instant restore ─────────────────────────────────────────────────

    public void GuildmasterRestore()
    {
        _exhausted        = false;
        UsesRemaining     = MaxUses;
        _lastRegenAtTicks = DateTime.UtcNow.Ticks;
        Hue               = FunctionalHue;
        ((IUsesRemaining)this).ShowUsesRemaining = true;
        InvalidateProperties();

        var owner = GetOwnerMobile();
        if (owner != null)
            ApplyBaseSkillMod(owner);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Tier 2 — Reinforced Hammer of Hephaestus
// ─────────────────────────────────────────────────────────────────────────────

public partial class ReinforcedHammerOfHephaestus : SmithHammer
{
    private const int    FunctionalHue    = 0x0506;
    private const int    ExhaustedHue     = 0x0415;
    private const int    MaxUses          = 400;
    private const int    RegenMinutes     = 15;
    private const int    FamCap           = 250;
    private const string RegKey           = "legacy.reinforced_hammer_of_hephaestus";

    // T2 Bonus — C: skill bonus via SkillMod
    // Total SkillMod = (sum of all familiarity / (FamCap * MetalCount)) * MaxSkillBonus
    private const double MaxSkillBonus    = 2.5;

    // ── State ─────────────────────────────────────────────────────────────────

    private bool                  _exhausted;
    private long                  _lastRegenAtTicks;
    private Dictionary<int, int>  _metalFamiliarity = new();
    private SkillMod?             _skillMod;     // NOT serialized — transient, familiarity bonus
    private SkillMod?             _baseSkillMod; // NOT serialized — transient, fixed +10 base

    // ── Constructor ───────────────────────────────────────────────────────────

    [Constructible]
    public ReinforcedHammerOfHephaestus() : base(MaxUses)
    {
        Name              = "Reinforced Hammer of Hephaestus";
        LootType          = LootType.Blessed;
        Hue               = FunctionalHue;
        _lastRegenAtTicks = DateTime.UtcNow.Ticks;
    }

    // Required by ModernUO world-load deserializer
    public ReinforcedHammerOfHephaestus(Serial serial) : base(serial) { }

    // ── Properties ────────────────────────────────────────────────────────────

    public override bool BreakOnDepletion => false;
    public          bool Exhausted        => _exhausted;

    // ── Serialization (manual) ────────────────────────────────────────────────

    public override void Serialize(IGenericWriter writer)
    {
        base.Serialize(writer);
        writer.Write(1); // version

        writer.Write(_exhausted);
        writer.Write(_lastRegenAtTicks);

        writer.Write(_metalFamiliarity.Count);
        foreach (var (k, v) in _metalFamiliarity)
        {
            writer.Write(k);
            writer.Write(v);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        base.Deserialize(reader);
        var version = reader.ReadInt();

        switch (version)
        {
            case 0:
                _exhausted        = reader.ReadBool();
                _lastRegenAtTicks = reader.ReadLong();
                _metalFamiliarity = new Dictionary<int, int>();
                break;

            case 1:
                _exhausted        = reader.ReadBool();
                _lastRegenAtTicks = reader.ReadLong();
                var count         = reader.ReadInt();
                _metalFamiliarity = new Dictionary<int, int>(count);
                for (var i = 0; i < count; i++)
                    _metalFamiliarity[reader.ReadInt()] = reader.ReadInt();
                break;
        }

        if (_lastRegenAtTicks == 0)
            _lastRegenAtTicks = DateTime.UtcNow.Ticks;
    }

    // ── Passive regen ─────────────────────────────────────────────────────────

    private void ApplyPassiveRegen()
    {
        if (UsesRemaining >= MaxUses && !_exhausted) return;

        var now     = DateTime.UtcNow;
        var elapsed = now - new DateTime(_lastRegenAtTicks, DateTimeKind.Utc);
        var earned  = (int)(elapsed.TotalMinutes / RegenMinutes);

        if (earned <= 0) return;

        var wasExhausted = _exhausted;
        UsesRemaining    = Math.Min(MaxUses, UsesRemaining + earned);
        _lastRegenAtTicks += TimeSpan.FromMinutes((long)earned * RegenMinutes).Ticks;

        if (wasExhausted && UsesRemaining > 0)
        {
            _exhausted = false;
            Hue        = FunctionalHue;
            ((IUsesRemaining)this).ShowUsesRemaining = true;
            InvalidateProperties();
        }
    }

    // ── Exhaustion ────────────────────────────────────────────────────────────

    private void TriggerExhaustion(Mobile from)
    {
        if (_exhausted) return;
        _exhausted = true;
        Hue        = ExhaustedHue;
        ((IUsesRemaining)this).ShowUsesRemaining = false;
        InvalidateProperties();
        RemoveSkillMod();
        RemoveBaseSkillMod();
        from.SendMessage(0x22,
            "The Reinforced Hammer of Hephaestus is spent. " +
            "Speak with the Blacksmith Guildmaster to restore it immediately.");
    }

    // ── Base SkillMod (Phase 4E) ──────────────────────────────────────────────

    private void ApplyBaseSkillMod(Mobile m)
    {
        RemoveBaseSkillMod();
        if (!_exhausted)
        {
            _baseSkillMod = new DefaultSkillMod(SkillName.Blacksmith, "ReinforcedHammerOfHephaestusBase", true, 10.0);
            m.AddSkillMod(_baseSkillMod);
        }
    }

    private void RemoveBaseSkillMod()
    {
        if (_baseSkillMod != null)
        {
            _baseSkillMod.Remove();
            _baseSkillMod = null;
        }
    }

    // ── Metal Familiarity + T2 C-bonus (SkillMod) ────────────────────────────

    /// <summary>
    /// Called from CraftItem.cs after each successful Blacksmithy craft.
    /// Records familiarity and refreshes the T2 SkillMod.
    /// </summary>
    public void RecordFamiliarity(Type resourceType, Mobile from)
    {
        var key = HammerMetal.InferResource(resourceType);
        if (key < 0) return;

        _metalFamiliarity.TryGetValue(key, out var cur);
        if (cur < FamCap)
        {
            _metalFamiliarity[key] = cur + 1;
            InvalidateProperties();
            UpdateSkillMod(from); // C-bonus: refresh SkillMod after each gain
        }
    }

    // ── T2 SkillMod management ────────────────────────────────────────────────

    private double GetSkillBonus()
    {
        if (_exhausted) return 0;
        var total    = _metalFamiliarity.Values.Sum();
        var maxTotal = FamCap * HammerMetal.Count;
        return Math.Round((total / (double)maxTotal) * MaxSkillBonus, 1);
    }

    private void ApplySkillMod(Mobile m)
    {
        RemoveSkillMod();
        var bonus = GetSkillBonus();
        if (bonus > 0)
        {
            _skillMod = new DefaultSkillMod(SkillName.Blacksmith, "ReinforcedHammerOfHephaestus", true, bonus);
            m.AddSkillMod(_skillMod);
        }
    }

    private void RemoveSkillMod()
    {
        if (_skillMod != null)
        {
            _skillMod.Remove();
            _skillMod = null;
        }
    }

    private void UpdateSkillMod(Mobile? from)
    {
        var owner = from ?? GetOwnerMobile();
        if (owner != null)
            ApplySkillMod(owner);
    }

    private Mobile? GetOwnerMobile()
    {
        return RootParent as Mobile ?? Parent as Mobile;
    }

    // ── Familiarity snapshots ─────────────────────────────────────────────────

    public Dictionary<int, int> GetFamiliaritySnapshot() =>
        new Dictionary<int, int>(_metalFamiliarity);

    public void LoadFamiliaritySnapshot(Dictionary<int, int> snapshot, int targetCap)
    {
        _metalFamiliarity = new Dictionary<int, int>();
        foreach (var (k, v) in snapshot)
            _metalFamiliarity[k] = Math.Min(targetCap, v);
        InvalidateProperties();
    }

    /// <summary>
    /// Merge another hammer's familiarity into this one (T3+T3 → T4 combine path).
    /// Each metal is summed then capped at targetCap.
    /// </summary>
    public void MergeFamiliarity(Dictionary<int, int> other, int targetCap)
    {
        foreach (var (k, v) in other)
        {
            _metalFamiliarity.TryGetValue(k, out var existing);
            _metalFamiliarity[k] = Math.Min(targetCap, existing + v);
        }
        InvalidateProperties();
    }

    // ── Overrides ─────────────────────────────────────────────────────────────

    public override bool OnEquip(Mobile from)
    {
        ApplyPassiveRegen();
        if (_exhausted || UsesRemaining <= 0)
        {
            from.SendMessage(0x22,
                "The Reinforced Hammer of Hephaestus is exhausted. " +
                "Wait for it to regenerate, or speak with the Blacksmith Guildmaster.");
            return false;
        }
        return base.OnEquip(from);
    }

    public override void OnDoubleClick(Mobile from)
    {
        ApplyPassiveRegen();

        if (UsesRemaining <= 0 && !_exhausted)
            TriggerExhaustion(from);

        if (_exhausted)
        {
            var last     = new DateTime(_lastRegenAtTicks, DateTimeKind.Utc);
            var minsLeft = (int)Math.Ceiling(RegenMinutes - (DateTime.UtcNow - last).TotalMinutes);
            from.SendMessage(0x22,
                $"The Reinforced Hammer of Hephaestus is exhausted. " +
                $"Next charge in ~{Math.Max(0, minsLeft)} minute{(minsLeft == 1 ? "" : "s")}.");
            from.SendGump(new HammerFamiliarityGump(from, this));
            return;
        }

        // Not near a forge — show familiarity panel instead of the craft gump.
        DefBlacksmithy.CheckAnvilAndForge(from, 2, out _, out var nearForge);
        if (!nearForge)
        {
            from.SendMessage(0x59, "You must be near a forge to smith. Showing Metal Familiarity.");
            from.SendGump(new HammerFamiliarityGump(from, this));
            return;
        }

        base.OnDoubleClick(from);
    }

    public override void OnSingleClick(Mobile from)
    {
        base.OnSingleClick(from);
        ApplyPassiveRegen();

        if (_exhausted)
            LabelTo(from, "[Exhausted — regenerating]");
        else
            LabelTo(from, $"[{UsesRemaining}/{MaxUses} uses — T2  +{10.0 + GetSkillBonus():F1} BS total]");
    }

    public override void GetProperties(IPropertyList list)
    {
        ApplyPassiveRegen();
        base.GetProperties(list);

        list.Add("Society of Smiths Legacy Tool — T2");

        if (_exhausted)
        {
            list.Add("(Exhausted — regenerating)");
        }
        else
        {
            var famBonus = GetSkillBonus();
            var totalBonus = 10.0 + famBonus;
            list.Add(famBonus > 0
                ? $"+{totalBonus:F1} Blacksmithy (+10 base, +{famBonus:F1} familiarity)"
                : "+10 Blacksmithy while equipped");
        }

        var totalFam = _metalFamiliarity.Values.Sum();
        list.Add(totalFam > 0
            ? $"Metal Familiarity: {totalFam} strikes — single-click for breakdown"
            : "Metal Familiarity: 0 forge strikes (single-click for details)");
    }

    // ── Registry ──────────────────────────────────────────────────────────────

    public override void OnAdded(IEntity parent)
    {
        base.OnAdded(parent);

        PlayerMobile? pm = parent switch
        {
            PlayerMobile directPm                                     => directPm,
            Container c when c.RootParent is PlayerMobile containerPm => containerPm,
            _                                                         => null
        };

        if (pm?.Account is IAccount acct)
        {
            ClusterFRestorationRegistry.Unlock(acct, "legacy.hammer_of_hephaestus", "item_acquisition");
            ClusterFRestorationRegistry.Unlock(acct, RegKey, "item_acquisition");

            if (!_exhausted)
            {
                var entry = ClusterFRestorationRegistry.GetEntry(acct, RegKey);
                if (entry != null) entry.HasActiveCopy = true;
            }
        }

        // Reapply SkillMods after world load or container move
        if (pm != null && !_exhausted)
        {
            ApplyBaseSkillMod(pm);
            ApplySkillMod(pm);
        }
    }

    public override void OnRemoved(IEntity parent)
    {
        RemoveBaseSkillMod();
        RemoveSkillMod();
        base.OnRemoved(parent);
    }

    public override void OnDelete()
    {
        RemoveBaseSkillMod();
        RemoveSkillMod();

        PlayerMobile? owner = Parent switch
        {
            PlayerMobile pm => pm,
            Container    c  => c.RootParent as PlayerMobile,
            _               => null
        };

        if (owner?.Account is IAccount acct)
            ClusterFRestorationRegistry.ClearActiveCopy(acct, RegKey);

        base.OnDelete();
    }

    public void GuildmasterRestore()
    {
        _exhausted        = false;
        UsesRemaining     = MaxUses;
        _lastRegenAtTicks = DateTime.UtcNow.Ticks;
        Hue               = FunctionalHue;
        ((IUsesRemaining)this).ShowUsesRemaining = true;
        InvalidateProperties();

        var owner = GetOwnerMobile();
        if (owner != null)
        {
            ApplyBaseSkillMod(owner);
            ApplySkillMod(owner);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// HammerFamiliarityGump — opened via double-click when not near a forge,
// or via single-click in the T1/T2 label.
// Shows per-metal familiarity, current bonus, and bonus tier documentation.
// ─────────────────────────────────────────────────────────────────────────────

public class HammerFamiliarityGump : Gump
{
    private const int W   = 480;
    private const int BgId = 9270;

    public HammerFamiliarityGump(Mobile from, Item hammer) : base(60, 40)
    {
        Closable   = true;
        Disposable = true;

        bool isT2              = hammer is ReinforcedHammerOfHephaestus;
        var  tier              = isT2 ? "T2" : "T1";
        var  title             = isT2 ? "Reinforced Hammer of Hephaestus" : "Hammer of Hephaestus";
        var  famCap            = isT2 ? 250 : 100;
        var  maxUses           = isT2 ? 400 : 150;
        var  usesRemaining     = ((IUsesRemaining)hammer).UsesRemaining;
        var  exhausted         = isT2
            ? ((ReinforcedHammerOfHephaestus)hammer).Exhausted
            : ((HammerOfHephaestus)hammer).Exhausted;

        Dictionary<int, int> fam = isT2
            ? ((ReinforcedHammerOfHephaestus)hammer).GetFamiliaritySnapshot()
            : ((HammerOfHephaestus)hammer).GetFamiliaritySnapshot();

        // Dynamic height: header + metal rows + footer
        int H = 130 + HammerMetal.Count * 22 + 80;

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        // ── Header ────────────────────────────────────────────────────────────

        AddLabel(W / 2 - 110, 12, 1153, title);
        AddLabel(W / 2 - 70,  28, 999,  $"Metal Familiarity — {tier}");
        AddImageTiled(10, 46, W - 20, 2, 9304);

        // ── Status row ────────────────────────────────────────────────────────

        var statusText = exhausted
            ? "(Exhausted — regenerating)"
            : $"{usesRemaining}/{maxUses} uses remaining";
        AddLabel(16, 52, exhausted ? 0x22 : 999, statusText);

        // ── Active bonus description ──────────────────────────────────────────

        AddImageTiled(10, 70, W - 20, 2, 9304);

        if (!isT2)
        {
            // T1 — D bonus
            AddLabel(16, 74, 1154, "Active Bonus — Use Refund (Tier 1):");
            AddLabel(16, 90, 999,
                "Each metal: familiarity/100 * 20% chance to refund a hammer use per craft.");
        }
        else
        {
            // T2 — C bonus
            var t2h   = (ReinforcedHammerOfHephaestus)hammer;
            var total = fam.Values.Sum();
            var max   = famCap * HammerMetal.Count;
            var bonus = Math.Round((total / (double)max) * 2.5, 1);

            AddLabel(16, 74, 1154, "Active Bonus — Blacksmithy Skill (Tier 2):");
            AddLabel(16, 90, 999,
                $"Total familiarity: {total}/{max}  |  Current bonus: +{bonus:F1} Blacksmithy (max +2.5)");
        }

        AddImageTiled(10, 108, W - 20, 2, 9304);

        // ── Per-metal rows ────────────────────────────────────────────────────

        AddLabel(16,       114, 1154, "Metal");
        AddLabel(200,      114, 1154, "Familiarity");
        AddLabel(310,      114, 1154, "Progress");
        AddLabel(W - 100,  114, 1154, "Bonus");

        var y = 134;
        foreach (var (resource, name) in HammerMetal.All)
        {
            fam.TryGetValue(resource, out var count);

            // Metal name
            var nameColor = count > 0 ? 999 : 0x666;
            AddLabel(16, y, nameColor, name);

            // Count
            AddLabel(200, y, count >= famCap ? 0x44 : 999, $"{count}/{famCap}");

            // Progress bar (10 segments)
            var filled   = (int)Math.Round(count / (double)famCap * 10);
            var barHtml  = $"<BASEFONT COLOR=#C8A000>{"█".PadRight(filled, '█')}</BASEFONT>" +
                           $"<BASEFONT COLOR=#444444>{"░".PadRight(10 - filled, '░')}</BASEFONT>";
            AddHtml(310, y - 2, 90, 20, barHtml, false, false);

            // Bonus value for this metal
            if (!isT2)
            {
                // T1 D-bonus: per-metal refund chance
                var chance = Math.Round((count / (double)famCap) * 20.0, 1);
                AddLabel(W - 100, y, count > 0 ? 0x44 : 0x666, $"{chance:F1}%");
            }
            else
            {
                // T2 C-bonus is cross-metal; show familiarity contribution instead
                AddLabel(W - 100, y, count > 0 ? 0x44 : 0x666, $"+{count}");
            }

            y += 22;
        }

        // ── Future tier stubs ─────────────────────────────────────────────────

        AddImageTiled(10, y, W - 20, 2, 9304);
        y += 6;
        AddLabel(16, y, 0x666, "T3: Resource efficiency bonus — unlocked with Tier 3 hammer");
        y += 18;
        AddLabel(16, y, 0x666, "T4: Exceptional quality bonus — unlocked with Tier 4 hammer");
        y += 22;

        // ── Footer ────────────────────────────────────────────────────────────

        AddImageTiled(10, H - 32, W - 20, 2, 9304);
        AddButton(W - 50, H - 24, 4023, 4025, 0);
        AddLabel(W - 28, H - 22, 1154, "X");
    }

    public override void OnResponse(NetState sender, in RelayInfo info) { }
}

// ─────────────────────────────────────────────────────────────────────────────
// HammerRestoreGump — opened from SmithGuildmasterGump
// ─────────────────────────────────────────────────────────────────────────────

public class HammerRestoreGump : Gump
{
    private readonly PlayerMobile _pm;

    private const int RestoreSealCost    = 10;
    private const int RestoreIronCost    = 200;
    private const int UpgradeSealCost    = 25;
    private const int UpgradeIronCost    = 500;
    private const int UpgradeValoriteCost = 100;
    private const int UpgradeGoldCost    = 10_000;
    private const int UpgradeStandingReq = 5_000;
    private const double UpgradeSkillReq = 80.0;

    private const int W    = 460;
    private const int H    = 420;
    private const int BgId = 9270;

    public HammerRestoreGump(PlayerMobile pm) : base(100, 60)
    {
        _pm = pm;

        Closable   = true;
        Disposable = true;

        AddBackground(0, 0, W, H, BgId);
        AddAlphaRegion(6, 6, W - 12, H - 12);

        AddLabel(W / 2 - 105, 12, 1153, "Hammer of Hephaestus");
        AddLabel(W / 2 - 85,  28, 999,  "Society of Smiths");
        AddImageTiled(10, 48, W - 20, 2, 9304);

        var acct = pm.Account as IAccount;
        var data = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : null;

        if (data == null)
            AddLabel(18, 60, 999, "Account data unavailable.");
        else
            BuildContent(data, acct!);

        AddImageTiled(10, H - 38, W - 20, 2, 9304);
        AddButton(18, H - 28, 4014, 4015, 1);
        AddLabel(40, H - 26, 999, "Back");
        AddButton(W - 50, H - 28, 4023, 4025, 0);
        AddLabel(W - 28, H - 26, 1154, "X");
    }

    private void BuildContent(ClusterFAccountData data, IAccount acct)
    {
        data.GuildReputation.TryGetValue("smithing", out var standing);
        data.GuildCurrency.TryGetValue("smithing",   out var seals);

        var hammer1   = FindHammer<HammerOfHephaestus>();
        var hammer2   = FindHammer<ReinforcedHammerOfHephaestus>();
        var ironInPack = _pm.Backpack?.GetAmount(typeof(IronIngot)) ?? 0;
        var bypass    = DevTestingCrystal.IsActive(_pm);

        var y = 58;

        // ── Current state ─────────────────────────────────────────────────────

        AddLabel(18, y, 1154, "Current Status"); y += 16;

        if (hammer1 == null && hammer2 == null)
        {
            AddLabel(18, y, 999, "No Hammer of Hephaestus found in your pack."); y += 16;
            AddLabel(18, y, 999, "Restore it from the Restoration Registry, or earn it via a quest.");
        }
        else if (hammer2 != null)
        {
            var status = hammer2.Exhausted ? "(Exhausted)" : $"{hammer2.UsesRemaining} uses remaining";
            AddLabel(18, y, 999, $"Reinforced Hammer of Hephaestus — T2 — {status}"); y += 16;
            var total = hammer2.GetFamiliaritySnapshot().Values.Sum();
            AddLabel(18, y, 999, $"Total Metal Familiarity: {total} forge strikes");
        }
        else if (hammer1 != null)
        {
            var status = hammer1.Exhausted ? "(Exhausted)" : $"{hammer1.UsesRemaining} uses remaining";
            AddLabel(18, y, 999, $"Hammer of Hephaestus — T1 — {status}"); y += 16;
            var total = hammer1.GetFamiliaritySnapshot().Values.Sum();
            AddLabel(18, y, 999, $"Total Metal Familiarity: {total} forge strikes");
        }

        y += 10;
        AddImageTiled(10, y, W - 20, 1, 9304); y += 8;

        // ── Instant restore ───────────────────────────────────────────────────

        if (hammer1 != null || hammer2 != null)
        {
            AddLabel(18, y, 1154, "Instant Restoration"); y += 16;
            AddLabel(18, y, 999, $"Cost: {RestoreSealCost} Smithing Seals + {RestoreIronCost} Iron Ingots"); y += 16;

            var canSeals  = bypass || seals     >= RestoreSealCost;
            var canIron   = bypass || ironInPack >= RestoreIronCost;
            var canRestore = canSeals && canIron;

            static string Clr(bool ok) => ok ? "#FFD700" : "#FF6666";
            AddHtml(18, y, W - 36, 32,
                $"<BASEFONT COLOR={Clr(canSeals)}>Seals: {seals}/{RestoreSealCost}</BASEFONT>  " +
                $"<BASEFONT COLOR={Clr(canIron)}>Iron: {ironInPack}/{RestoreIronCost}</BASEFONT>",
                false, false);
            y += 36;

            if (canRestore)
            {
                AddButton(18, y, 4011, 4012, 10);
                AddLabel(44, y + 2, 999, "Restore Hammer to Full Uses");
            }
            else
            {
                AddLabel(18, y, 0x22, "Requirements not met for instant restoration.");
            }

            y += 26;
            AddImageTiled(10, y, W - 20, 1, 9304); y += 8;
        }

        // ── T2 upgrade ────────────────────────────────────────────────────────

        if (hammer1 != null && hammer2 == null)
        {
            AddLabel(18, y, 1154, "Upgrade to T2 — Reinforced Hammer"); y += 16;

            var skill       = _pm.Skills[SkillName.Blacksmith].Value;
            var valoriteAmt = _pm.Backpack?.GetAmount(typeof(ValoriteIngot)) ?? 0;
            var goldAmt     = _pm.Backpack?.GetAmount(typeof(Gold)) ?? 0;

            var reqRank     = bypass || standing    >= UpgradeStandingReq;
            var reqSkill    = bypass || skill       >= UpgradeSkillReq;
            var reqSeals    = bypass || seals       >= UpgradeSealCost;
            var reqIron     = bypass || ironInPack  >= UpgradeIronCost;
            var reqValorite = bypass || valoriteAmt >= UpgradeValoriteCost;
            var reqGold     = bypass || goldAmt     >= UpgradeGoldCost;
            var reqHammer   = hammer1 != null && !hammer1.Exhausted;
            var allMet      = reqRank && reqSkill && reqSeals && reqIron
                           && reqValorite && reqGold && reqHammer;

            static string Clr2(bool ok) => ok ? "#FFD700" : "#FF6666";
            var fam = hammer1!.GetFamiliaritySnapshot().Values.Sum();

            AddHtml(18, y, W - 36, 130,
                $"<BASEFONT COLOR={Clr2(reqRank)}>Rank: Journeyman (5,000 Standing) — {standing:N0}/{UpgradeStandingReq:N0}</BASEFONT><BR>" +
                $"<BASEFONT COLOR={Clr2(reqSkill)}>Blacksmithy: {skill:F1}/{UpgradeSkillReq:F0} required</BASEFONT><BR>" +
                $"<BASEFONT COLOR={Clr2(reqSeals)}>Smithing Seals: {seals}/{UpgradeSealCost} required</BASEFONT><BR>" +
                $"<BASEFONT COLOR={Clr2(reqIron)}>Iron Ingots: {ironInPack}/{UpgradeIronCost} required</BASEFONT><BR>" +
                $"<BASEFONT COLOR={Clr2(reqValorite)}>Valorite Ingots: {valoriteAmt}/{UpgradeValoriteCost} required</BASEFONT><BR>" +
                $"<BASEFONT COLOR={Clr2(reqGold)}>Gold: {goldAmt:N0}/{UpgradeGoldCost:N0} required</BASEFONT><BR>" +
                $"<BASEFONT COLOR={Clr2(reqHammer)}>Hammer (non-exhausted) in pack: {(reqHammer ? "Yes" : "No")}</BASEFONT><BR>" +
                $"<BASEFONT COLOR=#AAAAAA>Familiarity carried over: {fam} forge strikes (capped at T2 max 250/metal)</BASEFONT>",
                false, true);
            y += 135;

            if (allMet)
            {
                AddButton(18, y, 4011, 4012, 20);
                AddLabel(44, y + 2, 999, "Upgrade to Reinforced Hammer of Hephaestus");
            }
            else
            {
                AddLabel(18, y, 0x22, "Requirements not met for upgrade.");
            }
        }
        else if (hammer1 == null && hammer2 == null)
        {
            AddLabel(18, y, 999, "Acquire the Hammer of Hephaestus (T1) first.");
        }
    }

    private T? FindHammer<T>() where T : SmithHammer
    {
        if (_pm.Backpack == null) return null;
        foreach (var item in _pm.Backpack.Items)
            if (item is T h) return h;
        if (_pm.FindItemOnLayer(Layer.OneHanded) is T eq) return eq;
        return null;
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 0) return;

        if (info.ButtonID == 1)
        {
            var def  = ClusterFGuildSystem.GetDefForGuildmaster(typeof(BlacksmithGuildmaster));
            var acct = _pm.Account as IAccount;
            if (def != null && acct != null)
                _pm.SendGump(new SmithGuildmasterGump(_pm, def, acct));
            return;
        }

        if (info.ButtonID == 10) HandleRestore();
        else if (info.ButtonID == 20) HandleUpgrade();
    }

    private void HandleRestore()
    {
        var acct = _pm.Account as IAccount;
        if (acct == null) return;

        var data  = ClusterFAccountPersistence.GetOrCreate(acct);
        var pack  = _pm.Backpack;
        if (pack == null) return;

        data.GuildCurrency.TryGetValue("smithing", out var seals);
        var ironAmt = pack.GetAmount(typeof(IronIngot));
        var bypass  = DevTestingCrystal.IsActive(_pm);

        if (!bypass && (seals < RestoreSealCost || ironAmt < RestoreIronCost))
        {
            _pm.SendMessage(0x22, "Requirements no longer met. Restoration cancelled.");
            _pm.SendGump(new HammerRestoreGump(_pm));
            return;
        }

        var hammer2 = FindHammer<ReinforcedHammerOfHephaestus>();
        var hammer1 = FindHammer<HammerOfHephaestus>();

        if (hammer2 == null && hammer1 == null)
        {
            _pm.SendMessage(0x22, "No Hammer of Hephaestus found in your pack.");
            _pm.SendGump(new HammerRestoreGump(_pm));
            return;
        }

        if (!bypass)
        {
            data.GuildCurrency["smithing"] = seals - RestoreSealCost;
            pack.ConsumeTotal(typeof(IronIngot), RestoreIronCost);
        }

        if (hammer2 != null) hammer2.GuildmasterRestore();
        else hammer1!.GuildmasterRestore();

        _pm.SendMessage(0x44,
            "The Guildmaster lays hands on the hammer, and it blazes to life once more.");
        _pm.PlaySound(0x35D);
        _pm.SendGump(new HammerRestoreGump(_pm));
    }

    private void HandleUpgrade()
    {
        var acct = _pm.Account as IAccount;
        if (acct == null) return;

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        var pack = _pm.Backpack;
        if (pack == null) return;

        data.GuildReputation.TryGetValue("smithing", out var standing);
        data.GuildCurrency.TryGetValue("smithing",   out var seals);
        var skill       = _pm.Skills[SkillName.Blacksmith].Value;
        var ironAmt     = pack.GetAmount(typeof(IronIngot));
        var valoriteAmt = pack.GetAmount(typeof(ValoriteIngot));
        var goldAmt     = pack.GetAmount(typeof(Gold));
        var srcHammer   = FindHammer<HammerOfHephaestus>();
        var bypass      = DevTestingCrystal.IsActive(_pm);

        if (srcHammer == null || srcHammer.Exhausted
            || (!bypass && (standing < UpgradeStandingReq || skill < UpgradeSkillReq
                || seals < UpgradeSealCost || ironAmt < UpgradeIronCost
                || valoriteAmt < UpgradeValoriteCost || goldAmt < UpgradeGoldCost)))
        {
            _pm.SendMessage(0x22, "Requirements no longer met. Upgrade cancelled.");
            _pm.SendGump(new HammerRestoreGump(_pm));
            return;
        }

        // Snapshot familiarity BEFORE deleting T1
        var famSnapshot = srcHammer.GetFamiliaritySnapshot();

        if (!bypass)
        {
            data.GuildCurrency["smithing"] = seals - UpgradeSealCost;
            pack.ConsumeTotal(typeof(IronIngot),     UpgradeIronCost);
            pack.ConsumeTotal(typeof(ValoriteIngot), UpgradeValoriteCost);
            pack.ConsumeTotal(typeof(Gold),          UpgradeGoldCost);
        }

        ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.hammer_of_hephaestus");
        srcHammer.Delete();

        var upgraded = new ReinforcedHammerOfHephaestus();
        // Carry familiarity forward — capped at T2 cap (250 per metal)
        upgraded.LoadFamiliaritySnapshot(famSnapshot, targetCap: 250);
        pack.DropItem(upgraded);

        _pm.SendMessage(0x44,
            "The Guildmaster strikes the hammer against the forge-stone — " +
            "the Reinforced Hammer of Hephaestus is yours. Your metal mastery carries forward.");
        _pm.PlaySound(0x35D);
        _pm.SendGump(new HammerRestoreGump(_pm));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// HammerBODAutoFill
//
// Called from CraftItem.cs after AddToBackpack when the tool is a
// HammerOfHephaestus or ReinforcedHammerOfHephaestus and the craft system
// is DefBlacksmithy.
//
// Logic:
//   1. Scan all SmallSmithBODs in the player's guild book + backpack.
//   2. For each open BOD, check whether the just-crafted item satisfies
//      the BOD's type, material, and quality requirements.
//   3. First matching small BOD wins — item is deleted, AmountCur incremented.
//   4. If no small BOD matches, scan LargeSmithBODs for a matching entry.
//   5. First matching large BOD entry wins — item is deleted, entry.Amount
//      incremented; notifications include piece progress and overall completion.
//
// Material matching uses CraftResource comparison for full post-Valorite support
// (Platinum through Celestial) rather than relying on BulkMaterialType range checks.
// ─────────────────────────────────────────────────────────────────────────────

public static class HammerBODAutoFill
{
    /// <summary>
    /// Entry point from CraftItem.cs.  Call after from.AddToBackpack(item).
    /// Safe to call even if item was already deleted (null-checks guard).
    /// </summary>
    public static void TryAutoFill(PlayerMobile pm, Item crafted)
    {
        if (crafted == null || crafted.Deleted) return;
        if (pm.Account is not IAccount acct) return;
        if (!ClusterFGuildSystem.IsJoined(acct, "smithing")) return;

        // Only armor, weapons, and clothing can satisfy BODs.
        var armor    = crafted as BaseArmor;
        var weapon   = crafted as BaseWeapon;
        var clothing = crafted as BaseClothing;
        if (armor == null && weapon == null && clothing == null) return;

        // Dragon scale armor uses CraftResourceType.Scales, not Metal.
        // Scale-based armor must NOT fill metal smith BODs.
        // [Future: chromatic dragon armor will have its own BOD/guild system.]
        if (armor != null && CraftResources.GetType(armor.Resource) != CraftResourceType.Metal)
            return;

        var resource  = armor?.Resource ?? weapon?.Resource ?? CraftResource.Iron;
        var material  = SmallBOD.GetMaterial(resource);
        var craftType = crafted.GetType();

        // ── Pass 1: small BODs ─────────────────────────────────────────────────
        SmallSmithBOD? smallMatch = null;

        if (pm.Backpack != null)
        {
            foreach (var item in pm.Backpack.Items)
            {
                if (item is SmithGuildBook book)
                {
                    foreach (var bod in book.Items)
                    {
                        if (bod is SmallSmithBOD sbod
                            && MatchesSmallBOD(sbod, craftType, armor, weapon, clothing, resource, material))
                        {
                            smallMatch = sbod;
                            break;
                        }
                    }
                    if (smallMatch != null) break;
                }

                if (smallMatch == null && item is SmallSmithBOD loose
                    && MatchesSmallBOD(loose, craftType, armor, weapon, clothing, resource, material))
                {
                    smallMatch = loose;
                    break;
                }
            }
        }

        if (smallMatch != null)
        {
            crafted.Delete();
            smallMatch.AmountCur++;
            pm.PlaySound(0x249);

            var label = SmallBODLabel(smallMatch);
            if (smallMatch.Complete)
                pm.SendMessage(0x44, $"[Hammer] Bulk order complete: {label}!");
            else
                pm.SendMessage(0x59,
                    $"[Hammer] Filed into bulk order: {label} ({smallMatch.AmountCur}/{smallMatch.AmountMax}).");
            return;
        }

        // ── Pass 2: large BODs ─────────────────────────────────────────────────
        LargeSmithBOD?  largeBod   = null;
        LargeBulkEntry? largeEntry = null;

        if (pm.Backpack != null)
        {
            foreach (var item in pm.Backpack.Items)
            {
                if (item is SmithGuildBook book)
                {
                    foreach (var bod in book.Items)
                    {
                        if (bod is LargeSmithBOD lbod)
                        {
                            var entry = FindLargeEntry(lbod, craftType, armor, weapon, clothing, resource);
                            if (entry != null) { largeBod = lbod; largeEntry = entry; break; }
                        }
                    }
                    if (largeBod != null) break;
                }

                if (largeBod == null && item is LargeSmithBOD looseLarge)
                {
                    var entry = FindLargeEntry(looseLarge, craftType, armor, weapon, clothing, resource);
                    if (entry != null) { largeBod = looseLarge; largeEntry = entry; }
                }

                if (largeBod != null) break;
            }
        }

        if (largeBod == null || largeEntry == null) return;

        // Consume item into the large BOD entry.
        crafted.Delete();
        largeEntry.Amount++;
        pm.PlaySound(0x249);

        var entryName = Regex.Replace(largeEntry.Details.Type?.Name ?? "Item", "(?<=[a-z])(?=[A-Z])", " ");

        if (largeBod.Complete)
        {
            pm.SendMessage(0x44, $"[Hammer] Large bulk order complete!");
        }
        else if (largeEntry.Amount >= largeBod.AmountMax)
        {
            // This entry just finished — count remaining open entries
            var remaining = 0;
            foreach (var e in largeBod.Entries)
                if (e.Amount < largeBod.AmountMax) remaining++;
            pm.SendMessage(0x59,
                $"[Hammer] {entryName} piece done — {remaining} piece{(remaining == 1 ? "" : "s")} remaining in large order.");
        }
        else
        {
            pm.SendMessage(0x59,
                $"[Hammer] Filed into large order: {entryName} ({largeEntry.Amount}/{largeBod.AmountMax}).");
        }
    }

    // ── Matching — small BODs ─────────────────────────────────────────────────

    private static bool MatchesSmallBOD(
        SmallSmithBOD    bod,
        Type             craftType,
        BaseArmor?       armor,
        BaseWeapon?      weapon,
        BaseClothing?    clothing,
        CraftResource    resource,
        BulkMaterialType material)
    {
        if (bod.Complete) return false;
        if (bod.AmountCur >= bod.AmountMax) return false;
        if (bod.Type == null) return false;

        if (craftType != bod.Type && !craftType.IsSubclassOf(bod.Type)) return false;

        // Material check: vanilla range uses BulkMaterialType; extended ores use CraftResource.
        if (!MaterialMatches(bod.Material, resource, material)) return false;

        if (bod.RequireExceptional && !IsExceptional(armor, weapon, clothing)) return false;

        return true;
    }

    // ── Matching — large BOD entries ──────────────────────────────────────────

    private static LargeBulkEntry? FindLargeEntry(
        LargeSmithBOD large,
        Type          craftType,
        BaseArmor?    armor,
        BaseWeapon?   weapon,
        BaseClothing? clothing,
        CraftResource resource)
    {
        if (large.Complete) return null;
        if (large.Entries == null) return null;

        foreach (var entry in large.Entries)
        {
            if (entry.Amount >= large.AmountMax) continue;
            if (entry.Details.Type == null)      continue;

            if (craftType != entry.Details.Type && !craftType.IsSubclassOf(entry.Details.Type)) continue;

            // Material: BOD material as CraftResource via direct mapping
            var bodResource = BulkToCraftResource(large.Material);
            if (large.Material != BulkMaterialType.None && resource != bodResource) continue;

            if (large.RequireExceptional && !IsExceptional(armor, weapon, clothing)) continue;

            return entry;
        }

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the crafted item's material satisfies the BOD's material requirement.
    /// Handles vanilla materials via BulkMaterialType range, and extended ores via CraftResource.
    /// </summary>
    private static bool MaterialMatches(BulkMaterialType bodMat, CraftResource resource, BulkMaterialType material)
    {
        if (bodMat == BulkMaterialType.None) return true; // Iron BOD — any material

        // Vanilla range (DullCopper–Valorite)
        if (bodMat is >= BulkMaterialType.DullCopper and <= BulkMaterialType.Valorite)
            return material == bodMat;

        // Extended ores — compare CraftResource directly
        return resource == BulkToCraftResource(bodMat);
    }

    private static bool IsExceptional(BaseArmor? armor, BaseWeapon? weapon, BaseClothing? clothing) =>
        armor?.Quality   == ArmorQuality.Exceptional   ||
        weapon?.Quality  == WeaponQuality.Exceptional  ||
        clothing?.Quality == ClothingQuality.Exceptional;

    /// <summary>Maps BulkMaterialType → CraftResource for all tiers including extended ores.</summary>
    private static CraftResource BulkToCraftResource(BulkMaterialType mat) => mat switch
    {
        BulkMaterialType.DullCopper  => CraftResource.DullCopper,
        BulkMaterialType.ShadowIron  => CraftResource.ShadowIron,
        BulkMaterialType.Copper      => CraftResource.Copper,
        BulkMaterialType.Bronze      => CraftResource.Bronze,
        BulkMaterialType.Gold        => CraftResource.Gold,
        BulkMaterialType.Agapite     => CraftResource.Agapite,
        BulkMaterialType.Verite      => CraftResource.Verite,
        BulkMaterialType.Valorite    => CraftResource.Valorite,
        BulkMaterialType.Platinum    => CraftResource.Platinum,
        BulkMaterialType.Toxic       => CraftResource.Toxic,
        BulkMaterialType.Blaze       => CraftResource.Blaze,
        BulkMaterialType.Frost       => CraftResource.Frost,
        BulkMaterialType.Obsidian    => CraftResource.Obsidian,
        BulkMaterialType.Mythril     => CraftResource.Mythril,
        BulkMaterialType.Adamantium  => CraftResource.Adamantium,
        BulkMaterialType.Celestial   => CraftResource.Celestial,
        _                            => CraftResource.Iron,
    };

    // ── Label helpers ─────────────────────────────────────────────────────────

    private static string SmallBODLabel(SmallSmithBOD bod)
    {
        var mat = BulkMatName(bod.Material);
        var exc = bod.RequireExceptional ? "Exceptional " : "";
        var name = Regex.Replace(bod.Type?.Name ?? "Item", "(?<=[a-z])(?=[A-Z])", " ");
        return $"{exc}{mat}{name}";
    }

    private static string BulkMatName(BulkMaterialType mat) => mat switch
    {
        BulkMaterialType.DullCopper  => "Dull Copper ",
        BulkMaterialType.ShadowIron  => "Shadow Iron ",
        BulkMaterialType.Copper      => "Copper ",
        BulkMaterialType.Bronze      => "Bronze ",
        BulkMaterialType.Gold        => "Gold ",
        BulkMaterialType.Agapite     => "Agapite ",
        BulkMaterialType.Verite      => "Verite ",
        BulkMaterialType.Valorite    => "Valorite ",
        BulkMaterialType.Platinum    => "Platinum ",
        BulkMaterialType.Toxic       => "Toxic ",
        BulkMaterialType.Blaze       => "Blaze ",
        BulkMaterialType.Frost       => "Frost ",
        BulkMaterialType.Obsidian    => "Obsidian ",
        BulkMaterialType.Adamantium  => "Adamantium ",
        BulkMaterialType.Mythril     => "Mythril ",
        BulkMaterialType.Celestial   => "Celestial ",
        _                            => "",
    };
}
