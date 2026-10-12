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
//
// cc-P71 (cc-P66 decision 4, Chase 2026-10-06: "update these to appropriate levels at 200"): five more effects keep growing
// past OSI's top, and here they scale instead of clamping. Each follows pinned up to OSI's top skill, then one shared
// diminishing-returns curve from OSI's value there to a chosen value at 200, and stays at that value past 200 (worn bonuses
// can read above the cap, D-113): Nerve Strike's paralysis chance, the stable slots Animal Taming, Animal Lore and
// Veterinary give, Fire Horn damage, Stealth steps, and the heal per bandage. Every value at 200, every OSI top skill and
// the curve's bend are one config table, clusterf.skillScaling.* in modernuo.json, written with these defaults on the first
// start. OSI's value at each top is computed from pinned's own formula at that top, so the curve always meets pinned there.
// Players only, as above. Notes: shard-migration notes/cc-P71-high-skill-effect-scaling.md.

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

    // ---------------------------------------------------------------- cc-P71: scaling to 200

    public const double ShardSkillCap = 200.0;

    // Defaults, each argued in the cc-P71 notes. Configure replaces them from modernuo.json.
    public const double DefaultCurve = 1.5;
    public const double DefaultNerveStrikeChanceAt200 = 0.95;
    public const int DefaultStableSlotsPerSkillAt200 = 6;
    public const double DefaultFireHornDamageAt200 = 1.10;
    public const int DefaultStealthStepsAt200 = 34;
    public const double DefaultBandageAverageAt200 = 72.0;

    /// <summary>clusterf.skillScaling.enabled: off, all five read exactly as pinned (and grow without bound again).</summary>
    public static bool ScalingEnabled { get; set; } = true;

    /// <summary>
    /// clusterf.skillScaling.curve, the bend k of <see cref="Curve" />: 0 is a straight line; 1.5 puts 40, 68 and 87 percent
    /// of the rise at 140, 160 and 180 (from a top of 120).
    /// </summary>
    public static double CurveBend { get; set; } = DefaultCurve;

    public static double NerveStrikeOsiTop { get; set; } = OsiBushido;
    public static double NerveStrikeChanceAt200 { get; set; } = DefaultNerveStrikeChanceAt200;
    public static double StableSlotsOsiTop { get; set; } = 120.0;
    public static int StableSlotsPerSkillAt200 { get; set; } = DefaultStableSlotsPerSkillAt200;
    public static double FireHornOsiTop { get; set; } = OsiMusicianship;
    public static double FireHornDamageAt200 { get; set; } = DefaultFireHornDamageAt200;
    public static double StealthOsiTop { get; set; } = 120.0;
    public static int StealthStepsAt200 { get; set; } = DefaultStealthStepsAt200;
    public static double BandageOsiTop { get; set; } = 120.0;
    public static double BandageAverageAt200 { get; set; } = DefaultBandageAverageAt200;

    public static void Configure()
    {
        const string k = "clusterf.skillScaling.";
        ScalingEnabled = ServerConfiguration.GetOrUpdateSetting(k + "enabled", true);
        CurveBend = Math.Clamp(ServerConfiguration.GetOrUpdateSetting(k + "curve", DefaultCurve), 0.0, 10.0);
        NerveStrikeOsiTop = Top(ServerConfiguration.GetOrUpdateSetting(k + "nerveStrike.osiTop", OsiBushido));
        NerveStrikeChanceAt200 =
            ServerConfiguration.GetOrUpdateSetting(k + "nerveStrike.chanceAt200", DefaultNerveStrikeChanceAt200);
        StableSlotsOsiTop = Top(ServerConfiguration.GetOrUpdateSetting(k + "stableSlots.osiTop", 120.0));
        StableSlotsPerSkillAt200 =
            ServerConfiguration.GetOrUpdateSetting(k + "stableSlots.perSkillAt200", DefaultStableSlotsPerSkillAt200);
        FireHornOsiTop = Top(ServerConfiguration.GetOrUpdateSetting(k + "fireHorn.osiTop", OsiMusicianship));
        FireHornDamageAt200 =
            ServerConfiguration.GetOrUpdateSetting(k + "fireHorn.damageAt200", DefaultFireHornDamageAt200);
        StealthOsiTop = Top(ServerConfiguration.GetOrUpdateSetting(k + "stealth.osiTop", 120.0));
        StealthStepsAt200 = ServerConfiguration.GetOrUpdateSetting(k + "stealth.stepsAt200", DefaultStealthStepsAt200);
        BandageOsiTop = Top(ServerConfiguration.GetOrUpdateSetting(k + "healing.osiTop", 120.0));
        BandageAverageAt200 =
            ServerConfiguration.GetOrUpdateSetting(k + "healing.averageAt200", DefaultBandageAverageAt200);

        Console.WriteLine($"ClusterF skill scaling: {DescribeScaling()}.");
    }

    private static double Top(double value) => Math.Clamp(value, 50.0, ShardSkillCap - 1.0);

    public static string DescribeScaling() =>
        !ScalingEnabled
            ? "off (Nerve Strike, stable slots, Fire Horn, Stealth steps and bandages read as pinned)"
            : $"curve {CurveBend:0.##}; at 200: Nerve Strike {NerveStrikeChanceAtTop:0.###} -> {NerveStrikeTarget:0.###}, " +
              $"stable slots per skill {StableSlotsAtTop} -> {StableSlotsTarget}, " +
              $"Fire Horn x{FireHornTarget:0.###}, Stealth steps {StealthStepsAtTop} -> {StealthStepsTarget}, " +
              $"bandage average {BandageAverageAtTop:0.##} -> {BandageTarget:0.##}";

    /// <summary>
    /// The share of the rise reached at progress t (0 at OSI's top, 1 at 200): (1 - e^(-k t)) / (1 - e^(-k)). Rises from
    /// the first point past the top, ever more slowly, and reaches exactly 1 at 200; no step and no flat stretch before it.
    /// </summary>
    public static double Curve(double t)
    {
        if (t <= 0.0)
        {
            return 0.0;
        }

        if (t >= 1.0)
        {
            return 1.0;
        }

        var bend = CurveBend;
        return bend < 1e-6 ? t : (1.0 - Math.Exp(-bend * t)) / (1.0 - Math.Exp(-bend));
    }

    /// <summary>Progress from OSI's top (0) to the shard's cap (1), held at 1 above the cap.</summary>
    public static double Progress(double value, double osiTop) =>
        Math.Clamp((value - osiTop) / (ShardSkillCap - osiTop), 0.0, 1.0);

    private static bool Scales(Mobile m, double value, double osiTop) => ScalingEnabled && m.Player && value > osiTop;

    // 1. Nerve Strike (Items/Weapons/Abilities/NerveStrike.cs:66): pinned (150 / 7 + 4 x Bushido / 7) / 100, 0.90 at 120 and
    // certain from 137.5.

    private static double PinnedNerveStrikeChance(double bushido) => (150.0 / 7.0 + 4.0 * bushido / 7.0) / 100.0;

    public static double NerveStrikeChanceAtTop => PinnedNerveStrikeChance(NerveStrikeOsiTop);

    // Never certain, never below OSI's own top.
    private static double NerveStrikeTarget => Math.Clamp(NerveStrikeChanceAt200, NerveStrikeChanceAtTop, 0.99);

    public static double NerveStrikeChance(Mobile attacker)
    {
        var bushido = attacker.Skills.Bushido.Value;

        if (!Scales(attacker, bushido, NerveStrikeOsiTop))
        {
            return PinnedNerveStrikeChance(bushido);
        }

        var top = NerveStrikeChanceAtTop;
        return top + (NerveStrikeTarget - top) * Curve(Progress(bushido, NerveStrikeOsiTop));
    }

    // 2. Stable slots (Mobiles/Vendors/NPC/AnimalTrainer.cs:58-97): Taming, Lore and Veterinary each add
    // (int)((skill - 90) / 10) from 100, 3 at 120 and 11 at 200, on top of 2 to 5 from their sum. Past the top each skill's
    // share is top + rise x curve, the three shares summed before one truncation, so the slots step about every 9 points
    // when the skills climb together (one truncation per skill would hold each share flat from 160 to 199).

    private static int PinnedStableSlots(double value) => (int)((value - 90.0) / 10);

    public static int StableSlotsAtTop => PinnedStableSlots(StableSlotsOsiTop);

    private static int StableSlotsTarget => Math.Max(StableSlotsPerSkillAt200, StableSlotsAtTop);

    /// <summary>
    /// pinnedMax is GetMaxStabled's own total; past the top the skills' pinned shares in it are replaced by the curve's.
    /// </summary>
    public static int StableSlots(Mobile from, int pinnedMax, double taming, double lore, double vet)
    {
        if (!ScalingEnabled || !from.Player)
        {
            return pinnedMax;
        }

        var top = StableSlotsAtTop;
        var rise = StableSlotsTarget - top;
        var max = pinnedMax;
        var extra = 0.0;

        foreach (var value in (ReadOnlySpan<double>)[taming, lore, vet])
        {
            if (value > StableSlotsOsiTop)
            {
                max -= PinnedStableSlots(value) - top;
                extra += rise * Curve(Progress(value, StableSlotsOsiTop));
            }
        }

        return max + (int)(extra + 1e-9);
    }

    // 3. Fire Horn (Items/Skill Items/Misc/FireHorn.cs:125-130): each bard skill's Fixed value scaled v + 2 x max(0, v - 900),
    // weighted 1 Musicianship, 3 Provocation, 3 Discordance, 1 Peacemaking. Each skill's scaled value past the top rises to
    // damageAt200 times its value at the top, so all four at 200 deal damageAt200 times the damage at all four tops, and one
    // skill alone its weight's share of that.

    private static int PinnedFireHornScaled(int fixedValue) => fixedValue + Math.Max(0, fixedValue - 900) * 2;

    private static double FireHornTarget => Math.Max(FireHornDamageAt200, 1.0);

    public static int FireHornScaled(Mobile from, int fixedValue)
    {
        if (!Scales(from, fixedValue / 10.0, FireHornOsiTop))
        {
            return PinnedFireHornScaled(fixedValue);
        }

        var top = PinnedFireHornScaled((int)Math.Round(FireHornOsiTop * 10.0));
        return (int)(top * (1.0 + (FireHornTarget - 1.0) * Curve(Progress(fixedValue / 10.0, FireHornOsiTop))));
    }

    // 4. Stealth steps (Skills/Stealth.cs:102): (int)(Stealth / 5) under AOS, 24 at 120 and 40 at 200.

    public static int StealthStepsAtTop => (int)(StealthOsiTop / 5.0);

    private static int StealthStepsTarget => Math.Max(StealthStepsAt200, StealthStepsAtTop);

    /// <summary>pinnedSteps is pinned's own count, returned unchanged below the top, for creatures and before AOS.</summary>
    public static int StealthSteps(Mobile m, int pinnedSteps)
    {
        var stealth = m.Skills.Stealth.Value;

        if (!Core.AOS || !Scales(m, stealth, StealthOsiTop))
        {
            return pinnedSteps;
        }

        var top = StealthStepsAtTop;
        return top + (int)((StealthStepsTarget - top) * Curve(Progress(stealth, StealthOsiTop)));
    }

    // 5. The heal per bandage (Items/Skill Items/Misc/Bandage.cs:387-388 pinned, AOS): min = A / 8 + H / 5 + 4,
    // max = A / 6 + H / 2.5 + 4, so the average is A x 7 / 48 + H x 0.3 + 4: 57.5 at 120/120, 93.2 at 200/200. Each of the
    // healer's two skills (Healing and Anatomy, or Veterinary and Animal Lore on a creature) is read past the top as
    // top + rise x curve, the rise chosen so 200/200 averages averageAt200. Only the heal amount reads it: the chance, cures
    // and resurrections read the whole skill.

    private const double BandageAverageSlope = 7.0 / 48.0 + 0.3;

    private static double PinnedBandageAverage(double anatomy, double healing) =>
        anatomy * 7.0 / 48.0 + healing * 0.3 + 4.0;

    public static double BandageAverageAtTop => PinnedBandageAverage(BandageOsiTop, BandageOsiTop);

    private static double BandageTarget => Math.Max(BandageAverageAt200, BandageAverageAtTop);

    public static double BandageHealSkill(Mobile healer, double value)
    {
        if (!Scales(healer, value, BandageOsiTop))
        {
            return value;
        }

        var rise = (BandageTarget - BandageAverageAtTop) / BandageAverageSlope;
        return BandageOsiTop + rise * Curve(Progress(value, BandageOsiTop));
    }
}
