// ShardVersion.cs
//
// F-15 (cc-P22): the shard's version and its patch notes, from one file, D:\ShatteredLegacy\CHANGELOG.md. Ours.
//
// Where it comes from: docker/uo/Dockerfile copies CHANGELOG.md and VERSION into the image at
// /modernuo/Data/ShatteredLegacy/ (Core.BaseDirectory is /modernuo), so the notes a server shows are always the
// notes of the build it is running; no file outside the image is read and nothing is typed twice. They are parsed
// here at server start (Configure), by the one parser the shard's tests gate (PatchNotesVerification).
//
// What reads it:
//   - the bulletin gump on login (ClusterFBulletinSystem.OnLogin): "Shattered Legacy <version>", the short list and an
//     All patch notes button, once per account per version (the account's FlagValues[SeenVersionKey]; no saved type
//     changes);
//   - [version, for any player: the version, and the Patch History gump (cc-P64, PatchHistoryGump.cs): every version
//     in the changelog, newest first, paged, each line wrapped;
//   - status.json (ShardStatusPublisher), shard.version;
//   - the wiki page: RenderDokuWiki, written at server start to /modernuo/Data/ShatteredLegacy/patch_notes.txt in
//     the container, which scripts/Publish-PatchNotes.ps1 copies to the wiki on Haven.
//
// The changelog's format is explained at the top of CHANGELOG.md itself. In short: "## 2026.09.30" starts a
// version (newest first; a second update that day is 2026.09.30.2), "### Added", "### Changed" and "### Fixed" start
// its lists, and each "- " line under one is a player-facing item. Everything else (the explanation at the top,
// blank lines, comments) is ignored. cc-P64: "### Staff" is parsed too, into Staff, which only the Patch History gump
// reads and only for staff; Sections() (the bulletin, the wiki page, players) leaves it out, as it always has.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Server.Accounting;
using Server.Gumps;

namespace Server;

public sealed class PatchNoteVersion
{
    public PatchNoteVersion(string version) => Version = version;

    public string Version { get; }
    public List<string> Added { get; } = new();
    public List<string> Changed { get; } = new();
    public List<string> Fixed { get; } = new();

    /// <summary>cc-P64: the "### Staff" list. Never in Sections(): players, the bulletin and the wiki do not see it.</summary>
    public List<string> Staff { get; } = new();

    /// <summary>cc-P64: the date the version names, "October 5, 2026"; "" if its first three parts are not a date.</summary>
    public string Date =>
        DateTime.TryParseExact(Version.Length >= 10 ? Version[..10] : Version, "yyyy.MM.dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var d)
            ? d.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)
            : "";

    public bool IsEmpty => Added.Count == 0 && Changed.Count == 0 && Fixed.Count == 0;

    public IEnumerable<(string Section, List<string> Lines)> Sections()
    {
        yield return ("Added", Added);
        yield return ("Changed", Changed);
        yield return ("Fixed", Fixed);
    }
}

public static class PatchNotes
{
    // cc-P64: nothing in game opens the wiki now. Its page waits for the fresh-world launch (Chase, 2026-10-05) and was
    // stale, so the bulletin's Full notes button became All patch notes (PatchHistoryGump). These two constants and
    // NotesUrl stay so the button can come back at launch: BulletinGump.OnResponse says where.
    public const string WikiBase = "https://wiki.shatteredlegacyuo.com/";

    /// <summary>The wiki page id: namespace uo, as every page on the wiki is (/uo:start, /uo:connection).</summary>
    public const string WikiPage = "uo:patch_notes";

    private static readonly Regex VersionPattern = new(@"^\d{4}\.\d{2}\.\d{2}(\.\d+)?$", RegexOptions.Compiled);

    public static bool IsVersion(string text) => !string.IsNullOrEmpty(text) && VersionPattern.IsMatch(text);

    /// <summary>Every version section in the changelog, in file order (newest first). Never throws.</summary>
    public static List<PatchNoteVersion> Parse(string markdown)
    {
        var versions = new List<PatchNoteVersion>();
        if (string.IsNullOrEmpty(markdown))
        {
            return versions;
        }

        PatchNoteVersion current = null;
        List<string> list = null;
        var inComment = false;

        foreach (var raw in markdown.Split('\n'))
        {
            var line = raw.TrimEnd('\r').Trim();

            // HTML comments, which the format explanation at the top may use, are skipped whole.
            if (inComment)
            {
                inComment = !line.Contains("-->");
                continue;
            }

            if (line.StartsWith("<!--", StringComparison.Ordinal))
            {
                inComment = !line.Contains("-->");
                continue;
            }

            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                var heading = line[3..].Trim();
                if (IsVersion(heading))
                {
                    current = new PatchNoteVersion(heading);
                    versions.Add(current);
                }
                else
                {
                    current = null; // a level-2 heading that is not a version ends the section
                }

                list = null;
                continue;
            }

            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                list = current == null ? null : line[4..].Trim().ToLowerInvariant() switch
                {
                    "added"   => current.Added,
                    "changed" => current.Changed,
                    "fixed"   => current.Fixed,
                    "staff"   => current.Staff,
                    _         => null
                };
                continue;
            }

            if (list != null && (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal)))
            {
                var item = line[2..].Trim();
                if (item.Length > 0)
                {
                    list.Add(item);
                }
            }
        }

        return versions;
    }

    /// <summary>
    /// The anchor DokuWiki gives the heading "Version X": sectionID (inc/pageutils.php) runs cleanID (lowercase, the
    /// space to the separator "_") and then drops every ':' and '.'. So "Version 2026.09.30.2" is version_202609302.
    /// </summary>
    public static string Anchor(string version) => "version_" + version.Replace(".", "").Replace(":", "");

    public static string NotesUrl(string version) => $"{WikiBase}{WikiPage}#{Anchor(version)}";

    /// <summary>The short list for the bulletin gump: one line per item, its section first.</summary>
    public static List<string> BulletinLines(PatchNoteVersion v)
    {
        var lines = new List<string>();
        foreach (var (section, items) in v.Sections())
        {
            foreach (var item in items)
            {
                lines.Add($"{section}: {item}");
            }
        }

        return lines;
    }

    /// <summary>The whole wiki page, DokuWiki markup, one section per version, newest first.</summary>
    public static string RenderDokuWiki(IReadOnlyList<PatchNoteVersion> versions)
    {
        var sb = new StringBuilder();
        sb.Append("====== Patch notes ======\n\n");
        sb.Append("What changed on the Shattered Legacy test shard, newest first. In game, ''[version'' shows the ");
        sb.Append("version you are playing. This page is generated from the shard's changelog; edits here are ");
        sb.Append("overwritten at the next update.\n\n");

        if (versions.Count == 0)
        {
            sb.Append("No versions yet.\n");
            return sb.ToString();
        }

        foreach (var v in versions)
        {
            sb.Append($"===== Version {v.Version} =====\n\n");
            foreach (var (section, items) in v.Sections())
            {
                if (items.Count == 0)
                {
                    continue;
                }

                sb.Append($"**{section}**\n\n");
                foreach (var item in items)
                {
                    sb.Append($"  * {item}\n");
                }

                sb.Append('\n');
            }
        }

        return sb.ToString();
    }
}

public static class ShardVersion
{
    /// <summary>The account FlagValues key holding the last version whose notes the account was shown.</summary>
    public const string SeenVersionKey = "bulletin.seenVersion";

    public const string ShardName = "Shattered Legacy";

    private static List<PatchNoteVersion> _versions = new();

    /// <summary>The version this build is, or "" when it has none.</summary>
    public static string Current { get; private set; } = "";

    /// <summary>The changelog section for Current, or null when there is none.</summary>
    public static PatchNoteVersion CurrentNotes { get; private set; }

    public static IReadOnlyList<PatchNoteVersion> All => _versions;

    public static string DataDirectory => Path.Combine(Core.BaseDirectory, "Data", "ShatteredLegacy");

    public static void Configure()
    {
        var changelogPath = Path.Combine(DataDirectory, "CHANGELOG.md");
        var versionPath = Path.Combine(DataDirectory, "VERSION");

        try
        {
            var changelog = File.Exists(changelogPath) ? File.ReadAllText(changelogPath) : "";
            var versionFile = File.Exists(versionPath) ? File.ReadAllText(versionPath) : "";

            foreach (var warning in Load(changelog, versionFile))
            {
                Console.WriteLine($"[ShardVersion] {warning}");
            }

            Console.WriteLine(Current.Length > 0
                ? $"[ShardVersion] {ShardName} {Current} ({_versions.Count} version(s) in the changelog)"
                : "[ShardVersion] No version: the changelog has no version sections yet.");

            WriteWikiPage();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ShardVersion] Could not read the patch notes: {ex.Message}");
        }

        CommandSystem.Register("version", AccessLevel.Player, Version_OnCommand);
    }

    /// <summary>
    /// Takes the changelog and the VERSION file's text. VERSION wins when it names a version; else the newest
    /// changelog section. Returns what did not line up, for the console. Never throws.
    /// </summary>
    public static List<string> Load(string changelog, string versionFile)
    {
        var warnings = new List<string>();
        _versions = PatchNotes.Parse(changelog);

        var stated = (versionFile ?? "").Trim();
        if (stated.Length > 0 && !PatchNotes.IsVersion(stated))
        {
            warnings.Add($"VERSION holds '{stated}', which is not a date version (2026.09.30 or 2026.09.30.2); ignored.");
            stated = "";
        }

        var newest = _versions.Count > 0 ? _versions[0].Version : "";
        Current = stated.Length > 0 ? stated : newest;
        CurrentNotes = _versions.Find(v => v.Version == Current);

        if (stated.Length > 0 && newest.Length > 0 && stated != newest)
        {
            warnings.Add($"VERSION says {stated} but the newest changelog section is {newest}.");
        }

        if (Current.Length > 0 && CurrentNotes == null)
        {
            warnings.Add($"The changelog has no section for {Current}; no notes will be shown.");
        }

        return warnings;
    }

    private static void WriteWikiPage()
    {
        if (!Directory.Exists(DataDirectory))
        {
            return;
        }

        File.WriteAllText(Path.Combine(DataDirectory, "patch_notes.txt"), PatchNotes.RenderDokuWiki(_versions));
    }

    /// <summary>The notes this account has not been shown yet: the current version's, once. Null when none.</summary>
    public static PatchNoteVersion UnseenNotesFor(IAccount acct)
    {
        if (acct == null || CurrentNotes == null || CurrentNotes.IsEmpty)
        {
            return null;
        }

        var seen = ClusterFAccountPersistence.GetOrCreate(acct).GetFlagValue(SeenVersionKey);
        return seen == CurrentNotes.Version ? null : CurrentNotes;
    }

    public static void MarkSeen(IAccount acct, PatchNoteVersion notes)
    {
        if (acct != null && notes != null)
        {
            ClusterFAccountPersistence.GetOrCreate(acct).SetFlagValue(SeenVersionKey, notes.Version);
        }
    }

    [Usage("version")]
    [Description("Shows the shard's version and every version's patch notes.")]
    [ShardCommand(CommandCategory.Player)]
    public static void Version_OnCommand(CommandEventArgs e)
    {
        var m = e.Mobile;

        if (Current.Length == 0)
        {
            m.SendMessage($"{ShardName} has no version number yet.");
            return;
        }

        m.SendMessage($"{ShardName} {Current}");

        if (CurrentNotes == null || CurrentNotes.IsEmpty)
        {
            m.SendMessage("There are no notes for this version.");
        }

        // cc-P64: every version, not just this one.
        if (_versions.Count > 0)
        {
            m.SendGump(new PatchHistoryGump(m));
        }
    }

    // For tests: a known state without files.
    internal static void Reset() => Load("", "");
}
