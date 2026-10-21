// ClusterF player skill gain. Three jobs, all in the four handlers pinned's skill checks call (Mobile.SkillCheck*Handler,
// installed by pinned Skills/SkillCheck.cs:33-40 and wrapped here):
//
// 1. Rate (since ClusterF): a use that rolls a gain rolls it up to chanceAttempts times, and a gain is repeated up to
//    amountMultiplier times. cc-P66 B: the first attempt is pinned's own handler; the others call pinned's public
//    SkillCheck.CheckSkill (SkillCheck.cs:108) with the same window, so SkillEvents.SkillUsed (raised only by the four
//    handlers, SkillCheck.cs:52, :87, :174, :207) fires once per use, not once per attempt. cc-P191 (Chase 2026-10-19,
//    "earned, without the crystal"): the shard's base rate is 2 chance attempts and a 1x amount (was 3 and 5x), so every
//    gain is pinned's own 0.1 and comes about twice as often as stock; the defaults below match. The measurement is
//    tests/Skills/SkillGainRateVerification.cs, notes cc-P191.
//
// 2. Worn bonuses (cc-P66 B, Chase's decision 7 after cc-P53 C / D-113). Pinned's no-challenge test reads Value, which
//    includes item and temporary bonuses, so a use whose window ends at the skill's cap stopped gaining at Base = cap - bonus.
//    Every pinned call that passes the cap as its maximum (BaseWeapon.cs:2524, :2527, Spell.cs:625, and the gated
//    MagerySpell.cs:55, :82, MysticSpell.cs:121; our Meditation.cs:115) is a gain-only roll, so for a player such a window
//    ends at Cap + (Value - Base) instead: the roll stops when Base reaches the cap, whatever is worn. cc-P67 Part E: craft
//    gain ceilings below the cap follow the same rule in ClusterFCraftGain, which adds the bonus itself and calls
//    CheckSkillWornBonusIncluded so it is not added twice.
//
// 3. Hook G (cc-P66 B, Chase's decision 8). A stock use that says "no challenge" (Value at or above its maximum) gains
//    nothing, so sixteen skills stopped at 100, 120 or 142 with no harder target anywhere. For the uses in TryRow, a player
//    at or above the use's stock maximum gets a gain-only roll after the stock result, on the window
//    (row minimum, Cap + (Value - Base)); the use's own outcome is the stock one, untouched. Below each use's stock maximum
//    nothing differs from pinned. Not the casting schools (Chase: a Training Ward or a quest line, later). Detect Hidden
//    rolls only when its scan found a hider or a trap (DetectHiddenFound, called from the patched DetectHidden.cs).
//    cc-P226: Arms Lore on a weapon or armor is a row keyed on the item's tier; its crafting check rolls through
//    ArmsLoreCrafted, called from the patched BaseWeapon and BaseArmor OnCraft.
//
// Notes: shard-migration notes/cc-P66-clamps-and-hook-g.md, Part B. The rows' keys are pinned literals; the fact
// SkillGainRowKeysVerification reads each pinned line in the build tree, so an upstream change to one stops the build.

using System;
using Server.Items;
using Server.Misc;
using Server.Mobiles;

namespace Server;

public static class ClusterFSkillGain
{
    private const int DefaultChanceAttempts = 2;
    private const int DefaultAmountMultiplier = 1;
    private const int MinimumMultiplier = 1;
    private const int MaximumMultiplier = 20;

    private static bool _enabled;
    private static int _chanceAttempts;
    private static int _amountMultiplier;
    private static bool _pastStockCeiling = true;

    private static SkillCheckLocationHandler _baseLocationHandler;
    private static SkillCheckDirectLocationHandler _baseDirectLocationHandler;
    private static SkillCheckTargetHandler _baseTargetHandler;
    private static SkillCheckDirectTargetHandler _baseDirectTargetHandler;

    public enum CheckKind
    {
        Location,
        Target
    }

    /// <summary>ClusterF's rate (attempts and amount). Off means pinned's rate; worn bonuses and hook G still apply.</summary>
    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public static int ChanceAttempts
    {
        get => _chanceAttempts;
        set => _chanceAttempts = ClampMultiplier(value);
    }

    public static int AmountMultiplier
    {
        get => _amountMultiplier;
        set => _amountMultiplier = ClampMultiplier(value);
    }

    /// <summary>Hook G (clusterf.skillGain.pastStockCeiling, default true).</summary>
    public static bool PastStockCeiling
    {
        get => _pastStockCeiling;
        set => _pastStockCeiling = value;
    }

    public static void Configure()
    {
        _enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.skillGain.enabled", true);
        _chanceAttempts = ClampMultiplier(
            ServerConfiguration.GetOrUpdateSetting("clusterf.skillGain.chanceAttempts", DefaultChanceAttempts)
        );
        _amountMultiplier = ClampMultiplier(
            ServerConfiguration.GetOrUpdateSetting("clusterf.skillGain.amountMultiplier", DefaultAmountMultiplier)
        );
        _pastStockCeiling = ServerConfiguration.GetOrUpdateSetting("clusterf.skillGain.pastStockCeiling", true);

        CommandSystem.Register("ClusterFSkillGain", AccessLevel.Administrator, ClusterFSkillGain_OnCommand);
    }

    [CallPriority(100)]
    public static void Initialize()
    {
        _baseLocationHandler = Mobile.SkillCheckLocationHandler;
        _baseDirectLocationHandler = Mobile.SkillCheckDirectLocationHandler;
        _baseTargetHandler = Mobile.SkillCheckTargetHandler;
        _baseDirectTargetHandler = Mobile.SkillCheckDirectTargetHandler;

        Mobile.SkillCheckLocationHandler = ClusterFSkillCheckLocation;
        Mobile.SkillCheckDirectLocationHandler = ClusterFSkillCheckDirectLocation;
        Mobile.SkillCheckTargetHandler = ClusterFSkillCheckTarget;
        Mobile.SkillCheckDirectTargetHandler = ClusterFSkillCheckDirectTarget;

        Console.WriteLine($"ClusterF skill gain: {Describe()}.");
    }

    private static string Describe() =>
        $"rate {(_enabled ? "enabled" : "disabled")}, {_chanceAttempts} chance attempt(s), {_amountMultiplier}x gain amount; " +
        $"past stock ceilings {(_pastStockCeiling ? "on" : "off")}";

    [Usage("ClusterFSkillGain")]
    [Description("Reports the active ClusterF player skill gain settings.")]
    [ShardCommand(CommandCategory.Diagnostic)]
    private static void ClusterFSkillGain_OnCommand(CommandEventArgs e) =>
        e.Mobile.SendMessage($"ClusterF skill gain: {Describe()}.");

    private static bool ClusterFSkillCheckLocation(Mobile from, SkillName skillName, double minSkill, double maxSkill)
    {
        var skill = from.Skills[skillName];

        if (skill == null || _baseLocationHandler == null)
        {
            return false;
        }

        maxSkill = EffectiveMax(from, skill, maxSkill);
        var result = ObserveAttempt(from, skill, () => _baseLocationHandler(from, skillName, minSkill, maxSkill));
        var location = LocationKey(from);

        MoreAttempts(from, skill, location, () => WindowChance(skill.Value, minSkill, maxSkill));
        PastStockCeilingRoll(from, skill, CheckKind.Location, null, location, minSkill, maxSkill);

        return result;
    }

    private static bool ClusterFSkillCheckDirectLocation(Mobile from, SkillName skillName, double chance)
    {
        var skill = from.Skills[skillName];

        if (skill == null || _baseDirectLocationHandler == null)
        {
            return false;
        }

        var result = ObserveAttempt(from, skill, () => _baseDirectLocationHandler(from, skillName, chance));
        MoreAttempts(from, skill, LocationKey(from), () => DirectChance(chance));

        return result;
    }

    private static bool ClusterFSkillCheckTarget(
        Mobile from,
        SkillName skillName,
        object target,
        double minSkill,
        double maxSkill
    )
    {
        var skill = from.Skills[skillName];

        if (skill == null || _baseTargetHandler == null)
        {
            return false;
        }

        // cc-P70: Minstrel's Bells III eases the owner's Peacemaking, Provocation and Discordance near the mule.
        var bardEase = Server.Mobiles.MuleModules.BardSkillEase(from, skillName);
        minSkill -= bardEase;
        maxSkill -= bardEase;

        maxSkill = EffectiveMax(from, skill, maxSkill);
        var result = ObserveAttempt(from, skill, () => _baseTargetHandler(from, skillName, target, minSkill, maxSkill));

        MoreAttempts(from, skill, target, () => WindowChance(skill.Value, minSkill, maxSkill));
        PastStockCeilingRoll(from, skill, CheckKind.Target, target, target, minSkill, maxSkill);

        return result;
    }

    private static bool ClusterFSkillCheckDirectTarget(Mobile from, SkillName skillName, object target, double chance)
    {
        var skill = from.Skills[skillName];

        if (skill == null || _baseDirectTargetHandler == null)
        {
            return false;
        }

        var result = ObserveAttempt(from, skill, () => _baseDirectTargetHandler(from, skillName, target, chance));
        MoreAttempts(from, skill, target, () => DirectChance(chance));

        return result;
    }

    /// <summary>
    /// cc-P66 B, decision 7: a player's window that ends at the skill's cap ends at Cap + (Value - Base) instead, so a worn
    /// bonus no longer stops the roll before Base reaches the cap. Any other maximum is returned unchanged.
    /// </summary>
    public static double EffectiveMax(Mobile from, Skill skill, double maxSkill) =>
        !_wornBonusIncluded && maxSkill == skill.Cap && WornBonus(from, skill) > 0.0
            ? skill.Cap + WornBonus(from, skill)
            : maxSkill;

    /// <summary>A player's worn and temporary bonus on <paramref name="skill"/> (Value - Base), or 0.</summary>
    public static double WornBonus(Mobile from, Skill skill) =>
        from?.Player == true && skill.Value > skill.Base ? skill.Value - skill.Base : 0.0;

    // cc-P67 Part E: set while a craft's gain roll runs on a window whose top already carries the worn bonus
    // (ClusterFCraftGain), so EffectiveMax does not add it twice when that top lands exactly on the cap (Mythril's
    // 187.5 ceiling plus a +12.5 item is 200).
    [ThreadStatic]
    private static bool _wornBonusIncluded;

    /// <summary>Mobile.CheckSkill on a window whose top already counts the worn bonus (see EffectiveMax).</summary>
    public static bool CheckSkillWornBonusIncluded(Mobile from, SkillName skill, double minSkill, double maxSkill)
    {
        _wornBonusIncluded = true;
        try
        {
            return from.CheckSkill(skill, minSkill, maxSkill);
        }
        finally
        {
            _wornBonusIncluded = false;
        }
    }

    // The chance pinned's CheckLocation/CheckTarget would roll for this window (SkillCheck.cs:58-70, :180-192), or NaN where
    // it makes no roll (too difficult, no challenge).
    private static double WindowChance(double value, double minSkill, double maxSkill) =>
        value < minSkill || value >= maxSkill || minSkill >= maxSkill
            ? double.NaN
            : (value - minSkill) / (maxSkill - minSkill);

    // As pinned's CheckDirectLocation/CheckDirectTarget (SkillCheck.cs:93-101, :213-221).
    private static double DirectChance(double chance) => chance is < 0.0 or >= 1.0 ? double.NaN : chance;

    // The anti-macro key pinned's CheckLocation builds (SkillCheck.cs:72-73).
    private static Point2D LocationKey(Mobile from)
    {
        var size = AntiMacroSystem.Settings?.LocationSize ?? 5;
        return new Point2D(from.Location.X / size, from.Location.Y / size);
    }

    // ClusterF's attempts after the first: pinned's own roll, without the handler, so SkillUsed is not raised again.
    private static void MoreAttempts(Mobile from, Skill skill, object amObj, Func<double> chance)
    {
        for (var attemptIndex = 1; attemptIndex < _chanceAttempts && ShouldBoost(from, skill); attemptIndex++)
        {
            var c = chance();

            if (double.IsNaN(c))
            {
                return;
            }

            ObserveAttempt(from, skill, () => SkillCheck.CheckSkill(from, skill, amObj, c));
        }
    }

    private static bool ObserveAttempt(Mobile from, Skill skill, Func<bool> attempt)
    {
        var before = skill.BaseFixedPoint;
        var result = attempt();

        // cc-P189: ServUO's apprentice training multiplies each gain (Regions/ApprenticeTraining.cs), whatever the rate.
        Server.Regions.ApprenticeTraining.OnGain(from, skill, before);

        if (ShouldBoost(from, skill) && skill.BaseFixedPoint > before)
        {
            ApplyAmountBonus(from, skill);
        }

        return result;
    }

    private static void ApplyAmountBonus(Mobile from, Skill skill)
    {
        for (var gainIndex = 1; gainIndex < _amountMultiplier && ShouldBoost(from, skill); gainIndex++)
        {
            var before = skill.BaseFixedPoint;
            SkillCheck.Gain(from, skill);

            if (skill.BaseFixedPoint <= before)
            {
                break;
            }

            Server.Regions.ApprenticeTraining.OnGain(from, skill, before); // cc-P189: each gain, as ServUO's Gain
        }
    }

    private static bool ShouldBoost(Mobile from, Skill skill) =>
        _enabled &&
        (from?.Player == true || from is Server.Companions.BaseCompanion { Controlled: true }) && // cc-P264: companions too
        from.Alive &&
        from.Skills.Cap > 0 &&
        skill?.Lock == SkillLock.Up &&
        skill.Base < skill.Cap;

    private static int ClampMultiplier(int value) => Math.Clamp(value, MinimumMultiplier, MaximumMultiplier);

    // ---------------------------------------------------------------- hook G

    /// <summary>
    /// The hook G window for a stock call, or false when the call is not one of the rows. (Min, Max) is the window before
    /// worn bonuses; Max is the skill's cap except for Animal Lore. Every key is a pinned literal (file:line beside it).
    /// </summary>
    public static bool TryRow(
        Skill skill, CheckKind kind, object target, double minSkill, double maxSkill, out double min, out double max
    )
    {
        min = minSkill;
        max = skill.Cap;

        var location = kind == CheckKind.Location;

        switch (skill.SkillName)
        {
            // Skills/Hiding.cs:73 (0, 100); in a friend's house (-100, 0), which stays stock.
            case SkillName.Hiding:
                return location && minSkill == 0.0 && maxSkill == 100.0;

            // Skills/Stealth.cs:96-100 (-20 + 2 x AR, 60 + 2 x AR), AR below 42 (:91). Below 142, the heaviest armor's
            // top, the armor ladder is stock: a light stealther past 60 still has to put armor on to keep gaining.
            case SkillName.Stealth:
                return location && maxSkill - minSkill == 80.0 && maxSkill >= 60.0 && skill.Value >= 142.0;

            // Skills/Snooping.cs:84 (0, 100).
            case SkillName.Snooping:
                return !location && minSkill == 0.0 && maxSkill == 100.0;

            // Skills/Poisoning.cs:124, the potion's window: Greater (60, 100), Deadly, Darkglow, Parasitic (95, 100). Lesser
            // and Regular top out below 100 and never qualify.
            case SkillName.Poisoning:
                return !location && maxSkill == 100.0 && minSkill is 60.0 or 95.0;

            // Items/Skill Items/Misc/Bandage.cs:444-445 (0, 120), the primary skill. Creature self-heals (60-90) excluded.
            case SkillName.Healing:
            case SkillName.Veterinary:
                return location && minSkill == 0.0 && maxSkill == 120.0;

            // Skills/Begging.cs:111 (0, 100).
            case SkillName.Begging:
                return !location && minSkill == 0.0 && maxSkill == 100.0;

            // Skills/ForensicEval.cs:36 (40, 100) on a mobile, :54 (0, 100) on a corpse.
            case SkillName.Forensics:
                return !location && maxSkill == 100.0 && minSkill is 0.0 or 40.0;

            // Skills/AnimalLore.cs:59, AnimalTaming.cs:372, :393 (0, 120) on a creature. Past 120 the window is the
            // creature's own tame window with no prior owners (AnimalTaming.cs:396-406: MinTameSkill - 0.1 to + 49.9), so
            // a rabbit trains nothing and Lore climbs on the creatures Taming climbs on.
            case SkillName.AnimalLore:
                {
                    if (location || minSkill != 0.0 || maxSkill != 120.0 || target is not BaseCreature bc)
                    {
                        return false;
                    }

                    min = bc.MinTameSkill - 0.1;
                    max = bc.MinTameSkill + 49.9;
                    return true;
                }

            // Items/Skill Items/Musical Instruments/BaseInstrument.cs:528 (0, 120), every bard use's Musicianship roll.
            case SkillName.Musicianship:
                return location && minSkill == 0.0 && maxSkill == 120.0;

            // Skills/Tracking/Tracking.cs:163 (21.1, 100), the passive roll after a successful track.
            case SkillName.Tracking:
                return location && minSkill == 21.1 && maxSkill == 100.0;

            // Skills/TasteID.cs:37 (0, 100) on food.
            case SkillName.TasteID:
                return !location && minSkill == 0.0 && maxSkill == 100.0 && target is Food;

            // Items/Skill Items/Camping/Kindling.cs:38 (0, 100).
            case SkillName.Camping:
                return location && minSkill == 0.0 && maxSkill == 100.0;

            // Spells/Base/SpellHelper.cs:371 (curses) and Necromancy PainSpike.cs:59, MindRot.cs:58, CorpseSkin.cs:70,
            // BloodOathSpell.cs:82, all (0, 120) and all gain-only. The Magery and Mysticism resist rolls pass the cap and
            // stop at 111 by their own gate; they are left alone.
            case SkillName.MagicResist:
                return location && minSkill == 0.0 && maxSkill == 120.0;

            // Spells/Bushido/MomentumStrike.cs:67 (RequiredSkill 70, 120).
            case SkillName.Bushido:
                return location && minSkill == 70.0 && maxSkill == 120.0;

            // Skills/ArmsLore.cs:33, :89 (0, 100) on a weapon or armor: the inspect, and cc-P226's field maintenance, which
            // rides on the same use. Past 100 the window is the item's tier (ClusterFArmsLore.Window: material, exceptional,
            // magic, +/- 50), so practice on better gear is what trains it. The swamp dragon's barding (:107) stays stock.
            case SkillName.ArmsLore:
                {
                    if (location || minSkill != 0.0 || maxSkill != 100.0 || !ClusterFArmsLore.IsGear(target))
                    {
                        return false;
                    }

                    (min, max) = ClusterFArmsLore.Window((Item)target);
                    return true;
                }

            // cc-P239 (F-14): Appraisal (Skills/ItemIdentification.cs, (0, 100) through ClusterFAppraisal) on a creature or
            // NPC. Past 100 the window is the creature's level +/- 25 on the skill's 0 to 200 scale, so appraising stronger
            // creatures is what trains it. On an item it stays stock.
            case SkillName.ItemID:
                {
                    if (location || minSkill != 0.0 || maxSkill != 100.0 || target is not BaseCreature bc)
                    {
                        return false;
                    }

                    var level = ClusterFCreatureLevels.Level(bc);
                    min = level - 25.0;
                    max = level + 25.0;
                    return true;
                }

            // Skills/Stealing.cs:285-304, :316 (weight x 10 - 22.5, + 27.5): only a full 10-stone steal, (77.5, 127.5),
            // the top cc-P57's stack fix left. The caught test is clamped first (ClusterFSkillClamps, cc-P66 A).
            case SkillName.Stealing:
                return !location && minSkill == 77.5 && maxSkill == 127.5;

            default:
                return false;
        }
    }

    /// <summary>
    /// The gain-only chance hook G rolls for a stock call, or NaN when it rolls nothing: not a player, below the call's own
    /// maximum (the stock roll already happened), not a row, or Base already at the cap.
    /// </summary>
    public static double PastStockCeilingChance(
        Mobile from, Skill skill, CheckKind kind, object target, double minSkill, double maxSkill
    )
    {
        if (!_pastStockCeiling || from?.Player != true || !from.Alive || skill == null)
        {
            return double.NaN;
        }

        var value = skill.Value;

        if (value < maxSkill || minSkill >= maxSkill ||
            !TryRow(skill, kind, target, minSkill, maxSkill, out var min, out var max))
        {
            return double.NaN;
        }

        return WindowChance(value, min, max + (value - skill.Base));
    }

    private static void PastStockCeilingRoll(
        Mobile from, Skill skill, CheckKind kind, object target, object amObj, double minSkill, double maxSkill
    )
    {
        if (!double.IsNaN(PastStockCeilingChance(from, skill, kind, target, minSkill, maxSkill)))
        {
            Roll(from, skill, amObj, () => PastStockCeilingChance(from, skill, kind, target, minSkill, maxSkill));
        }
    }

    /// <summary>
    /// Detect Hidden past its scan's stock top (Skills/DetectHidden.cs:151, :175, both (0, 100)): called by the patched scan
    /// when it found a hider or a trap (a trapped chest or container targeted, a hidden floor trap or a faction trap swept).
    /// Below 100 the scan's own roll is stock's and this rolls nothing.
    /// </summary>
    public static void DetectHiddenFound(Mobile src)
    {
        var skill = src?.Skills.DetectHidden;

        if (!double.IsNaN(DetectHiddenFoundChance(src, skill)))
        {
            Roll(src, skill, LocationKey(src), () => DetectHiddenFoundChance(src, skill));
        }
    }

    public static double DetectHiddenFoundChance(Mobile src, Skill skill)
    {
        if (!_pastStockCeiling || src?.Player != true || !src.Alive || skill == null || skill.Value < 100.0)
        {
            return double.NaN;
        }

        return WindowChance(skill.Value, 0.0, skill.Cap + (skill.Value - skill.Base));
    }

    /// <summary>
    /// cc-P226: Arms Lore past its crafting check's stock top (BaseWeapon.cs:684, BaseArmor.cs:605, both (0, 100), rolled
    /// on an exceptional craft): called by the patched OnCraft after that roll, on the new item's tier
    /// (ClusterFArmsLore.Window). Below 100 the craft's own roll is stock's and this rolls nothing.
    /// </summary>
    public static void ArmsLoreCrafted(Mobile from, Item item)
    {
        var skill = from?.Skills.ArmsLore;

        if (!double.IsNaN(ArmsLoreCraftedChance(from, skill, item)))
        {
            Roll(from, skill, LocationKey(from), () => ArmsLoreCraftedChance(from, skill, item));
        }
    }

    public static double ArmsLoreCraftedChance(Mobile from, Skill skill, Item item)
    {
        if (!_pastStockCeiling || from?.Player != true || !from.Alive || skill == null || skill.Value < 100.0 ||
            !ClusterFArmsLore.IsGear(item))
        {
            return double.NaN;
        }

        var (min, max) = ClusterFArmsLore.Window(item);
        return WindowChance(skill.Value, min, max + (skill.Value - skill.Base));
    }

    // One gain-only roll through pinned's CheckSkill, then ClusterF's further attempts when its rate is on.
    private static void Roll(Mobile from, Skill skill, object amObj, Func<double> chance)
    {
        var first = chance();
        ObserveAttempt(from, skill, () => SkillCheck.CheckSkill(from, skill, amObj, first));

        for (var attemptIndex = 1; attemptIndex < _chanceAttempts && ShouldBoost(from, skill); attemptIndex++)
        {
            var c = chance();

            if (double.IsNaN(c))
            {
                return;
            }

            ObserveAttempt(from, skill, () => SkillCheck.CheckSkill(from, skill, amObj, c));
        }
    }
}
