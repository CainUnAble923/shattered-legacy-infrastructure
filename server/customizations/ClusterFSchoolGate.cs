// cc-P714: initiation gates for spell schools (P709's design, Chase's decisions of 2026-10-27; shard-migration
// notes/cc-P714-gated-skills.md). Chivalry and Necromancy each need a teacher's quest before a character can cast or
// train them. General by design: a school is a row in Schools (its skill, its id, its teacher), the quest calls Grant,
// and the permission is a per-character flag in SchoolGateStore. Adding Bushido or Ninjitsu later is a row and a quest.
//
// What the gate does, for a player character (staff and creatures are never gated) without the school's permission:
//   - Casting: Spell.Cast refuses any spell whose CastSkill is the school (server/patches/Spell-school-gate.patch), so a
//     book, a scroll, a hotkey or a macro all meet the same check. The refusal names the teacher, at most every 5 s.
//   - Gain: the four skill-check handlers ClusterFSkillGain installs return false for the school before any roll.
//   - Points from elsewhere: the school's cap is held at the character's own base (0 for a new character) wherever the
//     shard's cap policy applies caps (ClusterFSkillCaps.Apply: login, world load, the command), so NPC teaching,
//     Scrolls of Transcendence and soulstones find no room. A power scroll for the school is refused before it is used
//     (server/patches/PowerScroll-school-gate.patch). Nothing is taken away: a base above zero stays as it is.
//   - Granting gives permission only: the skill stays where it is and its cap goes back to the shard's normal cap.
//
// The permission is kept for the character's life: death, logout, restarts, resets, decay, karma and guild changes do
// not touch it. A deleted character's rows are dropped at the next save. Both schools may be held at once.
//
// On by default on the test shard only (TestCenter.Enabled). clusterf.schoolGates.enabled = on or off overrides it;
// [SchoolGate on|off|auto sets it. Off, every check passes and character creation is stock (CharacterCreation.cs).

using System;
using System.Collections.Generic;
using System.IO;
using Server.Commands;
using Server.Logging;
using Server.Mobiles;
using Server.Spells;
using Server.Targeting;

namespace Server;

public sealed record GatedSchool(string Id, SkillName Skill, string Name, string Teacher, string Place, string Quest);

/// <summary>cc-P714: one character's progress in one school's initiation quest (ClusterFInitiation).</summary>
public sealed class InitiationProgress
{
    public int Step;          // the quest's own step numbers; 0 not started
    public int Count;         // the step's counter (scavengers driven off, plates recovered)
    public int Sites;         // bitmask of field sites done
    public int Mark;          // a number the step records (the tithing balance when the tithe step began)
    public bool KitGiven;     // the one-time supplies were delivered
    public bool IntroViewed;  // the tutorial's demonstration was shown (not a permission)
}

public class SchoolGateStore : GenericPersistence
{
    // 0: cc-P714.
    public const int CurrentVersion = 0;

    private static SchoolGateStore _instance;

    internal static readonly Dictionary<Serial, HashSet<string>> Permissions = new();
    internal static readonly Dictionary<(Serial, string), InitiationProgress> Progress = new();
    internal static string Mode = "auto";

    public SchoolGateStore() : base("SchoolGates", 10)
    {
    }

    public static SchoolGateStore Instance => _instance ??= new SchoolGateStore();

    private static bool Alive(Serial s) => World.FindMobile(s) is { Deleted: false };

    public override void Serialize(IGenericWriter writer)
    {
        var deadPermissions = new List<Serial>();
        foreach (var serial in Permissions.Keys)
        {
            if (!Alive(serial))
            {
                deadPermissions.Add(serial);
            }
        }

        foreach (var serial in deadPermissions)
        {
            Permissions.Remove(serial);
        }

        var deadProgress = new List<(Serial, string)>();
        foreach (var key in Progress.Keys)
        {
            if (!Alive(key.Item1))
            {
                deadProgress.Add(key);
            }
        }

        foreach (var key in deadProgress)
        {
            Progress.Remove(key);
        }

        writer.WriteEncodedInt(CurrentVersion);
        writer.Write(Mode);
        writer.WriteEncodedInt(Permissions.Count);
        foreach (var (serial, schools) in Permissions)
        {
            writer.Write(serial);
            writer.WriteEncodedInt(schools.Count);
            foreach (var id in schools)
            {
                writer.Write(id);
            }
        }

        writer.WriteEncodedInt(Progress.Count);
        foreach (var ((serial, id), p) in Progress)
        {
            writer.Write(serial);
            writer.Write(id);
            writer.WriteEncodedInt(p.Step);
            writer.WriteEncodedInt(p.Count);
            writer.WriteEncodedInt(p.Sites);
            writer.WriteEncodedInt(p.Mark);
            writer.Write(p.KitGiven);
            writer.Write(p.IntroViewed);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"SchoolGateStore version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        Mode = reader.ReadString() ?? "auto";
        Permissions.Clear();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var serial = reader.ReadSerial();
            var n = reader.ReadEncodedInt();
            var set = new HashSet<string>(StringComparer.Ordinal);
            for (var j = 0; j < n; j++)
            {
                set.Add(reader.ReadString());
            }

            Permissions[serial] = set;
        }

        Progress.Clear();
        count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var serial = reader.ReadSerial();
            var id = reader.ReadString();
            Progress[(serial, id)] = new InitiationProgress
            {
                Step = reader.ReadEncodedInt(),
                Count = reader.ReadEncodedInt(),
                Sites = reader.ReadEncodedInt(),
                Mark = reader.ReadEncodedInt(),
                KitGiven = reader.ReadBool(),
                IntroViewed = reader.ReadBool()
            };
        }
    }
}

public static class ClusterFSchoolGate
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(ClusterFSchoolGate));

    public static readonly TimeSpan RefusalInterval = TimeSpan.FromSeconds(5);

    /// <summary>The gated schools. A later gate (Bushido, Ninjitsu) is a new row here and a quest that calls Grant.</summary>
    public static readonly GatedSchool[] Schools =
    [
        new("chivalry", SkillName.Chivalry, "Chivalry", "Sister Aveline", "Fort Whitestone", "A Place on the Watch"),
        new("necromancy", SkillName.Necromancy, "Necromancy", "Oren Pike", "the Dhampire Ruins", "Names for the Last Door")
    ];

    /// <summary>Whether the shard counts as the test shard (TestCenter.Enabled); a seam for the test host.</summary>
    public static Func<bool> IsTestShard { get; set; } = () => Server.Misc.TestCenter.Enabled;

    /// <summary>Every line the gate says to a player passes through here as well, so the tests can read them.</summary>
    public static Action<Mobile, string> Said { get; set; }

    private static readonly Dictionary<(Serial, SkillName), long> _lastRefusal = new();

    /// <summary>For the tests: the next refusal is said whatever the interval.</summary>
    public static void ForgetRefusals() => _lastRefusal.Clear();

    public static void Configure()
    {
        _ = SchoolGateStore.Instance;
        var setting = ServerConfiguration.GetSetting("clusterf.schoolGates.enabled", "");
        if (setting is "on" or "off")
        {
            SchoolGateStore.Mode = setting;
        }

        CommandSystem.Register("SchoolGate", AccessLevel.Administrator, SchoolGate_OnCommand);
    }

    /// <summary>on, off or auto (the test shard only).</summary>
    public static string Mode
    {
        get => SchoolGateStore.Mode;
        set => SchoolGateStore.Mode = value is "on" or "off" ? value : "auto";
    }

    public static bool Enabled => Mode switch
    {
        "on" => true,
        "off" => false,
        _ => IsTestShard()
    };

    public static GatedSchool Find(SkillName skill)
    {
        foreach (var s in Schools)
        {
            if (s.Skill == skill)
            {
                return s;
            }
        }

        return null;
    }

    public static GatedSchool Find(string id)
    {
        foreach (var s in Schools)
        {
            if (string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return s;
            }
        }

        return null;
    }

    /// <summary>Whether the character holds the school's permission (the stored flag, whatever the switch says).</summary>
    public static bool HasPermission(Mobile m, GatedSchool school) =>
        m != null && school != null && SchoolGateStore.Permissions.TryGetValue(m.Serial, out var set) && set.Contains(school.Id);

    /// <summary>
    /// False only when the gate is on, the skill is a gated school, the mobile is a player character below staff, and it
    /// has not earned the school.
    /// </summary>
    public static bool IsPermitted(Mobile m, SkillName skill)
    {
        if (!Enabled || m is not PlayerMobile || m.AccessLevel > AccessLevel.Player)
        {
            return true;
        }

        var school = Find(skill);
        return school == null || HasPermission(m, school);
    }

    /// <summary>Rook's lead for graduates and skippers (P709's text, with Oren Pike as the Keepers' local teacher).</summary>
    public const string RookLead =
        "To learn Chivalry, speak with Sister Aveline at Fort Whitestone. To learn Necromancy, speak with Oren Pike, clerk of the Keepers of the Last Door, at the Dhampire Ruins. " +
        "Each teacher gives you a quest before you can train or cast that school's spells. You can learn both.";

    public static string RefusalLine(GatedSchool s) =>
        $"You have not learned {s.Name}. {s.Teacher} at {s.Place} teaches it: finish the quest {s.Quest} first. " +
        $"A book or scroll alone cannot teach you.";

    public static void Say(Mobile m, string line)
    {
        m.SendMessage(0x22, line);
        Said?.Invoke(m, line);
    }

    /// <summary>Tells the player which teacher to visit, at most once every RefusalInterval.</summary>
    public static void Refuse(Mobile m, SkillName skill)
    {
        var school = Find(skill);
        if (school == null)
        {
            return;
        }

        var now = Core.TickCount;
        if (_lastRefusal.TryGetValue((m.Serial, skill), out var last) && now - last < (long)RefusalInterval.TotalMilliseconds)
        {
            return;
        }

        _lastRefusal[(m.Serial, skill)] = now;
        Say(m, RefusalLine(school));
    }

    /// <summary>The cast check (Spell-school-gate.patch): false refuses the cast before mana, reagents or skill are used.</summary>
    public static bool CanCast(Mobile caster, Spell spell)
    {
        if (spell == null || IsPermitted(caster, spell.CastSkill))
        {
            return true;
        }

        Refuse(caster, spell.CastSkill);
        return false;
    }

    /// <summary>The gain check (ClusterFSkillGain's handlers): false before any roll.</summary>
    public static bool AllowGain(Mobile m, SkillName skill) => IsPermitted(m, skill);

    /// <summary>The power scroll check (PowerScroll-school-gate.patch): false refuses before the scroll is used.</summary>
    public static bool CanUseScroll(Mobile m, SkillName skill)
    {
        if (IsPermitted(m, skill))
        {
            return true;
        }

        Refuse(m, skill);
        return false;
    }

    /// <summary>
    /// Holds each unearned school's cap at the character's own base, so no point can be added from anywhere.
    /// ClusterFSkillCaps.Apply calls it after setting the normal caps.
    /// </summary>
    public static void ApplyCaps(PlayerMobile pm)
    {
        if (pm?.Skills == null)
        {
            return;
        }

        foreach (var school in Schools)
        {
            if (!IsPermitted(pm, school.Skill))
            {
                var skill = pm.Skills[school.Skill];
                skill.CapFixedPoint = skill.BaseFixedPoint;
            }
        }
    }

    /// <summary>
    /// Saves the permission. True when it was new. The skill is not changed; its cap goes back to the shard's normal cap.
    /// </summary>
    public static bool Grant(PlayerMobile pm, string schoolId, string why)
    {
        var school = Find(schoolId);
        if (pm == null || school == null)
        {
            return false;
        }

        if (!SchoolGateStore.Permissions.TryGetValue(pm.Serial, out var set))
        {
            SchoolGateStore.Permissions[pm.Serial] = set = new HashSet<string>(StringComparer.Ordinal);
        }

        if (!set.Add(school.Id))
        {
            return false;
        }

        logger.Information("School gate: {Character} ({Serial}) may now learn {School} ({Why})", pm.Name, pm.Serial, school.Name, why);
        ClusterFSkillCaps.ApplyTo(pm);
        return true;
    }

    /// <summary>Staff only ([SchoolGate revoke): takes the permission away; the skill is not changed.</summary>
    public static bool Revoke(PlayerMobile pm, string schoolId, string why)
    {
        var school = Find(schoolId);
        if (pm == null || school == null || !SchoolGateStore.Permissions.TryGetValue(pm.Serial, out var set) || !set.Remove(school.Id))
        {
            return false;
        }

        logger.Information("School gate: {Character} ({Serial}) lost {School} ({Why})", pm.Name, pm.Serial, school.Name, why);
        ClusterFSkillCaps.ApplyTo(pm);
        return true;
    }

    public static InitiationProgress ProgressOf(Mobile m, string schoolId)
    {
        var key = (m.Serial, schoolId);
        if (!SchoolGateStore.Progress.TryGetValue(key, out var p))
        {
            SchoolGateStore.Progress[key] = p = new InitiationProgress();
        }

        return p;
    }

    public static string Describe(Mobile m)
    {
        var parts = new List<string>();
        foreach (var s in Schools)
        {
            var p = SchoolGateStore.Progress.GetValueOrDefault((m.Serial, s.Id));
            parts.Add(
                $"{s.Name}: {(HasPermission(m, s) ? "learned" : "not learned")}" +
                (p == null ? "" : $" (quest step {p.Step}, kit {(p.KitGiven ? "given" : "not given")})")
            );
        }

        return string.Join("; ", parts);
    }

    [Usage("SchoolGate [on|off|auto|status|grant <school>|revoke <school>]")]
    [Description("cc-P714: the Chivalry and Necromancy initiation gates. on, off or auto (test shard only) sets the switch; status, grant and revoke target a player. Grants are logged.")]
    [ShardCommand(CommandCategory.DevTool)]
    private static void SchoolGate_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

        switch (verb)
        {
            case "on" or "off" or "auto":
                Mode = verb;
                from.SendMessage($"School gates: {Mode} (in force: {(Enabled ? "yes" : "no")}).");
                return;
            case "":
                from.SendMessage($"School gates: {Mode} (in force: {(Enabled ? "yes" : "no")}). Schools: chivalry, necromancy.");
                return;
            case "status" or "grant" or "revoke":
                {
                    var id = e.Length > 1 ? e.GetString(1) : "";
                    if (verb != "status" && Find(id) == null)
                    {
                        from.SendMessage("Name a school: chivalry or necromancy.");
                        return;
                    }

                    from.SendMessage("Target a player.");
                    from.Target = new SchoolGateTarget(verb, id);
                    return;
                }
            default:
                from.SendMessage("Usage: [SchoolGate [on|off|auto|status|grant <school>|revoke <school>]");
                return;
        }
    }

    private sealed class SchoolGateTarget : Target
    {
        private readonly string _verb;
        private readonly string _id;

        public SchoolGateTarget(string verb, string id) : base(-1, false, TargetFlags.None)
        {
            _verb = verb;
            _id = id;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not PlayerMobile pm)
            {
                from.SendMessage("That is not a player.");
                return;
            }

            switch (_verb)
            {
                case "grant":
                    from.SendMessage(Grant(pm, _id, $"staff test grant by {from.Name}") ? $"{pm.Name} may now learn {_id}." : $"{pm.Name} already had {_id}.");
                    break;
                case "revoke":
                    from.SendMessage(Revoke(pm, _id, $"staff revoke by {from.Name}") ? $"{pm.Name} lost {_id}." : $"{pm.Name} did not have {_id}.");
                    break;
            }

            from.SendMessage($"{pm.Name}: {Describe(pm)}");
        }
    }
}
