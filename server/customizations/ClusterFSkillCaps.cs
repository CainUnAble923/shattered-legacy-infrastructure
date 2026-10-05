using System;
using ModernUO.CodeGeneratedEvents;
using Server.Mobiles;
using Server.Network;

namespace Server;

public static class ClusterFSkillCaps
{
    // cc-P53 (D64, Chase 2026-10-04): the shard's top is 200, the 2026-10-03 ladder's loop 21. Was 300.
    public const double DefaultIndividualSkillCap = 200.0;

    // The 300 every config file already holds was written by this class's own default (GetOrUpdateSetting stores a
    // missing key once), so it is rewritten once to 200 and the marker below records that it was. A value anyone set
    // by hand (anything but 300), or any value set after the marker, is never touched.
    public const double LegacyIndividualSkillCap = 300.0;
    public const string IndividualCapKey = "clusterf.skillCaps.individualCap";
    public const string RescaledMarkerKey = "clusterf.skillCaps.rescaledTo200";
    private const int FixedPointScale = 10;

    private static bool _enabled;
    private static double _individualSkillCap;
    private static double _totalSkillCap;

    public static void Configure()
    {
        _enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.skillCaps.enabled", true);
        RescaleStoredLegacyCap();
        _individualSkillCap = ServerConfiguration.GetOrUpdateSetting(IndividualCapKey, DefaultIndividualSkillCap);
        _totalSkillCap = ServerConfiguration.GetOrUpdateSetting("clusterf.skillCaps.totalCap", 0.0);

        EventSink.WorldLoad += ApplyToLoadedPlayers;
        CommandSystem.Register("ClusterFSkillCaps", AccessLevel.Administrator, ClusterFSkillCaps_OnCommand);
    }

    [CallPriority(100)]
    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        if (_enabled)
        {
            Apply(pm);
        }
    }

    [Usage("ClusterFSkillCaps [all]")]
    [Description("Applies ClusterF skill cap policy to the caller or all online players.")]
    [ShardCommand(CommandCategory.DevTool)]
    private static void ClusterFSkillCaps_OnCommand(CommandEventArgs e)
    {
        if (!_enabled)
        {
            e.Mobile.SendMessage("ClusterF skill caps are disabled.");
            return;
        }

        if (e.Length > 0 && e.GetString(0).Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var count = 0;

            foreach (var state in NetState.Instances)
            {
                if (state.Mobile is PlayerMobile onlinePlayer)
                {
                    Apply(onlinePlayer);
                    count++;
                }
            }

            e.Mobile.SendMessage(
                $"ClusterF skill caps applied to {count} online player(s): {_individualSkillCap:0.#} per skill, {TotalSkillCap:0.#} total."
            );
            return;
        }

        if (e.Mobile is not PlayerMobile player)
        {
            e.Mobile.SendMessage("ClusterF skill caps can only be applied to player mobiles.");
            return;
        }

        Apply(player);
        e.Mobile.SendMessage(
            $"ClusterF skill caps applied: {_individualSkillCap:0.#} per skill, {TotalSkillCap:0.#} total."
        );
    }

    /// <summary>
    /// One-time: a stored individual cap of exactly the old default (300) becomes the new default (200). Returns true
    /// when it rewrote the value. Sets the marker either way, so it runs once per config file.
    /// </summary>
    public static bool RescaleStoredLegacyCap()
    {
        if (ServerConfiguration.GetSetting(RescaledMarkerKey, false))
        {
            return false;
        }

        var stored = ServerConfiguration.GetSetting(IndividualCapKey, -1.0);
        var rewrite = stored == LegacyIndividualSkillCap;

        if (rewrite)
        {
            ServerConfiguration.SetSetting(IndividualCapKey, DefaultIndividualSkillCap);
            Console.WriteLine(
                $"[ClusterFSkillCaps] {IndividualCapKey} was the old default {LegacyIndividualSkillCap:0}; now {DefaultIndividualSkillCap:0} (cc-P53)."
            );
        }

        ServerConfiguration.SetSetting(RescaledMarkerKey, true);
        return rewrite;
    }

    /// <summary>The individual cap in force (read at Configure).</summary>
    public static double IndividualSkillCap => _individualSkillCap;

    /// <summary>Re-reads the configured caps; for the test host, where Configure's registrations must not repeat.</summary>
    public static void ReloadForTests()
    {
        _enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.skillCaps.enabled", true);
        _individualSkillCap = ServerConfiguration.GetOrUpdateSetting(IndividualCapKey, DefaultIndividualSkillCap);
        _totalSkillCap = ServerConfiguration.GetOrUpdateSetting("clusterf.skillCaps.totalCap", 0.0);
    }

    public static void ApplyTo(PlayerMobile player) => Apply(player);

    public static double TotalSkillCap =>
        _totalSkillCap > 0 ? _totalSkillCap : SkillInfo.Table.Length * _individualSkillCap;

    private static int IndividualSkillCapFixedPoint =>
        Math.Clamp((int)Math.Round(_individualSkillCap * FixedPointScale), 0, ushort.MaxValue);

    private static int TotalSkillCapFixedPoint =>
        Math.Max(0, (int)Math.Round(TotalSkillCap * FixedPointScale));

    private static void ApplyToLoadedPlayers()
    {
        if (!_enabled)
        {
            return;
        }

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is PlayerMobile player)
            {
                Apply(player);
            }
        }
    }

    private static void Apply(PlayerMobile player)
    {
        var skills = player.Skills;

        if (skills == null)
        {
            return;
        }

        var individualCap = IndividualSkillCapFixedPoint;

        // By design this overwrites every skill's cap, including one a power scroll raised
        // (PowerScroll.Use sets Skills[x].Cap, pinned PowerScroll.cs:272): the scroll lasts until the
        // next login or world load (D43, cc-P23). F-12 replaces the flat value with a per-character
        // max level, and power scrolls then raise that (shard-migration notes/f12-levels-loops-caps.md).
        for (var i = 0; i < skills.Length; i++)
        {
            skills[i].CapFixedPoint = individualCap;
        }

        skills.Cap = TotalSkillCapFixedPoint;
    }
}
