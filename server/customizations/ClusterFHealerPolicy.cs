using Server.Commands;

namespace Server
{
    public static class ClusterFHealerPolicy
    {
        private static bool m_Enabled;

        public static bool AllowCriminals { get; private set; }
        public static bool AllowMurderers { get; private set; }

        public static void Configure()
        {
            m_Enabled = Config.Get("clusterf.healerPolicy.enabled", true);
            AllowCriminals = Config.Get("clusterf.healerPolicy.allowCriminals", true);
            AllowMurderers = Config.Get("clusterf.healerPolicy.allowMurderers", true);

            CommandSystem.Register(
                "ClusterFHealerPolicy",
                AccessLevel.Administrator,
                ClusterFHealerPolicy_OnCommand
            );
        }

        [Usage("ClusterFHealerPolicy")]
        [Description("Reports the current ClusterF healer resurrection policy.")]
        private static void ClusterFHealerPolicy_OnCommand(CommandEventArgs e)
        {
            e.Mobile.SendMessage(
                "ClusterF healer policy - enabled: {0}, allowCriminals: {1}, allowMurderers: {2}.",
                m_Enabled,
                AllowCriminals,
                AllowMurderers
            );
        }
    }
}
