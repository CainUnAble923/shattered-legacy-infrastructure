using Server.Mobiles;
using Server.Network;

namespace Server;

public static class ClusterFHealerPolicy
{
    private static bool _enabled;

    public static bool AllowCriminals { get; private set; }
    public static bool AllowMurderers { get; private set; }

    public static void Configure()
    {
        _enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.healerPolicy.enabled", true);
        AllowCriminals = ServerConfiguration.GetOrUpdateSetting("clusterf.healerPolicy.allowCriminals", true);
        AllowMurderers = ServerConfiguration.GetOrUpdateSetting("clusterf.healerPolicy.allowMurderers", true);

        CommandSystem.Register("ClusterFHealerPolicy", AccessLevel.Administrator, ClusterFHealerPolicy_OnCommand);
    }

    [Usage("ClusterFHealerPolicy")]
    [Description("Reports the current ClusterF healer resurrection policy.")]
    private static void ClusterFHealerPolicy_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage(
            $"ClusterF healer policy — enabled: {_enabled}, allowCriminals: {AllowCriminals}, allowMurderers: {AllowMurderers}."
        );
    }
}
