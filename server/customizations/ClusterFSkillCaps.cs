using System;
using ModernUO.CodeGeneratedEvents;
using Server.Mobiles;
using Server.Network;

namespace Server;

public static class ClusterFSkillCaps
{
    private const double DefaultIndividualSkillCap = 300.0;
    private const int FixedPointScale = 10;

    private static bool _enabled;
    private static double _individualSkillCap;
    private static double _totalSkillCap;

    public static void Configure()
    {
        _enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.skillCaps.enabled", true);
        _individualSkillCap = ServerConfiguration.GetOrUpdateSetting(
            "clusterf.skillCaps.individualCap",
            DefaultIndividualSkillCap
        );
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

    private static double TotalSkillCap =>
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

        for (var i = 0; i < skills.Length; i++)
        {
            skills[i].CapFixedPoint = individualCap;
        }

        skills.Cap = TotalSkillCapFixedPoint;
    }
}
