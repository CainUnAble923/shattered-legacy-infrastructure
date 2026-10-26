// cc-P631 (P566 design, stage 1): the New Haven townsfolk's dialogue table. Notes: shard-migration
// notes/cc-P631-build-townsfolk-rest.md; design notes/cc-P566-design-townsfolk-features.md.
//
// The table is data, server/data/townsfolk-talk.json, which the Dockerfile copies to Data/ShatteredLegacy/ in the builder's
// Distribution (so the tests read the shipped bytes) and so into the runtime image. It is read once at startup and by an
// Administrator's [ClusterFTownsfolk reload. A table is checked whole before it is used; a bad reload keeps the old one,
// and a bad or missing first load leaves talk off. Nothing is fabricated in its place.
//
// Matching is exact: the player's words, case-folded, punctuation to spaces, whitespace collapsed, must equal an alias
// ("what is your job"), optionally after the NPC's own name. No substrings, no regular expressions, so "forest" is not
// "rest" and an ordinary sentence with "job" in it is not the job topic. Inspired by David's conversational townsperson
// (uploaded by Zavreyon; P405 notes); no code or dialogue copied.
//
// P647 (P631/P643 review fixes): an unreadable file is a failed load like a bad one; a topic may not repeat a response;
// the reserved words cover every stock pet order (multiword ones too); each launch profile must name its own region;
// a topic id is a short plain identifier.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Server.Engines.Townsfolk;

public enum TalkPhase
{
    Any,
    Core,
    AfterCore
}

public sealed record TalkResponse(TalkPhase Phase, string Text);

public sealed record TalkTopic(string Id, IReadOnlyList<string> Aliases, IReadOnlyList<TalkResponse> Responses);

public sealed class TalkProfile
{
    public TalkProfile(string tag, string region, IReadOnlyList<TalkTopic> topics)
    {
        Tag = tag;
        Region = region;
        Topics = topics;
        foreach (var topic in topics)
        {
            foreach (var alias in topic.Aliases)
            {
                ByAlias[alias] = topic;
            }
        }
    }

    public string Tag { get; }

    /// <summary>The region (or an ancestor of it) the speaking NPC must stand in, by name.</summary>
    public string Region { get; }

    public IReadOnlyList<TalkTopic> Topics { get; }

    public Dictionary<string, TalkTopic> ByAlias { get; } = new(StringComparer.Ordinal);

    public TalkTopic? Topic(string id) => Topics.FirstOrDefault(t => t.Id == id);
}

public sealed class TalkTable
{
    public TalkTable(IReadOnlyList<TalkProfile> profiles) => Profiles = profiles.ToDictionary(p => p.Tag, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, TalkProfile> Profiles { get; }

    public TalkProfile? Profile(string? tag) => tag != null && Profiles.TryGetValue(tag, out var p) ? p : null;
}

public static class ClusterFTalkRules
{
    public const int SchemaVersion = 1;
    public const string FileName = "townsfolk-talk.json";

    public const int MaxFileBytes = 256 * 1024;
    public const int MaxProfiles = 8;
    public const int MaxTopics = 16;
    public const int MaxAliases = 12;
    public const int MaxResponses = 8;
    public const int MaxAliasLength = 80;
    public const int MaxResponseLength = 240;
    public const int MaxSpeechLength = 160;
    public const int MinRumors = 2;

    public const string NameToken = "{name}";

    /// <summary>The launch profiles, exactly (a later prompt extends this list).</summary>
    public static readonly string[] LaunchProfiles = ["plaza", "trades", "innkeeper"];

    /// <summary>The region each launch profile answers in, exactly (descendant regions still count at runtime).</summary>
    public static readonly IReadOnlyDictionary<string, string> LaunchRegions = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["plaza"] = "New Haven",
        ["trades"] = "New Haven",
        ["innkeeper"] = "the New Haven Inn"
    };

    public const int MaxTopicIdLength = 32;

    /// <summary>Every launch profile answers these.</summary>
    public static readonly string[] LaunchTopics = ["hello", "name", "job", "help", "inn", "rumors", "goodbye"];

    /// <summary>
    /// Words and phrases the table may never claim: the inn's rest (its own service, stage 2), vendor, trainer, bank,
    /// guard and pet commands, and the tutorial's. An alias equal to one, or containing the word "rest", is refused.
    /// The pet orders are every one stock BaseAI/OnSpeech.cs answers (P647), the multiword ones included.
    /// </summary>
    public static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "rest", "buy", "sell", "vendor buy", "vendor sell", "train", "teach", "bank", "balance", "withdraw", "check",
        "guards", "guard", "all follow", "all come", "all stay", "all stop", "all kill", "all attack", "all guard",
        "all follow me", "all guard me", "all obey", "follow", "follow me", "come", "stay", "stop", "kill", "attack",
        "guard me", "drop", "friend", "release", "transfer", "obey", "move", "time", "join", "resign", "skip", "tutorial"
    };

    // -- The loaded table -------------------------------------------------------------------------------------------

    /// <summary>The table in use, or null while talk is off for want of a valid table.</summary>
    public static TalkTable? Current { get; private set; }

    /// <summary>The last load's problems, for [ClusterFTownsfolk status.</summary>
    public static IReadOnlyList<string> LastErrors { get; private set; } = [];

    public static string? LoadedFrom { get; private set; }

    public static void Configure() => Reload();

    /// <summary>
    /// Reads and checks the shipped table. On success it replaces the current table; on failure the current table (if
    /// any) stays and the problems are kept. Returns whether the new table is now in use.
    /// </summary>
    public static bool Reload(string? path = null)
    {
        path ??= FindDataFile();
        if (path == null)
        {
            LastErrors = [$"{FileName}: not found under Data/ShatteredLegacy"];
            Console.WriteLine($"Townsfolk: {LastErrors[0]}; talk stays off.");
            return false;
        }

        var (table, errors) = LoadFile(path);
        if (table == null)
        {
            LastErrors = errors;
            Console.WriteLine($"Townsfolk: {path} refused ({errors.Count} problems): {string.Join("; ", errors.Take(5))}");
            return false;
        }

        Current = table;
        LoadedFrom = path;
        LastErrors = [];
        return true;
    }

    /// <summary>Forgets the table (tests).</summary>
    public static void Unload()
    {
        Current = null;
        LoadedFrom = null;
        LastErrors = [];
    }

    /// <summary>
    /// The shipped file: Data/ShatteredLegacy/ beside the server (the runtime image), else Distribution/Data/ShatteredLegacy/
    /// in a folder above it (the builder, where the tests run). Null when neither exists.
    /// </summary>
    public static string? FindDataFile()
    {
        var beside = Path.Combine(Core.BaseDirectory, "Data", "ShatteredLegacy", FileName);
        if (File.Exists(beside))
        {
            return beside;
        }

        for (var d = new DirectoryInfo(Core.BaseDirectory); d != null; d = d.Parent)
        {
            var candidate = Path.Combine(d.FullName, "Distribution", "Data", "ShatteredLegacy", FileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    public static (TalkTable? Table, List<string> Errors) LoadFile(string path)
    {
        var info = new FileInfo(path);
        if (!info.Exists)
        {
            return (null, [$"{path}: missing"]);
        }

        if (info.Length > MaxFileBytes)
        {
            return (null, [$"{path}: {info.Length} bytes, over {MaxFileBytes}"]);
        }

        string json;
        try
        {
            json = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Held open, unreadable, or gone since the check (P647): a failed load, never a thrown startup or reload.
            return (null, [$"{path}: unreadable: {ex.Message}"]);
        }

        return Parse(json);
    }

    // -- Parsing and checking ---------------------------------------------------------------------------------------

    public static (TalkTable? Table, List<string> Errors) Parse(string json)
    {
        var errors = new List<string>();
        if (Encoding.UTF8.GetByteCount(json) > MaxFileBytes)
        {
            return (null, [$"over {MaxFileBytes} bytes"]);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return (null, [$"not JSON: {ex.Message}"]);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (!Object(root, "file", ["schemaVersion", "profiles"], errors))
            {
                return (null, errors);
            }

            if (!root.TryGetProperty("schemaVersion", out var v) || v.ValueKind != JsonValueKind.Number ||
                !v.TryGetInt32(out var version) || version != SchemaVersion)
            {
                errors.Add($"schemaVersion must be {SchemaVersion}");
            }

            if (!root.TryGetProperty("profiles", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                errors.Add("profiles must be a list");
                return (null, errors);
            }

            if (list.GetArrayLength() > MaxProfiles)
            {
                errors.Add($"more than {MaxProfiles} profiles");
            }

            var profiles = new List<TalkProfile>();
            foreach (var p in list.EnumerateArray())
            {
                if (ReadProfile(p, errors) is { } profile)
                {
                    if (profiles.Any(x => x.Tag == profile.Tag))
                    {
                        errors.Add($"profile {profile.Tag} is listed twice");
                    }
                    else
                    {
                        profiles.Add(profile);
                    }
                }
            }

            foreach (var tag in LaunchProfiles.Where(t => profiles.All(p => p.Tag != t)))
            {
                errors.Add($"launch profile {tag} is missing");
            }

            return errors.Count == 0 ? (new TalkTable(profiles), errors) : (null, errors);
        }
    }

    private static TalkProfile? ReadProfile(JsonElement p, List<string> errors)
    {
        if (!Object(p, "profile", ["tag", "region", "topics"], errors))
        {
            return null;
        }

        var tag = Str(p, "tag", "profile", errors);
        var where = $"profile {tag ?? "?"}";
        var region = Str(p, "region", where, errors);
        if (tag != null && !LaunchProfiles.Contains(tag))
        {
            errors.Add($"{where}: not a known profile tag");
        }
        else if (tag != null && region != null && region != LaunchRegions[tag])
        {
            // A typo would silence the resident; a wider region would widen who it talks to (P647).
            errors.Add($"{where}: region must be \"{LaunchRegions[tag]}\"");
        }

        if (!p.TryGetProperty("topics", out var list) || list.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{where}: topics must be a list");
            return null;
        }

        if (list.GetArrayLength() > MaxTopics)
        {
            errors.Add($"{where}: more than {MaxTopics} topics");
        }

        var topics = new List<TalkTopic>();
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var t in list.EnumerateArray())
        {
            if (ReadTopic(t, where, errors) is not { } topic)
            {
                continue;
            }

            if (topics.Any(x => x.Id == topic.Id))
            {
                errors.Add($"{where}: topic {topic.Id} is listed twice");
                continue;
            }

            foreach (var alias in topic.Aliases)
            {
                if (!aliases.Add(alias))
                {
                    errors.Add($"{where}: alias \"{alias}\" is claimed twice");
                }
            }

            topics.Add(topic);
        }

        foreach (var id in LaunchTopics.Where(id => topics.All(t => t.Id != id)))
        {
            errors.Add($"{where}: launch topic {id} is missing");
        }

        if (topics.FirstOrDefault(t => t.Id == "rumors") is { } rumors && rumors.Responses.Count(r => r.Phase == TalkPhase.Any) < MinRumors)
        {
            errors.Add($"{where}: rumors needs at least {MinRumors} lines for everyone");
        }

        return tag == null || region == null ? null : new TalkProfile(tag, region, topics);
    }

    private static TalkTopic? ReadTopic(JsonElement t, string where, List<string> errors)
    {
        if (!Object(t, $"{where} topic", ["id", "aliases", "responses"], errors))
        {
            return null;
        }

        var id = Str(t, "id", $"{where} topic", errors);
        if (id != null && !IsPlainId(id))
        {
            // Unknown ids show as their own menu label, so they stay plain (P647).
            errors.Add($"{where} topic: id must be a plain identifier (a-z, 0-9, _, up to {MaxTopicIdLength})");
            id = null;
        }

        where = $"{where} topic {id ?? "?"}";

        var aliases = new List<string>();
        if (!t.TryGetProperty("aliases", out var al) || al.ValueKind != JsonValueKind.Array || al.GetArrayLength() == 0)
        {
            errors.Add($"{where}: aliases must be a non-empty list");
        }
        else
        {
            if (al.GetArrayLength() > MaxAliases)
            {
                errors.Add($"{where}: more than {MaxAliases} aliases");
            }

            foreach (var a in al.EnumerateArray())
            {
                if (a.ValueKind != JsonValueKind.String)
                {
                    errors.Add($"{where}: an alias is not text");
                    continue;
                }

                var raw = a.GetString()!;
                var norm = Normalize(raw);
                if (raw.Length > MaxAliasLength || norm == null || norm.Length == 0)
                {
                    errors.Add($"{where}: alias \"{raw}\" is empty or too long");
                }
                else if (norm != raw)
                {
                    errors.Add($"{where}: alias \"{raw}\" must be written as it is matched: \"{norm}\"");
                }
                else if (Reserved.Contains(norm) || norm.Split(' ').Contains("rest"))
                {
                    errors.Add($"{where}: alias \"{raw}\" is a reserved command");
                }
                else if (aliases.Contains(norm))
                {
                    errors.Add($"{where}: alias \"{raw}\" is listed twice");
                }
                else
                {
                    aliases.Add(norm);
                }
            }
        }

        var responses = new List<TalkResponse>();
        if (!t.TryGetProperty("responses", out var rl) || rl.ValueKind != JsonValueKind.Array || rl.GetArrayLength() == 0)
        {
            errors.Add($"{where}: responses must be a non-empty list");
        }
        else
        {
            if (rl.GetArrayLength() > MaxResponses)
            {
                errors.Add($"{where}: more than {MaxResponses} responses");
            }

            foreach (var r in rl.EnumerateArray())
            {
                if (!Object(r, $"{where} response", ["phase", "text"], errors))
                {
                    continue;
                }

                var phase = Str(r, "phase", $"{where} response", errors) switch
                {
                    "any" => TalkPhase.Any,
                    "core" => TalkPhase.Core,
                    "afterCore" => TalkPhase.AfterCore,
                    null => (TalkPhase?)null,
                    var other => Bad(errors, $"{where}: unknown phase \"{other}\"")
                };
                var text = Str(r, "text", $"{where} response", errors);
                if (text != null && TextProblem(text) is { } problem)
                {
                    errors.Add($"{where}: {problem}");
                }
                else if (text != null && responses.Any(x => x.Text == text))
                {
                    // Two identical lines leave nothing to choose once that line was last said (P647).
                    errors.Add($"{where}: response \"{text}\" is listed twice");
                }
                else if (text != null && phase != null)
                {
                    responses.Add(new TalkResponse(phase.Value, text));
                }
            }

            if (responses.Count > 0 && responses.All(r => r.Phase != TalkPhase.Any))
            {
                errors.Add($"{where}: needs a response for phase any");
            }
        }

        return id == null ? null : new TalkTopic(id, aliases, responses);
    }

    /// <summary>A topic id: a lowercase letter, then lowercase letters, digits or underscores, at most MaxTopicIdLength.</summary>
    public static bool IsPlainId(string id) =>
        id.Length <= MaxTopicIdLength && id[0] is >= 'a' and <= 'z' &&
        id.All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_');

    private static TalkPhase? Bad(List<string> errors, string message)
    {
        errors.Add(message);
        return null;
    }

    /// <summary>Why a response text is refused, or null: too long, markup, control characters, or a brace other than {name}.</summary>
    public static string? TextProblem(string text)
    {
        if (text.Length == 0 || text.Length > MaxResponseLength)
        {
            return $"response is empty or over {MaxResponseLength} characters";
        }

        if (text.Any(c => char.IsControl(c) || c is '<' or '>'))
        {
            return "response has markup or control characters";
        }

        if (text.Replace(NameToken, "").Any(c => c is '{' or '}'))
        {
            return "response has a template other than {name}";
        }

        return null;
    }

    private static bool Object(JsonElement e, string what, string[] allowed, List<string> errors)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{what} must be an object");
            return false;
        }

        foreach (var prop in e.EnumerateObject())
        {
            if (!allowed.Contains(prop.Name))
            {
                errors.Add($"{what}: unknown field \"{prop.Name}\"");
            }
        }

        return true;
    }

    private static string? Str(JsonElement e, string name, string where, List<string> errors)
    {
        if (e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String && v.GetString() is { Length: > 0 } s)
        {
            return s;
        }

        errors.Add($"{where}: {name} must be non-empty text");
        return null;
    }

    // -- Matching ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Speech as it is matched: case-folded, punctuation as spaces, whitespace collapsed and trimmed. Null when longer than
    /// MaxSpeechLength (no answer to an essay).
    /// </summary>
    public static string? Normalize(string? text)
    {
        if (text == null || text.Length > MaxSpeechLength)
        {
            return null;
        }

        var sb = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
            {
                if (space && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(c);
                space = false;
            }
            else
            {
                space = true;
            }
        }

        return sb.ToString();
    }

    /// <summary>The topic this speech asks for in a profile: an exact alias, or the NPC's name then an exact alias.</summary>
    public static TalkTopic? Match(TalkProfile profile, string? normalized, string? npcName)
    {
        if (string.IsNullOrEmpty(normalized))
        {
            return null;
        }

        if (profile.ByAlias.TryGetValue(normalized, out var topic))
        {
            return topic;
        }

        var name = Normalize(npcName);
        if (!string.IsNullOrEmpty(name) && normalized.StartsWith(name + " ", StringComparison.Ordinal) &&
            profile.ByAlias.TryGetValue(normalized[(name.Length + 1)..], out topic))
        {
            return topic;
        }

        return null;
    }

    /// <summary>The responses for this phase: the phase's own if it has any, else everyone's.</summary>
    public static IReadOnlyList<TalkResponse> ResponsesFor(TalkTopic topic, TalkPhase phase)
    {
        if (phase != TalkPhase.Any)
        {
            var own = topic.Responses.Where(r => r.Phase == phase).ToList();
            if (own.Count > 0)
            {
                return own;
            }
        }

        return topic.Responses.Where(r => r.Phase == TalkPhase.Any).ToList();
    }
}
