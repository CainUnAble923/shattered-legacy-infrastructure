// cc-P631 (P566 design, stage 1): who answers the townsfolk topics, when, and what they say. Notes: shard-migration
// notes/cc-P631-build-townsfolk-rest.md.
//
// One answer per spoken topic: among the talking residents near the player (same facet, in range, seen, in line of sight,
// standing in their profile's region) whose profile matches the words, the nearest, then the lowest serial. Speech that
// names an NPC first ("Ellis, hello") matches that NPC alone (ClusterFTalkRules.Match), so naming one picks it. Only that one replies, privately (an overhead line to the speaker's client alone), never as the System. A
// player gets one answer every 3 seconds, and the same topic from the same NPC once every 15; a throttled topic is
// silent. The topic menu (double-click or Talk) uses the same checks and the same cooldowns.
//
// Off by default (clusterf.townsfolk.enabled). The tutorial phase is read without creating anything:
// ClusterFAccountPersistence.Get, never ClusterFTutorial.RecordOf (which creates account data).

#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Server.Accounting;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Tutorial;

namespace Server.Engines.Townsfolk;

/// <summary>An NPC that answers the townsfolk topics under a profile of the table.</summary>
public interface ITalkResident
{
    string TalkProfile { get; }
}

public static class ClusterFTownsfolkService
{
    private const string Prefix = "clusterf.townsfolk.";
    public const int ReplyHue = 0x3B2;

    // -- Settings -----------------------------------------------------------------------------------------------------

    /// <summary>The test shard's switch ([ClusterFTownsfolk on|off): overrides the setting until restart. Never saved.</summary>
    public static bool? EnabledOverride { get; set; }

    public static bool SettingEnabled => ServerConfiguration.GetOrUpdateSetting(Prefix + "enabled", false);

    public static int TalkRange => ServerConfiguration.GetOrUpdateSetting(Prefix + "talkRange", 3);

    public static int TalkCooldownSeconds => ServerConfiguration.GetOrUpdateSetting(Prefix + "talkCooldownSeconds", 3);

    public static int RepeatCooldownSeconds => ServerConfiguration.GetOrUpdateSetting(Prefix + "repeatCooldownSeconds", 15);

    /// <summary>Why the talk settings are refused, or null.</summary>
    public static string? SettingsProblem(int range, int talkCooldown, int repeatCooldown) =>
        range is < 1 or > 3 ? $"talkRange {range} is outside 1-3" :
        talkCooldown < 1 ? $"talkCooldownSeconds {talkCooldown} must be at least 1" :
        repeatCooldown < 1 ? $"repeatCooldownSeconds {repeatCooldown} must be at least 1" : null;

    public static string? SettingsProblem() => SettingsProblem(TalkRange, TalkCooldownSeconds, RepeatCooldownSeconds);

    /// <summary>Talk is on: switched on, the settings sound, and a valid table loaded.</summary>
    public static bool Enabled =>
        (EnabledOverride ?? SettingEnabled) && SettingsProblem() == null && ClusterFTalkRules.Current != null;

    // -- Per-player state (never saved; gone with the mobile) ---------------------------------------------------------

    private sealed class TalkState
    {
        public DateTime NextTalk;
        public readonly Dictionary<(Serial Npc, string Topic), DateTime> NextRepeat = new();
        public readonly Dictionary<(string Profile, string Topic), string> LastLine = new();
    }

    private static readonly ConditionalWeakTable<Mobile, TalkState> _state = new();

    /// <summary>Raised for every reply sent (tests watch it): the NPC, the player, the client it went to, the line.</summary>
    public static event Action<Mobile, Mobile, NetState?, string>? Replied;

    // -- Eligibility --------------------------------------------------------------------------------------------------

    /// <summary>Whether this NPC may talk to this player now: both there, same facet, in range, seen, in sight, in its region.</summary>
    public static bool CanTalk(Mobile npc, Mobile pm)
    {
        if (npc is not ITalkResident resident || npc.Deleted || pm.Deleted || !pm.Alive || !npc.Alive || pm.Map == null ||
            pm.Map == Map.Internal || npc.Map != pm.Map)
        {
            return false;
        }

        if (!pm.InRange(npc.Location, TalkRange) || !pm.CanSee(npc) || !pm.InLOS(npc))
        {
            return false;
        }

        return ClusterFTalkRules.Current?.Profile(resident.TalkProfile) is { } profile && npc.Region.IsPartOf(profile.Region);
    }

    /// <summary>
    /// The one resident that answers this speech: of those whose profile matches the words, the nearest, then the lowest
    /// serial. A name-first phrase matches only the NPC it names, so it needs no rule of its own here.
    /// </summary>
    public static Mobile? Select(Mobile pm, string? normalized)
    {
        var table = ClusterFTalkRules.Current;
        if (table == null || string.IsNullOrEmpty(normalized) || pm.Map == null)
        {
            return null;
        }

        Mobile? best = null;
        var bestDistance = int.MaxValue;
        foreach (var m in pm.Map.GetMobilesInRange(pm.Location, TalkRange))
        {
            if (m is not ITalkResident resident || table.Profile(resident.TalkProfile) is not { } profile ||
                ClusterFTalkRules.Match(profile, normalized, m.Name) == null || !CanTalk(m, pm))
            {
                continue;
            }

            var dx = m.X - pm.X;
            var dy = m.Y - pm.Y;
            var distance = dx * dx + dy * dy;
            if (best == null || distance < bestDistance || distance == bestDistance && m.Serial < best.Serial)
            {
                best = m;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>
    /// The tutorial phase for this character, read only: Core while its own core is unfinished, AfterCore once its own
    /// core is done, Any for everyone else (another character on the account, no record, no account).
    /// </summary>
    public static TalkPhase PhaseOf(Mobile pm)
    {
        if (pm.Account is not IAccount acct || ClusterFAccountPersistence.Get(acct)?.Tutorial is not { } record ||
            record.CharacterSerial != pm.Serial.Value)
        {
            return TalkPhase.Any;
        }

        if (ClusterFTutorial.CoreDone(record))
        {
            return TalkPhase.AfterCore;
        }

        return record.State == TutorialState.InCore ? TalkPhase.Core : TalkPhase.Any;
    }

    // -- Answering ----------------------------------------------------------------------------------------------------

    /// <summary>Speech the townsfolk own (regular or whisper, from a living player). Called by each resident's OnSpeech.</summary>
    public static bool TryHandleSpeech(Mobile npc, SpeechEventArgs e)
    {
        var pm = e.Mobile;
        if (!Enabled || e.Handled || pm is not PlayerMobile || !pm.Alive ||
            e.Type is not (MessageType.Regular or MessageType.Whisper) || npc is not ITalkResident resident)
        {
            return false;
        }

        var normalized = ClusterFTalkRules.Normalize(e.Speech);
        if (ClusterFTalkRules.Current?.Profile(resident.TalkProfile) is not { } profile ||
            ClusterFTalkRules.Match(profile, normalized, npc.Name) is not { } topic || Select(pm, normalized) != npc)
        {
            return false;
        }

        e.Handled = true; // ours, even when throttled: no other resident answers it
        Answer(npc, pm, topic.Id);
        return true;
    }

    /// <summary>
    /// Says this topic to the player if the NPC may talk now and the cooldowns allow: the global 3 seconds and the
    /// 15-second repeat of this topic from this NPC. Returns the line sent, or null (silent).
    /// </summary>
    public static string? Answer(Mobile npc, Mobile pm, string topicId)
    {
        if (!Enabled || !CanTalk(npc, pm) || npc is not ITalkResident resident ||
            ClusterFTalkRules.Current?.Profile(resident.TalkProfile) is not { } profile || profile.Topic(topicId) is not { } topic)
        {
            return null;
        }

        var state = _state.GetOrCreateValue(pm);
        var now = Core.Now;
        var repeatKey = (npc.Serial, topic.Id);
        if (now < state.NextTalk || state.NextRepeat.TryGetValue(repeatKey, out var next) && now < next)
        {
            return null;
        }

        var lines = ClusterFTalkRules.ResponsesFor(topic, PhaseOf(pm));
        state.LastLine.TryGetValue((profile.Tag, topic.Id), out var last);
        var choices = new List<string>();
        foreach (var r in lines)
        {
            if (lines.Count == 1 || r.Text != last)
            {
                choices.Add(r.Text);
            }
        }

        var chosen = choices[Utility.Random(choices.Count)];
        state.LastLine[(profile.Tag, topic.Id)] = chosen;
        state.NextTalk = now + TimeSpan.FromSeconds(TalkCooldownSeconds);
        state.NextRepeat[repeatKey] = now + TimeSpan.FromSeconds(RepeatCooldownSeconds);

        var text = chosen.Replace(ClusterFTalkRules.NameToken, npc.Name ?? "");
        npc.PrivateOverheadMessage(MessageType.Regular, ReplyHue, false, text, pm.NetState);
        Replied?.Invoke(npc, pm, pm.NetState, text);
        return text;
    }

    /// <summary>Opens the topic menu if this NPC may talk to the player now. Opening it says and grants nothing.</summary>
    public static bool OpenMenu(Mobile npc, Mobile pm)
    {
        if (!Enabled || pm is not PlayerMobile player || player.NetState == null || !CanTalk(npc, pm))
        {
            return false;
        }

        player.CloseGump<TownsfolkTalkGump>();
        player.SendGump(new TownsfolkTalkGump(player, npc));
        return true;
    }

    /// <summary>Forgets a player's cooldowns (tests).</summary>
    public static void ResetPlayer(Mobile pm) => _state.Remove(pm);

    // -- Staff --------------------------------------------------------------------------------------------------------

    public static void Configure()
    {
        CommandSystem.Register("ClusterFTownsfolk", AccessLevel.Administrator, OnCommand);
    }

    [Usage("ClusterFTownsfolk [status|reload|on|off]")]
    [Description("The New Haven townsfolk talk (stage 1): shows whether it is on and the table loaded; reload re-reads Data/ShatteredLegacy/townsfolk-talk.json, keeping the old table if the new one is refused; on and off switch talk until restart.")]
    [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.TestOnly)]
    private static void OnCommand(CommandEventArgs e)
    {
        var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
        switch (verb)
        {
            case "reload":
                e.Mobile.SendMessage(ClusterFTalkRules.Reload()
                    ? "Townsfolk table reloaded."
                    : $"Townsfolk table refused; the old one stays: {string.Join("; ", ClusterFTalkRules.LastErrors)}");
                return;
            case "on":
                EnabledOverride = true;
                break;
            case "off":
                EnabledOverride = false;
                break;
        }

        var table = ClusterFTalkRules.Current;
        e.Mobile.SendMessage(
            $"Townsfolk talk: {(Enabled ? "on" : "off")}; table {(table == null ? "not loaded" : $"{table.Profiles.Count} profiles from {ClusterFTalkRules.LoadedFrom}")}" +
            (SettingsProblem() is { } p ? $"; settings refused: {p}" : "") + "."
        );
    }
}
