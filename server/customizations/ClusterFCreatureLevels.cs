// =============================================================================
// ClusterFCreatureLevels.cs
// cc-P239 (F-14): every creature and NPC has a level, every player sees a con color against it, and Item
// Identification's new job, Appraisal, tells more as the skill grows. Ours: OSI shows no creature levels.
//
// Level. OSI's own Bard difficulty (pinned BaseInstrument.GetBaseDifficulty, Items/Skill Items/Musical
// Instruments/BaseInstrument.cs:249-305) without its SE cap of 160, rounded, 1 to 200 (F-12's range): one level per
// point of difficulty, so a level reads as the Bard difficulty a bard would face. Computed every time, never stored.
// Below 160 the value is pinned's exactly (a fact checks every creature type). A hand override beats it: the type
// table (TypeOverrides), then champions (200), peerless bosses (190), and invulnerable NPCs (1, and always grey).
// The spread over all 1,011 creature types and where it goes wrong: notes/cc-P239-creature-levels-appraisal.md.
//
// Player level. F-12 is not built, so the con color reads IPlayerLevelSource (PlayerLevels). Until F-12 replaces it,
// ProvisionalPlayerLevel says the player's best combat skill, 1 to 200 (clusterf.creatureLevels.provisionalPlayerLevel,
// default true; false gives everyone clusterf.creatureLevels.fixedPlayerLevel, default 50).
//
// Con color. Seven bands by the level gap (creature minus player): grey -20 and below, green -19 to -10, blue -9 to -3,
// yellow -2 to +2, orange +3 to +9, red +10 to +19, skull +20 and up. Shown free to every player as its own overhead
// label under the name on a single click (and the client's all-names), from BaseCreature.OnSingleClick
// (BaseCreature-con-color-line.patch). The name line is pinned's own (Mobile.OnSingleClick), so its notoriety hue is
// untouched; a word goes with each color ("(even match)"), so it never reads as a notoriety color. The property list
// cannot carry it: it is built once per object and sent alike to every viewer. clusterf.creatureLevels.conLine
// (default true) switches the line off.
//
// [CreatureLevels writes every creature type's difficulty and level to a file, for staff to review outliers.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Server.Gumps;
using Server.Items;
using Server.Network;

namespace Server.Mobiles
{
    /// <summary>The player level the con color reads. F-12's build supplies its own and nothing else changes.</summary>
    public interface IPlayerLevelSource
    {
        int LevelOf(Mobile player);
    }

    /// <summary>
    /// cc-P239: until F-12, a player's level is their best combat skill (Base, whole points), 1 to 200: a Grandmaster
    /// swordsman is level 100, a fresh character level 1.
    /// </summary>
    public sealed class ProvisionalPlayerLevel : IPlayerLevelSource
    {
        public static readonly SkillName[] CombatSkills =
        {
            SkillName.Archery, SkillName.Fencing, SkillName.Macing, SkillName.Swords, SkillName.Wrestling,
            SkillName.Throwing, SkillName.Magery, SkillName.Necromancy, SkillName.Chivalry, SkillName.Bushido,
            SkillName.Ninjitsu, SkillName.Spellweaving, SkillName.Mysticism, SkillName.AnimalTaming, SkillName.Provocation
        };

        public int LevelOf(Mobile player)
        {
            if (!ClusterFCreatureLevels.ProvisionalPlayerLevelEnabled)
            {
                return ClusterFCreatureLevels.ClampLevel(ClusterFCreatureLevels.FixedPlayerLevel);
            }

            var best = 0.0;
            foreach (var name in CombatSkills)
            {
                var skill = player?.Skills[name];
                if (skill != null && skill.Base > best)
                {
                    best = skill.Base;
                }
            }

            return ClusterFCreatureLevels.ClampLevel((int)best);
        }
    }

    public enum ConColor { Grey, Green, Blue, Yellow, Orange, Red, Skull }

    public sealed record CreatureLevelRow(Type Type, string Name, double Difficulty, double Uncapped, int Computed, int Level, string Why);

    public static class ClusterFCreatureLevels
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 200;

        // The level each override rule gives.
        public const int ChampionLevel = 200;
        public const int PeerlessLevel = 190;
        public const int InvulnerableLevel = 1;

        public static bool ConLineEnabled { get; set; } = true;
        public static bool ProvisionalPlayerLevelEnabled { get; set; } = true;
        public static int FixedPlayerLevel { get; set; } = 50;

        public static IPlayerLevelSource PlayerLevels { get; set; } = new ProvisionalPlayerLevel();

        /// <summary>Hand levels by exact type; checked before every other rule. Story NPCs and bosses the rules miss.</summary>
        public static readonly Dictionary<Type, int> TypeOverrides = new()
        {
            [typeof(Harrower)] = 200, // the end of Doom's champion spawns, but a BaseCreature, not a BaseChampion
        };

        public static void Configure()
        {
            ConLineEnabled = ServerConfiguration.GetOrUpdateSetting("clusterf.creatureLevels.conLine", true);
            ProvisionalPlayerLevelEnabled = ServerConfiguration.GetOrUpdateSetting("clusterf.creatureLevels.provisionalPlayerLevel", true);
            FixedPlayerLevel = ServerConfiguration.GetOrUpdateSetting("clusterf.creatureLevels.fixedPlayerLevel", 50);
            CommandSystem.Register("CreatureLevels", AccessLevel.Administrator, OnReportCommand);
        }

        public static int ClampLevel(int level) => Math.Clamp(level, MinLevel, MaxLevel);

        /// <summary>
        /// Pinned's GetBaseDifficulty (BaseInstrument.cs:249-305), step for step, without its last line (the SE cap of 160).
        /// It calls pinned's own helpers, so a change to them reaches both.
        /// </summary>
        public static double UncappedDifficulty(Mobile m)
        {
            var val = m.HitsMax * (Core.LBR ? 1.6 : 0.625) + m.StamMax + m.ManaMax;

            val += m.SkillsTotal / 10.0;

            if (val > 700)
            {
                val = 700 + (int)((val - 700) * (3.0 / 11));
            }

            var bc = m as BaseCreature;

            if (BaseInstrument.IsMageryCreature(bc))
            {
                val += 100;
            }

            if (BaseInstrument.IsFireBreathingCreature(bc))
            {
                val += 100;
            }

            if (BaseInstrument.IsPoisonImmune(bc))
            {
                val += 100;
            }

            if (m is VampireBat or VampireBatFamiliar)
            {
                val += 100;
            }

            val += BaseInstrument.GetPoisonLevel(bc) * 20;

            val /= 10;

            if (bc?.IsParagon == true)
            {
                val += 40.0;
            }

            return val;
        }

        public static int ComputedLevel(Mobile m) => ClampLevel((int)Math.Round(UncappedDifficulty(m), MidpointRounding.AwayFromZero));

        public static int Level(Mobile m) => Level(m, out _);

        public static int Level(Mobile m, out string why)
        {
            if (m is not BaseCreature bc)
            {
                why = "a player: their own level";
                return PlayerLevel(m);
            }

            if (TypeOverrides.TryGetValue(bc.GetType(), out var level))
            {
                why = "override table";
                return ClampLevel(level);
            }

            if (bc is BaseChampion)
            {
                why = "a champion";
                return ChampionLevel;
            }

            if (bc is BasePeerless)
            {
                why = "a peerless boss";
                return PeerlessLevel;
            }

            if (bc.IsInvulnerable)
            {
                why = "invulnerable: not a fight";
                return InvulnerableLevel;
            }

            var d = UncappedDifficulty(bc);
            why = $"Bard difficulty {d:F1}" + (bc.IsParagon ? " (a paragon: OSI's +40 is in it)" : "");
            return ClampLevel((int)Math.Round(d, MidpointRounding.AwayFromZero));
        }

        public static int PlayerLevel(Mobile m) => ClampLevel(PlayerLevels.LevelOf(m));

        public static ConColor Con(int playerLevel, int creatureLevel)
        {
            var gap = creatureLevel - playerLevel;
            return gap switch
            {
                <= -20 => ConColor.Grey,
                <= -10 => ConColor.Green,
                <= -3  => ConColor.Blue,
                <= 2   => ConColor.Yellow,
                <= 9   => ConColor.Orange,
                <= 19  => ConColor.Red,
                _      => ConColor.Skull
            };
        }

        public static ConColor Con(Mobile viewer, Mobile target) =>
            target is BaseCreature { IsInvulnerable: true } ? ConColor.Grey : Con(PlayerLevel(viewer), Level(target));

        // hues.mul (client 7.0): grey 152,152,152; green 24,192,0; blue 40,88,192; yellow 192,192,0; orange 192,80,0;
        // red 192,24,0; skull 152,0,32.
        public static int Hue(ConColor c) => c switch
        {
            ConColor.Grey   => 0x3B2,
            ConColor.Green  => 0x44,
            ConColor.Blue   => 0x63,
            ConColor.Yellow => 0x35,
            ConColor.Orange => 0x2B,
            ConColor.Red    => 0x26,
            _               => 0x20
        };

        public static string Word(ConColor c) => c switch
        {
            ConColor.Grey   => "(trivial)",
            ConColor.Green  => "(easy)",
            ConColor.Blue   => "(fair)",
            ConColor.Yellow => "(even match)",
            ConColor.Orange => "(tough)",
            ConColor.Red    => "(dangerous)",
            _               => "(deadly)"
        };

        /// <summary>BaseCreature-con-color-line.patch: after pinned's name label, the con line, to that viewer only.</summary>
        public static void OnSingleClick(BaseCreature bc, Mobile from)
        {
            if (!ConLineEnabled || bc == null || bc.Deleted || from == null || from == bc || !from.Player || from.NetState == null)
            {
                return;
            }

            var con = Con(from, bc);
            bc.PrivateOverheadMessage(MessageType.Label, Hue(con), true, Word(con), from.NetState);
        }

        // ---- the report ---------------------------------------------------------------------------------------------

        /// <summary>Every creature type there is (pinned and ours) that can be made with no arguments.</summary>
        public static List<Type> CreatureTypes() =>
            (AssemblyHandler.Assemblies ?? new[] { typeof(BaseCreature).Assembly })
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(BaseCreature).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null)
            .Distinct()
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .ToList();

        /// <summary>Makes one of each type, scores it and deletes it. A type that cannot be made is a failure line.</summary>
        public static List<CreatureLevelRow> BuildReport(out List<string> failures)
        {
            failures = new List<string>();
            var rows = new List<CreatureLevelRow>();

            foreach (var t in CreatureTypes())
            {
                BaseCreature bc = null;
                try
                {
                    bc = (BaseCreature)Activator.CreateInstance(t);
                    var level = Level(bc, out var why);
                    rows.Add(new CreatureLevelRow(
                        t, bc.Name ?? t.Name, BaseInstrument.GetBaseDifficulty(bc), UncappedDifficulty(bc), ComputedLevel(bc), level, why
                    ));
                }
                catch (Exception e)
                {
                    failures.Add($"{t.FullName}: {e.GetType().Name}: {e.Message}");
                }
                finally
                {
                    try
                    {
                        bc?.Delete();
                    }
                    catch
                    {
                        // a creature whose delete throws is still reported; the report goes on
                    }
                }
            }

            rows.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level) : string.CompareOrdinal(a.Type.FullName, b.Type.FullName));
            return rows;
        }

        public static string ReportText(List<CreatureLevelRow> rows, List<string> failures)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# Creature levels (cc-P239, F-14): {rows.Count} creature types, {failures.Count} could not be made.");
            sb.AppendLine("# level, computed level, pinned's Bard difficulty (capped at 160), uncapped, type, name, why");
            foreach (var r in rows)
            {
                sb.AppendLine($"{r.Level,4} {r.Computed,4} {r.Difficulty,7:F1} {r.Uncapped,8:F1}  {r.Type.FullName}  \"{r.Name}\"  {r.Why}");
            }

            foreach (var f in failures)
            {
                sb.AppendLine("FAILED " + f);
            }

            return sb.ToString();
        }

        [Usage("CreatureLevels")]
        [Description("Writes every creature type's Bard difficulty and level to creature-levels.txt in the temp folder (one of each is made and deleted).")]
        [ShardCommand(CommandCategory.Diagnostic, Shard = CommandShard.TestFirst)]
        private static void OnReportCommand(CommandEventArgs e)
        {
            var rows = BuildReport(out var failures);
            var path = Path.Combine(Path.GetTempPath(), "creature-levels.txt");
            File.WriteAllText(path, ReportText(rows, failures));

            int Of(Type t) => rows.FirstOrDefault(r => r.Type == t)?.Level ?? 0;
            e.Mobile.SendMessage($"{rows.Count} creature types, {failures.Count} could not be made. Written to {path}.");
            e.Mobile.SendMessage($"Check: rat {Of(typeof(Rat))}, dragon {Of(typeof(Dragon))}, ancient wyrm {Of(typeof(AncientWyrm))}.");
        }
    }

    /// <summary>
    /// cc-P239 (F-14): Item Identification's new job, renamed Appraisal for everyone. On a creature or NPC
    /// (ItemIdentification-appraisal.patch) it tells more as the skill grows; on an item and on a player it is stock.
    ///   0 to 29.9   the con color and one vague line
    ///   30 to 59.9  adds the level and a line in the style of Anatomy and Animal Lore
    ///   60 to 89.9  adds hits, the main stats and the resistances as short text
    ///   90 and up   adds a stat card (AppraisalCardGump)
    /// The use rolls Appraisal (0, 100) for gain; past 100 it gains on creatures near its level (ClusterFSkillGain hook G,
    /// the row for ItemID), as P191's skills do. Its result does not gate what is shown: the steps are the skill's.
    /// </summary>
    public static class ClusterFAppraisal
    {
        public const string NewName = "Appraisal";

        public const double LevelStep = 30.0;
        public const double StatsStep = 60.0;
        public const double CardStep = 90.0;

        public static void Initialize()
        {
            // Data/skills.json says "Item Identification" (SkillsInfo.Configure loads it); the client says Appraisal
            // through the player package's skills.mul copy (Build-UoOverrides.ps1, Build-Skills).
            SkillInfo.Table[(int)SkillName.ItemID].Name = NewName;
        }

        public static bool TryAppraise(Mobile from, Mobile target)
        {
            if (from == null || target is not BaseCreature bc || bc.Deleted)
            {
                return false;
            }

            from.CheckTargetSkill(SkillName.ItemID, bc, 0.0, 100.0);

            var skill = from.Skills[SkillName.ItemID].Value;
            var con = ClusterFCreatureLevels.Con(from, bc);
            var lines = Describe(from, bc, skill);
            for (var i = 0; i < lines.Count; i++)
            {
                from.SendMessage(i == 0 ? ClusterFCreatureLevels.Hue(con) : 0x3B2, lines[i]);
            }

            if (skill >= CardStep)
            {
                from.CloseGump<AppraisalCardGump>();
                from.SendGump(new AppraisalCardGump(from, bc));
            }

            return true;
        }

        public static List<string> Describe(Mobile from, BaseCreature bc, double skill)
        {
            var con = ClusterFCreatureLevels.Con(from, bc);
            var lines = new List<string> { $"{bc.Name}: {VagueLine(bc, con)}" };

            if (skill >= LevelStep)
            {
                lines.Add($"Level {ClusterFCreatureLevels.Level(bc)}. {LoreLine(bc)}");
            }

            if (skill >= StatsStep)
            {
                lines.Add($"Hits {bc.Hits}/{bc.HitsMax}, Str {bc.Str}, Dex {bc.Dex}, Int {bc.Int}.");
                lines.Add(
                    $"Resists: physical {bc.PhysicalResistance}, fire {bc.FireResistance}, cold {bc.ColdResistance}, " +
                    $"poison {bc.PoisonResistance}, energy {bc.EnergyResistance}."
                );
            }

            return lines;
        }

        public static string VagueLine(BaseCreature bc, ConColor con) =>
            bc.IsInvulnerable
                ? "It is no threat to anyone."
                : con switch
                {
                    ConColor.Grey   => "It looks harmless to you.",
                    ConColor.Green  => "It should give you little trouble.",
                    ConColor.Blue   => "You could take it.",
                    ConColor.Yellow => "It looks like an even match.",
                    ConColor.Orange => "It looks like it could hurt you.",
                    ConColor.Red    => "It looks dangerous.",
                    _               => "It looks like it would kill you."
                };

        // In the style of Anatomy and Animal Lore: words, not numbers.
        public static string LoreLine(BaseCreature bc)
        {
            var str = bc.Str switch { < 50 => "frail", < 100 => "fit", < 200 => "strong", < 400 => "mighty", _ => "colossal" };
            var dex = bc.Dex switch { < 50 => "slow", < 100 => "quick", < 150 => "agile", _ => "lightning fast" };
            var mind = bc.Int switch { < 50 => "a dim", < 100 => "a sharp", < 200 => "a clever", _ => "a brilliant" };
            return $"It looks {str} and {dex}, with {mind} mind.";
        }

        // ---- the stat card's text, one line each --------------------------------------------------------------------

        private static readonly Regex Words = new("(?<=[a-z])(?=[A-Z])", RegexOptions.Compiled);

        public static List<string> CardLines(Mobile from, BaseCreature bc)
        {
            var con = ClusterFCreatureLevels.Con(from, bc);
            var level = ClusterFCreatureLevels.Level(bc, out var why);
            var lines = new List<string>
            {
                $"Level {level} {ClusterFCreatureLevels.Word(con)}",
                $"Hits {bc.Hits}/{bc.HitsMax}   Stamina {bc.Stam}/{bc.StamMax}   Mana {bc.Mana}/{bc.ManaMax}",
                $"Str {bc.Str}   Dex {bc.Dex}   Int {bc.Int}",
                $"Resists: Physical {bc.PhysicalResistance}%  Fire {bc.FireResistance}%  Cold {bc.ColdResistance}%  " +
                $"Poison {bc.PoisonResistance}%  Energy {bc.EnergyResistance}%"
            };

            var damage = new List<string>();
            void Dmg(string name, int pct)
            {
                if (pct > 0)
                {
                    damage.Add($"{name} {pct}%");
                }
            }

            Dmg("Physical", bc.PhysicalDamage);
            Dmg("Fire", bc.FireDamage);
            Dmg("Cold", bc.ColdDamage);
            Dmg("Poison", bc.PoisonDamage);
            Dmg("Energy", bc.EnergyDamage);
            lines.Add($"Damage {bc.DamageMin}-{bc.DamageMax}: {(damage.Count > 0 ? string.Join("  ", damage) : "none")}");

            var special = new List<string>();
            if (BaseInstrument.IsMageryCreature(bc))
            {
                special.Add($"Casts spells (Magery {bc.Skills.Magery.Value:F0})");
            }

            if (BaseInstrument.IsFireBreathingCreature(bc))
            {
                special.Add("Breathes fire");
            }

            if (bc.HitPoison != null)
            {
                special.Add($"Poisons on hit ({bc.HitPoison.Name})");
            }

            if (bc.PoisonImmune != null)
            {
                special.Add($"Immune to {bc.PoisonImmune.Name} poison");
            }

            foreach (var ability in bc.GetMonsterAbilities() ?? Array.Empty<MonsterAbility>())
            {
                var name = Words.Replace(ability.GetType().Name, " ");
                if (!special.Contains(name) && ability.AbilityType != MonsterAbilityType.FireBreath)
                {
                    special.Add(name);
                }
            }

            var weapon = bc.GetWeaponAbility();
            if (weapon != null)
            {
                special.Add(Words.Replace(weapon.GetType().Name, " "));
            }

            if (bc.BardImmune)
            {
                special.Add("Unmoved by music");
            }

            lines.Add("Special: " + (special.Count > 0 ? string.Join(", ", special) : "none"));

            var kind = new List<string>();
            if (bc.IsParagon)
            {
                kind.Add("Paragon");
            }

            if (bc is BaseChampion || bc is BasePeerless || ClusterFCreatureLevels.TypeOverrides.ContainsKey(bc.GetType()))
            {
                kind.Add("Boss");
            }

            lines.Add(kind.Count > 0 ? $"{string.Join(", ", kind)} ({why})" : $"Neither paragon nor boss ({why})");
            return lines;
        }
    }

    /// <summary>cc-P239: Appraisal at 90 and up. Read only; one per player (a new one replaces it).</summary>
    public sealed class AppraisalCardGump : Gump
    {
        private const int W = 460;

        public AppraisalCardGump(Mobile from, BaseCreature bc) : base(80, 60)
        {
            Creature = bc;
            Lines.AddRange(ClusterFAppraisal.CardLines(from, bc));

            var h = 50 + Lines.Count * 22;
            AddBackground(0, 0, W, h, 9270);
            AddAlphaRegion(6, 6, W - 12, h - 12);
            AddLabel(18, 14, ClusterFCreatureLevels.Hue(ClusterFCreatureLevels.Con(from, bc)), bc.Name ?? bc.GetType().Name);
            AddImageTiled(12, 36, W - 24, 2, 9304);

            for (var i = 0; i < Lines.Count; i++)
            {
                AddLabelCropped(18, 44 + i * 22, W - 36, 20, 1153, Lines[i]);
            }
        }

        public BaseCreature Creature { get; }
        public List<string> Lines { get; } = new();
    }
}
