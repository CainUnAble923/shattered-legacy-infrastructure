using System;
using ModernUO.CodeGeneratedEvents;
using Server.Mobiles;
using Server.Network;

namespace Server;

public static class ClusterFStatCaps
{
    private const int DefaultIndividualStatCap = 500;
    private const int DefaultTotalStatCap = 1500;

    private static bool _enabled;
    private static int _individualStatCap;
    private static int _totalStatCap;

    public static void Configure()
    {
        _enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.statCaps.enabled", true);
        _individualStatCap = ServerConfiguration.GetOrUpdateSetting(
            "clusterf.statCaps.individualCap",
            DefaultIndividualStatCap
        );
        _totalStatCap = ServerConfiguration.GetOrUpdateSetting("clusterf.statCaps.totalCap", DefaultTotalStatCap);

        EventSink.WorldLoad += ApplyToLoadedPlayers;
        CommandSystem.Register("ClusterFStatCaps", AccessLevel.Administrator, ClusterFStatCaps_OnCommand);
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

    [Usage("ClusterFStatCaps [all]")]
    [Description("Applies ClusterF stat cap policy to the caller or all online players.")]
    private static void ClusterFStatCaps_OnCommand(CommandEventArgs e)
    {
        if (!_enabled)
        {
            e.Mobile.SendMessage("ClusterF stat caps are disabled.");
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
                $"ClusterF stat caps applied to {count} online player(s): {_individualStatCap} per stat, {_totalStatCap} total."
            );
            return;
        }

        if (e.Mobile is not PlayerMobile player)
        {
            e.Mobile.SendMessage("ClusterF stat caps can only be applied to player mobiles.");
            return;
        }

        Apply(player);
        e.Mobile.SendMessage($"ClusterF stat caps applied: {_individualStatCap} per stat, {_totalStatCap} total.");
    }

    private static void ApplyToLoadedPlayers()
    {
        if (!_enabled)
        {
            return;
        }

        var count = 0;

        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile is PlayerMobile player)
            {
                Apply(player);
                count++;
            }
        }

        Console.WriteLine(
            $"ClusterF stat caps applied to {count} loaded player(s): {_individualStatCap} per stat, {_totalStatCap} total."
        );
    }

    public static int IndividualStatCap => _enabled ? _individualStatCap : 150;

    private static void Apply(PlayerMobile player)
    {
        player.StatCap = Math.Max(0, _totalStatCap);
    }
}
