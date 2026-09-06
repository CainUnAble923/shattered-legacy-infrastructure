using Server.Mobiles;

namespace Server;

public static class ClusterFSelfResurrect
{
    private static bool _enabled;

    public static void Configure()
    {
        _enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.selfResurrect.enabled", true);
        CommandSystem.Register("SelfRes", AccessLevel.Player, SelfRes_OnCommand);
    }

    [Usage("SelfRes")]
    [Description("Resurrects your ghost. Only works when you are dead.")]
    private static void SelfRes_OnCommand(CommandEventArgs e)
    {
        if (!_enabled)
        {
            e.Mobile.SendMessage("Self-resurrection is not available.");
            return;
        }

        var m = e.Mobile;

        if (m.Alive)
        {
            m.SendMessage("You are not dead.");
            return;
        }

        if (m.Map?.CanFit(m.Location, 16, false, false) != true)
        {
            m.SendMessage("You cannot be resurrected here. Move to a better location and try again.");
            return;
        }

        m.PlaySound(0x214);
        m.FixedEffect(0x376A, 10, 16);
        m.Resurrect();
        m.SendMessage("You have returned from the spirit realm.");
    }
}
