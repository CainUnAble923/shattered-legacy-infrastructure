// cc-P66 Part A (batch X, cc-P54 B.4/B.5, Chase's decision 9: clamps first). Where a pinned effect read a skill with no
// upper bound, a player's skill above OSI's top made it certain or immune: never caught stealing, hiding beside a
// combatant, a 20-tile Detect Hidden sweep, bard difficulty cut by 50, a block on almost every hit, a 210-tile Tracking
// search, a Nether Cyclone that feeds its victim. Each of those reads now counts a player's skill at most at the highest
// value OSI lets a player have, so nothing changes at or below OSI's range and nothing past it grows into a certainty.
// Creatures read their skills exactly as pinned.
//
// OSI's top Value for a skill is its power-scroll cap where one exists (pinned Items/Special/Special Scrolls/
// PowerScroll.cs:9-54: Musicianship :29, Magic Resist :25, Parry :18, Stealing :40, Bushido :48, all 120) and 100
// otherwise (Hiding, Detect Hidden, Tracking). On OSI an item's skill bonus stops at the skill's cap (pinned
// Misc/AOS.cs:1357-1358 builds item mods with ObeyCap; D-113 lifted that here), so the cap is also OSI's top Value.
//
// Each method is called from one patched pinned line, named beside it. Notes: shard-migration
// notes/cc-P66-clamps-and-hook-g.md, Part A.
//
// cc-P67 (cc-P54 B.3/B.4, decision 11): four more, shipped before the skills behind them climb. Tinkering's trap damage
// and the axe's Lumberjacking damage bonus (Part A, Tinkering now climbs with the shard's metals; Lumberjacking already
// reaches 148 on the extended woods), and potion strength and the deep-water fishing finds (Part B, before Alchemy and
// Fishing get their content). Tinkering and Fishing count at most 120, OSI's tops with the power scrolls pinned does not
// carry (OSI's tinker bulk orders and High Seas fishing; pinned PowerScroll.cs:63-66 lists Fishing, commented out);
// Lumberjacking and Alchemy 100. Notes: shard-migration notes/cc-P67-craft-ceilings.md.

using System;

namespace Server;

public static class ClusterFSkillClamps
{
    public const double OsiStealing = 120.0;
    public const double OsiHiding = 100.0;
    public const double OsiDetectHidden = 100.0;
    public const double OsiMusicianship = 120.0;
    public const double OsiTracking = 100.0;
    public const double OsiMagicResist = 120.0;
    public const double OsiParry = 120.0;
    public const double OsiBushido = 120.0;
    public const double OsiTinkering = 120.0;
    public const double OsiLumberjacking = 100.0;
    public const double OsiAlchemy = 100.0;
    public const double OsiFishing = 120.0;

    /// <summary>
    /// The highest block chance pinned's CheckParry gives at OSI's tops (Items/Weapons/BaseWeapon.cs:1491-1574): no shield,
    /// a two-hander (divisor 41140), Parry and Bushido 120, +0.05 for Parry 100+, and Evasion up with GM Anatomy and
    /// Tactics (Spells/Bushido/Evasion.cs:175-205: 1 + (120 - 60) x 0.004 + 0.16 + 0.10 = 1.5). That is 0.600036. A shield
    /// tops out lower (0.35 at Parry 120).
    /// </summary>
    public static readonly double OsiMaxBlockChance =
        (OsiParry * OsiBushido / 41140.0 + 0.05) * (1.0 + (OsiBushido - 60.0) * 0.004 + 0.16 + 0.10);

    private static double AtMost(Mobile m, SkillName name, double osiTop)
    {
        var value = m.Skills[name].Value;
        return m.Player ? Math.Min(value, osiTop) : value;
    }

    /// <summary>Skills/Stealing.cs:337, caught = skill &lt; Random(150): at 120 a thief is still caught 1 time in 5.</summary>
    public static double StealingCaughtSkill(Mobile thief) => AtMost(thief, SkillName.Stealing, OsiStealing);

    /// <summary>Skills/Hiding.cs:53, range = min((100 - skill) / 2 + 8, 18): never below 8 for a player.</summary>
    public static double HidingCombatSkill(Mobile m) => AtMost(m, SkillName.Hiding, OsiHiding);

    /// <summary>Skills/DetectHidden.cs:145, radius = skill / 10: at most 10 tiles. The hider contest reads the whole skill.</summary>
    public static double DetectHiddenRangeSkill(Mobile m) => AtMost(m, SkillName.DetectHidden, OsiDetectHidden);

    /// <summary>
    /// Skills/Peacemaking.cs:164-169, Provocation.cs:133-138, Discordance.cs:155-160: difficulty -= (music - 100) / 2, so at
    /// most 10 off.
    /// </summary>
    public static double MusicianshipReductionSkill(Mobile m) => AtMost(m, SkillName.Musicianship, OsiMusicianship);

    /// <summary>Skills/Tracking/Tracking.cs:166, range = 10 + skill / 10 x 10: at most 110 tiles.</summary>
    public static double TrackingRangeSkill(Mobile m) => AtMost(m, SkillName.Tracking, OsiTracking);

    /// <summary>
    /// Spells/Mysticism/NetherCycloneSpell.cs:81, drain = (caster's two skills) / 1200 - resist / 800: a 160 resister made
    /// a 120/120 mystic's drain negative (the spell gave stamina and mana back).
    /// </summary>
    public static double NetherCycloneResistSkill(Mobile m) => AtMost(m, SkillName.MagicResist, OsiMagicResist);

    /// <summary>
    /// Engines/Craft/DefTinkering.cs:678-682: a trap's level is Tinkering / 10 and sets both its damage (TrapLevel,
    /// TrappableContainer.ExecuteTrap.cs:65, :107, :128: explosion 10-30 x level) and its disarm difficulty
    /// (TrapPower = level x 9, Skills/RemoveTrap.cs:61). Only the damage level is counted at most at OSI's top (12): the
    /// power keeps growing with Tinkering, so Remove Trap still has harder traps to train on.
    /// </summary>
    public static int TrapDamageLevel(Mobile tinker) => (int)(AtMost(tinker, SkillName.Tinkering, OsiTinkering) / 10);

    /// <summary>Items/Weapons/BaseWeapon.cs:2544, an axe's damage bonus 0.2% per Lumberjacking point + 10% at 100: at most 30%.</summary>
    public static double AxeLumberjackingSkill(Mobile m) => AtMost(m, SkillName.Lumberjacking, OsiLumberjacking);

    /// <summary>Items/Skill Items/Magical/Potions/BasePotion.cs:210, Alchemy x 10 / 33 percent potion strength: at most 30.</summary>
    public static double PotionAlchemySkill(Mobile m) => AtMost(m, SkillName.Alchemy, OsiAlchemy);

    /// <summary>
    /// Engines/Harvest/Fishing.cs:183, the deep-water finds (net, big fish, treasure map, bottle: (Fishing - 80) / 4000
    /// each, 0.5% at 100, 3% at 200): a chance that grows with Fishing counts it at most at 120 (1%). The entries whose
    /// chance falls as Fishing rises (rare fish, boots, nothing) read the whole skill.
    /// </summary>
    public static double FishingFindSkill(Mobile m, double value, bool growsWithSkill) =>
        growsWithSkill && m.Player ? Math.Min(value, OsiFishing) : value;

    /// <summary>Items/Weapons/BaseWeapon.cs:1528, :1569: a player's final block chance, at most <see cref="OsiMaxBlockChance" />.</summary>
    public static double BlockChance(Mobile defender, double chance) =>
        defender.Player ? Math.Min(chance, OsiMaxBlockChance) : chance;
}
