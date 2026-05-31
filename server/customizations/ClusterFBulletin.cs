using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server;

// ── Bulletin category ─────────────────────────────────────────────────────────

public enum BulletinCategory
{
    CriticalNotice,   // downtime, urgent changes
    LeagueDispatch,   // world updates, expedition news (default)
    GuildNotice,      // profession/guild updates
    EventNotice,      // temporary events and world changes
    PersonalNotice,   // future: player-specific unlocks, promotions
}

// ── Bulletin entry ────────────────────────────────────────────────────────────

public class BulletinEntry
{
    public int              Id       { get; }
    public BulletinCategory Category { get; }
    public string           Message  { get; }
    public DateTime         PostedAt { get; }

    public BulletinEntry(int id, BulletinCategory cat, string msg, DateTime postedAt)
    {
        Id = id; Category = cat; Message = msg; PostedAt = postedAt;
    }

    public BulletinEntry(IGenericReader r)
    {
        Id       = r.ReadInt();
        Category = (BulletinCategory)r.ReadInt();
        Message  = r.ReadString();
        PostedAt = r.ReadDateTime();
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(Id);
        w.Write((int)Category);
        w.Write(Message);
        w.Write(PostedAt);
    }
}

// ── Bulletin system ───────────────────────────────────────────────────────────

/// <summary>
/// League Dispatch / MOTD bulletin system for Shattered Legacy.
///
/// Bulletins are global records stored in ClusterFBulletinPersistence.
/// Each account tracks the highest bulletin ID it has seen in ClusterFAccountData.
/// On login, any bulletins with ID > LastSeenBulletinId are shown in a gump.
///
/// Admin commands:
///   [ClusterFBulletin add <category> <message text>
///   [ClusterFBulletin list
///   [ClusterFBulletin remove <id>
///   [ClusterFBulletin load           — load/reload from bulletins.txt
///
/// Category names: critical, dispatch, guild, event, personal
///
/// File-based authoring (recommended for long messages):
///   Edit /opt/uo/modernuo/Configuration/bulletins.txt
///   One bulletin per line: category|message text
///   Lines starting with # are comments and are ignored.
///   Run [ClusterFBulletin load in-game to publish.
///
/// Example bulletins.txt:
///   dispatch|Welcome to Shattered Legacy! Old Haven has been restored.
///   critical|Server maintenance Saturday at 10pm EST. Save your progress.
/// </summary>
public static class ClusterFBulletinSystem
{
    private static readonly List<BulletinEntry> _bulletins = new();
    private static int _nextId = 1;

    // Path to the file-based bulletin source.
    private static readonly string BulletinFilePath =
        Path.Combine(Core.BaseDirectory, "Configuration", "bulletins.txt");

    public static IReadOnlyList<BulletinEntry> Bulletins => _bulletins;
    public static int MaxId => _bulletins.Count > 0 ? _bulletins[^1].Id : 0;

    public static void Configure()
    {
        CommandSystem.Register("ClusterFBulletin", AccessLevel.Administrator, OnCommand);
    }

    // ── Login hook ────────────────────────────────────────────────────────

    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        if (_bulletins.Count == 0) return;
        if (pm.Account is not IAccount acct) return;

        var data = ClusterFAccountPersistence.GetOrCreate(acct);
        var lastSeen = data.LastSeenBulletinId;

        var unread = new List<BulletinEntry>();
        foreach (var b in _bulletins)
            if (b.Id > lastSeen)
                unread.Add(b);

        if (unread.Count == 0) return;

        // Small delay so the player has fully loaded in before we send the gump.
        Timer.StartTimer(TimeSpan.FromSeconds(2.0), () =>
        {
            if (pm.NetState != null)
                pm.SendGump(new BulletinGump(pm, unread));
        });
    }

    // ── Mark all current bulletins as seen for this account ───────────────

    public static void MarkSeen(IAccount acct)
    {
        var id = MaxId;
        if (id == 0) return;
        ClusterFAccountPersistence.GetOrCreate(acct).LastSeenBulletinId = id;
    }

    // ── Command handler ───────────────────────────────────────────────────

    [Usage("ClusterFBulletin <add|list|remove> [args]")]
    [Description("Manages the League Dispatch / MOTD bulletin board.")]
    private static void OnCommand(CommandEventArgs e)
    {
        if (e.Length == 0)
        {
            e.Mobile.SendMessage("Usage: ClusterFBulletin <add|list|remove> [args]");
            return;
        }

        switch (e.GetString(0).ToLowerInvariant())
        {
            case "add":    CmdAdd(e);    break;
            case "list":   CmdList(e);   break;
            case "remove": CmdRemove(e); break;
            case "load":   CmdLoad(e);   break;
            default:
                e.Mobile.SendMessage("Unknown subcommand. Use: add, list, remove, load");
                break;
        }
    }

    private static void CmdAdd(CommandEventArgs e)
    {
        // [ClusterFBulletin add <category> <message words...>
        if (e.Length < 3)
        {
            e.Mobile.SendMessage("Usage: ClusterFBulletin add <category> <message>");
            e.Mobile.SendMessage("Categories: critical, dispatch, guild, event, personal");
            return;
        }

        var cat = e.GetString(1).ToLowerInvariant() switch
        {
            "critical" => BulletinCategory.CriticalNotice,
            "guild"    => BulletinCategory.GuildNotice,
            "event"    => BulletinCategory.EventNotice,
            "personal" => BulletinCategory.PersonalNotice,
            _          => BulletinCategory.LeagueDispatch, // "dispatch" and unknown default here
        };

        var parts = new List<string>();
        for (var i = 2; i < e.Length; i++)
            parts.Add(e.GetString(i));
        var msg = string.Join(" ", parts);

        var entry = new BulletinEntry(_nextId++, cat, msg, DateTime.UtcNow);
        _bulletins.Add(entry);

        e.Mobile.SendMessage($"[Bulletin #{entry.Id}] Posted: [{entry.Category}] {msg}");
    }

    private static void CmdList(CommandEventArgs e)
    {
        if (_bulletins.Count == 0)
        {
            e.Mobile.SendMessage("No bulletins posted.");
            return;
        }

        foreach (var b in _bulletins)
        {
            var date = b.PostedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
            e.Mobile.SendMessage($"#{b.Id} [{b.Category}] {date}: {b.Message}");
        }
    }

    private static void CmdRemove(CommandEventArgs e)
    {
        if (e.Length < 2 || !int.TryParse(e.GetString(1), out var id))
        {
            e.Mobile.SendMessage("Usage: ClusterFBulletin remove <id>");
            return;
        }

        var removed = _bulletins.RemoveAll(b => b.Id == id);
        e.Mobile.SendMessage(removed > 0
            ? $"Removed bulletin #{id}."
            : $"No bulletin with ID #{id} found.");
    }

    private static void CmdLoad(CommandEventArgs e)
    {
        // [ClusterFBulletin load
        // Reads Configuration/bulletins.txt and adds any lines not already
        // present as bulletins. Existing bulletins are not duplicated.
        // Format: category|message text
        // Lines starting with # are comments.

        if (!File.Exists(BulletinFilePath))
        {
            // Create a template file so the admin knows the format.
            File.WriteAllText(BulletinFilePath,
                "# Shattered Legacy — League Dispatch bulletin file\n" +
                "# One bulletin per line: category|message text\n" +
                "# Categories: critical, dispatch, guild, event, personal\n" +
                "# Run [ClusterFBulletin load in-game after editing.\n" +
                "#\n" +
                "# dispatch|Welcome to Shattered Legacy!\n");
            e.Mobile.SendMessage($"No bulletins.txt found — created template at {BulletinFilePath}");
            e.Mobile.SendMessage("Edit it over SSH and run [ClusterFBulletin load again.");
            return;
        }

        var lines   = File.ReadAllLines(BulletinFilePath);
        var added   = 0;
        var skipped = 0;

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            var sep = line.IndexOf('|');
            if (sep < 1)
            {
                e.Mobile.SendMessage($"[SKIP] Bad format (missing |): {line}");
                skipped++;
                continue;
            }

            var catStr = line[..sep].Trim().ToLowerInvariant();
            var msg    = line[(sep + 1)..].Trim();

            if (string.IsNullOrEmpty(msg))
            {
                e.Mobile.SendMessage($"[SKIP] Empty message: {line}");
                skipped++;
                continue;
            }

            // Don't re-add if an identical message already exists.
            if (_bulletins.Exists(b => b.Message.Equals(msg, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }

            var cat = catStr switch
            {
                "critical" => BulletinCategory.CriticalNotice,
                "guild"    => BulletinCategory.GuildNotice,
                "event"    => BulletinCategory.EventNotice,
                "personal" => BulletinCategory.PersonalNotice,
                _          => BulletinCategory.LeagueDispatch,
            };

            var entry = new BulletinEntry(_nextId++, cat, msg, DateTime.UtcNow);
            _bulletins.Add(entry);
            added++;
        }

        e.Mobile.SendMessage($"Bulletin load complete: {added} added, {skipped} skipped.");
    }

    // ── Internal persistence API ──────────────────────────────────────────

    internal static void Load(List<BulletinEntry> entries, int nextId)
    {
        _bulletins.Clear();
        _bulletins.AddRange(entries);
        _nextId = nextId;
    }

    internal static (List<BulletinEntry> entries, int nextId) GetForSave() =>
        (_bulletins, _nextId);
}

// ── Persistence item ──────────────────────────────────────────────────────────

/// <summary>
/// Singleton Item that serializes the global bulletin list into the world save.
/// </summary>
public class ClusterFBulletinPersistence : Item
{
    private static ClusterFBulletinPersistence _instance;

    public static void Configure()
    {
        // Item creation must happen after world load — not during Configure.
        EventSink.WorldLoad += EnsureExistence;
    }

    private static void EnsureExistence()
    {
        _instance ??= new ClusterFBulletinPersistence();
    }

    private ClusterFBulletinPersistence() : base(1) => Movable = false;

    public ClusterFBulletinPersistence(Serial serial) : base(serial) => _instance = this;

    public override string DefaultName => "ClusterF Bulletin Persistence — Internal";

    public override void Serialize(IGenericWriter w)
    {
        base.Serialize(w);
        w.Write(0); // version

        var (entries, nextId) = ClusterFBulletinSystem.GetForSave();
        w.Write(nextId);
        w.Write(entries.Count);
        foreach (var entry in entries)
            entry.Serialize(w);
    }

    public override void Deserialize(IGenericReader r)
    {
        base.Deserialize(r);
        var version = r.ReadInt();

        var nextId  = r.ReadInt();
        var count   = r.ReadInt();
        var entries = new List<BulletinEntry>(count);
        for (var i = 0; i < count; i++)
            entries.Add(new BulletinEntry(r));

        ClusterFBulletinSystem.Load(entries, nextId);
    }
}

// ── Bulletin gump ─────────────────────────────────────────────────────────────

/// <summary>
/// Displays unread League Dispatch bulletins to the player on login.
/// Dismissing (or closing) the gump marks all current bulletins as seen.
/// </summary>
public class BulletinGump : Gump
{
    private readonly Mobile _mobile;

    // Category display names and hues (UO label hue values, not RGB hex).
    private static readonly (string Label, int Hue)[] CategoryStyles =
    [
        ("! Critical Notice",  37),   // red
        ("League Dispatch",    1154), // gold/yellow
        ("Guild Notice",       96),   // light blue
        ("Event Notice",       68),   // green
        ("Personal Notice",    1645), // lavender
    ];

    private const int BgGumpId  = 9270;  // stone/parchment background
    private const int GumpWidth = 480;
    private const int EntryHeight = 74;
    private const int HeaderH    = 72;
    private const int FooterH    = 50;

    public BulletinGump(Mobile m, List<BulletinEntry> entries) : base(60, 60)
    {
        _mobile = m;

        Closable   = true;
        Disposable = true;

        var visibleCount = Math.Min(entries.Count, 7); // cap display at 7 entries
        var totalH = HeaderH + visibleCount * EntryHeight + FooterH;

        AddBackground(0, 0, GumpWidth, totalH, BgGumpId);
        AddAlphaRegion(10, 10, GumpWidth - 20, totalH - 20);

        // ── Header ────────────────────────────────────────────────────────
        AddLabel(GumpWidth / 2 - 75, 14, 1154, "League Dispatch");
        AddLabel(GumpWidth / 2 - 95, 34, 999,  "New notices since your last visit:");

        // Horizontal rule (thin line image)
        AddImageTiled(10, 58, GumpWidth - 20, 2, 9304);

        // ── Entries ───────────────────────────────────────────────────────
        var y = HeaderH;
        for (var i = 0; i < visibleCount; i++)
        {
            var b   = entries[i];
            var idx = (int)b.Category;
            var (label, hue) = idx < CategoryStyles.Length
                ? CategoryStyles[idx]
                : CategoryStyles[1];

            var dateStr = b.PostedAt.ToLocalTime().ToString("MMM d, yyyy");

            // Category tag + date
            AddLabel(20,           y + 4,  hue,  label);
            AddLabel(GumpWidth - 130, y + 4,  999,  dateStr);

            // Message text (wraps within width)
            AddHtml(20, y + 24, GumpWidth - 40, 40, $"<BASEFONT COLOR=#DDDDDD>{b.Message}</BASEFONT>", false, false);

            // Separator between entries
            if (i < visibleCount - 1)
                AddImageTiled(10, y + EntryHeight - 2, GumpWidth - 20, 2, 9304);

            y += EntryHeight;
        }

        // Overflow notice if there are more entries than shown
        if (entries.Count > 7)
        {
            AddLabel(20, y + 4, 999,
                $"... and {entries.Count - 7} more. Use [ClusterFBulletin list to view all.");
            y += 20;
        }

        // ── Footer / dismiss button ───────────────────────────────────────
        AddImageTiled(10, y + 4, GumpWidth - 20, 2, 9304);
        AddButton(GumpWidth / 2 - 40, y + 14, 4023, 4025, 1); // OK button
        AddLabel(GumpWidth / 2 - 16, y + 16, 1154, "Dismiss");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        // Mark as seen on any interaction — close (0) or dismiss button (1).
        if (_mobile.Account is IAccount acct)
            ClusterFBulletinSystem.MarkSeen(acct);
    }
}
