using Server.Commands;

namespace Server
{
    public static class ClusterFSelfResurrect
    {
        private static bool m_Enabled;

        public static void Configure()
        {
            m_Enabled = Config.Get("clusterf.selfResurrect.enabled", true);
            CommandSystem.Register("SelfRes", AccessLevel.Player, SelfRes_OnCommand);
        }

        [Usage("SelfRes")]
        [Description("Resurrects your ghost. Only works when you are dead.")]
        private static void SelfRes_OnCommand(CommandEventArgs e)
        {
            if (!m_Enabled)
            {
                e.Mobile.SendMessage("Self-resurrection is not available.");
                return;
            }

            Mobile mobile = e.Mobile;

            if (mobile.Alive)
            {
                mobile.SendMessage("You are not dead.");
                return;
            }

            if (mobile.Map == null || !mobile.Map.CanFit(mobile.Location, 16, false, false))
            {
                mobile.SendMessage("You cannot be resurrected here. Move to a better location and try again.");
                return;
            }

            mobile.PlaySound(0x214);
            mobile.FixedEffect(0x376A, 10, 16);
            mobile.Resurrect();
            mobile.SendMessage("You have returned from the spirit realm.");
        }
    }
}
