// cc-P79 (F-41): the fanfare service, phase 1. Notes: shard-migration notes/cc-P79-loot-boxes-fanfare.md.
//
// One service every celebration calls: achievements, box awards and openings, level-ups, loops, League rank-ups and trial
// chapters, reaching 200. Three levels:
//   Minor     the player only: a small burst and a sound sent to that player's client alone, the line overhead (private).
//   Major     nearby players: bursts and sounds in the area, the line overhead for all to see.
//   Capstone  the whole shard: a bigger show in the area and a shard-wide broadcast naming the player.
// Phase 1 is server side only: stock UO firework bursts (0x373A family, pinned FireworksWand.cs:99), sparkles (0x376A)
// and the firework sounds (0x11B to 0x11E), hued per tier and growing with the level. Overhead text stands in for the
// voice over. No client data, no custom sound.
//
// Merge: several fanfares on one player within MergeWindow play as one, at the highest level asked, with the lines
// stacked. Mute: [FanfareMute (a per-character setting, CharacterLootRecord.FanfareMuted) silences the player's own
// effects and overhead lines; the chat lines still arrive, and a capstone's shard broadcast always shows.
// Lines rotate at random from Configuration/fanfare-lines.json (pools by name; placeholders [Player], [level], [loop],
// [count], [tier]); a missing file is written with the approved pools, a bad one is refused whole.
// Phase 2 hook: VoiceHook, called with a voice key ("achievement", "box:<tier name>", ...), is null in phase 1.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Accounting;
using Server.Commands;
using Server.Mobiles;
using Server.Network;

namespace Server.LootBoxes;

public enum FanfareLevel
{
    Minor,
    Major,
    Capstone
}

public sealed record FanfareVars(string? Level = null, int Loop = 0, int Count = 0, string? Tier = null);

/// <summary>What one flushed fanfare did, kept for the tests and [Fanfare last.</summary>
public sealed record FanfarePlayed(FanfareLevel Level, int Tier, List<string> Lines, bool Effects, string? Broadcast);

public static class ClusterFFanfare
{
    public static TimeSpan MergeWindow { get; set; } = TimeSpan.FromSeconds(2.5);

    /// <summary>Phase 2: the announcer voice. Called with the player and a voice key. Null in phase 1: no custom sound.</summary>
    public static Action<PlayerMobile, string>? VoiceHook { get; set; }

    // Pending fanfares per player, flushed once the merge window closes.
    private sealed class Pending
    {
        public FanfareLevel Level;
        public int Tier;
        public readonly List<string> Lines = new();
        public readonly List<string> Broadcasts = new();
        public readonly List<string> VoiceKeys = new();
        public Timer? Timer;
    }

    private static readonly Dictionary<PlayerMobile, Pending> _pending = new();

    public static readonly Dictionary<PlayerMobile, FanfarePlayed> LastPlayed = new();

    public static void Configure()
    {
        foreach (var line in FanfareLines.LoadOrWriteDefaults(FanfareLines.FilePath))
        {
            Console.WriteLine($"[Fanfare] {line}");
        }

        CommandSystem.Register("FanfareMute", AccessLevel.Player, OnMuteCommand);
    }

    public static bool IsMuted(PlayerMobile pm) =>
        pm.Account is IAccount acct && ClusterFAccountPersistence.Get(acct)?.GetLootRecord(pm.Serial)?.FanfareMuted == true;

    public static void SetMuted(PlayerMobile pm, bool muted)
    {
        if (pm.Account is IAccount acct)
        {
            ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateLootRecord(pm.Serial).FanfareMuted = muted;
        }
    }

    /// <summary>
    /// Queues a fanfare: a line from the pool (or the given line), at a level and tier (1 to 6, the hue and size). Merged
    /// with any other queued for this player in the window.
    /// </summary>
    public static void Play(PlayerMobile pm, FanfareLevel level, string pool, FanfareVars? vars = null, int tier = 1,
        string? line = null, string? voiceKey = null)
    {
        if (pm.Deleted)
        {
            return;
        }

        var text = line ?? FanfareLines.Pick(pool, pm, vars);

        if (!_pending.TryGetValue(pm, out var p))
        {
            _pending[pm] = p = new Pending { Level = level, Tier = tier };
            p.Timer = Timer.DelayCall(MergeWindow, () => Flush(pm));
        }

        p.Level = (FanfareLevel)Math.Max((int)p.Level, (int)level);
        p.Tier = Math.Max(p.Tier, tier);

        if (text.Length > 0)
        {
            if (level == FanfareLevel.Capstone)
            {
                p.Broadcasts.Add(text);
            }
            else
            {
                p.Lines.Add(text);
            }
        }

        if (voiceKey != null)
        {
            p.VoiceKeys.Add(voiceKey);
        }
    }

    /// <summary>Plays every queued fanfare now (the merge timer's call; tests call it directly).</summary>
    public static void FlushAll()
    {
        foreach (var pm in new List<PlayerMobile>(_pending.Keys))
        {
            Flush(pm);
        }
    }

    public static bool HasPending(PlayerMobile pm) => _pending.ContainsKey(pm);

    public static FanfarePlayed? Flush(PlayerMobile pm)
    {
        if (!_pending.Remove(pm, out var p))
        {
            return null;
        }

        p.Timer?.Stop();

        var muted = IsMuted(pm);
        var lines = new List<string>(p.Lines);
        lines.AddRange(p.Broadcasts);
        var hue = TierHue(p.Tier);

        // The chat lines always arrive (System voice).
        foreach (var l in lines)
        {
            pm.SendMessage(hue, $"System: {l}");
        }

        string? broadcast = null;
        if (p.Broadcasts.Count > 0)
        {
            broadcast = string.Join(" ", p.Broadcasts);
            World.Broadcast(hue, false, $"System: {broadcast}");
        }

        var effects = !muted && !pm.Deleted && pm.Map != null && pm.Map != Map.Internal;
        if (effects)
        {
            PlayEffects(pm, p.Level, hue);
            foreach (var l in lines)
            {
                if (p.Level == FanfareLevel.Minor)
                {
                    if (pm.NetState != null)
                    {
                        pm.PrivateOverheadMessage(MessageType.Regular, hue, false, l, pm.NetState);
                    }
                }
                else
                {
                    pm.PublicOverheadMessage(MessageType.Regular, hue, false, l);
                }
            }

            if (VoiceHook != null)
            {
                foreach (var key in p.VoiceKeys)
                {
                    VoiceHook(pm, key);
                }
            }
        }

        var played = new FanfarePlayed(p.Level, p.Tier, lines, effects, broadcast);
        LastPlayed[pm] = played;
        return played;
    }

    // The rarity colors by tier (LootTable.Rarity), so a tier 6 fanfare is Celestial.
    public static int TierHue(int tier) => LootTable.HueOf(LootTable.Current.Rarity[Math.Clamp(tier, 1, 6) - 1]);

    private static void PlayEffects(PlayerMobile pm, FanfareLevel level, int hue)
    {
        var bursts = level switch { FanfareLevel.Minor => 1, FanfareLevel.Major => 3, _ => 6 };

        for (var i = 0; i < bursts; i++)
        {
            var at = i == 0
                ? pm.Location
                : new Point3D(pm.X + Utility.RandomMinMax(-2, 2), pm.Y + Utility.RandomMinMax(-2, 2), pm.Z + Utility.RandomMinMax(5, 15));
            var burst = 0x373A + 0x10 * Utility.Random(4);
            var sound = Utility.Random(0x11B, 4);

            if (level == FanfareLevel.Minor)
            {
                // The player's client alone: the same hued location effect pinned's Effects.SendLocationEffect builds.
                if (pm.NetState is { } ns)
                {
                    SendPrivateEffect(ns, at, burst, hue);
                    pm.SendSound(sound, pm.Location);
                }
            }
            else
            {
                Effects.SendLocationEffect(at, pm.Map, burst, 16, 10, hue, 0);
                Effects.PlaySound(at, pm.Map, sound);
            }
        }

        if (level != FanfareLevel.Minor)
        {
            // Sparkles around the player, bigger at a capstone.
            pm.FixedParticles(0x376A, 9, level == FanfareLevel.Capstone ? 64 : 32, 5030, hue, 0, EffectLayer.Waist);
        }
    }

    // The hued location effect pinned's Effects.SendLocationEffect builds, sent to one client.
    private static void SendPrivateEffect(NetState ns, Point3D at, int itemID, int hue)
    {
        Span<byte> effect = stackalloc byte[OutgoingEffectPackets.HuedEffectLength].InitializePacket();
        OutgoingEffectPackets.CreateLocationHuedEffect(effect, at, itemID, 10, 16, hue, 0);
        ns.Send(effect);
    }

    [Usage("FanfareMute")]
    [Description("Mutes or unmutes your own fanfare (its effects and overhead lines). Chat lines still arrive, and shard-wide announcements always show.")]
    [ShardCommand(CommandCategory.Player)]
    private static void OnMuteCommand(CommandEventArgs e)
    {
        if (e.Mobile is not PlayerMobile pm)
        {
            return;
        }

        var muted = !IsMuted(pm);
        SetMuted(pm, muted);
        pm.SendMessage(0x44, muted ? "Your fanfare is muted. Shard-wide announcements still show." : "Your fanfare is back on.");
    }
}

/// <summary>The fanfare line pools, from Configuration/fanfare-lines.json (approved by Chase 2026-10-11).</summary>
public static class FanfareLines
{
    public const string FileName = "fanfare-lines.json";

    public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

    public const string MinorAchievement = "minorAchievement";
    public const string MajorAchievement = "majorAchievement";
    public const string RepeatAchievement = "repeatAchievement";
    public const string BoxAwarded = "boxAwarded";
    public const string BoxRare = "boxRare";
    public const string BoxInBox = "boxInBox";
    public const string LevelUp = "levelUp";
    public const string LoopComplete = "loopComplete";
    public const string Reached200 = "reached200";

    public static readonly Dictionary<string, string[]> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        [MinorAchievement] =
        [
            "Congratulations. You did a thing.",
            "Achievement unlocked. Lower your expectations accordingly.",
            "Someone had to do it. Apparently it was you.",
            "A small step for you. An even smaller one for Britannia.",
            "I've logged it. Don't make me regret it.",
            "Well done. Please don't make it your whole personality.",
            "Noted. Filed. Mildly impressed.",
            "You walked into a tree. Repeatedly. On purpose.",
        ],
        [MajorAchievement] =
        [
            "Impressive. For you.",
            "Okay, that one was actually hard. Don't let it go to your head.",
            "I had a bet that you'd quit. I lost.",
            "The odds were against you. The odds have been informed.",
            "Britannia will remember this. Briefly.",
            "Even the monsters are talking about it. Mostly complaining.",
            "That was either skill or luck. I'm choosing luck.",
        ],
        [RepeatAchievement] =
        [
            "Again? That's [count] times now.",
            "Loop [loop]. You did this again. Of course you did.",
            "Deja vu isn't a strategy, but you're making it work.",
            "No box this time. You know why.",
            "I'd give you a reward, but you already have the memory.",
            "[count] times. At this point it's a hobby.",
            "Same achievement, same you, slightly more tired.",
        ],
        [BoxAwarded] =
        [
            "Try not to drop it.",
            "A box! Contents may vary. Wildly.",
            "Open it somewhere safe. I mean it.",
            "It's a box. Act surprised.",
            "Shake it if you want. It won't tell you anything.",
            "Earned, not bought. You're welcome.",
            "Somewhere, a goblin is jealous.",
        ],
        [BoxRare] =
        [
            "Ooh. Even I didn't expect that.",
            "Well. Look at you.",
            "That's going to make someone very jealous.",
            "The odds of that were embarrassing. For the odds.",
            "Don't sell that to a vendor. I'm begging you.",
            "I'd take a screenshot if I were you.",
            "Fine. You win this one.",
        ],
        [BoxInBox] =
        [
            "Oh, look. Another box.",
            "You're welcome.",
            "It's boxes all the way down.",
            "The real treasure was the box we found along the way.",
            "I'm not saying it's a trick. I'm saying it's a box.",
            "Open it. I dare you.",
            "Inside every box is a smaller, sadder box.",
        ],
        [LevelUp] =
        [
            "Level [level]. The bar remains low.",
            "Level [level]. Your mother would be proud. Probably.",
            "Level [level]. Keep going. Or don't. I'll be here.",
            "You've grown. Spiritually, numerically, mostly numerically.",
            "Level [level]. Somewhere, a rat is nervous.",
            "Ding. That's the official sound of progress.",
            "Level [level]. Your stats thank you. I'm neutral.",
        ],
        [LoopComplete] =
        [
            "[Player] has died and been reborn. Again. Loop [loop].",
            "[Player] has reset the clock. Loop [loop]. Everyone act natural.",
            "Loop [loop] for [Player]. Time is a flat circle, and they keep walking it.",
            "[Player] started over. Loop [loop]. Stronger, wiser, same face.",
            "Attention, Britannia. [Player] has looped. Again. Loop [loop].",
            "[Player] has seen the end and politely declined it. Loop [loop].",
            "Loop [loop]. [Player] remembers everything. That's the problem.",
        ],
        [Reached200] =
        [
            "Someone actually did it. [Player] has reached 200. Everyone act surprised.",
            "[Player] has reached the top. There is no more ladder. Only view.",
            "Attention, Britannia. [Player] has reached 200. I've run out of jokes.",
            "[Player] did everything. Over and over. As many loops as it took.",
            "200. [Player] has broken the cap, the curve and my spirit.",
        ],
    };

    public static Dictionary<string, string[]> Current { get; set; } = Defaults;

    public static string Source { get; private set; } = "built-in defaults";

    /// <summary>A random line from a pool with its placeholders filled; "" for an unknown or empty pool.</summary>
    public static string Pick(string pool, Mobile? player, FanfareVars? vars)
    {
        if (!Current.TryGetValue(pool, out var lines) || lines.Length == 0)
        {
            return "";
        }

        return Fill(lines[Utility.Random(lines.Length)], player, vars);
    }

    public static string Fill(string line, Mobile? player, FanfareVars? vars) =>
        line.Replace("[Player]", player?.Name ?? "Someone")
            .Replace("[level]", vars?.Level ?? "?")
            .Replace("[loop]", (vars?.Loop ?? 0).ToString())
            .Replace("[count]", (vars?.Count ?? 0).ToString())
            .Replace("[tier]", vars?.Tier ?? "");

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, AllowTrailingCommas = true, ReadCommentHandling = JsonCommentHandling.Skip };

    public static List<string> Apply(string json, string source)
    {
        var errors = new List<string>();
        Dictionary<string, string[]>? pools = null;
        try
        {
            pools = JsonSerializer.Deserialize<Dictionary<string, string[]>>(json, Json);
        }
        catch (JsonException ex)
        {
            errors.Add($"not valid JSON: {ex.Message}");
        }

        if (pools != null)
        {
            foreach (var name in Defaults.Keys)
            {
                if (!pools.TryGetValue(name, out var lines) || lines == null || lines.Length == 0)
                {
                    errors.Add($"pool '{name}' is missing or empty.");
                }
                else if (Array.Exists(lines, l => string.IsNullOrWhiteSpace(l) || l.Contains('[') && !KnownPlaceholders(l)))
                {
                    errors.Add($"pool '{name}': an empty line or an unknown [placeholder].");
                }
            }
        }

        if (errors.Count > 0 || pools == null)
        {
            var lines = new List<string> { $"{FileName} REFUSED ({errors.Count} error{(errors.Count == 1 ? "" : "s")}); kept {Source}:" };
            errors.ForEach(e => lines.Add($"  {e}"));
            return lines;
        }

        Current = new Dictionary<string, string[]>(pools, StringComparer.OrdinalIgnoreCase);
        Source = source;
        return [$"Loaded the fanfare lines from {source}."];
    }

    private static bool KnownPlaceholders(string line)
    {
        var stripped = line.Replace("[Player]", "").Replace("[level]", "").Replace("[loop]", "").Replace("[count]", "").Replace("[tier]", "");
        return !stripped.Contains('[');
    }

    public static List<string> LoadOrWriteDefaults(string path)
    {
        if (!File.Exists(path))
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(Defaults, Json));
                return [$"{FileName} was missing; wrote the approved pools to {path}."];
            }
            catch (Exception ex)
            {
                return [$"{FileName} is missing and could not be written ({ex.Message}); using the built-in pools."];
            }
        }

        try
        {
            return Apply(File.ReadAllText(path), path);
        }
        catch (Exception ex)
        {
            return [$"Could not read {path} ({ex.Message}); kept {Source}."];
        }
    }
}
